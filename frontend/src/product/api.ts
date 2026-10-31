// Explicit foundation consumer contract. Regenerate OpenAPI types at the integrated gate.
export interface Session { authenticated: boolean; setupRequired: boolean; actorId: string | null; tenantId: string | null; tenantName?: string | null; canRead: boolean; csrfToken: string }
export interface Company { id: string; code: string; name: string }
export interface Vessel extends Company { companyId: string; kind: 'Ship' | 'Tug' }
export interface Page<T> { items: T[]; offset: number; limit: number; hasMore: boolean }
export class ApiError extends Error {
  constructor(public status: number) { super(`Request failed (${status})`) }
}
async function get<T>(path: string, signal: AbortSignal): Promise<T> {
  const response = await fetch(`/api/v1/${path}`, { credentials: 'same-origin', signal, headers: { Accept: 'application/json' } })
  if (!response.ok) throw new ApiError(response.status)
  return response.json() as Promise<T>
}
export const session = (signal: AbortSignal) => get<Session>('auth/session', signal)
export const companies = (offset: number, signal: AbortSignal) => get<Page<Company>>(`companies?offset=${offset}&limit=25`, signal)
export const vessels = (offset: number, kind: string, signal: AbortSignal) => get<Page<Vessel>>(`vessels?offset=${offset}&limit=25${kind ? `&kind=${kind}` : ''}`, signal)
export const company = (id: string, signal: AbortSignal) => get<Company>(`companies/${encodeURIComponent(id)}`, signal)
export const vessel = (id: string, signal: AbortSignal) => get<Vessel>(`vessels/${encodeURIComponent(id)}`, signal)
