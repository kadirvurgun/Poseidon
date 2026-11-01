import { useEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import { loginHref } from './workspace-location'
import * as service from './register-api'
import type { Draft, FieldKey, RegisterKind, RegisterPage, Revision } from './register-api'

const labels: Record<FieldKey, [string, string]> = { title: ['Title', 'Başlık'], authority: ['Declared authority', 'Beyan edilen otorite'], locator: ['Source reference (text only)', 'Kaynak referansı (yalnız metin)'], instrument: ['Instrument', 'Düzenleme'], amendment: ['Version / amendment declaration', 'Sürüm / değişiklik beyanı'], effectiveDate: ['Declared effective date', 'Beyan edilen yürürlük tarihi'], declaration: ['Declaration / provenance', 'Beyan / köken'], observedAt: ['Source observed at (with UTC offset)', 'Kaynak gözlem zamanı (UTC farkı ile)'] }
type Props = { kind: RegisterKind; id: string | null; language: 'tr' | 'en'; csrf: string; canWrite: boolean; offline: boolean; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void; onNavigate: (href: string) => void }
export function RegulatoryRegister(props: Props) {
  return <RegisterContext key={`${props.kind}/${props.id ?? 'list'}`} {...props} />
}
function RegisterContext({ kind, id, language, csrf, canWrite, offline, onDirty, onSaving, onNavigate }: Props) {
  const en = language === 'en'; const text = (a: string, b: string) => en ? a : b
  const [rows, setRows] = useState<RegisterPage<Draft> | null>(null)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [content, setContent] = useState(service.blankContent)
  const [sourceId, setSourceId] = useState<string | null>(null)
  const [sourceRevision, setSourceRevision] = useState<number | null>(null)
  const [sources, setSources] = useState<RegisterPage<Draft> | null>(null)
  const [sourceVersions, setSourceVersions] = useState<RegisterPage<Revision> | null>(null)
  const [sourceOffset, setSourceOffset] = useState(0)
  const [sourceVersionOffset, setSourceVersionOffset] = useState(0)
  const [sourceQuery, setSourceQuery] = useState('')
  const [sourceSearch, setSourceSearch] = useState('')
  const [history, setHistory] = useState<RegisterPage<Revision> | null>(null)
  const [historyOffset, setHistoryOffset] = useState(0)
  const [frozen, setFrozen] = useState<{ key: string; body: string } | null>(null)
  const [saving, setSaving] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [error, setError] = useState<number | null>(null)
  const [historyError, setHistoryError] = useState(false)
  const [sourceError, setSourceError] = useState(false)
  const [sourceAttempt, setSourceAttempt] = useState(0)
  const [historyAttempt, setHistoryAttempt] = useState(0)
  const [notice, setNotice] = useState('')
  const [offset, setOffset] = useState(0)
  const [query, setQuery] = useState('')
  const [search, setSearch] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [creating, setCreating] = useState(false)
  const [conflict, setConflict] = useState(false)
  const editor = !!id || creating
  const retained = useRef(false)
  useEffect(() => { retained.current = dirty || !!frozen }, [dirty, frozen])
  const editable = canWrite && (!draft || draft.canEdit)
  useEffect(() => { onDirty(dirty || !!frozen) }, [dirty, frozen, onDirty])
  useEffect(() => { onSaving(saving) }, [saving, onSaving])
  useEffect(() => {
    if (offline || id && retained.current) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    const work = id ? service.readDraft(kind, id, c.signal).then(d => {
      if (!active) return
      setDraft(d); setContent(d.current.content); setSourceId(d.current.sourceId); setSourceRevision(d.current.sourceRevision)
    }) : service.listDrafts(kind, search, offset, c.signal).then(d => { if (active) setRows(d) })
    work.catch(e => { if (active) setError(e instanceof ApiError ? e.status : 0) })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [id, kind, search, offset, attempt, offline])
  useEffect(() => {
    if (!id || offline) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    service.historyDraft(kind, id, historyOffset, c.signal).then(d => { if (active) setHistory(d) }).catch(() => { if (active) setHistoryError(true) })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [id, kind, historyOffset, historyAttempt, offline])
  useEffect(() => {
    if (!editor || kind !== 'requirements' || offline) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    service.listDrafts('sources', sourceSearch, sourceOffset, c.signal).then(d => { if (active) setSources(d) }).catch(() => { if (active) setSourceError(true) })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [editor, kind, sourceSearch, sourceOffset, sourceAttempt, offline])
  useEffect(() => {
    if (!sourceId || offline) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    service.historyDraft('sources', sourceId, sourceVersionOffset, c.signal).then(d => { if (active) setSourceVersions(d) }).catch(() => { if (active) setSourceError(true) })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [sourceId, sourceVersionOffset, sourceAttempt, offline])
  // Reconnection refreshes list/history only; dirty editor values and frozen commands stay in memory.
  const denied = error === 401 || error === 403 || error === 404
  const locked = saving || !!frozen || conflict || denied || !editable
  const save = async () => {
    if (!editable || saving || offline || conflict || denied) return
    if (!service.validContent(content) || (sourceId !== null && sourceRevision === null)) { setError(400); return }
    const payload: service.Command = { expectedVersion: draft?.version ?? 0, content, sourceId, sourceRevision }
    const command = frozen ?? { key: crypto.randomUUID(), body: JSON.stringify(payload) }
    setFrozen(command); setSaving(true); setError(null)
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    try {
      const d = await service.saveDraft(kind, id ?? draft?.current.id ?? null, command.body, csrf, command.key, c.signal)
      setDraft(d); setContent(d.current.content); setDirty(false); setFrozen(null); onDirty(false); setNotice(text('Draft saved. Unverified.', 'Taslak kaydedildi. Doğrulanmadı.'))
      setHistory(null); setHistoryAttempt(v => v + 1)
    } catch (e) {
      const status = e instanceof ApiError ? e.status : 0; setError(status)
      if ([400, 401, 403, 404, 409].includes(status)) setFrozen(null)
      if (status === 409) setConflict(true)
    } finally { clearTimeout(timer); setSaving(false) }
  }
  const title = (d: Draft) => d.current.content[kind === 'sources' ? 'title' : 'instrument'].value ?? d.current.id
  const pageButtons = (page: { hasMore: boolean } | null, value: number, next: (n: number) => void) => <div className="form-actions"><button type="button" disabled={!value || saving} onClick={() => next(Math.max(0, value - 25))}>{text('Previous', 'Önceki')}</button><button type="button" disabled={!page?.hasMore || saving} onClick={() => next(value + 25)}>{text('Next', 'Sonraki')}</button></div>
  return <section aria-label={text('Regulatory draft register', 'Mevzuat taslak kaydı')}>
    <p>{text('Tenant-local DRAFT · Declared and unverified. Dates and hashes confer no legal authority. Source references are stored as text.', 'Kuruluşa özel TASLAK · Beyan edilmiş ve doğrulanmamış. Tarihler ve özet değerleri hukuki yetki vermez. Kaynak referansları metin olarak saklanır.')}</p>
    {error !== null && <div role="alert"><p>{error === 401 ? text('Sign in to continue; retain your draft.', 'Taslağınızı koruyarak oturum açın.') : error === 403 ? text('Permission denied.', 'Yetkiniz yok.') : error === 409 ? text('Conflict. Your edits are retained. Reload only after discarding them.', 'Çatışma. Düzenlemeleriniz korundu. Yalnız vazgeçtikten sonra yeniden yükleyin.') : error === 400 ? text('Check field states, bounds, dates and source revision.', 'Alan durumlarını, sınırları, tarihleri ve kaynak sürümünü kontrol edin.') : text('Service unavailable. Your draft is retained.', 'Hizmete ulaşılamıyor. Taslağınız korundu.')}</p>{error === 401 && <a href={loginHref()}>{text('Sign in', 'Oturum aç')}</a>}{!editor && <button onClick={() => { setError(null); setAttempt(v => v + 1) }}>{text('Retry', 'Tekrar dene')}</button>}</div>}
    {notice && <p role="status">{notice}</p>}
    {error !== null && id && !draft && <button onClick={() => { setError(null); setAttempt(v => v + 1) }}>{text('Retry draft', 'Taslağı tekrar dene')}</button>}
    {draft && !id && <button onClick={() => onNavigate(`/app/${kind}/${draft.current.id}`)}>{text('Open saved draft and history', 'Kayıtlı taslağı ve geçmişi aç')}</button>}
    {conflict && id && <button onClick={() => onNavigate(`/app/${kind}/${id}`)}>{text('Reload saved draft', 'Kayıtlı taslağı yeniden yükle')}</button>}
    {!editor ? <>
      {canWrite && <button onClick={() => { setCreating(true); setDirty(false); setError(null) }}>{text('New draft', 'Yeni taslak')}</button>}
      <form onSubmit={e => { e.preventDefault(); setRows(null); setError(null); setSearch(query.trim()); setOffset(0); setAttempt(v => v + 1) }}><label>{text('Search drafts', 'Taslaklarda ara')}<input type="search" maxLength={100} value={query} onChange={e => setQuery(e.target.value)} /></label><button>{text('Search', 'Ara')}</button></form>
      {!rows && error === null && <p role="status">{text('Loading…', 'Yükleniyor…')}</p>}
      {rows && <><ul>{rows.items.map(d => <li key={d.current.id}><a href={`/app/${kind}/${d.current.id}`} onClick={e => { e.preventDefault(); onNavigate(`/app/${kind}/${d.current.id}`) }}>{title(d)} · {text('Revision', 'Sürüm')} {d.current.revision} · DRAFT</a></li>)}</ul>{!rows.items.length && <p>{text('No drafts found.', 'Taslak bulunamadı.')}</p>}{pageButtons(rows, offset, n => { setRows(null); setOffset(n) })}</>}
    </> : <>
      {id && !draft ? error === null && <p role="status">{text('Loading…', 'Yükleniyor…')}</p> : <form onSubmit={e => { e.preventDefault(); void save() }}>
        {draft && <p>{draft.current.id} · DRAFT · {text('Revision', 'Sürüm')} {draft.current.revision} · {text('Saved at', 'Kayıt zamanı')}: {draft.current.savedAt}</p>}
        <fieldset disabled={locked}><legend>{text('Draft fields', 'Taslak alanları')}</legend>
          {(Object.keys(service.fieldBounds) as FieldKey[]).filter(key => kind === 'sources' ? key !== 'instrument' : !['title', 'authority', 'locator'].includes(key)).map(key => <div className="profile-field" key={key}>
            <label htmlFor={`reg-${key}-state`}>{labels[key][en ? 0 : 1]} · {text('State', 'Durum')}</label><select id={`reg-${key}-state`} value={content[key].state} onChange={e => { const state = e.target.value as 'Missing' | 'Unknown' | 'Known'; setContent(v => ({ ...v, [key]: { state, value: state === 'Known' ? '' : null } })); setDirty(true) }}>
              <option value="Missing">{text('Missing', 'Eksik')}</option><option value="Unknown">{text('Unknown', 'Bilinmiyor')}</option><option value="Known">{text('Known (declared)', 'Bilinen (beyan)')}</option>
            </select>{content[key].state === 'Known' && <><label htmlFor={`reg-${key}`}>{labels[key][en ? 0 : 1]}</label><input id={`reg-${key}`} type={key === 'effectiveDate' ? 'date' : 'text'} maxLength={service.fieldBounds[key]} value={content[key].value ?? ''} onChange={e => { setContent(v => ({ ...v, [key]: { state: 'Known', value: e.target.value } })); setDirty(true) }} /></>}
          </div>)}
          {kind === 'requirements' && <section aria-label={text('Immutable source citation', 'Değişmez kaynak atfı')}>
            <p>{text('Selected source revision', 'Seçili kaynak sürümü')}: {sourceId ?? text('Missing', 'Eksik')} / {sourceRevision ?? text('Missing', 'Eksik')}</p>
            <label>{text('Find a source', 'Kaynak bul')}<input maxLength={100} value={sourceQuery} onChange={e => setSourceQuery(e.target.value)} /></label><button type="button" onClick={() => { setSources(null); setSourceSearch(sourceQuery.trim()); setSourceOffset(0); setSourceAttempt(v => v + 1) }}>{text('Search sources', 'Kaynaklarda ara')}</button>
            <label htmlFor="reg-source">{text('Source draft', 'Kaynak taslağı')}</label><select id="reg-source" value={sourceId ?? ''} onChange={e => { setSourceId(e.target.value || null); setSourceRevision(null); setSourceVersions(null); setSourceVersionOffset(0); setDirty(true) }}><option value="">{text('Missing', 'Eksik')}</option>{sourceId && !sources?.items.some(d => d.current.id === sourceId) && <option value={sourceId}>{sourceId}</option>}{sources?.items.map(d => <option key={d.current.id} value={d.current.id}>{d.current.content.title.value ?? d.current.id}</option>)}</select>
            {pageButtons(sources, sourceOffset, n => { setSources(null); setSourceOffset(n) })}
            {sourceId && <><label htmlFor="reg-source-revision">{text('Exact revision', 'Kesin sürüm')}</label><select id="reg-source-revision" value={sourceRevision ?? ''} onChange={e => { setSourceRevision(e.target.value ? Number(e.target.value) : null); setDirty(true) }}><option value="">{text('Missing', 'Eksik')}</option>{sourceRevision && !sourceVersions?.items.some(r => r.revision === sourceRevision) && <option value={sourceRevision}>{sourceRevision}</option>}{sourceVersions?.items.map(r => <option key={r.revision} value={r.revision}>{r.revision} · {r.content.amendment.value ?? 'DRAFT'} · {r.hash.slice(0, 12)}</option>)}</select>{pageButtons(sourceVersions, sourceVersionOffset, n => { setSourceVersions(null); setSourceVersionOffset(n) })}</>}
            {sourceVersions?.items.filter(r => r.revision === sourceRevision).map(r => <details key={r.revision}><summary>{text('Selected immutable source content', 'Seçili değişmez kaynak içeriği')}</summary><dl>{Object.entries(r.content).map(([key, f]) => <div key={key}><dt>{labels[key as FieldKey][en ? 0 : 1]}</dt><dd>{f.state === 'Known' ? f.value : f.state}</dd></div>)}</dl></details>)}
          </section>}
        </fieldset>
        {sourceError && <div role="alert">{text('Source selection unavailable.', 'Kaynak seçimi kullanılamıyor.')}<button type="button" onClick={() => { setSourceError(false); setSourceAttempt(v => v + 1) }}>{text('Retry sources', 'Kaynakları tekrar dene')}</button></div>}
        {editable && <button disabled={saving || offline || conflict || denied} type="submit">{saving ? text('Saving…', 'Kaydediliyor…') : frozen ? text('Retry frozen command', 'Sabit komutu tekrar dene') : text('Save draft', 'Taslağı kaydet')}</button>}
        {(dirty || frozen || conflict) && <p role="status">{text('Unsaved draft retained in this tab.', 'Kaydedilmemiş taslak bu sekmede korunuyor.')}</p>}
        <button type="button" disabled={saving} onClick={() => onNavigate(`/app/${kind}`)}>{text('Back to register', 'Kayıtlara dön')}</button>
      </form>}
      {id && <section aria-label={text('Immutable history', 'Değişmez geçmiş')}><h2>{text('Immutable history', 'Değişmez geçmiş')}</h2>{historyError ? <div role="alert">{text('History unavailable. Editor retained.', 'Geçmiş kullanılamıyor. Düzenlemeler korundu.')}<button onClick={() => { setHistoryError(false); setHistoryAttempt(v => v + 1) }}>{text('Retry history', 'Geçmişi tekrar dene')}</button></div> : !history ? <p role="status">{text('Loading history…', 'Geçmiş yükleniyor…')}</p> : <>{history.items.map(r => <details key={r.revision}><summary>{text('Revision', 'Sürüm')} {r.revision} · {r.savedAt} · {r.provenance}</summary><dl>{Object.entries(r.content).map(([key, f]) => <div key={key}><dt>{labels[key as FieldKey][en ? 0 : 1]}</dt><dd>{f.state === 'Known' ? f.value : f.state === 'Unknown' ? text('Unknown', 'Bilinmiyor') : text('Missing', 'Eksik')}</dd></div>)}<dt>{text('Source revision', 'Kaynak sürümü')}</dt><dd>{r.sourceId ?? '—'} / {r.sourceRevision ?? '—'}</dd><dt>{text('Integrity hash (not authority)', 'Bütünlük özeti (yetki değil)')}</dt><dd className="register-hash">{r.hash}</dd></dl></details>)}{pageButtons(history, historyOffset, n => { setHistory(null); setHistoryOffset(n) })}</>}</section>}
    </>}
  </section>
}
