import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { TugOfflinePanel } from './TugOfflinePanel'

// S4 WP-D DEV-033 / DEV-034: unlock-time receipt reconciliation in the real Tug offline panel.
const mock = vi.hoisted(() => ({ authorize: vi.fn(), inventory: vi.fn(), shell: vi.fn(), lost: vi.fn() }))
vi.mock('./tug-offline', () => ({ TugOfflineApplication: { authorize: mock.authorize }, tugOfflineInventory: mock.inventory, reportTugLostKey: mock.lost }))
vi.mock('./offline-application', () => ({ prepareOfflineShell: mock.shell }))
const job = '11111111-1111-4111-8111-111111111111', vessel = '22222222-2222-4222-8222-222222222222'
const first = '44444444-4444-4444-8444-000000000001', second = '44444444-4444-4444-8444-000000000002'
const row = (commandId: string, capturedAt: string) => ({ commandId, revision: 1, body: JSON.stringify({ commandId, baseVersion: 8, capturedAt, plannedStart: null, plannedEnd: null }) })
const committed = { result: 'Committed' as const, serverVersion: 9, revision: 3, parentCommandId: null }
function application() {
  return { context: { id: job, plannedStart: null, plannedEnd: null }, active: vi.fn(() => true), close: vi.fn(),
    captures: vi.fn(), capture: vi.fn(), replay: vi.fn(), conflict: vi.fn(), lifecycle: vi.fn(), reconcile: vi.fn() }
}
let app: ReturnType<typeof application>
beforeEach(() => {
  vi.clearAllMocks(); app = application(); mock.authorize.mockResolvedValue(app); mock.inventory.mockResolvedValue([]); mock.shell.mockResolvedValue(undefined)
  app.captures.mockResolvedValue([row(first, '2026-11-19T10:00:00Z'), row(second, '2026-11-19T10:01:00Z')])
  app.reconcile.mockResolvedValue(new Map([[first, 'NotReceived'], [second, 'NotReceived']]))
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true }); Object.defineProperty(document, 'hidden', { configurable: true, value: false })
})
async function authorize() { await userEvent.click(screen.getByRole('button', { name: 'Authorize encrypted capture' })); await screen.findByText('Enrolled encrypted device capture available') }
const items = () => Array.from(document.querySelectorAll('.offline-capture-list > li')) as HTMLElement[]
const replayButton = (index: number) => within(items()[index]).getByRole('button', { name: 'Replay exact capture' })

describe('S4 WP-D tug reconnect reconciliation', () => {
  it('a lost response after commit is recovered from the durable receipt on unlock without resending', async () => {
    // reconcile() durably stores the server receipt; the queue re-read shows it committed.
    app.reconcile.mockImplementation(async () => {
      app.captures.mockResolvedValue([{ ...row(first, '2026-11-19T10:00:00Z'), outcome: committed }, row(second, '2026-11-19T10:01:00Z')])
      return new Map([[first, 'Recorded'], [second, 'NotReceived']])
    })
    render(<TugOfflinePanel job={job} vessel={vessel} language="en" />); await authorize()
    expect(app.reconcile).toHaveBeenCalledOnce(); expect(app.replay).not.toHaveBeenCalled()
    expect(items()[0]).toHaveTextContent(`${first} · Committed`)
    // The next capture of the same job is now the oldest unresolved one.
    expect(replayButton(1)).toBeEnabled()
  })
  it('a payload mismatch is visible as not saved and blocks later captures of the job', async () => {
    app.reconcile.mockResolvedValue(new Map([[first, 'PayloadMismatch'], [second, 'NotReceived']]))
    render(<TugOfflinePanel job={job} vessel={vessel} language="en" />); await authorize()
    expect(within(items()[0]).getByRole('alert')).toHaveTextContent('Not saved: the server holds different content for this command key')
    expect(items()[0]).not.toHaveTextContent(`${first} · Committed`)
    expect(replayButton(0)).toBeDisabled(); expect(replayButton(1)).toBeDisabled()
    expect(within(items()[1]).getByRole('status')).toHaveTextContent('Waiting for an earlier change to this record to reach the server')
    expect(app.replay).not.toHaveBeenCalled()
  })
  it('reconciliation failure keeps captures pending and replayable in order', async () => {
    app.reconcile.mockRejectedValue(new Error('network'))
    render(<TugOfflinePanel job={job} vessel={vessel} language="en" />); await authorize()
    expect(replayButton(0)).toBeEnabled(); expect(replayButton(1)).toBeDisabled()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
