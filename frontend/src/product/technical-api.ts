import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { calendar, timestamp } from './date-validation'

// Authored DTO adapter; OpenAPI generation/parity is DEFERRED to the bundle gate.
// Decimal values stay strings throughout transport and forms, never JS numbers.
export type Asset = {
  id: string; vesselId: string; parentId: string | null; code: string | null; name: string | null;
  level: string; systemFamily: string | null; equipmentType: string | null; criticality: string | null;
  maker: string | null; model: string | null; serialNumber: string | null; location: string | null;
  commissionDate: string | null; responsibleRoleId: string | null; notes: string | null;
  status: string; provenance: string; revision: number; version: number;
}
export type Inputs = { triggerType?: string | null; intervalValue?: string | null; intervalUnit?: string | null;
  meterSource?: string | null; lastDoneDate?: string | null; lastDoneMeter?: string | null; currentMeter?: string | null;
  observedAt?: string | null; observationRevision?: string | null; condition?: string | null; timeZone?: string | null;
  provenance?: string | null; warningLead?: string | null; gracePeriod?: string | null; postponementAllowed?: string | null;
  approvalRule?: string | null; criticality?: string | null; applicableRequirement?: string | null; planType?: string | null; observationId?: string | null }
export type Due = { state: string; reason: string; nextDueDate: string | null; nextDueMeter: string | null; algorithm: string }
export type Plan = { id: string; assetId: string; jobId: string | null; ownerRoleId: string | null; inputs: Inputs;
  due: Due; revision: number; version: number; referenceAt: string; savedAt: string; assetRevision: number; status: string; provenance: string; observationState?: string; observationAssessedAt?: string | null }
export type Snapshot = { items: Asset[]; complete: boolean; canWrite: boolean; canReadHistory: boolean }
export type Plans = { items: Plan[]; canWrite: boolean; canReadHistory: boolean }
export type References = { jobs: { id: string; code: string; title: string; provenance: string }[]; owners: string[]; policy: string }
export type EquipmentCommand = Omit<Asset, 'vesselId' | 'version' | 'revision' | 'status' | 'provenance'> & { baseVersion: number | null }
export type PlanCommand = { id: string; baseVersion: number | null; assetId: string; jobId: string | null; ownerRoleId: string | null; inputs: Inputs; referenceAt: string }
export type Saved = { id: string; revision: number; version: number; replayed: boolean }
export const equipmentPath = (vessel: string) => `equipment-hierarchy/${encodeURIComponent(vessel)}`
export const planPath = (vessel: string) => `maintenance-plan/${encodeURIComponent(vessel)}`
export async function snapshot(vessel: string, signal: AbortSignal) {
  const value = await request<Snapshot>(equipmentPath(vessel), signal)
  if (value?.complete !== true || !Array.isArray(value.items) || typeof value.canWrite !== 'boolean' ||
    value.items.some(x => x.vesselId !== vessel || typeof x.id !== 'string' || typeof x.version !== 'number' || x.version < 1)) throw new ApiError(502, 'invalid_contract')
  return value
}
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const positive = (value: unknown) => typeof value === 'number' && Number.isSafeInteger(value) && value > 0
export function decodeTechnicalPlans(value: unknown): Plans {
  const invalid = (): never => { throw new ApiError(502, 'invalid_technical_plan_contract') }
  if (!object(value) || !Array.isArray(value.items) || typeof value.canWrite !== 'boolean' || typeof value.canReadHistory !== 'boolean') return invalid()
  const ids = new Set<string>()
  for (const plan of value.items) {
    if (!object(plan) || !isGuid(plan.id) || ids.has(plan.id) || !isGuid(plan.assetId) ||
      !(plan.jobId === null || isGuid(plan.jobId)) || !(plan.ownerRoleId === null || isGuid(plan.ownerRoleId)) ||
      !positive(plan.version) || !positive(plan.revision) || !positive(plan.assetRevision) || plan.status !== 'Draft' || typeof plan.provenance !== 'string' ||
      !timestamp(plan.referenceAt) || !timestamp(plan.savedAt) || !object(plan.inputs) || !Object.values(plan.inputs).every(v => v === null || typeof v === 'string') ||
      !object(plan.due) || !['Due', 'NotDue', 'ReviewRequired'].includes(plan.due.state as string) || typeof plan.due.reason !== 'string' || typeof plan.due.algorithm !== 'string' ||
      !(plan.due.nextDueDate === null || typeof plan.due.nextDueDate === 'string' && calendar(plan.due.nextDueDate)) ||
      !(plan.due.nextDueMeter === null || typeof plan.due.nextDueMeter === 'string' && plan.due.nextDueMeter.length <= 100 && /^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$/.test(plan.due.nextDueMeter)) ||
      !(plan.observationState === undefined || typeof plan.observationState === 'string') ||
      !(plan.observationAssessedAt === undefined || plan.observationAssessedAt === null || timestamp(plan.observationAssessedAt))) return invalid()
    ids.add(plan.id)
  }
  return value as Plans
}
export const plans = async (vessel: string, signal: AbortSignal) => decodeTechnicalPlans(await request<unknown>(planPath(vessel), signal))
export const references = (vessel: string, signal: AbortSignal) => request<References>(`${planPath(vessel)}/references`, signal)
export const owners = (vessel: string, signal: AbortSignal) => request<string[]>(`${equipmentPath(vessel)}/owners`, signal)
export const addJob = (vessel: string, body: { id: string; code: string; title: string }, csrf: string, signal: AbortSignal) =>
  request<{ id: string }>(`${planPath(vessel)}/jobs`, signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf }, body: JSON.stringify(body) })
export const history = (vessel: string, id: string, plan: boolean, signal: AbortSignal) => request<unknown[]>(`${plan ? planPath(vessel) : equipmentPath(vessel)}/${encodeURIComponent(id)}/history`, signal)
export function save(vessel: string, plan: boolean, body: EquipmentCommand | PlanCommand, csrf: string, key: string, signal: AbortSignal) {
  return request<Saved>(plan ? planPath(vessel) : equipmentPath(vessel), signal, { method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, body: JSON.stringify(body) })
}
