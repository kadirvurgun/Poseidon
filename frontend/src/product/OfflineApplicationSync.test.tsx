import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { OfflineApplicationPanel } from './OfflineApplicationPanel'

// S4 WP-D DEV-033 / DEV-034: reconnect reconciliation and per-resource ordering in the real Crew panel.
const mock = vi.hoisted(() => ({ authorize: vi.fn(), inventory: vi.fn(), cleanup: vi.fn(), shell: vi.fn() }))
vi.mock('./offline-application', () => ({ OfflineApplication: { authorize: mock.authorize }, inventory: mock.inventory,
  discardLostCaptures: mock.cleanup, prepareOfflineShell: mock.shell }))
const vessel = '11111111-1111-4111-8111-111111111111', assignment = '22222222-2222-4222-8222-222222222222'
const enrollment = '33333333-3333-4333-8333-333333333333'
const first = '44444444-4444-4444-8444-000000000001', second = '44444444-4444-4444-8444-000000000002'
const props = { vessel, assignment, language: 'en' as const, onDirty: vi.fn() }
const row = (commandId: string, capturedAt: string) => ({ commandId, revision: 1, body: JSON.stringify({ commandId, baseVersion: 7, capturedAt }) })
let app: ReturnType<typeof application>
function application() {
  return { context: { id: enrollment, watchGroups: ['SYNTHETIC-A'] }, active: vi.fn(() => true), remaining: vi.fn(() => 3600000), close: vi.fn(), onWithdraw: vi.fn(),
    captures: vi.fn().mockResolvedValue([row(second, '2026-11-19T10:01:00Z'), row(first, '2026-11-19T10:00:00Z')]),
    snapshot: vi.fn().mockResolvedValue({ watchGroup: null, baseVersion: 7, revision: 1 }),
    reconcile: vi.fn().mockResolvedValue(new Map([[first, 'NotReceived'], [second, 'NotReceived']])),
    capture: vi.fn(), replay: vi.fn().mockResolvedValue({ result: 'Committed' }),
    history: vi.fn().mockResolvedValue({ items: [], offset: 0, hasMore: false }), conflict: vi.fn(), discard: vi.fn(), revoke: vi.fn() }
}
beforeEach(() => {
  vi.clearAllMocks(); app = application(); mock.authorize.mockResolvedValue(app); mock.inventory.mockResolvedValue({ items: [] }); mock.shell.mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true })
  Object.defineProperty(document, 'hidden', { configurable: true, value: false })
})
async function authorize() { await userEvent.click(screen.getByRole('button', { name: 'Authorize / renew / recover' })); await screen.findByText('Encrypted capture available for this authorization.') }
const item = (commandId: string) => screen.getByText(commandId).closest('li') as HTMLElement
const replayButton = (commandId: string) => within(item(commandId)).getByRole('button', { name: 'Replay / check original outcome' })

describe('S4 WP-D reconnect reconciliation (DEV-033/DEV-034)', () => {
  it('reconciles server receipts on online unlock before rendering the queue', async () => {
    render(<OfflineApplicationPanel {...props} />); await authorize()
    expect(app.reconcile).toHaveBeenCalledOnce()
    expect(app.replay).not.toHaveBeenCalled()
  })
  it('only the oldest pending capture of the record may be replayed; the newer one waits', async () => {
    render(<OfflineApplicationPanel {...props} />); await authorize()
    expect(replayButton(first)).toBeEnabled()
    expect(replayButton(second)).toBeDisabled()
    expect(within(item(second)).getByRole('status')).toHaveTextContent('Waiting for an earlier change to this record to reach the server')
  })
  it('a key reused with different content is shown as not saved and cannot be resent', async () => {
    app.reconcile.mockResolvedValue(new Map([[first, 'PayloadMismatch'], [second, 'NotReceived']]))
    render(<OfflineApplicationPanel {...props} />); await authorize()
    expect(within(item(first)).getByRole('alert')).toHaveTextContent('Not saved: the server holds different content for this command key')
    expect(replayButton(first)).toBeDisabled(); expect(replayButton(second)).toBeDisabled()
  })
  it('reconciliation failure keeps every capture pending and the panel usable', async () => {
    app.reconcile.mockRejectedValue(new Error('network'))
    render(<OfflineApplicationPanel {...props} />); await authorize()
    expect(replayButton(first)).toBeEnabled()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
