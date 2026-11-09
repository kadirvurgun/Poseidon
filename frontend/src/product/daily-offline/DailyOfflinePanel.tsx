import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { Button } from '../../components/ui/button'
import { Badge } from '../../components/ui/badge'
import { EnterpriseActions } from '../EnterprisePatterns'
import { ApiError } from '../api'
import { dailyFields, type DailyFields, type DailyKind, type DailyValue } from '../daily-report-fields'
import type { DailyRecord } from '../daily-operations-api'
import { DailyOfflineApplication, dailyOfflineInventory, dailyOfflineLifecycle, discardDailyLostCaptures } from './daily-offline-application'
import { projectDailyQueue, type DailyPending, type DailyConflict, type DailyInventoryItem, type DailyOfflineContext } from './daily-offline-contract'
import './daily-offline.css'

type Props = { vessel: string; kind: DailyKind; recordId: string; record: DailyRecord | null; fields: DailyFields; enabled: boolean;
  language: 'en' | 'tr'; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void; onCommitted: () => void }
export function DailyOfflinePanel({ vessel, kind, recordId, record, fields, enabled, language, onDirty, onSaving, onCommitted }: Props) {
  const en = language === 'en', app = useRef<DailyOfflineApplication | null>(null), epoch = useRef(0), running = useRef(false), request = useRef<AbortController | null>(null)
  const [context, setContext] = useState<DailyOfflineContext | null>(null), [active, setActive] = useState(false), [online, setOnline] = useState(navigator.onLine), [busy, setBusy] = useState(false)
  const [contexts, setContexts] = useState<DailyInventoryItem[]>([]), [selected, setSelected] = useState(''), [captures, setCaptures] = useState<DailyPending[]>([])
  const [local, setLocal] = useState<DailyFields>({}), [dirty, setDirty] = useState(false), [retained, setRetained] = useState(false), [conflict, setConflict] = useState<DailyConflict | null>(null)
  const [status, setStatus] = useState<'locked' | 'ready' | 'unavailable' | 'retained' | 'unknown' | 'keyLost'>('locked'), [confirmPurge, setConfirmPurge] = useState(false)
  const callbacks = useRef({ onDirty, onSaving, onCommitted })
  const retainedDraft = useRef<{ context: DailyOfflineContext; fields: DailyFields } | null>(null)
  const privateState = useRef({ local, dirty, pending: false })
  const queue = useMemo(() => projectDailyQueue(captures, context), [captures, context])
  const [memoryHeld, setMemoryHeld] = useState(false)
  useLayoutEffect(() => { privateState.current = { local, dirty, pending: queue.pending > 0 } }, [local, dirty, queue.pending])
  useLayoutEffect(() => { callbacks.current = { onDirty, onSaving, onCommitted } }, [onDirty, onSaving, onCommitted])
  const withdraw = useCallback(() => {
    const previous = app.current
    if (previous && privateState.current.dirty) retainedDraft.current = { context: previous.context, fields: structuredClone(privateState.current.local) }
    const hasRetained = !!retainedDraft.current || privateState.current.pending || !!previous && running.current
    if (hasRetained) { setRetained(true); callbacks.current.onDirty(true) }
    setMemoryHeld(!!retainedDraft.current)
    epoch.current++; request.current?.abort(); previous?.close(); app.current = null
    setActive(false); setContext(null); setLocal({}); setCaptures([]); setConflict(null); setBusy(false); setDirty(false); setStatus('retained'); running.current = false; callbacks.current.onSaving(false)
  }, [])
  useEffect(() => { callbacks.current.onDirty(dirty || retained || queue.pending > 0); return () => callbacks.current.onDirty(false) }, [dirty, retained, queue.pending])
  useEffect(() => { callbacks.current.onSaving(busy); return () => callbacks.current.onSaving(false) }, [busy])
  useEffect(() => {
    const lifetime = epoch
    const network = () => { setOnline(navigator.onLine); if (navigator.onLine) withdraw() }
    const hidden = () => { if (document.hidden) withdraw() }
    window.addEventListener('online', network); window.addEventListener('offline', network); window.addEventListener('pagehide', withdraw); document.addEventListener('visibilitychange', hidden)
    return () => { lifetime.current++; request.current?.abort(); app.current?.close(); window.removeEventListener('online', network); window.removeEventListener('offline', network); window.removeEventListener('pagehide', withdraw); document.removeEventListener('visibilitychange', hidden); callbacks.current.onDirty(false); callbacks.current.onSaving(false) }
  }, [withdraw, vessel, kind, recordId])
  useEffect(() => { if (!enabled) withdraw() }, [enabled, withdraw])
  useEffect(() => { if (!active || !app.current) return; const timer = setTimeout(withdraw, app.current.remaining()); return () => clearTimeout(timer) }, [active, withdraw])
  async function run(work: (signal: AbortSignal, current: number) => Promise<void>, replay = false) {
    if (running.current || !enabled) return
    running.current = true; setBusy(true); callbacks.current.onSaving(true)
    const current = ++epoch.current, controller = new AbortController(); request.current?.abort(); request.current = controller
    const timer = setTimeout(() => controller.abort(), 20000)
    try { await work(controller.signal, current) }
    catch (error) {
      if (current !== epoch.current) return
      if (error instanceof ApiError && [401, 403, 404].includes(error.status)) { setRetained(true); withdraw(); setStatus('unavailable') }
      else { if (replay) setRetained(true); setStatus(error instanceof Error && error.message === 'key_lost_data_retained' ? 'keyLost' : replay ? 'unknown' : 'unavailable') }
    } finally { clearTimeout(timer); if (current === epoch.current) { running.current = false; setBusy(false); callbacks.current.onSaving(false) } }
  }
  const fresh = (current: number, signal: AbortSignal) => current === epoch.current && !signal.aborted && enabled
  const inventory = () => run(async (signal, current) => { const rows = await dailyOfflineInventory(kind, vessel, recordId, signal); if (fresh(current, signal)) setContexts(rows) })
  const authorize = () => run(async (signal, current) => {
    const previous = app.current
    if (previous && privateState.current.dirty) { retainedDraft.current = { context: previous.context, fields: structuredClone(privateState.current.local) }; setMemoryHeld(true) }
    previous?.close(); app.current = null; setActive(false); setContext(null); setLocal({}); setCaptures([]); setConflict(null)
    const value = await DailyOfflineApplication.authorize(kind, vessel, recordId, selected || null, signal)
    try {
      const rows = await value.captures()
      if (!fresh(current, signal)) { value.close(); return }
      const held = retainedDraft.current
      const same = held && held.context.id === value.context.id && held.context.actorId === value.context.actorId && held.context.tenantId === value.context.tenantId && held.context.recordId === value.context.recordId && held.context.kind === value.context.kind && held.context.policyDigest === value.context.policyDigest && held.context.authorityDigest === value.context.authorityDigest && held.context.schemaHash === value.context.schemaHash
      if (held && !same) { value.close(); throw new Error('original_unsaved_context_required') }
      value.onWithdraw(withdraw); app.current = value; setSelected(value.context.id); setCaptures(rows); setContext(value.context); setActive(true); setDirty(!!same); setRetained(false); setMemoryHeld(false); setStatus('ready')
      setLocal(same && held ? held.fields : Object.fromEntries(value.context.allowedFields.map(key => [key, fields[key] ?? value.context.baseFields[key]])))
      retainedDraft.current = null
    } catch (error) { value.close(); throw error }
  })
  const capture = () => run(async (_signal, current) => {
    const value = app.current; if (!value?.active()) throw new Error('reauthorization_required_data_retained')
    const rows = await value.capture(local); if (current !== epoch.current) return
    setCaptures(rows); setDirty(false); setRetained(false); setStatus('ready')
  })
  const replay = (row: DailyPending) => run(async (signal, current) => {
    const value = app.current; if (!value?.active()) throw new Error('reauthorization_required_data_retained')
    const outcome = await value.replay(row, signal); const rows = await value.captures()
    if (!fresh(current, signal)) return
    setCaptures(rows); setRetained(false); setStatus('ready')
    if (outcome.result === 'MaterialConflict' && !projectDailyQueue(rows, value.context).resolved.has(row.commandId)) setConflict(await value.conflict(row.commandId, signal))
    else { setConflict(null); if (outcome.result !== 'MaterialConflict') callbacks.current.onCommitted() }
  }, true)
  const resolve = (choice: 'keepServer' | 'useCaptured') => run(async (signal, current) => {
    const value = app.current; if (!value?.active() || !conflict) return
    // This creates a retained child command. Sending it remains a separate,
    // explicit action, and the server rechecks the exact current version.
    const rows = await value.capture({}, { conflict, choice }); if (fresh(current, signal)) { setCaptures(rows); setConflict(null); setStatus('ready') }
  })
  const label = (key: string) => dailyFields(kind).find(f => f.key === key)?.[language] ?? key
  const viewValue = (value: DailyValue) => value.state === 'Known' ? value.value : value.state === 'Unknown' ? (en ? 'Unknown' : 'Bilinmiyor') : (en ? 'Missing' : 'Eksik')
  const locked = !enabled || busy, current = enabled && document.visibilityState !== 'hidden' && active ? context : null
  return <section className="daily-offline-panel" aria-label={en ? 'Encrypted daily capture' : 'Şifreli günlük kayıt'}>
    <header><h3>{en ? 'Encrypted offline draft' : 'Şifreli çevrimdışı taslak'}</h3><Badge tone={current ? 'info' : 'warning'}>{current ? (en ? 'Explicit bounded authorization' : 'Açık ve süreli yetkilendirme') : (en ? 'Locked · online authorization required' : 'Kilitli · çevrimiçi yetkilendirme gerekli')}</Badge></header>
    <p>{en ? 'Only explicitly classified fields can be captured. Review, approval, verification and system fields are excluded. Retained ciphertext is not proof of a saved server report.' : 'Yalnız açıkça sınıflandırılmış alanlar kaydedilebilir. İnceleme, onay, doğrulama ve sistem alanları kapsam dışıdır. Korunan şifreli veri, sunucu raporunun kaydedildiğini göstermez.'}</p>
    {status === 'unavailable' && <p role="alert">{en ? 'Daily offline policy, current permission, storage or key context is unavailable. No authority is inferred from crew or tug access.' : 'Günlük çevrimdışı politika, güncel yetki, depolama veya anahtar bağlamı kullanılamıyor. Mürettebat veya römorkör erişiminden yetki türetilmez.'}</p>}
    {status === 'keyLost' && <p role="alert">{en ? 'Original key unavailable. Retained data was not replaced or decrypted. Explicit loss/revoke and authorized cleanup are available.' : 'Özgün anahtar kullanılamıyor. Korunan veri değiştirilmedi veya çözülmedi. Açık kayıp/iptal ve yetkili temizleme kullanılabilir.'}</p>}
    {status === 'unknown' && <p role="alert">{en ? 'Submission outcome unknown. The exact signed envelope and UUID remain encrypted; retry that same queued command after reauthorization.' : 'Gönderim sonucu bilinmiyor. İmzalanan özgün zarf ve UUID şifreli korunur; yeniden yetkilendirme sonrası kuyruktaki aynı komutu tekrar deneyin.'}</p>}
    {memoryHeld && <p role="status">{en ? 'Unsaved classified edits are held only in this page and hidden until the original context is reauthorized. They have not been encrypted; closing this page can lose them.' : 'Kaydedilmemiş sınıflı düzenlemeler yalnız bu sayfada tutulur ve özgün bağlam yeniden yetkilendirilene kadar gizlenir. Henüz şifrelenmemiştir; sayfayı kapatmak kayba yol açabilir.'}</p>}
    {!online && <p role="status">{en ? 'Offline. An existing unexpired handle can capture; replay and renewal require reconnect. Restart remains locked.' : 'Çevrimdışı. Süresi dolmamış mevcut yetkiyle kayıt yapılabilir; gönderim ve yenileme için bağlantı gerekir. Yeniden başlatma kilitli kalır.'}</p>}
    <label>{en ? 'Retained daily context' : 'Korunan günlük bağlamı'}<select value={selected} disabled={locked || !!current || memoryHeld} onChange={event => setSelected(event.target.value)}><option value="">{en ? 'Create a new context' : 'Yeni bağlam oluştur'}</option>{selected && !contexts.some(c => c.id === selected) && <option value={selected}>{en ? 'Selected authorized context' : 'Seçili yetkili bağlam'}</option>}{contexts.map((c, i) => <option key={c.id} value={c.id}>{en ? 'Context' : 'Bağlam'} {i + 1} · {c.revokedAt ? (en ? 'Revoked' : 'İptal') : c.expiresAt}</option>)}</select></label>
    <EnterpriseActions align="start" aria-label={en ? 'Daily offline authorization actions' : 'Günlük çevrimdışı yetki işlemleri'}>
      <Button variant="outline" disabled={locked || !online} onClick={inventory}>{en ? 'Find retained contexts' : 'Korunan bağlamları bul'}</Button>
      <Button disabled={locked || !online} onClick={authorize}>{selected ? (en ? 'Reauthorize retained context' : 'Korunan bağlamı yeniden yetkilendir') : (en ? 'Authorize encrypted capture' : 'Şifreli kaydı yetkilendir')}</Button>
      {current && <Button variant="outline" disabled={busy} onClick={withdraw}>{en ? 'Lock retained data' : 'Korunan veriyi kilitle'}</Button>}
    </EnterpriseActions>
    {current && <>
      <p>{en ? 'Base revision' : 'Temel sürüm'}: {current.baseRevision}. {record && record.version !== current.baseVersion ? (en ? 'The visible record differs from this original base; replay will check conflicts.' : 'Görünen kayıt bu özgün temelden farklı; gönderimde çakışma kontrol edilir.') : ''}</p>
      <p>{en ? 'Unlisted ordinary editor fields are not saved to this offline context.' : 'Listelenmeyen normal düzenleyici alanları bu çevrimdışı bağlama kaydedilmez.'}</p>
      <div className="daily-offline-fields">{current.allowedFields.map(key => { const value = local[key] ?? { state: 'Missing' as const, value: null }, metadata = dailyFields(kind).find(f => f.key === key)
        return <label key={key}>{label(key)}<select aria-label={`${label(key)} — ${en ? 'Offline data state' : 'Çevrimdışı bilgi durumu'}`} value={value.state} disabled={locked} onChange={event => { setLocal(v => ({ ...v, [key]: { state: event.target.value as DailyValue['state'], value: event.target.value === 'Known' ? '' : null } })); setDirty(true) }}><option value="Missing">{en ? 'Missing' : 'Eksik'}</option><option value="Unknown">{en ? 'Unknown' : 'Bilinmiyor'}</option><option value="Known" disabled={metadata?.kind === 'reference'}>{en ? 'Known' : 'Biliniyor'}</option></select>{metadata?.kind === 'enum' ? <select aria-label={label(key)} value={value.value ?? ''} disabled={locked || value.state !== 'Known'} onChange={event => { setLocal(v => ({ ...v, [key]: { state: 'Known', value: event.target.value } })); setDirty(true) }}><option value="">{en ? 'Choose a value' : 'Değer seçin'}</option>{metadata.options?.map(option => <option key={option}>{option}</option>)}</select> : <textarea aria-label={label(key)} value={value.value ?? ''} disabled={locked || value.state !== 'Known' || metadata?.kind === 'reference'} maxLength={metadata?.kind === 'narrative' ? 4000 : 512} rows={metadata?.kind === 'narrative' ? 3 : 1} onChange={event => { setLocal(v => ({ ...v, [key]: { state: 'Known', value: event.target.value } })); setDirty(true) }} />}</label> })}</div>
      <EnterpriseActions align="start"><Button disabled={locked} onClick={capture}>{en ? 'Save classified fields to encrypted queue' : 'Sınıflı alanları şifreli kuyruğa kaydet'}</Button></EnterpriseActions>
      <p role="status">{en ? `Pending daily commands: ${queue.pending}` : `Bekleyen günlük komutları: ${queue.pending}`}</p>
      <ul className="daily-offline-queue">{captures.map((row, i) => <li key={row.commandId}><strong>{en ? 'Capture' : 'Kayıt'} {i + 1}</strong><span>{queue.resolved.has(row.commandId) ? (en ? 'Conflict resolved · original outcome retained' : 'Çakışma çözüldü · özgün sonuç korunuyor') : row.outcome?.result === 'Committed' ? (en ? 'Server committed' : 'Sunucuda kaydedildi') : row.outcome?.result === 'ServerRetained' ? (en ? 'Server retained · no report revision added' : 'Sunucu korundu · rapor sürümü eklenmedi') : row.outcome ? (en ? 'Human conflict resolution required' : 'İnsan tarafından çakışma çözümü gerekli') : (en ? 'Retained · server outcome unconfirmed' : 'Korunuyor · sunucu sonucu teyitsiz')}</span><EnterpriseActions align="start"><Button variant="outline" disabled={locked || !online} onClick={() => replay(row)}>{row.outcome ? (en ? 'Recover exact outcome' : 'Özgün sonucu getir') : (en ? 'Send exact queued command' : 'Özgün kuyruk komutunu gönder')}</Button>{row.outcome?.result === 'MaterialConflict' && !queue.resolved.has(row.commandId) && <Button variant="outline" disabled={locked || !online} onClick={() => void run(async (signal, n) => { const value = app.current; if (!value?.active()) return; const detail = await value.conflict(row.commandId, signal); if (fresh(n, signal)) setConflict(detail) })}>{en ? 'Review conflict' : 'Çakışmayı incele'}</Button>}</EnterpriseActions></li>)}</ul>
      {conflict && <section aria-label={en ? 'Daily material conflict' : 'Günlük maddi çakışma'}><h4>{en ? 'Before / captured / current server' : 'Önceki / yakalanan / güncel sunucu'}</h4><p>{en ? 'Choose explicitly. A retained resolution is a new command with a reason and parent lineage, never a silent overwrite.' : 'Açıkça seçim yapın. Korunan çözüm, gerekçe ve üst kayıt bağıyla yeni komuttur; sessiz üzerine yazma değildir.'}</p><table><thead><tr><th>{en ? 'Field' : 'Alan'}</th><th>{en ? 'Before' : 'Önceki'}</th><th>{en ? 'Captured' : 'Yakalanan'}</th><th>{en ? 'Server' : 'Sunucu'}</th></tr></thead><tbody>{conflict.fields.map(f => <tr key={f.key}><th>{label(f.key)}</th><td>{viewValue(f.base)}</td><td>{viewValue(f.captured)}</td><td>{viewValue(f.server)}</td></tr>)}</tbody></table>{!conflict.resolved && !conflict.choices.length && <p role="status">{en ? 'Current human resolution authority is unavailable. No resolution command is enabled.' : 'Güncel insan çözüm yetkisi kullanılamıyor. Çözüm komutu etkin değil.'}</p>}<EnterpriseActions align="start">{conflict.choices.map(choice => <Button key={choice} disabled={locked || !online} onClick={() => resolve(choice)}>{choice === 'keepServer' ? (en ? 'Retain server · record reason' : 'Sunucuyu koru · gerekçe kaydet') : (en ? 'Use captured · record correction reason' : 'Yakalananı kullan · düzeltme gerekçesi kaydet')}</Button>)}</EnterpriseActions></section>}
    </>}
    {selected && <details><summary>{en ? 'Revoke, key loss and retained-data cleanup' : 'İptal, anahtar kaybı ve korunan veri temizliği'}</summary><EnterpriseActions align="start">{(['revoke', 'lost'] as const).map(action => <Button key={action} variant="outline" disabled={locked || !online} onClick={() => void run(async (signal, n) => { await dailyOfflineLifecycle(kind, vessel, recordId, selected, action, signal); if (fresh(n, signal)) { setRetained(true); withdraw() } })}>{action === 'lost' ? (en ? 'Report original key lost' : 'Özgün anahtar kaybını bildir') : (en ? 'Revoke capture context' : 'Kayıt bağlamını iptal et')}</Button>)}</EnterpriseActions><label><input type="checkbox" checked={confirmPurge} disabled={locked} onChange={event => setConfirmPurge(event.target.checked)} />{en ? 'I explicitly confirm deletion of this context’s retained ciphertext and local key. Unsynchronized data may be lost.' : 'Bu bağlamın korunan şifreli verisi ve yerel anahtarının silinmesini açıkça onaylıyorum. Eşitlenmemiş veri kaybolabilir.'}</label><Button variant="outline" disabled={locked || !online || !confirmPurge} onClick={() => void run(async (signal, n) => { await discardDailyLostCaptures(kind, vessel, recordId, selected, true, signal); if (fresh(n, signal)) { retainedDraft.current = null; withdraw(); setMemoryHeld(false); setRetained(false); setConfirmPurge(false) } })}>{en ? 'Delete confirmed local context' : 'Onaylanan yerel bağlamı sil'}</Button></details>}
  </section>
}
