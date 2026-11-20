import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RequisitionsView, SparesView, readSparesLocation } from './CriticalSparesWorkspace'
import { sparesCopy } from './CriticalSparesWorkspace'
import { criticalSpareApi } from './critical-spares-api'
import { executionActor, executionTenant, executionVessel, requisitionFixture, spareFixture, sparePageFixture } from './work-order-execution-test-fixtures'

vi.mock('./api', async original => ({ ...(await original<typeof import('./api')>()),
  session: vi.fn(async () => ({ authenticated: true, canRead: true, tenantId: executionTenant, actorId: executionActor, csrfToken: 'csrf' })) }))

describe('DEV-110 critical spares and requisitions', () => {
  beforeEach(() => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); vi.spyOn(crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-0000000000aa') })
  afterEach(() => vi.restoreAllMocks())
  const t = sparesCopy.en

  it('parses only closed routes', () => {
    expect(readSparesLocation('/app/critical-spares/' + executionVessel, '?tab=requisitions')).toEqual({ vessel: executionVessel, tab: 'requisitions', invalid: false })
    expect(readSparesLocation('/app/critical-spares', '?tab=orders').invalid).toBe(true)
    expect(readSparesLocation('/app/critical-spares/not-a-guid', '').invalid).toBe(true)
  })

  it('alerts low stock, never shows unknown stock as healthy and proposes a requisition with the exact spare revision', async () => {
    const unknown = spareFixture({ id: '44444444-4444-4444-8444-444444444445', count: null, stock: { ...spareFixture().stock, state: 'STOCK_UNKNOWN', counted: null, countedAt: null, consumedSinceCount: null, projected: null, proposedQuantity: null } })
    vi.spyOn(criticalSpareApi, 'list').mockResolvedValue(sparePageFixture([spareFixture(), unknown]))
    const send = vi.spyOn(criticalSpareApi, 'send').mockResolvedValue({})
    render(<SparesView vessel={executionVessel} online t={t}/>); const user = userEvent.setup()
    expect(await screen.findByText('Low stock: 1')).toBeVisible()
    expect(screen.getByText('Stock unknown', { selector: '[data-state]' })).toBeVisible()
    expect(screen.queryByText('Above minimum (declared)', { selector: '[data-state]' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Propose requisition' })).toHaveLength(1)
    await user.click(screen.getByRole('button', { name: 'Propose requisition' }))
    await waitFor(() => expect(send).toHaveBeenCalledTimes(1))
    expect(JSON.parse(send.mock.calls[0][0].body)).toMatchObject({ spareId: spareFixture().id, spareRevision: 1 })
  })

  it('lets only a different human decide and requires a reason for rejection', async () => {
    vi.spyOn(criticalSpareApi, 'requisitions').mockResolvedValue({ items: [requisitionFixture(), requisitionFixture({ id: '55555555-5555-4555-8555-555555555556', proposedByCurrentActor: true })], offset: 0, limit: 25, hasMore: false })
    const send = vi.spyOn(criticalSpareApi, 'send').mockResolvedValue({})
    render(<RequisitionsView vessel={executionVessel} online t={t}/>); const user = userEvent.setup()
    expect(await screen.findByText('You proposed this; another person must decide.')).toBeVisible()
    expect(screen.getAllByRole('button', { name: 'Approve for ERP export' })).toHaveLength(1)
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Approve for ERP export' }))
    await waitFor(() => expect(send).toHaveBeenCalledTimes(1))
    expect(JSON.parse(send.mock.calls[0][0].body)).toMatchObject({ decision: 'APPROVE', approvedQuantity: null, reasonCode: null })
  })
})
