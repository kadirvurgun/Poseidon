import { useEffect, useState } from 'react'
import { getSystemStatus } from './api'
import { AgentsDashboard } from './agents/AgentsDashboard'
import { ProductApp } from './product/ProductApp'

type Language = 'tr' | 'en'
type Connection = 'loading' | 'available' | 'error'
const content = {
  tr: {
    environment: 'GELİŞTİRME ORTAMI', page: 'Sistem bağlantısı', eyebrow: 'UYGULAMA TEMELİ / 01',
    heading: 'Önce sağlam bir bağlantı.', intro: 'POSEIDON uygulama altyapısının teknik bağlantı kontrolü.',
    loading: 'Bağlantı kontrol ediliyor', available: 'API bağlantısı kuruldu', error: 'API bağlantısı kurulamadı',
    loadingDetail: 'Servisten yanıt bekleniyor. Bu kontrol otomatik olarak tamamlanır.',
    availableDetail: 'Teknik durum servisi beklenen yanıtı verdi.',
    errorDetail: 'Servise ulaşılamadı veya beklenen yanıt alınamadı. Bağlantıyı yeniden kontrol edin.',
    retry: 'Tekrar dene', check: 'Yeniden kontrol et', technical: 'Yalnızca teknik bağlantı',
    scope: 'Bu durum, gemi veya römorkör operasyonuna hazır olunduğunu ya da bir emniyet kararının onaylandığını göstermez.',
    endpoint: 'Kontrol edilen uç nokta', protocol: 'Bağlantı sözleşmesi', expected: 'Beklenen servis / durum',
    phase: 'Uygulama temeli', phaseDetail: 'Kimlik, yetki ve operasyon akışları sonraki geliştirme adımlarında ele alınacak.',
    footer: 'Kontrollü geliştirme · APP-FOUNDATION-01', skip: 'Ana içeriğe geç', language: 'Dil',
  },
  en: {
    environment: 'DEVELOPMENT ENVIRONMENT', page: 'System connection', eyebrow: 'APPLICATION FOUNDATION / 01',
    heading: 'Start with a reliable connection.', intro: 'A technical connection check for the POSEIDON application foundation.',
    loading: 'Checking connection', available: 'API connection established', error: 'API connection unavailable',
    loadingDetail: 'Waiting for the service response. This check completes automatically.',
    availableDetail: 'The technical status service returned the expected response.',
    errorDetail: 'The service could not be reached or returned an unexpected response. Check the connection again.',
    retry: 'Retry', check: 'Check again', technical: 'Technical connection only',
    scope: 'This status does not indicate ship or tug operational readiness or approval of a safety decision.',
    endpoint: 'Checked endpoint', protocol: 'Connection contract', expected: 'Expected service / status',
    phase: 'Application foundation', phaseDetail: 'Identity, authorization and operational journeys will be addressed in later development steps.',
    footer: 'Controlled development · APP-FOUNDATION-01', skip: 'Skip to main content', language: 'Language',
  },
}

export function App() {
  if (window.location.pathname === '/app' || window.location.pathname.startsWith('/app/')) return <ProductApp />
  return window.location.pathname.replace(/\/$/, '') === '/agents' ? <AgentsDashboard /> : <ExistingApp />
}

export function ExistingApp() {
  const [language, setLanguage] = useState<Language>('tr')
  const [connection, setConnection] = useState<Connection>('loading')
  const [attempt, setAttempt] = useState(0)
  const t = content[language]

  useEffect(() => {
    document.documentElement.lang = language
    document.title = `POSEIDON · ${content[language].page}`
  }, [language])

  useEffect(() => {
    const controller = new AbortController()
    const timeout = setTimeout(() => controller.abort(), 10_000)
    let current = true
    getSystemStatus(controller.signal)
      .then(() => { if (current) setConnection('available') })
      .catch(() => { if (current) setConnection('error') })
      .finally(() => clearTimeout(timeout))
    return () => { current = false; clearTimeout(timeout); controller.abort() }
  }, [attempt])

  const retry = () => { setConnection('loading'); setAttempt(value => value + 1) }

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">{t.skip}</a>
      <aside className="sidebar" aria-label="POSEIDON">
        <a className="brand" href="#main" aria-label="POSEIDON"><span className="brand-mark" aria-hidden="true">P<span /></span><span>POSEIDON<small>MARITIME PLATFORM</small></span></a>
        <div className="sidebar-rule" />
        <div className="sidebar-section">WORKSPACE</div>
        <a className="nav-item" href="#main" aria-current="page"><span aria-hidden="true">◈</span>{t.page}</a>
        <a className="nav-item" href="/app">{language === 'tr' ? 'Şirket ve filo' : 'Companies and fleet'}</a>
        <a className="nav-item" href="/agents">{language === 'tr' ? 'Geliştirme operasyonları' : 'Development operations'}</a>
        <div className="sidebar-bottom"><span className="compass" aria-hidden="true">✧</span><p>{t.phase}</p><small>v0.1 · DEVELOPMENT</small></div>
      </aside>
      <div className="workspace">
        <header className="topbar"><span className="breadcrumb">POSEIDON <span aria-hidden="true">/</span> {t.page}</span><div className="topbar-actions"><span className="environment"><span />{t.environment}</span><div className="languages" role="group" aria-label={t.language}><button type="button" aria-pressed={language === 'tr'} onClick={() => setLanguage('tr')}>TR</button><button type="button" aria-pressed={language === 'en'} onClick={() => setLanguage('en')}>EN</button></div></div></header>
        <main id="main" tabIndex={-1}>
          <div className="page-heading"><p className="eyebrow">{t.eyebrow}</p><h1>{t.heading}</h1><p className="intro">{t.intro}</p></div>
          <section className={`connection-card connection-${connection}`} aria-labelledby="connection-title" data-testid="connection-card" data-state={connection}>
            <div className="connection-top"><span className={`connection-icon ${connection === 'loading' ? 'spinning' : ''}`} aria-hidden="true">{connection === 'loading' ? '◌' : connection === 'available' ? '↗' : '!'}</span><span className="card-tag">API / HTTP</span></div>
            <div role="status" aria-live="polite" aria-atomic="true"><h2 id="connection-title">{t[connection]}</h2><p className="connection-detail">{connection === 'loading' ? t.loadingDetail : connection === 'available' ? t.availableDetail : t.errorDetail}</p></div>
            <div className="endpoint"><span>{t.endpoint}</span><code>/api/v1/system/status</code></div>
            <button className="primary-button" type="button" onClick={retry} disabled={connection === 'loading'} data-testid="connection-retry">{connection === 'loading' ? t.loading : connection === 'error' ? t.retry : t.check}<span aria-hidden="true">↻</span></button>
          </section>
          <div className="context-grid"><section className="scope-note" aria-labelledby="scope-title"><span className="info-mark" aria-hidden="true">i</span><div><h2 id="scope-title">{t.technical}</h2><p>{t.scope}</p></div></section><section className="contract" aria-labelledby="contract-title"><h2 id="contract-title">{t.protocol}</h2><p>{t.expected}</p><code>poseidon <span>/</span> available</code></section></div>
          <div className="phase-note"><span aria-hidden="true">↳</span><p><strong>{t.phase}.</strong> {t.phaseDetail}</p></div>
        </main>
        <footer><span>{t.footer}</span><span className="footer-marker" aria-hidden="true">01 / FOUNDATION</span></footer>
      </div>
    </div>
  )
}
