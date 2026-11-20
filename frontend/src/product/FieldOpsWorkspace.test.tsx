import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FieldOpsWorkspace, fieldOpsRoute } from './FieldOpsWorkspace'
import { decodeDrydockView, decodeExecutionView, decodeJobView, decodeLogView, emptyDrydock, emptyLog } from './field-ops-api'
import { changeAppLanguage } from '../i18n'

// S9 WP-D DEV-089/090/111 UI. Synthetic identities and FICTIONAL data only; nothing here is a GO, a Master
// review or a verification presented as current, and the mocked API proves UI behaviour only.
const vessel = '11111111-1111-4111-8111-111111111111', job = '22222222-2222-4222-8222-222222222222', exe = '33333333-3333-4333-8333-333333333333'
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666'
const other = '77777777-7777-4777-8777-777777777777', logId = '88888888-8888-4888-8888-888888888888', dockId = '99999999-9999-4999-8999-999999999999'
const H = 'C'.repeat(64), T = '2026-11-20T08:00:00Z'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const execution = (patch: Record<string, unknown> = {}) => ({ id: exe, vesselId: vessel, jobId: job, tugId: vessel, assignmentId: other, assignmentRevision: 1, planId: other, planRevision: 3,
  briefingId: other, briefingRevision: 2, readinessDecisionId: other, state: 'ACTIVE', verificationState: 'PENDING', revision: 3, version: 7, sequence: 3, hash: H, startedAt: T,
  startedBy: actorId, startPosition: 'FICTIONAL berth', endedAt: null, endedBy: null, endPosition: null, verifiedBy: null, verifiedAt: null, provenance: 'UserDeclaredUnverified', ...patch })
const ev = (sequence: number, kind: string, patch: Record<string, unknown> = {}) => ({ sequence, kind, occurredAt: T, recordedAt: T, actorId, duty: null, value: null, unit: null,
  result: null, text: null, reference: null, weather: null, reason: null, position: null, hash: H, ...patch })
const executionView = (patch: Record<string, unknown> = {}) => ({ execution: execution(), jobNumber: 'FICTIONAL-JOB-1', assistedVessel: 'FICTIONAL MV', ledger: { towlineConnectedAt: T,
  towlineReleasedAt: null, towlineConnected: true, maxTowLoad: [{ value: '42.5', unit: 't', samples: 1 }], communicationState: 'UNKNOWN', winchState: 'UNKNOWN', emergencyReleaseTested: 'UNKNOWN',
  activeSeconds: 600 }, timeline: [ev(1, 'STARTED', { duty: 'TugMaster' }), ev(2, 'TOWLINE_CONNECTED'), ev(3, 'LOAD_OBSERVED', { value: '42.5', unit: 't' })],
  authorities: [{ id: grant, duty: 'TugMaster' }], canRecord: true, canTransition: true, canVerify: false, allowedActions: ['SUSPEND', 'ABORT', 'COMPLETE'], completeBlockers: ['towline_still_connected'],
  recordableKinds: ['TOWLINE_CONNECTED', 'TOWLINE_RELEASED', 'LOAD_OBSERVED', 'NOTE'], safetyEvents: 'UNKNOWN:SafetyEventSourceUnavailable', authority: 'TugMasterHumanRecord', ...patch })
const jobView = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, jobId: job, jobNumber: 'FICTIONAL-JOB-1', jobStatus: 'Submitted',
  tugs: [{ tugId: vessel, assignmentId: other, readinessDecisionId: null, decision: null, blockers: ['readiness_go_missing'], authorities: [{ id: grant, duty: 'TugMaster' }], canStart: false,
    executionId: null, executionState: null }], executions: [], ...patch })
const logView = (patch: Record<string, unknown> = {}) => ({ log: { id: logId, vesselId: vessel, logDate: '2026-11-20', watch: 'OTHER', revision: 1, version: 2, state: 'DRAFT',
  content: { ...emptyLog(), timeZone: 'UTC' }, hash: H, submittedBy: null, verifiedRevision: null, capturedAt: T, syncedAt: T, syncState: 'SYNCED', provenance: 'UserDeclaredUnverified', savedAt: T },
  reconciliation: { state: 'REVIEW_REQUIRED', reasons: [{ scope: 'meter', code: 'no_meter_readings', subject: null }], jobsCompleted: 0, operatingHours: '0', openInterval: false, meterHours: null },
  masterReview: 'PENDING', defectsReported: 0, safetyEvents: 'UNKNOWN:SafetyEventSourceUnavailable', intervalFrom: T, intervalTo: T, executions: [], submitBlockers: ['daily_summary_missing'],
  reviews: [], revisions: [], authorities: [], canEdit: true, canSubmit: true, canReview: false, offlineCapture: 'InMemoryUnsavedOnly', ...patch })
const wo = '12121212-1212-4212-8212-121212121212'
const dockView = (patch: Record<string, unknown> = {}) => ({ package: { id: dockId, vesselId: vessel, revision: 2, version: 5, state: 'DRAFT', content: { ...emptyDrydock(),
  items: [{ id: other, category: 'WORK_ORDER', title: 'FICTIONAL hull cleaning', equipmentId: null, workPackage: null, classRequirement: null, statutoryRequirement: null, priority: 'HIGH',
    links: [{ kind: 'WORK_ORDER', targetId: wo, pinnedRevision: 1 }] }] }, hash: H, actualStart: null, actualFinish: null, completedBy: null, verifiedBy: null, verifiedAt: null,
  provenance: 'UserDeclaredUnverified', savedAt: T },
  readiness: { state: 'REVIEW_REQUIRED', items: [{ itemId: other, state: 'REVIEW_REQUIRED', links: [{ kind: 'WORK_ORDER', targetId: wo, pinnedRevision: 1, currentRevision: 2, state: 'STALE',
    reason: 'TargetChangedSincePinned', due: null }] }], unlinked: 0, overdue: 0, stale: 1, unavailable: 0, meaning: 'LinkageCompletenessOnly;NotASurveyOrClassResult' },
  timeline: [{ revision: 1, state: 'DRAFT', reasonCode: 'CREATE', note: null, actorId, savedAt: T }], planBlockers: ['link_stale'], completeBlockers: ['completion_evidence_missing'],
  allowedActions: ['PLAN', 'CANCEL'], canEdit: true, canVerify: false, unreadableLinkKinds: [], external: 'SupplierBudgetExternalReferenceOnly', ...patch })

const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let bodies: Record<string, unknown> = {}, status: Record<string, number> = {}
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; bodies = { job: jobView(), exe: executionView(), log: logView(), dock: dockView() }; status = {}
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST' || init?.method === 'PUT') { commands.push({ url, init }); return json({}, status.command ?? 200) }
    if (url.includes(`towage-executions/${exe}`)) return json(bodies.exe, status.exe ?? 200)
    if (url.includes('towage-executions?')) return json(bodies.job)
    if (url.includes(`tug-daily-logs/${logId}`)) return json(bodies.log)
    if (url.includes('tug-daily-logs?')) return json({ vesselId: vessel, items: [], canWrite: true })
    if (url.includes(`drydock-packages/${dockId}`)) return json(bodies.dock)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('parses closed routes and refuses unknown or malformed parameters', () => {
  expect(fieldOpsRoute(`/app/towage-execution?vessel=${vessel}&job=${job}`)).toMatchObject({ screen: 'towage-execution', invalid: false })
  expect(fieldOpsRoute(`/app/tug-daily-log?vessel=${vessel}&job=${job}`).invalid).toBe(true)
  expect(fieldOpsRoute(`/app/drydock-planning?vessel=not-a-guid`).invalid).toBe(true)
  expect(fieldOpsRoute(`/app/drydock-planning?vessel=${vessel}&id=${dockId}`)).toMatchObject({ screen: 'drydock-planning', id: dockId, invalid: false })
})

it('rejects contracts that would show a start with blockers, a self-verification, a reconciled log with reasons or stale links as current', () => {
  expect(() => decodeJobView(jobView({ tugs: [{ ...jobView().tugs[0], canStart: true }] }), vessel, job)).toThrow()
  expect(() => decodeExecutionView(executionView({ execution: execution({ state: 'COMPLETED', endedAt: T, endedBy: actorId, verificationState: 'VERIFIED', verifiedBy: actorId }) }), vessel)).toThrow()
  expect(() => decodeLogView(logView({ reconciliation: { ...logView().reconciliation, state: 'RECONCILED' } }), vessel)).toThrow()
  expect(() => decodeLogView(logView({ safetyEvents: '0' }), vessel)).toThrow()
  expect(() => decodeDrydockView(dockView({ readiness: { ...dockView().readiness, state: 'LINKS_CURRENT' } }), vessel)).toThrow()
  expect(decodeExecutionView(executionView(), vessel).execution.state).toBe('ACTIVE')
})

it('shows the start gate blockers and never offers a start without a current GO', async () => {
  history.replaceState(null, '', `/app/towage-execution?vessel=${vessel}&job=${job}`)
  render(<FieldOpsWorkspace/>)
  expect(await screen.findByText('readiness_go_missing')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Start operation' })).not.toBeInTheDocument()
})

it('records a Tug Master abort with appointment and reason on the exact execution version', async () => {
  history.replaceState(null, '', `/app/towage-execution?vessel=${vessel}&id=${exe}`)
  const user = userEvent.setup()
  render(<FieldOpsWorkspace/>)
  expect(await screen.findByText('42.5 t (1)')).toBeInTheDocument()
  expect(screen.getByText('towline_still_connected')).toBeInTheDocument()
  const form = screen.getByRole('form', { name: 'Suspend' })
  await user.selectOptions(within(form).getByLabelText('Action'), 'ABORT')
  await user.type(within(form).getByLabelText('Reason'), 'FICTIONAL girting risk, stop immediately')
  await user.click(within(form).getByRole('button', { name: 'Abort' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toContain(`towage-executions/${exe}/transitions?vessel=${vessel}`)
  expect(JSON.parse(String(commands[0].init.body))).toMatchObject({ expectedVersion: 7, action: 'ABORT', authorityId: grant })
  expect(new Headers(commands[0].init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
})

it('keeps a daily log save in memory while offline and replays it with the same key when the connection returns', async () => {
  history.replaceState(null, '', `/app/tug-daily-log?vessel=${vessel}&id=${logId}`)
  const user = userEvent.setup()
  render(<FieldOpsWorkspace/>)
  expect(await screen.findByText('no_meter_readings')).toBeInTheDocument()
  Object.defineProperty(navigator, 'onLine', { value: false, configurable: true })
  window.dispatchEvent(new Event('offline'))
  await user.click(await screen.findByRole('button', { name: 'Keep for sending when online' }))
  expect(await screen.findByText('Waiting to sync')).toBeInTheDocument()
  expect(commands).toHaveLength(0)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  window.dispatchEvent(new Event('online'))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].init.method).toBe('PUT')
  expect(await screen.findByText('Synced')).toBeInTheDocument()
})

it('marks a changed linked work order as stale and offers a re-pin instead of showing current linkage', async () => {
  history.replaceState(null, '', `/app/drydock-planning?vessel=${vessel}&id=${dockId}`)
  render(<FieldOpsWorkspace/>)
  expect(await screen.findByText('A linked record changed after it was pinned. Review it and re-pin before planning.')).toBeInTheDocument()
  expect(screen.getByText('link_stale')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Re-pin to r2' })).toBeInTheDocument()
  expect(screen.queryByText('Links current')).not.toBeInTheDocument()
})

it('shows a forbidden state when the server denies the record', async () => {
  status.exe = 403
  history.replaceState(null, '', `/app/towage-execution?vessel=${vessel}&id=${exe}`)
  render(<FieldOpsWorkspace/>)
  expect(await screen.findByText('You do not have access to this record.')).toBeInTheDocument()
})
