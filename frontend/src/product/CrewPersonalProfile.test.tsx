import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { beforeEach, expect, it, vi } from 'vitest'
import { CrewPersonalProfile, type PersonalProfile } from './CrewPersonalProfile'
import { ApiError, request } from './api'
vi.mock('./api', async () => ({ ...(await vi.importActual('./api')), request: vi.fn() }))
const read = vi.mocked(request)
const fixture: PersonalProfile = { version: 12, revision: 1, policyVersion: 'synthetic-v1', purpose: 'crew-profile', storageState: 'Protected',
  data: { passportNo: 'SYNTHETIC-KEEP', email: 'synthetic@example.invalid', seaServiceMonths: '123456789012345678901234567890.0001' },
  readable: [2, 7, 14], writable: [7, 14], options: [] }
const props = { vessel: 'vessel', assignment: 'assignment', language: 'en' as const, csrf: 'synthetic-csrf', onDirty: vi.fn() }
beforeEach(() => { vi.clearAllMocks(); read.mockResolvedValue(fixture) })
it('renders only current authorized fields and keeps exact numeric declarations as text', async () => {
  render(<CrewPersonalProfile {...props} />)
  expect(await screen.findByLabelText('Sea service months')).toHaveValue(fixture.data.seaServiceMonths)
  expect(screen.getByLabelText('Passport number')).toHaveAttribute('readonly')
  expect(screen.queryByLabelText('Contact phone')).not.toBeInTheDocument()
})
it('patches only explicitly changed fields, preserving all unmapped operator values', async () => {
  render(<CrewPersonalProfile {...props} />)
  fireEvent.change(await screen.findByLabelText('Email'), { target: { value: 'changed@example.invalid' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save declaration' }))
  await waitFor(() => expect(read.mock.calls.some(call => call[2]?.method === 'PUT')).toBe(true))
  const call = read.mock.calls.find(value => value[2]?.method === 'PUT')!
  expect(JSON.parse(call[2]!.body as string)).toEqual({ baseVersion: 12, policyVersion: 'synthetic-v1', purpose: 'crew-profile', fields: [14], data: { email: 'changed@example.invalid' } })
})
it('freezes the exact semantic attempt and UUID after lost acknowledgement', async () => {
  read.mockImplementation(async (_path, _signal, init) => { if (init?.method === 'PUT') throw new Error('response lost'); return fixture })
  render(<CrewPersonalProfile {...props} />)
  fireEvent.change(await screen.findByLabelText('Email'), { target: { value: 'changed@example.invalid' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save declaration' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Retry same attempt' }))
  await waitFor(() => expect(read.mock.calls.filter(call => call[2]?.method === 'PUT')).toHaveLength(2))
  const calls = read.mock.calls.filter(call => call[2]?.method === 'PUT')
  expect(calls[0][2]).toEqual(calls[1][2])
  expect(screen.getByLabelText('Email')).toBeDisabled()
})
it('removes private projections when the current mutation is denied', async () => {
  read.mockImplementation(async (_path, _signal, init) => { if (init?.method === 'PUT') throw new ApiError(403); return fixture })
  render(<CrewPersonalProfile {...props} />)
  fireEvent.change(await screen.findByLabelText('Email'), { target: { value: 'changed@example.invalid' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save declaration' }))
  expect(await screen.findByText('Personal field access withheld.')).toBeInTheDocument()
  expect(screen.queryByDisplayValue('SYNTHETIC-KEEP')).not.toBeInTheDocument()
})
it('aborts stale context reads and does not display a previous person response', async () => {
  let resolve!: (value: PersonalProfile) => void
  read.mockImplementationOnce(() => new Promise(done => { resolve = done }))
  const view = render(<CrewPersonalProfile {...props} />)
  const previousSignal = read.mock.calls[0][1]
  view.rerender(<CrewPersonalProfile {...props} assignment="other" />)
  await screen.findByLabelText('Email'); resolve({ ...fixture, data: { email: 'stale@example.invalid' } })
  await waitFor(() => expect(previousSignal.aborted).toBe(true))
  expect(screen.queryByDisplayValue('stale@example.invalid')).not.toBeInTheDocument()
})
