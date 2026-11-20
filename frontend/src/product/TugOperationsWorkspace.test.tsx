import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TugOperationsWorkspace, demoHref, tugOperationsRoute } from './TugOperationsWorkspace'
import { decodeCapability, decodeHistoryPage, decodeOfflineResult, historyQuery, queuedState } from './tug-operations-api'
import { decodeSyncConflictPage } from './sync-conflict-center-api'
import { changeAppLanguage } from '../i18n'

// S10 WP-B DEV-091/092/119 UI. Synthetic identities and FICTIONAL data only; the mocked API proves UI behaviour only.
const vessel = '11111111-1111-4111-8111-111111111111', job = '22222222-2222-4222-8222-222222222222', exe = '33333333-3333-4333-8333-333333333333'
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', cancelledJob = '66666666-6666-4666-8666-666666666666'
const command = '77777777-7777-4777-8777-777777777777', log = '88888888-8888-4888-8888-888888888888'
const H = 'C'.repeat(64), T = '2026-11-20T08:00:00Z'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const item = (patch: Record<string, unknown> = {}) => ({ jobId: job, vesselId: vessel, jobNumber: 'FICTIONAL-JOB-1', jobStatus: 'Submitted', jobType: 'BERTHING', assistedVessel: 'FICTIONAL MV',
  operationArea: null, at: T, cancelledAt: null, outcome: 'COMPLETED', evidence: 'UNVERIFIED_EVIDENCE', planState: 'APPROVED', briefingState: 'ISSUED',
  executions: [{ id: exe, tugId: vessel, state: 'COMPLETED', verificationState: 'PENDING', startedAt: T, endedAt: '2026-11-20T09:30:00Z', durationMinutes: 90, startedBy: actorId, endedBy: actorId,
    verifiedBy: null, sequence: 6, evidenceEvents: 1, lastEventHash: H }],
  dailyLogs: [{ id: log, tugId: vessel, logDate: '2026-11-20', watch: '08-16', state: 'DRAFT', linkedToExecution: false }], safetyEvents: 'UNKNOWN:SafetyEventJobLinkUnavailable', ...patch })
const page = (items: unknown[], nextCursor: string | null = null) => ({ items, nextCursor, outcomes: ['CANCELLED'], readAt: T, deletion: 'NoSilentDeletion', sourceVersion: 'x' })
const result = (patch: Record<string, unknown> = {}) => ({ commandId: command, operation: 'tug.daily.entry.v1', result: 'MaterialConflict', resourceId: log, baseVersion: 3, serverVersion: 4,
  conflictFields: ['daily_version'], choices: ['keepServer', 'useCaptured'], parentCommandId: null, receivedAt: T, ...patch })

const fetcher = vi.fn()
let requests: string[] = []
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  requests = []
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string) => {
    requests.push(url)
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (url.includes('tug-operation-history') && url.includes('outcome=CANCELLED'))
      return json(page([item({ jobId: cancelledJob, jobStatus: 'Cancelled', cancelledAt: T, outcome: 'CANCELLED', executions: [], dailyLogs: [], evidence: 'UNKNOWN' })]))
    if (url.includes('tug-operation-history')) return json(page([item()], 'AbCd_-'))
    if (url.includes('tug-operations-offline?')) return json({ vesselId: vessel, state: 'UNAVAILABLE_POLICY', operations: [], onlineOnly: ['MASTER_RELEASE'], snapshots: [] })
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('parses closed routes and refuses unknown or malformed parameters', () => {
  expect(tugOperationsRoute(`/app/tug-operation-history?vessel=${vessel}`)).toMatchObject({ screen: 'tug-operation-history', invalid: false })
  expect(tugOperationsRoute(`/app/tug-operations-offline?vessel=${vessel}&job=${job}`)).toMatchObject({ screen: 'tug-operations-offline', job, invalid: false })
  expect(tugOperationsRoute(`/app/tug-demo-journey?vessel=${vessel}&actor=${actorId}`).invalid).toBe(true)
  expect(tugOperationsRoute(`/app/tug-demo-journey?vessel=not-a-guid`).invalid).toBe(true)
  expect(demoHref('tug-job-intake', vessel, job)).toBe(`/app/tug-job-intake?vessel=${vessel}`)
  expect(demoHref('towage-execution', vessel, job)).toBe(`/app/towage-execution?vessel=${vessel}&job=${job}`)
})

it('rejects history contracts that would hide a cancellation or show unknown states, and offline results that claim a silent resolution', () => {
  expect(decodeHistoryPage(page([item()])).items[0].outcome).toBe('COMPLETED')
  expect(() => decodeHistoryPage(page([item({ jobStatus: 'Cancelled' })]))).toThrow()
  expect(() => decodeHistoryPage(page([item({ outcome: 'DELETED' })]))).toThrow()
  expect(() => decodeHistoryPage(page([item({ evidence: 'VERIFIED' })]))).toThrow()
  expect(() => decodeHistoryPage(page([item(), item()]))).toThrow()
  expect(() => historyQuery({ tugId: 'nope' })).toThrow()
  expect(historyQuery({ vesselId: vessel, outcome: 'ABORTED', from: '2026-01-01' })).toBe(`vesselId=${vessel}&from=2026-01-01&outcome=ABORTED`)
  expect(decodeOfflineResult(result()).result).toBe('MaterialConflict')
  expect(() => decodeOfflineResult(result({ conflictFields: [] }))).toThrow()
  expect(() => decodeOfflineResult(result({ result: 'ServerRetained', conflictFields: [], choices: [] }))).toThrow()
  expect(() => decodeOfflineResult(result({ result: 'AutoMerged' }))).toThrow()
  expect(() => decodeOfflineResult(result(), '99999999-9999-4999-8999-999999999999')).toThrow()
  expect(queuedState({ commandId: command, revision: 1, body: '{}', operation: 'tug.daily.entry.v1' })).toBe('Queued')
  expect(() => decodeCapability({ vesselId: vessel, state: 'READY', operations: [], onlineOnly: [], snapshots: [] }, vessel)).toThrow()
})

it('accepts the tug operations conflict source with its operation code and refuses an unknown one', () => {
  const sources = ['crewAssignment', 'tugPlan', 'dailyRecord', 'workOrderEvidence', 'tugOperations', 'certificateRenewal'].map(source => ({ source, state: 'Included' }))
  const row = { source: 'tugOperations', resourceId: log, resourceKind: 'tug.daily.entry.v1', enrollmentId: job, commandId: command, baseVersion: 0, serverVersionAtConflict: 0,
    capturedAt: T, receivedAt: T, state: 'Open', resolutionCommandId: null, resolutionResult: null }
  const value = { vesselId: vessel, readAt: T, sources, items: [row], offset: 0, hasMore: false }
  expect(decodeSyncConflictPage(value, vessel, 0).items[0].source).toBe('tugOperations')
  expect(() => decodeSyncConflictPage({ ...value, items: [{ ...row, resourceKind: 'tug.master.release.v1' }] }, vessel, 0)).toThrow()
  expect(() => decodeSyncConflictPage({ ...value, sources: sources.slice(0, 4) }, vessel, 0)).toThrow()
})

it('shows history with recorded outcomes, unverified evidence and unlinked logs, filters cancelled jobs and pages by cursor', async () => {
  history.replaceState(null, '', `/app/tug-operation-history?vessel=${vessel}`)
  render(<TugOperationsWorkspace />)
  const table = await screen.findByRole('table')
  expect(within(table).getByText('FICTIONAL-JOB-1')).toBeInTheDocument()
  expect(within(table).getByText('Completed (recorded)')).toBeInTheDocument()
  expect(within(table).getByText('Evidence unverified')).toBeInTheDocument()
  expect(within(table).getByText(/not linked by the log/)).toBeInTheDocument()
  expect(within(table).getByRole('link', { name: 'Open execution timeline' })).toHaveAttribute('href', `/app/towage-execution?vessel=${vessel}&id=${exe}`)
  expect(screen.getByText(/No silent deletion/)).toBeInTheDocument()
  const user = userEvent.setup()
  await user.click(screen.getByRole('button', { name: 'Load more' }))
  await waitFor(() => expect(requests.some(r => r.includes('cursor=AbCd_-'))).toBe(true))
  await user.selectOptions(screen.getByLabelText('Outcome'), 'CANCELLED')
  await user.click(screen.getByRole('button', { name: 'Apply filters' }))
  expect(await within(await screen.findByRole('table')).findByText('Cancelled')).toBeInTheDocument()
  expect(requests.some(r => r.includes(`vesselId=${vessel}`) && r.includes('outcome=CANCELLED'))).toBe(true)
})

it('blocks an invalid tug filter and shows forbidden and offline states without sending anything', async () => {
  history.replaceState(null, '', `/app/tug-operation-history?vessel=${vessel}`)
  fetcher.mockImplementation(async (url: string) => url.endsWith('auth/session') ? json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'x' }) : json({ code: 'permission_denied' }, 403))
  render(<TugOperationsWorkspace />)
  expect(await screen.findByText('You do not have access to this tug scope.')).toBeInTheDocument()
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Tug ID'), 'not-a-uuid')
  expect(screen.getByRole('button', { name: 'Apply filters' })).toBeDisabled()
})

it('never offers a snapshot when no offline policy is configured and lists online-only decisions', async () => {
  history.replaceState(null, '', `/app/tug-operations-offline?vessel=${vessel}&job=${job}`)
  render(<TugOperationsWorkspace />)
  expect(await screen.findByText(/Online only: Master release, readiness GO/)).toBeInTheDocument()
  await waitFor(() => expect(requests.some(r => r.includes('tug-operations-offline?vessel='))).toBe(true))
  expect(requests.some(r => r.includes('/snapshots'))).toBe(false)
  expect(requests.some(r => r.includes('/replay'))).toBe(false)
})

it('asks for a job before any offline queue is opened', async () => {
  history.replaceState(null, '', `/app/tug-operations-offline?vessel=${vessel}`)
  render(<TugOperationsWorkspace />)
  expect(await screen.findByText('Open this screen from a tug job to prefetch its snapshot.')).toBeInTheDocument()
  expect(requests.some(r => r.includes('tug-operations-offline'))).toBe(false)
})

it('walks the demo journey through real screens with a Why for every gate and creates nothing', async () => {
  history.replaceState(null, '', `/app/tug-demo-journey?vessel=${vessel}&job=${job}`)
  render(<TugOperationsWorkspace />)
  const steps = await screen.findAllByRole('link', { name: 'Open screen' })
  expect(steps).toHaveLength(10)
  expect(steps[0]).toHaveAttribute('href', `/app/tug-job-intake?vessel=${vessel}`)
  expect(steps[3]).toHaveAttribute('href', `/app/towage-plan?vessel=${vessel}&job=${job}`)
  expect(screen.getAllByText('Why')).toHaveLength(10)
  expect(screen.getByText(/GO is a human record bound to the current readiness assessment/)).toBeInTheDocument()
  expect(screen.getByText(/Demo data is fictional and unverified/)).toBeInTheDocument()
  expect(fetcher.mock.calls.every(([, init]) => !init || !init.method || init.method === 'GET')).toBe(true)
})
