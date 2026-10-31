import type { components } from '../generated/api'

export type Session = components['schemas']['SessionDto']
export type Company = components['schemas']['CompanyDto']
export type Vessel = components['schemas']['VesselDto']
export type Page<T> = Omit<components['schemas']['PageDtoOfCompanyDto'], 'items'> & { items: T[] }

export class ApiError extends Error {
  constructor(public status: number) { super(`Request failed (${status})`) }
}
async function get<T>(path: string, signal: AbortSignal): Promise<T> {
  const response = await fetch(`/api/v1/${path}`, { credentials: 'same-origin', signal, headers: { Accept: 'application/json' } })
  if (!response.ok) throw new ApiError(response.status)
  return response.json() as Promise<T>
}

function decodeVessel(value: unknown): Vessel {
  if (!value || typeof value !== 'object' || !('kind' in value) || !('id' in value) || !('companyId' in value) || !('code' in value) || !('name' in value) ||
      typeof value.id !== 'string' || typeof value.companyId !== 'string' || typeof value.code !== 'string' || typeof value.name !== 'string' ||
      (value.kind !== 'Ship' && value.kind !== 'Tug')) {
    throw new ApiError(502)
  }
  return { id: value.id, companyId: value.companyId, code: value.code, name: value.name, kind: value.kind }
}
function decodeVessels(value: unknown): Page<Vessel> {
  if (!value || typeof value !== 'object' || !('items' in value) || !Array.isArray(value.items) || !('offset' in value) || !('limit' in value) || !('hasMore' in value) ||
      !(typeof value.offset === 'number' || typeof value.offset === 'string') || !(typeof value.limit === 'number' || typeof value.limit === 'string') || typeof value.hasMore !== 'boolean') throw new ApiError(502)
  return { offset: value.offset, limit: value.limit, hasMore: value.hasMore, items: value.items.map(decodeVessel) }
}

export const session = (signal: AbortSignal) => get<Session>('auth/session', signal)
export const companies = (offset: number, signal: AbortSignal) => get<Page<Company>>(`companies?offset=${offset}&limit=25`, signal)
export const vessels = async (offset: number, kind: string, signal: AbortSignal) => decodeVessels(await get<unknown>(`vessels?offset=${offset}&limit=25${kind ? `&kind=${kind}` : ''}`, signal))
export const company = (id: string, signal: AbortSignal) => get<Company>(`companies/${encodeURIComponent(id)}`, signal)
export const vessel = async (id: string, signal: AbortSignal) => decodeVessel(await get<unknown>(`vessels/${encodeURIComponent(id)}`, signal))
