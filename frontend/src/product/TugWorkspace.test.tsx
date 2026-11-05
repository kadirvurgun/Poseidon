import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TugWorkspace } from './TugWorkspace'
import { emptyTugInputs, tugRequestSource } from './tug-api'

const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const context = { vessel: { id: vessel, code: 'FICTIONAL', name: 'Synthetic tug' }, mode: 'Tug', crossMode: false, roleReferences: null,
  canWrite: true, owners: [], jobTypes: ['Harbour Towage'], priorities: ['Normal', 'High'], requestSources: ['Customer'], tugType: 'Unavailable', tugTypeContext: null, tugTypeOptions: [], tugTypePolicyVersion: null,
  bollardPull: { value: '45.01', version: 7, provenance: 'LegacyMeasurementUnverified', freshness: 'Unknown', observedAt: null, source: 'TugProfile' }, equipment: null, equipmentCoverage: 'Unavailable', readiness: 'Unknown', reason: 'VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable' }
const row = { id, vesselId: vessel, jobNumber: 'TUG-SYNTHETIC', revision: 1, version: 8, inputs: emptyTugInputs(), provenance: 'UserDeclaredUnverified', savedAt: '2026-11-01T00:00:00Z', missing: ['requested_start'], replayed: false, sourceVersion: tugRequestSource }
const fetcher = vi.fn()
let mutate: (init: RequestInit) => Promise<Response>
let actorId: string, tenantId: string, canWrite: boolean
beforeEach(() => {
  actorId = '33333333-3333-3333-3333-333333333333'; tenantId = '44444444-4444-4444-4444-444444444444'; canWrite = true
  history.replaceState(null, '', `/app/tug-job-intake?vessel=${vessel}`)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  mutate = async init => json({ ...row, id: JSON.parse(init.body as string).clientId, inputs: JSON.parse(init.body as string).inputs })
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.includes('/plan/options')) return json({ state: 'Unavailable', policyVersion: null, canWrite: false, tugs: [], types: [], offline: 'UnavailableForTug' })
    if (url.includes('/plan/history')) return json({ items: [], coverage: 'Latest100CurrentlyAuthorizedRevisions' })
    if (url.includes('/plan?')) return json({ state: 'Missing', plan: null, evidence: null })
    if (init?.method === 'POST' || init?.method === 'PUT') return mutate(init)
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url === '/api/v1/workspace') return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (url.includes('tug-command')) return json({ ...context, canWrite })
    if (url.includes('tug-job-board')) return json({ items: [{ request: row, windowState: 'MissingStart', plannedTimeSource: 'RequestedWindowOnly;NoApprovedPlan', assignedTugs: [], assignmentCoverage: 'UnavailableControlledAssignmentAdapter', availableBollardPull: null, readiness: 'Unknown', conflict: 'Unknown', declaredComparisons: [], comparisonCoverage: 'VisiblePageEnteredDataOnly', blockingReasons: ['AssignmentCoverageUnavailable'], nextAction: 'CompleteDraft', detailHref: `/app/tug-job-detail/${id}?vessel=${vessel}` }], hasMore: false })
    return json(row)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset() })
async function english() { await userEvent.click(screen.getByRole('button', { name: 'EN' })); await screen.findByLabelText('Assisted vessel', { exact: true }) }
describe('T-TUG-001/002/003 UI behavior; mocked API, all execution DEFERRED', () => {
  it('keeps exact BP and optional end in an explicitly incomplete draft request', async () => {
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Required BP (tonnes, exact decimal)'), '45.01')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByText('Draft saved. Operational approval remains unavailable.')
    const write = fetcher.mock.calls.find(c => c[1]?.method === 'POST')!
    expect(JSON.parse(write[1].body).inputs.requiredBollardPull).toBe('45.01')
    expect(JSON.parse(write[1].body).inputs.requestedEnd).toBeNull()
  })
  it('retains uncertain attempt including original ID, key and body, freezing edits until retry', async () => {
    let count = 0; mutate = async init => { if (count++ === 0) throw new TypeError('synthetic transport loss'); return json({ ...row, id: JSON.parse(init.body as string).clientId }) }
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL retained')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByText(/Save outcome uncertain/); expect(screen.getByLabelText('Assisted vessel', { exact: true })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    await screen.findByText('Draft saved. Operational approval remains unavailable.')
    const attempts = fetcher.mock.calls.filter(c => c[1]?.method === 'POST')
    expect(attempts).toHaveLength(2); expect(attempts[0][1].body).toBe(attempts[1][1].body)
    expect(attempts[0][1].headers['Idempotency-Key']).toBe(attempts[1][1].headers['Idempotency-Key'])
  })
  it('protects dirty navigation and retains the draft when the user stays', async () => {
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL keep')
    await userEvent.click(screen.getByRole('link', { name: 'Job board' }))
    await userEvent.click(screen.getByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('Assisted vessel', { exact: true })).toHaveValue('FICTIONAL keep')
    expect(location.pathname).toBe('/app/tug-job-intake')
  })
  it('shows actual coverage and Unknown without claiming assigned tugs or approved planned times', async () => {
    history.replaceState(null, '', `/app/tug-job-board?vessel=${vessel}`)
    render(<TugWorkspace />); await userEvent.click(screen.getByRole('button', { name: 'EN' }))
    await screen.findByRole('link', { name: 'TUG-SYNTHETIC' })
    expect(screen.getByText('Controlled assignment data unavailable')).toBeVisible()
    expect(screen.getByText('Requested window only; no approved plan')).toBeVisible()
    expect(screen.queryByText('Ready', { exact: true })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'TUG-SYNTHETIC' }))
    await waitFor(() => expect(location.pathname).toBe(`/app/tug-job-detail/${id}`))
  })
  it('denies malformed duplicate query selectors before fetching product data', async () => {
    history.replaceState(null, '', `/app/tug-job-board?vessel=${vessel}&vessel=${id}`)
    render(<TugWorkspace />); await userEvent.click(screen.getByRole('button', { name: 'EN' }))
    await screen.findByText('Data unavailable. Reconnect and retry.')
    expect(fetcher).not.toHaveBeenCalled()
  })
  it('groups the intake by work context and associates server validation with the exact field', async () => {
    mutate = async () => json({ code: 'invalid_tug_request', errors: { assisted_vessel: ['synthetic-field-error'] } }, 400)
    render(<TugWorkspace />); await english()
    expect(screen.getByRole('group', { name: 'Request and assisted vessel' })).toBeVisible()
    expect(screen.getByRole('group', { name: 'Requested time and location' })).toBeVisible()
    expect(screen.getByRole('group', { name: 'Resource needs and notes' })).toBeVisible()
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    const input = await screen.findByLabelText('Assisted vessel', { exact: true })
    await waitFor(() => expect(input).toHaveAttribute('aria-invalid', 'true'))
    expect(input).toHaveAccessibleDescription('Check this value.')
    expect(input).not.toBeDisabled()
  })
  it.each(['actor', 'tenant'] as const)('never sends retained inputs after the %s changes before saving', async change => {
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL private draft')
    if (change === 'actor') actorId = id; else tenantId = id
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByRole('button', { name: 'Recheck access' })
    expect(fetcher.mock.calls.filter(c => ['POST', 'PUT'].includes(c[1]?.method))).toHaveLength(0)
    expect(screen.queryByDisplayValue('FICTIONAL private draft')).not.toBeInTheDocument()
    expect(screen.queryByText('Draft saved. Operational approval remains unavailable.')).not.toBeInTheDocument()
  })
  it('withholds a successful write after identity changes and restores only its original frozen attempt', async () => {
    const original = actorId
    mutate = async init => { actorId = id; return json({ ...row, id: JSON.parse(init.body as string).clientId, inputs: JSON.parse(init.body as string).inputs }) }
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL original owner')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByRole('button', { name: 'Recheck access' })
    expect(screen.queryByText('Draft saved. Operational approval remains unavailable.')).not.toBeInTheDocument()
    expect(location.pathname).toBe('/app/tug-job-intake')
    actorId = original
    await userEvent.click(screen.getByRole('button', { name: 'Recheck access' }))
    await screen.findByText(/Save outcome uncertain/)
    expect(screen.getByLabelText('Assisted vessel', { exact: true })).toBeDisabled()
    mutate = async init => json({ ...row, id: JSON.parse(init.body as string).clientId, inputs: JSON.parse(init.body as string).inputs })
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    await screen.findByText('Draft saved. Operational approval remains unavailable.')
    const writes = fetcher.mock.calls.filter(c => c[1]?.method === 'POST')
    expect(writes).toHaveLength(2)
    expect(writes[0][1].body).toBe(writes[1][1].body)
    expect(writes[0][1].headers['Idempotency-Key']).toBe(writes[1][1].headers['Idempotency-Key'])
  })
  it('preserves an uncertain command through a later denial and current-access recovery', async () => {
    let count = 0
    mutate = async init => ++count === 1 ? json({}, 503) : count === 2 ? json({}, 403) : json({ ...row, id: JSON.parse(init.body as string).clientId, inputs: JSON.parse(init.body as string).inputs })
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL exact retry')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByText(/Save outcome uncertain/)
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    await screen.findByRole('button', { name: 'Recheck access' })
    await userEvent.click(screen.getByRole('button', { name: 'Recheck access' }))
    expect(await screen.findByLabelText('Assisted vessel', { exact: true })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    await screen.findByText('Draft saved. Operational approval remains unavailable.')
    const writes = fetcher.mock.calls.filter(c => c[1]?.method === 'POST')
    expect(writes).toHaveLength(3)
    expect(new Set(writes.map(call => call[1].body)).size).toBe(1)
    expect(new Set(writes.map(call => call[1].headers['Idempotency-Key'])).size).toBe(1)
  })
  it('locks duplicate submits synchronously before the first identity request finishes', async () => {
    let resolve!: (response: Response) => void
    mutate = () => new Promise<Response>(done => { resolve = done })
    render(<TugWorkspace />); await english()
    const form = screen.getByRole('form', { name: 'Job intake' })
    act(() => { fireEvent.submit(form); fireEvent.submit(form) })
    await waitFor(() => expect(fetcher.mock.calls.filter(c => c[1]?.method === 'POST')).toHaveLength(1))
    const write = fetcher.mock.calls.find(c => c[1]?.method === 'POST')!
    await act(async () => { resolve(json({ ...row, id: JSON.parse(write[1].body).clientId })) })
    await screen.findByText('Draft saved. Operational approval remains unavailable.')
  })
  it('withdraws retained inputs on focus identity change and recovers them only for the original actor', async () => {
    const original = actorId
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL retained on focus')
    actorId = id; fireEvent(window, new Event('focus'))
    await screen.findByRole('button', { name: 'Recheck access' })
    expect(screen.queryByDisplayValue('FICTIONAL retained on focus')).not.toBeInTheDocument()
    actorId = original
    await userEvent.click(screen.getByRole('button', { name: 'Recheck access' }))
    expect(await screen.findByLabelText('Assisted vessel', { exact: true })).toHaveValue('FICTIONAL retained on focus')
    expect(fetcher.mock.calls.filter(c => ['POST', 'PUT'].includes(c[1]?.method))).toHaveLength(0)
  })
})
