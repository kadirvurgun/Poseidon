import { useEffectEvent, useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import { OfflineConflictReview, type OfflineConflictResolution } from './OfflineConflictReview'
import { OfflineApplication, inventory, discardLostCaptures, prepareOfflineShell, type OfflineInventory, type PendingCapture, type OfflineConflictDetail, type OfflineOutcome } from './offline-application'

export function OfflineApplicationPanel({ vessel, assignment, language, onDirty }: {
  vessel: string; assignment: string; language: 'tr' | 'en'; onDirty: (value: boolean) => void
}) {
  const en = language === 'en'
  const app = useRef<OfflineApplication | null>(null), generation = useRef(0), request = useRef<AbortController | null>(null)
  const [contexts, setContexts] = useState<OfflineInventory['items']>([]), [selected, setSelected] = useState('')
  const [active, setActive] = useState(false), [online, setOnline] = useState(navigator.onLine), [busy, setBusy] = useState(false)
  const [status, setStatus] = useState<'locked' | 'ready' | 'retained' | 'failed' | 'denied' | 'keyLost'>('locked')
  const [captures, setCaptures] = useState<PendingCapture[]>([]), [watch, setWatch] = useState(''), [dirty, setDirty] = useState(false)
  const [snapshot, setSnapshot] = useState<{ watchGroup: string | null; baseVersion: number; revision: number } | null>(null)
  const [conflict, setConflict] = useState<OfflineConflictDetail | null>(null), [history, setHistory] = useState<OfflineOutcome[]>([])
  const [historyOffset, setHistoryOffset] = useState(0), [historyMore, setHistoryMore] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false), [uncertain, setUncertain] = useState(false)
  const [lease, setLease] = useState(0)
  const [authorizedContext, setAuthorizedContext] = useState<OfflineApplication['context'] | null>(null)
  const withdraw = useCallback(() => {
    generation.current++; request.current?.abort(); app.current?.close(); app.current = null
    setActive(false); setAuthorizedContext(null); setBusy(false); setCaptures([]); setConflict(null); setHistory([]); setHistoryOffset(0); setHistoryMore(false); setSnapshot(null); setStatus('retained')
    // Unsaved in-memory editor is retained while hidden, but never exposed after scope remount.
  }, [])
  useEffect(() => { onDirty(dirty || busy); return () => onDirty(false) }, [dirty, busy, onDirty])
  const closeScope = useEffectEvent(() => { generation.current++; request.current?.abort(); app.current?.close() })
  useEffect(() => {
    const hidden = () => { if (document.hidden) withdraw() }
    let observedOnline = navigator.onLine
    const changed = () => {
      observedOnline = navigator.onLine
      setOnline(observedOnline)
      // A real online event remains a conservative withdrawal signal.
      if (observedOnline) withdraw()
    }
    const synchronizeHint = () => {
      const currentOnline = navigator.onLine
      const reconnected = currentOnline && !observedOnline
      observedOnline = currentOnline
      setOnline(currentOnline)
      // Some browsers update the native hint after offline bootstrap without
      // emitting online. This local check grants no authority or network read.
      if (reconnected) withdraw()
    }
    const pagehide = () => withdraw()
    window.addEventListener('online', changed); window.addEventListener('offline', changed)
    window.addEventListener('pagehide', pagehide); document.addEventListener('visibilitychange', hidden)
    const networkHintTimer = window.setInterval(synchronizeHint, 1000)
    return () => {
      closeScope()
      window.clearInterval(networkHintTimer)
      window.removeEventListener('online', changed); window.removeEventListener('offline', changed)
      window.removeEventListener('pagehide', pagehide); document.removeEventListener('visibilitychange', hidden)
    }
  }, [vessel, assignment, withdraw])
  useEffect(() => {
    const authorized = app.current
    if (!active || !authorized) return
    // This timer WITHDRAWS expired access. It never contacts the server or renews authorization.
    const timer = setTimeout(() => { if (app.current === authorized) withdraw() }, authorized.remaining())
    return () => clearTimeout(timer)
  }, [active, lease, withdraw])
  async function run(work: (signal: AbortSignal, current: number) => Promise<void>) {
    if (busy) return
    const current = ++generation.current; request.current?.abort(); const controller = new AbortController(); request.current = controller
    setBusy(true)
    try { await work(controller.signal, current) }
    catch (error) {
      if (current !== generation.current || controller.signal.aborted) return
      if (error instanceof ApiError && [401, 403, 404].includes(error.status)) { withdraw(); setWatch(''); setDirty(false); setStatus('denied') }
      else { setStatus(error instanceof Error && error.message === 'key_lost_data_retained' ? 'keyLost' : 'failed'); setUncertain(true) }
    } finally { if (current === generation.current) setBusy(false) }
  }
  const fresh = (current: number, signal: AbortSignal) => current === generation.current && !signal.aborted
  const authorize = () => run(async (signal, current) => {
    const previous = app.current; app.current = null; previous?.close(); setActive(false); setAuthorizedContext(null); setCaptures([]); setSnapshot(null); setConflict(null); setHistory([])
    const value = await OfflineApplication.authorize(vessel, assignment, selected || null, signal)
    if (!fresh(current, signal)) { value.close(); return }
    let rows: PendingCapture[], base: { watchGroup: string | null; baseVersion: number; revision: number }
    try { await prepareOfflineShell(signal); rows = await value.captures(); base = await value.snapshot() }
    catch (error) { value.close(); throw error }
    if (!fresh(current, signal)) { value.close(); return }
    app.current = value; setAuthorizedContext(value.context); setSelected(value.context.id); setCaptures(rows); setSnapshot(base)
    value.onWithdraw(() => { if (app.current === value) withdraw() })
    setWatch(previous => dirty ? previous : base.watchGroup ?? ''); setActive(true); setLease(n => n + 1); setStatus('ready'); setUncertain(false); setConfirmDiscard(false)
  })
  const refresh = () => run(async (signal, current) => {
    const result = await inventory(vessel, assignment, signal)
    if (fresh(current, signal)) { setContexts(result.items); setStatus('retained') }
  })
  const capture = () => run(async (signal, current) => {
    const value = app.current; if (!value) return
    await value.capture(watch || null)
    const rows = await value.captures()
    if (fresh(current, signal)) { setCaptures(rows); setDirty(false); setStatus('ready') }
  })
  const replay = (row: PendingCapture) => run(async (signal, current) => {
    const value = app.current; if (!value) return
    await value.replay(row, signal); const rows = await value.captures()
    if (fresh(current, signal)) { setCaptures(rows); setStatus('ready'); setUncertain(false) }
  })
  const review = (id: string) => run(async (signal, current) => {
    const value = app.current; if (!value) return
    const result = await value.conflict(id, signal)
    if (fresh(current, signal)) { setConflict(result); setUncertain(false) }
  })
  const resolve = (resolution: OfflineConflictResolution) => run(async (signal, current) => {
    const value = app.current; if (!value) return
    // Persist resolution as a new frozen command BEFORE network submission.
    const id = await value.capture(null, resolution), rows = await value.captures()
    if (!fresh(current, signal)) return
    setCaptures(rows); setConflict(null); await value.replay(rows.find(x => x.commandId === id)!, signal)
    const updated = await value.captures(); if (fresh(current, signal)) { setCaptures(updated); setStatus('ready') }
  })
  const loadHistory = (offset: number) => run(async (signal, current) => {
    const value = app.current; if (!value) return
    const page = await value.history(signal, offset)
    if (fresh(current, signal)) { setHistory(page.items); setHistoryOffset(page.offset); setHistoryMore(page.hasMore) }
  })
  const live = active && authorizedContext !== null
  return <section aria-label={en ? 'Durable offline drafts' : 'Kalıcı çevrimdışı taslaklar'}>
    <h2>{en ? 'Durable offline drafts' : 'Kalıcı çevrimdışı taslaklar'}</h2>
    <p>{en ? 'Only configured draft watch groups can be cached and captured here. Identity, health, certificates and operational decisions are excluded. Drafts remain unverified.'
      : 'Burada yalnız yapılandırılmış taslak vardiya grupları saklanabilir. Kimlik, sağlık, belgeler ve operasyonel kararlar kapsam dışıdır. Taslaklar doğrulanmamıştır.'}</p>
    <p role="status">{status === 'ready' && live ? (en ? 'Encrypted capture available for this authorization.' : 'Bu yetkilendirmeyle şifreli kayıt kullanılabilir.')
      : status === 'denied' ? (en ? 'Current access denied. Local encrypted data retained.' : 'Güncel erişim reddedildi. Yerel şifreli veri korunuyor.')
      : status === 'keyLost' ? (en ? 'Local key unavailable. Data retained; recover the original browser key or explicitly discard.' : 'Yerel anahtar kullanılamıyor. Veri korunuyor; özgün tarayıcı anahtarını kurtarın veya açıkça silin.')
      : status === 'failed' ? (en ? 'Request failed or outcome unknown. Stored captures retained. Reauthorize and retry the same command.' : 'İstek başarısız veya sonuç belirsiz. Kayıtlar korunuyor. Yeniden yetkilendirip aynı komutu deneyin.')
      : (en ? 'Stored drafts are locked. Connect and explicitly authorize recovery.' : 'Saklanan taslaklar kilitli. Bağlanıp kurtarmayı açıkça yetkilendirin.')}</p>
    <a href={`/api/v1/auth/login?returnUrl=${encodeURIComponent(`/app/crew-roster/${vessel}/crew-profile/${assignment}`)}`}>{en ? 'Sign in again' : 'Yeniden oturum aç'}</a>
    <button disabled={!online || busy || dirty} onClick={() => void refresh()}>{en ? 'Find retained contexts' : 'Korunan bağlamları bul'}</button>
    <label>{en ? 'Offline context' : 'Çevrimdışı bağlam'}<select value={selected} disabled={busy || dirty} onChange={e => { withdraw(); setSelected(e.target.value) }}>
      <option value="">{en ? 'Enroll new device key' : 'Yeni cihaz anahtarı kaydet'}</option>
      {selected && !contexts.some(x => x.id === selected) && <option value={selected}>{en ? 'Current retained context' : 'Geçerli korunan bağlam'}</option>}
      {contexts.map((c, index) => <option key={c.id} value={c.id}>{en ? 'Retained context' : 'Korunan bağlam'} {index + 1}{c.revokedAt ? (en ? ' · revoked' : ' · iptal edildi') : ''}</option>)}
    </select></label>
    <button disabled={!online || busy} onClick={() => void authorize()}>{en ? 'Authorize / renew / recover' : 'Yetkilendir / yenile / kurtar'}</button>
    {live && snapshot && <>
      <p>{en ? 'Cached unverified base' : 'Saklanan doğrulanmamış temel'} · {snapshot.revision} · {snapshot.watchGroup ?? (en ? 'Unknown' : 'Bilinmiyor')}</p>
      <label>{en ? 'Offline draft watch group' : 'Çevrimdışı taslak vardiya grubu'}<select value={watch} disabled={busy} onChange={e => { setWatch(e.target.value); setDirty(true) }}>
        <option value="">{en ? 'Unknown' : 'Bilinmiyor'}</option>{authorizedContext?.watchGroups.map(v => <option key={v}>{v}</option>)}
      </select></label>
      <button disabled={busy} onClick={() => void capture()}>{en ? 'Capture encrypted draft' : 'Şifreli taslak kaydet'}</button>
      <button disabled={busy} onClick={() => { setWatch(snapshot.watchGroup ?? ''); setDirty(false) }}>{en ? 'Discard unsaved editor' : 'Kaydedilmemiş düzenlemeyi bırak'}</button>
      <ul aria-label={en ? 'Stored capture status' : 'Saklanan kayıt durumu'}>{captures.map((row, index) => <li key={row.commandId}>
        {en ? 'Capture' : 'Kayıt'} {index + 1} · {row.outcome?.result ?? (en ? 'Unsent / outcome unknown' : 'Gönderilmemiş / sonuç belirsiz')}
        <button disabled={busy || !online} onClick={() => void replay(row)}>{en ? 'Replay / check original outcome' : 'Tekrarla / özgün sonucu sorgula'}</button>
        {row.outcome?.result === 'MaterialConflict' && <button disabled={busy || !online} onClick={() => void review(row.commandId)}>{en ? 'Review material conflict' : 'Maddi çakışmayı incele'}</button>}
        <button disabled={busy || !confirmDiscard} onClick={() => void run(async (signal, current) => {
          const value = app.current; if (!value) return
          await value.discard(row, confirmDiscard); const rows = await value.captures()
          if (fresh(current, signal)) { setCaptures(rows); setConfirmDiscard(false) }
        })}>{en ? 'Discard this stored capture' : 'Bu saklanan kaydı sil'}</button>
      </li>)}</ul>
      <button disabled={busy || !online} onClick={() => void loadHistory(0)}>{en ? 'Load synchronization history' : 'Eşitleme geçmişini yükle'}</button>
      <ul aria-label={en ? 'Synchronization history' : 'Eşitleme geçmişi'}>{history.map((o, i) => <li key={o.commandId}>{i + 1} · {o.result} · {o.revision} · {o.receivedAt}{o.parentCommandId ? (en ? ' · explicit resolution lineage' : ' · açık çözüm bağı') : ''}</li>)}</ul>
      <button disabled={busy || !online || historyOffset === 0} onClick={() => void loadHistory(Math.max(0, historyOffset - 25))}>{en ? 'Previous synchronization history' : 'Önceki eşitleme geçmişi'}</button>
      <button disabled={busy || !online || !historyMore} onClick={() => void loadHistory(historyOffset + 25)}>{en ? 'Next synchronization history' : 'Sonraki eşitleme geçmişi'}</button>
      <button disabled={busy || !online || !confirmDiscard} onClick={() => void run(async signal => { await app.current?.revoke(false, signal); withdraw() })}>{en ? 'Revoke device context and retain data' : 'Cihaz bağlamını iptal et ve veriyi koru'}</button>
    </>}
    <label><input type="checkbox" checked={confirmDiscard} onChange={e => setConfirmDiscard(e.target.checked)} />{en ? 'I confirm the selected discard or revocation; unsent data may be irrecoverable.' : 'Seçilen silme veya iptali onaylıyorum; gönderilmemiş veri kurtarılamayabilir.'}</label>
    {selected && !live && <button disabled={!online || busy || !confirmDiscard} onClick={() => void run(async (signal, current) => {
      await discardLostCaptures(vessel, assignment, selected, confirmDiscard, signal)
      if (fresh(current, signal)) { setSelected(''); setStatus('retained'); setConfirmDiscard(false) }
    })}>{en ? 'Discard retained ciphertext and report key loss' : 'Korunan şifreli veriyi sil ve anahtar kaybını bildir'}</button>}
    {live && conflict && <>
      <p>{en ? 'Authorized base value' : 'Yetkili temel değer'}: {conflict.base.kind === 'known' ? conflict.base.text : (en ? 'Unknown / withheld' : 'Bilinmiyor / gizli')}</p>
      {conflict.resolved && <p>{en ? 'Recorded resolution' : 'Kayıtlı çözüm'} · {conflict.resolvedValue?.kind === 'known' ? conflict.resolvedValue.text : (en ? 'Unknown / withheld' : 'Bilinmiyor / gizli')}
        {conflict.resolvedBy === authorizedContext?.actorId ? (en ? ' · attributed to this account' : ' · bu hesaba atfedildi') : ''} · {conflict.resolutionReason}</p>}
      <OfflineConflictReview language={language} contextKey={authorizedContext!.id} state="ready" conflict={conflict}
        canResolve={conflict.resolved === null && conflict.currentVersion === Number(conflict.revision)} online={online} operation={busy ? 'submitting' : uncertain ? 'outcomeUnknown' : 'idle'}
        reasonLimit={500} onResolve={r => void resolve(r)} onCheckOutcome={() => void review(conflict.id)} onRetry={() => void review(conflict.id)} />
    </>}
  </section>
}
