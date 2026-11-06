import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { DefectEquipmentLaunch } from './DefectEquipmentLaunch'
import { defectApi } from './defect-api'
import type { DefectCapabilities } from './defect-api'
import type { Session } from './directory-contract'

vi.mock('./defect-api', () => ({ defectApi: { capabilities: vi.fn() } }))
const vessel = '10000000-0000-0000-0000-000000000001', asset = '20000000-0000-0000-0000-000000000001'
const owner: Session = { authenticated: true, canRead: true, actorId: asset, tenantId: vessel, tenantName: 'FICTIONAL workspace',
  csrfToken: 'synthetic-only', setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false }
const props = { session: owner, vessel, assetId: asset, revision: 3, language: 'en' as const, disabled: false, onNavigate: vi.fn() }
// Only CanWrite is consumed here; full transport decoding is covered by the
// owning adapter cases. A component mock conveys no server authorization.
const caps = (canWrite: boolean) => ({ canWrite } as DefectCapabilities)
beforeEach(() => { vi.resetAllMocks() })
afterEach(cleanup)

it('requires the actual target write capability and sends only source identity and revision', async () => {
  vi.mocked(defectApi.capabilities).mockResolvedValue(caps(true))
  render(<DefectEquipmentLaunch {...props} />)
  await userEvent.click(await screen.findByRole('button', { name: 'Open linked defect draft' }))
  expect(props.onNavigate).toHaveBeenCalledWith(`/app/defects/${vessel}?assetId=${asset}&assetRevision=3`)
  expect(props.onNavigate.mock.calls[0][0]).not.toMatch(/csrf|tenant|actor|criticality|safeguard|name|hash/i)
})

it('does not infer defect write authority from the source equipment workspace', async () => {
  vi.mocked(defectApi.capabilities).mockResolvedValue(caps(false))
  render(<DefectEquipmentLaunch {...props} />)
  await act(async () => { await Promise.resolve() })
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
  expect(props.onNavigate).not.toHaveBeenCalled()
})

it('respects an unresolved parent form and cannot leak an old actor capability into a new session', async () => {
  let resolve!: (value: DefectCapabilities) => void
  vi.mocked(defectApi.capabilities).mockImplementationOnce(() => new Promise(done => { resolve = done })).mockResolvedValue(caps(false))
  const view = render(<DefectEquipmentLaunch {...props} />)
  view.rerender(<DefectEquipmentLaunch {...props} session={{ ...owner, actorId: vessel }} />)
  await act(async () => { resolve(caps(true)); await Promise.resolve() })
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
  vi.mocked(defectApi.capabilities).mockResolvedValue(caps(true))
  view.rerender(<DefectEquipmentLaunch {...props} session={{ ...owner }} disabled />)
  expect(await screen.findByRole('button', { name: 'Open linked defect draft' })).toBeDisabled()
  expect(props.onNavigate).not.toHaveBeenCalled()
})
