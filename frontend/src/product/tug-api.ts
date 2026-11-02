import type { Plan, PlanningEvidence } from './tug-planning-api'
import { decodePlanningEvidence } from './tug-planning-api'
import { ApiError, request } from './api'
import { isGuid } from './identifiers'

export type TugInputs = {
  requestSource: string | null; customerRef: string | null; assistedVessel: string | null; assistedVesselImo: string | null; jobType: string | null
  requestedStart: string | null; requestedEnd: string | null; operationArea: string | null; pickupPoint: string | null
  destinationPoint: string | null; requiredBollardPull: string | null; requiredTugCount: number | null
  specialRequirements: string | null; priority: string | null; requestOwner: string | null; notes: string | null
}
export type TugRequest = { id: string; vesselId: string; jobNumber: string; revision: number; version: number; inputs: TugInputs; provenance: string; savedAt: string; missing: string[]; replayed: boolean; sourceVersion: string }
export type TugContext = {
  vessel: { id: string; code: string; name: string }; mode: 'Tug'; crossMode: boolean; roleReferences: string | null
  tugTypeOptions?: { code: string; tr: string; en: string }[]; tugTypePolicyVersion?: string | null; canWrite: boolean; owners: string[]; requestSources: string[]; jobTypes: string[]; priorities: string[]; tugType: string
  bollardPull: { value: string | null; version: number | null; provenance: string; freshness: string; observedAt: string | null }
  equipment: { id: string; level: string; equipmentType: string | null; revision: number; provenance: string }[] | null
  equipmentCoverage: string; readiness: string; reason: string
}
export type TugBoard = { items: { draftPlan?: Plan | null; planningEvidence?: PlanningEvidence | null; request: TugRequest; windowState: string; plannedTimeSource: string; assignedTugs: string[]; assignmentCoverage: string; availableBollardPull: string | null; readiness: string; conflict: string; declaredComparisons: { jobId: string; relation: string }[]; comparisonCoverage: string; blockingReasons: string[]; nextAction: string; detailHref: string }[]; hasMore: boolean }
export const emptyTugInputs = (): TugInputs => ({ requestSource: null, customerRef: null, assistedVessel: null, assistedVesselImo: null, jobType: null, requestedStart: null, requestedEnd: null, operationArea: null, pickupPoint: null, destinationPoint: null, requiredBollardPull: null, requiredTugCount: null, specialRequirements: null, priority: null, requestOwner: null, notes: null })
export type TugAttempt = { key: string; body: { clientId: string; baseVersion: number | null; inputs: TugInputs }; update: boolean }
export const tugApi = {
  context: (vessel: string, signal: AbortSignal) => request<TugContext>(`tug-command?vessel=${vessel}`, signal),
  vessels: (signal: AbortSignal) => request<{ vessels: TugContext['vessel'][] }>('tug-command', signal),
  detail: (id: string, vessel: string, signal: AbortSignal) => request<TugRequest>(`tug-jobs/${id}?vessel=${vessel}`, signal),
  board: async (vessel: string, priority: string, q: string, offset: number, signal: AbortSignal) => {
    const result = await request<TugBoard>(`tug-job-board?${new URLSearchParams({ vessel, priority, q, offset: String(offset), limit: '25' })}`, signal)
    if (!result || !Array.isArray(result.items) || result.items.length > 25 || typeof result.hasMore !== 'boolean') throw new ApiError(502, 'invalid_tug_board_contract')
    const ids = new Set<string>()
    for (const row of result.items) {
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
  save: (vessel: string, attempt: TugAttempt, csrf: string, signal: AbortSignal) => request<TugRequest>(`tug-jobs${attempt.update ? `/${attempt.body.clientId}` : ''}?vessel=${vessel}`, signal, {
    method: attempt.update ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify(attempt.body),
  }),
}
