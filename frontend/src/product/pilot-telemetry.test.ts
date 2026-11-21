import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { decodePilotSummary, PilotTelemetryQueue, pilotTelemetryOptedOut, setPilotTelemetryOptOut, validPilotEvent, type PilotEvent } from './pilot-telemetry'

// S11 WP-B DEV-072: closed client schema, local batching with retained idempotency keys, opt-out.
const session = { tenantId: '10000000-0000-4000-8000-000000000002', actorId: '10000000-0000-4000-8000-000000000003', csrfToken: 'synthetic-only' }
const at = '2026-11-20T10:00:00.000Z'
const ok = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const refused = (status: number, code: string) => new Response(JSON.stringify({ code }), { status, headers: { 'Content-Type': 'application/json' } })
const metric = (metricId: string, state = 'UNKNOWN', value: string | null = null, contributors: number | null = null) =>
  ({ metricId, definitionVersion: 1, unitCode: 'permille', state, value, contributors, reasons: state === 'UNKNOWN' ? ['NO_OBSERVATIONS'] : [], breakdown: [] })
const ids = ['pilot.journey.completion', 'pilot.role.adoption', 'pilot.ai.correction_rate', 'pilot.sync.conflicts', 'pilot.evidence.search_p95']
const summary = (items = ids.map(id => metric(id))) => ({ contractVersion: 'metrics.v1', windowDays: 7, from: at, asOf: at, basis: 'DECLARED_UNVERIFIED',
  retentionState: 'CONFIGURED', areas: ['SHIP'], items })

beforeEach(() => { localStorage.clear(); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); localStorage.clear() })

describe('pilot telemetry client', () => {
  it('accepts only closed codes and never a free-text, identity or mis-shaped event', () => {
    const base: PilotEvent = { kind: 'JOURNEY_COMPLETED', area: 'SHIP', journey: 'SHIP_DEPARTURE', aiSource: null, durationMs: null, occurredAt: at }
    expect(validPilotEvent(base)).toBe(true)
    expect(validPilotEvent({ ...base, journey: 'TUG_PLAN' })).toBe(false)
    expect(validPilotEvent({ ...base, journey: 'SHIP_DEPARTURE REF-42 J. Doe' as never })).toBe(false)
    expect(validPilotEvent({ ...base, kind: 'ROLE_ACTIVE' })).toBe(false)
    expect(validPilotEvent({ ...base, kind: 'AI_SUGGESTION_CORRECTED', journey: null, aiSource: 'TUG_RECOMMENDATION' })).toBe(false)
    expect(validPilotEvent({ ...base, kind: 'EVIDENCE_SEARCH', journey: null, durationMs: 600001 })).toBe(false)
    expect(validPilotEvent({ ...base, occurredAt: '2026-11-20T13:00:00+03:00' })).toBe(false)
  })

  it('sends one batch, keeps its idempotency key across a retry and clears it only after success', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(refused(503, 'service_unavailable'))
      .mockResolvedValueOnce(ok({ batchId: '10000000-0000-4000-8000-000000000009', accepted: 2, schemaVersion: 'pilot-event.v1', replayed: false }))
    let n = 0
    const queue = new PilotTelemetryQueue(session, () => true, () => `20000000-0000-4000-8000-00000000000${++n}`)
    expect(queue.record({ kind: 'ROLE_ACTIVE', area: 'TUG', journey: null, aiSource: null, durationMs: null, occurredAt: at })).toBe(true)
    expect(queue.record({ kind: 'SYNC_CONFLICT', area: 'TUG', journey: null, aiSource: null, durationMs: null, occurredAt: at })).toBe(true)
    expect(await queue.flush(new AbortController().signal)).toBe('RETRY')
    expect(await queue.flush(new AbortController().signal)).toBe('SENT')
    const [first, second] = fetch.mock.calls.map(c => c[1]!)
    expect(new Headers(first.headers).get('Idempotency-Key')).toBe(new Headers(second.headers).get('Idempotency-Key'))
    expect(second.body).toBe(first.body)
    expect(JSON.parse(String(second.body))).toEqual({ events: [expect.objectContaining({ kind: 'ROLE_ACTIVE' }), expect.objectContaining({ kind: 'SYNC_CONFLICT' })] })
    expect(String(second.body)).not.toContain(session.actorId)
    expect(String(second.body)).not.toContain(session.tenantId)
    expect(queue.size).toBe(0)
    expect(await queue.flush(new AbortController().signal)).toBe('IDLE')
  })

  it('drops everything when the server refuses enrolment or retention, and opting out discards the queue', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(refused(503, 'retention_unconfigured'))
    const queue = new PilotTelemetryQueue(session, () => true)
    queue.record({ kind: 'ROLE_ACTIVE', area: 'SHIP', journey: null, aiSource: null, durationMs: null, occurredAt: at })
    expect(await queue.flush(new AbortController().signal)).toBe('DROPPED')
    expect(queue.size).toBe(0)
    queue.record({ kind: 'ROLE_ACTIVE', area: 'SHIP', journey: null, aiSource: null, durationMs: null, occurredAt: at })
    setPilotTelemetryOptOut(true)
    expect(pilotTelemetryOptedOut()).toBe(true)
    expect(queue.size).toBe(0)
    expect(queue.record({ kind: 'ROLE_ACTIVE', area: 'SHIP', journey: null, aiSource: null, durationMs: null, occurredAt: at })).toBe(false)
    expect(await queue.flush(new AbortController().signal)).toBe('DISABLED')
    setPilotTelemetryOptOut(false)
    expect(pilotTelemetryOptedOut()).toBe(false)
  })

  it('does not send while offline or not enrolled', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch')
    const notEnrolled = new PilotTelemetryQueue(session, () => false)
    expect(notEnrolled.record({ kind: 'ROLE_ACTIVE', area: 'SHIP', journey: null, aiSource: null, durationMs: null, occurredAt: at })).toBe(false)
    const queue = new PilotTelemetryQueue(session, () => true)
    queue.record({ kind: 'ROLE_ACTIVE', area: 'SHIP', journey: null, aiSource: null, durationMs: null, occurredAt: at })
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    expect(await queue.flush(new AbortController().signal)).toBe('RETRY')
    expect(fetch).not.toHaveBeenCalled()
    expect(queue.size).toBe(1)
  })

  it('fails closed on a value outside VALUE or a zero substituted for an unknown metric', () => {
    expect(decodePilotSummary(summary(), 7).items).toHaveLength(5)
    expect(() => decodePilotSummary(summary(ids.map(id => metric(id, 'UNKNOWN', '0'))), 7)).toThrow()
    expect(() => decodePilotSummary(summary(ids.map(id => metric(id, 'SUPPRESSED', '3', 3))), 7)).toThrow()
    expect(() => decodePilotSummary(summary(ids.map(id => metric(id, 'VALUE', null, 6))), 7)).toThrow()
    expect(() => decodePilotSummary({ ...summary(), basis: 'VERIFIED' }, 7)).toThrow()
    expect(() => decodePilotSummary(summary(), 7 - 1)).toThrow()
  })
})
