import type { Execution, ExecutionView } from './work-order-execution-api'
import type { CriticalSpare, CriticalSparePage, Requisition } from './critical-spares-api'

// Fictional, unverified fixtures for DEV-054/DEV-057/DEV-110 component tests only.
export const executionVessel = '11111111-1111-4111-8111-111111111111'
export const executionActor = '22222222-2222-4222-8222-222222222222'
export const executionTenant = '77777777-7777-4777-8777-777777777777'
export const executionWorkOrder = '33333333-3333-4333-8333-333333333333'
export const spareId = '44444444-4444-4444-8444-444444444444'
export const requisitionId = '55555555-5555-4555-8555-555555555555'
export const executionFixture = (patch: Partial<Execution> = {}): Execution => ({
  workOrderId: executionWorkOrder, vesselId: executionVessel, revision: 2, version: 12, workOrderRevision: 1, workOrderVersion: 5, state: 'IN_PROGRESS', requiredEvidence: ['READING'],
  evidence: { readings: [], spares: [], files: [], workDone: null, findings: null }, evidenceHash: 'A'.repeat(64),
  gate: { state: 'MISSING', required: ['READING'], present: [], missing: ['READING'] }, blockers: ['REQUIRED_EVIDENCE_MISSING_READING', 'HUMAN_VERIFICATION_NOT_RECORDED'], transition: 'START',
  releasedAt: '2026-11-20T08:00:00Z', startedAt: '2026-11-20T09:00:00Z', submittedAt: null, verifiedAt: null, closedAt: null, returnReason: null, returnedAt: null,
  releasedByCurrentActor: false, executedByCurrentActor: true, sourceVersion: 'synthetic', hash: 'b'.repeat(64), provenance: 'UserDeclaredUnverified', savedAt: '2026-11-20T09:00:00Z',
  currentRevision: true, qualification: 'HUMAN_RECORDED_NOT_RETURN_TO_SERVICE', ...patch })
export const executionViewFixture = (current: Execution | null = executionFixture(), caps: Partial<ExecutionView['capabilities']> = {}): ExecutionView => ({
  workOrderId: executionWorkOrder, vesselId: executionVessel, workOrderRevision: 1, workOrderVersion: 5, current,
  capabilities: { canRelease: false, canExecute: true, canVerify: false, canClose: false, evidenceKinds: ['READING', 'SPARES_RECORD', 'PHOTO', 'DOCUMENT', 'WORK_DONE'], fileKinds: ['PHOTO', 'DOCUMENT'],
    returnReasons: ['EVIDENCE_INSUFFICIENT', 'READING_NOT_ACCEPTED', 'WORK_INCOMPLETE', 'OTHER_SEE_FINDINGS'], offline: { supported: false, state: 'UNAVAILABLE_POLICY' }, sourceVersion: 'synthetic',
    qualification: 'HUMAN_RECORDED_NOT_RETURN_TO_SERVICE', ...caps } })
export const spareFixture = (patch: Partial<CriticalSpare> = {}): CriticalSpare => ({
  id: spareId, vesselId: executionVessel, revision: 1, version: 3, definition: { partNumber: 'FICT-1', name: 'Fictional seal kit', unit: 'pcs', criticality: 'CRITICAL_DECLARED', assetId: null, minQuantity: '2', maxQuantity: '10', location: null },
  count: { quantity: '1', countedAt: '2026-11-20T07:00:00Z' }, stock: { state: 'LOW_STOCK', counted: '1', countedAt: '2026-11-20T07:00:00Z', consumedSinceCount: '0', projected: '1', minimum: '2', maximum: '10',
    proposedQuantity: '9', reasons: ['COUNT_DECLARED_UNVERIFIED'], qualification: 'DECLARED_COUNT_UNVERIFIED_ADVISORY' }, equipmentCriticality: 'UNKNOWN', openWorkOrderDemand: 0, openRequisitionId: null,
  transition: 'CREATE', sourceVersion: 'synthetic', hash: 'c'.repeat(64), savedAt: '2026-11-20T07:00:00Z', provenance: 'UserDeclaredUnverified', currentRevision: true, canEdit: true, ...patch })
export const sparePageFixture = (items: CriticalSpare[] = [spareFixture()], patch: Partial<CriticalSparePage> = {}): CriticalSparePage => ({
  items, offset: 0, limit: 25, hasMore: false, lowStock: items.filter(i => i.stock.state === 'LOW_STOCK').length, reviewRequired: items.filter(i => !['LOW_STOCK', 'ABOVE_MINIMUM'].includes(i.stock.state)).length,
  canWrite: true, canPropose: true, canApprove: false, criticalities: ['CRITICAL_DECLARED', 'NON_CRITICAL_DECLARED', 'UNKNOWN'],
  rejectReasons: ['STOCK_COUNT_DISPUTED', 'DEMAND_COVERED_ELSEWHERE', 'QUANTITY_NOT_ACCEPTED', 'OTHER_SEE_ERP'], qualification: 'DECLARED_COUNT_UNVERIFIED_ADVISORY', erpBoundary: 'EXPORT_REFERENCE_ONLY_NO_PURCHASE_ORDER', sourceVersion: 'synthetic', ...patch })
export const requisitionFixture = (patch: Partial<Requisition> = {}): Requisition => ({
  id: requisitionId, vesselId: executionVessel, spareId, spareRevision: 1, revision: 1, version: 4, state: 'PROPOSED', proposedQuantity: '9', basisState: 'LOW_STOCK', basisProjected: '1', basisMinimum: '2', basisMaximum: '10',
  spareCriticality: 'CRITICAL_DECLARED', equipmentCriticality: 'UNKNOWN', openWorkOrderDemand: 0, proposedAt: '2026-11-20T08:00:00Z', approvedQuantity: null, decidedAt: null, reasonCode: null,
  proposedByCurrentActor: false, decidedByCurrentActor: false, partNumber: 'FICT-1', name: 'Fictional seal kit', unit: 'pcs', erpReference: null, transition: 'PROPOSE', hash: 'd'.repeat(64),
  savedAt: '2026-11-20T08:00:00Z', currentRevision: true, canDecide: true, erpBoundary: 'EXPORT_REFERENCE_ONLY_NO_PURCHASE_ORDER', ...patch })
