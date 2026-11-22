import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { EnterpriseChrome } from '../product/EnterpriseChrome'
import type { Session } from '../product/directory-contract'
import type { EnterpriseDestination } from '../product/enterprise-navigation'
import { HelpCenter } from './HelpCenterWorkspace'

// HELP-CENTER-01: /app/help search, grouping by menu section, reviewed:false + machine-draft
// notices, permission filtering (D3 / 65.03 p50) and deep links to the page.
vi.mock('../product/WorkspaceContextBar', () => ({ WorkspaceContextBar: () => <div>Scoped context</div> }))
vi.mock('../product/WorkspaceRoleNavigation', () => ({ WorkspaceRoleNavigation: () => <div>Scoped work view</div> }))

const guideLoad = { timeout: 10000 }
const granted: EnterpriseDestination[] = [
  { key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' },
  { key: 'tug-job-board', href: '/app/tug-job-board' }, { key: 'sources', href: '/app/sources' },
]
const ready = (destinations = granted) => ({ destinations, loading: false, failed: false })

function mount(props: Partial<Parameters<typeof HelpCenter>[0]> = {}, path = '/app/help') {
  history.replaceState(null, '', path)
  const onNavigate = vi.fn()
  const fetcher = vi.fn()
  vi.stubGlobal('fetch', fetcher)
  render(<HelpCenter language="en" signedIn online navigation={ready()} onNavigate={onNavigate} {...props}/>)
  return { onNavigate, fetcher }
}
afterEach(async () => {
  vi.restoreAllMocks(); vi.unstubAllGlobals(); history.replaceState(null, '', '/')
  await act(async () => { await changeAppLanguage('en', false) })
})

describe('help center', () => {
  it('groups only the granted pages by menu section and never names a hidden page', async () => {
    const { fetcher } = mount()
    const list = await screen.findByRole('region', { name: 'Page guides' })
    expect(await within(list).findByRole('link', { name: 'Today' }, guideLoad)).toBeVisible()
    const headings = within(list).getAllByRole('heading', { level: 3 }).map(item => item.textContent)
    expect(headings).toHaveLength(3) // overview, tug, assurance: sidebar section order
    const overview = within(list).getAllByRole('region')[0]
    expect(within(overview).getAllByRole('link').map(link => link.textContent)).toEqual(['Today', 'Fleet'])
    expect(within(list).getByRole('link', { name: 'Job board' })).toBeVisible()
    expect(within(list).getByRole('link', { name: 'Source drafts' })).toBeVisible()
    // D3: a page outside the caller's navigation contributes nothing.
    expect(within(list).queryByRole('link', { name: 'Users and permissions' })).toBeNull()
    expect(within(list).getAllByRole('link')).toHaveLength(4)
    // reviewed:false is labelled on every entry; nothing claims review.
    expect(within(list).getAllByText('Draft')).toHaveLength(4)
    // D2: listing guides issues no API request.
    expect(fetcher).not.toHaveBeenCalled()
  })

  it('searches titles and guide text with locale folding and reports no match', async () => {
    mount()
    const list = await screen.findByRole('region', { name: 'Page guides' })
    await within(list).findByRole('link', { name: 'Today' }, guideLoad)
    const search = screen.getByRole('searchbox', { name: 'Search guides' })
    await userEvent.type(search, 'JOB BOARD')
    expect(within(list).getAllByRole('link').map(link => link.textContent)).toEqual(['Job board'])
    await userEvent.clear(search)
    await userEvent.type(search, 'zzzz-no-guide')
    expect(within(list).queryAllByRole('link')).toHaveLength(0)
    expect(screen.getByText('No guide matches your search.')).toBeVisible()
    await userEvent.type(search, '{Escape}')
    expect(within(list).getAllByRole('link')).toHaveLength(4)
  })

  it('opens a guide with the shared panel renderer, keeps it in the URL and deep links to the page', async () => {
    const { onNavigate } = mount()
    await userEvent.click(await screen.findByRole('link', { name: 'Fleet' }, guideLoad))
    expect(window.location.search).toBe('?page=fleet')
    expect(screen.getByRole('heading', { level: 2, name: 'Fleet' })).toBeVisible()
    expect(screen.getByRole('heading', { name: 'What this page is for' })).toBeVisible()
    expect(screen.getByRole('article')).toHaveTextContent('Draft guide — awaiting expert review')
    // Related pages are limited to the granted ones (today is related to fleet and granted).
    const related = screen.getByRole('heading', { name: 'Related pages' }).closest('section')!
    for (const link of within(related).queryAllByRole('link')) expect(granted.map(item => item.href)).toContain(link.getAttribute('href'))
    const open = screen.getByRole('link', { name: 'Open this page' })
    expect(open).toHaveAttribute('href', '/app/fleet')
    await userEvent.click(open)
    expect(onNavigate).toHaveBeenCalledWith('/app/fleet')
    await userEvent.click(screen.getByRole('button', { name: 'All guides' }))
    expect(window.location.search).toBe('')
    expect(await screen.findByRole('region', { name: 'Page guides' })).toBeVisible()
  })

  it('honours a ?page= deep link only for a granted page and does not reveal whether a hidden one exists', async () => {
    mount({}, '/app/help?page=today')
    expect(await screen.findByRole('heading', { name: 'What this page is for' }, guideLoad)).toBeVisible()
    expect(screen.getByRole('link', { name: 'Open this page' })).toHaveAttribute('href', '/app/today')
  })

  it('treats a hidden page key like an unknown address', async () => {
    mount({}, '/app/help?page=user-admin')
    expect(await screen.findByText('No guide to show for this address.', {}, guideLoad)).toBeVisible()
    expect(screen.queryByText('Users and permissions')).toBeNull()
    expect(screen.queryByRole('link', { name: 'Open this page' })).toBeNull()
  })

  it('lists nothing while the navigation is unknown, failed, signed out or offline', () => {
    const { unmount } = render(<HelpCenter language="en" signedIn online navigation={{ destinations: [], loading: true, failed: false }} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('status')).toHaveTextContent('Loading guides…'); unmount()
    const failed = render(<HelpCenter language="en" signedIn online navigation={{ destinations: [], loading: false, failed: true }} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('alert')).toHaveTextContent('Your page list could not be loaded'); failed.unmount()
    const out = render(<HelpCenter language="en" signedIn={false} online navigation={ready([])} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('alert')).toHaveTextContent('Sign in'); out.unmount()
    render(<HelpCenter language="en" signedIn online={false} navigation={ready()} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('status')).toHaveTextContent('You are offline')
    expect(screen.queryAllByRole('link')).toHaveLength(0)
    // The manual is described honestly: not bundled in this build.
    expect(screen.getByRole('heading', { name: 'User manual' })).toBeVisible()
    expect(screen.getByText(/not included in this build/)).toBeVisible()
  })

  it('shows the machine-drafted, not-reviewed notice for a draft locale and never for EN', async () => {
    await act(async () => { await changeAppLanguage('de', false) })
    mount({ language: 'de' })
    expect(screen.getByTestId('help-center-translation-unreviewed')).toHaveTextContent('nicht geprüft')
    const list = await screen.findByRole('region', { name: 'Seitenanleitungen' })
    expect(await within(list).findByRole('link', { name: 'Heute' }, guideLoad)).toBeVisible()
    expect(within(list).getAllByText('Entwurf').length).toBeGreaterThan(0)
  })

  it('has no draft-locale notice in English', () => {
    mount()
    expect(screen.queryByTestId('help-center-translation-unreviewed')).toBeNull()
  })
})

describe('help center navigation entry', () => {
  it('is reachable from the sidebar for any signed-in user without a server destination', async () => {
    history.replaceState(null, '', '/app/today')
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ destinations: [{ key: 'today', href: '/app/today' }], canSearch: false, canWriteRegulatory: false }), { headers: { 'Content-Type': 'application/json' } })))
    const session = { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, setupRequired: false,
      tenantId: '10000000-0000-0000-0000-000000000001', actorId: '20000000-0000-0000-0000-000000000001', tenantName: 'FICTIONAL workspace', csrfToken: 'synthetic' } as Session
    const onNavigate = vi.fn()
    render(<div className="product-shell enterprise-shell"><EnterpriseChrome session={session} language="en" onLanguageChange={vi.fn()} onNavigate={onNavigate} title="Today" mainId="m"/><main id="m"/></div>)
    const link = screen.getByRole('link', { name: 'Help center' })
    expect(link).toHaveAttribute('href', '/app/help')
    await userEvent.click(link)
    expect(onNavigate).toHaveBeenCalledWith('/app/help', expect.any(Function))
  })
})
