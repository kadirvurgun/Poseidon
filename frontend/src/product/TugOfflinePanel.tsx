import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterpriseFormGrid } from './EnterprisePatterns'
import { OfflineCaptureMetadata, OfflineQueueSummary } from './OfflineQueueSummary'
import { useEffect, useEffectEvent, useRef, useState } from 'react'
import { ApiError } from './api'
import { prepareOfflineShell } from './offline-application'
import { TugOfflineApplication, tugOfflineInventory, reportTugLostKey, type TugConflict, type TugPending } from './tug-offline'

export function TugOfflinePanel({ job, vessel, language, onDirty }: { job: string; vessel: string; language: 'tr' | 'en'; onDirty?: (v: boolean) => void }) {
  const en = language === 'en'
  const [inventory, setInventory] = useState<{ id: string; revokedAt: string | null }[]>([]), [enrollment, setEnrollment] = useState('')
  const [app, setApp] = useState<TugOfflineApplication | null>(null), [pending, setPending] = useState<TugPending[]>([])
  const [start, setStart] = useState(''), [end, setEnd] = useState(''), [busy, setBusy] = useState(false), [state, setState] = useState('locked')
  const [conflict, setConflict] = useState<TugConflict | null>(null)
  const [online, setOnline] = useState(navigator.onLine)
  const scope = useRef(new AbortController()), handle = useRef<TugOfflineApplication | null>(null)
  const generation = useRef(0)
  const closeScope = useEffectEvent((bootstrap: AbortController) => {
    generation.current++; bootstrap.abort(); scope.current.abort()
    handle.current?.close(); handle.current = null
  })
  const [dirty, setDirty] = useState(false)
  useEffect(() => { onDirty?.(busy || dirty); return () => onDirty?.(false) }, [busy, dirty, onDirty])
  const resetScope1 = [job, vessel] as const
  const [previousScope1, setPreviousScope1] = useState(resetScope1)
  // Withdraw stale state in the same render that changes the request scope.
  if (!Object.is(previousScope1[0], job) || !Object.is(previousScope1[1], vessel)) {
    setPreviousScope1(resetScope1)
    setApp(null); setPending([]); setConflict(null); setState('locked'); setStart(''); setEnd(''); setInventory([]); setEnrollment(''); setBusy(false); setDirty(false)
  }
  useEffect(() => {
    const c = new AbortController(); scope.current = c

    if (navigator.onLine) tugOfflineInventory(job, vessel, c.signal).then(v => { if (!c.signal.aborted) setInventory(v) }).catch(() => { /* No cached inventory authority. */ })
    const withdraw = () => {
      generation.current++; scope.current.abort()
      handle.current?.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setBusy(false); setDirty(false); setState('locked')
    }
    const hidden = () => { if (document.hidden) withdraw() }
    let observedOnline = navigator.onLine
    const changed = () => { observedOnline = navigator.onLine; setOnline(observedOnline); if (observedOnline) withdraw() }
    // Reuse the existing local lease observer; this does not fetch inventory or
    // authorize/decrypt any retained context when the native hint changes.
    const timer = window.setInterval(() => {
      const currentOnline = navigator.onLine
      const reconnected = currentOnline && !observedOnline
      observedOnline = currentOnline; setOnline(currentOnline)
      if (reconnected) withdraw()
      if (handle.current && !handle.current.active()) withdraw()
    }, 1000)
    window.addEventListener('online', changed); window.addEventListener('offline', changed); window.addEventListener('pagehide', withdraw); document.addEventListener('visibilitychange', hidden)
    return () => { closeScope(c); window.clearInterval(timer); window.removeEventListener('online', changed); window.removeEventListener('offline', changed); window.removeEventListener('pagehide', withdraw); document.removeEventListener('visibilitychange', hidden) }
  }, [job, vessel])
  async function work(action: (current: () => boolean) => Promise<void>) {
    // A new signal is created only by a fresh explicit user action.
    if (scope.current.signal.aborted) scope.current = new AbortController()
    const c = scope.current, operation = ++generation.current
    const current = () => !c.signal.aborted && operation === generation.current
    setBusy(true)
    try { await action(current) }
    catch (e) {
      if (!current()) return
      if (e instanceof ApiError && [401, 403, 404].includes(e.status)) {
        handle.current?.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('denied')
      } else setState(e instanceof Error && e.message === 'key_lost_data_retained' ? 'keyLost' : 'uncertain')
    } finally { if (current()) setBusy(false) }
  }
  async function authorize(current: () => boolean) {
    const c = scope.current
    handle.current?.close(); handle.current = null; setApp(null); setPending([]); setConflict(null)
    await prepareOfflineShell(c.signal)
    if (!current()) return
    const next = await TugOfflineApplication.authorize(job, vessel, enrollment || null, c.signal)
    if (!current()) { next.close(); return }
    let rows: TugPending[]
    try { rows = await next.captures() } catch (error) { next.close(); throw error }
    if (!current()) { next.close(); return }
    handle.current = next; setApp(next); setEnrollment(next.context.id); setPending(rows)
    setStart(next.context.plannedStart?.slice(0, 19) ?? ''); setEnd(next.context.plannedEnd?.slice(0, 19) ?? ''); setDirty(false); setState('active')
  }
  const iso = (s: string) => s ? `${s.length === 16 ? s + ':00' : s}Z` : null
  return <section className="offline-workspace" aria-label={en ? 'Offline tug draft window' : 'Çevrimdışı römorkör taslak aralığı'}>
    <h4>{en ? 'Offline tug draft window' : 'Çevrimdışı römorkör taslak aralığı'}</h4>
    <p>{en ? 'Only planned times are captured. Reconnect and current authorization are required to commit. BP, assignments, readiness, files and approvals are excluded.' : 'Yalnız plan zamanları yakalanır. Kaydetmek için yeniden bağlantı ve güncel yetki gerekir. BP, atamalar, hazırlık, dosyalar ve onaylar kapsam dışıdır.'}</p>
    <p role="status">{state === 'active' ? (en ? 'Enrolled encrypted device capture available' : 'Kayıtlı cihazda şifreli yakalama kullanılabilir') : state === 'denied' ? (en ? 'Current access denied; encrypted data retained locked' : 'Güncel erişim reddedildi; şifreli veri kilitli korundu') : state === 'uncertain' ? (en ? 'Outcome uncertain. Retained captures must be retried exactly or reviewed after reauthorization.' : 'Sonuç belirsiz. Korunan kayıtları aynen yineleyin veya yeniden yetkilendirmeden sonra inceleyin.') : (en ? 'Locked. Online authorization required after restart.' : 'Kilitli. Yeniden başlatma sonrası çevrimiçi yetkilendirme gerekir.')}</p>
    {!app && <><label>{en ? 'Recover enrollment' : 'Cihaz kaydını kurtar'}<select value={enrollment} disabled={busy} onChange={e => setEnrollment(e.target.value)}><option value="">{en ? 'New device enrollment' : 'Yeni cihaz kaydı'}</option>{inventory.filter(e => !e.revokedAt).map(e => <option key={e.id} value={e.id}>{e.id}</option>)}</select></label>
      <Button variant="outline" disabled={busy || !online} onClick={() => void work(authorize)}>{en ? 'Authorize encrypted capture' : 'Şifreli yakalamayı yetkilendir'}</Button>
      {state === 'keyLost' && <><p role="alert">{en ? 'Device key unavailable. Encrypted captures remain locked; no replacement key is created.' : 'Cihaz anahtarı kullanılamıyor. Şifreli kayıtlar kilitli kalır; yerine yeni anahtar oluşturulmaz.'}</p><Button variant="outline" disabled={busy || !online || !enrollment} onClick={() => void work(async current => { await reportTugLostKey(job, vessel, enrollment, scope.current.signal); if (current()) setState('locked') })}>{en ? 'Report lost key' : 'Kayıp anahtarı bildir'}</Button></>}
    </>}
    {app && <><form onSubmit={e => { e.preventDefault(); void work(async current => { const rows = await app.capture({ plannedStart: iso(start), plannedEnd: iso(end) }); if (current()) { setPending(rows); setDirty(false); setState('active') } }) }}>
      <EnterpriseFormGrid disabled={busy}><label>{en ? 'Offline planned start (UTC)' : 'Çevrimdışı planlanan başlangıç (UTC)'}<input type="datetime-local" step="1" value={start} onChange={e => { setStart(e.target.value); setDirty(true) }} /></label>
        <label>{en ? 'Offline planned end (UTC)' : 'Çevrimdışı planlanan bitiş (UTC)'}<input type="datetime-local" step="1" value={end} onChange={e => { setEnd(e.target.value); setDirty(true) }} /></label></EnterpriseFormGrid>
      <EnterpriseActions aria-label={en ? 'Local capture actions' : 'Yerel yakalama işlemleri'}><Button type="submit" disabled={busy}>{en ? 'Capture encrypted draft' : 'Şifreli taslağı yakala'}</Button></EnterpriseActions></form>
      {pending.length === 0 && <p>{en ? 'No device captures' : 'Cihaz kaydı yok'}</p>}
      <OfflineQueueSummary captures={pending} language={language} /><ol className="offline-capture-list">{pending.map(p => <li key={p.commandId}>{p.commandId} · {p.outcome?.result === 'Committed' ? (en ? 'Committed' : 'Kaydedildi') : p.outcome ? (en ? 'Material conflict' : 'Maddi çatışma') : (en ? 'Encrypted capture; not committed' : 'Şifreli yakalama; kaydedilmedi')}
        <OfflineCaptureMetadata capture={p} language={language} /><EnterpriseActions align="start" aria-label={en ? 'Capture actions' : 'Yakalama işlemleri'}><Button variant="outline" disabled={busy || !online} onClick={() => void work(async current => { await app.replay(p, scope.current.signal); if (!current()) return; const rows = await app.captures(); if (current()) { setPending(rows); setState('active') } })}>{en ? 'Replay exact capture' : 'Aynı yakalamayı gönder'}</Button>
        {p.outcome?.result === 'MaterialConflict' && <Button variant="outline" disabled={busy} onClick={() => void work(async current => { const v = await app.conflict(p.commandId, scope.current.signal); if (current()) setConflict(v) })}>{en ? 'Review conflict' : 'Çatışmayı incele'}</Button>}</EnterpriseActions>
      </li>)}</ol>
      {conflict && <article aria-label={en ? 'Window conflict review' : 'Zaman çatışması incelemesi'}><p>{en ? 'Base / captured / current server' : 'Temel / yakalanan / güncel sunucu'}</p><dl className="offline-window-comparison">{[conflict.base, conflict.captured, conflict.server].map((v, i) => <div key={i}><dt>{(en ? ['Before capture', 'Captured', 'Current server'] : ['Yakalama öncesi', 'Yakalanan', 'Güncel sunucu'])[i]}</dt><dd>{v.plannedStart ?? '—'} / {v.plannedEnd ?? '—'}</dd></div>)}</dl><EnterpriseActions align="start" aria-label={en ? 'Window resolution choices' : 'Zaman çözüm seçenekleri'}>
        {(['keepServer', 'useCaptured'] as const).filter(c => conflict.choices.includes(c)).map(choice => <Button variant="outline" key={choice} disabled={busy} onClick={() => void work(async current => { const rows = await app.capture(choice === 'keepServer' ? conflict.server : conflict.captured, { conflict, choice }); if (current()) { setPending(rows); setConflict(null) } })}>{choice === 'keepServer' ? (en ? 'Retain current server times' : 'Güncel sunucu zamanlarını koru') : (en ? 'Propose captured times with reason' : 'Yakalanan zamanları gerekçeyle öner')}</Button>)}
      </EnterpriseActions></article>}
      <Button variant="outline" disabled={busy} onClick={() => void work(async current => { await app.lifecycle('revoke', scope.current.signal); if (current()) { handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('locked') } })}>{en ? 'Revoke device capture' : 'Cihaz yakalama yetkisini geri çek'}</Button>
      <Button variant="outline" disabled={busy} onClick={() => { generation.current++; scope.current.abort(); app.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('locked') }}>{en ? 'Lock retained captures' : 'Korunan kayıtları kilitle'}</Button>
    </>}
  </section>
}
