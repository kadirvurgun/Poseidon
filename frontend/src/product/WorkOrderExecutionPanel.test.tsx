import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { WorkOrderExecutionPanel } from './WorkOrderExecutionPanel'
import { ApiError } from './api'
import { workOrderExecutionApi } from './work-order-execution-api'
import { criticalSpareApi } from './critical-spares-api'
import { executionActor, executionFixture, executionTenant, executionVessel, executionViewFixture, executionWorkOrder, sparePageFixture } from './work-order-execution-test-fixtures'

vi.mock('./CertificateDocumentPicker', () => ({ CertificateDocumentPicker: () => null }))
vi.mock('./api', async original => ({ ...(await original<typeof import('./api')>()),
  session: vi.fn(async () => ({ authenticated: true, canRead: true, tenantId: executionTenant, actorId: executionActor, csrfToken: 'csrf' })) }))

const renderPanel = () => render(<WorkOrderExecutionPanel vessel={executionVessel} workOrder={executionWorkOrder} language="en"/>)

describe('DEV-054 work order execution panel', () => {
  beforeEach(() => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
    vi.spyOn(crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-0000000000aa')
    vi.spyOn(criticalSpareApi, 'list').mockResolvedValue(sparePageFixture())
  })
  afterEach(() => vi.restoreAllMocks())

  it('shows a missing required-evidence gate, saves evidence with the version fence and disables submit until saved', async () => {
    vi.spyOn(workOrderExecutionApi, 'view').mockResolvedValue(executionViewFixture())
    const send = vi.spyOn(workOrderExecutionApi, 'send').mockResolvedValue(executionFixture({ revision: 3, currentRevision: false }))
    renderPanel(); const user = userEvent.setup()
    expect(await screen.findByText('Required evidence missing', { selector: '[data-gate]' })).toBeVisible()
    expect(screen.getByText('You executed this work, so you cannot verify or close it.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Submit evidence' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Add reading' }))
    await user.type(screen.getByLabelText('Reading'), 'TEMP')
    await user.type(screen.getByLabelText('Value'), '61.5')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save evidence' })).toBeEnabled())
    expect(screen.getByRole('button', { name: 'Submit evidence' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Save evidence' }))
    await waitFor(() => expect(send).toHaveBeenCalledTimes(1))
    const command = send.mock.calls[0][0]
    expect(command.kind).toBe('evidence')
    expect(JSON.parse(command.body)).toMatchObject({ expectedVersion: 12, expectedRevision: 2, evidence: { readings: [{ code: 'TEMP', value: '61.5' }] } })
  })

  it('offers verification only to a non-executor and keeps the same key after an unknown outcome', async () => {
    const submitted = executionFixture({ state: 'EVIDENCE_SUBMITTED', executedByCurrentActor: false, submittedAt: '2026-11-20T10:00:00Z',
      evidence: { readings: [{ code: 'TEMP', value: '61.5', unit: null, takenAt: null, note: null }], spares: [], files: [], workDone: null, findings: null },
      gate: { state: 'PRESENT_PENDING_HUMAN_VERIFICATION', required: ['READING'], present: ['READING'], missing: [] } })
    vi.spyOn(workOrderExecutionApi, 'view').mockResolvedValue(executionViewFixture(submitted, { canVerify: true, canExecute: false }))
    const send = vi.spyOn(workOrderExecutionApi, 'send').mockRejectedValueOnce(new ApiError(503, 'command_outcome_withheld')).mockResolvedValueOnce(executionFixture({ state: 'VERIFIED', currentRevision: false, verifiedAt: '2026-11-20T11:00:00Z' }))
    renderPanel(); const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Record verification' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Outcome unknown')
    await user.click(screen.getByRole('button', { name: 'Retry same command' }))
    await waitFor(() => expect(send).toHaveBeenCalledTimes(2))
    expect(send.mock.calls[1][0].key).toBe(send.mock.calls[0][0].key)
    expect(send.mock.calls[1][0].body).toBe(send.mock.calls[0][0].body)
    expect(JSON.parse(send.mock.calls[0][0].body)).toMatchObject({ action: 'VERIFY', expectedEvidenceHash: 'A'.repeat(64) })
    // Returning to the executor needs an explicit reason.
    expect(screen.getByRole('button', { name: 'Return to executor' })).toBeDisabled()
  })

  it('releases with an unconfigured policy disclosed as review required, never as nothing required', async () => {
    vi.spyOn(workOrderExecutionApi, 'view').mockResolvedValue(executionViewFixture(null, { canRelease: true }))
    const send = vi.spyOn(workOrderExecutionApi, 'send').mockResolvedValue(executionFixture({ state: 'RELEASED', currentRevision: false, requiredEvidence: null,
      gate: { state: 'REVIEW_REQUIRED', required: [], present: [], missing: [] }, transition: 'RELEASE' }))
    renderPanel(); const user = userEvent.setup()
    expect(await screen.findByText('Not released for execution.')).toBeVisible()
    expect(screen.getByLabelText('Not configured (review required)')).toBeChecked()
    await user.click(screen.getByLabelText('Declare required evidence'))
    expect(screen.getByRole('button', { name: 'Release work order' })).toBeDisabled()
    await user.click(screen.getByLabelText('Photo'))
    await user.click(screen.getByRole('button', { name: 'Release work order' }))
    await waitFor(() => expect(send).toHaveBeenCalledTimes(1))
    expect(JSON.parse(send.mock.calls[0][0].body)).toMatchObject({ action: 'RELEASE', requiredEvidence: ['PHOTO'], workOrderRevision: 1, workOrderVersion: 5 })
  })

  it('renders forbidden, offline-unavailable and offline-without-data states without inventing data', async () => {
    vi.spyOn(workOrderExecutionApi, 'view').mockRejectedValue(new ApiError(403, 'permission_denied'))
    const { unmount } = renderPanel()
    expect(await screen.findByText('You do not have permission to view this execution.')).toBeVisible()
    unmount()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    renderPanel()
    expect(screen.getByText('Offline. No execution data is loaded on this device.')).toBeVisible()
  })
})
