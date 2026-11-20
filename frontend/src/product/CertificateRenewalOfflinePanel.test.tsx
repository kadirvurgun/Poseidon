import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { ApiError } from './api'
import { emptyCertificateRenewal } from './certificate-content'
import { RenewalOfflineApplication, type RenewalPending } from './certificate-renewal-offline'
import { CertificateRenewalOfflinePanel } from './CertificateRenewalOfflinePanel'

// S10 WP-C DEV-066 flow 1 UI: queued never means saved, policy denial and offline states are explicit, the
// sync ledger reports zero loss, and online-only links are not editable offline.
const vessel = '11111111-1111-4111-8111-111111111111', renewal = '22222222-2222-4222-8222-222222222222'
function fakeApp() {
  const rows: RenewalPending[] = []
  const app = {
    context: { baseContent: { ...emptyCertificateRenewal(), plannedLocation: 'FICTIONAL base' } },
    captures: vi.fn(async () => rows.map(r => ({ ...r }))),
    capture: vi.fn(async () => { const commandId = crypto.randomUUID(); rows.push({ commandId, revision: 1, body: '{}' }); return commandId }),
    replay: vi.fn(async (row: RenewalPending) => { const outcome = { commandId: row.commandId, result: 'Committed' as const, renewalId: renewal, baseVersion: 1, serverVersion: 2, revision: 2, parentCommandId: null, receivedAt: '2026-11-21T08:00:00Z' }
      rows.find(r => r.commandId === row.commandId)!.outcome = outcome; return outcome }),
    conflict: vi.fn(), close: vi.fn(),
  }
  return app
}
beforeEach(async () => { await changeAppLanguage('en', false); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks() })

describe('CertificateRenewalOfflinePanel', () => {
  it('queues a draft as not saved and reports a zero-loss ledger after the server receipt', async () => {
    const app = fakeApp(); vi.spyOn(RenewalOfflineApplication, 'authorize').mockResolvedValue(app as unknown as RenewalOfflineApplication)
    const user = userEvent.setup(); render(<CertificateRenewalOfflinePanel vessel={vessel} renewal={renewal} language="en"/>)
    expect(screen.getByText('Configured, not pilot-verified')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Prepare offline draft' }))
    const location = await screen.findByLabelText('Planned location'); expect(location).toHaveValue('FICTIONAL base')
    expect(screen.queryByLabelText(/Replacement certificate/)).toBeNull(); expect(screen.getByText(/can only be changed online/)).toBeInTheDocument()
    await user.clear(location); await user.type(location, 'FICTIONAL offline')
    await user.click(screen.getByRole('button', { name: 'Queue draft' }))
    expect(await screen.findByText(/It is not saved until the server receipt arrives/)).toBeInTheDocument()
    expect(app.capture).toHaveBeenCalledWith(expect.objectContaining({ plannedLocation: 'FICTIONAL offline' }))
    await user.click(screen.getByRole('button', { name: 'Send queued drafts' }))
    expect(await screen.findByTestId('renewal-offline-ledger')).toHaveTextContent('Recorded 1 · Conflicts 0 · Pending 0 · Lost 0')
    expect(app.replay).toHaveBeenCalledTimes(1)
  })
  it('shows policy denial and keeps prepare disabled while offline', async () => {
    vi.spyOn(RenewalOfflineApplication, 'authorize').mockRejectedValue(new ApiError(403, 'offline_policy_denied'))
    const user = userEvent.setup(); const { unmount } = render(<CertificateRenewalOfflinePanel vessel={vessel} renewal={renewal} language="en"/>)
    await user.click(screen.getByRole('button', { name: 'Prepare offline draft' }))
    expect(await screen.findByText('Offline drafting is not configured for you on this vessel.')).toBeInTheDocument()
    unmount(); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    render(<CertificateRenewalOfflinePanel vessel={vessel} renewal={renewal} language="en"/>)
    expect(screen.getByRole('button', { name: 'Prepare offline draft' })).toBeDisabled()
  })
})
