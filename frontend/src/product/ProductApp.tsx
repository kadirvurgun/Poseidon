import { useEffect, useState } from 'react'
import * as api from './api'
import './product.css'
import { DirectoryEditor } from './DirectoryEditor'
import type { EditorTarget } from './DirectoryEditor'
import { DraftConfirmation } from './DraftConfirmation'
import { RegulatoryProfile } from './RegulatoryProfile'
import { editorCopy } from './editor-copy'
import { loginHref, readLocation, useWorkspaceLocation } from './workspace-location'
import { TodayWorkspace } from './TodayWorkspace'
import { WorkspaceSearch } from './WorkspaceSearch'

const copy = {
  tr: { today: 'Bugün', companies: 'Şirketler', fleet: 'Filo', signin: 'Oturum aç', signout: 'Oturumu kapat', loading: 'Yükleniyor…', retry: 'Tekrar dene', empty: 'Kayıt bulunamadı.', unavailable: 'Hizmete ulaşılamıyor. Lütfen tekrar deneyin.', forbidden: 'Bu çalışma alanına erişim yetkiniz yok.', expired: 'Devam etmek için oturum açın.', setup: 'Kurumsal oturum açma henüz yapılandırılmadı. Kuruluş yöneticinizle iletişime geçin.', tenant: 'Çalışma alanı', identity: 'Oturum', ship: 'Gemi', tug: 'Römorkör', all: 'Tüm araçlar', name: 'Ad', code: 'Kod', kind: 'Tür', next: 'Sonraki', previous: 'Önceki', close: 'Kapat', details: 'Ayrıntılar', skip: 'Ana içeriğe geç', failed: 'Oturum açma tamamlanamadı. Lütfen tekrar deneyin.', intro: 'Yetkili olduğunuz kayıtları görüntüleyin.', language: 'Dil', filter: 'Araç türü', page: 'Sayfa', technical: 'Sistem bağlantısı' },
  en: { today: 'Today', companies: 'Companies', fleet: 'Fleet', signin: 'Sign in', signout: 'Sign out', loading: 'Loading…', retry: 'Retry', empty: 'No records found.', unavailable: 'The service is unavailable. Please try again.', forbidden: 'You do not have access to this workspace.', expired: 'Sign in to continue.', setup: 'Organization sign-in has not been configured. Contact your organization administrator.', tenant: 'Workspace', identity: 'Session', ship: 'Ship', tug: 'Tug', all: 'All vessels', name: 'Name', code: 'Code', kind: 'Type', next: 'Next', previous: 'Previous', close: 'Close', details: 'Details', skip: 'Skip to main content', failed: 'Sign-in could not be completed. Please try again.', intro: 'Browse the records available in your workspace.', language: 'Language', filter: 'Vessel type', page: 'Page', technical: 'System connection' },
}
type State<T> = { state: 'loading' } | { state: 'ok'; data: T } | { state: 'error'; status: number }
const errorStatus = (error: unknown) => error instanceof api.ApiError ? error.status : 0

export function ProductApp() {
  const [language, setLanguage] = useState<'tr' | 'en'>('tr')
  const [identity, setIdentity] = useState<State<api.Session>>({ state: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [identityAttempt, setIdentityAttempt] = useState(0)
  const [offset, setOffset] = useState(0)
  const [kind, setKind] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [records, setRecords] = useState<State<api.Page<api.Company | api.Vessel>>>({ state: 'loading' })
  const [detail, setDetail] = useState<State<api.Company | api.Vessel>>({ state: 'loading' })
  const [logoutError, setLogoutError] = useState(false)
  const [editor, setEditor] = useState<EditorTarget | null>(null)
  const [drawer, setDrawer] = useState(false)
  const [powerSearch, setPowerSearch] = useState(false)
  const [offline, setOffline] = useState(!navigator.onLine)
  const [dirty, setDirty] = useState(false)
  const [saving, setSaving] = useState(false)
  const [pendingNavigation, setPendingNavigation] = useState<(() => void) | null>(null)
  const [notice, setNotice] = useState('')
  const [profileInstance, setProfileInstance] = useState(0)
  const t = copy[language]
  const et = editorCopy[language]
  const guard = (action: () => void) => {
    if (saving) return
    if (dirty) { setDrawer(false); setPendingNavigation(() => action) }
    else action()
  }
  const closeEditor = () => { setEditor(null); setDirty(false); setProfileInstance(v => v + 1) }
  const { route, move } = useWorkspaceLocation(guard, () => { closeEditor(); setDrawer(false); setPowerSearch(false); setDetail({ state: 'loading' }); setRecords({ state: 'loading' }); setOffset(0); setSearch(''); setSearchInput(''); setAttempt(v => v + 1); document.getElementById('product-main')?.focus() })
  const tab = route.tab
  const selected = route.selected
  const profileOpen = route.profile
  const setSelected = (id: string | null) => move({ tab, selected: id, profile: false })
  const setTab = (value: typeof tab) => move({ tab: value, selected: null, profile: false })
  const user = identity.state === 'ok' ? identity.data : null
  const canRead = user?.authenticated === true && user.canRead
  const heading = canRead && !route.invalid && (tab !== 'companies' || user?.canReadCompanies) ? t[tab] : t.tenant

  useEffect(() => {
    const update = () => setOffline(!navigator.onLine)
    window.addEventListener('online', update); window.addEventListener('offline', update)
    const unload = (event: BeforeUnloadEvent) => { if (dirty || saving) event.preventDefault() }
    window.addEventListener('beforeunload', unload)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update); window.removeEventListener('beforeunload', unload) }
  }, [dirty, saving])
  useEffect(() => {
    const key = (event: KeyboardEvent) => {
      if (user?.canSearch && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); setDrawer(false); setPowerSearch(true) }
      if (event.key === 'Escape') setDrawer(false)
    }
    window.addEventListener('keydown', key)
    return () => window.removeEventListener('keydown', key)
  }, [user?.canSearch])
  useEffect(() => {
    if (!drawer) return
    const previous = document.activeElement as HTMLElement | null
    const nav = document.getElementById('workspace-navigation')!
    const elements = () => Array.from(nav.querySelectorAll<HTMLElement>('button:not(:disabled), a[href]'))
    elements()[0]?.focus()
    const trap = (event: KeyboardEvent) => {
      if (event.key !== 'Tab') return
      const items = elements(); const first = items[0]; const last = items.at(-1)
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
      if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
    }
    nav.addEventListener('keydown', trap)
    return () => { nav.removeEventListener('keydown', trap); if (previous?.isConnected) previous.focus() }
  }, [drawer])

  useEffect(() => { document.documentElement.lang = language; document.title = `POSEIDON · ${heading}` }, [language, heading])
  useEffect(() => {
    const controller = new AbortController()
    let active = true
    const timeout = setTimeout(() => { controller.abort(); }, 10000)
    api.session(controller.signal).then(data => { if (active) { setIdentity({ state: 'ok', data }); if (window.location.pathname === '/app' && data.canRead && !data.canReadCompanies) move({ tab: 'fleet', selected: null, profile: false }, true) } }).catch(error => { if (active) setIdentity({ state: 'error', status: errorStatus(error) }) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [identityAttempt, move])
  useEffect(() => {
    if (tab === 'today' || !canRead || route.invalid || (tab === 'companies' && !user?.canReadCompanies)) return
    const controller = new AbortController()
    let active = true
    const timeout = setTimeout(() => { controller.abort(); }, 10000)
    const request = tab === 'companies' ? api.companies(offset, controller.signal, search) : api.vessels(offset, kind, controller.signal, search)
    request.then(data => { if (active) setRecords({ state: 'ok', data }) }).catch(error => { if (active) setRecords({ state: 'error', status: errorStatus(error) }) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [canRead, user?.canReadCompanies, route.invalid, tab, offset, kind, search, attempt])
  useEffect(() => {
    if (tab === 'today' || !selected || !canRead || (tab === 'companies' && !user?.canReadCompanies)) return
    const controller = new AbortController()
    let active = true
    const timeout = setTimeout(() => controller.abort(), 10000)
    const request = tab === 'companies' ? api.company(selected, controller.signal) : api.vessel(selected, controller.signal)
    request.then(data => { if (active) setDetail({ state: 'ok', data }) }).catch(error => { if (active) setDetail({ state: 'error', status: errorStatus(error) }) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [selected, tab, canRead, user?.canReadCompanies, attempt])

  const message = (status: number) => status === 401 ? t.expired : status === 403 ? t.forbidden : status === 404 ? t.empty : t.unavailable
  const retry = () => { setIdentity({ state: 'loading' }); setIdentityAttempt(v => v + 1); setRecords({ state: 'loading' }); setDetail({ state: 'loading' }); setAttempt(v => v + 1) }
  const navigate = (value: typeof tab) => { if (value === tab && !selected) { setDrawer(false); return }; guard(() => { closeEditor(); setDrawer(false); setNotice(''); setTab(value); setSearch(''); setSearchInput(''); setOffset(0); setRecords({ state: 'loading' }); setAttempt(v => v + 1); document.getElementById('product-main')?.focus() }) }
  const follow = (href: string) => {
    const next = href === '/app' && !user?.canReadCompanies ? { tab: 'fleet' as const, selected: null, profile: false } : readLocation(href)
    if (next.invalid || (next.tab === 'companies' && !user?.canReadCompanies)) return
    guard(() => { closeEditor(); move(next); setDrawer(false); setPowerSearch(false); setDetail({ state: 'loading' }); setRecords({ state: 'loading' }); setOffset(0); setSearch(''); setSearchInput(''); setAttempt(v => v + 1); document.getElementById('product-main')?.focus() })
  }
  const signout = async () => {
    if (!user) return
    // A normal form navigation follows the IdP logout redirect; the CSRF token stays in memory/DOM.
    const form = document.createElement('form')
    form.method = 'POST'; form.action = '/api/v1/auth/logout'
    const input = document.createElement('input'); input.type = 'hidden'; input.name = '__RequestVerificationToken'; input.value = user.csrfToken
    form.append(input); document.body.append(form)
    try { form.submit() } catch { form.remove(); setLogoutError(true) }
  }

  return <div className="product-shell" onClickCapture={event => {
    const anchor = (event.target as HTMLElement).closest('a')
    if (anchor && !anchor.getAttribute('href')?.startsWith('#') && (dirty || saving)) {
      event.preventDefault(); event.stopPropagation()
      const url = new URL(anchor.href)
      if (url.origin === window.location.origin && !readLocation(url.pathname).invalid) follow(url.pathname)
      else guard(() => { window.location.assign(anchor.href) })
    }
  }}>
    <a className="skip-link" href="#product-main">{t.skip}</a>
    <header className="product-header" inert={drawer}><a href="/app" className="product-brand">POSEIDON</a><button className="workspace-menu" aria-expanded={drawer} aria-controls="workspace-navigation" onClick={() => setDrawer(v => !v)}>{language === 'en' ? 'Menu' : 'Menü'}</button><span className="workspace-breadcrumb">{heading}</span>{user?.canSearch && <button onClick={() => setPowerSearch(true)}>{language === 'en' ? 'Search workspace' : 'Çalışma alanında ara'} <kbd>Ctrl K</kbd></button>}<div className="languages" role="group" aria-label={t.language}><button aria-pressed={language === 'tr'} onClick={() => setLanguage('tr')}>TR</button><button aria-pressed={language === 'en'} onClick={() => setLanguage('en')}>EN</button></div></header>
    <nav id="workspace-navigation" className={`product-nav ${drawer ? 'is-open' : ''}`} aria-label="POSEIDON" role={drawer ? "dialog" : undefined} aria-modal={drawer ? true : undefined}>{drawer && <button onClick={() => setDrawer(false)}>{t.close}</button>}{user?.canReadCompanies && <button aria-current={tab === 'companies' ? 'page' : undefined} onClick={() => navigate('companies')}>{t.companies}</button>}{canRead && <button aria-current={tab === 'today' ? 'page' : undefined} onClick={() => navigate('today')}>{t.today}</button>}{canRead && <button aria-current={tab === 'fleet' ? 'page' : undefined} onClick={() => navigate('fleet')}>{t.fleet}</button>}<a href="/">{t.technical}</a></nav>
    <main id="product-main" tabIndex={-1} inert={drawer}>
      <h1>{heading}</h1><p>{t.intro}</p>
      {offline && <p role="alert">{language === 'en' ? 'Offline. Displayed data is unverified; reconnect and retry.' : 'Çevrimdışı. Görüntülenen veriler doğrulanmadı; bağlanıp tekrar deneyin.'}</p>}
      {powerSearch && user?.canSearch && <WorkspaceSearch language={language} onClose={() => setPowerSearch(false)} onNavigate={follow} />}
      {canRead && (route.invalid || (tab === 'companies' && !user?.canReadCompanies)) && <p role="alert">{t.forbidden}</p>}
      {canRead && tab !== 'today' && selected && detail.state === 'ok' && 'kind' in detail.data && <div className="workspace-context"><span>{language === 'en' ? 'Current vessel' : 'Etkin araç'}: <strong>{detail.data.kind === 'Ship' ? t.ship : t.tug} · {detail.data.name}</strong></span><button disabled={saving} onClick={() => navigate('fleet')}>{language === 'en' ? 'Switch vessel' : 'Araç değiştir'}</button></div>}
      {identity.state === 'loading' && <p role="status">{t.loading}</p>}
      {identity.state === 'error' && <div role="alert"><p>{message(identity.status)}</p><button onClick={() => guard(() => { closeEditor(); retry() })}>{t.retry}</button></div>}
      {user && <section className="product-session" aria-label={t.identity}>
        {user.setupRequired ? <p role="status">{t.setup}</p> : !user.authenticated ? <><p>{t.expired}</p><a className="primary-button" href={loginHref()}>{t.signin}</a></> : <><p>{t.tenant}: <strong>{user.tenantName ?? user.tenantId ?? '—'}</strong></p><button onClick={() => guard(() => void signout())}>{t.signout}</button></>}
        {user.authenticated && !user.canRead && <p role="alert">{t.forbidden}</p>}
        {logoutError && <p role="alert">{t.unavailable}</p>}
        {new URLSearchParams(window.location.search).has('auth') && <p role="alert">{t.failed}</p>}
      </section>}
      {pendingNavigation && <DraftConfirmation language={language} onStay={() => setPendingNavigation(null)} onDiscard={() => {
        const action = pendingNavigation; setPendingNavigation(null); closeEditor(); action()
      }} />}
      {canRead && !route.invalid && tab === 'today' && <TodayWorkspace key={selected ?? 'select'} id={selected} language={language} offline={offline} onNavigate={follow} />}
      {canRead && tab !== 'today' && !route.invalid && (tab !== 'companies' || user?.canReadCompanies) && <>
        {notice && <p role="status" className="directory-notice">{notice}</p>}
        {user?.canCreate && <button className="primary-button" disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected: null, profile: false }); setNotice(''); setEditor({ instanceKey: crypto.randomUUID(), resource: tab === 'companies' ? 'companies' : 'vessels' }) })}>{et.create}</button>}
        {editor && user && <DirectoryEditor key={editor.instanceKey} target={editor} language={language} csrf={user.csrfToken} onDirty={setDirty} onSaving={setSaving}
          onCancel={() => guard(closeEditor)} onReload={() => guard(() => { closeEditor(); setDetail({ state: 'loading' }); setAttempt(v => v + 1) })}
          onSaved={record => { closeEditor(); setSelected(record.id); setDetail({ state: 'loading' }); setRecords({ state: 'loading' }); setOffset(0); setNotice(et.saved); setAttempt(v => v + 1) }} />}

        <form className="directory-search" onSubmit={event => { event.preventDefault(); guard(() => { closeEditor(); setSearch(searchInput.trim()); setOffset(0); setSelected(null); setRecords({ state: 'loading' }); setAttempt(v => v + 1) }) }}>
          <label htmlFor="directory-search">{language === 'tr' ? 'Kod veya ad ile ara' : 'Search by code or name'}</label>
          <div className="form-actions"><input id="directory-search" type="search" maxLength={100} value={searchInput} onChange={event => setSearchInput(event.target.value)} /><button type="submit" disabled={saving}>{language === 'tr' ? 'Ara' : 'Search'}</button></div>
        </form>
        {tab === 'fleet' && <label>{t.filter} <select value={kind} onChange={e => { const value = e.target.value; guard(() => { closeEditor(); setKind(value); setOffset(0); setSelected(null); setRecords({ state: 'loading' }) }) }}><option value="">{t.all}</option><option value="Ship">{t.ship}</option><option value="Tug">{t.tug}</option></select></label>}
        <section aria-label={t[tab]} aria-live="polite">
          {records.state === 'loading' && <p role="status">{t.loading}</p>}
          {records.state === 'error' && <div role="alert"><p>{message(records.status)}</p>{records.status === 401 ? <a href={loginHref()}>{t.signin}</a> : <button onClick={() => guard(() => { closeEditor(); retry() })}>{t.retry}</button>}</div>}
          {records.state === 'ok' && (records.data.items.length === 0 ? <p>{t.empty}</p> : <div className="product-table"><table><thead><tr><th>{t.code}</th><th>{t.name}</th>{tab === 'fleet' && <th>{t.kind}</th>}</tr></thead><tbody>{records.data.items.map(row => <tr key={row.id}><td>{row.code}</td><td><button onClick={() => { if (selected !== row.id) guard(() => { closeEditor(); setSelected(row.id); setDetail({ state: 'loading' }) }) }}>{row.name}</button></td>{'kind' in row && <td>{row.kind === 'Tug' ? t.tug : t.ship}</td>}</tr>)}</tbody></table></div>)}
          {records.state === 'ok' && <div className="product-pagination"><button disabled={offset === 0} onClick={() => guard(() => { closeEditor(); setOffset(v => Math.max(0, v - 25)); setRecords({ state: 'loading' }); setSelected(null) })}>{t.previous}</button><span>{t.page} {offset / 25 + 1}</span><button disabled={!records.data.hasMore} onClick={() => guard(() => { closeEditor(); setOffset(v => v + 25); setRecords({ state: 'loading' }); setSelected(null) })}>{t.next}</button></div>}
        </section>
        {selected && <section className="product-detail" aria-label={t.details}><h2>{t.details}</h2><button onClick={() => guard(() => { closeEditor(); setSelected(null) })}>{t.close}</button>{detail.state === 'loading' ? <p role="status">{t.loading}</p> : detail.state === 'error' ? <div role="alert"><p>{message(detail.status)}</p><button onClick={() => guard(() => { closeEditor(); retry() })}>{t.retry}</button></div> : <>{'kind' in detail.data && <button disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected, profile: true }) })}>{language === 'tr' ? 'Mevzuat profili' : 'Regulatory profile'}</button>}{detail.data.canEdit && <button disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected, profile: false }); setEditor({ instanceKey: crypto.randomUUID(), resource: tab === 'companies' ? 'companies' : 'vessels', record: detail.data }) })}>{et.edit}</button>}<dl><dt>{t.code}</dt><dd>{detail.data.code}</dd><dt>{t.name}</dt><dd>{detail.data.name}</dd>{'kind' in detail.data && <><dt>{t.kind}</dt><dd>{detail.data.kind === 'Tug' ? t.tug : t.ship}</dd></>}</dl></>}</section>}
        {profileOpen && selected && tab === 'fleet' && user && detail.state === 'ok' && <RegulatoryProfile key={`${selected}-${profileInstance}`} id={selected} language={language} csrf={user.csrfToken} onDirty={setDirty} onSaving={setSaving} />}
      </>}
    </main>
  </div>
}
