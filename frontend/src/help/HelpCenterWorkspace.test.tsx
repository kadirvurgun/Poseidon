import { act, cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { EnterpriseChrome } from '../product/EnterpriseChrome'
import type { Session } from '../product/directory-contract'
import type { EnterpriseDestination } from '../product/enterprise-navigation'
import { HelpCenter, HelpCenterWorkspace } from './HelpCenterWorkspace'

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
  render(<HelpCenter language="en" access="allowed" online navigation={ready()} onNavigate={onNavigate} {...props}/>)
  return { onNavigate, fetcher }
}
afterEach(async () => {
  vi.restoreAllMocks(); vi.unstubAllGlobals(); history.replaceState(null, '', '/')
  await act(async () => { await changeAppLanguage('en', false) })
})

describe('help center', () => {
  it('groups only the granted pages by menu section and never names a hidden page', async () => {
    const { fetcher } = mount()
    const list = await screen.findByRole('region', { name: 'Page guides' }, guideLoad)
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
    const list = await screen.findByRole('region', { name: 'Page guides' }, guideLoad)
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
    // a11y: the clicked link unmounted; focus moves to the opened guide's heading.
    expect(screen.getByRole('heading', { level: 2, name: 'Fleet' })).toHaveFocus()
    // Related pages are limited to the granted ones: fleet relates to today (granted) and to
    // companies, certificate-command-center, data-trust-center, departure-signoff (hidden).
    const related = screen.getByRole('heading', { name: 'Related pages' }).closest('section')!
    expect(within(related).getAllByRole('link').map(link => [link.textContent, link.getAttribute('href')])).toEqual([['Today', '/app/today']])
    for (const hidden of ['Companies', 'Data trust center', 'Departure sign-off']) expect(within(related).queryByText(hidden, { exact: false })).toBeNull()
    const open = screen.getByRole('link', { name: 'Open this page' })
    expect(open).toHaveAttribute('href', '/app/fleet')
    await userEvent.click(open)
    expect(onNavigate).toHaveBeenCalledWith('/app/fleet')
    await userEvent.click(screen.getByRole('button', { name: 'All guides' }))
    expect(window.location.search).toBe('')
    expect(await screen.findByRole('region', { name: 'Page guides' })).toBeVisible()
    expect(screen.getByRole('heading', { level: 2, name: 'Page guides' })).toHaveFocus()
  })

  it('honours a ?page= deep link only for a granted page and does not reveal whether a hidden one exists', async () => {
    mount({}, '/app/help?page=today')
    expect(await screen.findByRole('heading', { name: 'What this page is for' }, guideLoad)).toBeVisible()
    expect(screen.getByRole('link', { name: 'Open this page' })).toHaveAttribute('href', '/app/today')
  })

  it('renders a hidden page key exactly like an unknown address', async () => {
    const markup = async (key: string) => {
      mount({}, `/app/help?page=${key}`)
      await screen.findByText('No guide to show for this address.', {}, guideLoad)
      const html = document.querySelector('.help-center')!.outerHTML.replace(/_r_[a-z0-9]+_|:r[a-z0-9]+:|«r[a-z0-9]+»/g, 'ID')
      expect(document.body).not.toHaveTextContent('Users and permissions')
      expect(screen.queryByRole('link', { name: 'Open this page' })).toBeNull()
      cleanup()
      return html
    }
    expect(await markup('user-admin')).toBe(await markup('no-such-page-zz'))
  })

  it('lists nothing while the navigation is unknown, failed, signed out or offline', () => {
    const { unmount } = render(<HelpCenter language="en" access="allowed" online navigation={{ destinations: [], loading: true, failed: false }} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('status')).toHaveTextContent('Loading guides…'); unmount()
    const failed = render(<HelpCenter language="en" access="allowed" online navigation={{ destinations: [], loading: false, failed: true }} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('alert')).toHaveTextContent('Your page list could not be loaded'); failed.unmount()
    const out = render(<HelpCenter language="en" access="signedOut" online navigation={ready([])} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('alert')).toHaveTextContent('Sign in'); out.unmount()
    // A failed session read and a reader without access get their own honest message, not "Sign in".
    const unavailable = render(<HelpCenter language="en" access="unavailable" online navigation={ready()} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('alert')).toHaveTextContent('Your session could not be checked'); unavailable.unmount()
    const denied = render(<HelpCenter language="en" access="denied" online navigation={ready()} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('alert')).toHaveTextContent('no read access'); denied.unmount()
    render(<HelpCenter language="en" access="allowed" online={false} navigation={ready()} onNavigate={vi.fn()}/>)
    expect(screen.getByRole('status')).toHaveTextContent('You are offline')
    expect(screen.queryAllByRole('link')).toHaveLength(0)
    // Offline: the PDFs are not in the offline cache, so no download link is offered.
    expect(screen.getByRole('heading', { name: 'User manual' })).toBeVisible()
    expect(screen.getByText('The manual can be downloaded when you are back online.')).toBeVisible()
    expect(screen.queryByTestId('help-center-manual-en')).toBeNull()
  })

  it('offers the TR and EN manual PDFs with size and a draft label to a signed-in reader only', async () => {
    const { fetcher } = mount()
    const manual = screen.getByRole('region', { name: 'User manual' })
    const en = within(manual).getByRole('link', { name: /^User manual, English \(PDF, \d+(\.\d)? MB\)$/ })
    const tr = within(manual).getByRole('link', { name: /^User manual, Turkish \(PDF, \d+(\.\d)? MB\)$/ })
    expect(en).toHaveAttribute('download', 'poseidon-user-manual-en.pdf')
    expect(tr).toHaveAttribute('download', 'poseidon-user-manual-tr.pdf')
    expect(en).toHaveAttribute('type', 'application/pdf')
    expect(en.getAttribute('href')).toMatch(/poseidon-user-manual-en.*\.pdf$/)
    // The reader language comes first; reviewed:false keeps a draft note and a draft badge on each file.
    expect(within(manual).getAllByRole('link').map(link => link.getAttribute('hreflang'))).toEqual(['en', 'tr'])
    expect(within(manual).getByTestId('help-center-manual-draft')).toHaveTextContent('has not been reviewed by an expert')
    expect(within(manual).getAllByText('Draft')).toHaveLength(2)
    expect(within(manual).queryByText(/Turkish and English only/)).toBeNull()
    expect(screen.queryByText(/reviewed and approved|approved manual/i)).toBeNull()
    expect(fetcher).not.toHaveBeenCalled()
    cleanup()
    // Not a signed-in reader: no manual section, no link.
    for (const access of ['signedOut', 'denied', 'unavailable', 'pending'] as const) {
      const view = render(<HelpCenter language="en" access={access} online navigation={ready()} onNavigate={vi.fn()}/>)
      expect(screen.queryByRole('region', { name: 'User manual' })).toBeNull()
      expect(screen.queryByTestId('help-center-manual-en')).toBeNull()
      view.unmount()
    }
  })

  it('offers TR first in Turkish and notes TR/EN only for other languages', async () => {
    await act(async () => { await changeAppLanguage('tr', false) })
    const tr = render(<HelpCenter language="tr" access="allowed" online navigation={ready()} onNavigate={vi.fn()}/>)
    const manual = screen.getByRole('region', { name: 'Kullanım kılavuzu' })
    expect(within(manual).getAllByRole('link').map(link => link.getAttribute('hreflang'))).toEqual(['tr', 'en'])
    expect(within(manual).getByRole('link', { name: /^Kullanım kılavuzu, Türkçe \(PDF, \d+(,\d)? MB\)$/ })).toBeVisible()
    tr.unmount()
    await act(async () => { await changeAppLanguage('de', false) })
    render(<HelpCenter language="de" access="allowed" online navigation={ready()} onNavigate={vi.fn()}/>)
    const de = screen.getByRole('region', { name: 'Benutzerhandbuch' })
    expect(within(de).getByText('Das Handbuch ist nur auf Türkisch und Englisch verfügbar.')).toBeVisible()
    expect(within(de).getAllByRole('link').map(link => link.getAttribute('hreflang'))).toEqual(['en', 'tr'])
  })

  it('shows the machine-drafted, not-reviewed notice for a draft locale and never for EN', async () => {
    await act(async () => { await changeAppLanguage('de', false) })
    mount({ language: 'de' })
    expect(screen.getByTestId('help-center-translation-unreviewed')).toHaveTextContent('nicht geprüft')
    const list = await screen.findByRole('region', { name: 'Seitenanleitungen' }, guideLoad)
    expect(await within(list).findByRole('link', { name: 'Heute' }, guideLoad)).toBeVisible()
    expect(within(list).getAllByText('Entwurf').length).toBeGreaterThan(0)
  })

  it('says no page has a guide when granted pages have none, not that the search found nothing', async () => {
    mount({ navigation: ready([{ key: 'equipment', href: '/app/equipment' }]) })
    expect(await screen.findByText('None of your pages has a guide to show.', {}, guideLoad)).toBeVisible()
    expect(screen.queryByText('No guide matches your search.')).toBeNull()
  })

  it('announces only the result count while searching', async () => {
    mount()
    const list = await screen.findByRole('region', { name: 'Page guides' }, guideLoad)
    await within(list).findByRole('link', { name: 'Today' }, guideLoad)
    expect(within(list).getByRole('status')).toHaveTextContent('Guides shown: 4')
    expect(list.querySelector('[aria-live]')).toBeNull()
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search guides' }), 'fleet')
    expect(within(list).getByRole('status').textContent).toMatch(/^Guides shown: \d+$/)
  })

  it('has no draft-locale notice in English', () => {
    mount()
    expect(screen.queryByTestId('help-center-translation-unreviewed')).toBeNull()
  })
})

describe('help center workspace', () => {
  it('follows online/offline events instead of reading the connection once', async () => {
    let online = false
    vi.spyOn(navigator, 'onLine', 'get').mockImplementation(() => online)
    history.replaceState(null, '', '/app/help')
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      const body = url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, setupRequired: false,
        tenantId: '10000000-0000-0000-0000-000000000001', actorId: '20000000-0000-0000-0000-000000000001', tenantName: 'FICTIONAL workspace', csrfToken: 'synthetic' }
        : { destinations: [{ key: 'today', href: '/app/today' }], canSearch: false, canWriteRegulatory: false }
      return new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } })
    }))
    // The workspace uses the app language (TR by default).
    render(<HelpCenterWorkspace/>)
    expect(screen.getByText(/Çevrimdışısınız/)).toBeVisible()
    online = true
    act(() => { window.dispatchEvent(new Event('online')) })
    const list = await screen.findByRole('region', { name: 'Sayfa kılavuzları' }, guideLoad)
    expect(await within(list).findByRole('link', { name: 'Bugün' }, guideLoad)).toBeVisible()
    online = false
    act(() => { window.dispatchEvent(new Event('offline')) })
    expect(screen.getByText(/Çevrimdışısınız/)).toBeVisible()
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

  it('is not offered before a signed-in reader is known', () => {
    vi.stubGlobal('fetch', vi.fn())
    render(<div className="product-shell enterprise-shell"><EnterpriseChrome session={null} language="en" onLanguageChange={vi.fn()} onNavigate={vi.fn()} title="Help" mainId="m"/><main id="m"/></div>)
    expect(screen.queryByRole('link', { name: 'Help center' })).toBeNull()
  })
})
