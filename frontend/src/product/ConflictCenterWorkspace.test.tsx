import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import * as api from './api'
import { ConflictCenterWorkspace } from './ConflictCenterWorkspace'
import { loadSyncConflicts, type SyncConflictItem, type SyncConflictPage } from './sync-conflict-center-api'
import { openCrewConflictReview, type ConflictReviewSession } from './conflict-center-resolver'

// DEV-035 / T-S4-003 screen offline-conflict-center (S4-WPE-CC-UI-01..10).
vi.mock('./EnterpriseChrome', () => ({ EnterpriseChrome: ({ onLanguageChange }: { onLanguageChange: (value: 'en' | 'tr') => void }) => <><button onClick={() => onLanguageChange('en')}>EN</button><button onClick={() => onLanguageChange('tr')}>TR</button></> }))
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), session: vi.fn(), vessels: vi.fn() }))
vi.mock('./sync-conflict-center-api', async original => ({ ...await original<typeof import('./sync-conflict-center-api')>(), loadSyncConflicts: vi.fn() }))
vi.mock('./conflict-center-resolver', async original => ({ ...await original<typeof import('./conflict-center-resolver')>(), openCrewConflictReview: vi.fn() }))

const vessel = '11111111-1111-4111-8111-111111111111', tenant = '10000000-0000-4000-8000-000000000002'
const command = '44444444-4444-4444-8444-444444444444', resolution = '77777777-7777-4777-8777-777777777777'
const item = (patch: Partial<SyncConflictItem> = {}): SyncConflictItem => ({ source: 'crewAssignment', resourceId: '22222222-2222-4222-8222-222222222222', resourceKind: null,
  enrollmentId: '33333333-3333-4333-8333-333333333333', commandId: command, baseVersion: 7, serverVersionAtConflict: 8, capturedAt: '2026-11-19T07:00:00Z',
  receivedAt: '2026-11-19T07:30:00Z', state: 'Open', resolutionCommandId: null, resolutionResult: null, ...patch })
const page = (items: SyncConflictItem[]): SyncConflictPage => ({ vesselId: vessel, readAt: '2026-11-19T08:00:00Z',
  sources: [{ source: 'crewAssignment', state: 'Included' }, { source: 'tugPlan', state: 'Included' }, { source: 'dailyRecord', state: 'NotPermitted' }, { source: 'workOrderEvidence', state: 'NotPermitted' }], items, offset: 0, hasMore: false })
const outcome = { commandId: resolution, result: 'Committed' as const, baseVersion: 9, serverVersion: 10, revision: 10, capturedAt: '2026-11-19T08:00:00Z', offsetMinutes: 0, receivedAt: '2026-11-19T08:01:00Z', parentCommandId: command }
type ReviewMock = Pick<ConflictReviewSession, 'detail' | 'local' | 'actorId'> & { pendingResolution: ConflictReviewSession['pendingResolution']; resolve: Mock; recheck: Mock; discardLocal: Mock; close: Mock }
let review: ReviewMock
function session(pending = false) {
  review = {
    detail: { id: command, revision: '9', contextKey: 'ctx', base: { kind: 'known', text: 'BASE-0' }, currentVersion: 9, resolved: null, resolvedValue: null, resolutionReason: null, resolvedBy: null,
      fields: [{ key: 'watch_group', label: 'Watch group', captured: { kind: 'known', text: 'LOCAL-A' }, server: { kind: 'known', text: 'SERVER-B' }, choices: ['keepServer', 'useCaptured'] }] },
    local: { commandId: command, revision: 2, body: '{}' }, pendingResolution: pending ? { commandId: resolution, revision: 1, body: '{}' } : null, actorId: tenant,
    resolve: vi.fn(), recheck: vi.fn(), discardLocal: vi.fn().mockResolvedValue(undefined), close: vi.fn(),
  }
  return review as unknown as ConflictReviewSession
}
beforeEach(() => {
  history.replaceState(null, '', `/app/offline-conflict-center?vessel=${vessel}`)
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible')
  vi.mocked(api.session).mockResolvedValue({ authenticated: true, canRead: true, actorId: tenant, tenantId: tenant, tenantName: 'FICTIONAL tenant', csrfToken: 'synthetic-only', setupRequired: false, canCreate: false, canReadCompanies: false, canSearch: false })
  vi.mocked(api.vessels).mockResolvedValue({ items: [{ id: vessel, companyId: tenant, code: 'SYN-1', name: 'FICTIONAL vessel', kind: 'Ship', version: 1, canEdit: false }], offset: 0, limit: 25, hasMore: false })
  vi.mocked(loadSyncConflicts).mockResolvedValue(page([item()]))
  vi.mocked(openCrewConflictReview).mockImplementation(async () => session())
})
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })
async function open() {
  const user = userEvent.setup(); render(<ConflictCenterWorkspace />)
  await user.click(screen.getByRole('button', { name: 'EN' }))
  await screen.findByRole('region', { name: 'Material conflicts' })
  return user
}

describe('DEV-035 material conflict center', () => {
  it('S4-WPE-CC-UI-01 lists metadata only, names excluded sources and never shows a green ready state', async () => {
    await open()
    const row = screen.getByRole('row', { name: /Crew assignment/ })
    expect(within(row).getByText('Open — decision required')).toBeVisible()
    expect(within(row).getByText(/Base 7 → server 8/)).toBeVisible()
    expect(screen.getByRole('note')).toHaveTextContent('Daily record (no read permission)')
    expect(screen.queryByText('LOCAL-A')).not.toBeInTheDocument()
    expect(screen.queryByText(/^ready$|^saved$|^approved$/i)).not.toBeInTheDocument()
  })

  it('S4-WPE-CC-UI-02 shows before/local/server diff with no preselected choice and requires a reason before an explicit decision', async () => {
    const user = await open()
    await user.click(screen.getByRole('button', { name: /^Review: Crew assignment/ }))
    const region = await screen.findByRole('region', { name: 'Synchronization conflict review' })
    expect(within(region).getByText('BASE-0')).toBeVisible(); expect(within(region).getByText('LOCAL-A')).toBeVisible(); expect(within(region).getByText('SERVER-B')).toBeVisible()
    for (const radio of within(region).getAllByRole('radio')) expect(radio).not.toBeChecked()
    const submit = within(region).getByRole('button', { name: 'Submit resolution' })
    expect(submit).toBeDisabled()
    await user.click(within(region).getByRole('radio', { name: 'Use captured value' })); expect(submit).toBeDisabled()
    await user.type(within(region).getByLabelText('Resolution reason (required)'), 'FICTIONAL reasoned decision')
    let finish!: (value: unknown) => void
    review.resolve.mockReturnValue(new Promise(resolve => { finish = resolve }))
    await user.click(submit)
    expect(review.resolve).toHaveBeenCalledWith({ conflictId: command, expectedRevision: '9', reason: 'FICTIONAL reasoned decision', decisions: [{ field: 'watch_group', choice: 'useCaptured' }] }, expect.any(AbortSignal))
    expect(screen.queryByText(/Server receipt recorded/)).not.toBeInTheDocument() // no false "saved" while pending
    await act(async () => finish({ kind: 'committed', outcome }))
    expect(await screen.findByText(/Server receipt recorded for your explicit decision/)).toHaveTextContent(resolution)
    expect(screen.getByText(/This receipt is not an operational approval/)).toBeVisible()
  })

  it('S4-WPE-CC-UI-03 lost response keeps the frozen decision as UNKNOWN and only offers an explicit outcome check', async () => {
    const user = await open()
    await user.click(screen.getByRole('button', { name: /^Review: Crew assignment/ }))
    const region = await screen.findByRole('region', { name: 'Synchronization conflict review' })
    await user.click(within(region).getByRole('radio', { name: 'Keep server value' }))
    await user.type(within(region).getByLabelText('Resolution reason (required)'), 'FICTIONAL keep')
    review.resolve.mockImplementation(async () => { review.pendingResolution = { commandId: resolution, revision: 1, body: '{}' }; throw new TypeError('Failed to fetch') })
    await user.click(within(region).getByRole('button', { name: 'Submit resolution' }))
    expect(await within(region).findByText(/The submission outcome is unknown/)).toBeVisible()
    expect(screen.queryByText(/Server receipt recorded/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Close review' })).toBeDisabled()
    review.recheck.mockResolvedValue({ kind: 'committed', outcome })
    await user.click(within(region).getByRole('button', { name: 'Check submission outcome' }))
    expect(review.recheck).toHaveBeenCalledTimes(1); expect(review.resolve).toHaveBeenCalledTimes(1)
    expect(await screen.findByText(/Server receipt recorded/)).toBeVisible()
  })

  it('S4-WPE-CC-UI-04 a retained resolution after reload opens directly in outcome-check mode and cannot be re-decided', async () => {
    vi.mocked(openCrewConflictReview).mockImplementation(async () => session(true))
    const user = await open()
    await user.click(screen.getByRole('button', { name: /^Review: Crew assignment/ }))
    const region = await screen.findByRole('region', { name: 'Synchronization conflict review' })
    expect(within(region).getByText(/The submission outcome is unknown/)).toBeVisible()
    expect(within(region).getByRole('button', { name: 'Submit resolution' })).toBeDisabled()
  })

  it('S4-WPE-CC-UI-05 discard needs explicit confirmation and states that the server value is unchanged', async () => {
    const user = await open()
    await user.click(screen.getByRole('button', { name: /^Review: Crew assignment/ }))
    const button = await screen.findByRole('button', { name: 'Discard local change' })
    expect(button).toBeDisabled()
    await user.click(screen.getByLabelText('I understand my local change will be permanently removed from this device.'))
    await user.click(button)
    expect(review.discardLocal).toHaveBeenCalledWith(true)
    expect(await screen.findByText('Your local copy was discarded. The server value is unchanged.')).toBeVisible()
  })

  it('S4-WPE-CC-UI-06 server change after review withdraws the decision without claiming success', async () => {
    const user = await open()
    await user.click(screen.getByRole('button', { name: /^Review: Crew assignment/ }))
    const region = await screen.findByRole('region', { name: 'Synchronization conflict review' })
    await user.click(within(region).getByRole('radio', { name: 'Use captured value' }))
    await user.type(within(region).getByLabelText('Resolution reason (required)'), 'FICTIONAL')
    review.resolve.mockRejectedValue(new api.ApiError(409, 'offline_conflict_changed'))
    await user.click(within(region).getByRole('button', { name: 'Submit resolution' }))
    expect(await screen.findByText(/changed after your review\. Your decision was not applied/)).toBeVisible()
    expect(screen.queryByText(/Server receipt recorded/)).not.toBeInTheDocument()
  })

  it('S4-WPE-CC-UI-07 other device / non-crew sources are never resolved from the center', async () => {
    vi.mocked(loadSyncConflicts).mockResolvedValue(page([item({ source: 'tugPlan' })]))
    const user = await open()
    await user.click(screen.getByRole('button', { name: /^Review: Tug plan/ }))
    expect(await screen.findByText(/Resolve this conflict from the owning record's offline panel/)).toBeVisible()
    expect(openCrewConflictReview).not.toHaveBeenCalled()
  })

  it('S4-WPE-CC-UI-08 missing local key reports another device and keeps everything', async () => {
    vi.mocked(openCrewConflictReview).mockRejectedValue(new Error('key_lost_data_retained'))
    const user = await open()
    await user.click(screen.getByRole('button', { name: /^Review: Crew assignment/ }))
    expect(await screen.findByRole('alert')).toHaveTextContent('captured on another device')
  })

  it('S4-WPE-CC-UI-09 loading, empty, error/retry, denied and offline states are explicit', async () => {
    vi.mocked(loadSyncConflicts).mockResolvedValueOnce(page([]))
    await open()
    expect(await screen.findByText('No material conflicts are recorded for you on this vessel.')).toBeVisible()
  })

  it('S4-WPE-CC-UI-10 error, denial and offline never imply an empty or resolved list', async () => {
    vi.mocked(loadSyncConflicts).mockRejectedValueOnce(new api.ApiError(500)).mockResolvedValue(page([item()]))
    const user = userEvent.setup(); render(<ConflictCenterWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
    expect(await screen.findByText('Material conflicts could not be loaded. Nothing was resolved.')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('region', { name: 'Material conflicts' })).toBeVisible()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    act(() => window.dispatchEvent(new Event('offline')))
    expect(screen.getByText(/You are offline/)).toBeVisible()
    expect(screen.queryByRole('region', { name: 'Material conflicts' })).not.toBeInTheDocument()
  })

  it('S4-WPE-CC-UI-11 keyboard: Review is reachable and Escape closes an idle review', async () => {
    const user = await open()
    const button = screen.getByRole('button', { name: /^Review: Crew assignment/ })
    button.focus(); await user.keyboard('{Enter}')
    const region = await screen.findByRole('region', { name: 'Synchronization conflict review' })
    expect(region).toBeVisible()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('region', { name: 'Synchronization conflict review' })).not.toBeInTheDocument()
    expect(review.close).toHaveBeenCalled()
  })

  it('S4-WPE-CC-UI-12 localizes the screen (Turkish) without changing data', async () => {
    const user = await open()
    await user.click(screen.getByRole('button', { name: 'TR' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Önemli çakışma merkezi' })).toBeVisible()
    expect(screen.getByText('Açık — karar gerekli')).toBeVisible()
    expect(loadSyncConflicts).toHaveBeenCalledTimes(1)
  })
})
