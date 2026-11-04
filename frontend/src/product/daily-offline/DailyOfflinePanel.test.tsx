import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { ApiError } from '../api'
import { DailyOfflinePanel } from './DailyOfflinePanel'
import { dailyOfflineFixture, dailyCaptureFixture, offlineDailyCommand } from './daily-offline.test-fixture'
const mock = vi.hoisted(() => ({ authorize: vi.fn(), inventory: vi.fn(), lifecycle: vi.fn(), cleanup: vi.fn(), shell: vi.fn() }))
vi.mock('./daily-offline-application', () => ({ DailyOfflineApplication: { authorize: mock.authorize }, dailyOfflineInventory: mock.inventory, dailyOfflineLifecycle: mock.lifecycle, discardDailyLostCaptures: mock.cleanup }))
vi.mock('../offline-application', () => ({ prepareOfflineShell: mock.shell }))
const pending = () => ({ commandId: offlineDailyCommand, revision: 1, body: JSON.stringify(dailyCaptureFixture()) })
const props = () => ({ vessel: dailyOfflineFixture().vesselId, recordId: dailyOfflineFixture().recordId, kind: 'Noon' as const, record: null,
  fields: { position_lat: { state: 'Unknown' as const, value: null }, weather_summary: { state: 'Known' as const, value: 'UNCLASSIFIED ordinary text' } }, enabled: true, language: 'en' as const, onDirty: vi.fn(), onSaving: vi.fn(), onCommitted: vi.fn() })
function application() { return { context: dailyOfflineFixture(), active: vi.fn(() => true), remaining: vi.fn(() => 3600000), close: vi.fn(), onWithdraw: vi.fn(),
  captures: vi.fn().mockResolvedValue([]), capture: vi.fn().mockResolvedValue([pending()]), replay: vi.fn().mockResolvedValue({ result: 'Committed' }), conflict: vi.fn() } }
let app: ReturnType<typeof application>
beforeEach(() => { vi.clearAllMocks(); app = application(); mock.authorize.mockResolvedValue(app); mock.inventory.mockResolvedValue([]); mock.shell.mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true }); Object.defineProperty(document, 'hidden', { configurable: true, value: false }); Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' }) })
afterEach(() => vi.restoreAllMocks())
async function authorize() { await userEvent.click(screen.getByRole('button', { name: 'Authorize encrypted capture' })); await screen.findByText('Explicit bounded authorization') }
it('keeps restart locked until explicit online authorization and renders only classified fields', async () => {
  const p = props(); const first = render(<DailyOfflinePanel {...p} />)
  expect(mock.authorize).not.toHaveBeenCalled(); expect(screen.queryByLabelText('Latitude')).not.toBeInTheDocument()
  await authorize(); expect(screen.getByLabelText('Latitude')).toBeVisible(); expect(screen.queryByText('UNCLASSIFIED ordinary text')).not.toBeInTheDocument()
  expect(screen.queryByLabelText('Weather / sea summary')).not.toBeInTheDocument(); expect(screen.queryByText('REVIEWED')).not.toBeInTheDocument()
  first.unmount(); render(<DailyOfflinePanel {...p} />)
  expect(screen.getByText('Locked · online authorization required')).toBeVisible(); expect(screen.queryByLabelText('Latitude')).not.toBeInTheDocument()
  expect(mock.authorize).toHaveBeenCalledTimes(1); expect(app.replay).not.toHaveBeenCalled()
})
it('captures Unknown during a bounded offline handle and withdraws on reconnect without automatic replay or renewal', async () => {
  render(<DailyOfflinePanel {...props()} />); await authorize()
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: false }); fireEvent(window, new Event('offline'))
  await userEvent.click(screen.getByRole('button', { name: 'Save classified fields to encrypted queue' }))
  expect(app.capture).toHaveBeenCalledWith({ position_lat: { state: 'Unknown', value: null } })
  expect(screen.getByRole('button', { name: 'Send exact queued command' })).toBeDisabled()
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true }); fireEvent(window, new Event('online'))
  expect(app.close).toHaveBeenCalled(); expect(screen.queryByLabelText('Latitude')).not.toBeInTheDocument()
  expect(mock.authorize).toHaveBeenCalledTimes(1); expect(app.replay).not.toHaveBeenCalled()
})
it('retains the original envelope after an uncertain delivery and retries the exact queued object', async () => {
  app.captures.mockResolvedValue([pending()]); app.replay.mockRejectedValueOnce(new ApiError(503, 'command_outcome_withheld'))
  const p = props(); render(<DailyOfflinePanel {...p} />); await authorize()
  await userEvent.click(screen.getByRole('button', { name: 'Send exact queued command' }))
  expect(await screen.findByText(/Submission outcome unknown/)).toBeVisible()
  expect(p.onCommitted).not.toHaveBeenCalled(); expect(app.capture).not.toHaveBeenCalled()
  await userEvent.click(screen.getByRole('button', { name: 'Send exact queued command' }))
  await waitFor(() => expect(app.replay).toHaveBeenCalledTimes(2))
  expect(app.replay.mock.calls[0][0]).toEqual(app.replay.mock.calls[1][0]); expect(app.replay.mock.calls[1][0].body).toBe(pending().body)
})
it('removes private display on known denial or hidden state while preserving encrypted retention', async () => {
  const p = props(), view = render(<DailyOfflinePanel {...p} />); await authorize()
  view.rerender(<DailyOfflinePanel {...p} enabled={false} />)
  expect(screen.queryByLabelText('Latitude')).not.toBeInTheDocument(); expect(app.close).toHaveBeenCalled()
  expect(mock.cleanup).not.toHaveBeenCalled(); expect(app.replay).not.toHaveBeenCalled()
})
it('shows current policy unavailability and never substitutes crew/tug authority or manufactures a capture', async () => {
  mock.authorize.mockRejectedValue(new ApiError(403, 'offline_policy_denied'))
  render(<DailyOfflinePanel {...props()} />)
  await userEvent.click(screen.getByRole('button', { name: 'Authorize encrypted capture' }))
  expect(await screen.findByText(/Daily offline policy, current permission/)).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Save classified fields to encrypted queue' })).not.toBeInTheDocument()
  expect(app.capture).not.toHaveBeenCalled(); expect(mock.cleanup).not.toHaveBeenCalled()
})
it('requires explicit loss-cleanup confirmation and never replaces a missing key automatically', async () => {
  mock.authorize.mockRejectedValue(new Error('key_lost_data_retained')); mock.inventory.mockResolvedValue([{ id: dailyOfflineFixture().id, expiresAt: dailyOfflineFixture().expiresAt, retainUntil: dailyOfflineFixture().retainUntil, revokedAt: null, policyVersion: 'fixture' }])
  render(<DailyOfflinePanel {...props()} />)
  await userEvent.click(screen.getByRole('button', { name: 'Find retained contexts' }))
  await userEvent.selectOptions(screen.getByLabelText('Retained daily context'), dailyOfflineFixture().id)
  await userEvent.click(screen.getByRole('button', { name: 'Reauthorize retained context' }))
  expect(await screen.findByText(/Original key unavailable/)).toBeVisible(); expect(mock.cleanup).not.toHaveBeenCalled()
  await userEvent.click(screen.getByText('Revoke, key loss and retained-data cleanup'))
  expect(screen.getByRole('button', { name: 'Delete confirmed local context' })).toBeDisabled()
  await userEvent.click(screen.getByRole('checkbox'))
  await userEvent.click(screen.getByRole('button', { name: 'Delete confirmed local context' }))
  await waitFor(() => expect(mock.cleanup).toHaveBeenCalledTimes(1))
  expect(mock.cleanup.mock.calls[0].slice(0, 5)).toEqual(['Noon', props().vessel, props().recordId, dailyOfflineFixture().id, true])
})
it('an explicit elapsed authorization timer closes the handle without a renewal request', async () => {
  vi.useFakeTimers({ shouldAdvanceTime: true }); app.remaining.mockReturnValue(100)
  render(<DailyOfflinePanel {...props()} />); await authorize()
  await act(async () => { await vi.advanceTimersByTimeAsync(101) })
  expect(screen.queryByLabelText('Latitude')).not.toBeInTheDocument(); expect(app.close).toHaveBeenCalled(); expect(mock.authorize).toHaveBeenCalledTimes(1)
  vi.useRealTimers()
})

it('holds never-captured edits privately through withdrawal and restores them only under the original reauthorized context', async () => {
  const p = props(); render(<DailyOfflinePanel {...p} />); await authorize()
  await userEvent.selectOptions(screen.getByLabelText('Latitude — Offline data state'), 'Known')
  await userEvent.type(screen.getByLabelText('Latitude', { selector: 'textarea' }), '-0.001')
  await userEvent.click(screen.getByRole('button', { name: 'Lock retained data' }))
  expect(screen.queryByDisplayValue('-0.001')).not.toBeInTheDocument()
  expect(screen.getByText(/They have not been encrypted/)).toBeVisible(); expect(p.onDirty).toHaveBeenLastCalledWith(true)
  expect(app.capture).not.toHaveBeenCalled()
  await userEvent.click(screen.getByRole('button', { name: 'Reauthorize retained context' }))
  expect(await screen.findByDisplayValue('-0.001')).toBeVisible(); expect(app.capture).not.toHaveBeenCalled()
})
it('distinguishes an audited keep-server outcome from a newly committed report revision', async () => {
  app.captures.mockResolvedValue([{ ...pending(), outcome: { result: 'ServerRetained' } }])
  render(<DailyOfflinePanel {...props()} />); await authorize()
  expect(await screen.findByText('Server retained · no report revision added')).toBeVisible()
  expect(screen.queryByText('Server committed')).not.toBeInTheDocument()
})
