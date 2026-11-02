import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from './api'
import { OfflineApplicationPanel } from './OfflineApplicationPanel'

const mock = vi.hoisted(() => ({ authorize: vi.fn(), inventory: vi.fn(), cleanup: vi.fn(), shell: vi.fn() }))
vi.mock('./offline-application', () => ({ OfflineApplication: { authorize: mock.authorize }, inventory: mock.inventory,
  discardLostCaptures: mock.cleanup, prepareOfflineShell: mock.shell }))
const vessel = '11111111-1111-4111-8111-111111111111', assignment = '22222222-2222-4222-8222-222222222222'
const enrollment = '33333333-3333-4333-8333-333333333333', command = '44444444-4444-4444-8444-444444444444'
const props = { vessel, assignment, language: 'en' as const, onDirty: vi.fn() }
const pending = { commandId: command, revision: 1, body: '{"frozen":"synthetic"}' }
let app: ReturnType<typeof application>
function application() {
  return { context: { id: enrollment, watchGroups: ['SYNTHETIC-A', 'SYNTHETIC-B'] }, active: vi.fn(() => true), remaining: vi.fn(() => 3600000), close: vi.fn(), onWithdraw: vi.fn(),
    captures: vi.fn().mockResolvedValue([]), snapshot: vi.fn().mockResolvedValue({ watchGroup: null, baseVersion: 7, revision: 1 }),
    capture: vi.fn().mockResolvedValue(command), replay: vi.fn().mockResolvedValue({ result: 'Committed' }),
    history: vi.fn().mockResolvedValue({ items: [], offset: 0, hasMore: false }), conflict: vi.fn(), discard: vi.fn().mockResolvedValue(undefined), revoke: vi.fn().mockResolvedValue(undefined) }
}
beforeEach(() => {
  vi.clearAllMocks(); app = application(); mock.authorize.mockResolvedValue(app); mock.inventory.mockResolvedValue({ items: [] }); mock.shell.mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true })
  Object.defineProperty(document, 'hidden', { configurable: true, value: false })
})
async function authorize() { await userEvent.click(screen.getByRole('button', { name: 'Authorize / renew / recover' })); await screen.findByText('Encrypted capture available for this authorization.') }
describe('OFFLINE-APPLICATION-01 real workspace consumer (AUTHORED / NOT RUN)', () => {
  it('requires explicit enrollment and preserves incomplete unknown draft without producing readiness', async () => {
    render(<OfflineApplicationPanel {...props} />)
    expect(mock.authorize).not.toHaveBeenCalled(); expect(screen.queryByRole('button', { name: 'Capture encrypted draft' })).not.toBeInTheDocument()
    await authorize(); await userEvent.click(screen.getByRole('button', { name: 'Capture encrypted draft' }))
    expect(app.capture).toHaveBeenCalledWith(null); expect(mock.shell).toHaveBeenCalledOnce()
    expect(screen.getByText(/Drafts remain unverified/)).toBeVisible()
  })
  it('captures offline under the explicit active lease and requires online reauthorization on reconnect', async () => {
    app.captures.mockResolvedValue([pending]); render(<OfflineApplicationPanel {...props} />); await authorize()
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false }); fireEvent(window, new Event('offline'))
    await userEvent.selectOptions(screen.getByLabelText('Offline draft watch group'), 'SYNTHETIC-A')
    await userEvent.click(screen.getByRole('button', { name: 'Capture encrypted draft' })); expect(app.capture).toHaveBeenCalledWith('SYNTHETIC-A')
    expect(screen.getByRole('button', { name: 'Replay / check original outcome' })).toBeDisabled()
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: true }); fireEvent(window, new Event('online'))
    expect(app.close).toHaveBeenCalled(); expect(screen.queryByLabelText('Offline draft watch group')).not.toBeInTheDocument()
    expect(mock.authorize).toHaveBeenCalledOnce()
  })
  it('finds retained contexts and renews the selected context without a replacement enrollment', async () => {
    mock.inventory.mockResolvedValue({ items: [{ id: enrollment, revokedAt: null, expiresAt: '2026-11-02T00:00:00Z', policyVersion: 'synthetic' }] })
    render(<OfflineApplicationPanel {...props} />)
    await userEvent.click(screen.getByRole('button', { name: 'Find retained contexts' }))
    await screen.findByRole('option', { name: 'Retained context 1' }); await userEvent.selectOptions(screen.getByLabelText('Offline context'), enrollment)
    await authorize(); expect(mock.authorize).toHaveBeenCalledWith(vessel, assignment, enrollment, expect.any(AbortSignal))
  })
  it('retains the exact uncertain command and retries it without creating another capture', async () => {
    app.captures.mockResolvedValue([pending]); app.replay.mockRejectedValueOnce(new Error('network')).mockResolvedValueOnce({ result: 'Committed' })
    render(<OfflineApplicationPanel {...props} />); await authorize()
    await userEvent.click(screen.getByRole('button', { name: 'Replay / check original outcome' })); await screen.findByText(/Request failed or outcome unknown/)
    await userEvent.click(screen.getByRole('button', { name: 'Replay / check original outcome' }))
    await waitFor(() => expect(app.replay).toHaveBeenCalledTimes(2))
    expect(app.replay.mock.calls[0][0]).toBe(pending); expect(app.replay.mock.calls[1][0]).toBe(pending); expect(app.capture).not.toHaveBeenCalled()
  })
  it('withdraws private projections after current rights denial without deleting the store', async () => {
    app.captures.mockResolvedValue([pending]); app.replay.mockRejectedValue(new ApiError(403))
    render(<OfflineApplicationPanel {...props} />); await authorize()
    await userEvent.click(screen.getByRole('button', { name: 'Replay / check original outcome' }))
    await screen.findByText('Current access denied. Local encrypted data retained.')
    expect(app.close).toHaveBeenCalled(); expect(app.discard).not.toHaveBeenCalled(); expect(screen.queryByLabelText('Stored capture status')).not.toBeInTheDocument()
  })
  it('rejects a late authorized response after the page is hidden', async () => {
    let finish!: (value: typeof app) => void
    mock.authorize.mockReturnValue(new Promise(resolve => { finish = resolve }))
    render(<OfflineApplicationPanel {...props} />); await userEvent.click(screen.getByRole('button', { name: 'Authorize / renew / recover' }))
    Object.defineProperty(document, 'hidden', { configurable: true, value: true }); fireEvent(document, new Event('visibilitychange'))
    await act(async () => { finish(app) })
    expect(app.close).toHaveBeenCalled(); expect(screen.queryByLabelText('Offline draft watch group')).not.toBeInTheDocument()
  })
  it('uses the material conflict reviewer and stores a new explicit reasoned resolution before replay', async () => {
    app.captures.mockResolvedValue([pending, { ...pending, commandId: '55555555-5555-4555-8555-555555555555', outcome: { result: 'MaterialConflict' } }])
    app.conflict.mockResolvedValue({ id: command, revision: '9', contextKey: enrollment, currentVersion: 9, resolved: null, base: { kind: 'unknown' },
      fields: [{ key: 'watch_group', label: 'Watch group', captured: { kind: 'known', text: 'SYNTHETIC-A' }, server: { kind: 'known', text: 'SYNTHETIC-B' }, choices: ['keepServer', 'useCaptured'] }] })
    render(<OfflineApplicationPanel {...props} />); await authorize()
    await userEvent.click(screen.getByRole('button', { name: 'Review material conflict' })); await screen.findByText('Resolve synchronization conflict')
    expect(screen.getByRole('button', { name: 'Submit resolution' })).toBeDisabled()
    await userEvent.click(screen.getByRole('radio', { name: 'Use captured value' })); await userEvent.type(screen.getByLabelText('Resolution reason (required)'), 'Fictional explicit reconciliation')
    await userEvent.click(screen.getByRole('button', { name: 'Submit resolution' }))
    expect(app.capture).toHaveBeenCalledWith(null, { conflictId: command, expectedRevision: '9', reason: 'Fictional explicit reconciliation', decisions: [{ field: 'watch_group', choice: 'useCaptured' }] })
    await waitFor(() => expect(app.replay).toHaveBeenCalled())
  })
  it('requires confirmation before discard and never evicts stored data on failure', async () => {
    app.captures.mockResolvedValue([pending]); render(<OfflineApplicationPanel {...props} />); await authorize()
    expect(screen.getByRole('button', { name: 'Discard this stored capture' })).toBeDisabled()
    await userEvent.click(screen.getByRole('checkbox')); await userEvent.click(screen.getByRole('button', { name: 'Discard this stored capture' }))
    expect(app.discard).toHaveBeenCalledWith(pending, true)
    expect(within(screen.getByRole('region', { name: 'Durable offline drafts' })).getByRole('checkbox')).not.toBeChecked()
  })
  it('withdraws the expired lease without timer-based reauthorization or deletion', async () => {
    vi.useFakeTimers(); app.remaining.mockReturnValue(100)
    try {
      render(<OfflineApplicationPanel {...props} />)
      await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Authorize / renew / recover' })) })
      expect(screen.getByLabelText('Offline draft watch group')).toBeVisible()
      act(() => { vi.advanceTimersByTime(100) })
      expect(screen.queryByLabelText('Offline draft watch group')).not.toBeInTheDocument()
      expect(mock.authorize).toHaveBeenCalledOnce(); expect(app.close).toHaveBeenCalled(); expect(app.discard).not.toHaveBeenCalled()
    } finally { vi.useRealTimers() }
  })
  it('withdraws locally observed superseded context notices without exposing old headers', async () => {
    app.captures.mockResolvedValue([pending]); render(<OfflineApplicationPanel {...props} />); await authorize()
    act(() => { app.onWithdraw.mock.calls[0][0]() })
    expect(screen.queryByLabelText('Stored capture status')).not.toBeInTheDocument(); expect(app.close).toHaveBeenCalled()
    expect(mock.authorize).toHaveBeenCalledOnce(); expect(app.discard).not.toHaveBeenCalled()
  })
  it('pages current-context history without requesting a foreign inventory or inferring an empty full history', async () => {
    app.history.mockResolvedValueOnce({ items: [{ commandId: command, result: 'Committed', revision: 2, receivedAt: '2026-11-01T00:00:00Z', parentCommandId: null }], offset: 0, hasMore: true })
      .mockResolvedValueOnce({ items: [], offset: 25, hasMore: false })
    render(<OfflineApplicationPanel {...props} />); await authorize()
    await userEvent.click(screen.getByRole('button', { name: 'Load synchronization history' }))
    await userEvent.click(screen.getByRole('button', { name: 'Next synchronization history' }))
    expect(app.history).toHaveBeenLastCalledWith(expect.any(AbortSignal), 25)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Next synchronization history' })).toBeDisabled())
    expect(screen.getByRole('button', { name: 'Previous synchronization history' })).toBeEnabled()
  })
})
