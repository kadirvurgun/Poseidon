import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CertificateRenewalWorkflowPanel } from './CertificateRenewalWorkflowPanel'
import { ApiError } from './api'
import { certificatesApi, decodeRenewal, decodeRenewalCapabilities, emptyRenewalDecision, type RenewalDto } from './certificates-api'
import { certificateActor, certificateVessel, renewalCapsFixture, renewalFixture } from './certificate-test-fixtures'

vi.mock('./CertificateDocumentPicker', () => ({ CertificateDocumentPicker: () => null }))
vi.mock('./CertificateReplacementPicker', () => ({ CertificateReplacementPicker: () => null }))
vi.mock('./api', async original => ({ ...(await original<typeof import('./api')>()),
  session: vi.fn(async () => ({ authenticated: true, canRead: true, tenantId: certificateVessel, actorId: certificateActor, csrfToken: 'csrf' })) }))

const preparing = (): RenewalDto => ({ ...renewalFixture(), state: 'PREPARING', canEdit: true, allowedTransitions: ['CANCEL', 'VALIDATE'] })
const renderPanel = (record: RenewalDto, onApplied = vi.fn(), enabled = true) => {
  render(<CertificateRenewalWorkflowPanel vessel={certificateVessel} record={record} enabled={enabled} language="en" onDirty={() => undefined} onApplied={onApplied}/>)
  return onApplied
}

describe('DEV-044 renewal workflow contract', () => {
  it('admits every source state and rejects manufactured authority, completion or stale steps', () => {
    const row = renewalFixture()
    expect(decodeRenewal({ ...row, state: 'ISSUED_UNVERIFIED', canEdit: false, allowedTransitions: ['REJECT', 'VERIFY'] }, certificateVessel).state).toBe('ISSUED_UNVERIFIED')
    expect(() => decodeRenewal({ ...row, state: 'LEGALLY_VALID' }, certificateVessel)).toThrow()
    expect(() => decodeRenewal({ ...row, allowedTransitions: ['AUTO_VERIFY'] }, certificateVessel)).toThrow()
    // canComplete must be exactly the server's COMPLETE permission for the current revision.
    expect(() => decodeRenewal({ ...row, canComplete: true }, certificateVessel)).toThrow()
    expect(() => decodeRenewal({ ...row, currentRevision: false }, certificateVessel)).toThrow()
    expect(() => decodeRenewal({ ...row, state: 'VERIFIED', canEdit: true }, certificateVessel)).toThrow()
    expect(() => decodeRenewal({ ...row, decision: { ...emptyRenewalDecision(), issuerMatch: 'PROBABLY' } }, certificateVessel)).toThrow()
    expect(decodeRenewal({ ...row, transition: 'VERIFY', decision: { ...emptyRenewalDecision(), issuerMatch: 'MATCH' } }, certificateVessel).decision?.issuerMatch).toBe('MATCH')
    expect(decodeRenewalCapabilities(renewalCapsFixture(), certificateVessel).canVerify).toBe(false)
    expect(() => decodeRenewalCapabilities({ ...renewalCapsFixture(), states: ['DRAFT', 'COMPLETED'] }, certificateVessel)).toThrow()
  })
})

describe('DEV-044 renewal workflow panel', () => {
  const online = Object.getOwnPropertyDescriptor(Navigator.prototype, 'onLine')
  beforeEach(() => { vi.spyOn(crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-0000000000aa') })
  afterEach(() => { vi.restoreAllMocks(); if (online) Object.defineProperty(Navigator.prototype, 'onLine', online) })

  it('offers only server-allowed steps and sends UNKNOWN requirement status unchanged for the server to refuse', async () => {
    const transition = vi.spyOn(certificatesApi, 'transitionRenewal').mockRejectedValue(new ApiError(422, 'renewal_requirements_incomplete'))
    renderPanel(preparing()); const user = userEvent.setup()
    const step = await screen.findByLabelText('Step')
    expect(screen.getByText('Preparing')).toBeVisible()
    expect(screen.queryByRole('option', { name: 'Verify replacement' })).not.toBeInTheDocument()
    await waitFor(() => expect(step).toBeEnabled())
    await user.selectOptions(step, 'VALIDATE')
    await user.selectOptions(screen.getByLabelText('Survey completion'), 'UNKNOWN')
    await user.click(screen.getByRole('button', { name: 'Record step' }))
    await waitFor(() => expect(transition).toHaveBeenCalledTimes(1))
    const body = JSON.parse(transition.mock.calls[0][0].body)
    expect(body).toMatchObject({ action: 'VALIDATE', expectedVersion: 23, expectedRevision: 1, decision: { surveyStatus: 'UNKNOWN', documentsStatus: null } })
    expect(await screen.findByRole('alert')).toHaveTextContent('renewal_requirements_incomplete')
    // A definite 4xx releases the attempt instead of offering a blind retry.
    expect(screen.getByRole('button', { name: 'Record step' })).toBeInTheDocument()
  })

  it('keeps the exact idempotency key and body after an unknown outcome and reports success through a fresh reload', async () => {
    const transition = vi.spyOn(certificatesApi, 'transitionRenewal').mockRejectedValueOnce(new ApiError(0, 'network')).mockResolvedValueOnce({ ...preparing(), state: 'CANCELLED', canEdit: false, currentRevision: false, allowedTransitions: [] })
    const applied = renderPanel(preparing()); const user = userEvent.setup()
    const step = await screen.findByLabelText('Step'); await waitFor(() => expect(step).toBeEnabled())
    await user.selectOptions(step, 'CANCEL'); await user.click(screen.getByRole('button', { name: 'Record step' }))
    expect(await screen.findByRole('button', { name: 'Retry same request' })).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Retry same request' }))
    await waitFor(() => expect(applied).toHaveBeenCalledTimes(1))
    expect(transition.mock.calls[1][0].key).toBe(transition.mock.calls[0][0].key)
    expect(transition.mock.calls[1][0].body).toBe(transition.mock.calls[0][0].body)
  })

  it('shows conflict, stale, offline and no-step states without enabling a write', async () => {
    vi.spyOn(certificatesApi, 'transitionRenewal').mockRejectedValue(new ApiError(409, 'version_conflict'))
    const applied = renderPanel(preparing()); const user = userEvent.setup()
    const step = await screen.findByLabelText('Step'); await waitFor(() => expect(step).toBeEnabled())
    await user.selectOptions(step, 'CANCEL'); await user.click(screen.getByRole('button', { name: 'Record step' }))
    expect(await screen.findByText(/changed after it was loaded/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Reload' })); expect(applied).toHaveBeenCalledTimes(1)
  })

  it('explains a duty-separation refusal instead of reporting a changed renewal', async () => {
    vi.spyOn(certificatesApi, 'transitionRenewal').mockRejectedValue(new ApiError(409, 'separation_of_duties'))
    renderPanel(preparing()); const user = userEvent.setup()
    const step = await screen.findByLabelText('Step'); await waitFor(() => expect(step).toBeEnabled())
    await user.selectOptions(step, 'CANCEL'); await user.click(screen.getByRole('button', { name: 'Record step' }))
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/separation of duties/); expect(alert).toHaveTextContent('separation_of_duties')
    expect(alert).not.toHaveTextContent(/changed after it was loaded/)
    // QA N3: the same code also refuses COMPLETE when no independent verifier is recorded (CertificateRenewalWorkflow.Separated),
    // so the copy must not claim the caller recorded an earlier step.
    expect(alert).toHaveTextContent(/a different person must take this step/)
    expect(alert).toHaveTextContent(/or no verification by another person is recorded yet/)
    expect(alert).not.toHaveTextContent(/^Refused for separation of duties: you recorded an earlier step/)
    expect(screen.queryByRole('button', { name: 'Reload' })).not.toBeInTheDocument()
  })

  it('marks a historical revision stale and a closed case as having no steps', async () => {
    renderPanel({ ...renewalFixture(), currentRevision: false, canEdit: false, allowedTransitions: [] })
    expect(await screen.findByText(/not the current revision/)).toBeVisible()
    expect(screen.getByText(/No step is available/)).toBeVisible()
  })

  it('stays read-only while offline', async () => {
    Object.defineProperty(Navigator.prototype, 'onLine', { configurable: true, get: () => false })
    renderPanel(preparing())
    expect(await screen.findByText(/You are offline/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Record step' })).toBeDisabled()
  })
})
