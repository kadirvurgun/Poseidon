import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { WorkOrderPlanLaunch } from './WorkOrderPlanLaunch'
import { workOrderApi, type WorkOrderCapabilities } from './work-order-api'
import type { Session } from './directory-contract'

vi.mock('./work-order-api', () => ({ workOrderApi: { capabilities: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks() })
const vessel = '11111111-1111-1111-1111-111111111111', plan = '22222222-2222-2222-2222-222222222222'
const session: Session = { authenticated: true, canRead: true, actorId: plan, tenantId: vessel, tenantName: 'FICTIONAL workspace',
  csrfToken: 'synthetic-only', setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false }
const props = { session, vessel, planId: plan, revision: 4, language: 'en' as const, disabled: false, onNavigate: vi.fn() }
const capability = (canWrite: boolean) => ({ canWrite } as WorkOrderCapabilities)

it('opens only the authorized linked draft and sends no private plan values or approval state', async () => {
  vi.mocked(workOrderApi.capabilities).mockResolvedValue(capability(true))
  render(<WorkOrderPlanLaunch {...props} />)
  await userEvent.click(await screen.findByRole('button', { name: 'Open linked work order draft' }))
  expect(props.onNavigate).toHaveBeenCalledWith(`/app/work-order-center/${vessel}?planId=${plan}&planRevision=4`)
})

it('does not infer a target grant from maintenance access and respects the owning pending form', async () => {
  vi.mocked(workOrderApi.capabilities).mockResolvedValue(capability(false))
  const view = render(<WorkOrderPlanLaunch {...props} />)
  await act(async () => { await Promise.resolve() })
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
  vi.mocked(workOrderApi.capabilities).mockResolvedValue(capability(true))
  view.rerender(<WorkOrderPlanLaunch {...props} session={{ ...session }} disabled />)
  expect(await screen.findByRole('button', { name: 'Open linked work order draft' })).toBeDisabled()
  expect(props.onNavigate).not.toHaveBeenCalled()
})
