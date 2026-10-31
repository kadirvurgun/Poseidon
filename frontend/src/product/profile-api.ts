import { ApiError, request } from './api'
import { profileFields } from './profile-contract'
import type { Profile, ProfileCommand, ProfileHistory } from './profile-contract'

function decodeProfile(value: Profile, id: string): Profile {
  if (!value || typeof value.tenantId !== 'string' || value.vessel?.id !== id ||
    !['Ship', 'Tug'].includes(value.vessel.kind) || typeof value.vessel.canEdit !== 'boolean' ||
    typeof value.version !== 'number' || !Number.isInteger(value.version) || value.version < 0 ||
    typeof value.revision !== 'number' || !Number.isInteger(value.revision) || value.revision < 0 || !value.content ||
    profileFields.some(([key, kind]) => {
      const field = value.content[key]
      return !field || !['Missing', 'Unknown', 'Known'].includes(field.state ?? '') ||
        (field.state === 'Known' ? typeof field.value !== (kind === 'text' ? 'string' : 'number') : field.value != null)
    })) throw new ApiError(502, 'invalid_contract')
  return value
}
const path = (id: string) => `vessels/${encodeURIComponent(id)}/regulatory-profile`
export const loadProfile = async (id: string, signal: AbortSignal) => decodeProfile(await request<Profile>(path(id), signal), id)
export const loadProfileHistory = (id: string, offset: number, signal: AbortSignal) => request<ProfileHistory>(`${path(id)}/history?offset=${offset}&limit=25`, signal)
export const saveProfile = async (id: string, command: ProfileCommand, csrf: string, key: string, signal: AbortSignal) =>
  decodeProfile(await request<Profile>(path(id), signal, { method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(command) }), id)
