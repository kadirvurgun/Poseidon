import { useEffect, useRef, useState } from 'react'
import * as api from './api'
import * as service from './today-api'
import { loginHref } from './workspace-location'
import { dueReason, dueState } from './maintenance-copy'
import { todayTugCopy } from './today-tug-copy'
import { TodayTugExplanation } from './TodayTugExplanation'
import { Anchor, ArrowLeft, ArrowUpRight, RefreshCw, Ship } from 'lucide-react'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { Card } from '../components/ui/card'
import { TodayOverview } from './TodayOverview'

const copy = {
  en: { currentOwner: 'Current source owner role', planReference: 'Plan reference', assetReference: 'Equipment reference', observationAssessment: 'Current observation assessment', noObservation: 'No normalized observation requested', algorithm: 'Calculation rule/version', boundary: 'Next calculated date / meter', provenance: 'Input origin declaration', previousCards: 'Previous alerts', nextCards: 'Next alerts', TechnicalMaintenance: 'Maintenance plans', MaintenancePlan: 'Maintenance plan', MaintenanceDue: 'Draft planning calculation is due.', MaintenanceReviewRequired: 'Maintenance planning requires review.', Attention: 'Planning attention', Review: 'Review required', ReviewMaintenancePlan: 'Review maintenance plan', ownerRole: 'Assigned role reference', PlanningDraftNotVerified: 'Planning draft is unverified; completed maintenance and operational fitness are not established.', lineage: 'Plan / equipment revision', currentAsset: 'Current equipment revision', select: 'Select a vessel', next: 'Next vessels', previous: 'Previous vessels', loading: 'Loading…', refresh: 'Refresh', retry: 'Retry', why: 'Why?', close: 'Close Why', error: 'Source unavailable. Retry when connected.', denied: 'Access denied.', login: 'Sign in to continue', missing: 'Vessel unavailable in your scope.', stale: 'Displayed snapshot may be stale. Refresh to recheck sources.', offline: 'Offline. Displayed data is stale and unverified.', offlineEmpty: 'Offline. No server snapshot is available. Reconnect to load sources.', empty: 'No returned items on this page. This does not mean all clear.', coverage: 'Source coverage', Available: 'Available (unverified)', Missing: 'Missing', Unavailable: 'Unavailable in this context', RegulatoryDraft: 'Regulatory draft', OperationalTasks: 'Operational tasks', SafetyDecisions: 'Safety decisions', Permits: 'Permits', EvidenceVerification: 'Evidence verification', DraftAbsent: 'No regulatory draft is stored.', DraftUnverified: 'Stored regulatory draft is unverified.', Advisory: 'Advisory', owner: 'Owner: not assigned by a supported source', ReviewDraft: 'Review draft', ViewDraft: 'View draft', read: 'Read from server at', saved: 'Draft saved at', observed: 'Input source observation', unknown: 'Unknown', freshness: 'Input freshness is unverified. Save/read time does not verify the source.', counts: 'Declared / missing / unknown inputs', blockers: 'Unavailable prerequisites', VerificationUnavailable: 'Verification source unavailable', OperationalSourcesUnavailable: 'Operational sources unavailable', rule: 'Rule', context: 'Current vessel', switch: 'Switch vessel' },
  tr: { currentOwner: 'Güncel kaynak sorumlu rolü', planReference: 'Plan referansı', assetReference: 'Ekipman referansı', observationAssessment: 'Güncel gözlem değerlendirmesi', noObservation: 'Normalleştirilmiş gözlem istenmemiş', algorithm: 'Hesap kuralı/sürümü', boundary: 'Sonraki hesaplanan tarih / sayaç', provenance: 'Girdi kaynağı beyanı', previousCards: 'Önceki uyarılar', nextCards: 'Sonraki uyarılar', TechnicalMaintenance: 'Bakım planları', MaintenancePlan: 'Bakım planı', MaintenanceDue: 'Taslak planlama hesabına göre bakım zamanı geldi.', MaintenanceReviewRequired: 'Bakım planlaması inceleme gerektiriyor.', Attention: 'Planlama uyarısı', Review: 'İnceleme gerekli', ReviewMaintenancePlan: 'Bakım planını incele', ownerRole: 'Atanan rol referansı', PlanningDraftNotVerified: 'Planlama taslağı doğrulanmamış; tamamlanmış bakım veya operasyonel uygunluk belirtmez.', lineage: 'Plan / ekipman revizyonu', currentAsset: 'Güncel ekipman revizyonu', select: 'Araç seçin', next: 'Sonraki araçlar', previous: 'Önceki araçlar', loading: 'Yükleniyor…', refresh: 'Yenile', retry: 'Tekrar dene', why: 'Neden?', close: 'Neden ayrıntısını kapat', error: 'Kaynağa ulaşılamıyor. Bağlantı kurulduğunda tekrar deneyin.', denied: 'Erişim reddedildi.', login: 'Devam etmek için oturum açın', missing: 'Araç yetki alanınızda kullanılamıyor.', stale: 'Görüntülenen anlık veri eski olabilir. Kaynakları yeniden denetlemek için yenileyin.', offline: 'Çevrimdışı. Görüntülenen veriler eski ve doğrulanmamış.', offlineEmpty: 'Çevrimdışı. Sunucu verisi alınmadı. Kaynakları yüklemek için yeniden bağlanın.', empty: 'Bu sayfada dönen öğe yok. Bu, her şeyin uygun olduğu anlamına gelmez.', coverage: 'Kaynak kapsamı', Available: 'Kullanılabilir (doğrulanmamış)', Missing: 'Eksik', Unavailable: 'Bu bağlamda kullanılamıyor', RegulatoryDraft: 'Mevzuat taslağı', OperationalTasks: 'Operasyonel görevler', SafetyDecisions: 'Emniyet kararları', Permits: 'İzinler', EvidenceVerification: 'Kanıt doğrulama', DraftAbsent: 'Kaydedilmiş mevzuat taslağı yok.', DraftUnverified: 'Kayıtlı mevzuat taslağı doğrulanmamış.', Advisory: 'Bilgilendirme', owner: 'Sorumlu: desteklenen kaynak tarafından atanmamış', ReviewDraft: 'Taslağı incele', ViewDraft: 'Taslağı görüntüle', read: 'Sunucudan okuma zamanı', saved: 'Taslak kayıt zamanı', observed: 'Girdi kaynak gözlemi', unknown: 'Bilinmiyor', freshness: 'Girdi güncelliği doğrulanmadı. Kayıt/okuma zamanı kaynağı doğrulamaz.', counts: 'Beyan edilen / eksik / bilinmeyen girdiler', blockers: 'Kullanılamayan ön koşullar', VerificationUnavailable: 'Doğrulama kaynağı kullanılamıyor', OperationalSourcesUnavailable: 'Operasyonel kaynaklar kullanılamıyor', rule: 'Kural', context: 'Etkin araç', switch: 'Araç değiştir' },
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
  const t = { ...copy[language], ...todayTugCopy[language] }
  const [data, setData] = useState<Load<service.Today>>({ state: 'loading' })
  const [vessels, setVessels] = useState<Load<api.Page<api.Vessel>>>({ state: 'loading' })
  const [offset, setOffset] = useState(0)
  const [attempt, setAttempt] = useState(0)
  const [whyAttempt, setWhyAttempt] = useState(0)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [cardOffset, setCardOffset] = useState(0)
  const [why, setWhy] = useState<Load<service.Why>>({ state: 'loading' })
  const disclosureButton = useRef<HTMLButtonElement>(null)
  const [previousOffline, setPreviousOffline] = useState(offline)
  // Invalidate the disclosure before rendering a changed connection state. Reconnecting
  // starts a new read; an offline snapshot is never promoted to a current observation.
  if (previousOffline !== offline) {
    setPreviousOffline(offline); setExpanded(null); setWhy({ state: 'loading' })
    if (!offline) { setData({ state: 'loading' }); setVessels({ state: 'loading' }) }
  }
  useEffect(() => {
    if (offline) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    const promise = id ? service.today(id, c.signal, cardOffset).then(data => { if (active) setData({ state: 'ok', data }) })
      : service.todayVessels(offset, c.signal).then(data => { if (active) setVessels({ state: 'ok', data }) })
    promise.catch(e => { if (active) { if (id) setData({ state: 'error', status: status(e) }); else setVessels({ state: 'error', status: status(e) }) } })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [id, attempt, offset, cardOffset, offline])
  useEffect(() => {
    if (!id || !expanded || offline) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    service.why(id, c.signal, expanded).then(data => { if (active) setWhy({ state: 'ok', data }) }).catch(e => {
      if (!active) return
      const code = status(e)
      setWhy({ state: 'error', status: code })
      // A new denial invalidates the whole previously authorized snapshot.
      if ([401, 403, 404].includes(code)) { setData({ state: 'error', status: code }); setExpanded(null) }
    })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [id, expanded, whyAttempt, offline])

  const refresh = () => { setCardOffset(0); setExpanded(null); setWhy({ state: 'loading' }); setData({ state: 'loading' }); setVessels({ state: 'loading' }); setAttempt(v => v + 1) }
  if (!id) return <section aria-label={t.select}><h2>{t.select}</h2>{vessels.state === 'loading' ? <p role="status">{offline ? t.offlineEmpty : t.loading}</p> : vessels.state === 'error' ? <TodayError code={vessels.status} retry={refresh} offline={offline} t={t} /> : <>{offline && <p role="status">{t.offline}</p>}{vessels.data.items.length === 0 && <p>{t.empty}</p>}
    <div className="enterprise-vessel-grid">{vessels.data.items.map(v => { const Icon = v.kind === 'Tug' ? Anchor : Ship; return <Card className="enterprise-vessel-card" key={v.id}><span className="enterprise-vessel-icon"><Icon aria-hidden="true" /></span><div><h3>{v.code}</h3><p>{v.name}</p><Button variant="outline" size="sm" aria-label={`${v.code} · ${v.name}`} onClick={() => onNavigate(`/app/today/${v.id}`)}>{language === 'en' ? 'Open workspace' : 'Çalışma alanını aç'}<ArrowUpRight aria-hidden="true" /></Button></div></Card> })}</div>
    <div className="product-pagination"><Button variant="outline" disabled={!offset || offline} onClick={() => { setVessels({ state: 'loading' }); setOffset(v => Math.max(0, v - 25)) }}>{t.previous}</Button><Button variant="outline" disabled={!vessels.data.hasMore || offline || offset >= 100000} onClick={() => { setVessels({ state: 'loading' }); setOffset(v => v + 25) }}>{t.next}</Button><Button variant="ghost" disabled={offline} onClick={refresh}><RefreshCw aria-hidden="true" />{t.refresh}</Button></div></>}</section>
  return <section aria-label={language === 'en' ? 'Today' : 'Bugün'}><div className="today-card-toolbar"><Button variant="outline" disabled={offline || data.state === 'loading'} onClick={refresh}><RefreshCw aria-hidden="true" />{t.refresh}</Button><Button variant="ghost" onClick={() => onNavigate('/app/today')}><ArrowLeft aria-hidden="true" />{t.switch}</Button></div>
    {data.state === 'loading' ? <p role="status">{offline ? t.offlineEmpty : t.loading}</p> : data.state === 'error' ? <TodayError code={data.status} retry={refresh} offline={offline} t={t} /> : <>
      <h2>{t.context}: {data.data.vessel.kind === 'Tug' ? (language === 'tr' ? 'Römorkör' : 'Tug') : (language === 'tr' ? 'Gemi' : 'Ship')} · {data.data.vessel.code} · {data.data.vessel.name}</h2>
      <p role="status">{offline ? t.offline : t.stale}</p><p>{t.read}: <time dateTime={data.data.readAt}>{data.data.readAt}</time></p>
      <TodayOverview data={data.data} language={language} />
      <h3>{t.coverage}</h3><ul className="today-source-coverage">{data.data.coverage.map(c => <li key={c.source}>{t[c.source as keyof typeof t]}: {t[c.status as keyof typeof t]}</li>)}</ul>
      {data.data.cards.items.length === 0 && <p>{t.empty}</p>}
      {data.data.cards.items.map(card => {
        const open = expanded === card.key
        const disclosureId = card.key === 'regulatory-draft' ? 'today-why' : `today-why-${card.key.replace(/[^a-z0-9-]/g, '-')}`
        return <article key={card.key} className="product-detail today-feed-card" data-priority={card.priority} aria-label={card.source === 'TugJob' ? `${t.TugJob} ${card.key.slice('tug-job:'.length)}` : undefined}><div className="today-feed-card-header"><h3>{t[card.priority as keyof typeof t]} · {t[card.source as keyof typeof t]}</h3><Badge tone={card.priority === 'Attention' ? 'warning' : card.priority === 'Review' ? 'neutral' : 'info'}>{t[card.priority as keyof typeof t]}</Badge></div>
          {card.source === 'TugJob' && <p>{t.jobReference}: {card.key.slice('tug-job:'.length)}</p>}
          <p>{t[card.reason as keyof typeof t]}</p><p>{card.owner ? `${t.ownerRole}: ${card.owner}` : t.owner}</p>
          <div className="today-feed-card-actions"><Button variant="outline" onClick={() => onNavigate(card.href)}>{t[card.nextAction as keyof typeof t]}<ArrowUpRight aria-hidden="true" /></Button>
          <Button variant="ghost" aria-expanded={open} aria-controls={disclosureId} disabled={offline} onClick={event => {
            disclosureButton.current = event.currentTarget; setWhy({ state: 'loading' }); setExpanded(open ? null : card.key)
          }}>{open ? t.close : t.why}</Button></div>
          {open && !offline && <section id={disclosureId} aria-label={t.why} aria-live="polite">{why.state === 'loading' ? <p role="status">{t.loading}</p> : why.state === 'error' ? <TodayError code={why.status} retry={() => { disclosureButton.current?.focus(); setWhy({ state: 'loading' }); setWhyAttempt(v => v + 1) }} offline={offline} t={t} /> : <TodayExplanation why={why.data} language={language} t={t} />}</section>}
        </article>
      })}
      <button disabled={offline || cardOffset === 0} onClick={() => { setExpanded(null); setWhy({ state: 'loading' }); setData({ state: 'loading' }); setCardOffset(v => Math.max(0, v - 25)) }}>{t.previousCards}</button>
      <button disabled={offline || !data.data.cards.hasMore || cardOffset >= 100000} onClick={() => { setExpanded(null); setWhy({ state: 'loading' }); setData({ state: 'loading' }); setCardOffset(v => v + 25) }}>{t.nextCards}</button>
    </>}
  </section>
}

function TodayError({ code, retry, offline, t }: { code: number; retry: () => void; offline: boolean; t: Record<keyof typeof copy.en, string> }) {
  return <div role="alert" tabIndex={-1} ref={focusError}><p>{code === 401 ? t.login : code === 403 ? t.denied : code === 404 ? t.missing : t.error}</p>{code === 401 ? <a href={loginHref()}>{t.login}</a> : <button disabled={offline} onClick={retry}>{t.retry}</button>}</div>
}

function TodayExplanation({ why, language, t }: { why: service.Why; language: 'tr' | 'en'; t: Record<keyof typeof copy.en, string> }) {
  return <><p>{t[why.reason as keyof typeof t]}</p><p>{t.rule}: {why.rule}</p>
    <p>{t.read}: <time dateTime={why.readAt}>{why.readAt}</time></p><p>{t.currentOwner}: {why.owner ?? t.unknown}</p>
    {why.inputs && <p>{t.counts}: {why.inputs.known} / {why.inputs.missing} / {why.inputs.unknown}</p>}
    {why.tug && <TodayTugExplanation detail={why.tug} language={language} />}
    {why.maintenance && <><p>{dueState(why.maintenance.calculation.state, language === 'en')} · {dueReason(why.maintenance.calculation.reason, language === 'en')}</p>
      <p>{t.lineage}: {why.maintenance.planRevision} / {why.maintenance.assetRevision}</p><p>{t.currentAsset}: {why.maintenance.currentAssetRevision ?? t.unknown}</p>
      <p>{t.planReference}: {why.maintenance.planId}</p><p>{t.assetReference}: {why.maintenance.assetId}</p>
      <p>{t.algorithm}: {why.maintenance.calculation.algorithm}</p><p>{t.observationAssessment}: {why.maintenance.observationState === 'NotRequested' ? t.noObservation : dueReason(why.maintenance.observationState, language === 'en')}</p>
      <p>{t.boundary}: {why.maintenance.calculation.nextDueDate ?? t.unknown} / {why.maintenance.calculation.nextDueMeter ?? t.unknown}</p></>}
    <p>{t.provenance}: {why.provenance}</p><p>{t.saved}: {why.savedAt ?? t.unknown}</p><p>{t.observed}: {why.sourceObservedAt ?? t.unknown}</p><p>{t.freshness}</p>
    <p>{t.blockers}</p><ul>{why.blockers.map(b => <li key={b}>{t[b as keyof typeof t]}</li>)}</ul></>
}
