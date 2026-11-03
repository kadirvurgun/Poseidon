import { calendar, timestamp as date } from './date-validation'
import { isGuid as guid } from './identifiers'
import type { components } from '../generated/api'
import type { Page, Vessel } from './directory-contract'
import { ApiError, request } from './api'
import { decodePlanningEvidence } from './tug-planning-api'
import type { PlanningEvidence } from './tug-planning-api'

export type Today = components['schemas']['TodayDto']
// Authored extension until actual host/OpenAPI regeneration at the combined gate.
// Existing generated artifacts are preserved; this is not generated parity evidence.
type DraftWhy = components['schemas']['TodayWhyDto']
export type MaintenanceEvidence = { planId: string; planRevision: number; assetId: string; assetRevision: number;
  currentAssetRevision: number | null; jobAvailable: boolean; ownerEligible: boolean;
  calculation: { state: string; reason: string; nextDueDate: string | null; nextDueMeter: string | null; algorithm: string }; observationState: string }
export type TodayTugEvidence = { jobId: string; requestRevision: number; requestIncomplete: boolean;
  planRevision: number | null; plannedRequestRevision: number | null; plannedStart: string | null; plannedEnd: string | null;
  assignedTugs: string[]; currentOwnerRoleId: string | null; evidence: PlanningEvidence | null }
export type Why = Omit<DraftWhy, 'inputs' | 'maintenance'> & { inputs: DraftWhy['inputs'] | null; maintenance?: MaintenanceEvidence; tug?: TodayTugEvidence }
const pageLimit = 25
const todayOffset = 0
const object = (x: unknown): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x)
const exact = (x: Record<string, unknown>, keys: string[]) => Object.keys(x).length === keys.length && keys.every(k => k in x)
const integer = (x: unknown) => typeof x === 'number' && Number.isSafeInteger(x) && x >= 0
const one = (x: unknown, values: string[]) => typeof x === 'string' && values.includes(x)
const text = (x: unknown, max: number) => typeof x === 'string' && x.length > 0 && x.length <= max && !Array.from(x).some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))
const href = (x: unknown, id: string) => x === `/app/fleet/${id}/profile`
const planId = (key: unknown): string | null => {
  if (typeof key !== 'string' || !key.startsWith('maintenance-plan:')) return null
  const id = key.slice('maintenance-plan:'.length)
  return guid(id) && id === id.toLowerCase() && id !== '00000000-0000-0000-0000-000000000000' ? id : null
}
const planHref = (x: unknown, vessel: string, key: unknown) => planId(key) !== null && x === `/app/maintenance-plan/${vessel}?id=${planId(key)}`
const tugJobId = (key: unknown): string | null => {
  if (typeof key !== 'string' || !key.startsWith('tug-job:')) return null
  const id = key.slice('tug-job:'.length)
  return guid(id) && id === id.toLowerCase() && id !== '00000000-0000-0000-0000-000000000000' ? id : null
}
const tugHref = (x: unknown, vessel: string, key: unknown) => tugJobId(key) !== null && x === `/app/tug-job-detail/${tugJobId(key)}?vessel=${vessel}`
const tugAttention = ['TugConflictReviewRequired', 'TugReadinessBlocked']
const tugReview = ['TugRequestChanged', 'TugPlanMissing', 'TugBoundaryReviewRequired', 'TugRequestIncomplete', 'TugPlanningReviewRequired']
const rank = (priority: unknown) => priority === 'Attention' ? 0 : priority === 'Review' ? 1 : 2
function validVessel(x: unknown): x is Vessel {
  return object(x) && exact(x, ['id', 'companyId', 'code', 'name', 'kind', 'version', 'canEdit']) && guid(x.id) && guid(x.companyId) &&
    text(x.code, 64) && text(x.name, 200) && one(x.kind, ['Ship', 'Tug']) && integer(x.version) &&
    Number(x.version) > 0 && Number(x.version) <= 4294967295 && typeof x.canEdit === 'boolean'
}
function fail(): never { throw new ApiError(502, 'invalid_today_contract') }
export function decodeTodayVessels(x: unknown, offset: number): Page<Vessel> {
  if (!integer(offset) || offset > 100000 || !object(x) || !exact(x, ['items', 'offset', 'limit', 'hasMore']) || x.offset !== offset || x.limit !== pageLimit ||
    typeof x.hasMore !== 'boolean' || !Array.isArray(x.items) || x.items.length > pageLimit || (x.hasMore && x.items.length !== pageLimit) || !x.items.every(validVessel) ||
    new Set(x.items.map(v => v.id.toLowerCase())).size !== x.items.length) fail()
  return x as Page<Vessel>
}
export function decodeToday(x: unknown, id: string, offset = todayOffset): Today {
  if (!object(x) || !exact(x, ['vessel', 'readAt', 'coverage', 'cards']) || !validVessel(x.vessel) || x.vessel.id !== id || !guid(id) || !date(x.readAt) ||
    !integer(offset) || offset > 100000 || !Array.isArray(x.coverage) || x.coverage.length !== 7 || !object(x.cards) || !Array.isArray(x.cards.items) || x.cards.items.length > pageLimit ||
    !exact(x.cards, ['items', 'offset', 'limit', 'hasMore']) || x.cards.offset !== offset ||
    x.cards.limit !== pageLimit || typeof x.cards.hasMore !== 'boolean' || (x.cards.hasMore && x.cards.items.length !== pageLimit)) fail()
  const sources = ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
  const vesselKind = x.vessel.kind
  if (!x.coverage.every((c, i) => object(c) && exact(c, ['source', 'status']) && c.source === sources[i] && one(c.status,
    i === 0 ? ['Available', 'Missing'] : i === 5 || i === 6 && vesselKind === 'Tug' ? ['Available', 'Missing', 'Unavailable'] : ['Unavailable']))) fail()
  const draftMissing = x.coverage[0].status === 'Missing'
  const canEdit = x.vessel.canEdit
  const maintenanceAvailable = x.coverage[5].status === 'Available'
  const tugAvailable = x.coverage[6].status === 'Available'
  if (!x.cards.items.every(c => {
    if (!object(c) || !exact(c, ['key', 'priority', 'source', 'reason', 'owner', 'nextAction', 'href'])) return false
    if (c.key === 'regulatory-draft') return c.priority === 'Advisory' && c.source === 'RegulatoryDraft' &&
      c.reason === (draftMissing ? 'DraftAbsent' : 'DraftUnverified') && c.owner === null && c.nextAction === (canEdit ? 'ReviewDraft' : 'ViewDraft') && href(c.href, id)
    if (tugJobId(c.key) !== null) return tugAvailable && c.source === 'TugJob' && (c.owner === null || guid(c.owner)) &&
      (c.priority === 'Attention' && one(c.reason, tugAttention) || c.priority === 'Review' && one(c.reason, tugReview)) &&
      c.nextAction === 'ReviewTugJob' && tugHref(c.href, id, c.key)
    return maintenanceAvailable && planId(c.key) !== null && c.source === 'MaintenancePlan' &&
      (c.reason === 'MaintenanceDue' && c.priority === 'Attention' || c.reason === 'MaintenanceReviewRequired' && c.priority === 'Review') &&
      (c.owner === null || guid(c.owner)) && c.nextAction === 'ReviewMaintenancePlan' && planHref(c.href, id, c.key)
  }) || new Set(x.cards.items.map(c => c.key)).size !== x.cards.items.length) fail()
  for (let i = 1; i < x.cards.items.length; i++) {
    const before = x.cards.items[i - 1], current = x.cards.items[i]
    if (rank(before.priority) > rank(current.priority) || rank(before.priority) === rank(current.priority) && before.key >= current.key) fail()
  }
  return x as Today
}
export function decodeWhy(x: unknown, id: string, cardKey = 'regulatory-draft'): Why {
  if (tugJobId(cardKey) !== null) return decodeTugWhy(x, id, cardKey)
  if (planId(cardKey) !== null) return decodeMaintenanceWhy(x, id, cardKey)
  if (cardKey !== 'regulatory-draft') fail()
  if (!object(x) || !guid(id) || !exact(x, ['vesselId', 'cardKey', 'rule', 'reason', 'inputs', 'readAt', 'savedAt', 'sourceObservedAt', 'freshness', 'provenance', 'blockers', 'owner', 'nextAction', 'href']) ||
    x.vesselId !== id || x.cardKey !== 'regulatory-draft' || x.rule !== 'TW-DRAFT-01' ||
    !one(x.reason, ['DraftAbsent', 'DraftUnverified']) || !object(x.inputs) || x.inputs.reason !== x.reason ||
    !exact(x.inputs, ['known', 'missing', 'unknown', 'reason']) ||
    ![x.inputs.known, x.inputs.missing, x.inputs.unknown].every(integer) ||
    Number(x.inputs.known) + Number(x.inputs.missing) + Number(x.inputs.unknown) !== (x.reason === 'DraftAbsent' ? 0 : 20) ||
    !date(x.readAt) || !(x.savedAt === null || date(x.savedAt)) || !(x.sourceObservedAt === null || date(x.sourceObservedAt)) ||
    x.freshness !== 'Unverified' || (x.reason === 'DraftAbsent' ? x.provenance !== 'Missing' || x.savedAt !== null || x.sourceObservedAt !== null
      : !one(x.provenance, ['Unknown', 'Synthetic', 'DeclaredUnverified']) || x.savedAt === null) ||
    !Array.isArray(x.blockers) || x.blockers.length !== 2 || x.blockers[0] !== (x.reason === 'DraftAbsent' ? 'DraftAbsent' : 'VerificationUnavailable') ||
    x.blockers[1] !== 'OperationalSourcesUnavailable' || x.owner !== null || !one(x.nextAction, ['ViewDraft', 'ReviewDraft']) || !href(x.href, id)) fail()
  return x as Why
}
function decodeTugWhy(x: unknown, vessel: string, key: string): Why {
  if (!guid(vessel) || !object(x) || !exact(x, ['vesselId', 'cardKey', 'rule', 'reason', 'inputs', 'readAt', 'savedAt', 'sourceObservedAt',
    'freshness', 'provenance', 'blockers', 'owner', 'nextAction', 'href', 'tug']) || x.vesselId !== vessel || x.cardKey !== key ||
    x.rule !== 'TW-TUG-01' || !one(x.reason, [...tugAttention, ...tugReview]) || x.inputs !== null || !date(x.readAt) || !date(x.savedAt) ||
    x.sourceObservedAt !== null || x.freshness !== 'Unverified' || !text(x.provenance, 200) || !(x.owner === null || guid(x.owner)) ||
    x.nextAction !== 'ReviewTugJob' || !tugHref(x.href, vessel, key) || !Array.isArray(x.blockers) || x.blockers.length !== 3 ||
    x.blockers[0] !== 'DraftPlanningNotOperationalApproval' || x.blockers[1] !== 'OwningReadinessCoverageIncomplete' ||
    x.blockers[2] !== 'MasterDecisionRequired' || !object(x.tug)) fail()
  const p = x.tug
  if (!exact(p, ['jobId', 'requestRevision', 'requestIncomplete', 'planRevision', 'plannedRequestRevision', 'plannedStart', 'plannedEnd', 'assignedTugs', 'currentOwnerRoleId', 'evidence']) ||
    p.jobId !== tugJobId(key) || !integer(p.requestRevision) || p.requestRevision === 0 || typeof p.requestIncomplete !== 'boolean' ||
    !(p.currentOwnerRoleId === null || guid(p.currentOwnerRoleId)) || x.owner !== p.currentOwnerRoleId ||
    !Array.isArray(p.assignedTugs) || p.assignedTugs.length > 1000 || !p.assignedTugs.every(guid) ||
    new Set(p.assignedTugs.map(t => t.toLowerCase())).size !== p.assignedTugs.length ||
    !(p.plannedStart === null || date(p.plannedStart)) || !(p.plannedEnd === null || date(p.plannedEnd)) ||
    p.plannedStart !== null && p.plannedEnd !== null && Date.parse(p.plannedStart as string) > Date.parse(p.plannedEnd as string)) fail()
  if (p.planRevision === null) {
    if (p.plannedRequestRevision !== null || p.plannedStart !== null || p.plannedEnd !== null || p.assignedTugs.length !== 0 || p.evidence !== null || x.reason !== 'TugPlanMissing') fail()
  } else {
    if (!integer(p.planRevision) || p.planRevision === 0 || !integer(p.plannedRequestRevision) || p.plannedRequestRevision === 0 ||
      Number(p.plannedRequestRevision) > Number(p.requestRevision) || p.evidence === null) fail()
    const evidence = decodePlanningEvidence(p.evidence, p.assignedTugs as string[])
    const reason = evidence.conflictState === 'HumanReviewRequired' ? 'TugConflictReviewRequired' : evidence.readiness === 'Blocked' ? 'TugReadinessBlocked' :
      p.plannedRequestRevision !== p.requestRevision ? 'TugRequestChanged' : evidence.conflictState === 'BoundaryReviewRequired' ? 'TugBoundaryReviewRequired' :
      p.requestIncomplete ? 'TugRequestIncomplete' : 'TugPlanningReviewRequired'
    if (x.reason !== reason) fail()
  }
  return x as Why
}
const observationStates = ['NotRequested', 'ConfiguredDeclaredObservation', 'ObservationMissing', 'ObservationRevoked', 'ObservationScopeMismatch',
  'ObservationPolicyUnavailable', 'ObservationPolicyChanged', 'ObservationUnitOrKindMismatch', 'ObservationFuture', 'ObservationStale',
  'ObservationPrecisionUnsupported', 'ObservationConditionUnknown']
const calculationReasons = ['EquipmentRevisionChanged', 'MaintenanceJobUnavailable', 'MaintenanceOwnerUnavailable', 'ObservationAssessmentUnavailable',
  'IncompletePlanContext', 'MissingProvenance', 'MissingTimeZone', 'MissingCalendarInputs', 'UnsupportedCalendarInterval', 'FutureBaseline', 'CalendarBoundary',
  'MissingMeterAdapterOrReading', 'FutureObservation', 'ReadingFreshnessPolicyUnresolved', 'MissingOrResetMeterBaseline', 'DeclaredMeterBoundary',
  'MeterPrecisionExceeded', 'MeterRangeExceeded', 'MissingConditionAdapterOrObservation', 'ConditionFreshnessPolicyUnresolved', 'DeclaredConditionObservation',
  'MissingOrUnsupportedTrigger', ...observationStates.filter(s => s !== 'NotRequested' && s !== 'ConfiguredDeclaredObservation')]
function decodeMaintenanceWhy(x: unknown, vessel: string, key: string): Why {
  if (!guid(vessel) || !object(x) || !exact(x, ['vesselId', 'cardKey', 'rule', 'reason', 'inputs', 'readAt', 'savedAt', 'sourceObservedAt',
    'freshness', 'provenance', 'blockers', 'owner', 'nextAction', 'href', 'maintenance']) || x.vesselId !== vessel || x.cardKey !== key ||
    x.rule !== 'TW-MAINTENANCE-01' || !one(x.reason, ['MaintenanceDue', 'MaintenanceReviewRequired']) || x.inputs !== null || !date(x.readAt) ||
    !date(x.savedAt) || !(x.sourceObservedAt === null || date(x.sourceObservedAt)) || x.freshness !== 'Unverified' || !text(x.provenance, 200) ||
    !Array.isArray(x.blockers) || x.blockers.length !== 1 || x.blockers[0] !== 'PlanningDraftNotVerified' ||
    !(x.owner === null || guid(x.owner)) || x.nextAction !== 'ReviewMaintenancePlan' || !planHref(x.href, vessel, key) || !object(x.maintenance)) fail()
  const m = x.maintenance
  if (!exact(m, ['planId', 'planRevision', 'assetId', 'assetRevision', 'currentAssetRevision', 'jobAvailable', 'ownerEligible', 'calculation', 'observationState']) ||
    m.planId !== planId(key) || !guid(m.assetId) || !integer(m.planRevision) || m.planRevision === 0 || !integer(m.assetRevision) || m.assetRevision === 0 ||
    !(m.currentAssetRevision === null || integer(m.currentAssetRevision) && Number(m.currentAssetRevision) > 0) ||
    typeof m.jobAvailable !== 'boolean' || typeof m.ownerEligible !== 'boolean' || (m.ownerEligible ? x.owner === null : x.owner !== null) ||
    !one(m.observationState, observationStates) || !object(m.calculation)) fail()
  const c = m.calculation
  if (!exact(c, ['state', 'reason', 'nextDueDate', 'nextDueMeter', 'algorithm']) ||
    c.state !== (x.reason === 'MaintenanceDue' ? 'Due' : 'ReviewRequired') || !one(c.reason, calculationReasons) ||
    !(c.nextDueDate === null || typeof c.nextDueDate === 'string' && calendar(c.nextDueDate)) ||
    !(c.nextDueMeter === null || typeof c.nextDueMeter === 'string' && c.nextDueMeter.length <= 60 && !/[^0-9.]/.test(c.nextDueMeter) && /^[0-9]+(?:\.[0-9]+)?$/.test(c.nextDueMeter)) ||
    !text(c.algorithm, 180) || !(c.algorithm === 'planning-current-v1' || typeof c.algorithm === 'string' && c.algorithm.startsWith('planning-current-v1/') && c.algorithm.length > 20)) fail()
  if (c.state === 'Due' && (!m.jobAvailable || !m.ownerEligible || m.currentAssetRevision !== m.assetRevision ||
    !one(c.reason, ['CalendarBoundary', 'DeclaredMeterBoundary', 'DeclaredConditionObservation']))) fail()
  if (c.reason === 'CalendarBoundary' && c.nextDueDate === null || c.reason === 'DeclaredMeterBoundary' && c.nextDueMeter === null ||
    c.reason === 'EquipmentRevisionChanged' && m.currentAssetRevision === m.assetRevision ||
    c.reason === 'MaintenanceJobUnavailable' && m.jobAvailable || c.reason === 'MaintenanceOwnerUnavailable' && m.ownerEligible) fail()
  if (one(c.reason, ['ObservationRevoked', 'ObservationStale', 'ObservationPolicyChanged']) &&
    (c.reason !== m.observationState || c.nextDueDate !== null || c.nextDueMeter !== null)) fail()
  return x as Why
}
export const today = async (id: string, signal: AbortSignal, offset = todayOffset) => decodeToday(await request<unknown>(`poseidon-today?vesselId=${encodeURIComponent(id)}&offset=${offset}&limit=${pageLimit}`, signal), id, offset)
export const why = async (id: string, signal: AbortSignal, cardKey = 'regulatory-draft') => decodeWhy(await request<unknown>(`why-engine?vesselId=${encodeURIComponent(id)}&cardKey=${encodeURIComponent(cardKey)}`, signal), id, cardKey)
export const todayVessels = async (offset: number, signal: AbortSignal) => decodeTodayVessels(await request<unknown>(`vessels?offset=${offset}&limit=${pageLimit}`, signal), offset)
