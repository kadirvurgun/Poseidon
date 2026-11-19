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
// DEV-081 lifecycle (60.02 §5). Planning/assignment/readiness states are not reachable in this slice.
export const tugJobStates = ['Draft', 'Submitted', 'Cancelled'] as const
export type TugJobState = typeof tugJobStates[number]
export const tugBoardWindows = ['next4h', 'next24h', 'next72h'] as const
export type TugBoardWindow = typeof tugBoardWindows[number]
export type TugRequest = { id: string; vesselId: string; jobNumber: string; revision: number; version: number; inputs: TugInputs; provenance: string; savedAt: string; missing: string[]; replayed: boolean; sourceVersion: string; state: TugJobState }
export type TugLifecycleAttempt = { key: string; operation: 'submit' | 'cancel'; jobId: string; body: { expectedVersion: number; reason?: string } }
// A refused submission keeps the server's exact missing keys; nothing is defaulted client-side.
export class TugLifecycleError extends ApiError {
  constructor(status: number, code: string, public missing: string[] = [], correlationId?: string) { super(status, code, {}, correlationId) }
}
export type TugContext = {
  vessel: { id: string; code: string; name: string }; mode: 'Tug'; crossMode: boolean; roleReferences: string | null
  roleLens: TugRoleLens; shipModeDestinations: { key: TugShipModeKey; href: string }[]
  tugTypeContext: { tenantId: string; vesselId: string; code: string; sourceReference: string; sourceRevision: number; observedAt: string } | null
  tugTypeOptions?: { code: string; tr: string; en: string }[]; tugTypePolicyVersion?: string | null; canWrite: boolean; owners: string[]; requestSources: string[]; jobTypes: string[]; priorities: string[]; tugType: string
  bollardPull: { value: string | null; version: number | null; provenance: string; freshness: string; observedAt: string | null; source: 'TugProfile' }
  equipment: { id: string; level: string; equipmentType: string | null; revision: number; provenance: string }[] | null
  equipmentCoverage: string; readiness: string; reason: string
}
export type TugShipModeKey = 'noon-report' | 'deck-daily-log' | 'engine-daily-log'
export type TugRoleLabel = { reference: string; tr: string; en: string }
export type TugRoleLens = { roleState: 'Configured' | 'Missing' | 'Unavailable'; roleCatalogVersion: string | null; roles: TugRoleLabel[]; lensState: 'Configured' | 'Missing' | 'Unavailable'; lensVersion: string | null; lenses: TugRoleLabel[] }
const shipModeKeys: readonly TugShipModeKey[] = ['noon-report', 'deck-daily-log', 'engine-daily-log']
// DEV-080: role/lens labels come only from the server's provisioned catalog; Missing/Unavailable stay explicit.
function decodeRoleLens(value: unknown, roleReferences: unknown): TugRoleLens {
  function fail(): never { throw new ApiError(502, 'invalid_tug_context') }
  const states = ['Configured', 'Missing', 'Unavailable']
  const labels = (rows: unknown) => Array.isArray(rows) && rows.length <= 64 && rows.every(row => exact(row, ['reference', 'tr', 'en']) && isGuid(row.reference) && contextText(row.tr) && contextText(row.en)) && new Set(rows.map(row => (row as TugRoleLabel).reference)).size === rows.length
  if (!exact(value, ['roleState', 'roleCatalogVersion', 'roles', 'lensState', 'lensVersion', 'lenses']) || !states.includes(String(value.roleState)) || !states.includes(String(value.lensState)) ||
    !labels(value.roles) || !labels(value.lenses) ||
    (value.roleState === 'Configured' ? !contextText(value.roleCatalogVersion, 64) || (value.roles as unknown[]).length === 0 : value.roleCatalogVersion !== null || (value.roles as unknown[]).length !== 0) ||
    (value.lensState === 'Configured' ? !contextText(value.lensVersion, 64) : value.lensVersion !== null || (value.lenses as unknown[]).length !== 0) ||
    value.roleState === 'Missing' && roleReferences !== null ||
    (value.lenses as TugRoleLabel[]).some(lens => !(value.roles as TugRoleLabel[]).some(role => role.reference === lens.reference))) fail()
  if (value.roleState === 'Configured' && (typeof roleReferences !== 'string' || roleReferences.split(',').length !== (value.roles as unknown[]).length ||
    (value.roles as TugRoleLabel[]).some(role => !roleReferences.toLowerCase().split(',').includes(role.reference.toLowerCase())))) fail()
  return value as TugRoleLens
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
  function fail(): never { throw new ApiError(502, 'invalid_tug_request_contract') }
  if (!isGuid(vessel) || !exact(value, ['id', 'vesselId', 'jobNumber', 'revision', 'version', 'inputs', 'provenance', 'savedAt', 'missing', 'replayed', 'sourceVersion', 'state']) ||
    !tugJobStates.includes(value.state as TugJobState) ||
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
      (['notes', 'specialRequirements'].includes(key) ? /\p{Cc}/u.test(input.replace(/[\r\n\t]/g, '')) : /\p{Cc}/u.test(input))) fail()
  }
  return value as TugRequest
}
export function decodeTugContext(value: unknown, vessel: string, tenant?: string): TugContext {
  function fail(): never { throw new ApiError(502, 'invalid_tug_context') }
  if (!isGuid(vessel) || !exact(value, ['vessel', 'mode', 'crossMode', 'roleReferences', 'roleLens', 'shipModeDestinations', 'canWrite', 'owners', 'bollardPull', 'tugType', 'tugTypeContext', 'tugTypeOptions', 'tugTypePolicyVersion', 'equipment', 'equipmentCoverage', 'readiness', 'reason', 'jobTypes', 'priorities', 'requestSources']) || !exact(value.vessel, ['id', 'code', 'name']) || value.vessel.id !== vessel || !contextText(value.vessel.code) || !contextText(value.vessel.name) || value.mode !== 'Tug' || typeof value.crossMode !== 'boolean' || typeof value.canWrite !== 'boolean' || !(value.roleReferences === null || contextText(value.roleReferences, 4096)) || !Array.isArray(value.owners) || !value.owners.every(isGuid) || new Set(value.owners).size !== value.owners.length || value.readiness !== 'Unknown' || value.reason !== 'VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable') fail()
  decodeRoleLens(value.roleLens, value.roleReferences)
  // Ship Mode screens are server-listed only for an explicit cross-mode role, never inferred here.
  const ship = value.shipModeDestinations
  if (!Array.isArray(ship) || ship.length > shipModeKeys.length || !value.crossMode && ship.length !== 0 ||
    ship.some(row => !exact(row, ['key', 'href']) || !shipModeKeys.includes(row.key as TugShipModeKey) || row.href !== `/app/${String(row.key)}/${vessel}`) ||
    new Set(ship.map(row => (row as { key: string }).key)).size !== ship.length) fail()
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
  board: async (vessel: string, priority: string, q: string, offset: number, signal: AbortSignal, state: TugJobState | '' = '', window: TugBoardWindow | '' = '') => {
    const params = new URLSearchParams({ vessel, priority, q, offset: String(offset), limit: '25' })
    if (state) params.set('state', state)
    if (window) params.set('window', window)
    const result = await request<TugBoard>(`tug-job-board?${params}`, signal)
    if (!result || !Array.isArray(result.items) || result.items.length > 25 || typeof result.hasMore !== 'boolean') throw new ApiError(502, 'invalid_tug_board_contract')
    const ids = new Set<string>()
    for (const row of result.items) {
      decodeTugRequest(row?.request, vessel)
      if (state ? row.request.state !== state : row.request.state === 'Cancelled') throw new ApiError(502, 'invalid_tug_board_contract')
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
  lifecycle: async (vessel: string, attempt: TugLifecycleAttempt, csrf: string, signal: AbortSignal) => {
    const response = await fetch(`/api/v1/tug-jobs/${attempt.jobId}/${attempt.operation}?vessel=${vessel}`, { credentials: 'same-origin', signal, method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify(attempt.body) })
    let body: unknown = null
    try { body = await response.json() } catch { /* Proxy errors need not be JSON. */ }
    if (!response.ok) {
      const failure = object(body) ? body : {}
      const missing = Array.isArray(failure.missing) && failure.missing.length <= 17 && failure.missing.every(key => typeof key === 'string' && /^[a-z_]{1,64}$/.test(key)) ? failure.missing as string[] : []
      throw new TugLifecycleError(response.status, typeof failure.code === 'string' ? failure.code : 'request_failed', missing, typeof failure.correlationId === 'string' ? failure.correlationId : undefined)
    }
    const result = decodeTugRequest(body, vessel, attempt.jobId)
    if (result.state !== (attempt.operation === 'submit' ? 'Submitted' : 'Cancelled')) throw new ApiError(502, 'invalid_tug_request_contract')
    return result
  },
  save: async (vessel: string, attempt: TugAttempt, csrf: string, signal: AbortSignal) => decodeTugRequest(await request<unknown>(`tug-jobs${attempt.update ? `/${attempt.body.clientId}` : ''}?vessel=${vessel}`, signal, {
    method: attempt.update ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify(attempt.body),
  }), vessel, attempt.body.clientId),
}
