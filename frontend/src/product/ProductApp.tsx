import { useEffect, useState } from 'react'
import * as api from './api'
import './product.css'

const copy = {
  tr: { companies: 'Şirketler', fleet: 'Filo', signin: 'Oturum aç', signout: 'Oturumu kapat', loading: 'Yükleniyor…', retry: 'Tekrar dene', empty: 'Kayıt bulunamadı.', unavailable: 'Hizmete ulaşılamıyor. Lütfen tekrar deneyin.', forbidden: 'Bu çalışma alanına erişim yetkiniz yok.', expired: 'Devam etmek için oturum açın.', setup: 'Kurumsal oturum açma henüz yapılandırılmadı. Kuruluş yöneticinizle iletişime geçin.', tenant: 'Çalışma alanı', identity: 'Oturum', ship: 'Gemi', tug: 'Römorkör', all: 'Tüm araçlar', name: 'Ad', code: 'Kod', kind: 'Tür', next: 'Sonraki', previous: 'Önceki', close: 'Kapat', details: 'Ayrıntılar', skip: 'Ana içeriğe geç', failed: 'Oturum açma tamamlanamadı. Lütfen tekrar deneyin.', intro: 'Şirket ve filo kayıtlarınızı görüntüleyin.', language: 'Dil', filter: 'Araç türü', page: 'Sayfa', technical: 'Sistem bağlantısı' },
  en: { companies: 'Companies', fleet: 'Fleet', signin: 'Sign in', signout: 'Sign out', loading: 'Loading…', retry: 'Retry', empty: 'No records found.', unavailable: 'The service is unavailable. Please try again.', forbidden: 'You do not have access to this workspace.', expired: 'Sign in to continue.', setup: 'Organization sign-in has not been configured. Contact your organization administrator.', tenant: 'Workspace', identity: 'Session', ship: 'Ship', tug: 'Tug', all: 'All vessels', name: 'Name', code: 'Code', kind: 'Type', next: 'Next', previous: 'Previous', close: 'Close', details: 'Details', skip: 'Skip to main content', failed: 'Sign-in could not be completed. Please try again.', intro: 'Browse your company and fleet records.', language: 'Language', filter: 'Vessel type', page: 'Page', technical: 'System connection' },
}
type State<T> = { state: 'loading' } | { state: 'ok'; data: T } | { state: 'error'; status: number }
const errorStatus = (error: unknown) => error instanceof api.ApiError ? error.status : 0

export function ProductApp() {
  const [language, setLanguage] = useState<'tr' | 'en'>('tr')
  const [identity, setIdentity] = useState<State<api.Session>>({ state: 'loading' })
  const [tab, setTab] = useState<'companies' | 'fleet'>('companies')
  const [attempt, setAttempt] = useState(0)
  const [offset, setOffset] = useState(0)
  const [kind, setKind] = useState('')
  const [records, setRecords] = useState<State<api.Page<api.Company | api.Vessel>>>({ state: 'loading' })
  const [selected, setSelected] = useState<string | null>(null)
  const [detail, setDetail] = useState<State<api.Company | api.Vessel>>({ state: 'loading' })
  const [logoutError, setLogoutError] = useState(false)
  const t = copy[language]
  const user = identity.state === 'ok' ? identity.data : null
  const canRead = user?.authenticated === true && user.canRead

  useEffect(() => { document.documentElement.lang = language; document.title = `POSEIDON · ${copy[language][tab]}` }, [language, tab])
  useEffect(() => {
    const controller = new AbortController()
    let active = true
    const timeout = setTimeout(() => { controller.abort(); }, 10000)
    api.session(controller.signal).then(data => { if (active) setIdentity({ state: 'ok', data }) }).catch(error => { if (active) setIdentity({ state: 'error', status: errorStatus(error) }) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [attempt])
  useEffect(() => {
    if (!canRead) return
    const controller = new AbortController()
    let active = true
    const timeout = setTimeout(() => { controller.abort(); }, 10000)
    const request = tab === 'companies' ? api.companies(offset, controller.signal) : api.vessels(offset, kind, controller.signal)
    request.then(data => { if (active) setRecords({ state: 'ok', data }) }).catch(error => { if (active) setRecords({ state: 'error', status: errorStatus(error) }) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [canRead, tab, offset, kind, attempt])
  useEffect(() => {
    if (!selected || !canRead) return
    const controller = new AbortController()
    let active = true
    const timeout = setTimeout(() => controller.abort(), 10000)
    const request = tab === 'companies' ? api.company(selected, controller.signal) : api.vessel(selected, controller.signal)
    request.then(data => { if (active) setDetail({ state: 'ok', data }) }).catch(error => { if (active) setDetail({ state: 'error', status: errorStatus(error) }) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [selected, tab, canRead, attempt])

  const message = (status: number) => status === 401 ? t.expired : status === 403 ? t.forbidden : status === 404 ? t.empty : t.unavailable
  const retry = () => { setIdentity({ state: 'loading' }); setRecords({ state: 'loading' }); setDetail({ state: 'loading' }); setAttempt(v => v + 1) }
  const navigate = (value: typeof tab) => { if (value === tab) return; setTab(value); setOffset(0); setSelected(null); setRecords({ state: 'loading' }) }
  const signout = async () => {
    if (!user) return
    // A normal form navigation follows the IdP logout redirect; the CSRF token stays in memory/DOM.
    const form = document.createElement('form')
    form.method = 'POST'; form.action = '/api/v1/auth/logout'
    const input = document.createElement('input'); input.type = 'hidden'; input.name = '__RequestVerificationToken'; input.value = user.csrfToken
    form.append(input); document.body.append(form)
    try { form.submit() } catch { form.remove(); setLogoutError(true) }
  }

  return <div className="product-shell">
    <a className="skip-link" href="#product-main">{t.skip}</a>
    <header className="product-header"><a href="/app" className="product-brand">POSEIDON</a><div className="languages" role="group" aria-label={t.language}><button aria-pressed={language === 'tr'} onClick={() => setLanguage('tr')}>TR</button><button aria-pressed={language === 'en'} onClick={() => setLanguage('en')}>EN</button></div></header>
    <nav className="product-nav" aria-label="POSEIDON"><button aria-current={tab === 'companies' ? 'page' : undefined} onClick={() => navigate('companies')}>{t.companies}</button><button aria-current={tab === 'fleet' ? 'page' : undefined} onClick={() => navigate('fleet')}>{t.fleet}</button><a href="/">{t.technical}</a></nav>
    <main id="product-main" tabIndex={-1}>
      <h1>{t[tab]}</h1><p>{t.intro}</p>
      {identity.state === 'loading' && <p role="status">{t.loading}</p>}
      {identity.state === 'error' && <div role="alert"><p>{message(identity.status)}</p><button onClick={retry}>{t.retry}</button></div>}
      {user && <section className="product-session" aria-label={t.identity}>
        {user.setupRequired ? <p role="status">{t.setup}</p> : !user.authenticated ? <><p>{t.expired}</p><a className="primary-button" href="/api/v1/auth/login?returnUrl=%2Fapp">{t.signin}</a></> : <><p>{t.tenant}: <strong>{user.tenantName ?? user.tenantId ?? '—'}</strong></p><button onClick={() => void signout()}>{t.signout}</button></>}
        {user.authenticated && !user.canRead && <p role="alert">{t.forbidden}</p>}
        {logoutError && <p role="alert">{t.unavailable}</p>}
        {new URLSearchParams(window.location.search).has('auth') && <p role="alert">{t.failed}</p>}
      </section>}
      {canRead && <>
        {tab === 'fleet' && <label>{t.filter} <select value={kind} onChange={e => { setKind(e.target.value); setOffset(0); setSelected(null); setRecords({ state: 'loading' }) }}><option value="">{t.all}</option><option value="Ship">{t.ship}</option><option value="Tug">{t.tug}</option></select></label>}
        <section aria-label={t[tab]} aria-live="polite">
          {records.state === 'loading' && <p role="status">{t.loading}</p>}
          {records.state === 'error' && <div role="alert"><p>{message(records.status)}</p>{records.status === 401 ? <a href="/api/v1/auth/login?returnUrl=%2Fapp">{t.signin}</a> : <button onClick={retry}>{t.retry}</button>}</div>}
          {records.state === 'ok' && (records.data.items.length === 0 ? <p>{t.empty}</p> : <div className="product-table"><table><thead><tr><th>{t.code}</th><th>{t.name}</th>{tab === 'fleet' && <th>{t.kind}</th>}</tr></thead><tbody>{records.data.items.map(row => <tr key={row.id}><td>{row.code}</td><td><button onClick={() => { setSelected(row.id); setDetail({ state: 'loading' }) }}>{row.name}</button></td>{'kind' in row && <td>{row.kind === 'Tug' ? t.tug : t.ship}</td>}</tr>)}</tbody></table></div>)}
          {records.state === 'ok' && <div className="product-pagination"><button disabled={offset === 0} onClick={() => { setOffset(v => Math.max(0, v - 25)); setRecords({ state: 'loading' }); setSelected(null) }}>{t.previous}</button><span>{t.page} {offset / 25 + 1}</span><button disabled={!records.data.hasMore} onClick={() => { setOffset(v => v + 25); setRecords({ state: 'loading' }); setSelected(null) }}>{t.next}</button></div>}
        </section>
        {selected && <section className="product-detail" aria-label={t.details}><h2>{t.details}</h2><button onClick={() => setSelected(null)}>{t.close}</button>{detail.state === 'loading' ? <p role="status">{t.loading}</p> : detail.state === 'error' ? <div role="alert"><p>{message(detail.status)}</p><button onClick={retry}>{t.retry}</button></div> : <dl><dt>{t.code}</dt><dd>{detail.data.code}</dd><dt>{t.name}</dt><dd>{detail.data.name}</dd>{'kind' in detail.data && <><dt>{t.kind}</dt><dd>{detail.data.kind === 'Tug' ? t.tug : t.ship}</dd></>}</dl>}</section>}
      </>}
    </main>
  </div>
}
