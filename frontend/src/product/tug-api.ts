import type { Plan, PlanningEvidence } from './tug-planning-api'
import { decodePlanningEvidence } from './tug-planning-api'
import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { validDecimal } from './profile-api'

export type TugInputs = {
  requestSource: string | null; customerRef: string | null; assistedVessel: string | null; assistedVesselImo: string | null; jobType: string | null
  requestedStart: string | null; requestedEnd: string | null; operationArea: string | null; pickupPoint: string | null
  destinationPoint: string | null; requiredBollardPull: string | null; requiredTugCount: number | null
  specialRequirements: string | null; priority: string | null; requestOwner: string | null; notes: string | null
}
export type TugRequest = { id: string; vesselId: string; jobNumber: string; revision: number; version: number; inputs: TugInputs; provenance: string; savedAt: string; missing: string[]; replayed: boolean; sourceVersion: string }
export type TugContext = {
  vessel: { id: string; code: string; name: string }; mode: 'Tug'; crossMode: boolean; roleReferences: string | null
  tugTypeContext: { tenantId: string; vesselId: string; code: string; sourceReference: string; sourceRevision: number; observedAt: string } | null
  tugTypeOptions?: { code: string; tr: string; en: string }[]; tugTypePolicyVersion?: string | null; canWrite: boolean; owners: string[]; requestSources: string[]; jobTypes: string[]; priorities: string[]; tugType: string
  bollardPull: { value: string | null; version: number | null; provenance: string; freshness: string; observedAt: string | null; source: 'TugProfile' }
  equipment: { id: string; level: string; equipmentType: string | null; revision: number; provenance: string }[] | null
  equipmentCoverage: string; readiness: string; reason: string
}
export type TugBoard = { items: { draftPlan?: Plan | null; planningEvidence?: PlanningEvidence | null; request: TugRequest; windowState: string; plannedTimeSource: string; assignedTugs: string[]; assignmentCoverage: string; availableBollardPull: string | null; readiness: string; conflict: string; declaredComparisons: { jobId: string; relation: string }[]; comparisonCoverage: string; blockingReasons: string[]; nextAction: string; detailHref: string }[]; hasMore: boolean }
export const emptyTugInputs = (): TugInputs => ({ requestSource: null, customerRef: null, assistedVessel: null, assistedVesselImo: null, jobType: null, requestedStart: null, requestedEnd: null, operationArea: null, pickupPoint: null, destinationPoint: null, requiredBollardPull: null, requiredTugCount: null, specialRequirements: null, priority: null, requestOwner: null, notes: null })
export type TugAttempt = { key: string; body: { clientId: string; baseVersion: number | null; inputs: TugInputs }; update: boolean }
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, keys: string[]): value is Record<string, unknown> => object(value) && Object.keys(value).length === keys.length && keys.every(k => Object.hasOwn(value, k))
const contextText = (value: unknown, max = 200) => typeof value === 'string' && value.length > 0 && value.length <= max && value.trim() === value && !/\p{Cc}/u.test(value)
const positive = (value: unknown) => typeof value === 'number' && Number.isSafeInteger(value) && value > 0
export const tugRequestSource = 'poseidon-v46.3-20261030/14.02/1914-1931/TUG-FOUNDATION-01'
export function decodeTugRequest(value: unknown, vessel: string, id?: string): TugRequest {
  const fail = (): never => { throw new ApiError(502, 'invalid_tug_request_contract') }
  if (!isGuid(vessel) || !exact(value, ['id', 'vesselId', 'jobNumber', 'revision', 'version', 'inputs', 'provenance', 'savedAt', 'missing', 'replayed', 'sourceVersion']) ||
    !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel || !contextText(value.jobNumber, 100) || !positive(value.revision) ||
    !positive(value.version) || Number(value.version) > 4294967295 || !['UserDeclaredUnverified', 'SyntheticUnverified'].includes(String(value.provenance)) ||
    !timestamp(value.savedAt) || typeof value.replayed !== 'boolean' || value.sourceVersion !== tugRequestSource || !Array.isArray(value.missing) || value.missing.length > 17 ||
    !value.missing.every(key => typeof key === 'string' && /^[a-z_]{1,64}$/.test(key)) || new Set(value.missing).size !== value.missing.length ||
    !exact(value.inputs, Object.keys(emptyTugInputs()))) fail()
  for (const [key, input] of Object.entries(value.inputs as Record<string, unknown>)) {
    if (input === null) continue
    if (key === 'requiredTugCount') { if (!positive(input) || Number(input) > 1000) fail() }
    else if (key === 'requestOwner') { if (!isGuid(input)) fail() }
    else if (key === 'requestedStart' || key === 'requestedEnd') { if (!timestamp(input) || !/(?:Z|\+00:00)$/.test(String(input))) fail() }
    else if (key === 'requiredBollardPull') { if (typeof input !== 'string' || !/^(0|[1-9][0-9]{0,7})(\.[0-9]{1,2})?$/.test(input)) fail() }
    else if (typeof input !== 'string' || input.length === 0 || input.length > (['notes', 'specialRequirements'].includes(key) ? 2000 : 200) ||
      (['notes', 'specialRequirements'].includes(key) ? /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]/.test(input) : /\p{Cc}/u.test(input))) fail()
  }
  return value as TugRequest
}
export function decodeTugContext(value: unknown, vessel: string, tenant?: string): TugContext {
  function fail(): never { throw new ApiError(502, 'invalid_tug_context') }
  if (!isGuid(vessel) || !exact(value, ['vessel', 'mode', 'crossMode', 'roleReferences', 'canWrite', 'owners', 'bollardPull', 'tugType', 'tugTypeContext', 'tugTypeOptions', 'tugTypePolicyVersion', 'equipment', 'equipmentCoverage', 'readiness', 'reason', 'jobTypes', 'priorities', 'requestSources']) || !exact(value.vessel, ['id', 'code', 'name']) || value.vessel.id !== vessel || !contextText(value.vessel.code) || !contextText(value.vessel.name) || value.mode !== 'Tug' || typeof value.crossMode !== 'boolean' || typeof value.canWrite !== 'boolean' || !(value.roleReferences === null || contextText(value.roleReferences, 4096)) || !Array.isArray(value.owners) || !value.owners.every(isGuid) || new Set(value.owners).size !== value.owners.length || value.readiness !== 'Unknown' || value.reason !== 'VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable') fail()
  const bp = value.bollardPull
  if (!exact(bp, ['value', 'version', 'provenance', 'source', 'observedAt', 'freshness']) || bp.source !== 'TugProfile' || bp.freshness !== 'Unknown' || bp.observedAt !== null || !(bp.value === null && bp.version === null && bp.provenance === 'Missing' || validDecimal(bp.value) && positive(bp.version) && Number(bp.version) <= 4294967295 && bp.provenance === 'LegacyMeasurementUnverified')) fail()
  if (!Array.isArray(value.tugTypeOptions) || value.tugTypeOptions.length > 128 || value.tugTypeOptions.some(option => !exact(option, ['code', 'tr', 'en']) || !contextText(option.code, 64) || !contextText(option.tr, 100) || !contextText(option.en, 100)) || new Set(value.tugTypeOptions.map(option => option.code)).size !== value.tugTypeOptions.length || !(value.tugTypePolicyVersion === null || contextText(value.tugTypePolicyVersion, 80))) fail()
  const binding = value.tugTypeContext
  if (binding === null ? value.tugType !== 'Unavailable' : !exact(binding, ['tenantId', 'vesselId', 'code', 'sourceReference', 'sourceRevision', 'observedAt']) || !isGuid(binding.tenantId) || tenant !== undefined && binding.tenantId !== tenant || binding.vesselId !== vessel || binding.code !== value.tugType || !value.tugTypeOptions.some(option => option.code === binding.code) || !contextText(binding.sourceReference, 80) || !positive(binding.sourceRevision) || !timestamp(binding.observedAt) || value.tugTypePolicyVersion === null) fail()
  if (value.equipment === null ? value.equipmentCoverage !== 'Unavailable' : !Array.isArray(value.equipment) || value.equipmentCoverage !== 'DraftDeclarationsOnly' || value.equipment.some(asset => !exact(asset, ['id', 'level', 'equipmentType', 'revision', 'provenance']) || !isGuid(asset.id) || !contextText(asset.level, 64) || !(asset.equipmentType === null || contextText(asset.equipmentType, 200)) || !positive(asset.revision) || !contextText(asset.provenance, 100)) || new Set(value.equipment.map(asset => asset.id)).size !== value.equipment.length) fail()
  for (const key of ['jobTypes', 'priorities', 'requestSources']) { const options = value[key]; if (!Array.isArray(options) || options.length > 128 || !options.every(item => contextText(item, 100)) || new Set(options).size !== options.length) fail() }
  return value as TugContext
}
export const tugApi = {
  context: async (vessel: string, signal: AbortSignal, tenant?: string) => decodeTugContext(await request<unknown>(`tug-command?vessel=${vessel}`, signal), vessel, tenant),
  vessels: (signal: AbortSignal) => request<{ vessels: TugContext['vessel'][] }>('tug-command', signal),
  detail: async (id: string, vessel: string, signal: AbortSignal) => decodeTugRequest(await request<unknown>(`tug-jobs/${id}?vessel=${vessel}`, signal), vessel, id),
  board: async (vessel: string, priority: string, q: string, offset: number, signal: AbortSignal) => {
    const result = await request<TugBoard>(`tug-job-board?${new URLSearchParams({ vessel, priority, q, offset: String(offset), limit: '25' })}`, signal)
    if (!result || !Array.isArray(result.items) || result.items.length > 25 || typeof result.hasMore !== 'boolean') throw new ApiError(502, 'invalid_tug_board_contract')
    const ids = new Set<string>()
    for (const row of result.items) {
      decodeTugRequest(row?.request, vessel)
      if (!row || !isGuid(row.request?.id) || row.request.vesselId !== vessel || ids.has(row.request.id) ||
        row.detailHref !== `/app/tug-job-detail/${row.request.id}?vessel=${vessel}` ||
        !Array.isArray(row.assignedTugs) || !row.assignedTugs.every(isGuid) || new Set(row.assignedTugs).size !== row.assignedTugs.length ||
        !Array.isArray(row.blockingReasons) || !row.blockingReasons.every(reason => typeof reason === 'string') ||
        !Array.isArray(row.declaredComparisons) || !row.declaredComparisons.every(item => item && isGuid(item.jobId) && item.jobId !== row.request.id && ['Unknown', 'Separate', 'BoundaryTouch', 'Intersecting', 'InvalidWindow'].includes(item.relation))) throw new ApiError(502, 'invalid_tug_board_contract')
      ids.add(row.request.id)
      if (row.planningEvidence != null) {
        const current = decodePlanningEvidence(row.planningEvidence, row.assignedTugs)
        if (row.availableBollardPull !== current.availableBollardPull || row.readiness !== current.readiness || row.conflict !== current.conflictState) throw new ApiError(502, 'invalid_tug_board_contract')
      } else if (row.availableBollardPull !== null || row.readiness !== 'Unknown' || row.conflict !== 'Unknown') throw new ApiError(502, 'invalid_tug_board_contract')
    }
    return result
  },
  save: async (vessel: string, attempt: TugAttempt, csrf: string, signal: AbortSignal) => decodeTugRequest(await request<unknown>(`tug-jobs${attempt.update ? `/${attempt.body.clientId}` : ''}?vessel=${vessel}`, signal, {
    method: attempt.update ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify(attempt.body),
  }), vessel, attempt.body.clientId),
}
