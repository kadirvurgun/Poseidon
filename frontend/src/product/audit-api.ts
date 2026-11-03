import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { operationReasons as declaredOperationReasons, reasonCodes, scopeKinds } from './audit-command-vocabulary.json'

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
const operationReasons = new Map<string, string>(Object.entries(declaredOperationReasons))
const contextFields = new Set(['CommandName', 'TechnicalReasonCode', 'ScopeKind'])
const guidFields = new Set(['CompanyId', 'RequestOwner', 'AuthorityId', 'RuleId', 'AttestationId', 'UpstreamId'])
const timeFields = new Set(['PlannedStart', 'PlannedEnd', 'RequestedStart', 'RequestedEnd'])
const withheldFields = new Set(['Code', 'Name', 'Reason'])
const numericFields = new Set(['RequiredBollardPull', 'RequiredTugCount', 'GrossTonnage', 'BollardPullTonnes', 'Revision',
  'ActiveRevision', 'RuleRevision', 'QualificationRevision', 'ServerVersion'])
const enumFields: Record<string, readonly string[]> = {
  ScopeKind: scopeKinds,
  TechnicalReasonCode: reasonCodes,
  State: ['Draft', 'Uploaded', 'MappingDraft', 'Previewed', 'Committed', 'Pending', 'Receiving', 'CleanTransport', 'Abandoned',
    'SyntheticUnverified', 'Reviewed', 'Adopted', 'Withdrawn', 'DRAFT', 'IN_REVIEW', 'ADOPTED', 'AWAITING', 'FUTURE_EFFECTIVE',
    'IN_FORCE', 'SUPERSEDED', 'PENDING', 'VERIFIED', 'REJECTED', 'EXPIRED', 'RECORDED'],
  Kind: ['Ship', 'Tug', 'Source', 'Requirement', 'Control', 'Evidence', 'Verification', 'Decision', 'Meter', 'Condition'],
  SourceVersion: ['poseidon-v46.3-20261030/14.02/1914-1931/TUG-FOUNDATION-01', '14.02:2316:1932-1957:v46.3'],
  Lifecycle: ['DRAFT', 'SUBMITTED', 'COMPLETED', 'Enrolled', 'KeyEnrolled', 'Renewed', 'Revoked', 'KeyLost', 'CleanupAllowed'],
  Outcome: ['Committed', 'MaterialConflict', 'PreviewRecorded', 'EvaluationRecorded', 'valid', 'rejected'],
  Binding: ['ServerOwnedContext', 'OwnerReference', 'ProtectedStorage', 'LegacyStorage', 'CommittedResource', 'TenantPersonRecord', 'PositionReference', 'JobReference'],
  ReasonCode: ['INITIAL_AUTHORING', 'AUTHORING_CORRECTION', 'SYNTHETIC_INCOMPLETE_DRAFT', 'SUBMIT', 'COMPLETE',
    'REQUEST_REVIEW', 'HUMAN_VERIFIED', 'HUMAN_REJECTED', 'VALIDITY_ELAPSED', 'INPUT_SUPERSEDED', 'HUMAN_DECIDED'],
  ReviewStatus: ['NOT_REQUESTED', 'BLOCKED_SOURCE_ADMISSION'],
}
const changeFields = new Set([...withheldFields, ...numericFields, ...guidFields, ...timeFields,
  ...Object.keys(enumFields), 'ContentJson', 'CommandName'])
function safeChangeValue(field: string, value: unknown): boolean {
  if (value === null) return true
  if (typeof value !== 'string' || value.length > 128 || /[\r\n\u2028\u2029]/.test(value)) return false
  if (field === 'CommandName') return operationReasons.has(value)
  if (guidFields.has(field)) return isGuid(value)
  if (timeFields.has(field)) return timestamp(value)
  if (enumFields[field]) return enumFields[field].includes(value)
  if (numericFields.has(field)) return (field.endsWith('Revision') || field === 'Revision' || field === 'ServerVersion'
    ? /^\d+$(?![\s\S])/.test(value) : /^\d+(?:\.\d+)?$(?![\s\S])/.test(value))
  return false
}
// Metadata emitted by ActorResolver's closed permission filter through
// TrustedActor.AuditCapabilities (FoundationIdentity.cs), not client authority.
const auditCapabilities = new Set([
  'crew.catalog.govern', 'crew.assessment.read', 'regulatory.qualify', 'regulatory.facts.attest',
  'human.trace.read', 'human.review.read', 'human.review.submit', 'human.verify', 'human.decide',
  'control.read', 'control.write', 'control.owner.eligible', 'evidence.read', 'evidence.write',
  'applicability.read', 'applicability.evaluate', 'rules.write', 'rules.review',
  'import.legacy.convert', 'crew.identity.convert', 'crew.documents.read', 'crew.documents.write',
  'crew.position.write', 'maintenance.observation.write', 'crew.assignment.declare', 'import.read', 'import.write',
  'crew.certificate.read', 'crew.certificate.write', 'crew.profile.identity.read', 'crew.profile.identity.write',
  'crew.profile.contact.read', 'crew.profile.contact.write', 'crew.profile.medical.read', 'crew.profile.medical.write',
  'crew.profile.employment.read', 'crew.profile.employment.write', 'mode.tug', 'mode.cross',
  'tug.read', 'tug.write', 'tug.plan.write', 'tug.owner.eligible', 'crew.read', 'crew.write',
  'crew.identity.read', 'crew.identity.write', 'crew.history.read', 'directory.read', 'directory.write',
  'workspace.search', 'regulatory.draft.read', 'regulatory.draft.write', 'audit.read', 'file.read', 'file.write',
  'equipment.read', 'equipment.write', 'equipment.history.read', 'maintenance.read', 'maintenance.write',
  'maintenance.history.read', 'maintenance.owner.eligible',
])
function invalid(): never { throw new ApiError(502, 'invalid_audit_contract') }
export function changes(summary: string | null): Change[] {
  if (summary === null) return []
  let values: unknown
  try { values = JSON.parse(summary) } catch { return invalid() }
  if (!Array.isArray(values) || values.length > 20) return invalid()
  const seen = new Set<string>()
  return values.map(v => {
    if (!object(v) || !keys(v, ['field', 'before', 'after', 'evidence']) || typeof v.field !== 'string' ||
      !changeFields.has(v.field) || seen.has(v.field) ||
      (typeof v.evidence !== 'string' || !['ValuesWithheld', 'RevisionReference', 'Allowlisted'].includes(v.evidence))) return invalid()
    const field = v.field
    seen.add(field)
    const expected = field === 'ContentJson' ? 'RevisionReference' : withheldFields.has(field) ? 'ValuesWithheld' : 'Allowlisted'
    if (v.evidence !== expected) return invalid()
    if (withheldFields.has(field) || field === 'ContentJson' ? v.before !== null || v.after !== null
      : !safeChangeValue(field, v.before) || !safeChangeValue(field, v.after)) return invalid()
    if (contextFields.has(field) && (v.before !== null || v.after === null)) return invalid()
    return v as Change
  })
}
export function decodeAuditRecord(v: unknown): AuditRecord {
  const names = ['id', 'occurredAt', 'actorReference', 'actorType', 'action', 'entityType', 'resourceId', 'vesselId', 'capabilityContext', 'roleContext', 'changeSummary', 'beforeVersion', 'afterVersion', 'beforeRevision', 'afterRevision', 'reasonStatus', 'correlationId', 'metadataStatus', 'revisionHref']
  if (!object(v) || !keys(v, names) || !isGuid(v.id) || typeof v.occurredAt !== 'string' || !timestamp(v.occurredAt) ||
    !(v.actorReference === null || isGuid(v.actorReference) || v.actorReference === 'system:seed-import') ||
    typeof v.actorType !== 'string' || !['User', 'System', 'Unavailable'].includes(v.actorType) || typeof v.action !== 'string' || !['Added', 'Modified', 'Deleted'].includes(v.action) ||
    !auditTypes.includes(v.entityType as typeof auditTypes[number]) || !nullableGuid(v.resourceId) || !nullableGuid(v.vesselId) ||
    !revision(v.beforeVersion) || (typeof v.beforeVersion === 'number' && v.beforeVersion > 4294967295) || !revision(v.afterVersion) || (typeof v.afterVersion === 'number' && v.afterVersion > 4294967295) || !revision(v.beforeRevision) || !revision(v.afterRevision) ||
    !(v.reasonStatus === null || typeof v.reasonStatus === 'string' && ['NotRequiredForCommand', 'RecordedControlledCode', 'RecordedProtected', 'SystemOperation', 'HistoricalUnavailable'].includes(v.reasonStatus)) ||
    typeof v.metadataStatus !== 'string' || !['Recorded', 'HistoricalMetadataUnavailable', 'CapabilityContextUnavailable'].includes(v.metadataStatus) ||
    !(v.correlationId === null || isGuid(v.correlationId) || typeof v.correlationId === 'string' && v.correlationId.length === 32 && /^[0-9a-f]{32}$/i.test(v.correlationId)) || !(v.changeSummary === null || typeof v.changeSummary === 'string' && v.changeSummary.length <= 2048) ||
    !(v.roleContext === null || typeof v.roleContext === 'string' && v.roleContext.length <= 600 && v.roleContext.split(',').length <= 16 && v.roleContext.split(',').every(isGuid) && new Set(v.roleContext.split(',')).size === v.roleContext.split(',').length) ||
    !(v.capabilityContext === null || typeof v.capabilityContext === 'string' && v.capabilityContext.length <= 2048 && v.capabilityContext.split(',').every(p => auditCapabilities.has(p))) ||
    !(v.revisionHref === null || v.revisionHref === `/api/v1/audit/${v.id}/revision` && integer(v.afterRevision, 1) && isGuid(v.resourceId))) return invalid()
  const summary = changes(v.changeSummary as string | null)
  if (v.reasonStatus === 'RecordedControlledCode' || v.reasonStatus === 'RecordedProtected') {
    const command = summary.find(x => x.field === 'CommandName')?.after
    const reason = summary.find(x => x.field === 'TechnicalReasonCode')?.after
    const scope = summary.find(x => x.field === 'ScopeKind')?.after
    if (!command || !reason || operationReasons.get(command) !== reason || !scope || scope === 'Unresolved' ||
      v.actorType !== 'User' || !isGuid(v.actorReference) || !isGuid(v.resourceId) || !v.roleContext ||
      !v.capabilityContext || !v.correlationId || v.metadataStatus !== 'Recorded' ||
      (scope === 'Vessel' ? !isGuid(v.vesselId) : v.vesselId !== null)) return invalid()
  }
  if (v.reasonStatus === 'SystemOperation' && v.actorType !== 'System') return invalid()
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
