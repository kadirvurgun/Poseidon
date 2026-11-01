import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import type { CrewFileVersion } from './crew-files-api'

export type CrewPhoto = { personId: string; version: number | null; revision: number; policyVersion: string; purpose: string;
  canWrite: boolean; state: 'Missing' | 'Unavailable' | 'SelectedUnverified'; selected: CrewFileVersion | null }
export type PhotoAttempt = { key: string; command: { baseVersion: number | null; fileId: string | null; policyVersion: string; purpose: string } }
const bad = (): never => { throw new ApiError(502, 'invalid_person_photo_contract') }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const integer = (v: unknown, min: number): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const token = (v: unknown): v is string => typeof v === 'string' && /^[A-Za-z0-9_.-]{1,80}$/.test(v)
const route = (vessel: string, assignment: string) => {
  if (![vessel, assignment].every(isGuid)) return bad()
  return `vessels/${vessel}/crew/${assignment}/photo`
}
export async function crewPhoto(vessel: string, assignment: string, person: string, signal: AbortSignal): Promise<CrewPhoto> {
  if (!isGuid(person)) return bad()
  const v = await request<unknown>(route(vessel, assignment), signal)
  if (!object(v) || v.personId !== person || !(v.version === null || integer(v.version, 1) && v.version <= 4294967295) ||
    !integer(v.revision, 0) || (v.version === null) !== (v.revision === 0) || !token(v.policyVersion) || !token(v.purpose) ||
    typeof v.canWrite !== 'boolean' || !['Missing', 'Unavailable', 'SelectedUnverified'].includes(String(v.state))) return bad()
  if (v.state === 'SelectedUnverified') {
    const f = v.selected
    if (!object(f) || !isGuid(f.id) || f.personId !== person || f.kind !== 'Photo' || !integer(f.revision, 1) || f.expiresOn !== null ||
      f.policyVersion !== v.policyVersion || f.purpose !== v.purpose || f.sourceVersion !== 'poseidon-v46.3-20261030' ||
      f.state !== 'CleanTransport' || typeof f.createdAt !== 'string' || !Number.isFinite(Date.parse(f.createdAt)) ||
      !/(Z|[+-]\d{2}:\d{2})$/.test(f.createdAt)) return bad()
  } else if (v.selected !== null) return bad()
  return v as CrewPhoto
}
export async function saveCrewPhoto(vessel: string, assignment: string, person: string, attempt: PhotoAttempt, csrf: string, signal: AbortSignal) {
  const v = await request<unknown>(route(vessel, assignment), signal, { method: 'PUT',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify(attempt.command) })
  if (!object(v) || v.id !== person || !integer(v.revision, 1) || !integer(v.version, 1) || v.version > 4294967295 || typeof v.replayed !== 'boolean') return bad()
}
