import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TugJobLifecycle } from './TugJobLifecycle'
import { emptyTugInputs, tugRequestSource, type TugRequest } from './tug-api'
import { changeAppLanguage } from '../i18n'

// DEV-081 / T-TUG-002 submit/cancel UI. Synthetic identities and FICTIONAL data only.
const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const actorId = '33333333-3333-3333-3333-333333333333', tenantId = '44444444-4444-4444-4444-444444444444'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const record = (patch: Partial<TugRequest> = {}): TugRequest => ({ id, vesselId: vessel, jobNumber: 'TUG-FICTIONAL', revision: 1, version: 8, inputs: emptyTugInputs(),
  provenance: 'UserDeclaredUnverified', savedAt: '2026-11-01T00:00:00Z', missing: [], replayed: false, sourceVersion: tugRequestSource, state: 'Draft', ...patch })
const context = { vessel: { id: vessel, code: 'FICTIONAL', name: 'Synthetic tug' }, mode: 'Tug', crossMode: false, roleReferences: null,
  roleLens: { roleState: 'Missing', roleCatalogVersion: null, roles: [], lensState: 'Missing', lensVersion: null, lenses: [] }, shipModeDestinations: [],
  canWrite: true, owners: [], jobTypes: [], priorities: [], requestSources: [], tugType: 'Unavailable', tugTypeContext: null, tugTypeOptions: [], tugTypePolicyVersion: null,
  bollardPull: { value: null, version: null, provenance: 'Missing', freshness: 'Unknown', observedAt: null, source: 'TugProfile' }, equipment: null, equipmentCoverage: 'Unavailable',
  readiness: 'Unknown', reason: 'VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable' }
const fetcher = vi.fn()
let lifecycle: (url: string, init: RequestInit) => Promise<Response>
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('tug-command')) return json(context)
    if (url.includes('/submit') || url.includes('/cancel')) return lifecycle(url, init!)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })
const props = { vessel, canWrite: true, offline: false, language: 'en' as const, owner: `${tenantId}/${actorId}`, onReload: vi.fn() }

it('shows server missing keys for an incomplete draft and keeps the record unchanged when submission is refused', async () => {
  lifecycle = async () => json({ code: 'tug_request_incomplete', missing: ['requested_start', 'request_owner'], correlationId: 'synthetic' }, 422)
  const onChanged = vi.fn(), user = userEvent.setup()
  render(<TugJobLifecycle {...props} record={record({ missing: ['requested_start'] })} onChanged={onChanged}/>)
  expect(screen.getByText('Draft')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Submit request' }))
  const alert = await screen.findByRole('alert')
  expect(alert).toHaveTextContent('Submission blocked: required data is missing')
  expect(alert).toHaveTextContent('Requested start'); expect(onChanged).not.toHaveBeenCalled()
  const sent = fetcher.mock.calls.find(([url]) => String(url).includes('/submit'))!
  expect(JSON.parse(sent[1].body)).toEqual({ expectedVersion: 8 })
  expect(sent[1].headers['X-CSRF-TOKEN']).toBe('synthetic-only')
})

it('submits with the exact version and reports the server state without claiming approval', async () => {
  lifecycle = async () => json({ ...record({ revision: 2, version: 9, state: 'Submitted' }) })
  const onChanged = vi.fn(), user = userEvent.setup()
  render(<TugJobLifecycle {...props} record={record()} onChanged={onChanged}/>)
  expect(screen.getByText(/not an approval, assignment or readiness decision/)).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Submit request' }))
  await waitFor(() => expect(onChanged).toHaveBeenCalledWith(expect.objectContaining({ state: 'Submitted', version: 9 })))
  expect(await screen.findByRole('status')).toHaveTextContent('Request submitted.')
})

it('retains the exact key and body after an uncertain outcome and retries the same command', async () => {
  let calls = 0
  lifecycle = async () => { calls++; if (calls === 1) throw new TypeError('network'); return json({ ...record({ revision: 2, version: 9, state: 'Submitted' }) }) }
  const onChanged = vi.fn(), onPending = vi.fn(), user = userEvent.setup()
  render(<TugJobLifecycle {...props} record={record()} onChanged={onChanged} onPending={onPending}/>)
  await user.click(screen.getByRole('button', { name: 'Submit request' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('The outcome is uncertain')
  expect(onPending).toHaveBeenLastCalledWith(true)
  await user.click(screen.getByRole('button', { name: 'Retry same command' }))
  await waitFor(() => expect(onChanged).toHaveBeenCalled())
  const sent = fetcher.mock.calls.filter(([url]) => String(url).includes('/submit'))
  expect(sent).toHaveLength(2)
  expect(sent[0][1].headers['Idempotency-Key']).toBe(sent[1][1].headers['Idempotency-Key'])
  expect(sent[0][1].body).toBe(sent[1][1].body)
})

it('requires a reason before cancelling and is keyboard operable', async () => {
  lifecycle = async (_url, init) => { expect(JSON.parse(init.body as string)).toEqual({ expectedVersion: 8, reason: 'FICTIONAL withdrawn' }); return json({ ...record({ revision: 2, version: 9, state: 'Cancelled' }) }) }
  const onChanged = vi.fn(), user = userEvent.setup()
  render(<TugJobLifecycle {...props} record={record()} onChanged={onChanged}/>)
  await user.click(screen.getByRole('button', { name: 'Cancel request' }))
  const confirm = screen.getByRole('button', { name: 'Confirm cancellation' })
  expect(confirm).toBeDisabled()
  await user.type(screen.getByLabelText('Cancellation reason'), 'FICTIONAL withdrawn')
  expect(confirm).toBeEnabled()
  confirm.focus(); await user.keyboard('{Enter}')
  await waitFor(() => expect(onChanged).toHaveBeenCalledWith(expect.objectContaining({ state: 'Cancelled' })))
})

it('hides commands for read-only, offline and cancelled records', () => {
  const { rerender } = render(<TugJobLifecycle {...props} canWrite={false} record={record()} onChanged={vi.fn()}/>)
  expect(screen.queryByRole('button', { name: 'Submit request' })).not.toBeInTheDocument()
  rerender(<TugJobLifecycle {...props} offline record={record()} onChanged={vi.fn()}/>)
  expect(screen.getByRole('button', { name: 'Submit request' })).toBeDisabled()
  expect(screen.getByText('Lifecycle commands need a connection.')).toBeInTheDocument()
  rerender(<TugJobLifecycle {...props} record={record({ state: 'Cancelled' })} onChanged={vi.fn()}/>)
  expect(screen.queryByRole('button', { name: 'Cancel request' })).not.toBeInTheDocument()
  expect(screen.getByText(/no longer a draft/)).toBeInTheDocument()
  rerender(<TugJobLifecycle {...props} record={record({ state: 'Submitted' })} onChanged={vi.fn()}/>)
  expect(screen.queryByRole('button', { name: 'Submit request' })).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Cancel request' })).toBeInTheDocument()
})

it('renders Turkish lifecycle labels from the catalog', async () => {
  await changeAppLanguage('tr', false)
  render(<TugJobLifecycle {...props} language="tr" record={record({ state: 'Submitted' })} onChanged={vi.fn()}/>)
  expect(screen.getByText('Gönderildi')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Talebi iptal et' })).toBeInTheDocument()
})
