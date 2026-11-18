import { useEffect, useState } from 'react'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterprisePageHeader, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns'
import * as api from './api'
import { isGuid } from './identifiers'
import { loadSystemHealth, type SystemHealth } from './system-health-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import { decodeTodayVessels } from './today-api'
import './offline-enterprise.css'
import './product.css'

function selection() {
  const query = new URLSearchParams(window.location.search), values = query.getAll('vessel')
  if ([...query.keys()].some(key => key !== 'vessel') || values.length > 1 || values.length === 1 && !isGuid(values[0])) return { invalid: true, vessel: '' }
  return { invalid: false, vessel: values[0]?.toLowerCase() ?? '' }
}
export function SystemHealthWorkspace() {
  const [language, setLanguage] = useState<'tr' | 'en'>('tr'), en = language === 'en'
  const [route] = useState(selection), [vessel, setVessel] = useState(route.vessel), [offset, setOffset] = useState(0), [attempt, retry] = useState(0)
  const epoch = useWorkspaceRevalidation()
  const [identity, setIdentity] = useState<{ epoch: number; attempt: number; user?: api.Session; error?: boolean } | null>(null)
  const [page, setPage] = useState<{ epoch: number; attempt: number; offset: number; rows?: api.Page<api.Vessel>; error?: boolean } | null>(null)
  const online = navigator.onLine, visible = document.visibilityState !== 'hidden'
  const currentIdentity = identity?.epoch === epoch && identity.attempt === attempt ? identity : null
  const user = currentIdentity?.user ?? null
  const currentPage = page?.epoch === epoch && page.attempt === attempt && page.offset === offset ? page : null
  useEffect(() => {
    if (route.invalid || !online || !visible) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    api.session(c.signal).then(user => { if (active) setIdentity({ epoch, attempt, user }) }).catch(() => { if (active) setIdentity({ epoch, attempt, error: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [route.invalid, epoch, attempt, online, visible])
  useEffect(() => {
    if (!user?.authenticated || !user.canRead || !online || !visible) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    api.vessels(offset, '', c.signal).then(rows => decodeTodayVessels(rows, offset)).then(rows => { if (active) setPage({ epoch, attempt, offset, rows }) }).catch(() => { if (active) setPage({ epoch, attempt, offset, error: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [user, epoch, attempt, offset, online, visible])
  const title = en ? 'System health' : 'Sistem sağlığı'
  useEffect(() => { document.title = `POSEIDON · ${title}`; document.documentElement.lang = language }, [title, language])
  return <div className="product-shell enterprise-shell"><EnterpriseChrome session={user} vessel={vessel || null} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => { committed?.(); window.location.assign(href) }} title={title} mainId="health-main" />
    <main id="health-main" tabIndex={-1}><EnterprisePageHeader title={title} eyebrow={en ? 'SERVICE OBSERVATIONS' : 'SERVİS GÖZLEMLERİ'} description={en ? 'Observed API, synchronization and audit signals are separate. Configuration and missing telemetry do not establish healthy service or vessel readiness.' : 'API, eşitleme ve audit gözlemleri ayrıdır. Yapılandırma ve eksik telemetri sağlıklı servis veya araç hazırlığı belirtmez.'} actions={<Button variant="outline" disabled={!online} onClick={() => retry(n => n + 1)}>{en ? 'Refresh observations' : 'Gözlemleri yenile'}</Button>} />
      {route.invalid ? <p role="alert">{en ? 'Health context URL is invalid.' : 'Sağlık bağlamı adresi geçersiz.'}</p> : !online ? <p role="status">{en ? 'Offline. Current service observations are unavailable.' : 'Çevrimdışı. Güncel servis gözlemleri kullanılamıyor.'}</p> : !visible ? <p role="status">{en ? 'Return to this window to read current observations.' : 'Güncel gözlemler için bu pencereye dönün.'}</p> : !user ? <p role={currentIdentity?.error ? 'alert' : 'status'}>{currentIdentity?.error ? (en ? 'Session unavailable. No service state is inferred.' : 'Oturum kullanılamıyor. Servis durumu varsayılmadı.') : (en ? 'Checking current session…' : 'Güncel oturum kontrol ediliyor…')}</p> : !user.authenticated || !user.canRead ? <p role="alert">{en ? 'Current workspace access is unavailable.' : 'Güncel çalışma alanı erişimi kullanılamıyor.'}</p> : <>
        <EnterpriseToolbar><label>{en ? 'Vessel observation scope' : 'Araç gözlem kapsamı'}<select value={vessel} onChange={event => { const id = event.target.value; setVessel(id); history.replaceState(null, '', `/app/system-health${id ? '?vessel=' + id : ''}`) }}><option value="">{en ? 'Select vessel' : 'Araç seçin'}</option>{vessel && !currentPage?.rows?.items.some(row => row.id === vessel) && <option value={vessel}>{vessel}</option>}{currentPage?.rows?.items.map(row => <option key={row.id} value={row.id}>{row.code} · {row.name}</option>)}</select></label></EnterpriseToolbar>
        {currentPage?.error && <p role="alert">{en ? 'Vessel scope list unavailable.' : 'Araç kapsam listesi kullanılamıyor.'}</p>}
        {currentPage?.rows && <EnterprisePagination label={en ? 'Observation vessel pages' : 'Gözlem araç sayfaları'} previous={en ? 'Previous vessels' : 'Önceki araçlar'} next={en ? 'Next vessels' : 'Sonraki araçlar'} previousDisabled={!offset} nextDisabled={!currentPage.rows.hasMore || offset >= 100000} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)} />}
        {vessel ? <HealthSnapshot key={`${user.tenantId}:${user.actorId}:${vessel}:${epoch}:${attempt}`} vessel={vessel} language={language} /> : <p role="status">{en ? 'Choose a vessel to request authorized observations.' : 'Yetkili gözlemleri istemek için araç seçin.'}</p>}
      </>}
    </main></div>
}

function HealthSnapshot({ vessel, language }: { vessel: string; language: 'tr' | 'en' }) {
  const en = language === 'en', [result, setResult] = useState<{ data?: SystemHealth; error?: number } | null>(null)
  useEffect(() => {
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    loadSystemHealth(vessel, c.signal).then(data => { if (active) setResult({ data }) }).catch(error => { if (active) setResult({ error: error instanceof api.ApiError ? error.status : 0 }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [vessel])
  if (!result) return <p role="status">{en ? 'Loading scoped service observations…' : 'Kapsamlı servis gözlemleri yükleniyor…'}</p>
  if (!result.data) return <p role="alert">{[401, 403, 404].includes(result.error ?? 0) ? (en ? 'Health access is unavailable for this context.' : 'Bu bağlam için sağlık erişimi kullanılamıyor.') : (en ? 'Service observations unavailable. No healthy state is inferred.' : 'Servis gözlemleri kullanılamıyor. Sağlıklı durum varsayılmadı.')}</p>
  const data = result.data, unknown = en ? 'Unavailable' : 'Kullanılamıyor', number = (value: number | null) => value === null ? unknown : new Intl.NumberFormat(language, { maximumFractionDigits: 3 }).format(value)
  return <section aria-label={en ? 'Authorized service observations' : 'Yetkili servis gözlemleri'}><p>{en ? 'Read at' : 'Okuma zamanı'}: <time dateTime={data.readAt}>{data.readAt}</time></p><div className="health-observation-grid">
    <Card><h2>{en ? 'API' : 'API'}</h2><p>{en ? 'This tenant · observations in this process since start' : 'Bu kuruluş · süreç başlangıcından beri gözlemler'}</p><dl><dt>{en ? 'Requests' : 'İstekler'}</dt><dd>{number(data.api.requests)}</dd><dt>{en ? 'Errors' : 'Hatalar'}</dt><dd>{number(data.api.errors)}</dd><dt>{en ? 'Average latency (ms)' : 'Ortalama gecikme (ms)'}</dt><dd>{number(data.api.averageLatencyMs)}</dd></dl></Card>
    <Card><h2>{en ? 'Audit writer' : 'Audit yazıcısı'}</h2><p>{en ? 'This tenant · observations in this process since start' : 'Bu kuruluş · süreç başlangıcından beri gözlemler'}</p><dl><dt>{en ? 'Persisted' : 'Kalıcı kayıtlar'}</dt><dd>{number(data.audit.persisted)}</dd><dt>{en ? 'Unavailable writes' : 'Kullanılamayan yazımlar'}</dt><dd>{number(data.audit.unavailable)}</dd><dt>{en ? 'Average latency (ms)' : 'Ortalama gecikme (ms)'}</dt><dd>{number(data.audit.averageLatencyMs)}</dd></dl></Card>
    <Card><h2>{en ? 'Synchronization' : 'Eşitleme'}</h2><p>{en ? 'Selected vessel · persisted server outcomes' : 'Seçili araç · kalıcı sunucu sonuçları'}</p><dl><dt>{en ? 'Committed outcomes' : 'Kaydedilmiş sonuçlar'}</dt><dd>{number(data.sync.committed)}</dd><dt>{en ? 'Unresolved conflicts' : 'Çözümlenmemiş çakışmalar'}</dt><dd>{number(data.sync.unresolvedConflicts)}</dd><dt>{en ? 'Device backlog' : 'Cihaz kuyruğu'}</dt><dd>{unknown}</dd></dl></Card>
    <Card><h2>{en ? 'File service' : 'Dosya servisi'}</h2><p>{en ? 'Configuration only · no live storage or scanner probe' : 'Yalnız yapılandırma · canlı depolama veya tarayıcı kontrolü yapılmadı'}</p><dl><dt>{en ? 'Adapter' : 'Adaptör'}</dt><dd>{data.files.adapter === 'Unavailable' ? unknown : data.files.adapter}</dd><dt>{en ? 'Storage observation' : 'Depolama gözlemi'}</dt><dd>{data.files.state === 'ConfiguredUnprobed' ? (en ? 'Configured · unprobed' : 'Yapılandırılmış · kontrol edilmedi') : unknown}</dd><dt>{en ? 'Scanner observation' : 'Tarayıcı gözlemi'}</dt><dd>{data.files.scanState === 'ConfiguredUnprobed' ? (en ? 'Configured · unprobed' : 'Yapılandırılmış · kontrol edilmedi') : unknown}</dd></dl></Card>
    <Card><h2>{en ? 'Input freshness' : 'Girdi güncelliği'}</h2><p>{en ? 'This tenant · observed assessments in this process' : 'Bu kuruluş · bu süreçteki değerlendirme gözlemleri'}</p><dl>{(['current', 'stale', 'future', 'unknown', 'unavailable'] as const).map((key, index) => <div key={key}><dt>{(en ? ['Current', 'Stale', 'Future dated', 'Unknown', 'Unavailable'] : ['Güncel', 'Eski', 'İleri tarihli', 'Bilinmiyor', 'Kullanılamıyor'])[index]}</dt><dd>{number(data.freshness[key])}</dd></div>)}</dl></Card>
  </div></section>
}
