import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { WorkspaceRoleNavigation } from './WorkspaceRoleNavigation'
import { loadLenses, type Lenses } from './workspace-lens-api'
import type { Session } from './directory-contract'

vi.mock('./workspace-lens-api', async original => ({ ...await original<typeof import('./workspace-lens-api')>(), loadLenses: vi.fn() }))
const tenant = '10000000-0000-0000-0000-000000000001', actor = '10000000-0000-0000-0000-000000000002'
const vessel = '30000000-0000-0000-0000-000000000001', role = '20000000-0000-0000-0000-000000000001'
const session: Session = { authenticated: true, canRead: true, tenantId: tenant, actorId: actor, tenantName: null,
  setupRequired: false, canSearch: false, canReadCompanies: false, canCreate: false, csrfToken: 'synthetic-only' }
const result = (): Lenses => ({ tenantId: tenant, actorId: actor, vesselId: vessel, mode: 'Ship', state: 'Configured', version: 'synthetic-v1', canSearch: false,
  lenses: [{ reference: role, tr: 'KURGUSAL güverte', en: 'FICTIONAL deck', items: [{ key: 'crew', href: `/app/crew-roster/${vessel}`, group: 'daily' }] },
    { reference: '20000000-0000-0000-0000-000000000002', tr: 'KURGUSAL inceleme', en: 'FICTIONAL review', items: [{ key: 'audit', href: '/app/audit', group: 'review' }] }] })
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(yes => { resolve = yes }); return { promise, resolve } }
const props = { session, vessel, language: 'en' as const, onNavigate: vi.fn() }
beforeEach(() => { vi.mocked(loadLenses).mockResolvedValue(result()) })
afterEach(() => { vi.resetAllMocks(); vi.unstubAllGlobals(); vi.restoreAllMocks() })

describe('NAVIGATION-ROLE-LENS-01 shared presentation shell; all authored unverified', () => {
  it('requires explicit lens selection, simplifies daily work, groups bilingual labels and navigates through the owning guard', async () => {
    const user = userEvent.setup(), view = render(<WorkspaceRoleNavigation {...props} />)
    const lens = await screen.findByLabelText('Menu lens')
    expect(lens).toHaveValue(''); expect(screen.queryByRole('button', { name: 'Crew', exact: true })).not.toBeInTheDocument()
    await user.selectOptions(lens, role)
    await user.click(screen.getByRole('button', { name: 'Crew', exact: true }))
    expect(props.onNavigate).toHaveBeenCalledWith(`/app/crew-roster/${vessel}`)
    expect(screen.queryByRole('button', { name: 'Action history' })).not.toBeInTheDocument()
    view.rerender(<WorkspaceRoleNavigation {...props} language="tr" />)
    expect(screen.getByLabelText('Menü merceği')).toHaveValue(role)
    expect(screen.getByRole('button', { name: 'Mürettebat', exact: true })).toBeVisible()
    expect(loadLenses).toHaveBeenCalledTimes(1)
  })
  it('switches between genuine current roles without navigation or permission mutation', async () => {
    const user = userEvent.setup(); render(<WorkspaceRoleNavigation {...props} />)
    await user.selectOptions(await screen.findByLabelText('Menu lens'), role)
    await user.selectOptions(screen.getByLabelText('Menu lens'), result().lenses[1].reference)
    expect(screen.getByRole('button', { name: 'Action history' })).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Crew', exact: true })).not.toBeInTheDocument()
    expect(props.onNavigate).not.toHaveBeenCalled(); expect(loadLenses).toHaveBeenCalledTimes(1)
  })
  it.each(['Missing', 'Unavailable'] as const)('shows %s honestly without selecting a fallback role', async state => {
    vi.mocked(loadLenses).mockResolvedValue({ ...result(), state, version: null, lenses: [] })
    render(<WorkspaceRoleNavigation {...props} />)
    expect(await screen.findByText(state === 'Missing' ? 'No assigned role context.' : 'Role menu configuration unavailable.')).toBeVisible()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Vessel context' })).toBeVisible()
  })
  it('distinguishes configured mode exclusion from an empty authorized work intersection', async () => {
    vi.mocked(loadLenses).mockResolvedValue({ ...result(), lenses: [] })
    const view = render(<WorkspaceRoleNavigation {...props} />)
    expect(await screen.findByText('No role lens for this mode.')).toBeVisible()
    vi.mocked(loadLenses).mockResolvedValue({ ...result(), lenses: [{ ...result().lenses[0], items: [] }] })
    view.rerender(<WorkspaceRoleNavigation {...props} session={{ ...session }} />)
    await userEvent.setup().selectOptions(await screen.findByLabelText('Menu lens'), role)
    expect(screen.getByText('No available work in this lens and context.')).toBeVisible()
  })
  it('withdraws selection synchronously on refreshed session and never restores it from a late old response', async () => {
    const user = userEvent.setup(); const view = render(<WorkspaceRoleNavigation {...props} />)
    await user.selectOptions(await screen.findByLabelText('Menu lens'), role)
    const pending = deferred<Lenses>(); vi.mocked(loadLenses).mockReturnValue(pending.promise)
    view.rerender(<WorkspaceRoleNavigation {...props} session={{ ...session }} />)
    expect(screen.queryByRole('button', { name: 'Crew', exact: true })).not.toBeInTheDocument()
    await act(async () => { pending.resolve({ ...result(), lenses: [] }); await pending.promise })
    expect(await screen.findByText('No role lens for this mode.')).toBeVisible()
    expect(screen.queryByText('FICTIONAL deck')).not.toBeInTheDocument()
  })
  it('ignores an aborted successful old scope response after the new scope has already denied access', async () => {
    const first = deferred<Lenses>()
    vi.mocked(loadLenses).mockReturnValueOnce(first.promise).mockRejectedValueOnce(new Error('synthetic denial'))
    const view = render(<WorkspaceRoleNavigation {...props} />)
    view.rerender(<WorkspaceRoleNavigation {...props} vessel={role} />)
    await screen.findByText('Navigation unavailable.')
    await act(async () => { first.resolve(result()); await first.promise })
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Crew', exact: true })).not.toBeInTheDocument()
  })
  it('does not retain previous actor/tenant/vessel roles through logout or same-ID login', async () => {
    const view = render(<WorkspaceRoleNavigation {...props} />); await screen.findByLabelText('Menu lens')
    view.rerender(<WorkspaceRoleNavigation {...props} session={null} />)
    expect(screen.queryByRole('region')).not.toBeInTheDocument()
    const pending = deferred<Lenses>(); vi.mocked(loadLenses).mockReturnValue(pending.promise)
    view.rerender(<WorkspaceRoleNavigation {...props} />)
    expect(screen.queryByText('FICTIONAL deck')).not.toBeInTheDocument()
    view.rerender(<WorkspaceRoleNavigation {...props} session={{ ...session, actorId: role, tenantId: role }} vessel={role} />)
    await act(async () => { pending.resolve(result()); await pending.promise })
    // The wire decoder separately rejects wrong binding; no old selection can survive remount.
    expect(screen.queryByRole('button', { name: 'Crew', exact: true })).not.toBeInTheDocument()
  })
  it('focus revalidation withdraws all old menu and search results before accepting current grants', async () => {
    const user = userEvent.setup(); render(<WorkspaceRoleNavigation {...props} />)
    await user.selectOptions(await screen.findByLabelText('Menu lens'), role)
    const pending = deferred<Lenses>(); vi.mocked(loadLenses).mockReturnValue(pending.promise)
    act(() => window.dispatchEvent(new Event('focus')))
    expect(screen.queryByRole('button', { name: 'Crew', exact: true })).not.toBeInTheDocument()
    await act(async () => { pending.resolve({ ...result(), state: 'Missing', version: null, lenses: [] }); await pending.promise })
    expect(await screen.findByText('No assigned role context.')).toBeVisible()
  })
  it('keeps separately gated power search available without a configured role menu and closes it on revalidation', async () => {
    vi.mocked(loadLenses).mockResolvedValue({ ...result(), state: 'Unavailable', version: null, lenses: [], canSearch: true })
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => ({ items: [], offset: 0, limit: 25, hasMore: false }) })))
    const user = userEvent.setup(); render(<WorkspaceRoleNavigation {...props} />)
    await user.click(await screen.findByRole('button', { name: 'Search workspace', exact: true }))
    expect(screen.getByRole('dialog')).toBeVisible()
    vi.mocked(loadLenses).mockResolvedValue({ ...result(), canSearch: false })
    act(() => window.dispatchEvent(new Event('focus')))
    await screen.findByLabelText('Menu lens')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await user.keyboard('{Control>}k{/Control}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
  it('handles retry and preserves keyboard focus through presentation-only selection', async () => {
    vi.mocked(loadLenses).mockRejectedValueOnce(new Error('synthetic failure'))
    const user = userEvent.setup(); render(<WorkspaceRoleNavigation {...props} />)
    await screen.findByText('Navigation unavailable.')
    await user.click(screen.getByRole('button', { name: 'Reload menu' }))
    const menu = await screen.findByLabelText('Menu lens'); menu.focus()
    await user.selectOptions(menu, role); expect(menu).toHaveFocus()
    expect(within(screen.getByRole('navigation', { name: 'Daily work menu' })).getByText('Daily work')).toBeVisible()
  })
})
