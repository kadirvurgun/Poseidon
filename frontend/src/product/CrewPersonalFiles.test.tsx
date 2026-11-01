import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { CrewPersonalFiles } from './CrewPersonalFiles'
import { crewFileVersions } from './crew-files-api'
import { stageFile } from './file-api'

const vessel = '11111111-1111-1111-1111-111111111111', person = '22222222-2222-2222-2222-222222222222'
const file = '33333333-3333-3333-3333-333333333333'
const manifest = { personId: person, policyVersion: 'synthetic-v1', purpose: 'synthetic-files', kinds: ['Photo', 'Passport'],
  canWrite: true, hasMore: false, next: { Photo: 2, Passport: 1 }, items: [{ id: file, personId: person, kind: 'Photo', revision: 1,
    expiresOn: null, policyVersion: 'synthetic-v1', purpose: 'synthetic-files', sourceVersion: 'poseidon-v46.3-20261030', createdAt: '2026-11-01T10:00:00Z', state: 'CleanTransport' }] }
const transport = { items: [], offset: 0, hasMore: false, canWrite: true, available: true, maxBytes: 1024, allowedTypes: ['image/png'] }
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const fetcher = vi.fn()
const props = { vessel, assignment: person, person, csrf: 'synthetic-session', language: 'en' as const, onDirty: vi.fn() }
beforeEach(() => {
  fetcher.mockImplementation(async (url: string) => response(url.endsWith('/personal-files') ? manifest : transport))
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); fetcher.mockReset(); props.onDirty.mockReset() })

it('opens the exact person version with expiry unknown and preserves unverified meaning in both languages', async () => {
  const view = render(<CrewPersonalFiles {...props} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Photo · 1' }))
  await screen.findByRole('heading', { name: 'Private person file transport' })
  await waitFor(() => expect(fetcher.mock.calls.some(([url]) => String(url).includes(`/crew/${person}/personal-files/Photo/versions/1?offset=0`))).toBe(true))
  expect(screen.getByText('Expiry unknown')).toBeVisible()
  expect(screen.getByText('Scanned, unverified')).toBeVisible()
  view.rerender(<CrewPersonalFiles {...props} language="tr" />)
  expect(screen.getByRole('heading', { name: 'Özel personel dosyaları' })).toBeVisible()
  expect(screen.queryByText('READY')).not.toBeInTheDocument()
})

it('freezes kind and revision selection while a private file is dirty', async () => {
  render(<CrewPersonalFiles {...props} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Start new private version' }))
  const input = await screen.findByLabelText('Attachment file')
  await waitFor(() => expect(input).not.toBeDisabled())
  fireEvent.change(input, { target: { files: [new File(['synthetic'], 'synthetic.png', { type: 'image/png' })] } })
  expect(props.onDirty).toHaveBeenLastCalledWith(true)
  expect(screen.getByRole('button', { name: 'Photo · 1' })).toBeDisabled()
  expect(screen.getByLabelText('Private file kind')).toBeDisabled()
  fireEvent.click(screen.getByRole('button', { name: 'Discard local attempt' }))
  expect(props.onDirty).toHaveBeenLastCalledWith(false)
})

it('withdraws the manifest and file child after current access is denied', async () => {
  render(<CrewPersonalFiles {...props} />)
  await screen.findByRole('button', { name: 'Photo · 1' })
  fetcher.mockResolvedValue(response({}, 403))
  fireEvent.click(screen.getByRole('button', { name: 'Refresh private files' }))
  await screen.findByText('Private file access or policy unavailable.')
  expect(screen.queryByRole('button', { name: 'Photo · 1' })).not.toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Private person file transport' })).not.toBeInTheDocument()
})

it('hides prior person metadata synchronously and ignores a late old-person response', async () => {
  let resolve!: (r: Response) => void
  fetcher.mockImplementationOnce(() => new Promise<Response>(r => { resolve = r }))
  const view = render(<CrewPersonalFiles {...props} />)
  view.rerender(<CrewPersonalFiles {...props} person={vessel} assignment={vessel} />)
  resolve(response(manifest))
  await waitFor(() => expect(fetcher.mock.calls.length).toBeGreaterThan(1))
  expect(screen.queryByRole('button', { name: 'Photo · 1' })).not.toBeInTheDocument()
})

it('denies malformed or cross-person manifests at the real wire adapter', async () => {
  for (const invalid of [{ ...manifest, personId: vessel }, { ...manifest, items: [manifest.items[0], manifest.items[0]] },
    { ...manifest, next: { Photo: '2' } }, { ...manifest, kinds: ['MedicalDecision'] },
    { ...manifest, items: [{ ...manifest.items[0], state: 'Verified' }] },
    { ...manifest, items: [{ ...manifest.items[0], expiresOn: '2030-02-30' }] },
    { ...manifest, items: [{ ...manifest.items[0], sourceVersion: 'historical-runtime' }] }]) {
    fetcher.mockResolvedValue(response(invalid))
    await expect(crewFileVersions(vessel, person, person, new AbortController().signal)).rejects.toThrow()
  }
})

it('stages a fixed safe transport label with exact owner version and expiry, withholding original filename', async () => {
  const row = { id: file, vesselId: vessel, name: 'document.png', length: 9, revision: null, checksum: null, version: 1, state: 'Pending' }
  fetcher.mockResolvedValue(response(row))
  const attempt = { key: crypto.randomUUID(), file: new File(['synthetic'], 'PRIVATE-ORIGINAL.png', { type: 'image/png' }), checksum: 'A'.repeat(64) }
  await stageFile(vessel, attempt, props.csrf, new AbortController().signal, { type: 'Person', assignment: person, kind: 'Passport', revision: 4, expiresOn: '2030-01-02' })
  const [url, init] = fetcher.mock.calls[0] as [string, RequestInit]
  expect(url).toContain(`/crew/${person}/personal-files/Passport/versions/4`)
  expect(JSON.parse(init.body as string)).toEqual({ name: 'document.png', mime: 'image/png', length: 9, checksum: attempt.checksum, expiresOn: '2030-01-02' })
  expect(init.body).not.toContain('PRIVATE-ORIGINAL')
})

it('clears previously loaded transport details when a same-session detail request returns denial', async () => {
  const row = { id: file, vesselId: vessel, name: 'photo.png', length: 9, revision: null, checksum: null, version: 1, state: 'Pending' }
  let denied = false
  fetcher.mockImplementation(async (url: string) => response(url.endsWith('/personal-files') ? manifest : url.includes('?offset=') ? { ...transport, items: [row] } : denied ? {} : row, denied && !url.includes('?') && !url.endsWith('/personal-files') ? 403 : 200))
  render(<CrewPersonalFiles {...props} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Photo · 1' }))
  const panel = await screen.findByRole('region', { name: 'Private person file transport' })
  fireEvent.click(await within(panel).findByRole('button', { name: 'photo.png' }))
  await within(panel).findByRole('article', { name: 'File details' })
  denied = true; fireEvent.click(within(panel).getByRole('button', { name: 'photo.png' }))
  await waitFor(() => expect(within(panel).queryByRole('article')).not.toBeInTheDocument())
  expect(within(panel).queryByRole('button', { name: 'photo.png' })).not.toBeInTheDocument()
})
