import { afterEach, describe, expect, it, vi } from 'vitest'
import { decodeSyncResult, maxSyncOperations, orderReplayQueue, queueDisplayState, reconcileOperations, validSyncRequest,
  type QueuedOperation, type SyncOperationRef } from './sync-operations'

// S4 WP-D DEV-033 / DEV-034 (T-S4-001 / T-S4-002).
const vessel = '22222222-2222-4222-8222-222222222222', resource = '33333333-3333-4333-8333-333333333333'
const op = (n: number) => `44444444-4444-4444-8444-${n.toString().padStart(12, '0')}`
const ref = (n: number): SyncOperationRef => ({ operationId: op(n), domain: 'CrewWatchGroup', resourceId: resource, payloadDigest: 'a'.repeat(64) })
const empty = { result: null, baseVersion: null, serverVersion: null, revision: null, parentCommandId: null, receivedAt: null }
const item = (n: number, state: string, extra: object = empty) => ({ operationId: op(n), domain: 'CrewWatchGroup', resourceId: resource, state, ...extra })
const recorded = { result: 'Committed', baseVersion: 3, serverVersion: 4, revision: 5, parentCommandId: null, receivedAt: '2026-11-19T10:00:00.123456+00:00' }
const body = (items: unknown[]) => ({ vesselId: vessel, serverTime: '2026-11-19T10:00:01Z', items })

describe('sync reconciliation contract', () => {
  it('accepts exactly the asked operations in order with a complete receipt only for Recorded', () => {
    const result = decodeSyncResult(body([item(1, 'Recorded', recorded), item(2, 'NotReceived'), item(3, 'PayloadMismatch')]), vessel, [ref(1), ref(2), ref(3)])
    expect(result.map(x => x.state)).toEqual(['Recorded', 'NotReceived', 'PayloadMismatch'])
    expect(result[0].serverVersion).toBe(4)
  })
  it('rejects reordered, missing, foreign-vessel, unknown-state or outcome-leaking answers', () => {
    const refs = [ref(1), ref(2)]
    for (const value of [
      body([item(2, 'NotReceived'), item(1, 'NotReceived')]),
      body([item(1, 'NotReceived')]),
      { ...body([item(1, 'NotReceived'), item(2, 'NotReceived')]), vesselId: resource },
      body([item(1, 'Saved'), item(2, 'NotReceived')]),
      body([item(1, 'NotReceived', recorded), item(2, 'NotReceived')]),
      body([item(1, 'Recorded', { ...recorded, revision: null }), item(2, 'NotReceived')]),
      body([item(1, 'Recorded', { ...recorded, result: 'Approved' }), item(2, 'NotReceived')]),
      { ...body([item(1, 'NotReceived'), item(2, 'NotReceived')]), extra: true },
    ]) expect(() => decodeSyncResult(value, vessel, refs)).toThrow()
  })
  it('bounds and validates the request before any network call', () => {
    expect(validSyncRequest([ref(1)])).toBe(true)
    expect(validSyncRequest([])).toBe(false)
    expect(validSyncRequest([ref(1), ref(1)])).toBe(false)
    expect(validSyncRequest([{ ...ref(1), payloadDigest: 'x'.repeat(64) }])).toBe(false)
    expect(validSyncRequest(Array.from({ length: maxSyncOperations + 1 }, (_, i) => ref(i + 1)))).toBe(false)
  })
})

describe('reconcileOperations transport', () => {
  afterEach(() => vi.unstubAllGlobals())
  it('posts digests in the body with the session CSRF token and never in the URL', async () => {
    const fetch = vi.fn(async (url: string) => url.endsWith('auth/session')
      ? new Response(JSON.stringify({ authenticated: true, csrfToken: 'csrf-1', actorId: op(9), tenantId: op(8) }))
      : new Response(JSON.stringify(body([item(1, 'Recorded', recorded)]))))
    vi.stubGlobal('fetch', fetch)
    const result = await reconcileOperations(vessel, [ref(1)], new AbortController().signal)
    expect(result[0].state).toBe('Recorded')
    const [url, init] = fetch.mock.calls[1] as unknown as [string, RequestInit]
    expect(url).toBe('/api/v1/sync/operations'); expect(url).not.toContain('a'.repeat(64))
    expect(init.method).toBe('POST'); expect((init.headers as Record<string, string>)['X-CSRF-TOKEN']).toBe('csrf-1')
    expect(JSON.parse(String(init.body))).toEqual({ vesselId: vessel, operations: [ref(1)] })
  })
  it('refuses an invalid request locally', async () => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
    await expect(reconcileOperations(vessel, [], new AbortController().signal)).rejects.toThrow('invalid_sync_request')
    expect(fetch).not.toHaveBeenCalled()
  })
})

describe('durable queue presentation and per-resource ordering', () => {
  const q = (n: number, at: string, extra: Partial<QueuedOperation> = {}): QueuedOperation =>
    ({ operationId: op(n), resourceKey: 'crew/A', capturedAt: at, ...extra })
  it('never presents an unproven capture as saved', () => {
    expect(queueDisplayState(q(1, '2026-11-19T10:00:00Z'))).toBe('pending')
    expect(queueDisplayState(q(1, '2026-11-19T10:00:00Z', { sync: 'NotReceived' }))).toBe('pending')
    expect(queueDisplayState(q(1, '2026-11-19T10:00:00Z', { sync: 'PayloadMismatch' }))).toBe('rejected')
    expect(queueDisplayState(q(1, '2026-11-19T10:00:00Z', { sync: 'KeyReused' }))).toBe('rejected')
    expect(queueDisplayState(q(1, '2026-11-19T10:00:00Z', { sync: 'Unavailable' }))).toBe('unavailable')
    expect(queueDisplayState(q(1, '2026-11-19T10:00:00Z', { outcome: { result: 'MaterialConflict' } }))).toBe('conflict')
    expect(queueDisplayState(q(1, '2026-11-19T10:00:00Z', { outcome: { result: 'Committed' } }))).toBe('recorded')
  })
  it('lets only the oldest unresolved capture of a resource be sent; others wait', () => {
    const order = orderReplayQueue([q(3, '2026-11-19T10:02:00Z'), q(1, '2026-11-19T10:00:00Z', { outcome: { result: 'Committed' } }),
      q(2, '2026-11-19T10:01:00Z'), { ...q(4, '2026-11-19T09:00:00Z'), resourceKey: 'crew/B' }])
    expect(order.map(x => [x.op.operationId, x.state, x.sendable])).toEqual([
      [op(1), 'recorded', false], [op(2), 'pending', true], [op(3), 'blocked', false], [op(4), 'pending', true]])
  })
  it('a conflict or rejected predecessor holds later captures until an explicit resolution is queued', () => {
    const conflict = q(1, '2026-11-19T10:00:00Z', { outcome: { result: 'MaterialConflict' } })
    const later = q(2, '2026-11-19T10:01:00Z')
    expect(orderReplayQueue([conflict, later]).map(x => x.state)).toEqual(['conflict', 'blocked'])
    expect(orderReplayQueue([q(1, '2026-11-19T10:00:00Z', { sync: 'PayloadMismatch' }), later]).map(x => x.state)).toEqual(['rejected', 'blocked'])
    const resolution = q(3, '2026-11-19T10:02:00Z', { parentOperationId: op(1) })
    const order = orderReplayQueue([conflict, resolution])
    expect(order.map(x => [x.state, x.sendable])).toEqual([['conflict', false], ['pending', true]])
  })
  it('equal or invalid capture timestamps keep a stable key order', () => {
    const order = orderReplayQueue([q(2, 'not-a-time'), q(5, '2026-11-19T10:00:00Z'), q(1, '2026-11-19T10:00:00Z')])
    expect(order.map(x => x.op.operationId)).toEqual([op(1), op(5), op(2)])
  })
})
