import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { DowntimeSourceLaunch } from './DowntimeSourceLaunch'
import { downtimeApi, type DowntimeCapabilities } from './downtime-api'
import type { Session } from './directory-contract'

vi.mock('./downtime-api', () => ({ downtimeApi: { capabilities: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks() })
const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const session: Session = { authenticated: true, canRead: true, actorId: id, tenantId: vessel, tenantName: 'FICTIONAL workspace',
  csrfToken: 'synthetic-only', setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false }
const props = { session, vessel, kind: 'Defect' as const, sourceId: id, revision: 4, language: 'en' as const, disabled: false, onNavigate: vi.fn() }
const caps = (canWrite: boolean, originKinds: string[]) => ({ canWrite, originKinds } as DowntimeCapabilities)

it.each(['Defect', 'WorkOrder'] as const)('requires the target and exact %s origin grants and passes only saved metadata', async kind => {
  vi.mocked(downtimeApi.capabilities).mockResolvedValue(caps(true, [kind]))
  render(<DowntimeSourceLaunch {...props} kind={kind} />)
  await userEvent.click(await screen.findByRole('button', { name: 'Open linked downtime draft' }))
  expect(props.onNavigate).toHaveBeenCalledWith(`/app/operational-downtime/${vessel}?originKind=${kind}&originId=${id}&originRevision=4`)
})

it.each([[false, ['Defect']], [true, ['Engine']]] as const)('does not derive source adoption authority from target write alone %#', async (canWrite, kinds) => {
  vi.mocked(downtimeApi.capabilities).mockResolvedValue(caps(canWrite, [...kinds]))
  render(<DowntimeSourceLaunch {...props} />)
  await act(async () => { await Promise.resolve() })
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
})

it('keeps a pending parent form blocked', async () => {
  vi.mocked(downtimeApi.capabilities).mockResolvedValue(caps(true, ['Defect']))
  render(<DowntimeSourceLaunch {...props} disabled />)
  expect(await screen.findByRole('button', { name: 'Open linked downtime draft' })).toBeDisabled()
  expect(props.onNavigate).not.toHaveBeenCalled()
})
