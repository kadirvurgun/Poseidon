import { ApiError } from './api'
import { OfflineApplication, type OfflineConflictDetail, type OfflineOutcome, type PendingCapture } from './offline-application'
import type { OfflineConflictResolution } from './OfflineConflictReview'
import type { SyncConflictItem } from './sync-conflict-center-api'

// DEV-035 conflict-center adapter over the EXISTING device-bound crew offline application (DEV-033/034
// owned queue/sync). It never rewrites queue internals: resolution is a new frozen command captured with
// the conflict's exact current server version, persisted encrypted BEFORE submission, then replayed with the
// same UUID/body. A retained unsubmitted/uncertain resolution is reused, never duplicated.
export type ConflictReviewFailure = 'otherDevice' | 'denied' | 'unavailable'
export type ResolutionResult = { kind: 'committed'; outcome: OfflineOutcome } | { kind: 'conflictAgain'; outcome: OfflineOutcome }
export interface ConflictReviewSession {
  readonly detail: OfflineConflictDetail
  readonly local: PendingCapture | null
  readonly pendingResolution: PendingCapture | null
  readonly actorId: string
  resolve(resolution: OfflineConflictResolution, signal: AbortSignal): Promise<ResolutionResult>
  recheck(signal: AbortSignal): Promise<ResolutionResult>
  discardLocal(confirmed: boolean): Promise<void>
  close(): void
}

export function classifyReviewFailure(error: unknown): ConflictReviewFailure {
  if (error instanceof Error && error.message === 'key_lost_data_retained') return 'otherDevice'
  if (error instanceof ApiError && [401, 403, 404].includes(error.status)) return 'denied'
  return 'unavailable'
}

const parentOf = (capture: PendingCapture): string | null => {
  try { const body: unknown = JSON.parse(capture.body); return body && typeof body === 'object' && typeof (body as { parentCommandId?: unknown }).parentCommandId === 'string' ? (body as { parentCommandId: string }).parentCommandId : null }
  catch { return null }
}

function result(outcome: OfflineOutcome, parent: string): ResolutionResult {
  if (outcome.parentCommandId !== parent) throw new ApiError(502, 'invalid_resolution_outcome')
  return outcome.result === 'Committed' ? { kind: 'committed', outcome } : { kind: 'conflictAgain', outcome }
}

export async function openCrewConflictReview(vessel: string, item: SyncConflictItem, signal: AbortSignal): Promise<ConflictReviewSession> {
  if (item.source !== 'crewAssignment' || item.state !== 'Open') throw new ApiError(400, 'unsupported_conflict_review')
  // Explicit reauthorization of THIS enrollment under current rights; never a new enrollment or key.
  const app = await OfflineApplication.authorize(vessel, item.resourceId, item.enrollmentId, signal)
  try {
    const detail = await app.conflict(item.commandId, signal)
    let rows = await app.captures()
    let pending = rows.find(row => parentOf(row) === item.commandId && !row.outcome) ?? null
    const local = rows.find(row => row.commandId === item.commandId) ?? null
    const submit = async (capture: PendingCapture, s: AbortSignal) => {
      let outcome: OfflineOutcome
      try { outcome = await app.replay(capture, s) }
      catch (error) {
        // Definitive server rejection because the record changed after review: the frozen decision had no
        // effect and can never apply, so it is withdrawn and a NEW review with the fresh version is required.
        if (error instanceof ApiError && error.status === 409 && error.code === 'offline_conflict_changed') {
          await app.discard(capture, true); rows = await app.captures(); pending = null
        }
        throw error
      }
      rows = await app.captures(); pending = null
      return result(outcome, item.commandId)
    }
    return {
      detail, local, get pendingResolution() { return pending }, actorId: app.context.actorId,
      async resolve(resolution, s) {
        if (resolution.conflictId !== item.commandId || resolution.decisions.length !== 1) throw new ApiError(400, 'invalid_resolution')
        if (!pending) {
          const id = await app.capture(null, resolution)
          rows = await app.captures(); pending = rows.find(row => row.commandId === id) ?? null
          if (!pending) throw new Error('resolution_not_retained')
        }
        return submit(pending, s)
      },
      async recheck(s) {
        if (!pending) throw new Error('no_pending_resolution')
        return submit(pending, s) // exact same UUID/body; the server recovers the original receipt
      },
      async discardLocal(confirmed) {
        if (!local) throw new Error('no_local_capture')
        await app.discard(local, confirmed)
      },
      close() { app.close() },
    }
  } catch (error) { app.close(); throw error }
}
