import { useEffect, useRef, useState } from 'react'
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
  const [dirty, setDirty] = useState(false)
  useEffect(() => { onDirty?.(busy || dirty); return () => onDirty?.(false) }, [busy, dirty, onDirty])
  useEffect(() => {
    const c = new AbortController(); scope.current = c
    setApp(null); setPending([]); setConflict(null); setState('locked'); setStart(''); setEnd(''); setInventory([]); setEnrollment(''); setBusy(false); setDirty(false)
    if (navigator.onLine) tugOfflineInventory(job, vessel, c.signal).then(v => { if (!c.signal.aborted) setInventory(v) }).catch(() => { /* No cached inventory authority. */ })
    const timer = window.setInterval(() => { if (handle.current && !handle.current.active()) { handle.current.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('locked') } }, 1000)
    const withdraw = () => {
      if (!handle.current) return
      handle.current.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setDirty(false); setState('locked')
    }
    const hidden = () => { if (document.hidden) withdraw() }
    const changed = () => { setOnline(navigator.onLine); if (navigator.onLine) withdraw() }
    window.addEventListener('online', changed); window.addEventListener('offline', changed); window.addEventListener('pagehide', withdraw); document.addEventListener('visibilitychange', hidden)
    return () => { c.abort(); handle.current?.close(); handle.current = null; window.clearInterval(timer); window.removeEventListener('online', changed); window.removeEventListener('offline', changed); window.removeEventListener('pagehide', withdraw); document.removeEventListener('visibilitychange', hidden) }
  }, [job, vessel])
  async function work(action: () => Promise<void>) {
    const c = scope.current; setBusy(true)
    try { await action() }
    catch (e) {
      if (c.signal.aborted) return
      if (e instanceof ApiError && [401, 403, 404].includes(e.status)) {
        handle.current?.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('denied')
      } else setState(e instanceof Error && e.message === 'key_lost_data_retained' ? 'keyLost' : 'uncertain')
    } finally { if (!c.signal.aborted) setBusy(false) }
  }
  async function authorize() {
    const c = scope.current
    handle.current?.close(); handle.current = null; setApp(null); setPending([]); setConflict(null)
    await prepareOfflineShell(c.signal)
    const next = await TugOfflineApplication.authorize(job, vessel, enrollment || null, c.signal)
    if (c.signal.aborted) { next.close(); return }
    const rows = await next.captures(); if (c.signal.aborted) { next.close(); return }
    handle.current = next; setApp(next); setEnrollment(next.context.id); setPending(rows)
    setStart(next.context.plannedStart?.slice(0, 19) ?? ''); setEnd(next.context.plannedEnd?.slice(0, 19) ?? ''); setDirty(false); setState('active')
  }
  const iso = (s: string) => s ? `${s.length === 16 ? s + ':00' : s}Z` : null
  return <section aria-label={en ? 'Offline tug draft window' : 'Çevrimdışı römorkör taslak aralığı'}>
    <h4>{en ? 'Offline tug draft window' : 'Çevrimdışı römorkör taslak aralığı'}</h4>
    <p>{en ? 'Only planned times are captured. Reconnect and current authorization are required to commit. BP, assignments, readiness, files and approvals are excluded.' : 'Yalnız plan zamanları yakalanır. Kaydetmek için yeniden bağlantı ve güncel yetki gerekir. BP, atamalar, hazırlık, dosyalar ve onaylar kapsam dışıdır.'}</p>
    <p role="status">{state === 'active' ? (en ? 'Enrolled encrypted device capture available' : 'Kayıtlı cihazda şifreli yakalama kullanılabilir') : state === 'denied' ? (en ? 'Current access denied; encrypted data retained locked' : 'Güncel erişim reddedildi; şifreli veri kilitli korundu') : state === 'uncertain' ? (en ? 'Outcome uncertain. Retained captures must be retried exactly or reviewed after reauthorization.' : 'Sonuç belirsiz. Korunan kayıtları aynen yineleyin veya yeniden yetkilendirmeden sonra inceleyin.') : (en ? 'Locked. Online authorization required after restart.' : 'Kilitli. Yeniden başlatma sonrası çevrimiçi yetkilendirme gerekir.')}</p>
    {!app && <><label>{en ? 'Recover enrollment' : 'Cihaz kaydını kurtar'}<select value={enrollment} disabled={busy} onChange={e => setEnrollment(e.target.value)}><option value="">{en ? 'New device enrollment' : 'Yeni cihaz kaydı'}</option>{inventory.filter(e => !e.revokedAt).map(e => <option key={e.id} value={e.id}>{e.id}</option>)}</select></label>
      <button disabled={busy || !online} onClick={() => void work(authorize)}>{en ? 'Authorize encrypted capture' : 'Şifreli yakalamayı yetkilendir'}</button>
      {state === 'keyLost' && <><p role="alert">{en ? 'Device key unavailable. Encrypted captures remain locked; no replacement key is created.' : 'Cihaz anahtarı kullanılamıyor. Şifreli kayıtlar kilitli kalır; yerine yeni anahtar oluşturulmaz.'}</p><button disabled={busy || !online || !enrollment} onClick={() => void work(async () => { await reportTugLostKey(job, vessel, enrollment, scope.current.signal); setState('locked') })}>{en ? 'Report lost key' : 'Kayıp anahtarı bildir'}</button></>}
    </>}
    {app && <><form onSubmit={e => { e.preventDefault(); void work(async () => { setPending(await app.capture({ plannedStart: iso(start), plannedEnd: iso(end) })); setDirty(false); setState('active') }) }}>
      <fieldset disabled={busy}><label>{en ? 'Offline planned start (UTC)' : 'Çevrimdışı planlanan başlangıç (UTC)'}<input type="datetime-local" step="1" value={start} onChange={e => { setStart(e.target.value); setDirty(true) }} /></label>
        <label>{en ? 'Offline planned end (UTC)' : 'Çevrimdışı planlanan bitiş (UTC)'}<input type="datetime-local" step="1" value={end} onChange={e => { setEnd(e.target.value); setDirty(true) }} /></label></fieldset>
      <button disabled={busy}>{en ? 'Capture encrypted draft' : 'Şifreli taslağı yakala'}</button></form>
      {pending.length === 0 && <p>{en ? 'No device captures' : 'Cihaz kaydı yok'}</p>}
      <ol>{pending.map(p => <li key={p.commandId}>{p.commandId} · {p.outcome?.result === 'Committed' ? (en ? 'Committed' : 'Kaydedildi') : p.outcome ? (en ? 'Material conflict' : 'Maddi çatışma') : (en ? 'Encrypted capture; not committed' : 'Şifreli yakalama; kaydedilmedi')}
        <button disabled={busy || !online} onClick={() => void work(async () => { await app.replay(p, scope.current.signal); if (!scope.current.signal.aborted) { setPending(await app.captures()); setState('active') } })}>{en ? 'Replay exact capture' : 'Aynı yakalamayı gönder'}</button>
        {p.outcome?.result === 'MaterialConflict' && <button disabled={busy} onClick={() => void work(async () => { const v = await app.conflict(p.commandId, scope.current.signal); if (!scope.current.signal.aborted) setConflict(v) })}>{en ? 'Review conflict' : 'Çatışmayı incele'}</button>}
      </li>)}</ol>
      {conflict && <article aria-label={en ? 'Window conflict review' : 'Zaman çatışması incelemesi'}><p>{en ? 'Base / captured / current server' : 'Temel / yakalanan / güncel sunucu'}</p>{[conflict.base, conflict.captured, conflict.server].map((v, i) => <p key={i}>{v.plannedStart ?? '—'} / {v.plannedEnd ?? '—'}</p>)}
        {(['keepServer', 'useCaptured'] as const).filter(c => conflict.choices.includes(c)).map(choice => <button key={choice} disabled={busy} onClick={() => void work(async () => { setPending(await app.capture(choice === 'keepServer' ? conflict.server : conflict.captured, { conflict, choice })); setConflict(null) })}>{choice === 'keepServer' ? (en ? 'Retain current server times' : 'Güncel sunucu zamanlarını koru') : (en ? 'Propose captured times with reason' : 'Yakalanan zamanları gerekçeyle öner')}</button>)}
      </article>}
      <button disabled={busy} onClick={() => void work(async () => { await app.lifecycle('revoke', scope.current.signal); setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('locked') })}>{en ? 'Revoke device capture' : 'Cihaz yakalama yetkisini geri çek'}</button>
      <button disabled={busy} onClick={() => { app.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('locked') }}>{en ? 'Lock retained captures' : 'Korunan kayıtları kilitle'}</button>
    </>}
  </section>
}
