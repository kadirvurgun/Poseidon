import { useEffect, useRef, useState } from 'react'
import { TugOwningEvidence } from './TugOwningEvidence'
import { TugOfflinePanel } from './TugOfflinePanel'
import { ApiError } from './api'
import { planningApi, type Plan, type PlanAttempt, type PlanChange, type PlanHistory, type PlanningEvidence, type PlanningOptions } from './tug-planning-api'

const words = {
  en: { title: 'Draft planning', start: 'Planned start (UTC)', end: 'Planned end (UTC)', save: 'Save planning window', assign: 'Assign to draft', remove: 'Remove from draft', reassign: 'Reassign in draft', select: 'Eligible tug', replacement: 'Replacement tug', history: 'Planning history', review: 'Review exact revision', reload: 'Discard and reload planning', retry: 'Retry exact planning command', uncertain: 'Planning outcome uncertain. Retry the retained command.', conflict: 'Planning conflict. Input retained; discard and reload to continue.', denial: 'Planning access unavailable. Current rights and policy are required.', unavailable: 'Planning data unavailable.', boundary: 'Draft assignments and times require authorized operational review. No permit or safety approval.', unknown: 'Unknown', missing: 'Missing', unavailableState: 'Unavailable', saved: 'Draft plan saved.', historyNote: 'Historical unverified plan. No current availability or approval is implied.', bp: 'Available verified BP', declared: 'Declared BP evidence', geometry: 'Draft geometry conflicts', offline: 'Tug offline capture is unavailable under the current command policy.', dirty: 'Unsaved planning changes', read: 'Read-only planning', state: 'State', source: 'Source / revision / observation / freshness', noPlan: 'No draft plan', noHistory: 'No visible planning history' },
  tr: { title: 'Taslak planlama', start: 'Planlanan başlangıç (UTC)', end: 'Planlanan bitiş (UTC)', save: 'Plan aralığını kaydet', assign: 'Taslağa ata', remove: 'Taslak atamasını kaldır', reassign: 'Taslakta yeniden ata', select: 'Uygun römorkör', replacement: 'Yeni römorkör', history: 'Planlama geçmişi', review: 'Kesin sürümü incele', reload: 'Plan değişikliklerini bırak ve yenile', retry: 'Aynı plan komutunu yinele', uncertain: 'Plan kaydının sonucu belirsiz. Korunan komutu yineleyin.', conflict: 'Plan çakışması. Girdi korundu; devam etmek için bırakıp yenileyin.', denial: 'Planlama erişimi yok. Güncel yetki ve politika gerekir.', unavailable: 'Planlama verisi alınamadı.', boundary: 'Taslak atamalar ve zamanlar yetkili operasyon incelemesi gerektirir. İzin veya emniyet onayı değildir.', unknown: 'Bilinmiyor', missing: 'Eksik', unavailableState: 'Kullanılamıyor', saved: 'Taslak plan kaydedildi.', historyNote: 'Geçmiş doğrulanmamış plan. Güncel uygunluk veya onay oluşturmaz.', bp: 'Kullanılabilir doğrulanmış BP', declared: 'Beyan edilen BP kaynağı', geometry: 'Taslak zaman çakışmaları', offline: 'Güncel komut politikası römorkör çevrimdışı kaydını desteklemiyor.', dirty: 'Kaydedilmemiş plan değişiklikleri', read: 'Salt okunur planlama', state: 'Durum', source: 'Kaynak / sürüm / gözlem / güncellik', noPlan: 'Taslak plan yok', noHistory: 'Görünür plan geçmişi yok' },
}
const statuses: Record<string, [string, string]> = {
  CurrentRequestRevision: ['Güncel talep sürümü', 'Current request revision'], RequestChangedSincePlan: ['Talep plan kaydından sonra değişti; inceleme gerekir', 'Request changed since this plan; review required'],
  Unknown: ['Bilinmiyor', 'Unknown'], Missing: ['Eksik', 'Missing'], Unavailable: ['Kullanılamıyor', 'Unavailable'],
  DeclaredUnverified: ['Beyan edilmiş, doğrulanmamış', 'Declared, unverified'], Current: ['Güncel', 'Current'], Stale: ['Eski', 'Stale'], Future: ['Gelecek zamanlı', 'Future'], Revoked: ['Geri çekilmiş', 'Revoked'],
  PolicyChanged: ['Politika değişmiş', 'Policy changed'], UnitOrKindMismatch: ['Birim veya tür uyuşmuyor', 'Unit or kind mismatch'], UnverifiedOrIncomplete: ['Doğrulanmamış veya eksik', 'Unverified or incomplete'],
  HumanReviewRequired: ['Yetkili insan incelemesi gerekir', 'Authorized human review required'], BoundaryReviewRequired: ['Sınır teması incelemesi gerekir', 'Boundary touch review required'], Intersecting: ['Örtüşüyor', 'Intersecting'], BoundaryTouch: ['Sınır teması', 'Boundary touch'],
  Window: ['Plan aralığı', 'Planning window'], Assign: ['Taslak atama', 'Draft assignment'], Reassign: ['Taslak yeniden atama', 'Draft reassignment'], Remove: ['Taslak atama kaldırma', 'Draft removal'],
}
export function TugPlanningPanel({ id, vessel, requestVersion, csrf, language, onDirty }: { id: string; vessel: string; requestVersion: number; csrf: string; language: 'tr' | 'en'; onDirty: (dirty: boolean) => void }) {
  const t = words[language], label = (s: string) => statuses[s]?.[language === 'en' ? 1 : 0] ?? s
  const [requestState, setRequestState] = useState(''), [loading, setLoading] = useState(true)
  const [plan, setPlan] = useState<Plan | null>(null), [options, setOptions] = useState<PlanningOptions | null>(null)
  const [evidence, setEvidence] = useState<PlanningEvidence | null>(null), [history, setHistory] = useState<PlanHistory | null>(null), [selected, setSelected] = useState<Plan | null>(null)
  const [start, setStart] = useState(''), [end, setEnd] = useState(''), [tug, setTug] = useState(''), [replacement, setReplacement] = useState('')
  const [attempt, setAttempt] = useState<PlanAttempt | null>(null), [notice, setNotice] = useState(''), [busy, setBusy] = useState(false), [dirty, setDirty] = useState(false), [generation, reload] = useState(0)
  const [offlineDirty, setOfflineDirty] = useState(false)
  const active = useRef<AbortController | null>(null), review = useRef<AbortController | null>(null), loadedContext = useRef(''), changed = useRef(onDirty); changed.current = onDirty
  useEffect(() => { changed.current(dirty || busy || !!attempt || offlineDirty); return () => changed.current(false) }, [dirty, busy, attempt, offlineDirty])
  useEffect(() => {
    const c = new AbortController()
    const refresh = () => {
      setEvidence(null)
      if (!navigator.onLine) return
      planningApi.detail(id, vessel, c.signal).then(current => {
        if (c.signal.aborted) return
        if (current.plan?.revision !== plan?.revision) { setNotice('conflict'); return }
        setEvidence(current.evidence); setRequestState(current.requestState ?? '')
      }).catch(e => {
        if (c.signal.aborted) return
        if (e instanceof ApiError && [401, 403, 404].includes(e.status)) { setPlan(null); setOptions(null); setHistory(null); setSelected(null); setNotice('denial') }
      })
    }
    const timer = window.setInterval(refresh, 30000)
    window.addEventListener('focus', refresh); window.addEventListener('online', refresh); window.addEventListener('offline', refresh)
    return () => { c.abort(); window.clearInterval(timer); window.removeEventListener('focus', refresh); window.removeEventListener('online', refresh); window.removeEventListener('offline', refresh) }
  }, [id, vessel, plan?.revision])
  useEffect(() => {
    const c = new AbortController(), signal = AbortSignal.any([c.signal, AbortSignal.timeout(10000)])
    const key = `${id}/${vessel}/${requestVersion}`, sameContext = loadedContext.current === key; loadedContext.current = key
    active.current = c; setLoading(true); setBusy(false); setPlan(null); setOptions(null); setEvidence(null); setHistory(null); setSelected(null); setRequestState(''); setNotice(n => sameContext && n === 'saved' ? n : ''); setDirty(false); setAttempt(null)
    Promise.all([planningApi.detail(id, vessel, signal), planningApi.options(id, vessel, signal), planningApi.history(id, vessel, signal)])
      .then(([detail, opts, h]) => { if (c.signal.aborted) return; setPlan(detail.plan); setRequestState(detail.requestState ?? ''); setEvidence(detail.evidence); setOptions(opts); setHistory(h); setStart(detail.plan?.inputs.plannedStart?.slice(0, 19) ?? ''); setEnd(detail.plan?.inputs.plannedEnd?.slice(0, 19) ?? '') })
      .catch(e => { if (!c.signal.aborted) setNotice(e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denial' : 'unavailable') })
      .finally(() => { if (!c.signal.aborted) setLoading(false) })
    return () => { c.abort(); review.current?.abort() }
  }, [id, vessel, requestVersion, generation])
  async function save(change?: PlanChange) {
    if (!options?.policyVersion || !active.current) return
    const c = active.current, frozen = attempt ?? { key: crypto.randomUUID(), body: { baseVersion: plan?.version ?? null, requestVersion, policyVersion: options.policyVersion, change: change! } }
    setAttempt(frozen); setBusy(true); setNotice('')
    try {
      const result = await planningApi.save(id, vessel, frozen, csrf, AbortSignal.any([c.signal, AbortSignal.timeout(15000)]))
      if (c.signal.aborted) return
      setPlan(result); setAttempt(null); setDirty(false); setNotice('saved'); reload(x => x + 1)
    } catch (e) {
      if (c.signal.aborted) return
      if (e instanceof ApiError && [401, 403, 404].includes(e.status)) { setPlan(null); setOptions(null); setEvidence(null); setHistory(null); setSelected(null); setRequestState(''); setStart(''); setEnd(''); setTug(''); setReplacement(''); setAttempt(null); setDirty(false); setNotice('denial') }
      else setNotice(e instanceof ApiError && [400, 409].includes(e.status) ? 'conflict' : 'uncertain')
    } finally { if (!c.signal.aborted) setBusy(false) }
  }
  async function exact(revision: number) {
    review.current?.abort(); const c = new AbortController(); review.current = c; setSelected(null)
    try { const r = await planningApi.revision(id, vessel, revision, c.signal); if (!c.signal.aborted) setSelected(r) }
    catch (e) { if (!c.signal.aborted) { setSelected(null); setHistory(null); if (e instanceof ApiError && [401, 403, 404].includes(e.status)) { setPlan(null); setOptions(null); setEvidence(null); setRequestState(''); setStart(''); setEnd(''); setNotice('denial') } else setNotice('unavailable') } }
  }
  const locked = busy || !!attempt, editable = options?.canWrite && notice !== 'denial'
  if (loading) return <section aria-label={t.title}><h3>{t.title}</h3><p role="status">{language === 'en' ? 'Loading planning…' : 'Planlama yükleniyor…'}</p></section>
  return <section aria-label={t.title}><h3>{t.title}</h3><p>{t.boundary}</p>
    {notice && <p role="alert">{t[notice as keyof typeof t] ?? t.unavailable}</p>}{dirty && <p role="status">{t.dirty}</p>}
    {plan ? <p>r{plan.revision} · {plan.sourceVersion} · {plan.policyVersion} · {plan.savedAt}</p> : !['denial', 'unavailable'].includes(notice) && <p>{t.noPlan}</p>}
    <ul aria-label={language === 'en' ? 'Current draft assignments' : 'Güncel taslak atamaları'}>{plan?.inputs.assignedTugs.map(id => <li key={id}>{options?.tugs.find(v => v.id === id)?.code ?? id}</li>)}</ul>
    {requestState && <p role={requestState === 'RequestChangedSincePlan' ? 'alert' : undefined}>{label(requestState)}</p>}
    <p>{t.bp}: {evidence?.availableBollardPull ?? t.unknown} · {label(evidence?.capacityState ?? 'Unknown')}</p>
    <TugOwningEvidence value={evidence} language={language} />
    <ul aria-label={t.declared}>{evidence?.bp.map(b => <li key={b.tugId}>{b.tugId}: {b.declaredValue ?? '—'} · {label(b.state)}<p>{t.source}: {b.source ?? '—'} / {b.sourceRevision ?? '—'} / {b.observationId ?? '—'} / {b.observedAt ?? '—'} / {label(b.freshness)} · {b.policyVersion ?? '—'}</p></li>)}</ul>
    <p>{t.geometry}: {label(evidence?.conflictState ?? 'Unknown')}</p><ul>{evidence?.conflicts.map(c => <li key={`${c.jobId}/${c.revision}`}>{c.jobId} · r{c.revision} · {label(c.relation)} · {c.observedAt}</li>)}</ul>
    {options?.offline === 'SupportedDraftWindowOnly' && plan ? <TugOfflinePanel key={`${id}/${vessel}/${requestVersion}`} job={id} vessel={vessel} language={language} onDirty={setOfflineDirty} /> : <p>{t.offline}</p>}
    {!editable && <p>{options?.state === 'Unavailable' ? t.denial : t.read}</p>}
    {editable && <><form onSubmit={e => { e.preventDefault(); void save({ operation: 'Window', plannedStart: start ? `${start.length === 16 ? start + ':00' : start}Z` : null, plannedEnd: end ? `${end.length === 16 ? end + ':00' : end}Z` : null }) }}>
      <fieldset disabled={locked}><label>{t.start}<input type="datetime-local" step="1" value={start} onChange={e => { setStart(e.target.value); setDirty(true) }} /></label><label>{t.end}<input type="datetime-local" step="1" value={end} onChange={e => { setEnd(e.target.value); setDirty(true) }} /></label></fieldset><button disabled={locked}>{t.save}</button></form>
      <fieldset disabled={locked || dirty}><label>{t.select}<select value={tug} onChange={e => setTug(e.target.value)}><option value="">—</option>{options.tugs.map(v => <option value={v.id} key={v.id}>{v.code}</option>)}</select></label>
        <label>{t.replacement}<select value={replacement} onChange={e => setReplacement(e.target.value)}><option value="">—</option>{options.tugs.filter(v => !plan?.inputs.assignedTugs.includes(v.id)).map(v => <option value={v.id} key={v.id}>{v.code}</option>)}</select></label>
        <button type="button" disabled={!tug || !!plan?.inputs.assignedTugs.includes(tug)} onClick={() => void save({ operation: 'Assign', tugId: tug })}>{t.assign}</button>
        <button type="button" disabled={!tug || !plan?.inputs.assignedTugs.includes(tug)} onClick={() => void save({ operation: 'Remove', tugId: tug })}>{t.remove}</button>
        <button type="button" disabled={!tug || !replacement || !plan?.inputs.assignedTugs.includes(tug)} onClick={() => void save({ operation: 'Reassign', tugId: tug, replacementTugId: replacement })}>{t.reassign}</button>
      </fieldset></>}
    {attempt && notice === 'uncertain' && <button disabled={busy} onClick={() => void save()}>{t.retry}</button>}
    {(dirty || attempt || notice === 'conflict' || notice === 'unavailable') && <button disabled={busy} onClick={() => { setDirty(false); setAttempt(null); reload(x => x + 1) }}>{t.reload}</button>}
    <h4>{t.history}</h4>{history?.items.length === 0 && <p>{t.noHistory}</p>}<ol>{history?.items.map(h => <li key={h.revision}>r{h.revision} · {label(h.operation)} · {h.savedAt} <button disabled={busy} onClick={() => void exact(h.revision)}>{t.review} {h.revision}</button></li>)}</ol>
    {selected && <article aria-label={t.historyNote}><p>{t.historyNote}</p><p>r{selected.revision} · {selected.sourceVersion} · {selected.policyVersion} · {selected.savedAt}</p><p>{selected.inputs.plannedStart ?? t.missing} / {selected.inputs.plannedEnd ?? t.missing}</p><ul>{selected.inputs.assignedTugs.map(id => <li key={id}>{id}</li>)}</ul></article>}
  </section>
}
