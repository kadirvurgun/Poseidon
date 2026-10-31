import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ProductApp } from './ProductApp'

const anonymous = { authenticated: false, setupRequired: false, actorId: null, tenantId: null, canRead: false, csrfToken: '' }
const signedIn = { ...anonymous, authenticated: true, canRead: true, actorId: 'synthetic-actor', tenantId: 'synthetic-workspace', csrfToken: 'synthetic-csrf' }
const reply = (data: unknown, status = 200) => ({ ok: status === 200, status, json: async () => data }) as Response
afterEach(() => vi.unstubAllGlobals())

describe('FOUNDATION-APP-01 product states (mocked HTTP, not backend acceptance)', () => {
  it('keeps loading accessible while the session request is pending', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})))
    render(<ProductApp />)
    expect(screen.getByRole('status')).toHaveTextContent('Yükleniyor')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
  it('shows setup required without offering a login that cannot succeed', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply({ ...anonymous, setupRequired: true })))
    render(<ProductApp />)
    expect(await screen.findByText(/henüz yapılandırılmadı/)).toBeVisible()
    expect(screen.queryByRole('link', { name: 'Oturum aç' })).not.toBeInTheDocument()
  })
  it('links sign-in to the BFF and switches the full shell to English', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply(anonymous)))
    const user = userEvent.setup(); render(<ProductApp />)
    expect(await screen.findByRole('link', { name: 'Oturum aç' })).toHaveAttribute('href', '/api/v1/auth/login?returnUrl=%2Fapp')
    await user.click(screen.getByRole('button', { name: 'EN' }))
    expect(screen.getByRole('heading', { name: 'Companies' })).toBeVisible()
    expect(document.documentElement.lang).toBe('en')
    expect(screen.getByRole('link', { name: 'Sign in' })).toBeVisible()
  })
  it.each([401, 403, 503])('distinguishes HTTP %s from an empty list', async status => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(reply(signedIn)).mockResolvedValue(reply({}, status)))
    render(<ProductApp />)
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(status === 401 ? 'oturum açın' : status === 403 ? 'erişim yetkiniz yok' : 'ulaşılamıyor')
    expect(screen.queryByText('Kayıt bulunamadı.')).not.toBeInTheDocument()
  })
  it('shows real empty results and retries a failed service request', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(reply(signedIn)).mockRejectedValueOnce(new TypeError('offline'))
      .mockResolvedValueOnce(reply(signedIn)).mockResolvedValue(reply({ items: [], offset: 0, limit: 25, hasMore: false }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup(); render(<ProductApp />)
    await user.click(await screen.findByRole('button', { name: 'Tekrar dene' }))
    expect(await screen.findByText('Kayıt bulunamadı.')).toBeVisible()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
  it('loads company detail and requests ship/tug filters from the API', async () => {
    const fetchMock = vi.fn(async (url: string) => {
      if (url.endsWith('auth/session')) return reply(signedIn)
      if (url.endsWith('companies/company-one')) return reply({ id: 'company-one', code: 'C1', name: 'Synthetic Company' })
      if (url.includes('companies?')) return reply({ items: [{ id: 'company-one', code: 'C1', name: 'Synthetic Company' }], offset: 0, limit: 25, hasMore: false })
      return reply({ items: [{ id: 'tug-one', companyId: 'company-one', code: 'T1', name: 'Synthetic Tug', kind: 'Tug' }], offset: 0, limit: 25, hasMore: false })
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup(); render(<ProductApp />)
    await user.click(await screen.findByRole('button', { name: 'Synthetic Company' }))
    expect(await screen.findByRole('region', { name: 'Ayrıntılar' })).toHaveTextContent('Synthetic Company')
    await user.click(screen.getByRole('button', { name: 'Filo' }))
    await user.selectOptions(screen.getByRole('combobox'), 'Tug')
    expect(await screen.findByRole('button', { name: 'Synthetic Tug' })).toBeVisible()
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/v1/vessels?offset=0&limit=25&kind=Tug', expect.objectContaining({ credentials: 'same-origin' })))
    expect(screen.getByRole('cell', { name: 'Römorkör' })).toBeVisible()
  })
  it('does not request business records for an authenticated identity without read permission', async () => {
    const fetchMock = vi.fn().mockResolvedValue(reply({ ...signedIn, canRead: false }))
    vi.stubGlobal('fetch', fetchMock); render(<ProductApp />)
    expect(await screen.findByRole('alert')).toHaveTextContent('erişim yetkiniz yok')
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
