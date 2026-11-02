import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

export const auditTypes = ['Tenant', 'Company', 'Vessel', 'VesselProfile', 'TugProfile', 'TugJob', 'TugJobRevision', 'TugAssignment', 'TugDraftPlan', 'TugDraftPlanRevision', 'TugDraftPlanTug', 'RegulatoryDraft', 'RegulatoryDraftRevision', 'RegisterDraft', 'RegisterRevision', 'FileStage', 'StoredFile'] as const
export type AuditRecord = components['schemas']['AuditRecordDto'] & {
  beforeVersion: number | null; afterVersion: number | null; beforeRevision: number | null; afterRevision: number | null;
}
export type AuditPage = components['schemas']['AuditPageDto'] & { items: AuditRecord[]; total: number; limit: number }
export type Change = { field: string; before: string | null; after: string | null; evidence: string }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const keys = (v: Record<string, unknown>, names: string[]) => Object.keys(v).length === names.length && names.every(k => Object.hasOwn(v, k))
const integer = (v: unknown, min = 0): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const nullableGuid = (v: unknown) => v === null || isGuid(v)
const revision = (v: unknown) => v === null || integer(v, 1)
function invalid(): never { throw new ApiError(502, 'invalid_audit_contract') }
export function changes(summary: string | null): Change[] {
  if (summary === null) return []
  let values: unknown
  try { values = JSON.parse(summary) } catch { return invalid() }
  if (!Array.isArray(values) || values.length > 20) return invalid()
  return values.map(v => {
    if (!object(v) || !keys(v, ['field', 'before', 'after', 'evidence']) || typeof v.field !== 'string' ||
      !['Code', 'Name', 'Kind', 'CompanyId', 'SourceVersion', 'RequiredBollardPull', 'RequiredTugCount', 'RequestOwner', 'PlannedStart', 'PlannedEnd', 'RequestedStart', 'RequestedEnd', 'GrossTonnage', 'BollardPullTonnes', 'Revision', 'ContentJson', 'State'].includes(v.field) ||
      (typeof v.evidence !== 'string' || !['ValuesWithheld', 'RevisionReference', 'Allowlisted'].includes(v.evidence))) return invalid()
    const field = v.field
    const expected = v.field === 'ContentJson' ? 'RevisionReference' : ['Code', 'Name'].includes(field) ? 'ValuesWithheld' : 'Allowlisted'
    if (v.evidence !== expected) return invalid()
    const safeValue = (x: unknown) => x === null || typeof x === 'string' && x.length <= 128 && (
      v.field === 'State' ? ['Pending', 'Receiving', 'CleanTransport', 'Abandoned', 'SyntheticUnverified', 'Draft', 'Uploaded', 'MappingDraft', 'Previewed', 'Committed'].includes(x) : ['CompanyId', 'RequestOwner'].includes(field) ? isGuid(x) : ['PlannedStart', 'PlannedEnd', 'RequestedStart', 'RequestedEnd'].includes(field) ? timestamp(x) : v.field === 'SourceVersion' ? ['poseidon-v46.3-20261030/14.02/1914-1931/TUG-FOUNDATION-01', '14.02:2316:1932-1957:v46.3'].includes(x) : v.field === 'Kind' ? ['Ship', 'Tug', 'Source', 'Requirement'].includes(x) : (v.field === 'Revision' ? /^\d+$(?![\s\S])/.test(x) : /^\d+(?:\.\d+)?$(?![\s\S])/.test(x)) && !/[\r\n]/.test(x))
    if (['Code', 'Name', 'ContentJson'].includes(field) ? v.before !== null || v.after !== null : !safeValue(v.before) || !safeValue(v.after)) return invalid()
    return v as Change
  })
}
export function decodeAuditRecord(v: unknown): AuditRecord {
  const names = ['id', 'occurredAt', 'actorReference', 'actorType', 'action', 'entityType', 'resourceId', 'vesselId', 'capabilityContext', 'roleContext', 'changeSummary', 'beforeVersion', 'afterVersion', 'beforeRevision', 'afterRevision', 'reasonStatus', 'correlationId', 'metadataStatus', 'revisionHref']
  if (!object(v) || !keys(v, names) || !isGuid(v.id) || typeof v.occurredAt !== 'string' || !timestamp(v.occurredAt) ||
    !(v.actorReference === null || isGuid(v.actorReference) || v.actorReference === 'system:seed-import') ||
    typeof v.actorType !== 'string' || !['User', 'System', 'Unavailable'].includes(v.actorType) || typeof v.action !== 'string' || !['Added', 'Modified', 'Deleted'].includes(v.action) ||
    !auditTypes.includes(v.entityType as typeof auditTypes[number]) || !nullableGuid(v.resourceId) || !nullableGuid(v.vesselId) ||
    !revision(v.beforeVersion) || (typeof v.beforeVersion === 'number' && v.beforeVersion > 4294967295) || !revision(v.afterVersion) || (typeof v.afterVersion === 'number' && v.afterVersion > 4294967295) || !revision(v.beforeRevision) || !revision(v.afterRevision) || !(v.reasonStatus === null || v.reasonStatus === 'NotRequiredForCommand') ||
    typeof v.metadataStatus !== 'string' || !['Recorded', 'HistoricalMetadataUnavailable', 'CapabilityContextUnavailable'].includes(v.metadataStatus) ||
    !(v.correlationId === null || isGuid(v.correlationId) || typeof v.correlationId === 'string' && v.correlationId.length === 32 && /^[0-9a-f]{32}$/i.test(v.correlationId)) || !(v.changeSummary === null || typeof v.changeSummary === 'string' && v.changeSummary.length <= 2048) ||
    !(v.roleContext === null || typeof v.roleContext === 'string' && v.roleContext.length <= 600 && v.roleContext.split(',').length <= 16 && v.roleContext.split(',').every(isGuid) && new Set(v.roleContext.split(',')).size === v.roleContext.split(',').length) ||
    !(v.capabilityContext === null || typeof v.capabilityContext === 'string' && v.capabilityContext.length <= 2048 && v.capabilityContext.split(',').every(p => ['import.legacy.convert', 'crew.identity.convert', 'crew.position.write', 'maintenance.observation.write', 'crew.assignment.declare', 'import.read', 'import.write', 'crew.certificate.read', 'crew.certificate.write', 'crew.profile.identity.read', 'crew.profile.identity.write', 'crew.profile.contact.read', 'crew.profile.contact.write', 'crew.profile.employment.read', 'crew.profile.employment.write', 'mode.tug', 'mode.cross', 'tug.read', 'tug.write', 'tug.plan.write', 'tug.owner.eligible', 'crew.read', 'crew.write', 'crew.identity.read', 'crew.identity.write', 'crew.history.read', 'directory.read', 'directory.write', 'workspace.search', 'regulatory.draft.read', 'regulatory.draft.write', 'audit.read', 'file.read', 'file.write', 'equipment.read', 'equipment.write', 'equipment.history.read', 'maintenance.read', 'maintenance.write', 'maintenance.history.read', 'maintenance.owner.eligible'].includes(p))) ||
    !(v.revisionHref === null || v.revisionHref === `/api/v1/audit/${v.id}/revision` && integer(v.afterRevision, 1) && isGuid(v.resourceId))) return invalid()
  changes(v.changeSummary as string | null)
  return v as AuditRecord
}
export function decodeAuditPage(v: unknown, limit: number): AuditPage {
  if (!object(v) || !keys(v, ['items', 'total', 'limit', 'nextCursor']) || !Array.isArray(v.items) || v.items.length > limit ||
    !integer(v.total) || v.total < v.items.length || v.limit !== limit || v.nextCursor !== null && v.total <= v.items.length ||
    !(v.nextCursor === null || typeof v.nextCursor === 'string' && /^\d{18}~\d{18}~[0-9a-f-]{36}$/.test(v.nextCursor) && isGuid(v.nextCursor.split('~')[2]) && v.items.length === limit)) return invalid()
  const items = v.items.map(decodeAuditRecord)
  if (new Set(items.map(x => x.id)).size !== items.length) return invalid()
  return { items, total: v.total, limit, nextCursor: v.nextCursor as string | null }
}
export const auditPage = async (query: string, signal: AbortSignal) => {
  const filters = new URLSearchParams(query)
  const allowed = ['vesselId', 'resourceId', 'entityType', 'action', 'cursor']
  if (query.length > 1024 || Array.from(filters.keys()).some(k => !allowed.includes(k) || filters.getAll(k).length !== 1)) return invalid()
  const page = decodeAuditPage(await request<unknown>(`audit?limit=25${query ? `&${query}` : ''}`, signal), 25)
  if (!filters.has('cursor') && page.items.length !== Math.min(25, page.total)) return invalid()
  if (page.items.some(row => ['vesselId', 'resourceId', 'entityType', 'action'].some(k => filters.has(k) && row[k as keyof AuditRecord] !== filters.get(k)))) return invalid()
  return page
}
export const auditDetail = async (id: string, signal: AbortSignal) => {
  if (!isGuid(id)) return invalid()
  const row = decodeAuditRecord(await request<unknown>(`audit/${id}`, signal))
  if (row.id !== id) return invalid()
  return row
}
