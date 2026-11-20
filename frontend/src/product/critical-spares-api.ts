import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// DEV-110 / T-SUP2-001 strict client contract for /api/v1/critical-spares and /api/v1/technical-requisitions.
// A healthy stock state is only ABOVE_MINIMUM with a declared count and minimum; UNKNOWN/UNCONFIGURED
// are review-required. Approval yields an ERP export reference only (no purchase order/vendor/price).
export const criticalities = ['CRITICAL_DECLARED', 'NON_CRITICAL_DECLARED', 'UNKNOWN'] as const
export type Criticality = typeof criticalities[number]
export const stockStates = ['STOCK_UNKNOWN', 'MINIMUM_UNCONFIGURED', 'CONSUMPTION_UNRESOLVED', 'LOW_STOCK', 'ABOVE_MINIMUM'] as const
export type StockState = typeof stockStates[number]
export const requisitionStates = ['PROPOSED', 'APPROVED_FOR_ERP_EXPORT', 'REJECTED'] as const
export type RequisitionState = typeof requisitionStates[number]
export const rejectReasons = ['STOCK_COUNT_DISPUTED', 'DEMAND_COVERED_ELSEWHERE', 'QUANTITY_NOT_ACCEPTED', 'OTHER_SEE_ERP'] as const
export type RejectReason = typeof rejectReasons[number]
export const erpBoundary = 'EXPORT_REFERENCE_ONLY_NO_PURCHASE_ORDER'
export type SpareDefinition = { partNumber: string; name: string; unit: string; criticality: Criticality; assetId: string | null; minQuantity: string | null; maxQuantity: string | null; location: string | null }
export type SpareCount = { quantity: string; countedAt: string }
export type StockProjection = { state: StockState; counted: string | null; countedAt: string | null; consumedSinceCount: string | null; projected: string | null; minimum: string | null; maximum: string | null
  proposedQuantity: string | null; reasons: string[]; qualification: 'DECLARED_COUNT_UNVERIFIED_ADVISORY' }
export type CriticalSpare = { id: string; vesselId: string; revision: number; version: number; definition: SpareDefinition; count: SpareCount | null; stock: StockProjection; equipmentCriticality: string
  openWorkOrderDemand: number; openRequisitionId: string | null; transition: 'CREATE' | 'UPDATE'; sourceVersion: string; hash: string; savedAt: string; provenance: string; currentRevision: boolean; canEdit: boolean }
export type CriticalSparePage = { items: CriticalSpare[]; offset: number; limit: 25; hasMore: boolean; lowStock: number; reviewRequired: number; canWrite: boolean; canPropose: boolean; canApprove: boolean
  criticalities: Criticality[]; rejectReasons: RejectReason[]; qualification: string; erpBoundary: typeof erpBoundary; sourceVersion: string }
export type ErpReference = { kind: 'TechnicalRequisition'; referenceId: string; vesselId: string; partNumber: string; unit: string; quantity: string; sourceVersion: string; boundary: typeof erpBoundary }
export type Requisition = { id: string; vesselId: string; spareId: string; spareRevision: number; revision: number; version: number; state: RequisitionState; proposedQuantity: string | null; basisState: StockState
  basisProjected: string | null; basisMinimum: string | null; basisMaximum: string | null; spareCriticality: Criticality; equipmentCriticality: string; openWorkOrderDemand: number; proposedAt: string
  approvedQuantity: string | null; decidedAt: string | null; reasonCode: RejectReason | null; proposedByCurrentActor: boolean; decidedByCurrentActor: boolean; partNumber: string; name: string; unit: string
  erpReference: ErpReference | null; transition: 'PROPOSE' | 'APPROVE' | 'REJECT'; hash: string; savedAt: string; currentRevision: boolean; canDecide: boolean; erpBoundary: typeof erpBoundary }
export type RequisitionPage = { items: Requisition[]; offset: number; limit: 25; hasMore: boolean }
export type SupplyCommand = { vessel: string; key: string; path: string; method: 'POST' | 'PUT'; body: string }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const int = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const one = <T extends string>(v: unknown, set: readonly T[]): v is T => typeof v === 'string' && (set as readonly string[]).includes(v)
const str = (v: unknown, max: number) => typeof v === 'string' && v.trim().length > 0 && v.length <= max && !/\p{Cc}/u.test(v)
const opt = (v: unknown, max: number) => v === null || str(v, max)
export const quantity = (v: unknown): v is string => typeof v === 'string' && /^(?:0|[1-9][0-9]{0,27})(?:\.[0-9]*[1-9])?$/.test(v)
const signed = (v: unknown) => v === null || typeof v === 'string' && /^-?(?:0|[1-9][0-9]{0,27})(?:\.[0-9]*[1-9])?$/.test(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 32 && v.every(c => typeof c === 'string' && /^[A-Z0-9_]{1,80}$/.test(c))
const hash = (v: unknown) => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
function fail(): never { throw new ApiError(502, 'invalid_critical_spare_contract') }

export function validDefinition(d: unknown): d is SpareDefinition {
  return exact(d, ['partNumber', 'name', 'unit', 'criticality', 'assetId', 'minQuantity', 'maxQuantity', 'location']) && str(d.partNumber, 64) && str(d.name, 200) && str(d.unit, 32) &&
    one(d.criticality, criticalities) && (d.assetId === null || isGuid(d.assetId)) && (d.minQuantity === null || quantity(d.minQuantity)) && (d.maxQuantity === null || quantity(d.maxQuantity)) &&
    opt(d.location, 120) && (d.minQuantity === null || d.maxQuantity === null || Number(d.maxQuantity) >= Number(d.minQuantity))
}
function decodeStock(v: unknown): StockProjection {
  if (!exact(v, ['state', 'counted', 'countedAt', 'consumedSinceCount', 'projected', 'minimum', 'maximum', 'proposedQuantity', 'reasons', 'qualification']) || !one(v.state, stockStates) ||
    !(v.counted === null || quantity(v.counted)) || !(v.countedAt === null || timestamp(v.countedAt)) || !signed(v.consumedSinceCount) || !signed(v.projected) ||
    !(v.minimum === null || quantity(v.minimum)) || !(v.maximum === null || quantity(v.maximum)) || !(v.proposedQuantity === null || quantity(v.proposedQuantity)) || !codes(v.reasons) ||
    v.qualification !== 'DECLARED_COUNT_UNVERIFIED_ADVISORY') fail()
  // Healthy and low states require a declared count and a configured minimum; a proposal only exists when low.
  if ((v.state === 'LOW_STOCK' || v.state === 'ABOVE_MINIMUM') && (v.counted === null || v.minimum === null || v.projected === null) || v.proposedQuantity !== null && v.state !== 'LOW_STOCK') fail()
  return v as StockProjection
}
export function decodeSpare(v: unknown, vessel: string): CriticalSpare {
  if (!exact(v, ['id', 'vesselId', 'revision', 'version', 'definition', 'count', 'stock', 'equipmentCriticality', 'openWorkOrderDemand', 'openRequisitionId', 'transition', 'sourceVersion', 'hash',
    'savedAt', 'provenance', 'currentRevision', 'canEdit']) || !isGuid(v.id) || v.vesselId !== vessel || !int(v.revision) || !int(v.version, 1, 4294967295) || !validDefinition(v.definition) ||
    !(v.count === null || exact(v.count, ['quantity', 'countedAt']) && quantity(v.count.quantity) && timestamp(v.count.countedAt)) || !str(v.equipmentCriticality, 64) ||
    !int(v.openWorkOrderDemand, 0, 100000) || !(v.openRequisitionId === null || isGuid(v.openRequisitionId)) || !one(v.transition, ['CREATE', 'UPDATE'] as const) || typeof v.sourceVersion !== 'string' ||
    !hash(v.hash) || !timestamp(v.savedAt) || typeof v.provenance !== 'string' || typeof v.currentRevision !== 'boolean' || typeof v.canEdit !== 'boolean' || v.canEdit && !v.currentRevision) fail()
  return { ...(v as CriticalSpare), stock: decodeStock(v.stock) }
}
export function decodeRequisition(v: unknown, vessel: string): Requisition {
  if (!exact(v, ['id', 'vesselId', 'spareId', 'spareRevision', 'revision', 'version', 'state', 'proposedQuantity', 'basisState', 'basisProjected', 'basisMinimum', 'basisMaximum', 'spareCriticality',
    'equipmentCriticality', 'openWorkOrderDemand', 'proposedAt', 'approvedQuantity', 'decidedAt', 'reasonCode', 'proposedByCurrentActor', 'decidedByCurrentActor', 'partNumber', 'name', 'unit',
    'erpReference', 'transition', 'hash', 'savedAt', 'currentRevision', 'canDecide', 'erpBoundary']) || !isGuid(v.id) || v.vesselId !== vessel || !isGuid(v.spareId) || !int(v.spareRevision) ||
    !int(v.revision) || !int(v.version, 1, 4294967295) || !one(v.state, requisitionStates) || !(v.proposedQuantity === null || quantity(v.proposedQuantity)) || v.basisState !== 'LOW_STOCK' ||
    !signed(v.basisProjected) || !(v.basisMinimum === null || quantity(v.basisMinimum)) || !(v.basisMaximum === null || quantity(v.basisMaximum)) || !one(v.spareCriticality, criticalities) ||
    !str(v.equipmentCriticality, 64) || !int(v.openWorkOrderDemand, 0, 100000) || !timestamp(v.proposedAt) || !(v.approvedQuantity === null || quantity(v.approvedQuantity)) ||
    !(v.decidedAt === null || timestamp(v.decidedAt)) || !(v.reasonCode === null || one(v.reasonCode, rejectReasons)) || typeof v.proposedByCurrentActor !== 'boolean' ||
    typeof v.decidedByCurrentActor !== 'boolean' || !str(v.partNumber, 64) || !str(v.name, 200) || !str(v.unit, 32) || !one(v.transition, ['PROPOSE', 'APPROVE', 'REJECT'] as const) || !hash(v.hash) ||
    !timestamp(v.savedAt) || typeof v.currentRevision !== 'boolean' || typeof v.canDecide !== 'boolean' || v.erpBoundary !== erpBoundary) fail()
  // Only an approved decision carries an export reference; proposals and rejections never do.
  if ((v.state === 'APPROVED_FOR_ERP_EXPORT') !== (v.erpReference !== null) || v.state === 'APPROVED_FOR_ERP_EXPORT' && v.approvedQuantity === null || v.canDecide && v.state !== 'PROPOSED' ||
    v.state === 'PROPOSED' && v.decidedAt !== null) fail()
  if (v.erpReference !== null && (!exact(v.erpReference, ['kind', 'referenceId', 'vesselId', 'partNumber', 'unit', 'quantity', 'sourceVersion', 'boundary']) || v.erpReference.kind !== 'TechnicalRequisition' ||
    v.erpReference.referenceId !== v.id || v.erpReference.vesselId !== vessel || v.erpReference.quantity !== v.approvedQuantity || v.erpReference.boundary !== erpBoundary)) fail()
  return v as Requisition
}
const vesselQuery = (vessel: string) => { if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return 'vesselId=' + vessel }
const offsetQuery = (offset: number) => { if (!int(offset, 0, 100000)) throw new ApiError(400, 'invalid_context'); return offset ? '&offset=' + offset : '' }
export function freezeSpare(vessel: string, record: CriticalSpare | null, definition: SpareDefinition, count: SpareCount | null): SupplyCommand {
  if (!validDefinition(definition) || count !== null && (!quantity(count.quantity) || !timestamp(count.countedAt)) || record && (!record.currentRevision || !record.canEdit)) throw new ApiError(400, 'invalid_spare_input')
  const id = record?.id ?? crypto.randomUUID()
  return { vessel, key: crypto.randomUUID(), path: record ? `critical-spares/${id}?${vesselQuery(vessel)}` : `critical-spares?${vesselQuery(vessel)}`, method: record ? 'PUT' : 'POST',
    body: JSON.stringify({ id, expectedVersion: record?.version ?? 0, expectedRevision: record?.revision ?? 0, definition, count }) }
}
export function freezeProposal(vessel: string, spare: CriticalSpare): SupplyCommand {
  if (spare.stock.state !== 'LOW_STOCK' || spare.openRequisitionId) throw new ApiError(400, 'invalid_spare_input')
  return { vessel, key: crypto.randomUUID(), path: `technical-requisitions?${vesselQuery(vessel)}`, method: 'POST', body: JSON.stringify({ id: crypto.randomUUID(), spareId: spare.id, spareRevision: spare.revision }) }
}
export function freezeDecision(vessel: string, requisition: Requisition, decision: 'APPROVE' | 'REJECT', approvedQuantity: string | null, reasonCode: RejectReason | null): SupplyCommand {
  if (!requisition.canDecide || (decision === 'APPROVE' ? approvedQuantity !== null && !quantity(approvedQuantity) || reasonCode !== null : reasonCode === null || approvedQuantity !== null)) throw new ApiError(400, 'invalid_spare_input')
  return { vessel, key: crypto.randomUUID(), path: `technical-requisitions/${requisition.id}/decisions?${vesselQuery(vessel)}`, method: 'POST',
    body: JSON.stringify({ id: requisition.id, expectedVersion: requisition.version, expectedRevision: requisition.revision, decision, approvedQuantity, reasonCode }) }
}
export const criticalSpareApi = {
  list: async (vessel: string, offset: number, signal: AbortSignal): Promise<CriticalSparePage> => {
    const v = await request<unknown>(`critical-spares?${vesselQuery(vessel)}${offsetQuery(offset)}`, signal)
    if (!exact(v, ['items', 'offset', 'limit', 'hasMore', 'lowStock', 'reviewRequired', 'canWrite', 'canPropose', 'canApprove', 'criticalities', 'rejectReasons', 'qualification', 'erpBoundary', 'sourceVersion']) ||
      !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset || v.limit !== 25 || typeof v.hasMore !== 'boolean' || !int(v.lowStock, 0, 25) || !int(v.reviewRequired, 0, 25) ||
      ![v.canWrite, v.canPropose, v.canApprove].every(b => typeof b === 'boolean') || v.erpBoundary !== erpBoundary || typeof v.qualification !== 'string' || typeof v.sourceVersion !== 'string') fail()
    const items = v.items.map(item => decodeSpare(item, vessel))
    if (new Set(items.map(i => i.id)).size !== items.length || v.lowStock !== items.filter(i => i.stock.state === 'LOW_STOCK').length) fail()
    return { ...(v as CriticalSparePage), items }
  },
  requisitions: async (vessel: string, offset: number, signal: AbortSignal): Promise<RequisitionPage> => {
    const v = await request<unknown>(`technical-requisitions?${vesselQuery(vessel)}${offsetQuery(offset)}`, signal)
    if (!exact(v, ['items', 'offset', 'limit', 'hasMore']) || !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset || v.limit !== 25 || typeof v.hasMore !== 'boolean') fail()
    return { items: v.items.map(item => decodeRequisition(item, vessel)), offset, limit: 25, hasMore: v.hasMore }
  },
  send: async (command: SupplyCommand, csrf: string, signal: AbortSignal) => request<unknown>(command.path, signal,
    { method: command.method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: command.body }),
}
