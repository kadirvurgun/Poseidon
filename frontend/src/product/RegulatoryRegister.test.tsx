import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { RegulatoryRegister } from './RegulatoryRegister'
import { blankContent, decodeDraft, decodeRevision, saveDraft } from './register-api'
import type { Draft } from './register-api'

const id = '10000000-0000-0000-0000-000000000001'
const source = '10000000-0000-0000-0000-000000000002'
const draft: Draft = { version: 19, canEdit: true, current: { id, kind: 'Source', revision: 1, savedAt: '2026-11-01T00:00:00Z', provenance: 'DeclaredUnverified', hash: 'A'.repeat(64), content: blankContent(), sourceId: null, sourceRevision: null, lifecycle: 'DRAFT' } }
const response = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response
const page = <T,>(items: T[]) => ({ items, offset: 0, limit: 25, hasMore: false })
const props = () => ({ kind: 'sources' as const, id, language: 'en' as const, csrf: 'synthetic-csrf', canWrite: true, offline: false, onDirty: vi.fn(), onSaving: vi.fn(), onNavigate: vi.fn() })
function mock(save: (init: RequestInit) => Promise<Response> = async () => response({ ...draft, version: 20, current: { ...draft.current, revision: 2 } })) {
  const fetch = vi.fn((url: string, init: RequestInit) => init?.method ? save(init) : Promise.resolve(response(url.includes('/history') ? page([draft.current]) : draft)))
  vi.stubGlobal('fetch', fetch); return fetch
}
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('REG-SOURCE-DRAFT-01 authored component/transport tests; mocked transport only, NOT RUN', () => {
  it('saves incomplete explicit Unknown without inventing approval or fetching a declared locator', async () => {
    const fetch = mock(); const p = props(); render(<RegulatoryRegister {...p} />); const user = userEvent.setup()
    await screen.findByLabelText('Title · State'); await user.selectOptions(screen.getByLabelText('Title · State'), 'Unknown')
    await user.selectOptions(screen.getByLabelText('Source reference (text only) · State'), 'Known')
    await user.type(screen.getByLabelText('Source reference (text only)', { exact: true }), 'file:///declared-only')
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByText('Draft saved. Unverified.')
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(JSON.parse(call[1].body as string)).toMatchObject({ expectedVersion: 19, content: { title: { state: 'Unknown', value: null } } })
    expect(fetch.mock.calls.every(([url]) => url.startsWith('/api/v1/'))).toBe(true)
    expect(screen.queryByRole('button', { name: /Approve|Activate|Verify/ })).not.toBeInTheDocument()
    expect(p.onDirty).toHaveBeenLastCalledWith(false)
  })
  it('freezes an uncertain command and retries identical bytes/key through a language change', async () => {
    let count = 0; const fetch = mock(async () => ++count === 1 ? Promise.reject(new TypeError('network')) : response({ ...draft, version: 20 }))
    const p = props(); const { rerender } = render(<RegulatoryRegister {...p} />); const user = userEvent.setup()
    await screen.findByLabelText('Title · State'); await user.selectOptions(screen.getByLabelText('Title · State'), 'Unknown')
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByRole('button', { name: 'Retry frozen command' })
    expect(screen.getByLabelText('Title · State')).toBeDisabled()
    rerender(<RegulatoryRegister {...p} language="tr" />); await user.click(screen.getByRole('button', { name: 'Sabit komutu tekrar dene' }))
    await screen.findByText('Taslak kaydedildi. Doğrulanmadı.')
    const saves = fetch.mock.calls.filter(([, init]) => init?.method === 'PUT'); expect(saves).toHaveLength(2)
    expect(saves[0][1].body).toBe(saves[1][1].body); expect(saves[0][1].headers).toEqual(saves[1][1].headers)
  })
  it('retains dirty values through history failure, history retry, offline reconnect and conflict', async () => {
    let fail = true
    vi.stubGlobal('fetch', vi.fn((url: string, init: RequestInit) => Promise.resolve(response(init?.method ? { code: 'version_conflict' } : url.includes('history') ? fail ? {} : page([draft.current]) : draft, init?.method ? 409 : 200))))
    const p = props(); const { rerender } = render(<RegulatoryRegister {...p} />); const user = userEvent.setup()
    await screen.findByLabelText('Title · State'); await user.selectOptions(screen.getByLabelText('Title · State'), 'Known'); await user.type(screen.getByLabelText('Title', { exact: true }), 'Retained declaration')
    fail = false; await user.click(await screen.findByRole('button', { name: 'Retry history' })); await screen.findByText(/Revision 1 ·/, { selector: 'summary' })
    rerender(<RegulatoryRegister {...p} offline />); rerender(<RegulatoryRegister {...p} offline={false} />)
    await waitFor(() => expect(screen.getByLabelText('Title', { exact: true })).toHaveValue('Retained declaration'))
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByRole('button', { name: 'Reload saved draft' })
    expect(screen.getByLabelText('Title', { exact: true })).toHaveValue('Retained declaration'); expect(screen.getByRole('button', { name: 'Save draft' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Reload saved draft' })); expect(p.onNavigate).toHaveBeenCalledWith(`/app/sources/${id}`)
  })
  it('denies editing for a server read-only projection and exposes immutable history', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(response(url.includes('history') ? page([draft.current]) : { ...draft, canEdit: false }))))
    render(<RegulatoryRegister {...props()} />); await screen.findByLabelText('Title · State')
    expect(screen.getByLabelText('Title · State')).toBeDisabled(); expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Immutable history' })).toBeVisible()
  })
  it.each([401, 403])('shows a %s response and retains declared edits without another write', async status => {
    mock(async () => response({}, status)); render(<RegulatoryRegister {...props()} />); const user = userEvent.setup()
    await screen.findByLabelText('Title · State'); await user.selectOptions(screen.getByLabelText('Title · State'), 'Unknown'); await user.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByRole('alert'); expect(screen.getByLabelText('Title · State')).toHaveValue('Unknown'); expect(screen.getByRole('button', { name: 'Save draft' })).toBeDisabled()
  })
  it('searches the tenant register and shows an honest empty result', async () => {
    const fetch = vi.fn(() => Promise.resolve(response(page([])))); vi.stubGlobal('fetch', fetch)
    render(<RegulatoryRegister {...props()} id={null} />); const user = userEvent.setup()
    await screen.findByText('No drafts found.'); await user.type(screen.getByLabelText('Search drafts'), 'fictional'); await user.click(screen.getByRole('button', { name: 'Search' }))
    await waitFor(() => expect(fetch.mock.calls.length).toBe(2))
    expect((fetch.mock.calls as unknown as [string, RequestInit][])[1][0]).toContain('q=fictional')
  })
  it('pins a requirement to a selected immutable source revision instead of current source metadata', async () => {
    const requirement = { ...draft, current: { ...draft.current, kind: 'Requirement', sourceId: source, sourceRevision: 1 } }
    const sourceDraft = { ...draft, current: { ...draft.current, id: source, revision: 2 } }
    const sourceHistory = [sourceDraft.current, { ...sourceDraft.current, revision: 1, hash: 'B'.repeat(64) }]
    const fetch = vi.fn((url: string, init: RequestInit) => Promise.resolve(response(init?.method ? requirement : url.includes('source-drafts') ? url.includes('history') ? page(sourceHistory) : page([sourceDraft]) : url.includes('history') ? page([requirement.current]) : requirement)))
    vi.stubGlobal('fetch', fetch); render(<RegulatoryRegister {...props()} kind="requirements" />); const user = userEvent.setup()
    await screen.findByLabelText('Exact revision'); await waitFor(() => expect(screen.getByLabelText('Exact revision')).toHaveValue('1'))
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByText('Draft saved. Unverified.')
    expect(JSON.parse(fetch.mock.calls.find(([, init]) => init?.method)?.[1].body as string)).toMatchObject({ sourceId: source, sourceRevision: 1 })
  })
  it('labels source and revision selectors independently of their options while creating a requirement', async () => {
    const sourceDraft = { ...draft, current: { ...draft.current, id: source, revision: 2 } }
    const revisions = [sourceDraft.current, { ...sourceDraft.current, revision: 1 }]
    const requirement = { ...draft, current: { ...draft.current, kind: 'Requirement', sourceId: source, sourceRevision: 1 } }
    const fetch = vi.fn((url: string, init: RequestInit) => Promise.resolve(response(init?.method ? requirement : url.includes('source-drafts') ? url.includes('history') ? page(revisions) : page([sourceDraft]) : page([]))))
    vi.stubGlobal('fetch', fetch); render(<RegulatoryRegister {...props()} kind="requirements" id={null} />); const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'New draft' }))
    await screen.findByRole('option', { name: source })
    await user.selectOptions(screen.getByRole('combobox', { name: 'Source draft' }), source)
    await screen.findByRole('option', { name: /^1 ·/ })
    await user.selectOptions(screen.getByRole('combobox', { name: 'Exact revision' }), '1')
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByText('Draft saved. Unverified.')
    expect(JSON.parse(fetch.mock.calls.find(([, init]) => init?.method)?.[1].body as string)).toMatchObject({ sourceId: source, sourceRevision: 1 })
  })
  it.each([
    { ...draft, version: 1.5 }, { ...draft, current: { ...draft.current, lifecycle: 'InForce' } },
    { ...draft, current: { ...draft.current, sourceId: source, sourceRevision: 1 } },
    { ...draft, current: { ...draft.current, content: { ...blankContent(), observedAt: { state: 'Known', value: 'yesterday' } } } },
  ])('rejects invalid current contracts safely', value => { expect(() => decodeDraft(value, 'sources')).toThrow('Request failed (502)') })
  it('validates every immutable history field and uses same-origin command transport', async () => {
    expect(() => decodeRevision({ ...draft.current, provenance: 'Verified' }, 'sources')).toThrow()
    const fetch = vi.fn(() => Promise.resolve(response(draft))); vi.stubGlobal('fetch', fetch)
    await saveDraft('sources', id, '{"expectedVersion":19}', 'synthetic-csrf', 'synthetic-command-key', new AbortController().signal)
    expect(fetch).toHaveBeenCalledWith(`/api/v1/source-drafts/${id}`, expect.objectContaining({ credentials: 'same-origin', method: 'PUT', body: '{"expectedVersion":19}', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'synthetic-csrf', 'Idempotency-Key': 'synthetic-command-key' }) }))
  })
})
