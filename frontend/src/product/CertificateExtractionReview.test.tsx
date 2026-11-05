import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CertificateExtractionReview, type CertificateExtractionReviewProps } from './CertificateExtractionReview'
import { extractionActor, extractionFixture, extractionId, extractionSavedFixture, extractionVessel } from './certificate-extraction-test-fixtures'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
function transport() {
  const state = { actor: extractionActor, configured: true, confirm: undefined as ((init: RequestInit) => Promise<Response>) | undefined }
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return json({ authenticated: true, canRead: true, actorId: state.actor, tenantId: extractionVessel, csrfToken: 'synthetic-csrf-only' })
    if (url.endsWith(`/certificates/${extractionId}`)) return json({ ...extractionSavedFixture(), currentRevision: true, canEdit: true })
    if (url.endsWith('/confirm')) return state.confirm ? state.confirm(init!) : json(extractionSavedFixture())
    return state.configured ? json(extractionFixture()) : json({ code: 'source_contract_changed' }, 503)
  }); vi.stubGlobal('fetch', fetch); return { state, fetch }
}
function props(): CertificateExtractionReviewProps { return { vessel: extractionVessel, certificateId: extractionId, expectedVersion: 17, enabled: true, language: 'en', onDirty: vi.fn(), onSaving: vi.fn(), onApplied: vi.fn() } }
async function preview() { const user = userEvent.setup(); await waitFor(() => expect(screen.getByRole('button', { name: 'Extract from linked document' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Extract from linked document' })); await screen.findByLabelText('Reviewed value · Certificate number'); return user }
async function selectFact() { const user = await preview(); await user.clear(screen.getByLabelText('Reviewed value · Certificate number')); await user.type(screen.getByLabelText('Reviewed value · Certificate number'), 'MASKED-CORRECTED'); await user.click(screen.getByLabelText('I checked this fact against the source · Certificate number')); await user.click(screen.getByLabelText('I checked that this document belongs to this vessel and certificate')); return user }
beforeEach(() => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('DEV045 human confirmation interaction source, not executed', () => {
  it('never extracts automatically, shows missing/low confidence, and does not save without explicit per-fact selection', async () => {
    const { fetch } = transport(), p = props(); render(<CertificateExtractionReview {...p} />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Extract from linked document' })).toBeEnabled()); expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
    const user = await preview(); expect(screen.getAllByText('Low confidence · review').length).toBeGreaterThan(0); expect(screen.getAllByText('Confidence unavailable').length).toBeGreaterThan(0)
    await user.click(screen.getByRole('button', { name: 'Confirm selected facts and save draft' })); expect(await screen.findByRole('alert')).toHaveTextContent('Confirm vessel context')
    expect(fetch.mock.calls.filter(([url]) => url.endsWith('/confirm'))).toHaveLength(0); expect(p.onApplied).not.toHaveBeenCalled(); expect(p.onDirty).toHaveBeenLastCalledWith(true)
  })
  it('sends only selected corrected facts and treats success as immutable receipt', async () => {
    const { fetch } = transport(), p = props(); render(<CertificateExtractionReview {...p} />); const user = await selectFact()
    await user.click(screen.getByRole('button', { name: 'Confirm selected facts and save draft' })); await waitFor(() => expect(p.onApplied).toHaveBeenCalledWith(extractionSavedFixture()))
    const call = fetch.mock.calls.find(([url]) => url.endsWith('/confirm'))!; expect(JSON.parse(call[1]!.body as string)).toEqual({ expectedVersion: 17, reviewContext: extractionFixture().reviewContext, facts: [{ key: 'certificate_no', value: 'MASKED-CORRECTED', confirmed: true }], vesselContextConfirmed: true })
    expect(p.onDirty).toHaveBeenLastCalledWith(false); expect(p.onSaving).toHaveBeenLastCalledWith(false)
  })
  it('freezes unknown commands across later denial and retries exact original key/body without automatic writes', async () => {
    const { state, fetch } = transport(), p = props(); let attempts = 0
    state.confirm = async () => ++attempts === 1 ? json({ code: 'command_outcome_withheld' }, 503) : attempts === 2 ? json({ code: 'permission_denied' }, 403) : json(extractionSavedFixture())
    render(<CertificateExtractionReview {...p} />); const user = await selectFact(); await user.click(screen.getByRole('button', { name: 'Confirm selected facts and save draft' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Save outcome unknown'); expect(p.onSaving).toHaveBeenLastCalledWith(true); expect(screen.getByLabelText('Reviewed value · Certificate number')).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Retry exact confirmation' })); await waitFor(() => expect(attempts).toBe(2)); await user.click(await screen.findByRole('button', { name: 'Reauthorize original context' })); await waitFor(() => expect(screen.getByRole('button', { name: 'Retry exact confirmation' })).toBeEnabled())
    expect(p.onSaving).toHaveBeenLastCalledWith(true); await user.click(screen.getByRole('button', { name: 'Retry exact confirmation' })); await waitFor(() => expect(p.onApplied).toHaveBeenCalledTimes(1))
    const calls = fetch.mock.calls.filter(([url]) => url.endsWith('/confirm')); expect(calls).toHaveLength(3); expect(new Set(calls.map(([, init]) => init?.body)).size).toBe(1); expect(new Set(calls.map(([, init]) => JSON.stringify(init?.headers))).size).toBe(1)
  })
  it('hides retained private suggestions after identity/visibility changes and suppresses late applied callbacks', async () => {
    const { state } = transport(), p = props(); let complete: ((response: Response) => void) | undefined
    state.confirm = async () => new Promise<Response>(resolve => { complete = resolve })
    const view = render(<CertificateExtractionReview {...p} />), user = await selectFact(); await user.click(screen.getByRole('button', { name: 'Confirm selected facts and save draft' })); await waitFor(() => expect(complete).toBeDefined())
    state.actor = extractionId; view.rerender(<CertificateExtractionReview {...p} enabled={false} />)
    expect(screen.queryByDisplayValue('MASKED-CORRECTED')).not.toBeInTheDocument(); await act(async () => { complete!(json(extractionSavedFixture())) }); expect(p.onApplied).not.toHaveBeenCalled()
    view.rerender(<CertificateExtractionReview {...p} />); act(() => { window.dispatchEvent(new Event('focus')) }); await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('original identity'))
    expect(screen.queryByDisplayValue('MASKED-CORRECTED')).not.toBeInTheDocument(); expect(p.onDirty).toHaveBeenLastCalledWith(true)
    state.actor = extractionActor; act(() => { window.dispatchEvent(new Event('focus')) }); expect(await screen.findByDisplayValue('MASKED-CORRECTED')).toBeDisabled(); expect(p.onSaving).toHaveBeenLastCalledWith(true)
  })
  it('keeps missing processing configuration unavailable with no invented candidates', async () => {
    const { state, fetch } = transport(); state.configured = false; render(<CertificateExtractionReview {...props()} />)
    const user = userEvent.setup(); await waitFor(() => expect(screen.getByRole('button', { name: 'Extract from linked document' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Extract from linked document' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Processing policy'); expect(screen.queryByLabelText('Reviewed value · Certificate number')).not.toBeInTheDocument(); expect(fetch.mock.calls.filter(([url]) => url.endsWith('/confirm'))).toHaveLength(0)
  })
})
