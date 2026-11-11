import { act, cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { MaintenanceObservations } from './MaintenanceObservations'
import { ApiError, request, session } from './api'
vi.mock('./api', async () => ({ ...(await vi.importActual('./api')), request: vi.fn(), session: vi.fn() }))
const mocked = vi.mocked(request), sessions = vi.mocked(session)
const tenant = '11111111-1111-1111-1111-111111111111', actor = '22222222-2222-2222-2222-222222222222'
const vessel = '33333333-3333-3333-3333-333333333333', asset = '44444444-4444-4444-4444-444444444444', other = '55555555-5555-5555-5555-555555555555'
const props = { vessel, asset, owner: `${tenant}/${actor}`, en: true, onDirty: vi.fn(), onUncertain: vi.fn(), onBusy: vi.fn(), onAccessLost: vi.fn() }
const fixture = () => ({ sources: [{ tenantId: tenant, vesselId: vessel, assetId: asset, source: 'synthetic-meter', version: 'synthetic-v1', kind: 'Meter', unit: 'hours', freshnessSeconds: 3600, maxScale: 4 }], items: [], canWrite: true, state: 'ConfiguredDeclarationsOnly' })
let currentSession: Awaited<ReturnType<typeof session>>
const posts = () => mocked.mock.calls.filter(call => call[2]?.method === 'POST')
const reads = () => mocked.mock.calls.filter(call => call[2]?.method !== 'POST')
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done }); return { promise, resolve } }
beforeEach(() => {
  vi.clearAllMocks()
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  currentSession = { authenticated: true, tenantId: tenant, actorId: actor, csrfToken: 'synthetic-initial' } as Awaited<ReturnType<typeof session>>
  sessions.mockImplementation(async () => currentSession)
  mocked.mockImplementation(async (_path, _signal, init) => init?.method === 'POST' ? { id: JSON.parse(init.body as string).id, replayed: false } : fixture())
})
afterEach(() => cleanup())
async function fill() {
  fireEvent.change(await screen.findByLabelText('Configured source'), { target: { value: 'synthetic-meter' } })
  fireEvent.change(screen.getByLabelText('Source revision'), { target: { value: '17' } })
  fireEvent.change(screen.getByLabelText('Observed value (exact declaration)'), { target: { value: '19.5000' } })
  fireEvent.change(screen.getByLabelText('Observation instant (ISO with offset)'), { target: { value: '2026-11-01T10:00:00+03:00' } })
}
async function refreshAccess() {
  fireEvent.click(screen.getByRole('button', { name: 'Retry source read' }))
  await screen.findByLabelText('Configured source')
}
async function loseFirstResponse() {
  mocked.mockImplementation(async (_path, _signal, init) => { if (init?.method === 'POST') throw new TypeError('synthetic acknowledgement loss'); return fixture() })
  render(<MaintenanceObservations {...props} />); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await waitFor(() => expect(props.onUncertain).toHaveBeenLastCalledWith(true))
  await refreshAccess()
}

// MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01: AUTHORING, all execution deferred.
it('uses the configured declaration and fresh CSRF without changing exact lexical inputs', async () => {
  render(<MaintenanceObservations {...props} />); await fill()
  currentSession = { ...currentSession, csrfToken: 'synthetic-current-token' }
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await screen.findByText('Draft saved.')
  expect(posts()).toHaveLength(1)
  expect(JSON.parse(posts()[0][2]!.body as string)).toMatchObject({ value: '19.5000', sourceRevision: 17, policyVersion: 'synthetic-v1', observedAt: '2026-11-01T10:00:00+03:00' })
  expect(new Headers(posts()[0][2]!.headers).get('X-CSRF-TOKEN')).toBe('synthetic-current-token')
  expect(props.onUncertain).toHaveBeenLastCalledWith(false)
})
it('keeps missing policy unavailable instead of making an approved capture form', async () => {
  mocked.mockResolvedValue({ ...fixture(), sources: [], state: 'PolicyUnavailable' })
  render(<MaintenanceObservations {...props} />)
  expect(await screen.findByText('Source, unit and freshness policy unavailable.')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Record observation' })).not.toBeInTheDocument()
})
it.each([401, 403, 404, 400, 409])('retains an earlier unknown capture through later%s and retries original bytes after reauthorization', async status => {
  await loseFirstResponse()
  const original = posts()[0]
  mocked.mockImplementation(async (_path, _signal, init) => { if (init?.method === 'POST') throw new ApiError(status); return fixture() })
  fireEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }))
  await waitFor(() => expect(posts()).toHaveLength(2))
  await waitFor(() => expect(props.onBusy).toHaveBeenLastCalledWith(false))
  expect(props.onUncertain).toHaveBeenLastCalledWith(true)
  expect(screen.getByRole('button', { name: 'Discard and reload' })).toBeDisabled()
  currentSession = { ...currentSession, csrfToken: 'synthetic-reauthorized' }
  mocked.mockImplementation(async (_path, _signal, init) => init?.method === 'POST' ? { id: JSON.parse(init.body as string).id, replayed: true } : fixture())
  await refreshAccess()
  expect(posts()).toHaveLength(2)
  expect(screen.getByLabelText('Observed value (exact declaration)')).toHaveValue('19.5000')
  expect(screen.getByLabelText('Observed value (exact declaration)')).toBeDisabled()
  fireEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }))
  await screen.findByText('Draft saved.')
  expect(posts()).toHaveLength(3)
  for (const call of posts()) { expect(call[0]).toBe(original[0]); expect(call[2]!.body).toBe(original[2]!.body); expect(new Headers(call[2]!.headers).get('Idempotency-Key')).toBe(new Headers(original[2]!.headers).get('Idempotency-Key')) }
  expect(new Headers(posts()[2][2]!.headers).get('X-CSRF-TOKEN')).toBe('synthetic-reauthorized')
})
it.each(['actorId', 'tenantId'] as const)('does not transfer a retained draft to a replacement%s', async field => {
  const view = render(<MaintenanceObservations {...props} />); await fill()
  const original = currentSession
  currentSession = { ...currentSession, [field]: other }
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await screen.findByText(/Only the original account can continue/)
  expect(screen.queryByLabelText('Observed value (exact declaration)')).not.toBeInTheDocument()
  expect(posts()).toHaveLength(0)
  view.rerender(<MaintenanceObservations {...props} onDirty={vi.fn()} />)
  currentSession = { ...original, csrfToken: 'synthetic-new-token' }
  await refreshAccess()
  expect(screen.getByLabelText('Observed value (exact declaration)')).toHaveValue('19.5000')
  expect(posts()).toHaveLength(0)
})
it('keeps an unknown attempt when fresh session lookup fails before a retry POST', async () => {
  await loseFirstResponse()
  sessions.mockRejectedValue(new ApiError(503))
  fireEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }))
  await waitFor(() => expect(props.onBusy).toHaveBeenLastCalledWith(false))
  expect(posts()).toHaveLength(1)
  expect(props.onUncertain).toHaveBeenLastCalledWith(true)
  sessions.mockImplementation(async () => currentSession)
  await refreshAccess()
  expect(posts()).toHaveLength(1)
  expect(screen.getByRole('button', { name: 'Retry same attempt' })).toBeEnabled()
})
it('keeps drafts when callback identities and same-owner CSRF rotate, without resetting or sending', async () => {
  const view = render(<MaintenanceObservations {...props} />); await fill()
  const count = reads().length
  currentSession = { ...currentSession, csrfToken: 'synthetic-rotated' }
  view.rerender(<MaintenanceObservations {...props} onDirty={vi.fn()} onUncertain={vi.fn()} />)
  expect(reads()).toHaveLength(count)
  expect(screen.getByLabelText('Observed value (exact declaration)')).toHaveValue('19.5000')
  await refreshAccess()
  expect(screen.getByLabelText('Source revision')).toHaveValue('17')
  expect(posts()).toHaveLength(0)
})
it('issues no request or dirty callback on suspended mount, then reads fresh owner data on resume', async () => {
  const view = render(<MaintenanceObservations {...props} suspended />)
  expect(mocked).not.toHaveBeenCalled(); expect(sessions).not.toHaveBeenCalled(); expect(props.onDirty).not.toHaveBeenCalled()
  view.rerender(<MaintenanceObservations {...props} suspended={false} />)
  await fill()
  expect(reads()).toHaveLength(1); expect(posts()).toHaveLength(0)
})
it('withdraws private DOM on suspension and restores the exact unknown command only after fresh access and explicit retry', async () => {
  let count = 0
  mocked.mockImplementation(async (_path, _signal, init) => { if (init?.method === 'POST' && ++count === 1) throw new TypeError('synthetic loss'); return init?.method === 'POST' ? { id: JSON.parse(init.body as string).id, replayed: true } : fixture() })
  const view = render(<MaintenanceObservations {...props} />); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await waitFor(() => expect(props.onUncertain).toHaveBeenLastCalledWith(true))
  const original = posts()[0]
  view.rerender(<MaintenanceObservations {...props} suspended />)
  expect(screen.queryByText('synthetic-meter')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Retry same attempt' })).not.toBeInTheDocument()
  view.rerender(<MaintenanceObservations {...props} suspended={false} />)
  await screen.findByRole('button', { name: 'Retry same attempt' })
  expect(posts()).toHaveLength(1)
  fireEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }))
  await screen.findByText('Draft saved.')
  expect(posts()[1][2]!.body).toBe(original[2]!.body)
})
it('does not consume a late capture success after withdrawal, or silently resend it on resume', async () => {
  const response = deferred<unknown>()
  mocked.mockImplementation(async (_path, _signal, init) => init?.method === 'POST' ? response.promise : fixture())
  const view = render(<MaintenanceObservations {...props} />); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await waitFor(() => expect(posts()).toHaveLength(1))
  const id = JSON.parse(posts()[0][2]!.body as string).id
  view.rerender(<MaintenanceObservations {...props} suspended />)
  await act(async () => response.resolve({ id, replayed: false }))
  view.rerender(<MaintenanceObservations {...props} suspended={false} />)
  await screen.findByRole('button', { name: 'Retry same attempt' })
  expect(screen.queryByText('Draft saved.')).not.toBeInTheDocument()
  expect(posts()).toHaveLength(1)
})
it('fences a successful capture returned after the session was replaced', async () => {
  mocked.mockImplementation(async (_path, _signal, init) => {
    if (init?.method === 'POST') { currentSession = { ...currentSession, actorId: other }; return { id: JSON.parse(init.body as string).id, replayed: false } }
    return fixture()
  })
  render(<MaintenanceObservations {...props} />); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await screen.findByText(/Only the original account can continue/)
  expect(screen.queryByText('Draft saved.')).not.toBeInTheDocument()
  expect(screen.queryByLabelText('Observed value (exact declaration)')).not.toBeInTheDocument()
  expect(props.onUncertain).toHaveBeenLastCalledWith(true)
})
it('does not recapture after confirmed success followed by failed optional readback', async () => {
  let confirmed = false
  mocked.mockImplementation(async (_path, _signal, init) => {
    if (init?.method === 'POST') { confirmed = true; return { id: JSON.parse(init.body as string).id, replayed: false } }
    if (confirmed) throw new ApiError(503)
    return fixture()
  })
  render(<MaintenanceObservations {...props} />); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await screen.findByText('Draft saved.')
  expect(screen.queryByRole('button', { name: 'Record observation' })).not.toBeInTheDocument()
  expect(props.onUncertain).toHaveBeenLastCalledWith(false)
  mocked.mockResolvedValue(fixture())
  await refreshAccess()
  expect(screen.getByRole('button', { name: 'Record observation' })).toBeDisabled()
  expect(posts()).toHaveLength(1)
})
it('locks a success for the wrong observation UUID as unknown instead of confirming it', async () => {
  mocked.mockImplementation(async (_path, _signal, init) => init?.method === 'POST' ? { id: other, replayed: false } : fixture())
  render(<MaintenanceObservations {...props} />); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await waitFor(() => expect(props.onUncertain).toHaveBeenLastCalledWith(true))
  expect(screen.queryByText('Draft saved.')).not.toBeInTheDocument()
  await refreshAccess()
  expect(screen.getByRole('button', { name: 'Retry same attempt' })).toBeEnabled()
})
it('retains unknown revocation target/key/body through denial and never changes it to a capture', async () => {
  const row = { id: other, source: 'synthetic-meter', policyVersion: 'synthetic-v1', sourceRevision: 1, kind: 'Meter', unit: 'hours', value: '19.5000', observedAt: '2026-11-01T07:00:00Z', provenance: 'OperatorDeclared', revoked: false }
  let count = 0
  mocked.mockImplementation(async (_path, _signal, init) => {
    if (init?.method !== 'POST') return { ...fixture(), items: [row] }
    if (++count === 1) throw new TypeError('synthetic revoke acknowledgement loss')
    if (count === 2) throw new ApiError(404)
    return { id: other, state: 'Revoked' }
  })
  render(<MaintenanceObservations {...props} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Revoke observation' }))
  await waitFor(() => expect(props.onUncertain).toHaveBeenLastCalledWith(true))
  await refreshAccess(); fireEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }))
  await waitFor(() => expect(props.onBusy).toHaveBeenLastCalledWith(false))
  await refreshAccess(); expect(posts()).toHaveLength(2)
  fireEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }))
  await screen.findByText('Draft saved.')
  expect(posts()).toHaveLength(3)
  for (const call of posts()) { expect(call[0]).toBe(`equipment-hierarchy/${vessel}/${asset}/observations/${other}/revoke`); expect(call[2]!.body).toBe('{}'); expect(new Headers(call[2]!.headers).get('Idempotency-Key')).toBe(new Headers(posts()[0][2]!.headers).get('Idempotency-Key')) }
})
it('does not adopt retained fields when an existing component is given another owner or asset scope', async () => {
  const view = render(<MaintenanceObservations {...props} />); await fill()
  view.rerender(<MaintenanceObservations {...props} owner={`${tenant}/${other}`} asset={other} />)
  expect(screen.queryByLabelText('Source revision')).not.toBeInTheDocument()
  expect(posts()).toHaveLength(0)
  view.rerender(<MaintenanceObservations {...props} />)
  expect(await screen.findByLabelText('Source revision')).toHaveValue('17')
})
it('ignores a delayed response after unmount without notifying or publishing a confirmation', async () => {
  const response = deferred<unknown>()
  mocked.mockImplementation(async (_path, _signal, init) => init?.method === 'POST' ? response.promise : fixture())
  const view = render(<MaintenanceObservations {...props} />); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await waitFor(() => expect(posts()).toHaveLength(1))
  const count = props.onDirty.mock.calls.length, id = JSON.parse(posts()[0][2]!.body as string).id
  view.unmount(); await act(async () => response.resolve({ id, replayed: false }))
  expect(props.onDirty).toHaveBeenCalledTimes(count)
  expect(posts()).toHaveLength(1)
})
