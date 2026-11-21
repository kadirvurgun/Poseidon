import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, waitFor } from '@testing-library/react'
import { PilotTelemetryHost } from './PilotTelemetryHost'
import { pilotAiOutcome, pilotAreaForPath, pilotEvidenceSearch, pilotFlushNow, pilotForVessel, pilotJourneyAttempt, pilotJourneyCompleted, pilotRecord,
  pilotSyncConflict, pilotTelemetryActive, setPilotTelemetryOptOut } from './pilot-telemetry'

// S11 gate B1 (DEV-072): the app shell feeds the privacy-safe pilot telemetry for an enrolled session only, and the
// five AC metrics (role adoption, journey completion, AI correction, sync conflicts, evidence search time) are emitted
// as closed codes with no identity, record id, vessel id or free text.
const tenantId = '10000000-0000-4000-8000-0000000000a1', actorId = '10000000-0000-4000-8000-0000000000a2', vesselId = '10000000-0000-4000-8000-0000000000a3'
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const caps = (canContribute: boolean) => ({ canContribute, canRead: false, retentionState: 'CONFIGURED', retentionDays: 30, schemaVersion: 'pilot-event.v1',
  maxEventsPerBatch: 50, requestsPerMinute: 30, kinds: [], areas: ['SHIP', 'TUG'], shipJourneys: [], tugJourneys: [], aiSources: [], windowDays: [7, 30], minContributors: 5 })
const session = { authenticated: true, setupRequired: false, actorId, tenantId, canRead: true, csrfToken: 'synthetic-csrf', tenantName: 'FICTIONAL', canReadCompanies: false, canCreate: false, canSearch: false }
const posted: string[] = []

function serve(enrolled: boolean) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input)
    if (url.endsWith('/api/v1/auth/session')) return json(session)
    if (url.endsWith('/api/v1/pilot-metrics/capabilities')) return json(caps(enrolled))
    if (url.endsWith(`/api/v1/vessels/${vesselId}`)) return json({ id: vesselId, companyId: vesselId, code: 'FICTIONAL', name: 'FICTIONAL TUG', kind: 'Tug', version: 1, canEdit: false })
    if (url.endsWith('/api/v1/pilot-metrics/events')) { posted.push(String(init?.body)); return json({ batchId: tenantId, accepted: 1, schemaVersion: 'pilot-event.v1', replayed: false }) }
    return json({ code: 'not_found' }, 404)
  })
}
const events = () => posted.flatMap(body => (JSON.parse(body) as { events: Record<string, unknown>[] }).events)

beforeEach(() => { localStorage.clear(); posted.length = 0; vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear() })

describe('PilotTelemetryHost', () => {
  it('maps only single-mode screens to an area', () => {
    expect(pilotAreaForPath('/app/tug-job-board')).toBe('TUG')
    expect(pilotAreaForPath('/app/departure-signoff/' + vesselId)).toBe('SHIP')
    expect(pilotAreaForPath('/app/audit')).toBeNull()
    expect(pilotAreaForPath('/app/pilot-gate')).toBeNull()
  })

  it('starts for an enrolled session, records role adoption once and sends the five AC metrics without identity', async () => {
    serve(true)
    render(<PilotTelemetryHost pathname="/app/tug-job-board"/>)
    await waitFor(() => expect(events().map(e => e.kind)).toEqual(['ROLE_ACTIVE']))
    expect(events()[0]).toMatchObject({ kind: 'ROLE_ACTIVE', area: 'TUG', journey: null, aiSource: null, durationMs: null })

    pilotJourneyAttempt('TUG', 'TUG_JOB'); pilotJourneyAttempt('TUG', 'TUG_JOB'); pilotJourneyCompleted('TUG', 'TUG_JOB')
    pilotJourneyCompleted('SHIP', 'SHIP_DEPARTURE') // never attempted -> not observed
    pilotAiOutcome('SHIP', 'OCR', 'CORRECTED'); pilotAiOutcome('TUG', 'VOICE', 'REJECTED')
    pilotSyncConflict('SHIP')
    pilotEvidenceSearch('SHIP', 1234.6)
    pilotForVessel(vesselId, area => pilotAiOutcome(area, 'TUG_RECOMMENDATION', 'ACCEPTED'))
    await waitFor(() => expect(localStorage.getItem(`poseidon.pilot-telemetry.v1:${tenantId}:${actorId}`)).toContain('TUG_RECOMMENDATION'))
    await waitFor(() => expect(posted).toHaveLength(1))
    pilotFlushNow()
    await waitFor(() => expect(posted).toHaveLength(2))

    const sent = events()
    expect(sent.map(e => e.kind)).toEqual(['ROLE_ACTIVE', 'JOURNEY_STARTED', 'JOURNEY_COMPLETED', 'AI_SUGGESTION_CORRECTED', 'AI_SUGGESTION_REJECTED',
      'SYNC_CONFLICT', 'EVIDENCE_SEARCH', 'AI_SUGGESTION_ACCEPTED'])
    expect(sent.find(e => e.kind === 'EVIDENCE_SEARCH')).toMatchObject({ area: 'SHIP', durationMs: 1235 })
    expect(sent.at(-1)).toMatchObject({ area: 'TUG', aiSource: 'TUG_RECOMMENDATION' })
    for (const e of sent) expect(Object.keys(e).sort()).toEqual(['aiSource', 'area', 'durationMs', 'journey', 'kind', 'occurredAt'])
    for (const body of posted) for (const id of [tenantId, actorId, vesselId, 'synthetic-csrf', 'FICTIONAL']) expect(body).not.toContain(id)

    // Role adoption is once per actor, area and day.
    cleanup()
    render(<PilotTelemetryHost pathname="/app/tug-command"/>)
    await waitFor(() => expect(pilotTelemetryActive()).toBe(true))
    pilotFlushNow()
    await new Promise(r => setTimeout(r, 0))
    expect(events().filter(e => e.kind === 'ROLE_ACTIVE')).toHaveLength(1)
  })

  it('records an attempted but unconfirmed journey as abandoned when the page is left', async () => {
    serve(true)
    render(<PilotTelemetryHost pathname="/app/departure-signoff"/>)
    await waitFor(() => expect(posted).toHaveLength(1))
    pilotJourneyAttempt('SHIP', 'SHIP_DEPARTURE')
    window.dispatchEvent(new Event('pagehide'))
    await waitFor(() => expect(events().map(e => e.kind)).toEqual(['ROLE_ACTIVE', 'JOURNEY_STARTED', 'JOURNEY_ABANDONED']))
    expect(events().at(-1)).toMatchObject({ area: 'SHIP', journey: 'SHIP_DEPARTURE' })
  })

  it('stays off when the membership is not enrolled, the user opted out or the screen is not the product app', async () => {
    const fetch = serve(false)
    render(<PilotTelemetryHost pathname="/app/tug-job-board"/>)
    await waitFor(() => expect(fetch.mock.calls.some(c => String(c[0]).endsWith('/pilot-metrics/capabilities'))).toBe(true))
    await new Promise(r => setTimeout(r, 0))
    expect(pilotTelemetryActive()).toBe(false)
    expect(pilotRecord({ kind: 'SYNC_CONFLICT', area: 'SHIP', journey: null, aiSource: null, durationMs: null })).toBe(false)
    pilotForVessel(vesselId, () => { throw new Error('must not resolve while inactive') })
    expect(fetch.mock.calls.some(c => String(c[0]).includes('/vessels/'))).toBe(false)
    cleanup(); fetch.mockClear()

    setPilotTelemetryOptOut(true)
    render(<PilotTelemetryHost pathname="/app/tug-job-board"/>)
    render(<PilotTelemetryHost pathname="/agents"/>)
    await new Promise(r => setTimeout(r, 0))
    expect(fetch).not.toHaveBeenCalled()
    setPilotTelemetryOptOut(false)
  })
})
