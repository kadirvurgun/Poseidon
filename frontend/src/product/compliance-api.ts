import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { decodePassportCertificates, type PassportCertificates } from './compliance-certificate-contract'
import { decodeRow, type Row } from './control-evidence-api'
import { decodeEvaluationResults, validEvaluationSnapshot, type EvaluationResult, type Evaluation } from './applicability-api'

export const onboardingFields = ['vessel.code', 'vessel.name', 'profile.imo', 'profile.flag', 'profile.classSociety', 'profile.classNumber', 'profile.shipType', 'profile.gt', 'profile.buildDate', 'profile.tradingArea', 'profile.cargoContext'] as const
export type OnboardingField = typeof onboardingFields[number]
export type OnboardingBlocker = { subjectId: string; fieldPath: OnboardingField | 'profile'; reasonCode: 'PROFILE_MISSING' | 'REQUIRED_DATA_MISSING' | 'REQUIRED_DATA_UNKNOWN' | 'VERIFICATION_NOT_ESTABLISHED'; resolutionType: 'PROFILE_DECLARATION' | 'REQUIRED_DECLARATION' | 'POLICY_OR_EVIDENCE' | 'QUALIFIED_EVIDENCE' }
export type OnboardingAssessment = { vesselId: string; profileRevision: number | null; savedAt: string | null; provenance: string; freshness: 'Unverified'; state: 'REVIEW_REQUIRED' | 'DECLARED_COMPLETE_UNVERIFIED'; fields: { key: OnboardingField; state: 'Known' | 'Missing' | 'Unknown'; critical: true; complete: boolean }[]; blockers: string[]; blockerDetails: OnboardingBlocker[]; canEdit: boolean; canSubmit: false; source: '18.01/Backlog/row:40;59.01/paragraph:55' }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const positive = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const text = (v: unknown) => typeof v === 'string' && v.length > 0 && v.length <= 200 && v.trim() === v && !/\p{Cc}/u.test(v)
function fail(): never { throw new ApiError(502, 'invalid_compliance_contract') }
export function decodeOnboarding(value: unknown, vessel: string): OnboardingAssessment {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'profileRevision', 'savedAt', 'provenance', 'freshness', 'state', 'fields', 'blockers', 'blockerDetails', 'canEdit', 'canSubmit', 'source']) || value.vesselId !== vessel || !(value.profileRevision === null || positive(value.profileRevision)) || (value.profileRevision === null ? value.savedAt !== null : !timestamp(value.savedAt)) || !text(value.provenance) || value.freshness !== 'Unverified' || typeof value.state !== 'string' || !['REVIEW_REQUIRED', 'DECLARED_COMPLETE_UNVERIFIED'].includes(value.state) || !Array.isArray(value.fields) || value.fields.length !== onboardingFields.length || typeof value.canEdit !== 'boolean' || value.canSubmit !== false || value.source !== '18.01/Backlog/row:40;59.01/paragraph:55') fail()
  if (!value.fields.every(field => exact(field, ['key', 'state', 'critical', 'complete']) && typeof field.key === 'string' && onboardingFields.some(key => key === field.key) && typeof field.state === 'string' && ['Known', 'Missing', 'Unknown'].includes(field.state) && field.critical === true && typeof field.complete === 'boolean' && field.complete === (field.state === 'Known')) ||
    new Set(value.fields.map(field => field.key)).size !== onboardingFields.length || value.state !== (value.fields.every(field => field.complete) ? 'DECLARED_COMPLETE_UNVERIFIED' : 'REVIEW_REQUIRED') || !Array.isArray(value.blockers) || !value.blockers.includes('VERIFICATION_NOT_ESTABLISHED') || value.blockers.some(code => !['PROFILE_MISSING', 'REQUIRED_DATA_MISSING', 'REQUIRED_DATA_UNKNOWN', 'VERIFICATION_NOT_ESTABLISHED'].includes(code)) || new Set(value.blockers).size !== value.blockers.length || !Array.isArray(value.blockerDetails) || value.blockerDetails.length < 1 || value.blockerDetails.length > onboardingFields.length + 2) fail()
  const details = value.blockerDetails
  if (!details.every(detail => exact(detail, ['subjectId', 'fieldPath', 'reasonCode', 'resolutionType']) && detail.subjectId === vessel &&
      typeof detail.fieldPath === 'string' && (detail.fieldPath === 'profile' || onboardingFields.some(key => key === detail.fieldPath)) &&
      typeof detail.reasonCode === 'string' && ['PROFILE_MISSING', 'REQUIRED_DATA_MISSING', 'REQUIRED_DATA_UNKNOWN', 'VERIFICATION_NOT_ESTABLISHED'].includes(detail.reasonCode) &&
      typeof detail.resolutionType === 'string' && ['PROFILE_DECLARATION', 'REQUIRED_DECLARATION', 'POLICY_OR_EVIDENCE', 'QUALIFIED_EVIDENCE'].includes(detail.resolutionType) &&
      (detail.fieldPath === 'profile'
        ? detail.reasonCode === 'PROFILE_MISSING' && detail.resolutionType === 'PROFILE_DECLARATION' || detail.reasonCode === 'VERIFICATION_NOT_ESTABLISHED' && detail.resolutionType === 'QUALIFIED_EVIDENCE'
        : detail.reasonCode === 'REQUIRED_DATA_MISSING' && detail.resolutionType === 'REQUIRED_DECLARATION' || detail.reasonCode === 'REQUIRED_DATA_UNKNOWN' && detail.resolutionType === 'POLICY_OR_EVIDENCE')) ||
      new Set(details.map(detail => `${detail.fieldPath}\0${detail.reasonCode}`)).size !== details.length ||
      new Set(details.map(detail => detail.reasonCode)).size !== value.blockers.length || value.blockers.some(code => !details.some(detail => detail.reasonCode === code)) ||
      details.filter(detail => detail.reasonCode === 'VERIFICATION_NOT_ESTABLISHED' && detail.fieldPath === 'profile' && detail.resolutionType === 'QUALIFIED_EVIDENCE').length !== 1 ||
      (value.profileRevision === null) !== details.some(detail => detail.reasonCode === 'PROFILE_MISSING' && detail.fieldPath === 'profile' && detail.resolutionType === 'PROFILE_DECLARATION') ||
      !value.fields.every(field => field.complete ? !details.some(detail => detail.fieldPath === field.key) : details.some(detail => detail.fieldPath === field.key && detail.reasonCode === (field.state === 'Unknown' ? 'REQUIRED_DATA_UNKNOWN' : 'REQUIRED_DATA_MISSING') && detail.resolutionType === (field.state === 'Unknown' ? 'POLICY_OR_EVIDENCE' : 'REQUIRED_DECLARATION')))) fail()
  return value as OnboardingAssessment
}
export const loadOnboarding = async (vessel: string, signal: AbortSignal) => {
  if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context')
  return decodeOnboarding(await request<unknown>(`vessels/${vessel}/compliance-onboarding`, signal), vessel)
}

export type ApplicabilityReview = { available: boolean; evaluationId: string | null; profileRevision: number | null; evaluatedAt: string | null; current: boolean; results: EvaluationResult[]; conflicts: Evaluation['conflicts']; blockers: string[]; reviewerState: 'QUALIFIED_CURRENT' | 'REVIEW_REQUIRED'; manualOverrideAvailable: false; href: string; inputs: Record<string, unknown> | null; inputHash: string | null; ruleSetHash: string | null }
export type ControlPlan = { coverage: 'CURRENT_QUALIFIED_CATALOG_ONLY' | 'UNAVAILABLE'; requirements: { ruleId: string; ruleRevision: number; requirementId: string | null; requirementRevision: number | null; sourceId: string | null; sourceRevision: number | null; controlIds: string[]; state: 'LINKED' | 'REVIEW_REQUIRED'; blockers: string[] }[]; controls: Row[]; evidence: Row[]; hasMore: boolean; uncontrolledRequirementCount: number | null; orphanCriticalCount: null; blockers: string[] }
// DEV-042: freshness stays UNKNOWN without a published source-freshness policy; qualifiedAt is a recorded qualification time.
export type PassportSource = { sourceId: string; sourceRevision: number; ruleIds: string[]; qualifiedAt: string | null; freshness: 'UNKNOWN'; blockers: string[]; href: string }
export type CompliancePassport = { vesselId: string; readAt: string; state: 'REVIEW_REQUIRED'; freshness: 'Unverified'; certificateCoverage: PassportCertificates['coverage']; certificates: PassportCertificates; blockers: string[]; onboarding: OnboardingAssessment; applicability: ApplicabilityReview; plan: ControlPlan; sources: PassportSource[] }
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 4096 && v.every(code => typeof code === 'string' && code.length > 0 && code.length <= 200 && !/\p{Cc}/u.test(code))
export function decodeApplicabilityReview(value: unknown, vessel: string): ApplicabilityReview {
  if (!exact(value, ['available', 'evaluationId', 'profileRevision', 'evaluatedAt', 'current', 'results', 'conflicts', 'blockers', 'reviewerState', 'manualOverrideAvailable', 'href', 'inputs', 'inputHash', 'ruleSetHash']) || typeof value.available !== 'boolean' || typeof value.current !== 'boolean' || value.manualOverrideAvailable !== false || value.href !== `/app/fleet/${vessel}/passport#applicability` || !codes(value.blockers) || !Array.isArray(value.results) || !Array.isArray(value.conflicts) || value.conflicts.length > 4096 ||
    value.current && !value.available || value.available !== (value.evaluationId !== null) || (value.evaluationId !== null ? !isGuid(value.evaluationId) || !timestamp(value.evaluatedAt) || !(value.profileRevision === null || positive(value.profileRevision)) : value.evaluatedAt !== null || value.profileRevision !== null || value.current !== false || value.results.length !== 0 || value.conflicts.length !== 0) || value.reviewerState !== (value.current && value.available ? 'QUALIFIED_CURRENT' : 'REVIEW_REQUIRED')) fail()
  const hash = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
  if (value.evaluationId === null ? value.inputs !== null || value.inputHash !== null || value.ruleSetHash !== null : !object(value.inputs) || !timestamp(value.inputs.asOf) || JSON.stringify(value.inputs).length > 65536 || !hash(value.inputHash) || !hash(value.ruleSetHash) || !validEvaluationSnapshot(value.inputs, vessel, value.inputs.asOf, value.profileRevision, Object.hasOwn(value.inputs, 'qualifications'))) fail()
  if (value.results.length > 64 || value.evaluationId !== null && value.results.length === 0) fail()
  for (const row of value.results) { if (!object(row) || !object(row.prediction) || typeof row.prediction.authoritative !== 'boolean') fail(); decodeEvaluationResults([row], row.prediction.authoritative) }
  for (const conflict of value.conflicts) if (!exact(conflict, ['code', 'target', 'ruleReferences', 'status']) || typeof conflict.code !== 'string' || !['CANDIDATE_SOURCE_CONTRADICTION', 'INPUT_SOURCE_CONFLICT'].includes(conflict.code) || typeof conflict.target !== 'string' || conflict.target.length > 500 || !Array.isArray(conflict.ruleReferences) || conflict.ruleReferences.length > 64 || conflict.ruleReferences.some(ref => typeof ref !== 'string' || !/^[0-9a-f-]{36}\/[1-9][0-9]*$/.test(ref)) || conflict.status !== 'REVIEW_REQUIRED') fail()
  if (value.current && (!value.results.length || value.conflicts.length || value.blockers.length || value.results.some(row => !row.prediction.authoritative || !['APPLICABLE', 'NOT_APPLICABLE'].includes(row.prediction.result) || row.sourceId === null || row.requirementId === null))) fail()
  return value as ApplicabilityReview
}
function decodePlan(value: unknown, vessel: string): ControlPlan {
  if (!exact(value, ['coverage', 'requirements', 'controls', 'evidence', 'hasMore', 'uncontrolledRequirementCount', 'orphanCriticalCount', 'blockers']) || typeof value.coverage !== 'string' || !['CURRENT_QUALIFIED_CATALOG_ONLY', 'UNAVAILABLE'].includes(value.coverage) || !Array.isArray(value.requirements) || value.requirements.length > 64 || !Array.isArray(value.controls) || value.controls.length > 25 || !Array.isArray(value.evidence) || value.evidence.length > 25 || typeof value.hasMore !== 'boolean' || value.orphanCriticalCount !== null || !(value.uncontrolledRequirementCount === null || typeof value.uncontrolledRequirementCount === 'number' && Number.isSafeInteger(value.uncontrolledRequirementCount) && value.uncontrolledRequirementCount >= 0) || value.coverage === 'UNAVAILABLE' && value.uncontrolledRequirementCount !== null || !codes(value.blockers)) fail()
  for (const row of value.requirements) if (!exact(row, ['ruleId', 'ruleRevision', 'requirementId', 'requirementRevision', 'sourceId', 'sourceRevision', 'controlIds', 'state', 'blockers']) || !isGuid(row.ruleId) || !positive(row.ruleRevision) || !(row.requirementId === null && row.requirementRevision === null || isGuid(row.requirementId) && positive(row.requirementRevision)) || !(row.sourceId === null && row.sourceRevision === null || isGuid(row.sourceId) && positive(row.sourceRevision)) || !Array.isArray(row.controlIds) || row.controlIds.length > 25 || !row.controlIds.every(isGuid) || new Set(row.controlIds).size !== row.controlIds.length || typeof row.state !== 'string' || !['LINKED', 'REVIEW_REQUIRED'].includes(row.state) || row.state === 'LINKED' && row.controlIds.length === 0 || !codes(row.blockers)) fail()
  return { ...value, controls: value.controls.map(row => decodeRow(row, vessel, 'Control')), evidence: value.evidence.map(row => decodeRow(row, vessel, 'Evidence')) } as ControlPlan
}
export function decodePassport(value: unknown, vessel: string, expectedOffset?: number): CompliancePassport {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'readAt', 'state', 'freshness', 'certificateCoverage', 'blockers', 'onboarding', 'applicability', 'plan', 'certificates', 'sources']) || value.vesselId !== vessel || !timestamp(value.readAt) || value.state !== 'REVIEW_REQUIRED' || value.freshness !== 'Unverified' || !codes(value.blockers)) fail()
  const certificates = decodePassportCertificates(value.certificates, vessel, expectedOffset)
  if (certificates.coverage !== value.certificateCoverage || certificates.referenceDate !== String(value.readAt).slice(0, 10)) fail()
  if (!Array.isArray(value.sources) || value.sources.length > 64 || !value.sources.every(source => exact(source, ['sourceId', 'sourceRevision', 'ruleIds', 'qualifiedAt', 'freshness', 'blockers', 'href']) &&
    isGuid(source.sourceId) && positive(source.sourceRevision) && Array.isArray(source.ruleIds) && source.ruleIds.length > 0 && source.ruleIds.length <= 64 && source.ruleIds.every(isGuid) &&
    (source.qualifiedAt === null || timestamp(source.qualifiedAt)) && source.freshness === 'UNKNOWN' && codes(source.blockers) && source.blockers.includes('SOURCE_FRESHNESS_POLICY_UNAVAILABLE') &&
    source.href === `/app/sources/${source.sourceId}`) || (value.sources.length === 0 ? !value.blockers.includes('SOURCE_FRESHNESS_UNAVAILABLE') : !value.blockers.includes('SOURCE_FRESHNESS_POLICY_UNAVAILABLE'))) fail()
  return { ...value, certificates, onboarding: decodeOnboarding(value.onboarding, vessel), applicability: decodeApplicabilityReview(value.applicability, vessel), plan: decodePlan(value.plan, vessel) } as CompliancePassport
}
export const loadPassport = async (vessel: string, offset: number, signal: AbortSignal) => {
  if (!isGuid(vessel) || !Number.isSafeInteger(offset) || offset < 0 || offset > 100000) throw new ApiError(400, 'invalid_context')
  return decodePassport(await request<unknown>(`vessels/${vessel}/compliance-passport?offset=${offset}`, signal), vessel, offset)
}
