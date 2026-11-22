import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DepartureReadinessWorkspace, readDepartureRoute } from './DepartureReadinessWorkspace'
import { decodeDepartureReadiness } from './departure-readiness-api'
import { changeAppLanguage } from '../i18n'

// S9 WP-A DEV-058/059 UI. Synthetic identities and FICTIONAL codes only; no RELEASE fixture.
const vessel = '11111111-1111-4111-8111-111111111111', actorId = '44444444-4444-4444-8444-444444444444'
const tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666', decisionId = '77777777-7777-4777-8777-777777777777'
const H = 'A'.repeat(64)
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const future = new Date(Date.now() + 60_000).toISOString()
const decision = (patch: Record<string, unknown> = {}) => ({ id: decisionId, vesselId: vessel, sequence: 1, previousDecisionId: null, decision: 'HOLD',
  assessedState: 'REVIEW_REQUIRED', assessmentMode: 'CURRENT', assessmentHash: H, windowFrom: '2026-11-21', windowTo: '2026-11-23', reason: 'FICTIONAL drill status unconfirmed',
  duty: 'Master', actorId, recordedAt: '2026-11-21T08:00:00Z', validity: 'STALE', replayed: false, ...patch })
const readiness = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, from: '2026-11-21', to: '2026-11-23', mode: 'CURRENT', state: 'REVIEW_REQUIRED',
  categories: [{ category: 'crew', result: 'PASS', reasons: ['SAFE_MANNING_ELIGIBLE'] }, { category: 'technical', result: 'PASS', reasons: ['CONDITION_ALERTS:NO_OPEN_EXCEPTIONS'] },
    { category: 'statutory', result: 'UNKNOWN', reasons: ['CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED'] }, { category: 'emergency', result: 'UNKNOWN', reasons: ['EMERGENCY_SOURCE_UNAVAILABLE'] }, { category: 'safety', result: 'PASS', reasons: ['NO_OPEN_HIGH_SEVERITY_EVENT'] }],
  criticalBlockers: [], reviewReasons: ['emergency:EMERGENCY_SOURCE_UNAVAILABLE', 'statutory:CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED'], conditions: [],
  why: [{ category: 'statutory', result: 'UNKNOWN', reason: 'CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED', sourceKind: null, sourceId: null, sourceRevision: null },
    { category: 'emergency', result: 'UNKNOWN', reason: 'EMERGENCY_SOURCE_UNAVAILABLE', sourceKind: null, sourceId: null, sourceRevision: null }],
  assessmentHash: H, assessedAt: '2026-11-21T08:00:00Z', validUntil: future, decisions: [decision()], sequence: 1, authorities: [{ id: grant, duty: 'Master' }], canDecide: true,
  allowedDecisions: ['HOLD', 'DENY'], decisionAuthority: 'MasterHumanDecisionRequired;EngineNeverReleases;NotADepartureClearance', sourceLocator: 'synthetic', ...patch })
const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let body: unknown = readiness(), commandStatus = 200, commandBody: unknown = {}
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; body = readiness(); commandStatus = 200; commandBody = decision({ id: '88888888-8888-4888-8888-888888888888', sequence: 2, previousDecisionId: decisionId, validity: 'CURRENT' })
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST') { commands.push({ url, init }); return json(commandBody, commandStatus) }
    if (url.includes('departure-readiness?')) return typeof body === 'number' ? json({ code: 'permission_denied' }, body) : json(body)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })
const open = () => history.replaceState(null, '', `/app/departure-signoff/${vessel}?from=2026-11-21&to=2026-11-23`)

it('parses closed routes and refuses unknown, partial or reversed windows', () => {
  expect(readDepartureRoute(`/app/departure-signoff/${vessel}?from=2026-11-21&to=2026-11-23`)).toMatchObject({ vessel, invalid: false })
  expect(readDepartureRoute(`/app/departure-signoff/${vessel}?from=2026-11-21`).invalid).toBe(true)
  expect(readDepartureRoute(`/app/departure-signoff/${vessel}?from=2026-11-23&to=2026-11-21`).invalid).toBe(true)
  expect(readDepartureRoute(`/app/departure-signoff/${vessel}?from=2026-11-21&to=2026-11-23&extra=1`).invalid).toBe(true)
  expect(readDepartureRoute('/app/departure-signoff/not-a-guid').invalid).toBe(true)
})

it('rejects a READY projection with an unknown category, a FAIL outside NOT_READY and RELEASE outside CURRENT READY', () => {
  expect(() => decodeDepartureReadiness(readiness({ state: 'READY' }), vessel)).toThrow()
  expect(() => decodeDepartureReadiness(readiness({ categories: readiness().categories.map(c => c.category === 'crew' ? { ...c, result: 'FAIL' } : c) }), vessel)).toThrow()
  expect(() => decodeDepartureReadiness(readiness({ allowedDecisions: ['RELEASE', 'HOLD', 'DENY'] }), vessel)).toThrow()
  expect(() => decodeDepartureReadiness(readiness({ decisions: [decision({ decision: 'RELEASE' })] }), vessel)).toThrow()
  expect(() => decodeDepartureReadiness(readiness({ categories: readiness().categories.slice(1) }), vessel)).toThrow()
  expect(decodeDepartureReadiness(readiness(), vessel).state).toBe('REVIEW_REQUIRED')
})

it('shows categories and the Why list, never a ready tone, disables RELEASE and records an exact HOLD bound to the assessment', async () => {
  open()
  const user = userEvent.setup()
  render(<DepartureReadinessWorkspace/>)
  expect(await screen.findAllByText('Review required')).not.toHaveLength(0)
  const badge = document.querySelector('[data-readiness]')!
  expect(badge.getAttribute('data-tone')).toBe('warning')
  expect(screen.getByText('EMERGENCY_SOURCE_UNAVAILABLE')).toBeInTheDocument()
  expect(document.querySelector('[data-category="emergency"] [data-result="UNKNOWN"]')).not.toBeNull()
  expect(document.querySelector('[data-validity="STALE"]')).not.toBeNull()
  expect(screen.getByRole('radio', { name: 'Release' })).toBeDisabled()
  await user.click(screen.getByRole('radio', { name: 'Hold' }))
  await user.type(screen.getByLabelText('Reason'), 'FICTIONAL emergency drill not evidenced')
  await user.click(screen.getByRole('button', { name: 'Record' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  const sent = JSON.parse(String(commands[0].init.body))
  expect(sent).toEqual({ from: '2026-11-21', to: '2026-11-23', expectedAssessmentHash: H, expectedSequence: 1, decision: 'HOLD', reason: 'FICTIONAL emergency drill not evidenced', authorityId: grant })
  expect(commands[0].url).toContain(`departure-signoff?vessel=${vessel}`)
  expect(new Headers(commands[0].init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
  expect(await screen.findByText('Decision recorded.')).toBeInTheDocument()
})

it('explains a stale assessment conflict and keeps a non-appointed reader read-only', async () => {
  open(); commandStatus = 409; commandBody = { code: 'assessment_stale', correlationId: 'synthetic' }
  const user = userEvent.setup()
  const view = render(<DepartureReadinessWorkspace/>)
  await user.click(await screen.findByRole('radio', { name: 'Deny' }))
  await user.type(screen.getByLabelText('Reason'), 'FICTIONAL crew rest not evidenced')
  await user.click(screen.getByRole('button', { name: 'Record' }))
  expect(await screen.findByText('The assessment changed. Review the current assessment before deciding.')).toBeInTheDocument()
  view.unmount()
  body = readiness({ canDecide: false, authorities: [] })
  render(<DepartureReadinessWorkspace/>)
  expect(await screen.findByText('You hold no current Master appointment for this ship. Decisions are read-only.')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Record' })).toBeNull()
})

it('shows forbidden, an expired assessment as stale and offline as disabled', async () => {
  open(); body = 403
  const first = render(<DepartureReadinessWorkspace/>)
  expect(await screen.findByText('You do not have access to departure readiness for this vessel.')).toBeInTheDocument()
  first.unmount()
  body = readiness({ validUntil: '2020-01-01T00:01:00Z' })
  const second = render(<DepartureReadinessWorkspace/>)
  expect(await screen.findByText('This assessment is stale. Refresh before recording a decision.')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Record' })).toBeDisabled()
  second.unmount()
  body = readiness()
  Object.defineProperty(navigator, 'onLine', { value: false, configurable: true })
  render(<DepartureReadinessWorkspace/>)
  expect(await screen.findByText('You are offline. The assessment may be out of date and sign-off is disabled.')).toBeInTheDocument()
})

it('explains why Release is unavailable: open categories, the emergency and certificate-inventory source gaps and what would satisfy them', async () => {
  open()
  render(<DepartureReadinessWorkspace/>)
  const section = await screen.findByRole('region', { name: 'Why Release is unavailable' })
  expect(section.querySelector('[data-category="emergency"] [data-result="UNKNOWN"]')).not.toBeNull()
  expect(section.querySelector('[data-category="statutory"] [data-result="UNKNOWN"]')).not.toBeNull()
  expect(section.querySelector('[data-category="crew"]')).toBeNull()
  expect(section.querySelector('[data-gap="EMERGENCY_SOURCE_UNAVAILABLE"]')?.textContent).toMatch(/muster and drill records \(SOLAS III\/19\)/)
  expect(section.querySelector('[data-gap="CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED"]')?.textContent).toMatch(/required-certificate baseline/)
  expect(section.textContent).toContain('Hold and Deny can always be recorded with a reason.')
  expect(screen.getByRole('radio', { name: 'Release' })).toBeDisabled()
})

it('explains a forecast window and stays silent only for a current READY assessment', async () => {
  body = readiness({ mode: 'FORECAST', state: 'REVIEW_REQUIRED' })
  open()
  const view = render(<DepartureReadinessWorkspace/>)
  expect(await screen.findByText('This window is a forecast. Release needs a current window that includes today.')).toBeInTheDocument()
  view.unmount()
  const pass = readiness().categories.map(c => ({ ...c, result: 'PASS', reasons: ['FICTIONAL_PASS'] }))
  body = readiness({ state: 'READY', categories: pass, reviewReasons: [], why: [], allowedDecisions: ['RELEASE', 'HOLD', 'DENY'] })
  open()
  render(<DepartureReadinessWorkspace/>)
  expect(await screen.findAllByText('Ready')).not.toHaveLength(0)
  expect(screen.queryByRole('region', { name: 'Why Release is unavailable' })).not.toBeInTheDocument()
})
