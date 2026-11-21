import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { EnterpriseChrome } from '../product/EnterpriseChrome'
import type { Session } from '../product/directory-contract'
import { changeAppLanguage } from '../i18n'

// USER-GUIDE-01 UG-1: UG-HELP-01 (open/close/keyboard/focus), UG-HELP-02 (no request on open),
// UG-HELP-04 (related pages limited to the caller's navigation), draft label, missing content, mobile.
vi.mock('../product/WorkspaceContextBar', () => ({ WorkspaceContextBar: () => <div>Scoped context</div> }))
vi.mock('../product/WorkspaceRoleNavigation', () => ({ WorkspaceRoleNavigation: () => <div>Scoped work view</div> }))
const session = { authenticated: true, canRead: true, canReadCompanies: true, canCreate: false, canSearch: false, setupRequired: false,
  tenantId: '10000000-0000-0000-0000-000000000001', actorId: '20000000-0000-0000-0000-000000000001', tenantName: 'FICTIONAL workspace', csrfToken: 'synthetic' } as Session
const restricted = { destinations: [{ key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' }], canSearch: false, canWriteRegulatory: false }
const response = (value: unknown) => new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } })

function mount(path: string, menu = restricted, language: 'en' | 'tr' | 'de' = 'en') {
  history.replaceState(null, '', path)
  const fetcher = vi.fn(async () => response(menu))
  vi.stubGlobal('fetch', fetcher)
  const onNavigate = vi.fn()
  render(<div className="product-shell enterprise-shell"><EnterpriseChrome session={session} language={language} onLanguageChange={vi.fn()} onNavigate={onNavigate} title="Page title" mainId="test-main"/>
    <main id="test-main"><label>Notes<input aria-label="Notes"/></label><button>Work action</button></main></div>)
  return { fetcher, onNavigate }
}
const trigger = (name = 'Open page guide') => screen.getByRole('button', { name })
async function openedGuide() {
  const dialog = await screen.findByRole('dialog', { name: /Help/ })
  await within(dialog).findByRole('heading', { name: 'What this page is for' })
  return dialog
}
afterEach(() => { vi.restoreAllMocks(); history.replaceState(null, '', '/') })

describe('page help panel', () => {
  it('opens beside the page title without any request, shows the draft label and returns focus on Escape', async () => {
    const { fetcher } = mount('/app/today')
    await screen.findByRole('button', { name: 'Today' })
    const before = fetcher.mock.calls.length
    const button = trigger()
    expect(button.closest('.enterprise-breadcrumb')).not.toBeNull()
    expect(button).toHaveAttribute('aria-keyshortcuts', 'Shift+?')
    await userEvent.click(button)
    const dialog = await openedGuide()
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(within(dialog).getByRole('note')).toHaveTextContent('Draft guide — awaiting expert review')
    expect(within(dialog).getByRole('tab', { name: 'Guide' })).toHaveAttribute('aria-selected', 'true')
    expect(within(dialog).getByRole('button', { name: 'Close help' })).toHaveFocus()
    // D2: opening and reading the guide starts no API request, command or AI call.
    expect(fetcher.mock.calls.length).toBe(before)
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(button).toHaveFocus())
    expect(fetcher.mock.calls.length).toBe(before)
  })

  it('opens with Shift+? but never while the user types in a field', async () => {
    mount('/app/today')
    await screen.findByRole('button', { name: 'Today' })
    const field = screen.getByRole('textbox', { name: 'Notes' })
    field.focus()
    fireEvent.keyDown(field, { key: '?', shiftKey: true })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    fireEvent.keyDown(document.body, { key: '?', shiftKey: true })
    await openedGuide()
  })

  it('traps Tab inside the panel and keeps the background inert', async () => {
    mount('/app/today')
    await screen.findByRole('button', { name: 'Today' })
    await userEvent.click(trigger())
    const dialog = await openedGuide()
    expect(screen.getByRole('button', { name: 'Work action', hidden: true }).closest('[inert]')).not.toBeNull()
    const close = within(dialog).getByRole('button', { name: 'Close help' })
    await userEvent.tab({ shift: true })
    expect(dialog.contains(document.activeElement)).toBe(true)
    for (let i = 0; i < 20; i++) { await userEvent.tab(); expect(dialog.contains(document.activeElement)).toBe(true) }
    close.focus()
    await userEvent.click(close)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Work action' }).closest('[inert]')).toBeNull()
  })

  it('lists only related pages the caller can open, never naming or counting hidden ones (D3)', async () => {
    const { onNavigate } = mount('/app/today')
    await screen.findByRole('button', { name: 'Today' })
    await userEvent.click(trigger())
    const dialog = await openedGuide()
    const related = within(dialog).getByRole('heading', { name: 'Related pages' }).closest('section')!
    expect(within(related).getAllByRole('link').map(link => link.textContent)).toEqual(['Fleet'])
    expect(related).not.toHaveTextContent(/Job board|Sources|Conflict|\d/)
    await userEvent.click(within(related).getByRole('link', { name: 'Fleet' }))
    expect(onNavigate).toHaveBeenCalledWith('/app/fleet', expect.any(Function))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('shows every granted related destination when the navigation contains them', async () => {
    mount('/app/today', { destinations: [{ key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' }, { key: 'tug-job-board', href: '/app/tug-job-board' }, { key: 'sources', href: '/app/sources' }], canSearch: false, canWriteRegulatory: false })
    await screen.findByRole('button', { name: 'Today' })
    await userEvent.click(trigger())
    const dialog = await openedGuide()
    const related = within(dialog).getByRole('heading', { name: 'Related pages' }).closest('section')!
    expect(within(related).getAllByRole('link')).toHaveLength(3)
  })

  it('shows a neutral pending state for a page without a guide and the Why/AI tabs make no call', async () => {
    const { fetcher } = mount('/app/fleet')
    await screen.findByRole('button', { name: 'Today' })
    const before = fetcher.mock.calls.length
    await userEvent.click(trigger())
    const dialog = await screen.findByRole('dialog', { name: /Help/ })
    expect(await within(dialog).findByText('The guide for this page is being prepared.')).toBeVisible()
    await userEvent.click(within(dialog).getByRole('tab', { name: 'Why' }))
    expect(within(dialog).getByText('This page has no separate Why view.')).toBeVisible()
    await userEvent.keyboard('{ArrowRight}')
    expect(within(dialog).getByRole('tab', { name: 'AI' })).toHaveAttribute('aria-selected', 'true')
    expect(within(dialog).getByRole('tab', { name: 'AI' })).toHaveFocus()
    expect(within(dialog).getByText(/makes no AI call/)).toBeVisible()
    expect(fetcher.mock.calls.length).toBe(before)
  })

  it('points the Why tab to the page own Why view on a page that has one', async () => {
    mount('/app/today')
    await screen.findByRole('button', { name: 'Today' })
    await userEvent.click(trigger())
    const dialog = await openedGuide()
    await userEvent.click(within(dialog).getByRole('tab', { name: 'Why' }))
    expect(within(dialog).getByText(/never starts that read itself/)).toBeVisible()
  })

  it('uses the full-page layout at phone width', async () => {
    vi.stubGlobal('matchMedia', vi.fn((query: string) => ({ matches: query === '(max-width: 767px)', media: query, addEventListener: vi.fn(), removeEventListener: vi.fn() })))
    mount('/app/today')
    await screen.findByRole('button', { name: 'Today' })
    await userEvent.click(trigger())
    const dialog = await openedGuide()
    expect(dialog).toHaveClass('help-panel--page')
    expect(dialog).not.toHaveClass('help-panel--side')
  })

  it('uses the side layout on desktop and renders the Turkish guide', async () => {
    mount('/app/today', restricted, 'tr')
    await screen.findByRole('button', { name: 'Bugün' })
    await userEvent.click(trigger('Sayfa kılavuzunu aç'))
    const dialog = await screen.findByRole('dialog', { name: /Yardım/ })
    expect(await within(dialog).findByRole('heading', { name: 'Bu sayfa ne işe yarar' })).toBeVisible()
    expect(dialog).toHaveClass('help-panel--side')
    expect(within(dialog).getByRole('note')).toHaveTextContent('Taslak kılavuz — uzman onayı bekliyor')
  })

  it('falls back to the English guide for a locale without content and says so', async () => {
    await act(async () => { await changeAppLanguage('de', false) })
    mount('/app/today', restricted, 'de')
    await userEvent.click(await screen.findByRole('button', { name: 'Seitenanleitung öffnen' }))
    const dialog = await screen.findByRole('dialog', { name: /Hilfe/ })
    expect(await within(dialog).findByRole('heading', { name: 'What this page is for' })).toBeVisible()
    expect(within(dialog).getByText(/die englische Fassung wird angezeigt/)).toBeVisible()
    expect(within(dialog).getByRole('article')).toHaveAttribute('lang', 'en')
  })
})
