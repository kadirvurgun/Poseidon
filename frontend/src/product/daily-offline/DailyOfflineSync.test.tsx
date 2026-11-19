import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { DailyOfflinePanel } from './DailyOfflinePanel'
import { dailyOfflineFixture, dailyCaptureFixture, offlineDailyCommand } from './daily-offline.test-fixture'

// S4 WP-D DEV-033 / DEV-034: unlock-time receipt reconciliation in the real Daily offline panel.
const mock = vi.hoisted(() => ({ authorize: vi.fn(), inventory: vi.fn(), lifecycle: vi.fn(), cleanup: vi.fn(), shell: vi.fn() }))
vi.mock('./daily-offline-application', () => ({ DailyOfflineApplication: { authorize: mock.authorize }, dailyOfflineInventory: mock.inventory, dailyOfflineLifecycle: mock.lifecycle, discardDailyLostCaptures: mock.cleanup }))
vi.mock('../offline-application', () => ({ prepareOfflineShell: mock.shell }))
const later = '33333333-3333-4333-8333-000000000002'
const first = () => ({ commandId: offlineDailyCommand, revision: 1, body: JSON.stringify(dailyCaptureFixture()) })
const second = () => ({ commandId: later, revision: 1, body: JSON.stringify({ ...dailyCaptureFixture(), commandId: later, capturedAt: '2026-11-05T00:02:00Z' }) })
const receipt = { commandId: offlineDailyCommand, result: 'Committed' as const, recordId: dailyOfflineFixture().recordId, kind: 'Noon' as const,
  baseVersion: 0, serverVersion: 1, revision: 1, parentCommandId: null, receivedAt: '2026-11-05T00:05:00Z' }
const props = () => ({ vessel: dailyOfflineFixture().vesselId, recordId: dailyOfflineFixture().recordId, kind: 'Noon' as const, record: null,
  fields: { position_lat: { state: 'Unknown' as const, value: null } }, enabled: true, language: 'en' as const, onDirty: vi.fn(), onSaving: vi.fn(), onCommitted: vi.fn() })
function application() { return { context: dailyOfflineFixture(), active: vi.fn(() => true), remaining: vi.fn(() => 3600000), close: vi.fn(), onWithdraw: vi.fn(),
  captures: vi.fn().mockResolvedValue([first(), second()]), capture: vi.fn(), replay: vi.fn(), conflict: vi.fn(),
  reconcile: vi.fn().mockResolvedValue(new Map([[offlineDailyCommand, 'NotReceived'], [later, 'NotReceived']])) } }
let app: ReturnType<typeof application>
beforeEach(() => { vi.clearAllMocks(); app = application(); mock.authorize.mockResolvedValue(app); mock.inventory.mockResolvedValue([]); mock.shell.mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true }); Object.defineProperty(document, 'hidden', { configurable: true, value: false }); Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' }) })
async function authorize() { await userEvent.click(screen.getByRole('button', { name: 'Authorize encrypted capture' })); await screen.findByText('Explicit bounded authorization') }
const items = () => Array.from(document.querySelectorAll('.daily-offline-queue > li')) as HTMLElement[]

describe('S4 WP-D daily reconnect reconciliation', () => {
  it('a lost response after commit is recovered from the durable receipt on unlock without resending', async () => {
    app.reconcile.mockImplementation(async () => {
      app.captures.mockResolvedValue([{ ...first(), outcome: receipt }, second()])
      return new Map([[offlineDailyCommand, 'Recorded'], [later, 'NotReceived']])
    })
    render(<DailyOfflinePanel {...props()} />); await authorize()
    expect(app.reconcile).toHaveBeenCalledOnce(); expect(app.replay).not.toHaveBeenCalled()
    const [recovered, next] = items()
    expect(within(recovered).getByText('Server committed')).toBeVisible()
    expect(within(next).getByRole('button', { name: 'Send exact queued command' })).toBeEnabled()
  })
  it('a payload mismatch is visible as not saved and blocks the later capture of the record', async () => {
    app.reconcile.mockResolvedValue(new Map([[offlineDailyCommand, 'PayloadMismatch'], [later, 'NotReceived']]))
    render(<DailyOfflinePanel {...props()} />); await authorize()
    const [rejected, next] = items()
    expect(within(rejected).getByRole('alert')).toHaveTextContent('Not saved: the server holds different content for this command key')
    expect(within(rejected).queryByText('Server committed')).not.toBeInTheDocument()
    expect(within(rejected).getByRole('button', { name: 'Send exact queued command' })).toBeDisabled()
    expect(within(next).getByRole('button', { name: 'Send exact queued command' })).toBeDisabled()
    expect(within(next).getByRole('status')).toHaveTextContent('Waiting for an earlier change to this record to reach the server')
    expect(app.replay).not.toHaveBeenCalled()
  })
})
