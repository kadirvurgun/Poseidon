import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, expect, it, vi } from 'vitest'
import { ApiError } from './api'
import { TugOfflinePanel } from './TugOfflinePanel'

const mock = vi.hoisted(() => ({ authorize: vi.fn(), inventory: vi.fn(), shell: vi.fn(), lost: vi.fn() }))
vi.mock('./tug-offline', () => ({ TugOfflineApplication: { authorize: mock.authorize }, tugOfflineInventory: mock.inventory, reportTugLostKey: mock.lost }))
vi.mock('./offline-application', () => ({ prepareOfflineShell: mock.shell }))
const job = '11111111-1111-4111-8111-111111111111', vessel = '22222222-2222-4222-8222-222222222222'
const pending = { commandId: job, revision: 1, body: '{"frozen":"synthetic"}' }
const app = { context: { id: job, plannedStart: null, plannedEnd: null }, active: vi.fn(() => true), close: vi.fn(),
  captures: vi.fn(), capture: vi.fn(), replay: vi.fn(), conflict: vi.fn(), lifecycle: vi.fn() }
beforeEach(() => {
  vi.clearAllMocks(); mock.authorize.mockResolvedValue(app); mock.inventory.mockResolvedValue([]); mock.shell.mockResolvedValue(undefined)
  app.captures.mockResolvedValue([]); app.capture.mockResolvedValue([pending]); app.replay.mockResolvedValue({ result: 'Committed' })
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true }); Object.defineProperty(document, 'hidden', { configurable: true, value: false })
})
async function authorize() { await userEvent.click(screen.getByRole('button', { name: 'Authorize encrypted capture' })); await screen.findByText('Enrolled encrypted device capture available') }
it('captures only nullable times after real enrollment contract and withdraws on reconnect without automatic sync', async () => {
  render(<TugOfflinePanel job={job} vessel={vessel} language="en" />)
  expect(screen.queryByRole('button', { name: 'Capture encrypted draft' })).not.toBeInTheDocument(); await authorize()
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: false }); fireEvent(window, new Event('offline'))
  await userEvent.click(screen.getByRole('button', { name: 'Capture encrypted draft' }))
  expect(app.capture).toHaveBeenCalledWith({ plannedStart: null, plannedEnd: null }); expect(screen.getByText(/Encrypted capture; not committed/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Replay exact capture' })).toBeDisabled()
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true }); fireEvent(window, new Event('online'))
  expect(app.close).toHaveBeenCalled(); expect(app.replay).not.toHaveBeenCalled(); expect(screen.queryByLabelText('Offline planned start (UTC)')).not.toBeInTheDocument()
})
it('retains exact uncertain retry and hides context on current authorization denial', async () => {
  app.captures.mockResolvedValue([pending]); app.replay.mockRejectedValueOnce(new TypeError('transport')).mockRejectedValueOnce(new ApiError(403))
  render(<TugOfflinePanel job={job} vessel={vessel} language="en" />); await authorize()
  await userEvent.click(screen.getByRole('button', { name: 'Replay exact capture' })); await screen.findByText(/Outcome uncertain/)
  await userEvent.click(screen.getByRole('button', { name: 'Replay exact capture' })); await screen.findByText(/Current access denied/)
  expect(app.replay.mock.calls[0][0]).toEqual(pending); expect(app.replay.mock.calls[1][0]).toEqual(pending)
  expect(screen.queryByText(job)).not.toBeInTheDocument(); expect(screen.queryByLabelText('Offline planned start (UTC)')).not.toBeInTheDocument()
})
it('guards dirty values and closes the old handle on object change without carrying draft data', async () => {
  const dirty = vi.fn(); const view = render(<TugOfflinePanel job={job} vessel={vessel} language="en" onDirty={dirty} />); await authorize()
  fireEvent.change(screen.getByLabelText('Offline planned start (UTC)'), { target: { value: '2026-11-03T12:00:00' } }); await waitFor(() => expect(dirty).toHaveBeenLastCalledWith(true))
  view.rerender(<TugOfflinePanel job={vessel} vessel={vessel} language="tr" onDirty={dirty} />)
  expect(app.close).toHaveBeenCalled(); expect(screen.queryByLabelText('Offline planned start (UTC)')).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Kilitli')
})
