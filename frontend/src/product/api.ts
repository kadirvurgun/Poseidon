import type { Company, Vessel, Page, Session, CompanyCommand, VesselCreateCommand } from './directory-contract'
export type { Company, Vessel, Page, Session, DirectoryRecord } from './directory-contract'

export class ApiError extends Error {
  constructor(public status: number, public code = 'request_failed', public errors: Record<string, string[]> = {}, public correlationId?: string) {
    super(`Request failed (${status})`)
  }
}
async function request<T>(path: string, signal: AbortSignal, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/v1/${path}`, { credentials: 'same-origin', signal, ...init, headers: { Accept: 'application/json', ...init?.headers } })
  if (!response.ok) {
    let body: { code?: string; errors?: Record<string, string[]>; correlationId?: string } = {}
    try { body = await response.json() } catch { /* Proxy errors need not be JSON. */ }
    throw new ApiError(response.status, body.code, body.errors, body.correlationId)
  }
  return response.json() as Promise<T>
}
function decodeCompany(value: unknown): Company {
  if (!value || typeof value !== 'object' || !('id' in value) || !('code' in value) || !('name' in value) || !('version' in value) || !('canEdit' in value) ||
      typeof value.id !== 'string' || typeof value.code !== 'string' || typeof value.name !== 'string' || typeof value.version !== 'number' ||
      !Number.isInteger(value.version) || value.version < 1 || typeof value.canEdit !== 'boolean') throw new ApiError(502, 'invalid_contract')
  return { id: value.id, code: value.code, name: value.name, version: value.version, canEdit: value.canEdit }
}
function decodeVessel(value: unknown): Vessel {
  if (!value || typeof value !== 'object' || !('kind' in value) || !('id' in value) || !('companyId' in value) || !('code' in value) || !('name' in value) ||
      !('version' in value) || !('canEdit' in value) || typeof value.version !== 'number' || !Number.isInteger(value.version) || value.version < 1 || typeof value.canEdit !== 'boolean' ||
      typeof value.id !== 'string' || typeof value.companyId !== 'string' || typeof value.code !== 'string' || typeof value.name !== 'string' ||
      (value.kind !== 'Ship' && value.kind !== 'Tug')) throw new ApiError(502, 'invalid_contract')
  return { id: value.id, companyId: value.companyId, code: value.code, name: value.name, kind: value.kind, version: value.version, canEdit: value.canEdit }
}
function decodeVessels(value: unknown): Page<Vessel> {
  if (!value || typeof value !== 'object' || !('items' in value) || !Array.isArray(value.items) || !('offset' in value) || !('limit' in value) || !('hasMore' in value) ||
      !(typeof value.offset === 'number' || typeof value.offset === 'string') || !(typeof value.limit === 'number' || typeof value.limit === 'string') || typeof value.hasMore !== 'boolean') throw new ApiError(502)
  return { offset: value.offset, limit: value.limit, hasMore: value.hasMore, items: value.items.map(decodeVessel) }
}
export const session = (signal: AbortSignal) => request<Session>('auth/session', signal)
export const companies = async (offset: number, signal: AbortSignal, search = '') => {
  const page = await request<Page<unknown>>(`companies?offset=${offset}&limit=25${search ? `&q=${encodeURIComponent(search)}` : ''}`, signal)
  if (!page || !Array.isArray(page.items) || typeof page.hasMore !== 'boolean') throw new ApiError(502, 'invalid_contract')
  return { ...page, items: page.items.map(decodeCompany) }
}
export const vessels = async (offset: number, kind: string, signal: AbortSignal, search = '') => decodeVessels(await request<unknown>(`vessels?offset=${offset}&limit=25${kind ? `&kind=${encodeURIComponent(kind)}` : ''}${search ? `&q=${encodeURIComponent(search)}` : ''}`, signal))
export const company = async (id: string, signal: AbortSignal) => decodeCompany(await request<unknown>(`companies/${encodeURIComponent(id)}`, signal))
export const vessel = async (id: string, signal: AbortSignal) => decodeVessel(await request<unknown>(`vessels/${encodeURIComponent(id)}`, signal))
export async function saveDirectory(resource: 'companies' | 'vessels', id: string | undefined, body: CompanyCommand | VesselCreateCommand, csrf: string, key: string, signal: AbortSignal): Promise<Company | Vessel> {
  const result = await request<Company | Vessel>(`${resource}${id ? `/${encodeURIComponent(id)}` : ''}`, signal, {
    method: id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(body),
  })
  return resource === 'vessels' ? decodeVessel(result) : decodeCompany(result)
}
