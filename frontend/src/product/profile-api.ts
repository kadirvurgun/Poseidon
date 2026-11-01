import { isGuid as guid } from './identifiers'
import { calendar, timestamp } from './date-validation'
import { ApiError, request } from './api'
import { profileFields } from './profile-contract'
import type { Profile, ProfileCommand, ProfileHistory } from './profile-contract'

// All untrusted JSON is checked before components see it.
type ObjectValue = Record<string, unknown>
const object = (v: unknown): v is ObjectValue => !!v && typeof v === 'object' && !Array.isArray(v)
const integer = (v: unknown, min: number, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const text = (v: unknown, max = 500): v is string => typeof v === 'string' && v.length > 0 && v.length <= max && v.trim() === v && !Array.from(v).some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))
const provenance = (v: unknown) => typeof v === 'string' && ['Synthetic', 'Missing', 'Unknown', 'DeclaredUnverified'].includes(v)

export function validDecimal(v: unknown): v is string {
  if (typeof v !== 'string' || v.length > 59 || v.trim() !== v || !/^[0-9]+(?:\.[0-9]+)?$/.test(v)) return false
  const [whole, rawFraction = ''] = v.split('.')
  const fraction = rawFraction.replace(/0+$/, '')
  const coefficient = (whole + fraction).replace(/^0+/, '')
  const max = '79228162514264337593543950335'
  return fraction.length <= 28 && (coefficient.length < max.length || (coefficient.length === max.length && coefficient <= max))
}
function validContent(v: unknown): boolean {
  return object(v) && Object.keys(v).length === profileFields.length && profileFields.every(([key, kind]) => {
    const field = v[key]
    if (!object(field) || Object.keys(field).some(k => k !== 'state' && k !== 'value')) return false
    if (field.state === 'Missing' || field.state === 'Unknown') return field.value === null
    if (field.state !== 'Known') return false
    if (kind === 'number') return validDecimal(field.value)
    if (!text(field.value)) return false
    if (key === 'imo') return /^[0-9]{7}$/.test(field.value)
    if (key === 'buildDate') return calendar(field.value)
    if (key === 'sourceObservedAt') return timestamp(field.value)
    return true
  })
}
function decodeProfile(value: unknown, id: string): Profile {
  if (!object(value) || !guid(value.tenantId) || !object(value.vessel) || !guid(value.vessel.id) || value.vessel.id !== id ||
    !guid(value.vessel.companyId) || !text(value.vessel.code, 64) || !text(value.vessel.name, 200) ||
    (value.vessel.kind !== 'Ship' && value.vessel.kind !== 'Tug') || typeof value.vessel.canEdit !== 'boolean' || !integer(value.vessel.version, 1, 4294967295) ||
    !integer(value.version, 0, 4294967295) || !integer(value.revision, 0) || !provenance(value.provenance) || !validContent(value.content) ||
    (value.revision === 0 ? value.version !== 0 || value.savedAt !== null || value.provenance !== 'Missing' : value.version === 0 || !timestamp(value.savedAt))) throw new ApiError(502, 'invalid_contract')
  return value as Profile
}
function decodeHistory(value: unknown, offset: number): ProfileHistory {
  if (!object(value) || !integer(value.offset, 0, 100000) || value.offset !== offset || value.limit !== 25 ||
    typeof value.hasMore !== 'boolean' || !Array.isArray(value.items) || value.items.length > 25 ||
    (value.hasMore && value.items.length !== 25)) throw new ApiError(502, 'invalid_contract')
  let previous = Infinity
  for (const row of value.items) {
    if (!object(row) || !integer(row.revision, 1) || row.revision >= previous || !timestamp(row.savedAt) ||
      !provenance(row.provenance) || !validContent(row.content)) throw new ApiError(502, 'invalid_contract')
    previous = row.revision
  }
  return value as ProfileHistory
}
async function response(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  try { return await request<unknown>(path, signal, init) }
  catch (error) { if (error instanceof SyntaxError) throw new ApiError(502, 'invalid_contract'); throw error }
}
const path = (id: string) => `vessels/${encodeURIComponent(id)}/regulatory-profile`
export const loadProfile = async (id: string, signal: AbortSignal) => decodeProfile(await response(path(id), signal), id)
export const loadProfileHistory = async (id: string, offset: number, signal: AbortSignal) => decodeHistory(await response(`${path(id)}/history?offset=${offset}&limit=25`, signal), offset)
export const saveProfile = async (id: string, command: ProfileCommand, csrf: string, key: string, signal: AbortSignal) =>
  decodeProfile(await response(path(id), signal, { method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(command) }), id)
