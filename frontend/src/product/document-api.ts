import { ApiError, request } from './api'
import { isGuid } from './identifiers'

// Generated OpenAPI refresh is deliberately deferred to the combined gate.
export type DocumentDraft = { id: string; vesselId: string; revision: number; version: number; title: string | null; status: 'DraftUnverified'; provenance: 'DeclaredUnverified' | 'SyntheticUnverified' }
export type DocumentPage = { items: DocumentDraft[]; canWrite: boolean; offset: number; hasMore: boolean }
const bad = (): never => { throw new ApiError(502, 'invalid_document_contract') }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const integer = (v: unknown, min: number): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
export function decodeDocument(v: unknown, vessel: string): DocumentDraft {
  if (!object(v) || Object.keys(v).length !== 8 || !isGuid(v.id) || v.vesselId !== vessel || !integer(v.revision, 1) || !integer(v.version, 1) || v.version > 4294967295 ||
    v.title !== null && (typeof v.title !== 'string' || v.title.length > 200) || v.status !== 'DraftUnverified' || !['DeclaredUnverified', 'SyntheticUnverified'].includes(String(v.provenance))) return bad()
  return v as DocumentDraft
}
export async function documents(vessel: string, offset: number, signal: AbortSignal): Promise<DocumentPage> {
  if (!isGuid(vessel) || !integer(offset, 0)) return bad()
  const v = await request<unknown>(`vessels/${vessel}/documents?offset=${offset}`, signal)
  if (!object(v) || Object.keys(v).length !== 4 || !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset || typeof v.canWrite !== 'boolean' || typeof v.hasMore !== 'boolean') return bad()
  const items = v.items.map(x => decodeDocument(x, vessel))
  if (new Set(items.map(x => x.id)).size !== items.length || v.hasMore && items.length !== 25) return bad()
  return { items, canWrite: v.canWrite, offset, hasMore: v.hasMore }
}
export async function saveDocument(vessel: string, title: string, key: string, csrf: string, signal: AbortSignal, current?: DocumentDraft) {
  if (!isGuid(vessel) || !isGuid(key) || current && (!isGuid(current.id) || current.vesselId !== vessel)) return bad()
  return decodeDocument(await request<unknown>(`vessels/${vessel}/documents${current ? `/${current.id}/versions` : ''}`, signal, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key, 'X-CSRF-TOKEN': csrf, ...(current ? { 'If-Match': `"${current.version}"` } : {}) }, body: JSON.stringify({ title: title || null }),
  }), vessel)
}
export async function documentRevision(vessel: string, id: string, revision: number, signal: AbortSignal) {
  if (!isGuid(vessel) || !isGuid(id) || !integer(revision, 1)) return bad()
  const row = decodeDocument(await request<unknown>(`vessels/${vessel}/documents/${id}/versions/${revision}`, signal), vessel)
  if (row.id !== id || row.revision !== revision) return bad()
  return row
}
