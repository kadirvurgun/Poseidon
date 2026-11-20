import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PilotGateWorkspace } from './PilotGateWorkspace'
import { decodePilotGate, pilotGates } from './pilot-gate-api'
import { changeAppLanguage } from '../i18n'

// S11 WP-D DEV-075 UI. Synthetic identities and FICTIONAL codes only; no GO fixture.
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666'
const H = 'B'.repeat(64)
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const gates = (state: string = 'PASS') => pilotGates.map(g => ({ gate: g, state: g === 'SECURITY' ? state : 'PASS', reasons: g === 'SECURITY' && state !== 'PASS' ? ['SECURITY_GATE_SOURCE_NOT_INTEGRATED:DEV-068'] : [], source: 'FictionalSource' }))
const view = (patch: Record<string, unknown> = {}, reviewPatch: Record<string, unknown> = {}) => ({
  review: { recommendation: 'NO_GO_RECOMMENDED', gates: gates('DEFERRED'), openP0: 0, noGoReasons: ['SECURITY:DEFERRED'], hash: H, authority: 'ProductOwnerHumanDecisionRequired', ...reviewPatch },
  support: [{ item: 'SUPPORT_CONTACTS', latest: null }, { item: 'ONCALL_ROTA', latest: { id: '77777777-7777-4777-8777-777777777777', item: 'ONCALL_ROTA', sequence: 1, previousCheckId: null,
    state: 'READY', evidenceReference: 'FICTIONAL-ROTA', note: 'FICTIONAL rota', actorId, recordedAt: '2026-11-24T08:00:00Z' } }],
  decisions: [], sequence: 0, authorities: [{ id: grant, duty: 'ProductOwner' }], canDecide: true, canRecordSupport: true, allowedDecisions: ['NO_GO', 'CONDITIONAL_GO'],
  assessedAt: '2026-11-24T08:00:00Z', sourceLocator: 'synthetic', authority: 'x', ...patch })
const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let body: unknown = view()
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; body = view()
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST') { commands.push({ url, init }); return json({}) }
    if (url.includes('pilot-gate')) return typeof body === 'number' ? json({ code: 'permission_denied' }, body) : json(body)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
  history.replaceState(null, '', '/app/pilot-gate')
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('rejects a GO-eligible review with a non-PASS gate or unknown P0, an offered GO outside eligibility and a GO decision on a NO-GO review', () => {
  expect(() => decodePilotGate(view({}, { recommendation: 'GO_ELIGIBLE_PENDING_HUMAN_DECISION' }))).toThrow()
  expect(() => decodePilotGate(view({}, { recommendation: 'GO_ELIGIBLE_PENDING_HUMAN_DECISION', gates: gates(), openP0: null }))).toThrow()
  expect(() => decodePilotGate(view({ allowedDecisions: ['GO', 'NO_GO'] }))).toThrow()
  expect(() => decodePilotGate(view({ decisions: [{ id: '88888888-8888-4888-8888-888888888888', sequence: 1, previousDecisionId: null, decision: 'GO', recommendation: 'NO_GO_RECOMMENDED',
    reviewHash: H, conditions: [], rationale: 'FICTIONAL', duty: 'ProductOwner', actorId, recordedAt: '2026-11-24T08:00:00Z', validity: 'CURRENT' }] }))).toThrow()
  expect(() => decodePilotGate(view({}, { gates: gates().slice(1) }))).toThrow()
  expect(decodePilotGate(view()).review.recommendation).toBe('NO_GO_RECOMMENDED')
})

it('shows NO-GO recommended, deferred gates and disables GO; the support item without a mark stays unmarked', async () => {
  render(<PilotGateWorkspace/>)
  expect(await screen.findByText('NO-GO recommended')).toHaveAttribute('data-recommendation', 'NO_GO_RECOMMENDED')
  expect(screen.getByText('Deferred')).toHaveAttribute('data-gate-state', 'DEFERRED')
  expect(screen.getByText('SECURITY_GATE_SOURCE_NOT_INTEGRATED:DEV-068')).toBeInTheDocument()
  expect(screen.getByRole('radio', { name: 'GO' })).toBeDisabled()
  expect(screen.getByText('GO is possible only when every gate is PASS and open P0 is zero.')).toBeInTheDocument()
  expect(document.querySelector('[data-support="SUPPORT_CONTACTS"]')).toHaveAttribute('data-support-state', 'UNMARKED')
  expect(document.querySelector('[data-tone="success"]')).toBeNull()
})

it('records a NO-GO bound to the exact review hash and sequence with the appointment', async () => {
  render(<PilotGateWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Record go/no-go decision' })
  await userEvent.click(within(form).getByRole('radio', { name: 'NO-GO' }))
  await userEvent.type(within(form).getByLabelText('Rationale'), 'FICTIONAL owners deferred, UAT incomplete')
  await userEvent.click(within(form).getByRole('button', { name: 'Record' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toBe('/api/v1/pilot-gate/decisions')
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ decision: 'NO_GO', rationale: 'FICTIONAL owners deferred, UAT incomplete', conditions: [],
    expectedReviewHash: H, expectedSequence: 0, authorityId: grant })
})

it('requires conditions for a conditional GO and keeps decisions read-only without an appointment', async () => {
  const first = render(<PilotGateWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Record go/no-go decision' })
  await userEvent.click(within(form).getByRole('radio', { name: 'Conditional GO' }))
  await userEvent.type(within(form).getByLabelText('Rationale'), 'FICTIONAL deferred DR evidence')
  expect(within(form).getByRole('button', { name: 'Record' })).toBeDisabled()
  await userEvent.type(within(form).getByLabelText('Conditions (one per line)'), 'FICTIONAL DR rehearsal before cutover')
  expect(within(form).getByRole('button', { name: 'Record' })).toBeEnabled()
  first.unmount()
  body = view({ canDecide: false, authorities: [], canRecordSupport: false })
  render(<PilotGateWorkspace/>)
  expect(await screen.findByText(/no current Product Owner appointment/)).toBeInTheDocument()
  expect(screen.queryByRole('form', { name: 'Record go/no-go decision' })).toBeNull()
})

it('shows the forbidden state', async () => {
  body = 403
  render(<PilotGateWorkspace/>)
  expect(await screen.findByText('You do not have access to these records.')).toBeInTheDocument()
})
