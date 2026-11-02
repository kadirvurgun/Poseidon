import { expect, it } from 'vitest'
import { decodeTugCapture, decodeTugOfflineContext, tugOfflineOperation, tugOfflineSource } from './tug-offline'

const job = '11111111-1111-4111-8111-111111111111', vessel = '22222222-2222-4222-8222-222222222222'
const context = { id: job, tenantId: vessel, actorId: job, vesselId: vessel, jobId: job, deviceId: vessel, keyId: job,
  challenge: 'A'.repeat(43) + '=', policyVersion: 'fictional-v1', sourceVersion: tugOfflineSource, requestVersion: 7,
  baseVersion: 8, baseRevision: 2, plannedStart: null, plannedEnd: null, serverTime: '2026-11-02T00:00:00Z', expiresAt: '2026-11-02T01:00:00Z' }
const capture = { commandId: job, keyId: job, deviceId: vessel, policyVersion: 'fictional-v1', sourceVersion: tugOfflineSource,
  operation: tugOfflineOperation, requestVersion: 7, baseVersion: 8, capturedAt: '2026-11-02T00:01:00Z', offsetMinutes: 180, plannedStart: null, plannedEnd: null }
it('retains exact incomplete windows and rejects missing, coerced, wider or other-context wire fields', () => {
  const ctx = decodeTugOfflineContext(context, job, vessel)
  expect(decodeTugCapture(capture, ctx, job).plannedStart).toBeNull()
  for (const key of Object.keys(context)) { const broken = { ...context } as Record<string, unknown>; delete broken[key]; expect(() => decodeTugOfflineContext(broken, job, vessel)).toThrow() }
  for (const key of Object.keys(capture)) { const broken = { ...capture } as Record<string, unknown>; delete broken[key]; expect(() => decodeTugCapture(broken, ctx, job)).toThrow() }
  for (const patch of [{ ready: true }, { assignedTugs: [vessel] }, { watchGroup: 'A' }, { plannedStart: false }, { baseVersion: '8' },
    { operation: 'crew.draft.watch-group.v1' }, { sourceVersion: 'old' }, { requestVersion: 9 }, { capturedAt: '2026-11-02T00:01:00' },
    { plannedStart: '2026-11-02T01:00:00Z', plannedEnd: '2026-11-02T00:00:00Z' }]) expect(() => decodeTugCapture({ ...capture, ...patch }, ctx, job)).toThrow()
})
it('accepts only a new reasoned conflict command with exact current version and no silent rebase', () => {
  const ctx = decodeTugOfflineContext(context, job, vessel)
  expect(() => decodeTugCapture({ ...capture, baseVersion: 9 }, ctx, job)).toThrow()
  const resolution = { ...capture, baseVersion: 9, expectedServerVersion: 9, parentCommandId: vessel, choice: 'useCaptured', reason: 'INPUT_CORRECTED' }
  expect(decodeTugCapture(resolution, ctx, job).parentCommandId).toBe(vessel)
  for (const patch of [{ reason: '' }, { choice: 'approve' }, { expectedServerVersion: 8 }, { parentCommandId: null }]) expect(() => decodeTugCapture({ ...resolution, ...patch }, ctx, job)).toThrow()
})
