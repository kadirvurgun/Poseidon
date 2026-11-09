import { openNavigationTestWorkView } from './workspace-navigation.test-fixture'
import { act, render, screen, within } from '@testing-library/react'
import type { ComponentProps } from 'react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { EnterpriseChrome } from './EnterpriseChrome'
import { loadLenses, type Lenses } from './workspace-lens-api'
import type { Session } from './directory-contract'

vi.mock('./WorkspaceContextBar', () => ({ WorkspaceContextBar: () => <div>Scoped context</div> }))
vi.mock('./workspace-lens-api', async original => ({ ...await original<typeof import('./workspace-lens-api')>(), loadLenses: vi.fn() }))
const tenant = '10000000-0000-0000-0000-000000000001', actor = '10000000-0000-0000-0000-000000000002'
const vessel = '30000000-0000-0000-0000-000000000001', deck = '20000000-0000-0000-0000-000000000001', engineer = '20000000-0000-0000-0000-000000000002'
const session: Session = { authenticated: true, canRead: true, actorId: actor, tenantId: tenant, tenantName: 'FICTIONAL workspace',
  csrfToken: 'synthetic-only', setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false }
const result = (): Lenses => ({ tenantId: tenant, actorId: actor, vesselId: vessel, mode: 'Ship', state: 'Configured', version: 'synthetic-menu-v1', canSearch: true,
  lenses: [{ reference: deck, en: 'FICTIONAL deck', tr: 'KURGUSAL güverte', items: [{ key: 'crew', href: `/app/crew-roster/${vessel}`, group: 'daily' }] },
    { reference: engineer, en: 'FICTIONAL engineer', tr: 'KURGUSAL mühendis', items: [{ key: 'maintenance', href: `/app/maintenance-plan/${vessel}`, group: 'daily' }] }] })
const props = { session, vessel, language: 'en' as const, onLanguageChange: vi.fn(), onNavigate: vi.fn(), title: 'Today', mainId: 'role-test-main' }
const view = (value: ComponentProps<typeof EnterpriseChrome> = props) => <div className="product-shell enterprise-shell"><EnterpriseChrome {...value} /><main id="role-test-main" /></div>
beforeEach(() => {
  vi.mocked(loadLenses).mockResolvedValue(result())
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ destinations: [{ key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' }], canSearch: false, canWriteRegulatory: false }), { headers: { 'Content-Type': 'application/json' } })))
})
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); vi.unstubAllGlobals(); history.replaceState(null, '', '/') })

it('DEV-022 selects the first genuine role and replaces the primary sidebar when a different lens is chosen', async () => {
  history.replaceState(null, '', `/app/crew-roster/${vessel}`)
  const user = userEvent.setup(); const rendered = render(view())
  const nav = screen.getByRole('navigation', { name: 'POSEIDON' })
  const crew = await within(nav).findByRole('button', { name: 'Crew' })
  expect(crew).toHaveAttribute('aria-current', 'page')
  await openNavigationTestWorkView(user)
  const lens = within(nav).getByRole('combobox', { name: 'Menu lens' })
  expect(lens).toHaveValue(deck); expect(lens).toBeVisible()
  expect(within(nav).queryByRole('button', { name: 'Fleet' })).not.toBeInTheDocument()
  expect(within(nav).queryByRole('button', { name: 'Maintenance plans' })).not.toBeInTheDocument()
  await user.click(crew)
  expect(props.onNavigate).toHaveBeenCalledWith(`/app/crew-roster/${vessel}`, expect.any(Function))
  await user.selectOptions(lens, engineer)
  expect(await within(nav).findByRole('button', { name: 'Maintenance plans' })).toBeVisible()
  expect(within(nav).queryByRole('button', { name: 'Crew' })).not.toBeInTheDocument()
  expect(props.onNavigate).toHaveBeenCalledTimes(1)
  rendered.rerender(view({ ...props, language: 'tr' }))
  expect(screen.getByRole('combobox', { name: 'Menü merceği' })).toHaveValue(engineer)
  expect(screen.getByRole('button', { name: 'Bakım planları' })).toBeVisible()
})

it('a presentation lens never grants full product search when the current workspace denies it', async () => {
  const user = userEvent.setup(); render(view())
  await screen.findByRole('button', { name: 'Crew' })
  await openNavigationTestWorkView(user)
  await user.selectOptions(screen.getByRole('combobox', { name: 'Menu lens' }), engineer)
  expect(screen.queryByRole('button', { name: 'Search workspace' })).not.toBeInTheDocument()
  await user.keyboard('{Control>}k{/Control}')
  expect(screen.queryByRole('dialog', { name: 'Search workspace' })).not.toBeInTheDocument()
})

it('withdraws old role items synchronously on session changes, including a late old response', async () => {
  let resolve!: (value: Lenses) => void
  const late = new Promise<Lenses>(yes => { resolve = yes })
  const rendered = render(view()); await screen.findByRole('button', { name: 'Crew' })
  vi.mocked(loadLenses).mockReturnValueOnce(late)
  rendered.rerender(view({ ...props, session: { ...session, actorId: engineer } }))
  expect(screen.queryByRole('button', { name: 'Crew' })).not.toBeInTheDocument()
  // Even a mocked transport resolving an old actor projection cannot resurrect
  // the previous selected lens; wire tests separately enforce exact actor binding.
  vi.mocked(loadLenses).mockResolvedValueOnce({ ...result(), actorId: deck, state: 'Missing', version: null, lenses: [] })
  rendered.rerender(view({ ...props, session: { ...session, actorId: deck } }))
  await screen.findByText('No assigned role context.')
  await act(async () => { resolve(result()); await late })
  expect(screen.queryByRole('button', { name: 'Crew' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Maintenance plans' })).not.toBeInTheDocument()
  expect(screen.queryByRole('combobox', { name: 'Menu lens' })).not.toBeInTheDocument()
})

it('keeps an empty role lens explicit without falling back to a broader authorized menu', async () => {
  vi.mocked(loadLenses).mockResolvedValue({ ...result(), lenses: [{ ...result().lenses[0], items: [] }] })
  render(view())
  expect(await screen.findByText('No available work in this lens and context.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Fleet' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Today' })).not.toBeInTheDocument()
})
