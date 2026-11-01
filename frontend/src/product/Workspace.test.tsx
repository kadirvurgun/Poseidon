import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ProductApp } from './ProductApp'
import { readLocation } from './workspace-location'
import { profileFields } from './profile-contract'

const vessel = { id: '10000000-0000-0000-0000-000000000001', companyId: '10000000-0000-0000-0000-000000000002', code: 'S1', name: 'Synthetic Ship', kind: 'Ship', version: 7, canEdit: true }
const company = { id: vessel.companyId, code: 'C1', name: 'Synthetic Company', version: 7, canEdit: true }
const session = { authenticated: true, setupRequired: false, canRead: true, canReadCompanies: true, canCreate: true, canSearch: false, csrfToken: 'synthetic-token' }
const content = Object.fromEntries(profileFields.map(([key]) => [key, { state: 'Missing', value: null }]))
const profile = { tenantId: '10000000-0000-0000-0000-000000000003', vessel, version: 19, revision: 1, savedAt: '2026-01-01T00:00:00Z', provenance: 'Synthetic', content }
const reply = (body: unknown, status = 200) => ({ ok: status === 200, status, json: async () => body }) as Response
function mock(overrides: Partial<typeof session> = {}, detailStatus = 200) {
  const fetch = vi.fn(async (url: string) => {
    if (url.endsWith('auth/session')) return reply({ ...session, ...overrides })
    if (url.includes('workspace/search')) return reply({ items: [{ key: vessel.id, href: `/app/fleet/${vessel.id}`, label: vessel.name, kind: 'Ship' }], hasMore: false })
    if (url.includes('/history?')) return reply({ items: [], offset: 0, limit: 25, hasMore: false })
    if (url.endsWith('regulatory-profile')) return reply(profile)
    if (url.endsWith(vessel.id)) return reply(vessel, detailStatus)
    if (url.endsWith(company.id)) return reply(company)
    return reply({ items: url.includes('vessels?') ? [vessel] : [company], offset: 0, limit: 25, hasMore: false })
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
beforeEach(() => window.history.replaceState(null, '', '/app'))
afterEach(() => vi.restoreAllMocks())
describe('NAV-WORKSPACE-01 mocked component journeys, separate from real API/browser evidence', () => {
  it('uses a neutral workspace heading until the server provisions a destination', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})))
    render(<ProductApp />)
    expect(screen.getByRole('heading', { name: 'Çalışma alanı' })).toBeVisible()
    expect(document.title).toBe('POSEIDON · Çalışma alanı')
    expect(screen.queryByRole('button', { name: 'Şirketler' })).not.toBeInTheDocument()
  })
  it.each(['/app/fleet/%2Fsecret', '/app/fleet/%', '/app/unknown', '/app/companies/one/profile', '/app//fleet'])('fails closed for malformed or unsupported route %s', async path => {
    expect(readLocation(path).invalid).toBe(true)
    history.replaceState(null, '', path); const fetch = mock(); render(<ProductApp />)
    expect(await screen.findByRole('alert')).toHaveTextContent('erişim yetkiniz yok')
    expect(fetch).toHaveBeenCalledTimes(1)
  })
  it('refreshes an allowed profile deep link with distinct vessel context and exact fields', async () => {
    history.replaceState(null, '', `/app/fleet/${vessel.id}/profile`); mock(); render(<ProductApp />)
    expect(await screen.findByLabelText('GT — Bilgi durumu')).toHaveValue('Missing')
    expect(screen.getByText(/Etkin araç/)).toHaveTextContent('Gemi · Synthetic Ship')
    expect(window.location.pathname).toBe(`/app/fleet/${vessel.id}/profile`)
  })
  it.each([401, 403, 404])('denies stale or wrong-scope vessel detail (%s) without opening a profile', async status => {
    history.replaceState(null, '', `/app/fleet/${vessel.id}/profile`); const fetch = mock({}, status); render(<ProductApp />)
    expect(await screen.findByRole('alert')).toBeVisible()
    expect(screen.queryByLabelText('GT')).not.toBeInTheDocument()
    expect(fetch.mock.calls.some(([url]) => url.endsWith('regulatory-profile'))).toBe(false)
  })
  it('returns an expired deep link to the same validated local profile path after sign-in', async () => {
    const path = `/app/fleet/${vessel.id}/profile`
    history.replaceState(null, '', path); mock({ authenticated: false, canRead: false }); render(<ProductApp />)
    expect(await screen.findByRole('link', { name: 'Oturum aç' })).toHaveAttribute('href', `/api/v1/auth/login?returnUrl=${encodeURIComponent(path)}`)
  })
  it('does not discover company or search destinations for restricted readers', async () => {
    const fetch = mock({ canReadCompanies: false, canCreate: false }); const user = userEvent.setup(); render(<ProductApp />)
    expect(await screen.findByRole('button', { name: vessel.name })).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Şirketler' })).not.toBeInTheDocument()
    await user.keyboard('{Control>}k{/Control}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(fetch.mock.calls.some(([url]) => url.includes('companies') || url.includes('workspace/search'))).toBe(false)
  })
  it('guards Back repeatedly; cancel preserves draft and URL, discard and Forward reload saved state', async () => {
    mock(); const user = userEvent.setup(); render(<ProductApp />)
    await user.click(await screen.findByRole('button', { name: 'Filo' }))
    await user.click(await screen.findByRole('button', { name: vessel.name }))
    await user.click(await screen.findByRole('button', { name: 'Mevzuat profili' }))
    await user.selectOptions(await screen.findByLabelText('GT — Bilgi durumu'), 'Known')
    await user.type(screen.getByLabelText('GT'), '123.4567890123456789')
    const url = window.location.pathname
    act(() => history.back())
    const dialog = await screen.findByRole('dialog')
    await waitFor(() => expect(window.location.pathname).toBe(url))
    await user.click(within(dialog).getByRole('button', { name: 'Düzenlemeye devam et' }))
    expect(screen.getByLabelText('GT')).toHaveValue('123.4567890123456789')
    act(() => history.back())
    await user.click(await screen.findByRole('button', { name: 'Taslağı bırak' }))
    await waitFor(() => expect(window.location.pathname).toBe(`/app/fleet/${vessel.id}`))
    expect(screen.queryByLabelText('GT')).not.toBeInTheDocument()
    act(() => history.forward())
    expect(await screen.findByLabelText('GT — Bilgi durumu')).toHaveValue('Missing')
  })
  it('search and language and mobile drawer preserve a dirty profile; vessel switch uses the guard', async () => {
    history.replaceState(null, '', `/app/fleet/${vessel.id}/profile`); mock({ canSearch: true, canReadCompanies: false, canCreate: false })
    const user = userEvent.setup(); render(<ProductApp />)
    await user.selectOptions(await screen.findByLabelText('GT — Bilgi durumu'), 'Known'); await user.type(screen.getByLabelText('GT'), '42.1')
    await user.click(screen.getByRole('button', { name: 'EN' }))
    await user.click(screen.getByRole('button', { name: 'Menu' }))
    const drawer = screen.getByRole('dialog', { name: 'POSEIDON' })
    await user.click(within(drawer).getByRole('button', { name: 'Close' }))
    await user.keyboard('{Control>}k{/Control}')
    const search = screen.getByRole('dialog', { name: 'Search workspace' })
    await within(search).findByRole('link', { name: /Synthetic Ship/ })
    await user.click(within(search).getByRole('button', { name: 'Close' }))
    expect(screen.getByLabelText('GT')).toHaveValue('42.1')
    await user.click(screen.getByRole('button', { name: 'Switch vessel' }))
    await user.click(await screen.findByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('GT')).toHaveValue('42.1')
    expect(window.location.pathname).toContain('/profile')
    await user.click(screen.getByRole('link', { name: 'POSEIDON' }))
    await user.click(await screen.findByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('GT')).toHaveValue('42.1')
  })
  it('restores drawer focus and traps keyboard movement without navigation', async () => {
    mock(); const user = userEvent.setup(); render(<ProductApp />)
    await screen.findByRole('button', { name: 'Filo' })
    const menu = screen.getByRole('button', { name: 'Menü' }); await user.click(menu)
    expect(screen.getByRole('button', { name: 'Kapat' })).toHaveFocus()
    await user.tab({ shift: true }); expect(screen.getByRole('link', { name: 'Sistem bağlantısı' })).toHaveFocus()
    await user.keyboard('{Escape}'); expect(menu).toHaveFocus(); expect(menu).toHaveAttribute('aria-expanded', 'false')
  })
  it('keeps directory unknown saves frozen, blocks history and links, and retries the same command', async () => {
    const fetch = mock(); let request: RequestInit | undefined
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'PUT') {
        if (!request) { request = init; return reply({}, 503) }
        expect(init.body).toBe(request.body); expect(init.headers).toEqual(request.headers); return reply(company)
      }
      return fetch(url)
    }))
    const user = userEvent.setup(); render(<ProductApp />)
    await user.click(await screen.findByRole('button', { name: company.name }))
    await user.click(await screen.findByRole('button', { name: 'Düzenle' }))
    await user.type(screen.getByLabelText('Ad *'), ' draft')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    await screen.findByText(/sonucu doğrulanamadı/)
    const url = window.location.pathname
    await user.click(screen.getByRole('button', { name: 'Filo' }))
    act(() => history.back()); await new Promise(resolve => setTimeout(resolve, 60))
    await waitFor(() => expect(window.location.pathname).toBe(url))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Ad *')).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Aynı komutu tekrar dene' }))
    await waitFor(() => expect(screen.queryByLabelText('Ad *')).not.toBeInTheDocument())
  })
  it('marks offline data unverified and never turns failed transport into success', async () => {
    mock(); render(<ProductApp />); await screen.findByRole('table')
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    act(() => window.dispatchEvent(new Event('offline')))
    expect(screen.getByRole('alert')).toHaveTextContent('doğrulanmadı')
  })
})
