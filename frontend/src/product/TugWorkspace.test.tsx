import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TugWorkspace } from './TugWorkspace'
import { emptyTugInputs } from './tug-api'

const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const context = { vessel: { id: vessel, code: 'FICTIONAL', name: 'Synthetic tug' }, mode: 'Tug', crossMode: false, roleReferences: null,
  canWrite: true, owners: [], jobTypes: ['Harbour Towage'], priorities: ['Normal', 'High'], requestSources: ['Customer'], tugType: 'Unknown',
  bollardPull: { value: '45.01', version: 7, provenance: 'LegacyMeasurementUnverified', freshness: 'Unknown', observedAt: null }, equipment: null, equipmentCoverage: 'Unavailable', readiness: 'Unknown', reason: 'VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable' }
const row = { id, vesselId: vessel, jobNumber: 'TUG-SYNTHETIC', revision: 1, version: 8, inputs: emptyTugInputs(), provenance: 'UserDeclaredUnverified', savedAt: '2026-11-01T00:00:00Z', missing: ['requested_start'] }
const fetcher = vi.fn()
let mutate: (init: RequestInit) => Promise<Response>
beforeEach(() => {
  history.replaceState(null, '', `/app/tug-job-intake?vessel=${vessel}`)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  mutate = async init => json({ ...row, id: JSON.parse(init.body as string).clientId, inputs: JSON.parse(init.body as string).inputs })
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (init?.method === 'POST' || init?.method === 'PUT') return mutate(init)
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, csrfToken: 'synthetic-only' })
    if (url.includes('tug-command')) return json(context)
    if (url.includes('tug-job-board')) return json({ items: [{ request: row, windowState: 'MissingStart', plannedTimeSource: 'RequestedWindowOnly;NoApprovedPlan', assignedTugs: [], assignmentCoverage: 'UnavailableControlledAssignmentAdapter', availableBollardPull: null, readiness: 'Unknown', conflict: 'Unknown', declaredComparisons: [], comparisonCoverage: 'VisiblePageEnteredDataOnly', blockingReasons: ['AssignmentCoverageUnavailable'], nextAction: 'CompleteDraft', detailHref: `/app/tug-job-detail/${id}?vessel=${vessel}` }], hasMore: false })
    return json(row)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset() })
async function english() { await userEvent.click(screen.getByRole('button', { name: 'EN', exact: true })); await screen.findByLabelText('Assisted vessel', { exact: true }) }
describe('T-TUG-001/002/003 UI behavior; mocked API, all execution DEFERRED', () => {
  it('keeps exact BP and optional end in an explicitly incomplete draft request', async () => {
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Required BP (tonnes, exact decimal)'), '45.01')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft', exact: true }))
    await screen.findByText('Draft saved. Planning and dispatch approval remain unavailable.')
    const write = fetcher.mock.calls.find(c => c[1]?.method === 'POST')!
    expect(JSON.parse(write[1].body).inputs.requiredBollardPull).toBe('45.01')
    expect(JSON.parse(write[1].body).inputs.requestedEnd).toBeNull()
  })
  it('retains uncertain attempt including original ID, key and body, freezing edits until retry', async () => {
    let count = 0; mutate = async init => { if (count++ === 0) throw new TypeError('synthetic transport loss'); return json({ ...row, id: JSON.parse(init.body as string).clientId }) }
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL retained')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft', exact: true }))
    await screen.findByText(/Save outcome uncertain/); expect(screen.getByLabelText('Assisted vessel', { exact: true })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Retry', exact: true }))
    await screen.findByText('Draft saved. Planning and dispatch approval remain unavailable.')
    const attempts = fetcher.mock.calls.filter(c => c[1]?.method === 'POST')
    expect(attempts).toHaveLength(2); expect(attempts[0][1].body).toBe(attempts[1][1].body)
    expect(attempts[0][1].headers['Idempotency-Key']).toBe(attempts[1][1].headers['Idempotency-Key'])
  })
  it('protects dirty navigation and retains the draft when the user stays', async () => {
    render(<TugWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Assisted vessel', { exact: true }), 'FICTIONAL keep')
    await userEvent.click(screen.getByRole('link', { name: 'Job board', exact: true }))
    await userEvent.click(screen.getByRole('button', { name: 'Stay', exact: true }))
    expect(screen.getByLabelText('Assisted vessel', { exact: true })).toHaveValue('FICTIONAL keep')
    expect(location.pathname).toBe('/app/tug-job-intake')
  })
  it('shows actual coverage and Unknown without claiming assigned tugs or approved planned times', async () => {
    history.replaceState(null, '', `/app/tug-job-board?vessel=${vessel}`)
    render(<TugWorkspace />); await userEvent.click(screen.getByRole('button', { name: 'EN', exact: true }))
    await screen.findByRole('link', { name: 'TUG-SYNTHETIC' })
    expect(screen.getByText('UnavailableControlledAssignmentAdapter')).toBeVisible()
    expect(screen.getByText('RequestedWindowOnly;NoApprovedPlan')).toBeVisible()
    expect(screen.queryByText('Ready', { exact: true })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'TUG-SYNTHETIC' }))
    await waitFor(() => expect(location.pathname).toBe(`/app/tug-job-detail/${id}`))
  })
  it('denies malformed duplicate query selectors before fetching product data', async () => {
    history.replaceState(null, '', `/app/tug-job-board?vessel=${vessel}&vessel=${id}`)
    render(<TugWorkspace />); await userEvent.click(screen.getByRole('button', { name: 'EN', exact: true }))
    await screen.findByText('Data unavailable. Reconnect and retry.')
    expect(fetcher).not.toHaveBeenCalled()
  })
})
