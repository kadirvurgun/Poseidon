import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PilotProgramWorkspace, readProgramRoute } from './PilotProgramWorkspace'
import { decodeProgramView } from './pilot-program-api'
import { changeAppLanguage } from '../i18n'

// Pilot program kit UI (DEV-076/077/078/079/095/096/121). Synthetic identities and FICTIONAL codes only; no accepted
// week, no expand decision and no measured value is fabricated as a pass.
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666'
const programId = '11111111-1111-4111-8111-111111111111', ship = '22222222-2222-4222-8222-222222222222', tug = '33333333-3333-4333-8333-333333333333'
const H = 'C'.repeat(64), W = 'D'.repeat(64)
const at = '2026-12-02T08:00:00Z'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const item = (code: string, week: string, area = 'SHIP', types = ['PILOT_FEEDBACK']) =>
  ({ code, week, requirement: 'DEV-076', area, recordTypes: types, requiresAuditTrace: false, requiresCompletion: false, criterion: 'Daily feedback captured', locator: '18.01/sheet:Backlog/row:77' })
const weekView = (code: string, n: number, patch: Record<string, unknown> = {}, outcome: Record<string, unknown> = {}) => ({
  definition: { week: code, number: n, requirements: ['DEV-076'], locator: '18.01/sheet:Backlog/row:77', items: [item(code + '_DAILY_FEEDBACK', code)] },
  startDate: '2026-12-01', endDateExclusive: '2026-12-08',
  outcome: { week: code, phase: n === 1 ? 'IN_PROGRESS' : 'NOT_STARTED', recommendation: 'NOT_READY', items: [{ code: code + '_DAILY_FEEDBACK', state: 'MISSING', validLinks: 0, reasons: [] }],
    openP0: 0, overdueP0: 0, lateP0: 0, reasons: ['ITEM_MISSING:' + code + '_DAILY_FEEDBACK'], hash: W, ...outcome },
  links: [], closes: [], closeSequence: 0, feedbackCount: 0, allowedOutcomes: n === 1 ? ['NOT_ACCEPTED'] : [], closeDutyConflicts: [], ...patch })
const metric = (patch: Record<string, unknown> = {}) => ({ metricId: 'TUG_JOB_PREP_TIME', mode: 'TUG', unit: 'SECOND', direction: 'LOWER_BETTER', quality: 'UNKNOWN', baseline: 1800,
  current: null, delta: null, percentChange: null, comparison: 'NOT_COMPARABLE', reasons: ['PILOT_MEASUREMENT_MISSING'], baselineId: '77777777-7777-4777-8777-777777777777',
  currentId: null, telemetry: null, locator: '18.01/sheet:Backlog/row:97', ...patch })
const gate = (g: string) => ({ gate: g, state: 'UNKNOWN', reasons: ['NO_ATTESTED_RUNS'], source: 'UatRecords' })
const view = (patch: Record<string, unknown> = {}) => ({
  program: { id: programId, name: 'FICTIONAL dual-mode pilot', shipVesselId: ship, tugVesselId: tug, startDate: '2026-12-01', timeZone: 'UTC', catalogVersion: 'pilot-kit.v1',
    createdBy: actorId, createdAt: at, provenance: 'UserDeclaredUnverified' },
  weeks: [weekView('W1', 1), weekView('W2', 2), weekView('W3', 3), weekView('W4', 4)],
  issues: [{ id: '88888888-8888-4888-8888-888888888888', week: 'W1', area: 'SHIP', category: 'CREW', severity: 'P0', title: 'FICTIONAL crew list incomplete', state: 'OPEN',
    reportedBy: actorId, reportedAt: '2026-12-01T08:00:00Z', triagedBy: null, triagedAt: null, triageNote: null, resolvedBy: null, resolvedAt: null, resolution: null,
    resolutionReference: null, timing: 'OVERDUE', reportedLocalDate: '2026-12-01', version: 3 }],
  feedback: [], measurements: [],
  readout: { metrics: [metric()], telemetryState: 'NOT_AUTHORIZED', telemetryBasis: 'DECLARED_UNVERIFIED', telemetryRetention: 'UNKNOWN', telemetryWindowDays: 30,
    uatShip: gate('UAT_SHIP'), uatTug: gate('UAT_TUG'), defects: [{ mode: 'SHIP', state: 'COMPLETE', critical: 1, high: 0, medium: 0, low: 0, unassessed: 2, syntheticExcluded: 0 },
      { mode: 'TUG', state: 'UNKNOWN', critical: 0, high: 0, medium: 0, low: 0, unassessed: 0, syntheticExcluded: 0 }], openP0: 1 },
  review: { recommendation: 'FIX_RECOMMENDED', reasons: ['WEEK_NOT_CLOSED:W1', 'VALUE_UNKNOWN:TUG_JOB_PREP_TIME'], hash: H, authority: 'x' },
  decisions: [], decisionSequence: 0, authorities: [{ id: grant, duty: 'ProductOwner' }], canRecord: true, canTriage: true, canDecide: true,
  allowedDecisions: ['CONTINUE', 'FIX', 'STOP'], feedbackRetention: 'UNCONFIGURED', metricCatalog: [{ id: 'TUG_JOB_PREP_TIME', mode: 'TUG', unit: 'SECOND', direction: 'LOWER_BETTER',
    requirements: ['DEV-096'], telemetryMetricId: null, locator: 'x' }], recordTypes: ['PILOT_FEEDBACK'], assessedAt: at, authority: 'x', sourceLocator: 'synthetic', decisionDutyConflicts: [], ...patch })
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
    if (url.endsWith('pilot-programs')) return json({ programs: [], vessels: [{ id: ship, code: 'DEMO-SHIP', name: 'FICTIONAL ship', kind: 'SHIP' }], canCreate: true, catalogVersion: 'pilot-kit.v1' })
    if (url.includes('pilot-programs/')) return typeof body === 'number' ? json({ code: 'permission_denied' }, body) : json(body)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
  history.replaceState(null, '', `/app/pilot-program/${programId}`)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('rejects impossible combinations: an unknown value with a delta, a satisfied item without a link, an accepted not-ready week and an offered expand', () => {
  expect(() => decodeProgramView(view({ readout: { ...view().readout, metrics: [metric({ delta: 0 })] } }), programId)).toThrow()
  expect(() => decodeProgramView(view({ weeks: [weekView('W1', 1, {}, { items: [{ code: 'W1_DAILY_FEEDBACK', state: 'SATISFIED', validLinks: 0, reasons: [] }] }),
    weekView('W2', 2), weekView('W3', 3), weekView('W4', 4)] }), programId)).toThrow()
  expect(() => decodeProgramView(view({ weeks: [weekView('W1', 1, { allowedOutcomes: ['ACCEPTED'] }), weekView('W2', 2), weekView('W3', 3), weekView('W4', 4)] }), programId)).toThrow()
  expect(() => decodeProgramView(view({ allowedDecisions: ['EXPAND', 'FIX'] }), programId)).toThrow()
  expect(() => decodeProgramView(view({ decisions: [{ id: '99999999-9999-4999-8999-999999999999', sequence: 1, previousDecisionId: null, decision: 'EXPAND',
    recommendation: 'FIX_RECOMMENDED', programHash: H, rationale: 'FICTIONAL', duty: 'ProductOwner', actorId, recordedAt: at, validity: 'CURRENT' }] }), programId)).toThrow()
  expect(() => decodeProgramView(view(), '00000000-0000-4000-8000-000000000000')).toThrow()
  expect(decodeProgramView(view(), programId).review.recommendation).toBe('FIX_RECOMMENDED')
  expect(readProgramRoute('http://local/app/pilot-program/not-a-guid').invalid).toBe(true)
  expect(readProgramRoute('http://local/app/pilot-program').invalid).toBe(false)
})

it('shows missing items, unknown values, overdue P0 triage and blocks expand and acceptance without a green state', async () => {
  render(<PilotProgramWorkspace/>)
  expect(await screen.findByText('Fix recommended')).toHaveAttribute('data-recommendation', 'FIX_RECOMMENDED')
  expect(document.querySelector('[data-item="W1_DAILY_FEEDBACK"]')).toHaveAttribute('data-item-state', 'MISSING')
  expect(document.querySelector('[data-metric="TUG_JOB_PREP_TIME"]')).toHaveAttribute('data-quality', 'UNKNOWN')
  expect(within(document.querySelector('[data-metric="TUG_JOB_PREP_TIME"]') as HTMLElement).getAllByText('Unknown').length).toBeGreaterThan(0)
  expect(document.querySelector('[data-issue]')).toHaveAttribute('data-timing', 'OVERDUE')
  expect(screen.getByRole('radio', { name: 'Expand' })).toBeDisabled()
  expect(within(screen.getByRole('form', { name: 'Close week W1' })).getByRole('radio', { name: 'Accepted' })).toBeDisabled()
  expect(screen.getByText('Feedback capture is unavailable until an operator configures the retention policy.')).toBeInTheDocument()
  expect(screen.queryByRole('form', { name: 'Record feedback' })).toBeNull()
  expect(document.querySelector('[data-tone="success"]')).toBeNull()
})

it('closes a week as not accepted and records a FIX decision bound to the exact hashes, sequences and appointment', async () => {
  render(<PilotProgramWorkspace/>)
  const close = await screen.findByRole('form', { name: 'Close week W1' })
  await userEvent.click(within(close).getByRole('radio', { name: 'Not accepted' }))
  await userEvent.type(within(close).getByLabelText('Rationale'), 'FICTIONAL week evidence incomplete')
  await userEvent.click(within(close).getByRole('button', { name: 'Record' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toBe(`/api/v1/pilot-programs/${programId}/week-closes`)
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ week: 'W1', outcome: 'NOT_ACCEPTED', rationale: 'FICTIONAL week evidence incomplete', expectedWeekHash: W,
    expectedSequence: 0, authorityId: grant })
  const decide = await screen.findByRole('form', { name: 'Record program decision' })
  await userEvent.click(within(decide).getByRole('radio', { name: 'Fix' }))
  await userEvent.type(within(decide).getByLabelText('Rationale'), 'FICTIONAL baselines missing')
  await userEvent.click(within(decide).getByRole('button', { name: 'Record' }))
  await waitFor(() => expect(commands).toHaveLength(2))
  expect(JSON.parse(String(commands[1].init.body))).toEqual({ decision: 'FIX', rationale: 'FICTIONAL baselines missing', expectedProgramHash: H, expectedSequence: 0, authorityId: grant })
})

it('links a record only with a GUID and an allowed type, and triages an issue with its version', async () => {
  render(<PilotProgramWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Link a record W1' })
  await userEvent.selectOptions(within(form).getByLabelText('Checklist item'), 'W1_DAILY_FEEDBACK')
  await userEvent.selectOptions(within(form).getByLabelText('Record type'), 'PILOT_FEEDBACK')
  await userEvent.type(within(form).getByLabelText('Record identifier'), 'not-a-guid')
  expect(within(form).getByRole('button', { name: 'Link a record' })).toBeDisabled()
  await userEvent.clear(within(form).getByLabelText('Record identifier'))
  await userEvent.type(within(form).getByLabelText('Record identifier'), '99999999-9999-4999-8999-999999999999')
  await userEvent.click(within(form).getByRole('button', { name: 'Link a record' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ itemCode: 'W1_DAILY_FEEDBACK', recordType: 'PILOT_FEEDBACK', recordId: '99999999-9999-4999-8999-999999999999', note: null })
  const triage = await screen.findByRole('form', { name: 'Triage 88888888-8888-4888-8888-888888888888' })
  await userEvent.type(within(triage).getByLabelText('Triage note'), 'FICTIONAL import gap')
  await userEvent.click(within(triage).getByRole('button', { name: 'Triage' }))
  await waitFor(() => expect(commands).toHaveLength(2))
  expect(commands[1].url).toBe(`/api/v1/pilot-programs/${programId}/issues/88888888-8888-4888-8888-888888888888/triage`)
  expect(JSON.parse(String(commands[1].init.body))).toEqual({ note: 'FICTIONAL import gap', expectedVersion: 3 })
})

it('hides week close and decision for an evidence author and says why; trace and completion requirements are explained', async () => {
  const traced = { ...item('W1_DAILY_FEEDBACK', 'W1'), requiresAuditTrace: true, requiresCompletion: true }
  body = view({ weeks: [weekView('W1', 1, { closeDutyConflicts: ['LINK_AUTHOR', 'FEEDBACK_AUTHOR'],
    definition: { week: 'W1', number: 1, requirements: ['DEV-076'], locator: '18.01/sheet:Backlog/row:77', items: [traced] } }), weekView('W2', 2), weekView('W3', 3), weekView('W4', 4)],
    decisionDutyConflicts: ['MEASUREMENT_AUTHOR'] })
  render(<PilotProgramWorkspace/>)
  expect(await screen.findByText(/You cannot close this week because you authored evidence it rests on: checklist links, feedback/)).toBeInTheDocument()
  expect(screen.getByText(/You cannot record the program decision because you authored evidence it rests on: measurements/)).toBeInTheDocument()
  expect(screen.queryByRole('form', { name: 'Close week W1' })).toBeNull()
  expect(screen.queryByRole('form', { name: 'Record program decision' })).toBeNull()
  expect(screen.getByText(/it does not prove the record or flow is completed/)).toBeInTheDocument()
  expect(screen.getByText(/Completion required/)).toBeInTheDocument()
  expect(() => decodeProgramView(view({ decisionDutyConflicts: ['SELF_APPROVED'] }), programId)).toThrow()
})

it('keeps closes and decisions read-only without an appointment and shows the empty program list with a create form', async () => {
  body = view({ canDecide: false, authorities: [], allowedDecisions: [] })
  const first = render(<PilotProgramWorkspace/>)
  expect(await screen.findByText(/no current Product Owner appointment/)).toBeInTheDocument()
  expect(screen.queryByRole('form', { name: 'Record program decision' })).toBeNull()
  expect(screen.queryByRole('form', { name: 'Close week W1' })).toBeNull()
  first.unmount()
  history.replaceState(null, '', '/app/pilot-program')
  render(<PilotProgramWorkspace/>)
  expect(await screen.findByText('No pilot program recorded.')).toBeInTheDocument()
  expect(screen.getByRole('form', { name: 'Create pilot program' })).toBeInTheDocument()
})

it('shows the forbidden state and the invalid route state', async () => {
  body = 403
  const first = render(<PilotProgramWorkspace/>)
  expect(await screen.findByText('You do not have access to these records.')).toBeInTheDocument()
  first.unmount()
  history.replaceState(null, '', '/app/pilot-program/not-a-guid')
  render(<PilotProgramWorkspace/>)
  expect(await screen.findByText('This address is not a valid page.')).toBeInTheDocument()
})
