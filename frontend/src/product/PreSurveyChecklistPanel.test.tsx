import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PreSurveyChecklistPanel } from './PreSurveyChecklistPanel'
import { ApiError } from './api'
import { surveyApi, VerificationRejected, type PreSurveyChecklist } from './survey-api'
import { checklistFixture, surveyActor, surveyCapsFixture, surveyItem, surveyOwnerRole, surveyVessel } from './survey-test-fixtures'

vi.mock('./CertificateDocumentPicker', () => ({ CertificateDocumentPicker: () => null }))
vi.mock('./api', async original => ({ ...(await original<typeof import('./api')>()),
  session: vi.fn(async () => ({ authenticated: true, canRead: true, tenantId: surveyVessel, actorId: surveyActor, csrfToken: 'csrf' })) }))

const renderPanel = (record: PreSurveyChecklist, caps = surveyCapsFixture(), onApplied = vi.fn()) => {
  render(<PreSurveyChecklistPanel vessel={surveyVessel} record={record} capabilities={caps} language="en" onDirty={() => undefined} onApplied={onApplied} onReload={() => undefined}/>)
  return onApplied
}

describe('DEV-104 pre-survey checklist panel', () => {
  beforeEach(() => { vi.spyOn(crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-0000000000aa') })
  afterEach(() => vi.restoreAllMocks())

  it('shows a blocked advisory state, never a ready one, and sends the full item set with the version fence', async () => {
    const update = vi.spyOn(surveyApi, 'update').mockRejectedValue(new ApiError(403, 'owner_unavailable'))
    renderPanel(checklistFixture()); const user = userEvent.setup()
    expect(screen.getByText('Blocked', { selector: '[data-readiness]' })).toBeVisible()
    expect(screen.queryByText(/^Ready$/, { selector: '[data-readiness]' })).not.toBeInTheDocument()
    const owner = screen.getByLabelText('Owner role CERTIFICATE_ORIGINALS_AVAILABLE')
    await waitFor(() => expect(owner).toBeEnabled())
    await user.selectOptions(owner, surveyOwnerRole)
    await user.selectOptions(screen.getByLabelText('Answer CERTIFICATE_ORIGINALS_AVAILABLE'), 'READY')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(update).toHaveBeenCalledTimes(1))
    const body = JSON.parse(update.mock.calls[0][0].body)
    expect(body).toMatchObject({ expectedVersion: 41, expectedRevision: 2, certificateRevision: 1, certificateVersion: 7 })
    expect(body.items).toEqual([expect.objectContaining({ ownerRoleId: surveyOwnerRole, answer: 'READY', evidence: [] })])
    expect(await screen.findByRole('alert')).toHaveTextContent('owner_unavailable')
    // NOT_APPLICABLE is not offered when the template item does not allow it.
    expect(screen.queryByRole('option', { name: 'Not applicable' })).not.toBeInTheDocument()
  })

  it('keeps the exact key and body after an unknown outcome and retries the same request', async () => {
    const update = vi.spyOn(surveyApi, 'update').mockRejectedValueOnce(new ApiError(503, 'command_outcome_withheld')).mockResolvedValueOnce(checklistFixture({ currentRevision: false }))
    const applied = renderPanel(checklistFixture()); const user = userEvent.setup()
    const answer = screen.getByLabelText('Answer CERTIFICATE_ORIGINALS_AVAILABLE')
    await waitFor(() => expect(answer).toBeEnabled())
    await user.selectOptions(answer, 'ATTENTION')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText(/outcome is not known/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Retry the same request' }))
    await waitFor(() => expect(applied).toHaveBeenCalledTimes(1))
    expect(update.mock.calls[1][0].key).toBe(update.mock.calls[0][0].key)
    expect(update.mock.calls[1][0].body).toBe(update.mock.calls[0][0].body)
  })

  it('never offers verification of the reviewer’s own answer and lists every refused item', async () => {
    const own = surveyItem('OWN_ANSWER', { answer: 'READY', answeredByCurrentActor: true, itemId: '77777777-7777-4777-8777-777777777777' })
    const other = surveyItem('OTHER_ANSWER_ITEM', { answer: 'READY', itemId: '88888888-8888-4888-8888-888888888888' })
    const record = checklistFixture({ canEdit: false, canVerify: true, items: [own, other], readiness: { ...checklistFixture().readiness, total: 2 } })
    const verify = vi.spyOn(surveyApi, 'verify').mockRejectedValue(new VerificationRejected([{ itemId: other.itemId, code: 'evidence_required' }]))
    renderPanel(record, surveyCapsFixture({ canWrite: false, canVerify: true })); const user = userEvent.setup()
    expect(screen.getByText(/another person must verify it/)).toBeVisible()
    expect(screen.queryByLabelText('Decision OWN_ANSWER')).not.toBeInTheDocument()
    const decision = screen.getByLabelText('Decision OTHER_ANSWER_ITEM')
    await waitFor(() => expect(decision).toBeEnabled())
    await user.selectOptions(decision, 'VERIFIED')
    await user.click(screen.getByRole('button', { name: 'Record verification' }))
    await waitFor(() => expect(verify).toHaveBeenCalledTimes(1))
    expect(JSON.parse(verify.mock.calls[0][0].body).decisions).toEqual([{ itemId: other.itemId, expectedItemHash: other.itemHash, decision: 'VERIFIED', reasonCode: null }])
    expect(await screen.findByText('Items not recorded')).toBeVisible()
    expect(screen.getByText('evidence_required')).toBeVisible()
  })
})
