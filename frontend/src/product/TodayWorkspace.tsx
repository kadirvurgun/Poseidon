import { useEffect, useRef, useState } from 'react'
import * as api from './api'
import * as service from './today-api'
import { loginHref } from './workspace-location'

const copy = {
  en: { select: 'Select a vessel', next: 'Next vessels', previous: 'Previous vessels', loading: 'Loading…', refresh: 'Refresh', retry: 'Retry', why: 'Why?', close: 'Close Why', error: 'Source unavailable. Retry when connected.', denied: 'Access denied.', login: 'Sign in to continue', missing: 'Vessel unavailable in your scope.', stale: 'Displayed snapshot may be stale. Refresh to recheck sources.', offline: 'Offline. Displayed data is stale and unverified.', offlineEmpty: 'Offline. No server snapshot is available. Reconnect to load sources.', empty: 'No returned items on this page. This does not mean all clear.', coverage: 'Source coverage', Available: 'Available (unverified)', Missing: 'Missing', Unavailable: 'Unavailable: source not connected', RegulatoryDraft: 'Regulatory draft', OperationalTasks: 'Operational tasks', SafetyDecisions: 'Safety decisions', Permits: 'Permits', EvidenceVerification: 'Evidence verification', DraftAbsent: 'No regulatory draft is stored.', DraftUnverified: 'Stored regulatory draft is unverified.', Advisory: 'Advisory', owner: 'Owner: not assigned by a supported source', ReviewDraft: 'Review draft', ViewDraft: 'View draft', read: 'Read from server at', saved: 'Draft saved at', observed: 'Input source observation', unknown: 'Unknown', freshness: 'Input freshness is unverified. Save/read time does not verify the source.', counts: 'Declared / missing / unknown inputs', blockers: 'Unavailable prerequisites', VerificationUnavailable: 'Verification source unavailable', OperationalSourcesUnavailable: 'Operational sources unavailable', rule: 'Rule', context: 'Current vessel', switch: 'Switch vessel' },
  tr: { select: 'Araç seçin', next: 'Sonraki araçlar', previous: 'Önceki araçlar', loading: 'Yükleniyor…', refresh: 'Yenile', retry: 'Tekrar dene', why: 'Neden?', close: 'Neden ayrıntısını kapat', error: 'Kaynağa ulaşılamıyor. Bağlantı kurulduğunda tekrar deneyin.', denied: 'Erişim reddedildi.', login: 'Devam etmek için oturum açın', missing: 'Araç yetki alanınızda kullanılamıyor.', stale: 'Görüntülenen anlık veri eski olabilir. Kaynakları yeniden denetlemek için yenileyin.', offline: 'Çevrimdışı. Görüntülenen veriler eski ve doğrulanmamış.', offlineEmpty: 'Çevrimdışı. Sunucu verisi alınmadı. Kaynakları yüklemek için yeniden bağlanın.', empty: 'Bu sayfada dönen öğe yok. Bu, her şeyin uygun olduğu anlamına gelmez.', coverage: 'Kaynak kapsamı', Available: 'Kullanılabilir (doğrulanmamış)', Missing: 'Eksik', Unavailable: 'Kullanılamıyor: kaynak bağlı değil', RegulatoryDraft: 'Mevzuat taslağı', OperationalTasks: 'Operasyonel görevler', SafetyDecisions: 'Emniyet kararları', Permits: 'İzinler', EvidenceVerification: 'Kanıt doğrulama', DraftAbsent: 'Kaydedilmiş mevzuat taslağı yok.', DraftUnverified: 'Kayıtlı mevzuat taslağı doğrulanmamış.', Advisory: 'Bilgilendirme', owner: 'Sorumlu: desteklenen kaynak tarafından atanmamış', ReviewDraft: 'Taslağı incele', ViewDraft: 'Taslağı görüntüle', read: 'Sunucudan okuma zamanı', saved: 'Taslak kayıt zamanı', observed: 'Girdi kaynak gözlemi', unknown: 'Bilinmiyor', freshness: 'Girdi güncelliği doğrulanmadı. Kayıt/okuma zamanı kaynağı doğrulamaz.', counts: 'Beyan edilen / eksik / bilinmeyen girdiler', blockers: 'Kullanılamayan ön koşullar', VerificationUnavailable: 'Doğrulama kaynağı kullanılamıyor', OperationalSourcesUnavailable: 'Operasyonel kaynaklar kullanılamıyor', rule: 'Kural', context: 'Etkin araç', switch: 'Araç değiştir' },
}
type Load<T> = { state: 'loading' } | { state: 'ok'; data: T } | { state: 'error'; status: number }
const status = (e: unknown) => e instanceof api.ApiError ? e.status : 0
const focusError = (node: HTMLDivElement | null) => { node?.focus() }
type Props = { id: string | null; language: 'tr' | 'en'; offline: boolean; onNavigate: (href: string) => void }
// A changed vessel must never render or reuse the previous vessel's snapshot/disclosure.
export function TodayWorkspace(props: Props) {
  const id = props.id?.toLowerCase() ?? null
  return <TodayContext key={id ?? 'select'} {...props} id={id} />
}
function TodayContext({ id, language, offline, onNavigate }: Props) {
  const t = copy[language]
  const [data, setData] = useState<Load<service.Today>>({ state: 'loading' })
  const [vessels, setVessels] = useState<Load<api.Page<api.Vessel>>>({ state: 'loading' })
  const [offset, setOffset] = useState(0)
  const [attempt, setAttempt] = useState(0)
  const [whyAttempt, setWhyAttempt] = useState(0)
  const [expanded, setExpanded] = useState(false)
  const [why, setWhy] = useState<Load<service.Why>>({ state: 'loading' })
  const disclosureButton = useRef<HTMLButtonElement>(null)
  const [previousOffline, setPreviousOffline] = useState(offline)
  // Invalidate the disclosure before rendering a changed connection state. Reconnecting
  // starts a new read; an offline snapshot is never promoted to a current observation.
  if (previousOffline !== offline) {
    setPreviousOffline(offline); setExpanded(false); setWhy({ state: 'loading' })
    if (!offline) { setData({ state: 'loading' }); setVessels({ state: 'loading' }) }
  }
  useEffect(() => {
    if (offline) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    const promise = id ? service.today(id, c.signal).then(data => { if (active) setData({ state: 'ok', data }) })
      : service.todayVessels(offset, c.signal).then(data => { if (active) setVessels({ state: 'ok', data }) })
    promise.catch(e => { if (active) { if (id) setData({ state: 'error', status: status(e) }); else setVessels({ state: 'error', status: status(e) }) } })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [id, attempt, offset, offline])
  useEffect(() => {
    if (!id || !expanded || offline) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    service.why(id, c.signal).then(data => { if (active) setWhy({ state: 'ok', data }) }).catch(e => {
      if (!active) return
      const code = status(e)
      setWhy({ state: 'error', status: code })
      // A new denial invalidates the whole previously authorized snapshot.
      if ([401, 403, 404].includes(code)) { setData({ state: 'error', status: code }); setExpanded(false) }
    })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [id, expanded, whyAttempt, offline])

  const refresh = () => { setExpanded(false); setWhy({ state: 'loading' }); setData({ state: 'loading' }); setVessels({ state: 'loading' }); setAttempt(v => v + 1) }
  if (!id) return <section aria-label={t.select}><h2>{t.select}</h2>{vessels.state === 'loading' ? <p role="status">{offline ? t.offlineEmpty : t.loading}</p> : vessels.state === 'error' ? <TodayError code={vessels.status} retry={refresh} offline={offline} t={t} /> : <>{offline && <p role="status">{t.offline}</p>}{vessels.data.items.length === 0 && <p>{t.empty}</p>}<ul>{vessels.data.items.map(v => <li key={v.id}><button onClick={() => onNavigate(`/app/today/${v.id}`)}>{v.code} · {v.name}</button></li>)}</ul><button disabled={!offset || offline} onClick={() => { setVessels({ state: 'loading' }); setOffset(v => Math.max(0, v - 25)) }}>{t.previous}</button><button disabled={!vessels.data.hasMore || offline || offset >= 100000} onClick={() => { setVessels({ state: 'loading' }); setOffset(v => v + 25) }}>{t.next}</button><button disabled={offline} onClick={refresh}>{t.refresh}</button></>}</section>
  return <section aria-label={language === 'en' ? 'Today' : 'Bugün'}><button disabled={offline || data.state === 'loading'} onClick={refresh}>{t.refresh}</button><button onClick={() => onNavigate('/app/today')}>{t.switch}</button>
    {data.state === 'loading' ? <p role="status">{offline ? t.offlineEmpty : t.loading}</p> : data.state === 'error' ? <TodayError code={data.status} retry={refresh} offline={offline} t={t} /> : <>
      <h2>{t.context}: {data.data.vessel.kind === 'Tug' ? (language === 'tr' ? 'Römorkör' : 'Tug') : (language === 'tr' ? 'Gemi' : 'Ship')} · {data.data.vessel.code} · {data.data.vessel.name}</h2>
      <p role="status">{offline ? t.offline : t.stale}</p><p>{t.read}: <time dateTime={data.data.readAt}>{data.data.readAt}</time></p>
      <h3>{t.coverage}</h3><ul>{data.data.coverage.map(c => <li key={c.source}>{t[c.source as keyof typeof t]}: {t[c.status as keyof typeof t]}</li>)}</ul>
      {data.data.cards.items.length === 0 && <p>{t.empty}</p>}
      {data.data.cards.items.map(card => <article key={card.key} className="product-detail"><h3>{t.Advisory} · {t.RegulatoryDraft}</h3><p>{t[card.reason as 'DraftAbsent' | 'DraftUnverified']}</p><p>{t.owner}</p><button onClick={() => onNavigate(card.href)}>{t[card.nextAction as 'ReviewDraft' | 'ViewDraft']}</button><button ref={disclosureButton} aria-expanded={expanded} aria-controls="today-why" disabled={offline} onClick={() => { setWhy({ state: 'loading' }); setExpanded(v => !v) }}>{expanded ? t.close : t.why}</button>
        {expanded && !offline && <section id="today-why" aria-label={t.why} aria-live="polite">{why.state === 'loading' ? <p role="status">{t.loading}</p> : why.state === 'error' ? <TodayError code={why.status} retry={() => { disclosureButton.current?.focus(); setWhy({ state: 'loading' }); setWhyAttempt(v => v + 1) }} offline={offline} t={t} /> : <><p>{t[why.data.reason as 'DraftAbsent' | 'DraftUnverified']}</p><p>{t.rule}: {why.data.rule}</p><p>{t.counts}: {why.data.inputs.known} / {why.data.inputs.missing} / {why.data.inputs.unknown}</p><p>{t.saved}: {why.data.savedAt ?? t.unknown}</p><p>{t.observed}: {why.data.sourceObservedAt ?? t.unknown}</p><p>{t.freshness}</p><p>{t.blockers}</p><ul>{why.data.blockers.map(b => <li key={b}>{t[b as 'DraftAbsent' | 'VerificationUnavailable' | 'OperationalSourcesUnavailable']}</li>)}</ul></>}</section>}
      </article>)}
    </>}
  </section>
}

function TodayError({ code, retry, offline, t }: { code: number; retry: () => void; offline: boolean; t: Record<keyof typeof copy.en, string> }) {
  return <div role="alert" tabIndex={-1} ref={focusError}><p>{code === 401 ? t.login : code === 403 ? t.denied : code === 404 ? t.missing : t.error}</p>{code === 401 ? <a href={loginHref()}>{t.login}</a> : <button disabled={offline} onClick={retry}>{t.retry}</button>}</div>
}
