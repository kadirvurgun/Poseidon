import { ApiError, request } from './api'
import type { components } from '../generated/api'

// Refine actual generated DTOs only after validating the supported runtime states.
type Schemas = components['schemas']
export type Field = Schemas['TextInput'] & { state: 'Missing' | 'Unknown' | 'Known'; value: string | null }
export const fieldBounds = { title: 200, authority: 200, locator: 1000, instrument: 200, amendment: 200, effectiveDate: 10, declaration: 1000, observedAt: 40 } as const
export type FieldKey = keyof typeof fieldBounds
export type RegisterContent = Required<{ [K in keyof Schemas['RegisterContent']]: Field }>
export type Revision = Schemas['RegisterRevisionDto'] & { kind: 'Source' | 'Requirement'; revision: number; provenance: 'DeclaredUnverified' | 'SyntheticUnverified'; content: RegisterContent; sourceRevision: number | null; lifecycle: 'DRAFT' }
export type Draft = Schemas['RegisterDto'] & { version: number; current: Revision }
export type RegisterPage<T> = { items: T[]; offset: number; limit: number; hasMore: boolean }
export type RegisterKind = 'sources' | 'requirements'
export type Command = Schemas['RegisterCommand'] & { expectedVersion: number; content: RegisterContent; sourceId: string | null; sourceRevision: number | null }
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i
const positive = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const record = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const invalid = (): never => { throw new ApiError(502, 'invalid_contract') }
export const blankContent = (): RegisterContent => Object.fromEntries(Object.keys(fieldBounds).map(key => [key, { state: 'Missing', value: null }])) as RegisterContent
export function validContent(value: unknown): value is RegisterContent {
  if (!record(value) || Object.keys(value).length !== Object.keys(fieldBounds).length) return false
  for (const [key, bound] of Object.entries(fieldBounds)) {
    const f = value[key]
    if (!record(f) || !['Missing', 'Unknown', 'Known'].includes(String(f.state)) ||
      (f.state === 'Known' ? typeof f.value !== 'string' || !f.value.trim() || f.value.length > bound || Array.from(f.value).some(c => c.charCodeAt(0) < 32 || c.charCodeAt(0) === 127) : f.value !== null)) return false
  }
  const c = value as RegisterContent
  if (c.effectiveDate.state === 'Known' && (!/^\d{4}-\d{2}-\d{2}$/.test(c.effectiveDate.value!) || Number.isNaN(Date.parse(c.effectiveDate.value!)) || new Date(c.effectiveDate.value!).toISOString().slice(0, 10) !== c.effectiveDate.value)) return false
  if (c.observedAt.state === 'Known' && (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$/.test(c.observedAt.value!) || Number.isNaN(Date.parse(c.observedAt.value!)))) return false
  return true
}
export function decodeRevision(value: unknown, kind: RegisterKind): Revision {
  if (!record(value) || typeof value.id !== 'string' || !uuid.test(value.id) || value.kind !== (kind === 'sources' ? 'Source' : 'Requirement') ||
    !positive(value.revision) || typeof value.savedAt !== 'string' || !/Z$|[+-]\d{2}:\d{2}$/.test(value.savedAt) || Number.isNaN(Date.parse(value.savedAt)) ||
    !['DeclaredUnverified', 'SyntheticUnverified'].includes(String(value.provenance)) || typeof value.hash !== 'string' || !/^[A-F0-9]{64}$/.test(value.hash) ||
    value.lifecycle !== 'DRAFT' || !validContent(value.content) ||
    !(value.sourceId === null && value.sourceRevision === null || kind === 'requirements' && typeof value.sourceId === 'string' && uuid.test(value.sourceId) && positive(value.sourceRevision))) return invalid()
  const content = value.content
  if (kind === 'sources' ? content.instrument.state !== 'Missing' : [content.title, content.authority, content.locator].some(f => f.state !== 'Missing')) return invalid()
  return value as Revision
}
export function decodeDraft(value: unknown, kind: RegisterKind): Draft {
  if (!record(value) || !positive(value.version) || (value.version as number) > 4294967295 || typeof value.canEdit !== 'boolean') return invalid()
  return { version: value.version as number, canEdit: value.canEdit, current: decodeRevision(value.current, kind) }
}
function page<T>(value: unknown, decode: (v: unknown) => T): RegisterPage<T> {
  if (!record(value) || !Array.isArray(value.items) || value.items.length > 25 || typeof value.offset !== 'number' || !Number.isSafeInteger(value.offset) || value.offset < 0 || value.limit !== 25 || typeof value.hasMore !== 'boolean') return invalid()
  return { items: value.items.map(decode), offset: value.offset, limit: 25, hasMore: value.hasMore }
}
const path = (kind: RegisterKind) => kind === 'sources' ? 'source-drafts' : 'requirement-drafts'
export const listDrafts = async (kind: RegisterKind, q: string, offset: number, signal: AbortSignal) => page(await request<unknown>(`${path(kind)}?q=${encodeURIComponent(q)}&offset=${offset}&limit=25`, signal), v => decodeDraft(v, kind))
export const readDraft = async (kind: RegisterKind, id: string, signal: AbortSignal) => {
  const value = decodeDraft(await request<unknown>(`${path(kind)}/${encodeURIComponent(id)}`, signal), kind)
  if (value.current.id.toLowerCase() !== id.toLowerCase()) return invalid()
  return value
}
export const historyDraft = async (kind: RegisterKind, id: string, offset: number, signal: AbortSignal) => {
  const result = page(await request<unknown>(`${path(kind)}/${encodeURIComponent(id)}/history?offset=${offset}&limit=25`, signal), v => decodeRevision(v, kind))
  if (result.items.some(v => v.id.toLowerCase() !== id.toLowerCase())) return invalid()
  return result
}
export const saveDraft = async (kind: RegisterKind, id: string | null, body: string, csrf: string, key: string, signal: AbortSignal) => {
  const value = decodeDraft(await request<unknown>(`${path(kind)}${id ? '/' + encodeURIComponent(id) : ''}`, signal, {
    method: id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body,
  }), kind)
  if (id && value.current.id.toLowerCase() !== id.toLowerCase()) return invalid()
  return value
}
