import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { CrewPhotoSelection } from './CrewPhotoSelection'
import { crewPhoto, type CrewPhoto } from './crew-photo-api'
import type { CrewFileManifest } from './crew-files-api'

const vessel = '11111111-1111-1111-1111-111111111111', person = '22222222-2222-2222-2222-222222222222', file = '33333333-3333-3333-3333-333333333333'
const photo = { id: file, personId: person, kind: 'Photo' as const, revision: 1, expiresOn: null, policyVersion: 'synthetic-v1', purpose: 'synthetic-files', sourceVersion: 'poseidon-v46.3-20261030', createdAt: '2026-11-01T10:00:00Z', state: 'CleanTransport' }
const manifest: CrewFileManifest = { personId: person, policyVersion: 'synthetic-v1', purpose: 'synthetic-files', kinds: ['Photo'], canWrite: true, items: [photo], next: { Photo: 2 }, hasMore: false }
const missing: CrewPhoto = { personId: person, version: 10, revision: 1, policyVersion: 'synthetic-v1', purpose: 'synthetic-files', canWrite: true, state: 'Missing', selected: null }
const selected: CrewPhoto = { ...missing, state: 'SelectedUnverified', selected: photo, version: 11, revision: 2 }
const props = { vessel, person, assignment: person, csrf: 'synthetic', language: 'en' as const, manifest, blocked: false, onDirty: vi.fn(), onReview: vi.fn(), onDenied: vi.fn() }
const fetcher = vi.fn()
const response = (v: unknown, status = 200) => new Response(JSON.stringify(v), { status, headers: { 'Content-Type': 'application/json' } })
beforeEach(() => { fetcher.mockResolvedValue(response(missing)); vi.stubGlobal('fetch', fetcher) })
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset(); props.onDirty.mockReset(); props.onReview.mockReset(); props.onDenied.mockReset() })

it('requires explicit selection and opens only the saved exact version in either language', async () => {
  let saved = false
  fetcher.mockImplementation(async (_url: string, init: RequestInit) => {
    if (init.method === 'PUT') { saved = true; return response({ id: person, revision: 2, version: 11, replayed: false }) }
    return response(saved ? selected : missing)
  })
  const view = render(<CrewPhotoSelection {...props} />)
  await screen.findByText('No photo selected; unknown.')
  expect(screen.queryByRole('button', { name: 'Review selected photo · 1' })).not.toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Select photo version · 1' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Review selected photo · 1' }))
  expect(props.onReview).toHaveBeenCalledWith(photo)
  view.rerender(<CrewPhotoSelection {...props} language="tr" />)
  expect(screen.getByText('Seçili, doğrulanmamış.')).toBeVisible()
  const [, init] = fetcher.mock.calls.find(([, init]) => init.method === 'PUT')!
  expect(JSON.parse(init.body)).toEqual({ baseVersion: 10, fileId: file, policyVersion: 'synthetic-v1', purpose: 'synthetic-files' })
})

it('keeps an uncertain retry UUID and body frozen and prevents another selection or refresh', async () => {
  fetcher.mockImplementation(async (_url: string, init: RequestInit) => { if (init.method === 'PUT') throw new TypeError('synthetic transport loss'); return response(missing) })
  render(<CrewPhotoSelection {...props} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Select photo version · 1' }))
  await screen.findByText('Result uncertain. Retry the same selection.')
  expect(screen.getByRole('button', { name: 'Select photo version · 1' })).toBeDisabled()
  expect(screen.queryByRole('button', { name: 'Refresh photo selection' })).not.toBeInTheDocument()
  expect(props.onDirty).toHaveBeenLastCalledWith(true)
  fireEvent.click(screen.getByRole('button', { name: 'Retry same photo selection' }))
  await waitFor(() => expect(fetcher.mock.calls.filter(([, init]) => init.method === 'PUT')).toHaveLength(2))
  const calls = fetcher.mock.calls.filter(([, init]) => init.method === 'PUT')
  expect(calls[0][1].body).toBe(calls[1][1].body)
  expect(calls[0][1].headers['Idempotency-Key']).toBe(calls[1][1].headers['Idempotency-Key'])
})

it('withdraws saved selection on same-session denial and asks parent to withdraw private transport', async () => {
  fetcher.mockResolvedValue(response(selected)); render(<CrewPhotoSelection {...props} />)
  await screen.findByRole('button', { name: 'Review selected photo · 1' })
  fetcher.mockResolvedValue(response({}, 403)); fireEvent.click(screen.getByRole('button', { name: 'Refresh photo selection' }))
  await waitFor(() => expect(props.onDenied).toHaveBeenCalledOnce())
  expect(screen.queryByRole('button', { name: 'Review selected photo · 1' })).not.toBeInTheDocument()
})

it('hides previous person selection immediately and discards late responses after context change', async () => {
  let resolve!: (v: Response) => void
  fetcher.mockImplementationOnce(() => new Promise<Response>(r => { resolve = r }))
  const view = render(<CrewPhotoSelection {...props} />)
  view.rerender(<CrewPhotoSelection {...props} person={vessel} assignment={vessel} />)
  resolve(response(selected))
  await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2))
  expect(screen.queryByRole('button', { name: 'Review selected photo · 1' })).not.toBeInTheDocument()
})

it('requires explicit conflict discard and fresh profile version instead of overwriting', async () => {
  fetcher.mockImplementation(async (_url: string, init: RequestInit) => response(init.method === 'PUT' ? {} : missing, init.method === 'PUT' ? 409 : 200))
  render(<CrewPhotoSelection {...props} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Select photo version · 1' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Discard conflicted selection and reload' }))
  await waitFor(() => expect(fetcher.mock.calls.filter(([, init]) => init.method !== 'PUT')).toHaveLength(2))
  expect(fetcher.mock.calls.filter(([, init]) => init.method === 'PUT')).toHaveLength(1)
  expect(props.onDirty).toHaveBeenLastCalledWith(false)
})

it('rejects forged scope, approval, pending and inconsistent selection contracts at the wire boundary', async () => {
  for (const value of [{ ...selected, personId: vessel }, { ...selected, state: 'Verified' }, { ...selected, selected: { ...photo, personId: vessel } },
    { ...selected, selected: { ...photo, kind: 'Passport' } }, { ...selected, selected: { ...photo, state: 'Pending' } },
    { ...selected, selected: { ...photo, policyVersion: 'withdrawn' } }, { ...missing, selected: photo }, { ...missing, version: null }]) {
    fetcher.mockResolvedValue(response(value))
    await expect(crewPhoto(vessel, person, person, new AbortController().signal)).rejects.toThrow()
  }
})
