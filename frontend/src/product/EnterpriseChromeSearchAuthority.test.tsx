import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { EnterpriseChrome } from './EnterpriseChrome'
import type { Session } from './directory-contract'

vi.mock('./WorkspaceContextBar', () => ({ WorkspaceContextBar: () => <div>Scoped context</div> }))
vi.mock('./WorkspaceRoleNavigation', () => ({ WorkspaceRoleNavigation: () => <div>Scoped work view</div> }))
const session = { authenticated: true, canRead: true, canReadCompanies: false, canSearch: true, setupRequired: false,
  tenantId: '10000000-0000-0000-0000-000000000001', actorId: '20000000-0000-0000-0000-000000000001', tenantName: 'FICTIONAL workspace' } as Session
const menu = { destinations: [{ key: 'fleet', href: '/app/fleet' }], canSearch: true, canWriteRegulatory: false }
const result = (label: string) => ({ items: [{ key: '10000000-0000-0000-0000-000000000001', href: '/app/fleet/10000000-0000-0000-0000-000000000001', label, kind: 'Tug' }], offset: 0, limit: 25, hasMore: false })
const reply = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const props = { session, language: 'en' as const, onLanguageChange: vi.fn(), onNavigate: vi.fn(), title: 'Fleet', mainId: 'test-main' }
const view = (owner = session) => <div className="product-shell"><EnterpriseChrome {...props} session={owner} /><main id="test-main" /></div>
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('retains only the same-session query shell offline and waits for a fresh grant before rereading that query', async () => {
  const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  let resolveNavigation!: (response: Response) => void
  let pendingNavigation = false, searchReads = 0
  const fetch = vi.fn(async (url: string) => {
    if (url === '/api/v1/workspace') {
      if (!navigator.onLine) throw new TypeError('Synthetic disconnected transport')
      if (pendingNavigation) return new Promise<Response>(resolve => { resolveNavigation = resolve })
      return reply(menu)
    }
    if (url.startsWith('/api/v1/workspace/search?')) { searchReads++; return reply(result(searchReads > 2 ? 'FICTIONAL fresh result' : 'FICTIONAL old result')) }
    throw new Error('Unexpected test route')
  })
  vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); render(view())
  await user.click(await screen.findByRole('button', { name: 'Search workspace' }))
  const search = screen.getByRole('dialog', { name: 'Search workspace' })
  await screen.findByRole('link', { name: /FICTIONAL old result/ })
  await user.type(screen.getByRole('searchbox'), 'audit')
  await user.click(screen.getByRole('button', { name: 'Search' }))
  await waitFor(() => expect(searchReads).toBe(2))
  online.mockReturnValue(false); act(() => window.dispatchEvent(new Event('offline')))
  expect(screen.getByRole('dialog', { name: 'Search workspace' })).toBeVisible()
  expect(within(search).getByRole('status')).toHaveTextContent('Offline')
  expect(screen.queryByRole('link', { name: /FICTIONAL/ })).not.toBeInTheDocument()
  expect(screen.getByRole('searchbox')).toHaveValue('audit')
  expect(screen.getByRole('button', { name: 'Search' })).toBeDisabled()
  expect(searchReads).toBe(2)
  pendingNavigation = true; online.mockReturnValue(true)
  act(() => window.dispatchEvent(new Event('online')))
  expect(within(search).getByRole('status')).toHaveTextContent('Checking current search authorization')
  expect(screen.getByRole('button', { name: 'Search' })).toBeDisabled()
  expect(searchReads).toBe(2)
  await act(async () => resolveNavigation(reply(menu)))
  expect(await screen.findByRole('link', { name: /FICTIONAL fresh result/ })).toBeVisible()
  expect(searchReads).toBe(3)
  expect(fetch).toHaveBeenLastCalledWith('/api/v1/workspace/search?q=audit&offset=0&limit=25', expect.anything())
  expect(screen.getByRole('searchbox')).toHaveValue('audit')
})

it('closes the search shell immediately when the session owner changes', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => reply(url === '/api/v1/workspace' ? menu : result('FICTIONAL old owner result'))))
  const user = userEvent.setup(); const rendered = render(view())
  await user.click(await screen.findByRole('button', { name: 'Search workspace' }))
  await screen.findByRole('link', { name: /FICTIONAL old owner result/ })
  rendered.rerender(view({ ...session, actorId: '20000000-0000-0000-0000-000000000002' }))
  expect(screen.queryByRole('dialog', { name: 'Search workspace' })).not.toBeInTheDocument()
  expect(screen.queryByRole('link', { name: /FICTIONAL old owner result/ })).not.toBeInTheDocument()
})
