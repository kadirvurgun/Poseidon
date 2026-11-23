import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { blockerDisplayState, SafetyEventsWorkspace, safetyRoute } from './SafetyEventsWorkspace'
import { decodeEvent, emptyDraft, missingFields } from './safety-events-api'
import { changeAppLanguage } from '../i18n'

// S9 WP-B DEV-060/061 UI. Synthetic identities and FICTIONAL content only; no fixture is presented as a real incident,
// an approved investigation by a real person or a verified/effective action.
const vessel = '11111111-1111-4111-8111-111111111111', eventId = '22222222-2222-4222-8222-222222222222'
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const can = (patch: Record<string, boolean> = {}) => ({ edit: false, submit: false, withdraw: false, triage: false, readIdentity: false, message: false, investigate: false,
  reviewInvestigation: false, createAction: false, close: false, ...patch })
const event = (patch: Record<string, unknown> = {}) => ({ id: eventId, vesselId: vessel, eventNo: 'SE-20261120-22222222', revision: 1, version: 7, state: 'DRAFT', reportingMode: 'NAMED',
  fields: { ...emptyDraft(), category: 'NEAR_MISS' }, hash: 'A'.repeat(64), missing: ['narrative'], reporter: { mode: 'NAMED', actorId, isCurrentActor: true }, severity: null, severityClaim: null,
  investigationRequired: null, severityPolicy: 'UNCONFIGURED', origin: 'ONLINE', createdAt: '2026-11-20T08:00:00+00:00', savedAt: '2026-11-20T08:00:00+00:00', submittedAt: null,
  closedBy: null, closedAt: null, closureReason: null, triages: [], investigation: null, actions: [], effectiveness: 'NO_ACTIONS', closureBlockers: [], messages: [],
  can: can({ edit: true, withdraw: true }), followUpSecret: null, provenance: 'UserDeclaredUnverified', sourceVersion: 'synthetic', ...patch })
const page = (patch: Record<string, unknown> = {}) => ({ items: [{ id: eventId, eventNo: 'SE-20261120-22222222', state: 'SUBMITTED', reportingMode: 'CONFIDENTIAL', category: 'NEAR_MISS',
  summary: 'FICTIONAL line parted', occurrencePrecision: 'EXACT', occurredAt: '2026-11-20T06:00:00+00:00', occurrenceDate: null, severity: 'HIGH', mine: false, origin: 'ONLINE',
  savedAt: '2026-11-20T08:00:00+00:00' }], offset: 0, limit: 25, hasMore: false,
  blockers: { vesselId: vessel, state: 'BLOCKERS_PRESENT', items: [{ eventId, eventNo: 'SE-20261120-22222222', category: 'NEAR_MISS', severity: 'HIGH', state: 'TRIAGED', reason: 'HIGH_SEVERITY_OPEN', blocking: true }],
    coverage: ['SeverityPolicyUnconfigured'], evaluatedAt: '2026-11-20T08:00:00+00:00' },
  capabilities: { vesselId: vessel, canRead: true, canReport: true, canTriage: false, canInvestigate: false, canReview: false, canWriteActions: false, canVerifyActions: false,
    canReviewEffectiveness: false, canReadIdentity: false, anonymousChannel: 'UNAVAILABLE_GATEWAY_UNCONFIGURED', offline: { available: false, state: 'UNAVAILABLE_POLICY' } }, ...patch })

const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let detailBody: unknown = event(), commandResponse: () => Response = () => json(event())
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; detailBody = event(); commandResponse = () => json(event())
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST' || init?.method === 'PUT') { commands.push({ url, init }); return commandResponse() }
    if (url.includes(`safety-events/${eventId}?`)) return json(detailBody)
    if (url.includes('safety-events?')) return json(page())
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('parses the closed safety-event route and refuses unknown or malformed parameters', () => {
  expect(safetyRoute(`/app/safety-events?vessel=${vessel}&event=${eventId}`)).toMatchObject({ vessel, event: eventId, creating: false, invalid: false })
  expect(safetyRoute(`/app/safety-events?vessel=${vessel}&new=1`).creating).toBe(true)
  expect(safetyRoute(`/app/safety-events?vessel=${vessel}&event=${eventId}&new=1`).invalid).toBe(true)
  expect(safetyRoute('/app/safety-events?vessel=not-a-guid').invalid).toBe(true)
  expect(safetyRoute(`/app/safety-events?vessel=${vessel}&token=x`).invalid).toBe(true)
})

it('mirrors the submission requirements without converting UNKNOWN to No', () => {
  expect(missingFields(emptyDraft())).toEqual(['category', 'occurrence_precision', 'operation_context', 'summary', 'narrative', 'immediate_action_taken'])
  expect(missingFields({ ...emptyDraft(), occurrencePrecision: 'UNKNOWN', immediateActionTaken: 'UNKNOWN' })).toContain('occurrence_unknown_reason')
  expect(missingFields({ ...emptyDraft(), immediateActionTaken: 'UNKNOWN' })).not.toContain('immediate_action_text')
  // A general read that names a confidential reporter is a contract failure.
  expect(() => decodeEvent(event({ reportingMode: 'CONFIDENTIAL' }), vessel)).toThrow()
})

it('lists events with the blocker summary and never shows a confidential reporter', async () => {
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}`)
  render(<SafetyEventsWorkspace/>)
  const blockers = await screen.findByRole('region', { name: 'Open safety events' })
  expect(within(blockers).getByText('High-severity events open')).toBeTruthy()
  expect(within(blockers).getByText(/High severity, open/)).toBeTruthy()
  expect(screen.getByText('FICTIONAL line parted')).toBeTruthy()
  expect(screen.getAllByText('Confidential').length).toBeGreaterThan(0)
  expect(screen.getByTestId('offline-unavailable')).toBeTruthy()
})

// N6 honesty: with SeverityPolicyUnconfigured the server's "no open high-severity event" is not an all-clear.
const emptyBlockers = (coverage: string[]) => ({ vesselId: vessel, state: 'NO_OPEN_HIGH_SEVERITY_EVENT', items: [], coverage, evaluatedAt: '2026-11-20T08:00:00+00:00' })
const withBlockers = (blockers: unknown) => {
  const base = fetcher.getMockImplementation()!
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => url.includes('safety-events?') && !init?.method ? json(page({ items: [], blockers })) : base(url, init))
}

it('never shows an all-clear while the severity policy is unconfigured', async () => {
  expect(blockerDisplayState(emptyBlockers(['SeverityPolicyUnconfigured']) as never)).toEqual({ state: 'POLICY_UNCONFIGURED', unconfigured: true })
  expect(blockerDisplayState(emptyBlockers([]) as never)).toEqual({ state: 'NO_OPEN_HIGH_SEVERITY_EVENT', unconfigured: false })
  withBlockers(emptyBlockers(['SeverityPolicyUnconfigured']))
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}`)
  render(<SafetyEventsWorkspace/>)
  const blockers = await screen.findByRole('region', { name: 'Open safety events' })
  expect(within(blockers).queryByText('No open high-severity event')).toBeNull()
  const badge = within(blockers).getByText('Severity policy not configured – review required')
  expect(badge.getAttribute('data-state')).toBe('POLICY_UNCONFIGURED')
  expect(badge.getAttribute('data-tone')).toBe('warning')
  expect(within(blockers).getByTestId('severity-policy-unconfigured').textContent).toMatch(/cannot be ruled out/)
})

it('shows the Turkish honest unconfigured-policy message', async () => {
  await changeAppLanguage('tr', false)
  withBlockers(emptyBlockers(['SeverityPolicyUnconfigured']))
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}`)
  render(<SafetyEventsWorkspace/>)
  expect(await screen.findByText('Şiddet politikası yapılandırılmadı – inceleme gerekli')).toBeTruthy()
  expect(screen.queryByText('Açık yüksek şiddetli olay yok')).toBeNull()
})

it('keeps the server state and note when blockers are present under an unconfigured policy, and the plain state once configured', async () => {
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}`)
  const { unmount } = render(<SafetyEventsWorkspace/>)
  const blockers = await screen.findByRole('region', { name: 'Open safety events' })
  expect(within(blockers).getByText('High-severity events open')).toBeTruthy()
  expect(within(blockers).getByTestId('severity-policy-unconfigured')).toBeTruthy()
  unmount()
  withBlockers(emptyBlockers([]))
  render(<SafetyEventsWorkspace/>)
  const configured = await screen.findByRole('region', { name: 'Open safety events' })
  expect(within(configured).getByText('No open high-severity event')).toBeTruthy()
  expect(within(configured).queryByTestId('severity-policy-unconfigured')).toBeNull()
})

it('captures a confidential draft with an explicit follow-up preference and keeps the receipt id in the URL', async () => {
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}&new=1`)
  commandResponse = () => json(event({ reportingMode: 'CONFIDENTIAL', reporter: { mode: 'CONFIDENTIAL', actorId: null, isCurrentActor: true } }))
  detailBody = event({ reportingMode: 'CONFIDENTIAL', reporter: { mode: 'CONFIDENTIAL', actorId: null, isCurrentActor: true } })
  render(<SafetyEventsWorkspace/>)
  const user = userEvent.setup()
  await user.click(await screen.findByLabelText('Confidential'))
  expect(screen.getByText(/restricted vault/)).toBeTruthy()
  expect(screen.getByText(/Anonymous reporting needs the public intake channel/)).toBeTruthy()
  await user.selectOptions(screen.getByLabelText('Event type'), 'NEAR_MISS')
  await user.click(screen.getByRole('button', { name: 'Save draft' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  const body = JSON.parse(String(commands[0].init.body))
  expect(body).toMatchObject({ reportingMode: 'CONFIDENTIAL', contactPreference: 'IN_APP_FOLLOW_UP', fields: { category: 'NEAR_MISS', narrative: null } })
  expect(new Headers(commands[0].init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
  await waitFor(() => expect(window.location.search).toContain(`event=${eventId}`))
  expect(await screen.findByText('You (kept confidential)')).toBeTruthy()
})

it('shows the one-time follow-up code after a confidential submission', async () => {
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}&event=${eventId}`)
  const ready = event({ reportingMode: 'CONFIDENTIAL', reporter: { mode: 'CONFIDENTIAL', actorId: null, isCurrentActor: true }, missing: [], can: can({ edit: true, submit: true }) })
  detailBody = ready
  commandResponse = () => json({ ...ready, state: 'SUBMITTED', can: can(), followUpSecret: 'AAAAAAAAAAAAAAAAAAAAAA.' + 'B'.repeat(43) })
  render(<SafetyEventsWorkspace/>)
  await userEvent.setup().click(await screen.findByRole('button', { name: 'Submit report' }))
  expect((await screen.findByTestId('follow-up-secret')).textContent).toContain('AAAAAAAAAAAAAAAAAAAAAA.')
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ expectedVersion: 7, contentHash: 'A'.repeat(64) })
})

it('lists the server missing items on 422 and retries an uncertain command with the same key and body', async () => {
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}&event=${eventId}`)
  detailBody = event({ missing: [], can: can({ edit: true, submit: true }) })
  commandResponse = () => json({ code: 'safety_event_incomplete', missing: ['narrative', 'operation_context'], correlationId: 'c' }, 422)
  render(<SafetyEventsWorkspace/>)
  const user = userEvent.setup()
  await user.click(await screen.findByRole('button', { name: 'Submit report' }))
  const list = await screen.findByRole('list', { name: 'Missing or blocking items' })
  expect(within(list).getByText('What happened')).toBeTruthy(); expect(within(list).getByText('Operation')).toBeTruthy()
  let calls = 0
  commandResponse = () => { calls++; if (calls === 1) throw new TypeError('network'); return json(event({ state: 'SUBMITTED', can: can() })) }
  await user.click(screen.getByRole('button', { name: 'Submit report' }))
  await user.click(await screen.findByRole('button', { name: 'Retry the same command' }))
  await waitFor(() => expect(commands).toHaveLength(3))
  expect(new Headers(commands[2].init.headers).get('Idempotency-Key')).toBe(new Headers(commands[1].init.headers).get('Idempotency-Key'))
  expect(commands[2].init.body).toBe(commands[1].init.body)
})

it('shows closure and effectiveness as separate records with closure blockers', async () => {
  history.replaceState(null, '', `/app/safety-events?vessel=${vessel}&event=${eventId}`)
  detailBody = event({ state: 'ACTION_TRACKING', can: can(), severity: 'HIGH', investigationRequired: 'YES', effectiveness: 'PENDING',
    closureBlockers: ['EFFECTIVENESS_NOT_REVIEWED:33333333-3333-4333-8333-333333333333'],
    actions: [{ id: '33333333-3333-4333-8333-333333333333', kind: 'CORRECTIVE', title: 'FICTIONAL inspect lines', description: 'FICTIONAL', ownerId: actorId, dueDate: '2026-12-01',
      overdue: false, rootCauseIds: [], barrierIds: [], followUpOf: null, revision: 3, version: 2, state: 'VERIFIED', effectiveness: 'PENDING', steps: [], allowedSteps: [] }] })
  render(<SafetyEventsWorkspace/>)
  expect(await screen.findByText('Effectiveness not reviewed')).toBeTruthy()
  expect(screen.getByText('Closure and effectiveness are separate records.')).toBeTruthy()
  expect(screen.getAllByText('Pending').length).toBeGreaterThan(0)
  expect(screen.queryByRole('button', { name: 'Close event' })).toBeNull()
})
