import { afterEach, expect, it, vi } from 'vitest'
import { ApiError } from './api'
import { decodeSyncConflictPage, loadSyncConflicts } from './sync-conflict-center-api'

// DEV-035 / T-S4-003 strict index contract (S4-WPE-CC-API-01..05).
const vessel = '11111111-1111-4111-8111-111111111111'
const item = (patch: Record<string, unknown> = {}) => ({ source: 'crewAssignment', resourceId: '22222222-2222-4222-8222-222222222222', resourceKind: null,
  enrollmentId: '33333333-3333-4333-8333-333333333333', commandId: '44444444-4444-4444-8444-444444444444', baseVersion: 7, serverVersionAtConflict: 8,
  capturedAt: '2026-11-19T08:00:00Z', receivedAt: '2026-11-19T08:05:00.1234567+00:00', state: 'Open', resolutionCommandId: null, resolutionResult: null, ...patch })
const page = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, readAt: '2026-11-19T09:00:00Z',
  sources: [{ source: 'crewAssignment', state: 'Included' }, { source: 'tugPlan', state: 'NotPermitted' }, { source: 'dailyRecord', state: 'Partial' }, { source: 'workOrderEvidence', state: 'NotPermitted' }, { source: 'tugOperations', state: 'NotPermitted' }, { source: 'certificateRenewal', state: 'NotPermitted' }],
  items: [item()], offset: 0, hasMore: false, ...patch })
afterEach(() => { vi.unstubAllGlobals() })

it('S4-WPE-CC-API-01 accepts the exact metadata-only page and preserves unknown capture time as null', () => {
  const decoded = decodeSyncConflictPage(page({ items: [item(), item({ source: 'dailyRecord', resourceKind: 'Noon', capturedAt: null, commandId: '55555555-5555-4555-8555-555555555555' })] }), vessel, 0)
  expect(decoded.items).toHaveLength(2); expect(decoded.items[1].capturedAt).toBeNull()
})

it('S4-WPE-CC-API-02 rejects value leakage, extra members, foreign vessel and mismatched paging', () => {
  for (const bad of [page({ extra: 1 }), page({ vesselId: '99999999-9999-4999-8999-999999999999' }), page({ offset: 25 }),
    page({ items: [{ ...item(), captured: 'SYNTHETIC-A' }] }), page({ items: [item({ baseVersion: 0 })] }), page({ hasMore: 'no' }),
    page({ sources: [{ source: 'tugPlan', state: 'Included' }] }), page({ items: Array.from({ length: 26 }, (_, i) => item({ commandId: `44444444-4444-4444-8444-${String(i).padStart(12, '0')}` })) })])
    expect(() => decodeSyncConflictPage(bad, vessel, 0)).toThrow(ApiError)
})

it('S4-WPE-CC-API-03 rejects inconsistent resolution states so a conflict can never look resolved by another conflict', () => {
  const successor = '66666666-6666-4666-8666-666666666666'
  expect(() => decodeSyncConflictPage(page({ items: [item({ resolutionCommandId: successor, resolutionResult: 'Committed' })] }), vessel, 0)).toThrow()
  expect(() => decodeSyncConflictPage(page({ items: [item({ state: 'Resolved', resolutionCommandId: successor, resolutionResult: 'MaterialConflict' })] }), vessel, 0)).toThrow()
  expect(() => decodeSyncConflictPage(page({ items: [item({ state: 'Superseded', resolutionCommandId: successor, resolutionResult: 'Committed' })] }), vessel, 0)).toThrow()
  expect(() => decodeSyncConflictPage(page({ items: [item({ state: 'Resolved', resolutionCommandId: item().commandId, resolutionResult: 'Committed' })] }), vessel, 0)).toThrow()
  expect(decodeSyncConflictPage(page({ items: [item({ state: 'Resolved', resolutionCommandId: successor, resolutionResult: 'ServerRetained' })] }), vessel, 0).items[0].state).toBe('Resolved')
})

it('S4-WPE-CC-API-04 rejects rows from a source the server declared not permitted and duplicate rows', () => {
  expect(() => decodeSyncConflictPage(page({ items: [item({ source: 'tugPlan' })] }), vessel, 0)).toThrow()
  expect(() => decodeSyncConflictPage(page({ items: [item(), item()] }), vessel, 0)).toThrow()
})

it('S4-WPE-CC-API-05 sends only the closed vessel/offset query and refuses invalid context before any request', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(page({ offset: 25, items: [] })), { status: 200 }))
  vi.stubGlobal('fetch', fetch)
  await loadSyncConflicts(vessel, 25, new AbortController().signal)
  expect(fetch).toHaveBeenCalledWith(`/api/v1/sync/conflicts?vesselId=${vessel}&offset=25`, expect.objectContaining({ credentials: 'same-origin' }))
  await expect(loadSyncConflicts('not-a-guid', 0, new AbortController().signal)).rejects.toThrow(ApiError)
  await expect(loadSyncConflicts(vessel, 7, new AbortController().signal)).rejects.toThrow(ApiError)
  expect(fetch).toHaveBeenCalledTimes(1)
})
