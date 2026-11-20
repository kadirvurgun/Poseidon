import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, request, session } from './api'
import { openCrewConflictReview, classifyReviewFailure } from './conflict-center-resolver'
import { loadSyncConflicts, type SyncConflictItem } from './sync-conflict-center-api'
import type { OfflineConflictResolution } from './OfflineConflictReview'

// DEV-038 / T-S4-006 offline chaos regression pack (18.01 Backlog row 39): network drop/reconnect,
// duplicate request, stale base version, conflict paths, plus lost response, reload with pending queue,
// clock skew, concurrent edit from another device, storage quota exhaustion and session expiry.
// Deterministic: every fault is scripted on a fake transport, there are no sleeps/real timers, and each
// case asserts (1) no data loss, (2) no duplicate server effect, (3) no false "saved" receipt.
// The device store below stands in for the encrypted IndexedDB queue (owned by DEV-033/034); its
// persistence across "reload" is a module-level map. Real IndexedDB/encryption is covered separately by
// tests/Poseidon.Browser.Tests/specs/offline-record-store.spec.ts.

const vessel = '11111111-1111-4111-8111-111111111111', assignment = '22222222-2222-4222-8222-222222222222'
const enrollment = '33333333-3333-4333-8333-333333333333', conflictId = '44444444-4444-4444-8444-444444444444'
const actor = '55555555-5555-4555-8555-555555555555', tenant = '66666666-6666-4666-8666-666666666666'
type Stored = { commandId: string; revision: number; body: string; outcome?: Outcome }
type Outcome = { commandId: string; result: 'Committed' | 'MaterialConflict'; baseVersion: number; serverVersion: number; revision: number
  capturedAt: string; offsetMinutes: number; receivedAt: string; parentCommandId: string | null }

const chaos = vi.hoisted(() => ({
  disk: new Map<string, unknown>(), quota: Infinity, keyPresent: true, opened: 0, closed: 0,
}))

// --- Scripted server -------------------------------------------------------------------------------
type Fault = 'dropBefore' | 'dropAfter' | null
const server = {
  version: 8, value: 'SERVER-B', effects: 0, authenticated: true, now: 0, faults: [] as Fault[],
  outcomes: new Map<string, { body: string; outcome: Outcome }>(), replays: 0,
}
function reset() {
  Object.assign(server, { version: 8, value: 'SERVER-B', effects: 0, authenticated: true, now: Date.now(), faults: [], replays: 0 })
  server.outcomes = new Map()
  chaos.disk.clear(); chaos.quota = Infinity; chaos.keyPresent = true; chaos.opened = 0; chaos.closed = 0
  // The originally conflicted local capture is retained on the device (MaterialConflict receipt).
  chaos.disk.set(conflictId, { commandId: conflictId, revision: 1, body: JSON.stringify({ commandId: conflictId, watchGroup: 'LOCAL-A', baseVersion: 7 }),
    outcome: { commandId: conflictId, result: 'MaterialConflict', baseVersion: 7, serverVersion: 8, revision: 2, capturedAt: '2026-11-19T07:00:00Z', offsetMinutes: 0, receivedAt: '2026-11-19T07:30:00Z', parentCommandId: null } })
}
const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
function serve(url: string, init?: RequestInit): Response {
  if (url === '/api/v1/auth/session')
    return json(200, server.authenticated ? { authenticated: true, canRead: true, actorId: actor, tenantId: tenant, csrfToken: 'synthetic-csrf' } : { authenticated: false })
  if (url.startsWith('/api/v1/sync/conflicts')) {
    const resolved = [...server.outcomes.values()].find(o => o.outcome.parentCommandId === conflictId)
    return json(200, { vesselId: vessel, readAt: '2026-11-19T08:00:00Z', sources: [{ source: 'crewAssignment', state: 'Included' }, { source: 'tugPlan', state: 'Included' }, { source: 'dailyRecord', state: 'Included' }, { source: 'workOrderEvidence', state: 'NotPermitted' }, { source: 'tugOperations', state: 'NotPermitted' }, { source: 'certificateRenewal', state: 'NotPermitted' }],
      items: [{ source: 'crewAssignment', resourceId: assignment, resourceKind: null, enrollmentId: enrollment, commandId: conflictId, baseVersion: 7, serverVersionAtConflict: 8,
        capturedAt: '2026-11-19T07:00:00Z', receivedAt: '2026-11-19T07:30:00Z', state: resolved ? 'Resolved' : 'Open',
        resolutionCommandId: resolved?.outcome.commandId ?? null, resolutionResult: resolved ? 'Committed' : null }], offset: 0, hasMore: false })
  }
  if (url.endsWith('/replay') && init?.method === 'POST') {
    server.replays++
    if (!server.authenticated) return json(401, { code: 'unauthenticated' })
    const body = String(init.body), c = JSON.parse(body) as { commandId: string; parentCommandId: string; expectedServerVersion: number; choice: string; capturedAt: string }
    const recorded = server.outcomes.get(c.commandId)
    if (recorded) return recorded.body === body ? json(200, recorded.outcome) : json(409, { code: 'offline_command_reused' })
    // Server authority: client wall clock never grants acceptance (skew window 5 minutes).
    if (Date.parse(c.capturedAt) > server.now + 300000) return json(409, { code: 'offline_recovery_required' })
    if (c.expectedServerVersion !== server.version) return json(409, { code: 'offline_conflict_changed' })
    if ([...server.outcomes.values()].some(o => o.outcome.parentCommandId === c.parentCommandId)) return json(409, { code: 'offline_resolution_exists' })
    if (c.choice === 'useCaptured') server.value = 'LOCAL-A'
    server.version++; server.effects++
    const outcome: Outcome = { commandId: c.commandId, result: 'Committed', baseVersion: c.expectedServerVersion, serverVersion: server.version, revision: server.version,
      capturedAt: c.capturedAt, offsetMinutes: 0, receivedAt: new Date(server.now).toISOString(), parentCommandId: c.parentCommandId }
    server.outcomes.set(c.commandId, { body, outcome })
    return json(200, outcome)
  }
  return json(404, { code: 'resource_unavailable' })
}
const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
  const url = String(input)
  const fault = url.endsWith('/replay') ? server.faults.shift() ?? null : null
  if (fault === 'dropBefore') throw new TypeError('Failed to fetch') // request never reached the server
  const response = serve(url, init)
  if (fault === 'dropAfter') throw new TypeError('Failed to fetch') // committed, response lost
  return response
})

// --- Device-bound offline application stand-in (existing DEV-033/034 API surface) ------------------
vi.mock('./offline-application', () => ({
  OfflineApplication: {
    async authorize(v: string, a: string, e: string | null, signal: AbortSignal) {
      if (!chaos.keyPresent) throw new Error('key_lost_data_retained')
      const user = await session(signal); if (!user.authenticated) throw new ApiError(401)
      chaos.opened++
      const disk = chaos.disk as Map<string, Stored>
      return {
        context: { id: e, actorId: actor, tenantId: tenant, vesselId: v, assignmentId: a, watchGroups: ['LOCAL-A', 'SERVER-B'] },
        async conflict(id: string) {
          return { id, revision: String(server.version), contextKey: e, base: { kind: 'known', text: 'BASE-0' }, currentVersion: server.version, resolved: null,
            originalServerVersion: 8, resolvedValue: null, resolutionReason: null, resolvedBy: null,
            fields: [{ key: 'watch_group', label: 'Watch group', captured: { kind: 'known', text: 'LOCAL-A' }, server: { kind: 'known', text: server.value }, choices: ['keepServer', 'useCaptured'] }] }
        },
        async captures() { return [...disk.values()].map(row => ({ ...row })) },
        async capture(_watch: string | null, resolution: OfflineConflictResolution) {
          if (chaos.quota <= 0) throw new DOMException('quota', 'QuotaExceededError')
          chaos.quota--
          const commandId = crypto.randomUUID()
          disk.set(commandId, { commandId, revision: 1, body: JSON.stringify({ commandId, parentCommandId: resolution.conflictId,
            expectedServerVersion: Number(resolution.expectedRevision), choice: resolution.decisions[0].choice, reason: resolution.reason, capturedAt: new Date().toISOString() }) })
          return commandId
        },
        async replay(capture: Stored, s: AbortSignal) {
          const stored = disk.get(capture.commandId)
          if (!stored || stored.body !== capture.body) throw new ApiError(502)
          const user = await session(s); if (!user.authenticated) throw new ApiError(403)
          const outcome = await request<Outcome>(`vessels/${v}/crew/${a}/offline/${e}/replay`, s, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: capture.body })
          disk.set(capture.commandId, { ...stored, revision: stored.revision + 1, outcome })
          return outcome
        },
        async discard(capture: Stored, confirmed: boolean) { if (!confirmed) throw new Error('discard_confirmation_required'); disk.delete(capture.commandId) },
        close() { chaos.closed++ },
      }
    },
  },
}))

const item: SyncConflictItem = { source: 'crewAssignment', resourceId: assignment, resourceKind: null, enrollmentId: enrollment, commandId: conflictId,
  baseVersion: 7, serverVersionAtConflict: 8, capturedAt: '2026-11-19T07:00:00Z', receivedAt: '2026-11-19T07:30:00Z', state: 'Open', resolutionCommandId: null, resolutionResult: null }
const decide = (choice: 'keepServer' | 'useCaptured', expected = server.version): OfflineConflictResolution =>
  ({ conflictId, expectedRevision: String(expected), reason: 'FICTIONAL reasoned human decision', decisions: [{ field: 'watch_group', choice }] })
const signal = () => new AbortController().signal
const disk = () => chaos.disk as Map<string, Stored>
const pendingResolutions = () => [...disk().values()].filter(row => row.body.includes(`"parentCommandId":"${conflictId}"`) && !row.outcome)

beforeEach(() => { reset(); vi.stubGlobal('fetch', fetchMock); fetchMock.mockClear() })
afterEach(() => { vi.unstubAllGlobals(); vi.useRealTimers() })

describe('DEV-038 offline chaos regression pack (S4-WPE-CHAOS-01..10)', () => {
  it('S4-WPE-CHAOS-01 network drop mid-request: no server effect, decision retained, explicit reconnect applies exactly once', async () => {
    const review = await openCrewConflictReview(vessel, item, signal())
    server.faults.push('dropBefore')
    await expect(review.resolve(decide('useCaptured'), signal())).rejects.toThrow(TypeError)
    expect(server.effects).toBe(0); expect(server.value).toBe('SERVER-B')
    expect(review.pendingResolution).not.toBeNull(); expect(pendingResolutions()).toHaveLength(1) // no data loss
    const result = await review.recheck(signal())
    expect(result.kind).toBe('committed'); expect(server.effects).toBe(1); expect(server.value).toBe('LOCAL-A')
    expect(pendingResolutions()).toHaveLength(0); expect(disk().get(conflictId)).toBeDefined() // original capture never silently removed
  })

  it('S4-WPE-CHAOS-02 lost response after commit: no false saved, recovery returns the ORIGINAL receipt without a second effect', async () => {
    const review = await openCrewConflictReview(vessel, item, signal())
    server.faults.push('dropAfter')
    const attempt = review.resolve(decide('keepServer'), signal())
    await expect(attempt).rejects.toThrow(TypeError) // caller sees an unknown outcome, never "saved"
    expect(server.effects).toBe(1); expect(review.pendingResolution).not.toBeNull()
    const original = [...server.outcomes.values()][0].outcome
    const recovered = await review.recheck(signal())
    expect(recovered).toEqual({ kind: 'committed', outcome: original }); expect(server.effects).toBe(1)
  })

  it('S4-WPE-CHAOS-03 duplicate replay (double submit / concurrent retry) produces one server effect and one receipt', async () => {
    const review = await openCrewConflictReview(vessel, item, signal())
    server.faults.push('dropAfter')
    await expect(review.resolve(decide('useCaptured'), signal())).rejects.toThrow()
    const [a, b] = await Promise.all([review.recheck(signal()), review.recheck(signal()).catch(error => error)])
    expect(a.kind).toBe('committed'); expect(server.effects).toBe(1)
    if (!(b instanceof Error)) expect(b).toEqual(a)
    expect(server.outcomes.size).toBe(1)
  })

  it('S4-WPE-CHAOS-04 reload with a pending queue: the retained decision is reused, never re-decided or duplicated', async () => {
    const first = await openCrewConflictReview(vessel, item, signal())
    server.faults.push('dropBefore')
    await expect(first.resolve(decide('useCaptured'), signal())).rejects.toThrow()
    first.close() // reload: in-memory state gone, durable device queue retained
    const second = await openCrewConflictReview(vessel, item, signal())
    expect(second.pendingResolution?.commandId).toBe(pendingResolutions()[0].commandId)
    // Even a different new decision submits the FROZEN retained command, not a second one.
    const result = await second.resolve(decide('keepServer'), signal())
    expect(result.kind).toBe('committed'); expect(server.value).toBe('LOCAL-A'); expect(server.effects).toBe(1)
    expect([...disk().values()].filter(row => row.body.includes('parentCommandId'))).toHaveLength(1)
  })

  it('S4-WPE-CHAOS-05 clock skew: a far-future device clock never makes a decision accepted; nothing is lost or saved', async () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(server.now + 3 * 3600000)
    const review = await openCrewConflictReview(vessel, item, signal())
    const failure = await review.resolve(decide('useCaptured'), signal()).catch(error => error)
    expect(failure).toBeInstanceOf(ApiError); expect((failure as ApiError).code).toBe('offline_recovery_required')
    expect(server.effects).toBe(0); expect(server.value).toBe('SERVER-B')
    expect(pendingResolutions()).toHaveLength(1); expect(disk().get(conflictId)).toBeDefined()
  })

  it('S4-WPE-CHAOS-06 stale base version / concurrent edit from another device: decision withdrawn, other device value preserved', async () => {
    const review = await openCrewConflictReview(vessel, item, signal())
    const stale = decide('useCaptured')
    server.version = 9; server.value = 'OTHER-DEVICE' // concurrent edit after the review was read
    const failure = await review.resolve(stale, signal()).catch(error => error)
    expect(failure).toBeInstanceOf(ApiError); expect((failure as ApiError).code).toBe('offline_conflict_changed')
    expect(server.value).toBe('OTHER-DEVICE'); expect(server.effects).toBe(0) // no silent overwrite
    expect(review.pendingResolution).toBeNull(); expect(pendingResolutions()).toHaveLength(0)
    expect(disk().get(conflictId)).toBeDefined() // the original local change is still retained for a new review
    const fresh = await openCrewConflictReview(vessel, item, signal())
    expect(fresh.detail.revision).toBe('9'); expect(fresh.detail.fields[0].server).toEqual({ kind: 'known', text: 'OTHER-DEVICE' })
  })

  it('S4-WPE-CHAOS-07 storage quota exhaustion: nothing is sent, nothing is claimed, the original capture survives', async () => {
    const review = await openCrewConflictReview(vessel, item, signal())
    chaos.quota = 0
    await expect(review.resolve(decide('useCaptured'), signal())).rejects.toThrow(DOMException)
    expect(server.replays).toBe(0); expect(server.effects).toBe(0); expect(review.pendingResolution).toBeNull()
    expect(disk().get(conflictId)).toBeDefined()
  })

  it('S4-WPE-CHAOS-08 session expiry while offline: replay refuses without a session, keeps the decision and applies once after re-login', async () => {
    const review = await openCrewConflictReview(vessel, item, signal())
    server.authenticated = false
    const failure = await review.resolve(decide('useCaptured'), signal()).catch(error => error)
    expect(classifyReviewFailure(failure)).toBe('denied'); expect(server.replays).toBe(0); expect(server.effects).toBe(0)
    expect(pendingResolutions()).toHaveLength(1)
    server.authenticated = true
    expect((await review.recheck(signal())).kind).toBe('committed'); expect(server.effects).toBe(1)
  })

  it('S4-WPE-CHAOS-09 conflict path end to end: the index stays Open until an explicit decision and then shows the exact successor', async () => {
    expect((await loadSyncConflicts(vessel, 0, signal())).items[0].state).toBe('Open')
    const review = await openCrewConflictReview(vessel, item, signal())
    expect((await loadSyncConflicts(vessel, 0, signal())).items[0].state).toBe('Open') // opening a review resolves nothing
    const result = await review.resolve(decide('keepServer'), signal())
    const listed = (await loadSyncConflicts(vessel, 0, signal())).items[0]
    expect(listed.state).toBe('Resolved'); expect(result.kind === 'committed' && listed.resolutionCommandId === result.outcome.commandId).toBe(true)
    expect(server.value).toBe('SERVER-B'); expect(server.effects).toBe(1)
  })

  it('S4-WPE-CHAOS-10 missing device key (captured elsewhere): review refuses without discarding anything', async () => {
    chaos.keyPresent = false
    const failure = await openCrewConflictReview(vessel, item, signal()).catch(error => error)
    expect(classifyReviewFailure(failure)).toBe('otherDevice'); expect(disk().get(conflictId)).toBeDefined(); expect(server.replays).toBe(0)
  })
})
