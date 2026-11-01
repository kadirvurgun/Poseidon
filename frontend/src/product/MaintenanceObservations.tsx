import { useEffect, useRef, useState } from 'react'
import { ApiError, request } from './api'
type Source = { source: string; version: string; kind: string; unit: string; freshnessSeconds: number; maxScale: number }
type Observation = { id: string; source: string; policyVersion: string; sourceRevision: number; kind: string; unit: string; value: string | null; observedAt: string | null; provenance: string; revoked: boolean }
type Snapshot = { sources: Source[]; items: Observation[]; canWrite: boolean; state: string }
export function MaintenanceObservations({ vessel, asset, csrf, en, onDirty, onUncertain }: { vessel: string; asset: string; csrf: string; en: boolean; onDirty: (value: boolean) => void; onUncertain: (value: boolean) => void }) {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null), [error, setError] = useState(false), [refresh, setRefresh] = useState(0)
  const [source, setSource] = useState(''), [value, setValue] = useState(''), [time, setTime] = useState(''), [revision, setRevision] = useState('')
  const [state, setState] = useState<'idle' | 'saving' | 'uncertain' | 'denied'>('idle')
  const attempt = useRef<{ id: string; body: string; path: string } | null>(null), busy = useRef(false)
  const active = useRef<AbortController | null>(null)
  const path = `equipment-hierarchy/${encodeURIComponent(vessel)}/${encodeURIComponent(asset)}/observations`
  useEffect(() => {
    const controller = new AbortController(); active.current = controller; setSnapshot(null); setError(false)
    setSource(''); setValue(''); setTime(''); setRevision(''); attempt.current = null; onDirty(false); onUncertain(false); setState('idle')
    request<Snapshot>(path, controller.signal).then(result => { if (!Array.isArray(result?.sources) || !Array.isArray(result.items) || typeof result.canWrite !== 'boolean') throw new ApiError(502); if (!controller.signal.aborted) setSnapshot(result) })
      .catch(() => { if (!controller.signal.aborted) setError(true) })
    return () => { controller.abort(); active.current?.abort() }
  }, [path, csrf, refresh, onDirty, onUncertain])
  const run = async (revokeId?: string) => {
    if (!snapshot || busy.current) return
    if (!attempt.current) {
      const selected = snapshot.sources.find(s => s.source === source)
      if (!revokeId && (!selected || !/^[1-9][0-9]*$/.test(revision) || !Number.isSafeInteger(Number(revision)))) return
      const id = crypto.randomUUID()
      attempt.current = { id, path: revokeId ? `${path}/${encodeURIComponent(revokeId)}/revoke` : path,
        body: JSON.stringify(revokeId ? {} : { id, source, policyVersion: selected!.version, sourceRevision: Number(revision), kind: selected!.kind, unit: selected!.unit, value: value || null, observedAt: time || null }) }
    }
    const frozen = attempt.current, controller = new AbortController(); active.current = controller; busy.current = true; setState('saving'); onDirty(true)
    const timeout = setTimeout(() => controller.abort(), 15000)
    try {
      await request(frozen.path, controller.signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': frozen.id }, body: frozen.body })
      attempt.current = null; onDirty(false); onUncertain(false); setRefresh(r => r + 1)
    } catch (e) {
      if (e instanceof ApiError && [400, 401, 403, 404, 409].includes(e.status)) { attempt.current = null; onUncertain(false); setState('denied'); if ([401,403,404].includes(e.status)) { setSnapshot(null); onDirty(false) } }
      else { setState('uncertain'); onUncertain(true) }
    } finally { clearTimeout(timeout); busy.current = false }
  }
  if (error) return <p role="alert">{en ? 'Observation service unavailable.' : 'Gözlem servisi kullanılamıyor.'}</p>
  if (!snapshot) return <p role="status">{en ? 'Observation access unavailable.' : 'Gözlem erişimi kullanılamıyor.'}</p>
  const selected = snapshot.sources.find(s => s.source === source), locked = state === 'saving' || state === 'uncertain'
  return <section aria-label={en ? 'Meter and condition observations' : 'Sayaç ve koşul gözlemleri'}><h3>{en ? 'Meter and condition observations' : 'Sayaç ve koşul gözlemleri'}</h3>
    <p>{en ? 'Unverified observations support configured maintenance planning. They do not verify work or operational fitness.' : 'Doğrulanmamış gözlemler yapılandırılmış bakım planlamasını destekler. Tamamlanan işi veya operasyonel uygunluğu doğrulamaz.'}</p>
    {!snapshot.sources.length && <p>{en ? 'Source, unit and freshness policy unavailable.' : 'Kaynak, birim ve güncellik politikası yapılandırılmamış.'}</p>}
    <ul>{snapshot.items.map(item => <li key={item.id}>{item.source} · {item.kind === 'Meter' ? (en ? 'Meter' : 'Sayaç') : (en ? 'Condition' : 'Koşul')} · {item.value ?? (en ? 'Missing' : 'Eksik')} {item.unit} · {item.observedAt ?? (en ? 'Missing observation time' : 'Gözlem zamanı eksik')} · {item.revoked ? (en ? 'Revoked' : 'İptal edilmiş') : (en ? 'Unverified' : 'Doğrulanmamış')}
      <code>{item.id}</code>{snapshot.canWrite && !item.revoked && <button type="button" disabled={locked} onClick={() => void run(item.id)}>{en ? 'Revoke observation' : 'Gözlemi iptal et'}</button>}</li>)}</ul>
    {snapshot.canWrite && snapshot.sources.length > 0 && <form onSubmit={e => { e.preventDefault(); void run() }}><fieldset disabled={locked}>
      <label>{en ? 'Configured source' : 'Yapılandırılmış kaynak'}<select required value={source} onChange={e => { setSource(e.target.value); setValue(''); onDirty(true) }}><option value="">{en ? 'Select' : 'Seçiniz'}</option>{snapshot.sources.map(s => <option key={s.source} value={s.source}>{s.source} · {s.unit} · {s.version}</option>)}</select></label>
      <label>{en ? 'Source revision' : 'Kaynak revizyonu'}<input required inputMode="numeric" value={revision} onChange={e => { setRevision(e.target.value); onDirty(true) }} /></label>
      <label>{en ? 'Observed value (exact declaration)' : 'Gözlenen değer (kesin beyan)'}{selected?.kind === 'Condition' ? <select value={value} onChange={e => { setValue(e.target.value); onDirty(true) }}><option value="">{en ? 'Unknown' : 'Bilinmiyor'}</option><option value="True">{en ? 'True' : 'Doğru'}</option><option value="False">{en ? 'False' : 'Yanlış'}</option></select> : <input maxLength={1024} value={value} onChange={e => { setValue(e.target.value); onDirty(true) }} />}</label>
      <label>{en ? 'Observation instant (ISO with offset)' : 'Gözlem zamanı (ISO, saat farkıyla)'}<input value={time} onChange={e => { setTime(e.target.value); onDirty(true) }} /></label>
    </fieldset><button disabled={locked}>{en ? 'Record observation' : 'Gözlemi kaydet'}</button></form>}
    {state === 'uncertain' && <button type="button" onClick={() => void run()}>{en ? 'Retry same attempt' : 'Aynı denemeyi tekrarla'}</button>}
    {state === 'uncertain' && <p role="alert">{en ? 'Outcome uncertain. The exact original attempt is locked until retry.' : 'Sonuç belirsiz. Aynı özgün deneme tekrar edilene kadar kilitlidir.'}</p>}
    {state === 'denied' && <p role="alert">{en ? 'The observation was rejected. Check current access, source policy and values.' : 'Gözlem reddedildi. Güncel erişim, kaynak politikası ve değerleri kontrol edin.'}</p>}
    <button disabled={locked} type="button" onClick={() => { onDirty(false); setRefresh(r => r + 1) }}>{en ? 'Discard and reload' : 'Vazgeç ve yeniden yükle'}</button>
  </section>
}
