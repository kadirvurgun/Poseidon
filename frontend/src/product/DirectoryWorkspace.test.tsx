import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DirectoryEditor } from './DirectoryEditor'
import { ProductApp } from './ProductApp'
import { saveDirectory } from './api'

const record = { id: 'company-one', code: 'C1', name: 'Synthetic Company', version: 21, canEdit: true }
const session = { authenticated: true, setupRequired: false, canRead: true, canReadCompanies: true, canCreate: true, tenantName: 'Synthetic workspace', csrfToken: 'csrf-test' }
const reply = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response
function props(edit = false) {
  return { target: { resource: 'companies' as const, ...(edit ? { record } : {}) }, language: 'en' as const, csrf: 'csrf-test', onDirty: vi.fn(), onSaving: vi.fn(), onCancel: vi.fn(), onReload: vi.fn(), onSaved: vi.fn() }
}
beforeEach(() => window.history.replaceState(null, '', '/app'))
afterEach(() => vi.restoreAllMocks())

describe('DIR-WORKSPACE-01 mocked UI behavior (not real backend acceptance)', () => {
  it('validates required fields, associates errors and focuses the first invalid input', async () => {
    const fetchMock = vi.fn(); vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup(); render(<DirectoryEditor {...props()} />)
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(screen.getAllByText('This field is required.')).toHaveLength(2)
    expect(screen.getByLabelText('Code *')).toHaveFocus()
    expect(screen.getByLabelText('Name *')).toHaveAttribute('aria-invalid', 'true')
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('saves real normalized HTTP input with CSRF and command key and blocks duplicate clicks', async () => {
    let resolve!: (response: Response) => void
    const fetchMock = vi.fn(() => new Promise<Response>(r => { resolve = r }))
    vi.stubGlobal('fetch', fetchMock)
    const callbacks = props(); const user = userEvent.setup(); render(<DirectoryEditor {...callbacks} />)
    await user.type(screen.getByLabelText('Code *'), ' C2 '); await user.type(screen.getByLabelText('Name *'), ' New Company ')
    await user.dblClick(screen.getByRole('button', { name: 'Save' }))
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: 'Saving…' })).toBeDisabled()
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/companies', expect.objectContaining({ method: 'POST', body: JSON.stringify({ code: 'C2', name: 'New Company' }), headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-test', 'Idempotency-Key': expect.any(String) }) }))
    resolve(reply({ ...record, code: 'C2', name: 'New Company' }))
    await waitFor(() => expect(callbacks.onSaved).toHaveBeenCalledWith(expect.objectContaining({ code: 'C2' })))
  })
  it('requires explicit Ship/Tug and a company selected from the real API', async () => {
    const fetchMock = vi.fn().mockResolvedValue(reply({ items: [record], offset: 0, limit: 25, hasMore: false }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup(); render(<DirectoryEditor {...props()} target={{ resource: 'vessels' }} />)
    await screen.findByRole('option', { name: 'C1 · Synthetic Company' })
    await user.type(screen.getByLabelText('Code *'), 'T2'); await user.type(screen.getByLabelText('Name *'), 'Synthetic Tug')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(screen.getByLabelText('Type *')).toHaveValue('')
    expect(screen.getByLabelText('Type *')).toHaveAttribute('aria-invalid', 'true')
    await user.selectOptions(screen.getByLabelText('Type *'), 'Tug'); await user.selectOptions(screen.getByLabelText('Company *'), record.id)
    fetchMock.mockResolvedValue(reply({ id: 'tug-new', companyId: record.id, code: 'T2', name: 'Synthetic Tug', kind: 'Tug', version: 22, canEdit: true }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(fetchMock).toHaveBeenLastCalledWith('/api/v1/vessels', expect.objectContaining({ body: JSON.stringify({ code: 'T2', name: 'Synthetic Tug', kind: 'Tug', companyId: record.id }) }))
  })
  it('retains a stale draft, sends expected version, and offers explicit reload instead of resubmission', async () => {
    const fetchMock = vi.fn().mockResolvedValue(reply({ code: 'version_conflict' }, 409)); vi.stubGlobal('fetch', fetchMock)
    const callbacks = props(true); const user = userEvent.setup(); render(<DirectoryEditor {...callbacks} />)
    await user.clear(screen.getByLabelText('Name *')); await user.type(screen.getByLabelText('Name *'), 'Unsaved draft')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Your draft is retained')
    expect(screen.getByLabelText('Name *')).toHaveValue('Unsaved draft')
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/companies/company-one', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ code: 'C1', name: 'Unsaved draft', expectedVersion: 21 }) }))
    expect(callbacks.onSaved).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Discard draft and load current record' }))
    expect(callbacks.onReload).toHaveBeenCalledOnce()
  })
  it.each([{ status: 400, code: 'validation_failed' }, { status: 403, code: 'permission_denied' }, { status: 409, code: 'duplicate_code' }])('retains draft for HTTP $status and safe server field errors', async ({ status, code }) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply({ code, errors: { code: ['invalid'] }, correlationId: 'safe-reference' }, status)))
    const callbacks = props(); const user = userEvent.setup(); render(<DirectoryEditor {...callbacks} />)
    await user.type(screen.getByLabelText('Code *'), 'C2'); await user.type(screen.getByLabelText('Name *'), 'Local draft')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('safe-reference')
    expect(screen.getByLabelText('Name *')).toHaveValue('Local draft')
    expect(screen.getByLabelText('Code *')).toHaveAttribute('aria-invalid', 'true')
    expect(callbacks.onSaved).not.toHaveBeenCalled()
  })
  it('retries an ambiguous transport result with exactly the same payload and key', async () => {
    const fetchMock = vi.fn().mockRejectedValueOnce(new TypeError('connection lost')).mockResolvedValue(reply(record))
    vi.stubGlobal('fetch', fetchMock)
    const callbacks = props(); const user = userEvent.setup(); render(<DirectoryEditor {...callbacks} />)
    await user.type(screen.getByLabelText('Code *'), 'C1'); await user.type(screen.getByLabelText('Name *'), 'Synthetic Company')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('outcome could not be confirmed')
    expect(screen.getByLabelText('Name *')).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Retry same command' }))
    const first = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    const second = fetchMock.mock.calls[1] as unknown as [string, RequestInit]
    expect(second[1].body).toEqual(first[1].body); expect(second[1].headers).toEqual(first[1].headers)
    await waitFor(() => expect(callbacks.onSaved).toHaveBeenCalledOnce())
  })
  it('shows read-only records without create or edit controls', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => url.endsWith('auth/session') ? reply({ ...session, canCreate: false }) : url.includes('companies?') ? reply({ items: [{ ...record, canEdit: false }], offset: 0, limit: 25, hasMore: false }) : reply({ ...record, canEdit: false })))
    const user = userEvent.setup(); render(<ProductApp />)
    await user.click(await screen.findByRole('button', { name: record.name }))
    await screen.findByRole('region', { name: 'Ayrıntılar' })
    expect(screen.queryByRole('button', { name: 'Yeni kayıt' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Düzenle' })).not.toBeInTheDocument()
  })
  it('keeps draft after a declined local navigation and discards only with explicit confirmation', async () => {
    // jsdom implements neither native dialog method; browser tests use the real implementation.
    vi.spyOn(HTMLDialogElement.prototype, 'showModal').mockImplementation(function(this: HTMLDialogElement) { this.setAttribute('open', '') })
    vi.spyOn(HTMLDialogElement.prototype, 'close').mockImplementation(function(this: HTMLDialogElement) { this.removeAttribute('open') })
    vi.stubGlobal('fetch', vi.fn(async (url: string) => url.endsWith('auth/session') ? reply(session) : reply({ items: [], offset: 0, limit: 25, hasMore: false })))
    const user = userEvent.setup(); render(<ProductApp />)
    await user.click(await screen.findByRole('button', { name: 'Yeni kayıt' }))
    await user.type(screen.getByLabelText('Ad *'), 'Korunan taslak')
    await user.click(screen.getByRole('button', { name: 'Filo' }))
    await user.click(screen.getByRole('button', { name: 'Düzenlemeye devam et' }))
    expect(screen.getByLabelText('Ad *')).toHaveValue('Korunan taslak')
    await user.click(screen.getByRole('button', { name: 'Filo' }))
    await user.click(screen.getByRole('button', { name: /^Taslağı bırak$/ }))
    expect(screen.queryByLabelText('Ad *')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Filo' })).toBeVisible()
  })
  it('uses scoped HTTP search and opens restricted vessel workspace without requesting company metadata', async () => {
    const fetchMock = vi.fn(async (url: string) => url.endsWith('auth/session') ? reply({ ...session, canReadCompanies: false, canCreate: false }) : reply({ items: [], offset: 0, limit: 25, hasMore: false }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup(); render(<ProductApp />)
    await screen.findByRole('heading', { name: 'Filo' })
    expect(screen.queryByRole('button', { name: 'Şirketler' })).not.toBeInTheDocument()
    await user.type(screen.getByLabelText('Kod veya ad ile ara'), 'Synthetic & Tug')
    await user.click(screen.getByRole('button', { name: /^Ara$/ }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/v1/vessels?offset=0&limit=25&q=Synthetic%20%26%20Tug', expect.anything()))
    expect(fetchMock.mock.calls.some(([url]) => url.includes('/companies'))).toBe(false)
  })
  it('refreshes list and detail from HTTP after successful save', async () => {
    let committed = false
    const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      if (url.endsWith('auth/session')) return reply(session)
      if (init?.method === 'POST') { committed = true; return reply(record) }
      if (url.endsWith('companies/company-one')) return reply(record)
      return reply({ items: committed ? [record] : [], offset: 0, limit: 25, hasMore: false })
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup(); render(<ProductApp />)
    await user.click(await screen.findByRole('button', { name: 'Yeni kayıt' }))
    await user.type(screen.getByLabelText('Kod *'), 'C1'); await user.type(screen.getByLabelText('Ad *'), 'Synthetic Company')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    expect(await screen.findByText(/Kayıt kaydedildi/)).toBeVisible()
    expect(await screen.findByRole('button', { name: record.name })).toBeVisible()
    await waitFor(() => expect(screen.getByRole('region', { name: 'Ayrıntılar' })).toHaveTextContent(record.name))
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/companies/company-one', expect.anything())
  })
})

describe('DIR command HTTP transport', () => {
  it('carries no client actor or tenant and propagates safe ProblemDetails fields', async () => {
    const fetchMock = vi.fn().mockResolvedValue(reply({ code: 'duplicate_code', errors: { code: ['duplicate'] }, correlationId: 'correlation' }, 409))
    vi.stubGlobal('fetch', fetchMock)
    await expect(saveDirectory('companies', undefined, { code: 'C1', name: 'N' }, 'csrf', 'stable-command-key', new AbortController().signal)).rejects.toMatchObject({ status: 409, code: 'duplicate_code', errors: { code: ['duplicate'] }, correlationId: 'correlation' })
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/companies', expect.objectContaining({ credentials: 'same-origin', headers: { Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf', 'Idempotency-Key': 'stable-command-key' }, body: '{"code":"C1","name":"N"}' }))
  })
})
