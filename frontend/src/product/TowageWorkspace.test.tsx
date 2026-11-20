import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TowageWorkspace, towageRoute } from './TowageWorkspace'
import { HighRiskWorkWorkspace, highRiskRoute } from './HighRiskWorkWorkspace'
import { decodeBriefingView, decodeHighRiskPage, decodePlanView, emptyPlan } from './towage-api'
import { changeAppLanguage } from '../i18n'

// S8 WP-D DEV-087/088/107 UI. Synthetic identities and FICTIONAL data only; no approval, issue or acknowledgement fixture
// is presented as current, and nothing here is a GO or a permit.
const vessel = '11111111-1111-4111-8111-111111111111', job = '22222222-2222-4222-8222-222222222222', planId = '33333333-3333-4333-8333-333333333333'
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666'
const briefingId = '77777777-7777-4777-8777-777777777777'
const H = 'B'.repeat(64)
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const plan = (patch: Record<string, unknown> = {}) => ({ id: planId, vesselId: vessel, jobId: job, jobRevision: 2, revision: 3, version: 9, state: 'IN_REVIEW', content: emptyPlan(),
  hash: H, approvedRevision: null, submittedBy: actorId, provenance: 'UserDeclaredUnverified', savedAt: '2026-11-20T08:00:00Z', ...patch })
const planView = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, jobId: job, jobRevision: 2, requestState: 'CurrentRequestRevision', plan: plan(), blockers: [],
  assignedTugs: [vessel], decisions: [], revisions: [], authorities: [{ id: grant, duty: 'TugMaster' }], canEdit: false, canSubmit: false, canDecide: true, canAmend: false,
  limitUnits: { WIND_SPEED: 'kn' }, positions: [], methods: [], lineSources: [], scenarios: [], approvalAuthority: 'MasterOrTugMasterHumanRecord', sourceVersion: 'synthetic', ...patch })
const briefingView = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, jobId: job, briefing: { id: briefingId, vesselId: vessel, jobId: job, revision: 3, version: 4, state: 'ISSUED',
  content: { weather: 'FICTIONAL', current: 'FICTIONAL', visibility: 'FICTIONAL', hazards: [], preventiveBarriers: [], mitigatingBarriers: [], stopWorkAuthority: 'FICTIONAL',
    stopWorkTriggers: [], emergencyArrangements: 'FICTIONAL', riskAssessmentId: null, participants: [{ actorId, role: 'DECK_CREW', tugId: vessel }] }, hash: H, planRevision: 4, riskRevision: 2,
    provenance: 'UserDeclaredUnverified', savedAt: '2026-11-20T08:00:00Z' }, blockers: [], acknowledgementState: 'PENDING', acknowledgements: [],
  risk: { assessmentId: null, state: 'ATTENTION', reason: 'RiskAcknowledgementPending', revision: 2, approvedRevision: 2, acknowledgementState: 'PENDING' }, planState: 'APPROVED', planRevision: 4,
  readinessChecks: [{ category: 'briefing', result: 'FAIL', reason: 'BriefingAcknowledgementPending', critical: true, sourceId: briefingId, sourceRevision: 3 }], revisions: [], authorities: [],
  canEdit: false, canIssue: false, canAmend: false, canAcknowledge: true, roles: [], readinessEffect: 'IncompleteBriefingIsFailAndBlocksReady', sourceVersion: 'synthetic', ...patch })
const row = (patch: Record<string, unknown> = {}) => ({ workKind: 'TugJob', workId: job, workRef: 'FICTIONAL-JOB', vesselId: vessel, vesselCode: 'SYN-TUG', vesselName: 'Synthetic tug', vesselKind: 'Tug',
  workType: 'TOWAGE', plannedStart: null, responsibleRole: 'TugMaster', riskState: 'READY', permitState: 'UNKNOWN', criticalBarrierState: 'UNKNOWN', crewReadiness: 'READY', equipmentReadiness: 'READY',
  nextAction: { kind: 'COMPLETE_REQUIRED', dimension: 'briefing', reason: 'BriefingAcknowledgementPending' }, ownerDue: { ownerRole: 'TugMaster', dueAt: null, sourceKind: null },
  why: [{ dimension: 'briefing', state: 'BLOCKED', reason: 'BriefingAcknowledgementPending', sourceId: null }, { dimension: 'permit', state: 'UNKNOWN', reason: 'PermitToWorkSourceUnavailable', sourceId: null }],
  criticalBlockers: 1, reviewItems: 1, link: `/app/towage-risk-briefing?vessel=${vessel}&job=${job}`, ...patch })
const page = (patch: Record<string, unknown> = {}) => ({ items: [row()], offset: 0, limit: 20, hasMore: false, coverage: ['PermitToWorkSourceUnavailable'], workTypes: [], vessels: [], fleets: [],
  evaluatedAt: '2026-11-20T08:00:00Z', scoring: 'NoAggregateScore;EveryDimensionShown;CriticalFirst', sourceVersion: 'synthetic', ...patch })

const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let planBody: unknown = planView(), briefingBody: unknown = briefingView(), pageBody: unknown = page(), pageStatus = 200
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; planBody = planView(); briefingBody = briefingView(); pageBody = page(); pageStatus = 200
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST' || init?.method === 'PUT') { commands.push({ url, init }); return json({}, 200) }
    if (url.includes('towage-plans?')) return json(planBody)
    if (url.includes('towage-risk-briefings?')) return json(briefingBody)
    if (url.includes('hse/high-risk-work')) return json(pageStatus === 200 ? pageBody : { code: 'permission_denied' }, pageStatus)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('parses closed towage and high-risk routes and refuses unknown or malformed parameters', () => {
  expect(towageRoute(`/app/towage-plan?vessel=${vessel}&job=${job}`)).toMatchObject({ screen: 'towage-plan', invalid: false })
  expect(towageRoute(`/app/towage-risk-briefing?vessel=${vessel}&extra=1`).invalid).toBe(true)
  expect(towageRoute('/app/towage-plan?vessel=not-a-guid').invalid).toBe(true)
  expect(highRiskRoute('/app/high-risk-work?workType=TOWAGE').invalid).toBe(false)
  expect(highRiskRoute('/app/high-risk-work?workType=PAINTING').invalid).toBe(true)
  expect(highRiskRoute('/app/high-risk-work?score=1').invalid).toBe(true)
})

it('records the Tug Master plan approval as an exact human record bound to the content hash', async () => {
  history.replaceState(null, '', `/app/towage-plan?vessel=${vessel}&job=${job}`)
  const user = userEvent.setup()
  render(<TowageWorkspace/>)
  expect(await screen.findByText('In review')).toBeInTheDocument()
  expect(document.querySelector('[data-state]')!.getAttribute('data-tone')).toBe('warning')
  const approve = screen.getByRole('button', { name: 'Approve' })
  expect(approve).toBeDisabled()
  await user.type(screen.getByLabelText('Reason'), 'FICTIONAL plan review record')
  await user.click(approve)
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toBe(`/api/v1/towage-plans/${planId}/decisions?vessel=${vessel}`)
  expect((commands[0].init.headers as Record<string, string>)['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ expectedVersion: 9, contentHash: H, decision: 'APPROVE', reason: 'FICTIONAL plan review record', authorityId: grant })
})

it('shows the incomplete briefing as a failing readiness input and sends only the participant acknowledgement', async () => {
  history.replaceState(null, '', `/app/towage-risk-briefing?vessel=${vessel}&job=${job}`)
  const user = userEvent.setup()
  render(<TowageWorkspace/>)
  expect(await screen.findByText('BriefingAcknowledgementPending')).toBeInTheDocument()
  expect(document.querySelector('[data-result="FAIL"]')).not.toBeNull()
  expect(document.querySelector('[data-ack]')!.getAttribute('data-ack')).toBe('PENDING')
  await user.click(screen.getByRole('button', { name: 'Acknowledge' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toBe(`/api/v1/towage-risk-briefings/${briefingId}/acknowledgements?vessel=${vessel}`)
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ revision: 3, contentHash: H, choice: 'ACK' })
})

it('lists every dimension separately, never shows permit as known and expands the Why drill-down', async () => {
  history.replaceState(null, '', '/app/high-risk-work')
  const user = userEvent.setup()
  render(<HighRiskWorkWorkspace/>)
  expect(await screen.findByText('FICTIONAL-JOB')).toBeInTheDocument()
  expect(document.querySelector('tr[data-critical="true"]')).not.toBeNull()
  expect(document.querySelector('[data-next="COMPLETE_REQUIRED"]')).not.toBeNull()
  await user.click(screen.getByRole('button', { name: 'Why' }))
  const why = screen.getByRole('table', { name: 'Why FICTIONAL-JOB' })
  expect(within(why).getByText('PermitToWorkSourceUnavailable')).toBeInTheDocument()
  expect(within(why).getByText('BriefingAcknowledgementPending')).toBeInTheDocument()
})

it('renders empty and forbidden states for the fleet view', async () => {
  pageBody = page({ items: [] })
  history.replaceState(null, '', '/app/high-risk-work')
  const { unmount } = render(<HighRiskWorkWorkspace/>)
  expect(await screen.findByText('No high-risk work matches the filter.')).toBeInTheDocument()
  unmount()
  pageStatus = 403
  render(<HighRiskWorkWorkspace/>)
  expect(await screen.findByText('You do not have access to the high-risk work view.')).toBeInTheDocument()
})

it('decoders refuse contradictory towage and fleet projections', () => {
  expect(() => decodePlanView(planView({ plan: plan({ state: 'APPROVED', approvedRevision: 2 }) }), vessel, job)).toThrow()
  expect(decodePlanView(planView(), vessel, job).plan!.state).toBe('IN_REVIEW')
  expect(() => decodeBriefingView(briefingView({ readinessChecks: [{ category: 'briefing', result: 'PASS', reason: 'x', critical: true, sourceId: null, sourceRevision: null }] }), vessel, job)).toThrow()
  expect(() => decodeHighRiskPage(page({ items: [row({ nextAction: { kind: 'NONE', dimension: null, reason: null } })] }))).toThrow()
  expect(() => decodeHighRiskPage(page({ items: [row({ permitState: 'APPROVED' })] }))).toThrow()
  expect(decodeHighRiskPage(page()).items).toHaveLength(1)
})
