import { useEffect, useMemo, useRef, useState } from 'react'
import { documentRevision, documents, saveDocument, type DocumentDraft, type DocumentPage } from './document-api'
import { VesselFiles } from './VesselFiles'

export function VesselDocuments({ vessel, language, csrf, onDirty, onSaving }: { vessel: string; language: 'tr' | 'en'; csrf: string; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void }) {
  const en = language === 'en'
  const [offset, setOffset] = useState(0); const [reload, setReload] = useState(0)
  const [loaded, setLoaded] = useState<{ key: string; page: DocumentPage } | null>(null)
  const [selection, setSelection] = useState<{ key: string; row: DocumentDraft } | null>(null)
  const [title, setTitle] = useState(''); const [error, setError] = useState(false); const [busy, setBusy] = useState(false)
  const [editing, setEditing] = useState(false); const [attachmentDirty, setAttachmentDirty] = useState(false); const [attachmentBusy, setAttachmentBusy] = useState(false)
  const [attempt, setAttempt] = useState<{ key: string; title: string; current?: DocumentDraft } | null>(null)
  const c = useRef<AbortController | null>(null)
  const key = `${vessel}:${offset}:${reload}`; const page = loaded?.key === key ? loaded.page : null; const selected = selection?.key === key ? selection.row : null
  const owner = useMemo(() => selected ? { id: selected.id, revision: selected.revision } : undefined, [selected])
  const locked = busy || attachmentBusy || attachmentDirty || editing || !!attempt
  useEffect(() => {
    const request = new AbortController(); let active = true
    documents(vessel, offset, request.signal).then(value => { if (active) { setLoaded({ key, page: value }); setSelection(null); setError(false) } }).catch(() => { if (active && !request.signal.aborted) setError(true) })
    return () => { active = false; request.abort() }
  }, [vessel, offset, reload, key])
  useEffect(() => () => c.current?.abort(), [])
  const save = async () => {
    if (busy || !page?.canWrite) return
    const frozen = attempt ?? { key: crypto.randomUUID(), title, current: selected ?? undefined }
    setAttempt(frozen); setBusy(true); onSaving(true); setError(false)
    const request = new AbortController(); c.current = request
    try {
      const saved = await saveDocument(vessel, frozen.title, frozen.key, csrf, request.signal, frozen.current)
      setSelection({ key, row: saved }); setAttempt(null); setEditing(false); onDirty(false)
      setLoaded(previous => previous?.key === key ? { key, page: { ...previous.page, items: [saved, ...previous.page.items.filter(x => x.id !== saved.id)].slice(0, 25) } } : previous)
    } catch { setError(true) } finally { setBusy(false); onSaving(false) }
  }
  return <section aria-label={en ? 'Document drafts' : 'Belge taslakları'}>
    <h2>{en ? 'Document drafts' : 'Belge taslakları'}</h2>
    <p>{en ? 'Drafts and attachments remain unverified. Controlled publication and effective status require a separate authorized process.' : 'Taslaklar ve ekler doğrulanmamıştır. Kontrollü yayın ve yürürlük durumu ayrı yetkili süreç gerektirir.'}</p>
    {error && <p role="alert">{en ? 'Document request failed or outcome is unknown. A retry preserves the same command; refresh before resolving a conflict.' : 'Belge isteği başarısız veya sonucu belirsiz. Tekrar aynı komutu korur; çatışmayı çözmeden önce yenileyin.'}</p>}
    {!page && !error && <p role="status">{en ? 'Loading documents…' : 'Belgeler yükleniyor…'}</p>}
    <button disabled={locked} onClick={() => setReload(x => x + 1)}>{en ? 'Refresh documents' : 'Belgeleri yenile'}</button>
    {page && <>
      {page.items.length === 0 && <p>{en ? 'No document drafts in this vessel scope.' : 'Bu araç kapsamında belge taslağı yok.'}</p>}
      <ul>{page.items.map(row => <li key={row.id}><button disabled={locked} onClick={() => { setSelection({ key, row }); setTitle(row.title ?? '') }}>{row.title || (en ? 'Untitled draft' : 'Başlıksız taslak')}</button> · {en ? 'Unverified draft' : 'Doğrulanmamış taslak'} · {row.revision}</li>)}</ul>
      <button disabled={locked || offset === 0} onClick={() => setOffset(x => Math.max(0, x - 25))}>{en ? 'Previous documents' : 'Önceki belgeler'}</button>
      <button disabled={locked || !page.hasMore} onClick={() => setOffset(x => x + 25)}>{en ? 'Next documents' : 'Sonraki belgeler'}</button>
      {page.canWrite && <>
        <button disabled={locked} onClick={() => { setSelection(null); setTitle(''); setEditing(true); onDirty(true) }}>{en ? 'New document draft' : 'Yeni belge taslağı'}</button>
        {selected && <button disabled={locked} onClick={() => { setTitle(selected.title ?? ''); setEditing(true); onDirty(true) }}>{en ? 'Append draft revision' : 'Taslak revizyonu ekle'}</button>}
      </>}
      {(editing || attempt) && <form onSubmit={e => { e.preventDefault(); void save() }}>
        <label>{en ? 'Draft title (optional)' : 'Taslak başlığı (isteğe bağlı)'}<input maxLength={200} value={title} disabled={!!attempt || busy} onChange={e => setTitle(e.target.value)} /></label>
        <button disabled={busy}>{attempt ? (en ? 'Retry same draft' : 'Aynı taslağı tekrar dene') : (en ? 'Save unverified draft' : 'Doğrulanmamış taslağı kaydet')}</button>
        <button type="button" disabled={busy} onClick={() => { setAttempt(null); setEditing(false); onDirty(false) }}>{en ? 'Discard local draft' : 'Yerel taslağı bırak'}</button>
      </form>}
      {selected && !editing && !attempt && <article aria-label={en ? 'Document revision' : 'Belge revizyonu'}>
        <h3>{selected.title || (en ? 'Untitled draft' : 'Başlıksız taslak')}</h3><p>{en ? 'Revision' : 'Revizyon'} {selected.revision} · {en ? 'Unverified' : 'Doğrulanmamış'}</p>
        {selected.revision > 1 && <button disabled={locked} onClick={async () => { const request = new AbortController(); c.current = request; setBusy(true); try { const row = await documentRevision(vessel, selected.id, selected.revision - 1, request.signal); setSelection({ key, row }) } catch { setError(true) } finally { setBusy(false) } }}>{en ? 'Previous revision' : 'Önceki revizyon'}</button>}
        {owner && <VesselFiles key={`${owner.id}:${owner.revision}`} vessel={vessel} owner={owner} language={language} csrf={csrf} onDirty={v => { setAttachmentDirty(v); onDirty(v) }} onSaving={v => { setAttachmentBusy(v); onSaving(v) }} />}
      </article>}
    </>}
  </section>
}
