import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { beforeEach, expect, it, vi } from 'vitest'
import { MaintenanceObservations } from './MaintenanceObservations'
import { ApiError, request } from './api'
vi.mock('./api', async () => ({ ...(await vi.importActual('./api')), request: vi.fn() }))
const mocked = vi.mocked(request)
const value = { sources: [{ source: 'synthetic-meter', version: 'synthetic-v1', kind: 'Meter', unit: 'hours', freshnessSeconds: 3600, maxScale: 4 }], items: [], canWrite: true, state: 'ConfiguredDeclarationsOnly' }
const props = { vessel: 'vessel', asset: 'asset', csrf: 'synthetic-csrf', en: true, onDirty: vi.fn(), onUncertain: vi.fn() }
beforeEach(() => { vi.clearAllMocks(); mocked.mockResolvedValue(value) })
it('records an explicit exact value/time and selected versioned source without fabricated observations', async () => {
  render(<MaintenanceObservations {...props} />)
  fireEvent.change(await screen.findByLabelText('Configured source'), { target: { value: 'synthetic-meter' } })
  fireEvent.change(screen.getByLabelText('Source revision'), { target: { value: '1' } })
  fireEvent.change(screen.getByLabelText('Observed value (exact declaration)'), { target: { value: '19.5000' } })
  fireEvent.change(screen.getByLabelText('Observation instant (ISO with offset)'), { target: { value: '2026-11-01T10:00:00+03:00' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  await waitFor(() => expect(mocked.mock.calls.some(c => c[2]?.method === 'POST')).toBe(true))
  const posted = JSON.parse(mocked.mock.calls.find(c => c[2]?.method === 'POST')![2]!.body as string)
  expect(posted).toMatchObject({ value: '19.5000', sourceRevision: 1, policyVersion: 'synthetic-v1', observedAt: '2026-11-01T10:00:00+03:00' })
})
it('locks acknowledgement loss and retries the same immutable UUID/body', async () => {
  mocked.mockImplementation(async (_path, _signal, init) => { if (init?.method === 'POST') throw new Error('lost acknowledgement'); return value })
  render(<MaintenanceObservations {...props} />)
  fireEvent.change(await screen.findByLabelText('Configured source'), { target: { value: 'synthetic-meter' } })
  fireEvent.change(screen.getByLabelText('Source revision'), { target: { value: '1' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Retry same attempt' }))
  await waitFor(() => expect(mocked.mock.calls.filter(c => c[2]?.method === 'POST')).toHaveLength(2))
  const posts = mocked.mock.calls.filter(c => c[2]?.method === 'POST')
  expect(posts[0][2]).toEqual(posts[1][2]); expect(props.onUncertain).toHaveBeenCalledWith(true)
  expect(screen.getByRole('button', { name: 'Discard and reload' })).toBeDisabled()
})
it('distinguishes missing policy and leaves no capture form available', async () => {
  mocked.mockResolvedValue({ ...value, sources: [] })
  render(<MaintenanceObservations {...props} />)
  expect(await screen.findByText('Source, unit and freshness policy unavailable.')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Record observation' })).not.toBeInTheDocument()
})
it('clears the scoped observation projection on current permission denial', async () => {
  mocked.mockImplementation(async (_path, _signal, init) => { if (init?.method === 'POST') throw new ApiError(403); return value })
  render(<MaintenanceObservations {...props} />)
  fireEvent.change(await screen.findByLabelText('Configured source'), { target: { value: 'synthetic-meter' } })
  fireEvent.change(screen.getByLabelText('Source revision'), { target: { value: '1' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record observation' }))
  expect(await screen.findByText('Observation access unavailable.')).toBeInTheDocument()
  expect(screen.queryByLabelText('Observed value (exact declaration)')).not.toBeInTheDocument()
})
