import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TugReadinessWorkspace, tugReadinessRoute } from './TugReadinessWorkspace'
import { decodeReadiness, decodeTowEquipment, decodeCrewReadiness } from './tug-readiness-api'
import { changeAppLanguage } from '../i18n'

// S7 WP-B DEV-083..086 UI. Synthetic identities and FICTIONAL data only; no GO, acknowledgement or verification fixture.
const vessel = '11111111-1111-4111-8111-111111111111', job = '22222222-2222-4222-8222-222222222222', assignmentId = '33333333-3333-4333-8333-333333333333'
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666'
const H = 'A'.repeat(64)
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const assignment = (patch: Record<string, unknown> = {}) => ({ id: assignmentId, vesselId: vessel, jobId: job, jobRevision: 2, tugId: vessel, plannedStart: '2026-11-21T08:00:00Z',
  plannedEnd: '2026-11-21T10:00:00Z', role: 'Lead', state: 'Proposed', masterAck: 'Pending', scopeHash: H, revision: 1, version: 7, proposedAt: '2026-11-20T08:00:00Z',
  cancelledAt: null, cancelReason: null, replayed: false, sourceVersion: 'synthetic', ...patch })
const check = (category: string, result: string, reason: string) => ({ category, result, reason, critical: true, sourceId: null, sourceRevision: null })
const readiness = (patch: Record<string, unknown> = {}) => ({ assignment: assignment(), state: 'REVIEW_REQUIRED',
  categories: [{ category: 'weather', result: 'UNKNOWN', reasons: ['WholeWindowWeatherSourceUnavailable'] }], criticalBlockers: [],
  reviewReasons: ['weather:WholeWindowWeatherSourceUnavailable'], conditions: [], assessmentHash: H, requiredBollardPull: '40', availableBollardPull: null, bollardPullMargin: null,
  checks: [check('weather', 'UNKNOWN', 'WholeWindowWeatherSourceUnavailable')], conflicts: [], decisions: [], authorities: [{ id: grant, duty: 'TugMaster' }], canDecide: true,
  assessedAt: '2026-11-20T08:00:00Z', decisionAuthority: 'MasterOrTugMasterHumanDecisionRequired;EngineNeverIssuesGo', sourceLocator: 'synthetic', ...patch })
const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let readinessBody: unknown = readiness()
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; readinessBody = readiness()
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST' || init?.method === 'PUT') { commands.push({ url, init }); return json({}, 200) }
    if (url.includes('tug-readiness?')) return json(readinessBody)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('parses closed routes and refuses unknown or malformed query parameters', () => {
  expect(tugReadinessRoute(`/app/tug-readiness?vessel=${vessel}&assignment=${assignmentId}`)).toMatchObject({ screen: 'tug-readiness', invalid: false })
  expect(tugReadinessRoute(`/app/tug-readiness?vessel=${vessel}&extra=1`).invalid).toBe(true)
  expect(tugReadinessRoute(`/app/tug-readiness?vessel=not-a-guid`).invalid).toBe(true)
})

it('shows review required, never a ready tone, disables GO and sends an exact NO-GO human record', async () => {
  history.replaceState(null, '', `/app/tug-readiness?vessel=${vessel}&assignment=${assignmentId}`)
  const user = userEvent.setup()
  render(<TugReadinessWorkspace/>)
  expect(await screen.findAllByText('Review required')).not.toHaveLength(0)
  const badge = document.querySelector('[data-readiness]')!
  expect(badge.getAttribute('data-readiness')).toBe('REVIEW_REQUIRED')
  expect(badge.getAttribute('data-tone')).toBe('warning')
  const go = screen.getByRole('radio', { name: 'GO' })
  expect(go).toBeDisabled()
  await user.click(screen.getByRole('radio', { name: 'NO-GO' }))
  await user.type(screen.getByLabelText('Reason'), 'FICTIONAL comms unconfirmed')
  await user.click(screen.getByRole('button', { name: 'Record' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toBe(`/api/v1/tug-readiness/decisions?vessel=${vessel}`)
  const headers = commands[0].init.headers as Record<string, string>
  expect(headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ assignmentId, expectedAssessmentHash: H, decision: 'NO_GO', reason: 'FICTIONAL comms unconfirmed', authorityId: grant })
})

it('hides the decision form when the actor has no appointment', async () => {
  readinessBody = readiness({ canDecide: false, authorities: [] })
  history.replaceState(null, '', `/app/tug-readiness?vessel=${vessel}&assignment=${assignmentId}`)
  render(<TugReadinessWorkspace/>)
  expect(await screen.findAllByText('Review required')).not.toHaveLength(0)
  expect(screen.queryByRole('radio', { name: 'GO' })).toBeNull()
})

it('decoders refuse contradictory projections instead of displaying them', () => {
  expect(() => decodeReadiness(readiness({ state: 'READY' }), vessel, assignmentId)).toThrow()
  expect(() => decodeReadiness(readiness({ checks: [check('tow_equipment', 'FAIL', 'Winch:CriticalEquipmentFailed')] }), vessel, assignmentId)).toThrow()
  expect(() => decodeReadiness(readiness({ decisions: [{ id: grant, assignmentId, assignmentRevision: 1, decision: 'GO', assessedState: 'NOT_READY', assessmentHash: H,
    reason: 'x', duty: 'TugMaster', recordedAt: '2026-11-20T08:00:00Z', replayed: false }] }), vessel, assignmentId)).toThrow()
  expect(decodeReadiness(readiness(), vessel, assignmentId).state).toBe('REVIEW_REQUIRED')
  expect(() => decodeCrewReadiness({ tugId: vessel, assignmentId, state: 'READY', evaluatorState: 'Unavailable', evaluatorSource: null, checks: [], assessedAt: '2026-11-20T08:00:00Z',
    validUntil: null, coverage: 'x' }, assignmentId)).toThrow()
  expect(() => decodeTowEquipment({ assessment: { tugId: vessel, state: 'OK', items: [], checks: [], assessedAt: '2026-11-20T08:00:00Z' }, canWrite: false, canVerify: false,
    authorities: [], kinds: [], conditions: [], sourceLocator: 'x' }, vessel)).toThrow()
})

it('keeps GO disabled on a CONDITIONAL assessment and explains the server rule (GO only on READY)', async () => {
  readinessBody = readiness({ state: 'CONDITIONAL', categories: [{ category: 'weather', result: 'CONDITIONAL', reasons: ['FICTIONAL_WIND_LIMIT'] }], reviewReasons: [],
    conditions: ['weather:FICTIONAL_WIND_LIMIT'], checks: [check('weather', 'CONDITIONAL', 'FICTIONAL_WIND_LIMIT')] })
  history.replaceState(null, '', `/app/tug-readiness?vessel=${vessel}&assignment=${assignmentId}`)
  render(<TugReadinessWorkspace/>)
  expect(await screen.findAllByText('Conditional')).not.toHaveLength(0)
  expect(screen.getByRole('radio', { name: 'GO' })).toBeDisabled()
  expect(screen.getByRole('radio', { name: 'NO-GO' })).toBeEnabled()
  const hint = document.getElementById('tug-go-hint')!
  expect(hint.getAttribute('data-state')).toBe('go-conditional')
  expect(hint.textContent).toMatch(/GO needs a READY assessment/)
  expect(commands).toHaveLength(0)
})
