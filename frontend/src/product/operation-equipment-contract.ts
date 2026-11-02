import { isGuid } from './identifiers'

export type OperationReference = { jobId: string | null; jobRevision: number | null; jobVersion: number | null; planRevision: number | null; planVersion: number | null; profile: string | null; serviceAuthorizationId: string | null; serviceAuthorizationRevision: number | null }
export type ServiceAuthorization = { jobId: string | null; jobRevision: number | null; jobVersion: number | null; planRevision: number | null; planVersion: number | null; serviceContext: string | null; issuerReference: string | null }
export type CargoPlan = { planReference: string | null; packagedDangerousGoods: boolean | null }
export type WinchFitment = { assetId: string | null; assetRevision: number | null; assetVersion: number | null; fitted: boolean | null; registerReference: string | null }
export type OwningContent = { operation?: OperationReference | null; serviceAuthorization?: ServiceAuthorization | null; cargoPlan?: CargoPlan | null; winchFitment?: WinchFitment | null }
export type LiteralBinding = { field: string; ownerValue: string | null; ruleValue: string | null; sourceLocator: string | null }
export const owningFields = ['vessel.operatingArea', 'operation.jobType', 'operation.profile', 'operation.serviceContext', 'cargo.packagedDangerousGoods', 'equipment.towingWinchFitted']
type Obj = Record<string, unknown>
const exact = (v: unknown, keys: string[]): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const positive = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const text = (v: unknown) => v === null || typeof v === 'string' && v.length > 0 && v.length <= 2000 && v.trim() === v && !Array.from(v).some(c => c.charCodeAt(0) < 32 || c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159)
export function validOwning(key: string, v: unknown): boolean {
  if (v === null) return true
  if (key === 'cargoPlan') return exact(v, ['planReference', 'packagedDangerousGoods']) && text(v.planReference) && (v.packagedDangerousGoods === null || typeof v.packagedDangerousGoods === 'boolean')
  if (key === 'winchFitment') return exact(v, ['assetId', 'assetRevision', 'assetVersion', 'fitted', 'registerReference']) && text(v.registerReference) && (v.fitted === null || typeof v.fitted === 'boolean') &&
    (v.assetId === null && v.assetRevision === null && v.assetVersion === null || isGuid(v.assetId) && positive(v.assetRevision) && positive(v.assetVersion) && v.assetVersion <= 4294967295)
  const operation = key === 'operation'
  if (!operation && key !== 'serviceAuthorization' || !exact(v, ['jobId', 'jobRevision', 'jobVersion', 'planRevision', 'planVersion', ...(operation ? ['profile', 'serviceAuthorizationId', 'serviceAuthorizationRevision'] : ['serviceContext', 'issuerReference'])])) return false
  if (!(v.jobId === null && v.jobRevision === null && v.jobVersion === null || isGuid(v.jobId) && positive(v.jobRevision) && positive(v.jobVersion) && v.jobVersion <= 4294967295) ||
    !(v.planRevision === null && v.planVersion === null || v.jobId !== null && positive(v.planRevision) && positive(v.planVersion) && v.planVersion <= 4294967295)) return false
  return operation ? text(v.profile) && (v.serviceAuthorizationId === null && v.serviceAuthorizationRevision === null || isGuid(v.serviceAuthorizationId) && positive(v.serviceAuthorizationRevision)) : text(v.serviceContext) && text(v.issuerReference)
}
export function validBindings(v: unknown): v is LiteralBinding[] {
  return Array.isArray(v) && v.length <= 256 && v.every(b => exact(b, ['field', 'ownerValue', 'ruleValue', 'sourceLocator']) && owningFields.slice(0, 4).includes(b.field as string) && [b.ownerValue, b.ruleValue, b.sourceLocator].every(text)) && new Set(v.filter(b => b.ownerValue !== null).map(b => `${b.field}/${b.ownerValue}`)).size === v.filter(b => b.ownerValue !== null).length
}
