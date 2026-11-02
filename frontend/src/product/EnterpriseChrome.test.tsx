import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { EnterpriseChrome } from './EnterpriseChrome'
import type { Session } from './directory-contract'

vi.mock('./WorkspaceContextBar', () => ({ WorkspaceContextBar: () => <div>Scoped context</div> }))
vi.mock('./WorkspaceRoleNavigation', () => ({ WorkspaceRoleNavigation: () => <div>Scoped work view</div> }))
vi.mock('./WorkspaceSearch', () => ({ WorkspaceSearch: ({ onNavigate, onClose }: { onNavigate: (href: string) => void; onClose: () => void }) =>
  <div role="dialog" aria-label="Workspace search"><button onClick={() => onNavigate('/app/fleet')}>Go to fleet</button><button onClick={onClose}>Close search</button></div> }))
const tenant = '10000000-0000-0000-0000-000000000001', actor = '20000000-0000-0000-0000-000000000001'
const session = { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: true, setupRequired: false,
  tenantId: tenant, actorId: actor, tenantName: 'FICTIONAL workspace', csrfToken: 'synthetic' } as Session
const menu = { destinations: [{ key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' }], canSearch: true, canWriteRegulatory: false }
const response = (value: unknown) => new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } })
const props = { session, language: 'en' as const, onLanguageChange: vi.fn(), onNavigate: vi.fn(), title: 'Fleet', mainId: 'test-main' }
const view = (value = props) => <div className="product-shell enterprise-shell"><EnterpriseChrome {...value} /><main id="test-main"><button>Work action</button></main></div>
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); history.replaceState(null, '', '/') })

it('renders grouped server-authorized navigation with a current-page marker and no invented module entries', async () => {
  history.replaceState(null, '', '/app/fleet'); vi.stubGlobal('fetch', vi.fn(async () => response(menu)))
  render(view())
  const nav = screen.getByRole('navigation', { name: 'POSEIDON' })
  expect(await within(nav).findByRole('button', { name: 'Fleet' })).toHaveAttribute('aria-current', 'page')
  expect(within(nav).queryByRole('button', { name: 'Companies' })).not.toBeInTheDocument()
  expect(within(nav).queryByRole('button', { name: 'Job board' })).not.toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Skip to main content' })).toHaveAttribute('href', '#test-main')
})

it('withdraws the previous menu during an actor/tenant change and ignores a late old request', async () => {
  let finish!: (r: Response) => void; let signal: AbortSignal | undefined
  vi.stubGlobal('fetch', vi.fn((_url: string, init?: RequestInit) => {
    signal = init?.signal as AbortSignal
    return new Promise<Response>(resolve => { finish = resolve })
  }))
  const rendered = render(view()); await waitFor(() => expect(finish).toBeTypeOf('function'))
  const old = finish, oldSignal = signal
  rendered.rerender(view({ ...props, session: { ...session, actorId: '20000000-0000-0000-0000-000000000002' } }))
  expect(oldSignal?.aborted).toBe(true)
  await act(async () => old(response(menu)))
  expect(screen.queryByRole('button', { name: 'Fleet' })).not.toBeInTheDocument()
})

it('keeps the search dialog until the owning dirty-navigation guard actually commits the destination', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response(menu)))
  const navigate = vi.fn(); const user = userEvent.setup(); render(view({ ...props, onNavigate: navigate }))
  await user.click(await screen.findByRole('button', { name: /Search workspace/ }))
  await user.click(screen.getByRole('button', { name: 'Go to fleet' }))
  expect(screen.getByRole('dialog', { name: 'Workspace search' })).toBeVisible()
  expect(navigate).toHaveBeenCalledWith('/app/fleet', expect.any(Function))
  await act(async () => navigate.mock.calls[0][1]())
  expect(screen.queryByRole('dialog', { name: 'Workspace search' })).not.toBeInTheDocument()
})

it('supports mobile menu focus containment, Escape dismissal and focus restoration', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response(menu)))
  const user = userEvent.setup(); render(view())
  await screen.findByRole('button', { name: 'Fleet' })
  const trigger = screen.getByRole('button', { name: 'Menu' }); await user.click(trigger)
  const dialog = await screen.findByRole('dialog', { name: 'POSEIDON' })
  expect(dialog).toHaveAttribute('aria-modal', 'true')
  expect(document.getElementById('test-main')?.inert).toBe(true)
  await user.keyboard('{Escape}')
  await waitFor(() => expect(screen.queryByRole('dialog', { name: 'POSEIDON' })).not.toBeInTheDocument())
  expect(trigger).toHaveFocus(); expect(document.getElementById('test-main')?.inert).toBe(false)
})
