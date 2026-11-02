import { isGuid } from './identifiers'
import { calendar } from './date-validation'

export type ClassPack = { packReference: string | null; edition: string | null; surveyReference: string | null; iacsM79: boolean | null }
export type Msmd = { documentReference: string | null; issuerReference: string | null; roles: { positionId: string; count: number; sourceLocator: string }[] | null }
export type RoleAssessment = { assignmentId: string | null; assignmentRevision: number | null; assignmentVersion: number | null; positionId: string | null; positionVersion: number | null; from: string | null; to: string | null; criterion: string | null; recordReference: string | null; satisfied: boolean | null; credential: { id: string; revision: number; version: number } | null }
export type ComplianceContent = { classPack?: ClassPack | null; msmd?: Msmd | null; crewRoleAssessment?: RoleAssessment | null }
export type IsmInput = { evaluationId: string; ruleId: string; ruleRevision: number }
export type OwningReferences = { operationId: string | null; cargoPlanId: string | null; winchFitmentId: string | null; classPackId?: string | null; msmdId?: string | null; roleAssessmentIds?: string[] | null; sourceEvidenceId?: string | null; ism?: IsmInput | null }
export type CompliancePolicies = { classPack?: { packReference: string | null; edition: string | null; sourceLocator: string | null } | null; ismDependency?: { ruleId: string | null; ruleRevision: number | null; sourceLocator: string | null } | null; eligibility?: { positionId: string | null; sourceLocator: string | null; criteria: { code: string | null; ruleId: string | null; ruleRevision: number | null; sourceLocator: string | null; requireCredential: boolean | null }[] | null } | null }
type Obj = Record<string, unknown>
const exact = (v: unknown, keys: string[]): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const text = (v: unknown) => v === null || typeof v === 'string' && v.length > 0 && v.length <= 2000 && v.trim() === v && !/[\u0000-\u001f\u007f-\u009f]/.test(v)
const positive = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const bool = (v: unknown) => v === null || typeof v === 'boolean'
export const complianceKeys = ['classPack', 'msmd', 'crewRoleAssessment'] as const
export const complianceFields = ['class.rulePack.IACS_M79', 'regulatory.ismApplicable', 'regulatory.msmdVerified', 'crew.requiredRoleEligible', 'evidence.sourceVerified']
export function validCompliance(k: string, v: unknown): boolean {
  if (v === null) return true
  if (k === 'classPack') return exact(v, ['packReference', 'edition', 'surveyReference', 'iacsM79']) && [v.packReference, v.edition, v.surveyReference].every(text) && bool(v.iacsM79)
  if (k === 'msmd') return exact(v, ['documentReference', 'issuerReference', 'roles']) && [v.documentReference, v.issuerReference].every(text) && (v.roles === null || Array.isArray(v.roles) && v.roles.length <= 128 && v.roles.every(r => exact(r, ['positionId', 'count', 'sourceLocator']) && isGuid(r.positionId) && positive(r.count) && r.count <= 1000 && r.sourceLocator !== null && text(r.sourceLocator)) && new Set(v.roles.map(r => r.positionId)).size === v.roles.length)
  return k === 'crewRoleAssessment' && exact(v, ['assignmentId', 'assignmentRevision', 'assignmentVersion', 'positionId', 'positionVersion', 'from', 'to', 'criterion', 'recordReference', 'satisfied', 'credential']) &&
    (v.assignmentId === null && v.assignmentRevision === null && v.assignmentVersion === null || isGuid(v.assignmentId) && positive(v.assignmentRevision) && positive(v.assignmentVersion) && v.assignmentVersion <= 4294967295) &&
    (v.positionId === null && v.positionVersion === null || isGuid(v.positionId) && positive(v.positionVersion) && v.positionVersion <= 4294967295) &&
    (v.from === null && v.to === null || typeof v.from === 'string' && calendar(v.from) && typeof v.to === 'string' && calendar(v.to) && (v.from as string) < (v.to as string)) && [v.criterion, v.recordReference].every(text) && bool(v.satisfied) && (v.credential === null || exact(v.credential, ['id', 'revision', 'version']) && isGuid(v.credential.id) && positive(v.credential.revision) && positive(v.credential.version) && v.credential.version <= 4294967295)
}
export function validCompliancePolicies(v: Obj): boolean {
  const p = v.classPack, i = v.ismDependency, e = v.eligibility
  if (p != null && !(exact(p, ['packReference', 'edition', 'sourceLocator']) && Object.values(p).every(text))) return false
  if (i != null && !(exact(i, ['ruleId', 'ruleRevision', 'sourceLocator']) && (i.ruleId === null && i.ruleRevision === null || isGuid(i.ruleId) && positive(i.ruleRevision)) && text(i.sourceLocator))) return false
  return e == null || exact(e, ['positionId', 'sourceLocator', 'criteria']) && (e.positionId === null || isGuid(e.positionId)) && text(e.sourceLocator) && (e.criteria === null || Array.isArray(e.criteria) && e.criteria.length <= 32 && e.criteria.every(c => exact(c, ['code', 'ruleId', 'ruleRevision', 'sourceLocator', 'requireCredential']) && text(c.code) && text(c.sourceLocator) && bool(c.requireCredential) && (c.ruleId === null && c.ruleRevision === null || isGuid(c.ruleId) && positive(c.ruleRevision))) && new Set(e.criteria.map(c => c.code)).size === e.criteria.length)
}
export function validOwningReferences(v: unknown): v is OwningReferences {
  const base = ['operationId', 'cargoPlanId', 'winchFitmentId'], optional = ['classPackId', 'msmdId', 'roleAssessmentIds', 'sourceEvidenceId', 'ism']
  if (!exact(v, [...base, ...optional.filter(k => !!v && typeof v === 'object' && Object.hasOwn(v, k))])) return false
  if ([...base, 'classPackId', 'msmdId', 'sourceEvidenceId'].some(k => v[k] != null && !isGuid(v[k]))) return false
  if (v.roleAssessmentIds != null && (!Array.isArray(v.roleAssessmentIds) || v.roleAssessmentIds.length > 32 || !v.roleAssessmentIds.every(isGuid) || new Set(v.roleAssessmentIds).size !== v.roleAssessmentIds.length)) return false
  return v.ism == null || exact(v.ism, ['evaluationId', 'ruleId', 'ruleRevision']) && isGuid(v.ism.evaluationId) && isGuid(v.ism.ruleId) && positive(v.ism.ruleRevision)
}
