import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { calendar, timestamp } from './date-validation'

export type PositionRule = { tenantId: string; vesselId: string; positionId: string; code: string; rank: string | null; capacity: string | null; department: string | null; provenance: string; from: string | null; to: string | null }
export type CatalogContent = { version: string; sourceReference: string; timeZone: string; periodSemantics: string; positions: PositionRule[]; configured?: boolean }
export type CatalogEvent = { revision: number; state: 'Draft' | 'Reviewed' | 'Adopted' | 'Withdrawn'; policyVersion: string; actorId: string; roleId: string; reason: string; savedAt: string; content: CatalogContent; hash: string; priorHash: string | null }
export type Catalog = { version: number; activeRevision: number | null; hasMore: boolean; items: CatalogEvent[]; actions: { action: 'draft' | 'review' | 'adopt' | 'withdraw'; authorityId: string }[] }
export type CatalogCommand = { expectedVersion: number; authorityId: string; state: CatalogEvent['state']; reason: string; content?: CatalogContent }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const keys = (v: unknown, required: string[], optional: string[] = []): v is Record<string, unknown> => object(v) && required.every(k => Object.hasOwn(v, k)) && Object.keys(v).every(k => required.includes(k) || optional.includes(k))
const number = (v: unknown, min = 0) => Number.isSafeInteger(v) && Number(v) >= min
const text = (v: unknown, max: number) => typeof v === 'string' && v.length <= max
const nullable = (v: unknown, max: number) => v === null || text(v, max)
const invalid = (): never => { throw new ApiError(502, 'invalid_crew_catalog_contract') }
export function decodeCatalog(value: unknown, vessel: string): Catalog {
  if (!keys(value, ['version', 'activeRevision', 'hasMore', 'items', 'actions']) || !number(value.version) || Number(value.version) > 4294967295 ||
    value.activeRevision !== null && !number(value.activeRevision, 1) || typeof value.hasMore !== 'boolean' || !Array.isArray(value.items) || value.items.length > 25 ||
    !Array.isArray(value.actions) || value.actions.length > 4 || value.actions.some(a => !keys(a, ['action', 'authorityId']) || !['draft', 'review', 'adopt', 'withdraw'].includes(String(a.action)) || !isGuid(a.authorityId)) ||
    new Set(value.actions.map(a => a.action)).size !== value.actions.length) return invalid()
  let previous = Number.MAX_SAFE_INTEGER
  for (const e of value.items) {
    if (!keys(e, ['revision', 'state', 'policyVersion', 'actorId', 'roleId', 'reason', 'savedAt', 'content', 'hash', 'priorHash']) ||
      !number(e.revision, 1) || Number(e.revision) >= previous || !['Draft', 'Reviewed', 'Adopted', 'Withdrawn'].includes(String(e.state)) ||
      !text(e.policyVersion, 80) || !isGuid(e.actorId) || !isGuid(e.roleId) || !text(e.reason, 1000) || !timestamp(e.savedAt) ||
      typeof e.hash !== 'string' || !/^[A-F0-9]{64}$/.test(e.hash) || e.priorHash !== null && (typeof e.priorHash !== 'string' || !/^[A-F0-9]{64}$/.test(e.priorHash))) return invalid()
    const c = e.content
    if (!keys(c, ['version', 'sourceReference', 'timeZone', 'periodSemantics', 'positions'], ['configured']) || !text(c.version, 80) || c.version !== e.policyVersion ||
      !text(c.sourceReference, 512) || !text(c.timeZone, 128) || !text(c.periodSemantics, 80) || !Array.isArray(c.positions) || c.positions.length > 200 ||
      Object.hasOwn(c, 'configured') && typeof c.configured !== 'boolean') return invalid()
    for (const p of c.positions) {
      if (!keys(p, ['tenantId', 'vesselId', 'positionId', 'code', 'rank', 'capacity', 'department', 'provenance', 'from', 'to']) ||
        !isGuid(p.tenantId) || p.vesselId !== vessel || !isGuid(p.positionId) || !text(p.code, 64) || !text(p.provenance, 32) ||
        !nullable(p.rank, 100) || !nullable(p.capacity, 100) || !nullable(p.department, 100) || p.from !== null && !calendar(p.from) || p.to !== null && !calendar(p.to)) return invalid()
    }
    previous = Number(e.revision)
  }
  return value as Catalog
}
const root = (vessel: string) => { if (!isGuid(vessel)) return invalid(); return `vessels/${vessel}/crew/position-catalog` }
export async function readCatalog(vessel: string, signal: AbortSignal, before?: number) {
  return decodeCatalog(await request<unknown>(root(vessel) + (before === undefined ? '' : `?before=${before}`), signal), vessel)
}
export async function saveCatalog(vessel: string, command: CatalogCommand, key: string, csrf: string, signal: AbortSignal) {
  const value = await request<unknown>(`${root(vessel)}/events`, signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(command) })
  if (!keys(value, ['revision', 'version', 'replayed']) || !number(value.revision, 1) || !number(value.version, 1) || Number(value.version) > 4294967295 || typeof value.replayed !== 'boolean') return invalid()
}
