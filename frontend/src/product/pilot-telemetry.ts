import type { components } from '../generated/api'
import { ApiError, request } from './api'
import { timestamp } from './date-validation'

// S11 WP-B DEV-072 (18.01 Backlog row 73, 19.01 paragraph:136-161). Privacy-safe pilot telemetry
// client: closed codes only, no identity, record id, vessel id or free text. Events are queued
// locally per tenant+actor session and sent in batches with an Idempotency-Key that is kept until
// the outcome is known. The server derives tenant, contributor and role; this client never sends them.
export type PilotKind = 'JOURNEY_STARTED' | 'JOURNEY_COMPLETED' | 'JOURNEY_ABANDONED' | 'ROLE_ACTIVE' | 'AI_SUGGESTION_ACCEPTED' |
  'AI_SUGGESTION_CORRECTED' | 'AI_SUGGESTION_REJECTED' | 'SYNC_CONFLICT' | 'EVIDENCE_SEARCH'
export type PilotArea = 'SHIP' | 'TUG'
export type PilotJourney = 'SHIP_DEPARTURE' | 'SHIP_CERTIFICATE' | 'SHIP_CREW' | 'SHIP_PMS' | 'TUG_JOB' | 'TUG_ASSIGNMENT' | 'TUG_READINESS' | 'TUG_PLAN' | 'TUG_EXECUTION'
export type PilotAiSource = 'OCR' | 'VOICE' | 'TUG_RECOMMENDATION'
export type PilotEvent = { kind: PilotKind; area: PilotArea; journey: PilotJourney | null; aiSource: PilotAiSource | null; durationMs: number | null; occurredAt: string }
export type PilotCapabilities = components['schemas']['PilotMetricCapabilitiesDto']
export type PilotMetricState = 'VALUE' | 'SUPPRESSED' | 'UNKNOWN' | 'UNAVAILABLE'
export type PilotMetric = Omit<components['schemas']['PilotMetricResult'], 'state'> & { state: PilotMetricState }
export type PilotSummary = Omit<components['schemas']['PilotMetricSummaryDto'], 'items'> & { items: PilotMetric[] }
export const pilotMetricIds = ['pilot.journey.completion', 'pilot.role.adoption', 'pilot.ai.correction_rate', 'pilot.sync.conflicts', 'pilot.evidence.search_p95'] as const

const kinds: readonly PilotKind[] = ['JOURNEY_STARTED', 'JOURNEY_COMPLETED', 'JOURNEY_ABANDONED', 'ROLE_ACTIVE', 'AI_SUGGESTION_ACCEPTED',
  'AI_SUGGESTION_CORRECTED', 'AI_SUGGESTION_REJECTED', 'SYNC_CONFLICT', 'EVIDENCE_SEARCH']
const shipJourneys: readonly PilotJourney[] = ['SHIP_DEPARTURE', 'SHIP_CERTIFICATE', 'SHIP_CREW', 'SHIP_PMS']
const tugJourneys: readonly PilotJourney[] = ['TUG_JOB', 'TUG_ASSIGNMENT', 'TUG_READINESS', 'TUG_PLAN', 'TUG_EXECUTION']
const states: readonly PilotMetricState[] = ['VALUE', 'SUPPRESSED', 'UNKNOWN', 'UNAVAILABLE']
const maxQueued = 200, maxBatch = 50
const object = (x: unknown): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x)
const exact = (x: Record<string, unknown>, keys: string[]) => Object.keys(x).length === keys.length && keys.every(k => k in x)
const codes = (x: unknown) => Array.isArray(x) && x.length <= 16 && x.every(c => typeof c === 'string' && /^[A-Z][A-Z0-9_]{1,63}$/.test(c))
const count = (x: unknown) => typeof x === 'number' && Number.isSafeInteger(x) && x >= 0
function fail(): never { throw new ApiError(502, 'invalid_pilot_metrics_contract') }

// Shape check matching PilotMetricsPolicy.Validate; an invalid event is dropped locally, never sent.
export function validPilotEvent(e: PilotEvent): boolean {
  if (!kinds.includes(e.kind) || (e.area !== 'SHIP' && e.area !== 'TUG')) return false
  const journey = e.kind.startsWith('JOURNEY_'), ai = e.kind.startsWith('AI_')
  if (journey !== (e.journey !== null) || ai !== (e.aiSource !== null) || (e.kind === 'EVIDENCE_SEARCH') !== (e.durationMs !== null)) return false
  if (e.journey !== null && !(e.area === 'SHIP' ? shipJourneys : tugJourneys).includes(e.journey)) return false
  if (e.aiSource !== null && (!['OCR', 'VOICE', 'TUG_RECOMMENDATION'].includes(e.aiSource) || e.area === 'SHIP' && e.aiSource === 'TUG_RECOMMENDATION')) return false
  if (e.durationMs !== null && (!Number.isSafeInteger(e.durationMs) || e.durationMs < 0 || e.durationMs > 600000)) return false
  return timestamp(e.occurredAt) && e.occurredAt.endsWith('Z')
}

export function decodePilotCapabilities(x: unknown): PilotCapabilities {
  if (!object(x) || !exact(x, ['canContribute', 'canRead', 'retentionState', 'retentionDays', 'schemaVersion', 'maxEventsPerBatch', 'requestsPerMinute',
    'kinds', 'areas', 'shipJourneys', 'tugJourneys', 'aiSources', 'windowDays', 'minContributors']) || typeof x.canContribute !== 'boolean' ||
    typeof x.canRead !== 'boolean' || !['CONFIGURED', 'UNCONFIGURED'].includes(x.retentionState as string) || x.schemaVersion !== 'pilot-event.v1' ||
    (x.retentionState === 'CONFIGURED' ? !count(x.retentionDays) : x.retentionDays !== null) ||
    x.retentionState === 'UNCONFIGURED' && x.canContribute) fail()
  return x as PilotCapabilities
}

export function decodePilotSummary(x: unknown, windowDays: number): PilotSummary {
  if (!object(x) || !exact(x, ['contractVersion', 'windowDays', 'from', 'asOf', 'basis', 'retentionState', 'areas', 'items']) || x.contractVersion !== 'metrics.v1' ||
    !count(x.windowDays) || (x.windowDays as number) > windowDays || typeof x.from !== 'string' || !timestamp(x.from) || typeof x.asOf !== 'string' || !timestamp(x.asOf) ||
    x.basis !== 'DECLARED_UNVERIFIED' || !['CONFIGURED', 'UNCONFIGURED'].includes(x.retentionState as string) || !Array.isArray(x.areas) || !Array.isArray(x.items) ||
    x.items.length !== pilotMetricIds.length) fail()
  x.items.forEach((m, index) => {
    if (!object(m) || !exact(m, ['metricId', 'definitionVersion', 'unitCode', 'state', 'value', 'contributors', 'reasons', 'breakdown']) || m.metricId !== pilotMetricIds[index] ||
      !states.includes(m.state as PilotMetricState) || !codes(m.reasons) || !Array.isArray(m.breakdown)) fail()
    // A value exists ONLY in state VALUE; suppressed/unknown/unavailable never render as zero.
    if (m.state === 'VALUE' ? typeof m.value !== 'string' || !/^(0|[1-9][0-9]{0,17})$/.test(m.value) || !count(m.contributors) : m.value !== null || m.contributors !== null) fail()
    for (const c of m.breakdown as unknown[]) {
      if (!object(c) || !exact(c, ['dimension', 'key', 'state', 'value', 'contributors', 'reasons']) || !states.includes(c.state as PilotMetricState) ||
        (c.state === 'VALUE' ? typeof c.value !== 'string' || !count(c.contributors) : c.value !== null || c.contributors !== null)) fail()
    }
  })
  return x as PilotSummary
}

export const loadPilotCapabilities = async (signal: AbortSignal) => decodePilotCapabilities(await request<unknown>('pilot-metrics/capabilities', signal))
export const loadPilotSummary = async (windowDays: 7 | 30, signal: AbortSignal) =>
  decodePilotSummary(await request<unknown>(`pilot-metrics/summary?windowDays=${windowDays}`, signal), windowDays)

type Stored = { pending: PilotEvent[]; inflight: { key: string; events: PilotEvent[] } | null }
const optOutKey = 'poseidon.pilot-telemetry.opt-out'
const storageKey = (tenantId: string, actorId: string) => `poseidon.pilot-telemetry.v1:${tenantId}:${actorId}`
function read(key: string): Stored {
  try {
    const value = JSON.parse(localStorage.getItem(key) ?? 'null') as Stored | null
    if (value && Array.isArray(value.pending) && (value.inflight === null || Array.isArray(value.inflight?.events)))
      return { pending: value.pending.filter(validPilotEvent).slice(-maxQueued), inflight: value.inflight }
  } catch { /* Storage may be unavailable; telemetry is best effort and never blocks work. */ }
  return { pending: [], inflight: null }
}
function write(key: string, value: Stored) { try { localStorage.setItem(key, JSON.stringify(value)) } catch { /* best effort */ } }

export function pilotTelemetryOptedOut(): boolean { try { return localStorage.getItem(optOutKey) === '1' } catch { return true } }
export function setPilotTelemetryOptOut(optOut: boolean) {
  try {
    if (optOut) {
      localStorage.setItem(optOutKey, '1')
      // Opting out discards everything queued in this browser.
      for (let i = localStorage.length - 1; i >= 0; i--) { const key = localStorage.key(i); if (key?.startsWith('poseidon.pilot-telemetry.v1:')) localStorage.removeItem(key) }
    } else localStorage.removeItem(optOutKey)
  } catch { /* best effort */ }
}

export type PilotSession = { tenantId: string; actorId: string; csrfToken: string }
export type PilotFlushOutcome = 'SENT' | 'IDLE' | 'RETRY' | 'DROPPED' | 'DISABLED'

export class PilotTelemetryQueue {
  private readonly key: string
  constructor(private readonly session: PilotSession, private readonly enabled: () => boolean, private readonly newKey: () => string = () => crypto.randomUUID()) {
    this.key = storageKey(session.tenantId, session.actorId)
  }
  get size() { const s = read(this.key); return s.pending.length + (s.inflight?.events.length ?? 0) }
  record(event: Omit<PilotEvent, 'occurredAt'> & { occurredAt?: string }) {
    if (pilotTelemetryOptedOut() || !this.enabled()) return false
    const full: PilotEvent = { ...event, occurredAt: event.occurredAt ?? new Date().toISOString() }
    if (!validPilotEvent(full)) return false
    const s = read(this.key)
    s.pending = [...s.pending, full].slice(-maxQueued)
    write(this.key, s)
    return true
  }
  async flush(signal: AbortSignal): Promise<PilotFlushOutcome> {
    if (pilotTelemetryOptedOut() || !this.enabled()) return 'DISABLED'
    if (typeof navigator !== 'undefined' && !navigator.onLine) return 'RETRY'
    const s = read(this.key)
    // An unresolved batch is resent with its original key and body before anything new.
    if (!s.inflight) {
      if (s.pending.length === 0) return 'IDLE'
      s.inflight = { key: this.newKey(), events: s.pending.slice(0, maxBatch) }
      s.pending = s.pending.slice(maxBatch)
      write(this.key, s)
    }
    try {
      await request<unknown>('pilot-metrics/events', signal, {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': this.session.csrfToken, 'Idempotency-Key': s.inflight.key },
        body: JSON.stringify({ events: s.inflight.events }),
      })
      write(this.key, { ...s, inflight: null })
      return 'SENT'
    } catch (error) {
      if (!(error instanceof ApiError) || error.status === 429 || error.status >= 500 && error.code !== 'retention_unconfigured') return 'RETRY'
      // Refused by the server (invalid, not enrolled, retention unset, key reuse): never retried.
      write(this.key, error.status === 403 || error.code === 'retention_unconfigured' ? { pending: [], inflight: null } : { ...s, inflight: null })
      return 'DROPPED'
    }
  }
}

// ---------------------------------------------------------------------------------------------
// S11 gate B1 (DEV-072 AC: role adoption, journey completion, AI correction, sync conflicts and
// evidence search time measurable). The app-shell host (PilotTelemetryHost) starts ONE queue per
// enrolled session; product screens report closed-code observations through the helpers below.
// Every helper is a no-op unless the host started telemetry for an enrolled membership with a
// configured retention (capabilities.canContribute), never throws and never awaits the network on
// the caller's path: telemetry is best effort and cannot block, delay or fail a user action.
// No helper accepts a record id, name, query or free text; a vessel id is used only locally to
// classify SHIP/TUG through the authorized directory read and is never queued or sent.
// ---------------------------------------------------------------------------------------------
export const pilotFlushIntervalMs = 60000
type PilotRuntime = { queue: PilotTelemetryQueue; session: PilotSession; flush: () => void; stop: () => void }
let runtime: PilotRuntime | null = null
const vesselAreas = new Map<string, Promise<PilotArea | null>>()
const openJourneys = new Set<string>()

export const pilotTelemetryActive = () => runtime !== null
export const pilotFlushNow = () => runtime?.flush()

export function pilotRecord(event: Omit<PilotEvent, 'occurredAt'>): boolean {
  try { return runtime?.queue.record(event) ?? false } catch { return false }
}

// Path -> pilot area for screens that belong to exactly one operating mode (19.01 p139-140).
// Shared screens (audit, system health, pilot gate, directory) carry no area and emit no adoption.
const tugPaths = /^\/app\/(tug-[a-z-]+|towage-[a-z-]+|drydock-planning)(?:\/|$)/
const shipPaths = /^\/app\/(ship-demo|fuel-bunker-log|port-activity-sof|certificate-command-center|renewal-workflow|pre-survey-checklist|survey-planner|noon-report|deck-daily-log|engine-daily-log|crew-roster|crew-certificates|training-position-matrix|safe-manning|departure-signoff|pms-command-center|defects|work-order-center|equipment-hierarchy|maintenance-plan|critical-spares)(?:\/|$)/
export function pilotAreaForPath(pathname: string): PilotArea | null {
  return tugPaths.test(pathname) ? 'TUG' : shipPaths.test(pathname) ? 'SHIP' : null
}

// Role adoption (19.01 p159-161): at most one ROLE_ACTIVE per actor, area and UTC day in this
// browser. The server derives the role group from the trusted actor; the client sends no role.
export function pilotRoleActive(area: PilotArea | null, now = new Date()): boolean {
  if (!runtime || !area) return false
  const key = `poseidon.pilot-telemetry.v1:role:${runtime.session.tenantId}:${runtime.session.actorId}:${area}`, day = now.toISOString().slice(0, 10)
  try { if (localStorage.getItem(key) === day) return false } catch { return false }
  const recorded = pilotRecord({ kind: 'ROLE_ACTIVE', area, journey: null, aiSource: null, durationMs: null })
  if (recorded) try { localStorage.setItem(key, day) } catch { /* best effort */ }
  return recorded
}

// Task success (19.01 p141-143): a journey is STARTED when the user first attempts its terminal
// command, COMPLETED when the server confirms it and ABANDONED when the page is left after an
// attempt without a confirmed completion. A never-attempted journey is not observed (not a failure).
export function pilotJourneyAttempt(area: PilotArea, journey: PilotJourney) {
  const key = `${area}:${journey}`
  if (!runtime || openJourneys.has(key)) return
  if (pilotRecord({ kind: 'JOURNEY_STARTED', area, journey, aiSource: null, durationMs: null })) openJourneys.add(key)
}
export function pilotJourneyCompleted(area: PilotArea, journey: PilotJourney) {
  if (!openJourneys.delete(`${area}:${journey}`)) return
  pilotRecord({ kind: 'JOURNEY_COMPLETED', area, journey, aiSource: null, durationMs: null })
}
export function pilotAbandonOpenJourneys() {
  for (const key of [...openJourneys]) {
    const [area, journey] = key.split(':') as [PilotArea, PilotJourney]
    pilotRecord({ kind: 'JOURNEY_ABANDONED', area, journey, aiSource: null, durationMs: null })
  }
  openJourneys.clear()
}

// AI quality (19.01 p156-158): one observation per human decision on an AI suggestion.
export type PilotAiOutcome = 'ACCEPTED' | 'CORRECTED' | 'REJECTED'
export const pilotAiOutcome = (area: PilotArea, aiSource: PilotAiSource, outcome: PilotAiOutcome) =>
  pilotRecord({ kind: `AI_SUGGESTION_${outcome}`, area, journey: null, aiSource, durationMs: null })
// Offline (19.01 p153-155): one observation per sync conflict a human resolved in the conflict center.
export const pilotSyncConflict = (area: PilotArea) => pilotRecord({ kind: 'SYNC_CONFLICT', area, journey: null, aiSource: null, durationMs: null })
// Evidence prep (19.01 p150-152): elapsed time of one evidence search until its result is shown.
export const pilotEvidenceSearch = (area: PilotArea, durationMs: number) =>
  Number.isFinite(durationMs) && durationMs >= 0 &&
  pilotRecord({ kind: 'EVIDENCE_SEARCH', area, journey: null, aiSource: null, durationMs: Math.min(Math.round(durationMs), 600000) })

// Resolves a vessel's operating mode through the existing authorized directory read, only while
// telemetry is active (no extra request otherwise). The id stays in this module's memory.
export function pilotVesselArea(vesselId: string): Promise<PilotArea | null> {
  if (!runtime || !vesselId) return Promise.resolve(null)
  const id = vesselId.toLowerCase()
  let found = vesselAreas.get(id)
  if (!found) {
    const c = new AbortController(), timer = setTimeout(() => c.abort(), 10000)
    found = request<{ kind?: unknown }>(`vessels/${encodeURIComponent(id)}`, c.signal)
      .then(v => v?.kind === 'Tug' ? 'TUG' as const : v?.kind === 'Ship' ? 'SHIP' as const : null)
      .catch(() => { vesselAreas.delete(id); return null })
      .finally(() => clearTimeout(timer))
    vesselAreas.set(id, found)
  }
  return found
}
export function pilotForVessel(vesselId: string, emit: (area: PilotArea) => void) {
  if (!runtime) return
  void pilotVesselArea(vesselId).then(area => { if (area) emit(area) }).catch(() => undefined)
}

// Starts the shell runtime for an enrolled session and returns its stop function. Flushes every
// intervalMs, when the browser comes online and when the page is hidden or left; a pending batch
// survives offline periods and reloads in localStorage (bounded queue, original idempotency key).
export function startPilotTelemetry(session: PilotSession, options: { intervalMs?: number; newKey?: () => string } = {}): () => void {
  runtime?.stop()
  const queue = new PilotTelemetryQueue(session, () => true, options.newKey)
  let flushing = false, stopped = false
  const flush = () => {
    if (flushing || stopped) return
    flushing = true
    const c = new AbortController(), timer = setTimeout(() => c.abort(), 10000)
    void queue.flush(c.signal).catch(() => 'RETRY').finally(() => { clearTimeout(timer); flushing = false })
  }
  const hidden = () => { if (document.visibilityState === 'hidden') flush() }
  const pagehide = () => { pilotAbandonOpenJourneys(); flush() }
  const interval = setInterval(flush, options.intervalMs ?? pilotFlushIntervalMs)
  window.addEventListener('online', flush)
  window.addEventListener('pagehide', pagehide)
  document.addEventListener('visibilitychange', hidden)
  const current: PilotRuntime = { queue, session, flush, stop: () => {
    stopped = true; clearInterval(interval)
    window.removeEventListener('online', flush); window.removeEventListener('pagehide', pagehide); document.removeEventListener('visibilitychange', hidden)
    if (runtime === current) { runtime = null; openJourneys.clear(); vesselAreas.clear() }
  } }
  runtime = current
  return current.stop
}
