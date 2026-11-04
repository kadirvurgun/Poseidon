import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { NotificationInbox } from './NotificationWorkspace'
import * as service from './notification-inbox-api'
import { ApiError } from './api'
import { notificationInbox, notificationPreference, noticeVessel, noticeId } from './notification.test-fixture'
import type { Session } from './directory-contract'

vi.mock('./notification-inbox-api', async original => ({ ...await original<typeof import('./notification-inbox-api')>(), loadInbox: vi.fn(), loadPreference: vi.fn(), sendNoticeCommand: vi.fn() }))
const session: Session = { authenticated: true, canRead: true, tenantId: noticeVessel, actorId: noticeId, tenantName: 'FICTIONAL tenant', csrfToken: 'synthetic-only', setupRequired: false, canCreate: false, canReadCompanies: false, canSearch: false }
const props = () => ({ vessel: noticeVessel, session, authorized: true, epoch: 0, language: 'en' as const, onDirty: vi.fn(), onSaving: vi.fn(), onNavigate: vi.fn() })
beforeEach(() => { vi.mocked(service.loadInbox).mockResolvedValue(notificationInbox()); vi.mocked(service.loadPreference).mockResolvedValue(notificationPreference()); vi.mocked(service.sendNoticeCommand).mockResolvedValue({ id: noticeId, revision: 2, version: 8, state: 'Acknowledged' }) })
afterEach(() => vi.resetAllMocks())

it('shows scoped severity/source/due state and mandatory routing even when advisory notifications are muted', async () => {
  vi.mocked(service.loadPreference).mockResolvedValue({ ...notificationPreference(), mutedAdvisory: true })
  render(<NotificationInbox {...props()} />)
  expect(await screen.findByText('Critical')).toBeVisible()
  expect(screen.getByText('Mandatory route · cannot be muted')).toBeVisible()
  expect(screen.getByRole('checkbox')).toBeChecked()
  expect(screen.getByText('2026-11-05')).toBeVisible()
  expect(screen.getByText(/in-app delivery only/)).toBeVisible()
  expect(screen.queryByRole('button', { name: /mute critical/i })).not.toBeInTheDocument()
})
it('does not infer acknowledgement or preference write authority from read access', async () => {
  vi.mocked(service.loadInbox).mockResolvedValue({ ...notificationInbox(), canAcknowledge: false, canWritePreference: false })
  render(<NotificationInbox {...props()} />); await screen.findByText('Critical')
  expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
  expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  expect(service.loadPreference).not.toHaveBeenCalled(); expect(service.sendNoticeCommand).not.toHaveBeenCalled()
})
it.each(['Acknowledged', 'SourceChanged', 'SourceUnavailable'] as const)('keeps %s notices read-only despite an acknowledgement capability', async state => {
  vi.mocked(service.loadInbox).mockResolvedValue({ ...notificationInbox(), items: [{ ...notificationInbox().items[0], state }] })
  render(<NotificationInbox {...props()} />)
  expect(await screen.findByRole('button', { name: 'Acknowledge' })).toBeDisabled()
  expect(service.sendNoticeCommand).not.toHaveBeenCalled()
})
it('retains the exact acknowledgement key/body through an uncertain outcome, translation and explicit retry', async () => {
  vi.mocked(service.sendNoticeCommand).mockRejectedValueOnce(new ApiError(503))
  const p = props(), user = userEvent.setup(), view = render(<NotificationInbox {...p} />)
  await user.click(await screen.findByRole('button', { name: 'Acknowledge' }))
  await screen.findByRole('button', { name: 'Retry exact notification command' })
  expect(service.sendNoticeCommand).toHaveBeenCalledTimes(1)
  const original = vi.mocked(service.sendNoticeCommand).mock.calls[0][1]
  expect(JSON.parse(original.body)).toEqual({ expectedVersion: 7 })
  expect(p.onSaving).toHaveBeenLastCalledWith(true)
  view.rerender(<NotificationInbox {...p} language="tr" />)
  await user.click(screen.getByRole('button', { name: 'Aynı bildirim komutunu yinele' }))
  await waitFor(() => expect(service.sendNoticeCommand).toHaveBeenCalledTimes(2))
  expect(vi.mocked(service.sendNoticeCommand).mock.calls[1][1]).toEqual(original)
})
it('withdraws notices and preserves an unsaved preference through temporary authorization loss', async () => {
  const p = props(), user = userEvent.setup(), view = render(<NotificationInbox {...p} />)
  await user.click(await screen.findByRole('checkbox'))
  expect(p.onDirty).toHaveBeenLastCalledWith(true)
  view.rerender(<NotificationInbox {...p} authorized={false} epoch={1} />)
  expect(screen.queryByText('Critical')).not.toBeInTheDocument()
  expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  expect(service.sendNoticeCommand).not.toHaveBeenCalled()
  view.rerender(<NotificationInbox {...p} authorized epoch={2} />)
  expect(await screen.findByRole('checkbox')).toBeChecked()
  expect(service.sendNoticeCommand).not.toHaveBeenCalled()
})
it('keeps unavailable policy explicit rather than describing an empty page as healthy', async () => {
  vi.mocked(service.loadInbox).mockResolvedValue({ ...notificationInbox(), items: [], policyState: 'Unavailable' })
  render(<NotificationInbox {...props()} />)
  expect(await screen.findByText(/Notification policy unavailable/)).toHaveTextContent('does not mean all clear')
  expect(screen.getByText('No returned notifications on this page.')).toBeVisible()
})

it('shows unavailable source as unresolved recorded history without acknowledgement or a fabricated clear', async () => {
  vi.mocked(service.loadInbox).mockResolvedValue({ ...notificationInbox(), items: [{ ...notificationInbox().items[0], state: 'SourceUnavailable' }] })
  render(<NotificationInbox {...props()} />)
  expect(await screen.findByText('Source unavailable')).toBeVisible()
  expect(screen.getByText(/does not clear or rearm/)).toBeVisible()
  expect(screen.queryByText('Source changed')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Acknowledge' })).toBeDisabled()
  expect(service.sendNoticeCommand).not.toHaveBeenCalled()
})
