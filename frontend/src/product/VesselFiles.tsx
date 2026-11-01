import { useEffect, useRef, useState } from 'react'
import { downloadFile, fileDetail, filePage, stageFile, uploadFile, type FileAttempt, type FilePage, type FileRecord } from './file-api'

export function VesselFiles({ vessel, language, csrf, onDirty, onSaving }: { vessel: string; language: 'tr' | 'en'; csrf: string; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void }) {
  const en = language === 'en'
  const stateLabel = (state: FileRecord['state']) => ({ Pending: en ? 'Pending upload' : 'Yükleme bekliyor', Receiving: en ? 'Quarantined / interrupted' : 'Karantina / kesilmiş', CleanTransport: en ? 'Scanned transport, unverified evidence' : 'Taranmış aktarım, doğrulanmamış kanıt', Abandoned: en ? 'Abandoned / reconciliation' : 'Bırakıldı / uzlaştırma', SyntheticUnverified: en ? 'Synthetic unverified metadata' : 'Sentetik doğrulanmamış meta veri' })[state]
  const [loaded, setLoaded] = useState<{ key: string; page: FilePage } | null>(null); const [offset, setOffset] = useState(0)
  const [reload, setReload] = useState(0); const [error, setError] = useState(false)
  const [file, setFile] = useState<File | null>(null); const attempt = useRef<FileAttempt | null>(null)
  const [frozen, setFrozen] = useState(false); const [busy, setBusy] = useState(false); const [progress, setProgress] = useState(0)
  const [selection, setSelection] = useState<{ key: string; row: FileRecord } | null>(null); const [notice, setNotice] = useState('')
  const controller = useRef<AbortController | null>(null)
  const pageKey = `${vessel}:${offset}:${reload}`
  const selected = selection?.key === pageKey ? selection.row : null
  const setSelected = (row: FileRecord | null) => setSelection(row ? { key: pageKey, row } : null)
  const page = loaded?.key === pageKey ? loaded.page : null
  useEffect(() => { const c = new AbortController(); let active = true; filePage(vessel, offset, c.signal).then(value => { if (active) { setLoaded({ key: pageKey, page: value }); setSelection(null); setError(false) } }).catch(() => { if (active && !c.signal.aborted) setError(true) }); return () => { active = false; c.abort() } }, [vessel, offset, reload, pageKey])
  useEffect(() => () => controller.current?.abort(), [])
  const start = async () => {
    if (!file || busy || !page?.available || !page.canWrite) return
    setBusy(true); onSaving(true); setError(false); setProgress(0)
    const c = new AbortController(); controller.current = c
    try {
      if (!attempt.current) {
        if (file.size === 0 || file.size > page.maxBytes || file.type !== 'image/png' || !/^[A-Za-z0-9_.-]+\.png$(?![\s\S])/i.test(file.name) || file.name.includes('..')) throw Error()
        const checksum = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', await file.arrayBuffer())), x => x.toString(16).padStart(2, '0')).join('').toUpperCase()
        attempt.current = { key: crypto.randomUUID(), file, checksum }; setFrozen(true)
      }
      const staged = await stageFile(vessel, attempt.current, csrf, c.signal)
      const row = staged.state === 'Pending' ? await uploadFile(vessel, staged.id, attempt.current.file, csrf, c.signal, setProgress) : await fileDetail(vessel, staged.id, c.signal)
      setSelected(row)
      if (row.state !== 'CleanTransport') { setNotice(en ? 'Quarantined or interrupted. This attempt cannot upload again. Reconcile its state before starting another.' : 'Karantinada veya kesildi. Bu deneme yeniden yüklenemez. Yeni denemeden önce durumunu uzlaştırın.'); return }
      attempt.current = null; setFile(null); setFrozen(false); onDirty(false)
      setNotice(en ? 'Transport scanned. Evidence remains unverified.' : 'Aktarım tarandı. Kanıt doğrulanmış değildir.'); setReload(x => x + 1)
    } catch { setError(true); setNotice(en ? 'Outcome unknown or rejected. Retry checks the same frozen attempt; cancellation does not undo a server commit.' : 'Sonuç belirsiz veya reddedildi. Tekrar aynı sabit denemeyi kontrol eder; iptal sunucu kaydını geri almaz.') }
    finally { setBusy(false); onSaving(false) }
  }
  const download = async () => {
    if (!selected) return; setBusy(true); onSaving(true); const c = new AbortController(); controller.current = c
    try { const blob = await downloadFile(vessel, selected, csrf, c.signal); const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = selected.name; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000) }
    catch { setError(true) } finally { setBusy(false); onSaving(false) }
  }
  return <section className="vessel-files" aria-label={en ? 'Vessel files' : 'Araç dosyaları'}>
    <h2>{en ? 'Vessel files' : 'Araç dosyaları'}</h2>
    <p>{en ? 'File presence and a malware scan do not verify evidence or activate readiness.' : 'Dosyanın bulunması ve zararlı yazılım taraması kanıtı doğrulamaz, hazırlığı etkinleştirmez.'}</p>
    {error && <p role="alert">{en ? 'File service unavailable or request rejected.' : 'Dosya servisi kullanılamıyor veya istek reddedildi.'}</p>}
    {notice && <p role="status">{notice}</p>}
    {!page && !error && <p role="status">{en ? 'Loading files…' : 'Dosyalar yükleniyor…'}</p>}
    {!busy && <button disabled={frozen} onClick={() => setReload(x => x + 1)}>{en ? 'Refresh files' : 'Dosyaları yenile'}</button>}
    {page && <>
      {!page.available && <p role="status">{en ? 'Storage, scanner and operator settings are required before upload/download.' : 'Yükleme/indirme için depolama, tarayıcı ve operatör ayarları gereklidir.'}</p>}
      {page.canWrite && <form onSubmit={e => { e.preventDefault(); void start() }}>
        <label>{en ? 'PNG file' : 'PNG dosyası'} <input type="file" accept="image/png,.png" disabled={frozen || busy || !page.available} onChange={e => { setFile(e.target.files?.[0] ?? null); onDirty(!!e.target.files?.length) }} /></label>
        <p>{en ? 'Allowed: passive PNG subset. Operator byte limit:' : 'İzin verilen: pasif PNG alt kümesi. Operatör bayt sınırı:'} {page.maxBytes}</p>
        <button disabled={!file || busy || !page.available}>{frozen ? (en ? 'Retry same attempt' : 'Aynı denemeyi kontrol et') : (en ? 'Upload file' : 'Dosyayı yükle')}</button>
        {busy && <><progress aria-label={en ? 'Upload progress' : 'Yükleme ilerlemesi'} max={100} value={progress} /><button type="button" onClick={() => controller.current?.abort()}>{en ? 'Cancel request' : 'İsteği iptal et'}</button></>}
        {!busy && (file || frozen) && <button type="button" onClick={() => { attempt.current = null; setFrozen(false); setFile(null); onDirty(false); setNotice(en ? 'Local attempt discarded; quarantined server bytes may remain for reconciliation.' : 'Yerel deneme bırakıldı; karantina baytları uzlaştırma için sunucuda kalabilir.') }}>{en ? 'Discard local attempt' : 'Yerel denemeyi bırak'}</button>}
      </form>}
      {page.items.length === 0 ? <p>{en ? 'No files in this vessel scope.' : 'Bu araç kapsamında dosya yok.'}</p> : <ul>{page.items.map(row => <li key={row.id}><button disabled={busy || frozen} onClick={async () => { controller.current?.abort(); const c = new AbortController(); controller.current = c; try { const detail = await fileDetail(vessel, row.id, c.signal); if (!c.signal.aborted) setSelected(detail) } catch { if (!c.signal.aborted) { setSelected(null); setError(true) } } }}>{row.name}</button> · {stateLabel(row.state)}</li>)}</ul>}
      <button disabled={offset === 0 || busy || frozen} onClick={() => setOffset(x => Math.max(0, x - 25))}>{en ? 'Previous files' : 'Önceki dosyalar'}</button><button disabled={!page.hasMore || busy || frozen} onClick={() => setOffset(x => x + 25)}>{en ? 'Next files' : 'Sonraki dosyalar'}</button>
    </>}
    {selected && <article aria-label={en ? 'File details' : 'Dosya ayrıntıları'}><h3>{selected.name}</h3><dl><dt>{en ? 'Transport state' : 'Aktarım durumu'}</dt><dd>{stateLabel(selected.state)}</dd><dt>SHA-256</dt><dd>{selected.checksum ?? '—'}</dd><dt>{en ? 'Revision' : 'Revizyon'}</dt><dd>{selected.revision ?? '—'}</dd></dl>{selected.state === 'CleanTransport' && <button disabled={busy || !page?.available} onClick={() => void download()}>{en ? 'Download file' : 'Dosyayı indir'}</button>}</article>}
  </section>
}
