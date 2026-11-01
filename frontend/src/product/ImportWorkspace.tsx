import { WorkspaceContextBar } from './WorkspaceContextBar'
import { useEffect, useRef, useState } from 'react'
import { ApiError, session } from './api'
import type { Session } from './directory-contract'
import * as api from './import-api'
import { roster, type CrewPage } from './crew-api'
import { ImportMappingEditor, type ImportMappingBinding } from './ImportMappingEditor'
import { ImportValidationResults } from './ImportValidationResults'
import './import-workspace.css'

export function ImportWorkspace({ vessel }: { vessel: string }) {
  const [language, setLanguage] = useState<'tr' | 'en'>('tr'), [identity, setIdentity] = useState<Session | null>(null)
  const [href, setHref] = useState(window.location.href), [generation, setGeneration] = useState(0), [error, setError] = useState(0)
  const [dirty, setDirty] = useState(false), [pending, setPending] = useState<string | null>(null)
  const location = useRef(href), dirtyRef = useRef(false)
  dirtyRef.current = dirty
  const en = language === 'en'
  useEffect(() => {
    const controller = new AbortController(); setIdentity(null); setError(0)
    session(controller.signal).then(s => { if (!controller.signal.aborted) { if (s.authenticated && s.canRead) setIdentity(s); else setError(401) } })
      .catch(e => { if (!controller.signal.aborted) setError(e instanceof ApiError ? e.status : 503) })
    return () => controller.abort()
  }, [generation])
  useEffect(() => {
    const leave = (event: BeforeUnloadEvent) => { if (dirtyRef.current) { event.preventDefault(); event.returnValue = '' } }
    const pop = () => { const next = window.location.href; if (dirtyRef.current) { history.pushState(null, '', location.current); setPending(next) }
      else { location.current = next; setHref(next) } }
    const privacy = () => { if (document.visibilityState === 'hidden') setIdentity(null); else setGeneration(g => g + 1) }
    window.addEventListener('beforeunload', leave); window.addEventListener('popstate', pop); document.addEventListener('visibilitychange', privacy)
    return () => { window.removeEventListener('beforeunload', leave); window.removeEventListener('popstate', pop); document.removeEventListener('visibilitychange', privacy) }
  }, [])
  useEffect(() => { document.documentElement.lang = language; document.title = `POSEIDON · ${en ? 'Controlled import' : 'Kontrollü içe aktarma'}` }, [language, en])
  function navigate(next: string) {
    if (dirty) { setPending(next); return }
    history.pushState(null, '', next); location.current = window.location.href; setHref(window.location.href)
  }
  return <div className="product-shell import-workspace"><a className="skip-link" href="#import-main">{en ? 'Import' : 'İçe aktarma'}</a>
    <header><a href={`/app?tab=fleet&selected=${vessel}`}>{en ? 'Vessel' : 'Araç'}</a><div role="group" aria-label={en ? 'Language' : 'Dil'}>
      <button onClick={() => setLanguage('tr')} aria-pressed={!en}>TR</button><button onClick={() => setLanguage('en')} aria-pressed={en}>EN</button></div><WorkspaceContextBar session={identity} vessel={vessel} language={language} /></header>
    <main id="import-main" tabIndex={-1}><h1>{en ? 'Controlled import' : 'Kontrollü içe aktarma'}</h1>
      <p>{en ? 'Vessel · crew · certificate · equipment. Declarations remain unverified. Trial results do not grant owner sign-off, pilot authorization or readiness.' : 'Araç · mürettebat · sertifika · ekipman. Beyanlar doğrulanmamış kalır. Deneme sonuçları veri sahibi onayı, pilot yetkisi veya hazır olma durumu oluşturmaz.'}</p>
      {error ? <div role="alert"><p>{error === 401 ? (en ? 'Sign in again.' : 'Yeniden oturum açın.') : (en ? 'Import access unavailable.' : 'İçe aktarma erişimi kullanılamıyor.')}</p>
        <button onClick={() => setGeneration(g => g + 1)}>{en ? 'Retry' : 'Tekrar dene'}</button>{error === 401 && <a href={`/api/v1/auth/login?returnUrl=${encodeURIComponent(window.location.pathname)}`}>{en ? 'Sign in' : 'Oturum aç'}</a>}</div>
        : !identity ? <p role="status">{en ? 'Loading session…' : 'Oturum yükleniyor…'}</p>
        : <Importer key={`${generation}:${identity.tenantId}:${identity.actorId}:${identity.csrfToken}:${vessel}:${href}`} vessel={vessel} language={language} user={identity} href={href} navigate={navigate} savedNavigate={next => { setDirty(false); dirtyRef.current = false; history.pushState(null, '', next); location.current = window.location.href; setHref(window.location.href) }} onDirty={setDirty} />}
    </main>
    {pending && <div role="dialog" aria-modal="true" aria-label={en ? 'Unsaved import changes' : 'Kaydedilmemiş içe aktarma değişiklikleri'}>
      <p>{en ? 'Discard the current mapping or uncertain attempt?' : 'Geçerli eşleme veya belirsiz deneme bırakılsın mı?'}</p>
      <button autoFocus onClick={() => setPending(null)}>{en ? 'Keep working' : 'Düzenlemeye devam et'}</button>
      <button onClick={() => { setDirty(false); dirtyRef.current = false; const next = pending; setPending(null); history.pushState(null, '', next); location.current = window.location.href; setHref(window.location.href) }}>{en ? 'Discard and leave' : 'Bırak ve ayrıl'}</button>
    </div>}
  </div>
}

function Importer({ vessel, language, user, href, navigate, savedNavigate, onDirty }: { vessel: string; language: 'tr' | 'en'; user: Session; href: string; navigate: (href: string) => void; savedNavigate: (href: string) => void; onDirty: (dirty: boolean) => void }) {
  const en = language === 'en', query = new URL(href).searchParams
  const [templates, setTemplates] = useState<api.ImportTemplate[]>([]), [category, setCategory] = useState('vessel')
  const [assignment, setAssignment] = useState(query.get('assignment') ?? ''), [crew, setCrew] = useState<CrewPage | null>(null), [crewOffset, setCrewOffset] = useState(0)
  const [batch, setBatch] = useState<api.ImportBatch | null>(null), [bindings, setBindings] = useState<readonly ImportMappingBinding[]>([])
  const [preview, setPreview] = useState<api.Preview | null>(null), [outcome, setOutcome] = useState<api.Committed | null>(null)
  const [busy, setBusy] = useState(true), [error, setError] = useState(0), [failure, setFailure] = useState<'draft' | 'preview' | null>(null)
  const [file, setFile] = useState<File | null>(null), [dirty, setDirty] = useState(false), [uncertain, setUncertain] = useState(false)
  const [historyRows, setHistoryRows] = useState<Awaited<ReturnType<typeof api.history>> | null>(null), [historyOffset, setHistoryOffset] = useState(0)
  const attempt = useRef<{ key: string; preview: api.Preview } | null>(null), operation = useRef<AbortController | null>(null), mounted = useRef(true)
  const selectedTemplate = batch?.template ?? templates.find(t => t.category === category)
  const contextKey = `${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${batch?.id ?? 'new'}:${selectedTemplate?.version}:${selectedTemplate?.fingerprint}`
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; operation.current?.abort(); onDirty(false) } }, [onDirty])
  useEffect(() => { onDirty(dirty || uncertain || busy && !!attempt.current) }, [dirty, uncertain, busy, onDirty])
  useEffect(() => {
    const c = new AbortController(); operation.current = c
    async function load() {
      const list = await api.templates(vessel, c.signal); if (c.signal.aborted) return
      setTemplates(list); if (list[0]) setCategory(query.has('assignment') && list.some(t => t.category === 'certificate') ? 'certificate' : list[0].category)
      const id = query.get('batch')
      if (id) {
        const b = await api.detail(vessel, id, c.signal); if (c.signal.aborted) return
        setBatch(b); setCategory(b.category); setBindings(b.bindings)
        if (query.get('preview')) {
          const p = await api.page(vessel, b.id, { previewId: query.get('preview')!, revision: query.get('revision') ?? '', templateFingerprint: query.get('fingerprint') ?? '' } as api.Preview,
            Number(query.get('offset') ?? 0), c.signal)
          if (!c.signal.aborted) { api.results(p, b.template, contextKey); setPreview(p) }
        }
      }
    }
    load().catch(e => fail(e, c)).finally(() => { if (!c.signal.aborted) setBusy(false) })
    return () => c.abort()
    // A new href or actor/session mounts a fresh private workspace.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  useEffect(() => { const c = new AbortController(); setHistoryRows(null); api.history(vessel, historyOffset, c.signal).then(rows => { if (!c.signal.aborted) setHistoryRows(rows) }).catch(() => {})
    return () => c.abort() }, [vessel, historyOffset, outcome])
  useEffect(() => { if (category !== 'certificate' || batch) return; const c = new AbortController(); setCrew(null); setAssignment('')
    roster(vessel, crewOffset, c.signal).then(rows => { if (!c.signal.aborted) { setCrew(rows); if (rows.items.some(r => r.id === query.get('assignment'))) setAssignment(query.get('assignment')!) } }).catch(() => { if (!c.signal.aborted) setError(403) }); return () => c.abort()
  }, [category, vessel, crewOffset, batch])
  function start() { operation.current?.abort(); const c = new AbortController(); operation.current = c; setBusy(true); setError(0); return c }
  function fail(e: unknown, c: AbortController) { if (!c.signal.aborted && mounted.current) {
    const status = e instanceof ApiError ? e.status : 503; setError(status)
    if ([401, 403, 404].includes(status)) { setBatch(null); setPreview(null); setBindings([]); setHistoryRows(null); setTemplates([]); setFile(null); onDirty(false) }
  } }
  function finish(c: AbortController) { if (!c.signal.aborted && mounted.current) setBusy(false) }
  function route(b: api.ImportBatch, p?: api.Preview, offset = 0) {
    const q = new URLSearchParams({ batch: b.id }); if (p) { q.set('preview', p.previewId); q.set('revision', p.revision); q.set('fingerprint', p.templateFingerprint); q.set('offset', String(offset)) }
    return `/app/excel-migration/${vessel}?${q}`
  }
  async function upload() {
    if (!file || !selectedTemplate || busy || category === 'certificate' && !assignment) return
    if (file.size > selectedTemplate.maxBytes || !file.name.toLowerCase().endsWith('.xlsx')) { setError(400); return }
    const c = start()
    try { const b = await api.upload(vessel, category, assignment, file, user.csrfToken, c.signal); if (!c.signal.aborted) navigate(route(b)) }
    catch (e) { fail(e, c) } finally { finish(c) }
  }
  async function save(previewAfter: boolean) {
    if (!batch || busy || uncertain) return
    const c = start(); setFailure(null); setPreview(null)
    try {
      const b = await api.draft(vessel, batch, bindings, user.csrfToken, c.signal); if (c.signal.aborted) return
      setBatch(b); setDirty(false); onDirty(false)
      if (previewAfter) { const p = await api.preview(vessel, b.id, user.csrfToken, c.signal); if (!c.signal.aborted) { setPreview(p); savedNavigate(route(b, p)) } }
    } catch (e) { if (!c.signal.aborted) setFailure(previewAfter ? 'preview' : 'draft'); fail(e, c) } finally { finish(c) }
  }
  async function commit() {
    if (!batch || !preview || busy || dirty) return
    if (!attempt.current) attempt.current = { key: crypto.randomUUID(), preview }
    const c = start()
    try {
      const result = await api.commit(vessel, batch.id, attempt.current.preview, user.csrfToken, attempt.current.key, c.signal)
      if (!c.signal.aborted) { setOutcome(result); setBatch({ ...batch, state: result.state }); setUncertain(false); attempt.current = null; onDirty(false) }
    } catch (e) {
      if (!c.signal.aborted) {
        // An interrupted response can follow an actual commit. Keep the exact attempt.
        const status = e instanceof ApiError ? e.status : 0
        setUncertain(status === 0 || status >= 500); if (status > 0 && status < 500) attempt.current = null
      }
      fail(e, c)
    } finally { finish(c) }
  }
  async function download(reject: boolean) {
    if (!selectedTemplate || busy) return
    const c = start()
    try { const blob = await api.download(vessel, category, reject ? batch?.id ?? null : null, reject ? preview : null, c.signal)
      if (!c.signal.aborted && mounted.current) { const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = reject ? 'poseidon-rejects.csv' : `poseidon-${category}-declaration-v1.xlsx`; link.click(); URL.revokeObjectURL(url) }
    } catch (e) { fail(e, c) } finally { finish(c) }
  }
  const state = busy && !batch ? 'loading' : error === 401 || error === 403 || error === 404 ? 'denied' : error ? 'unavailable' : 'ready'
  let resultPage: ReturnType<typeof api.results> | null = null
  try { if (preview && batch) resultPage = api.results(preview, batch.template, contextKey) } catch { /* Invalid/stale contracts render the safe unavailable component state. */ }
  const categories: Record<string, string> = { vessel: en ? 'Vessel' : 'Araç', crew: en ? 'Crew' : 'Mürettebat', certificate: en ? 'Certificate' : 'Sertifika', equipment: en ? 'Equipment' : 'Ekipman' }
  const states: Record<string, string> = { Uploaded: en ? 'Uploaded' : 'Yüklendi', MappingDraft: en ? 'Mapping draft' : 'Eşleme taslağı', Previewed: en ? 'Previewed' : 'Önizlendi', Committed: en ? 'Committed' : 'Kaydedildi' }
  const unknown = en ? 'Unavailable' : 'Kullanılamıyor'
  return <>
    {error > 0 && <p role="alert">{error === 409 ? (en ? 'The source, mapping or current record changed. Reload and validate again.' : 'Kaynak, eşleme veya geçerli kayıt değişti. Yeniden yükleyip doğrulayın.') : state === 'denied' ? (en ? 'Import access denied.' : 'İçe aktarma erişimi reddedildi.') : (en ? 'The request could not be completed.' : 'İstek tamamlanamadı.')}</p>}
    <button disabled={busy || uncertain} onClick={() => navigate(`/app/excel-migration/${vessel}`)}>{en ? 'New import / reload' : 'Yeni içe aktarma / yeniden yükle'}</button>
    {!batch && <section aria-label={en ? 'Import source' : 'İçe aktarma kaynağı'}>
      <label>{en ? 'Category' : 'Kategori'}<select value={category} disabled={busy} onChange={e => { setCategory(e.target.value); setFile(null); setBindings([]); setPreview(null) }}>{templates.map(t => <option key={t.category} value={t.category}>{({ vessel: en ? 'Vessel' : 'Araç', crew: en ? 'Crew' : 'Mürettebat', certificate: en ? 'Certificate' : 'Sertifika', equipment: en ? 'Equipment' : 'Ekipman' } as Record<string, string>)[t.category]}</option>)}</select></label>
      {category === 'certificate' && <><label>{en ? 'Authorized crew assignment' : 'Yetkili personel ataması'}<select value={assignment} onChange={e => setAssignment(e.target.value)} disabled={busy || !crew}><option value="">{en ? 'Choose' : 'Seçin'}</option>{crew?.items.map((r, i) => <option key={r.id} value={r.id}>{r.fullName ?? `${en ? 'Assignment' : 'Atama'} ${crewOffset + i + 1}`}</option>)}</select></label>
        <button disabled={!crew || busy || crewOffset === 0} onClick={() => setCrewOffset(x => x - 25)}>{en ? 'Previous assignments' : 'Önceki atamalar'}</button><button disabled={!crew?.hasMore || busy} onClick={() => setCrewOffset(x => x + 25)}>{en ? 'Next assignments' : 'Sonraki atamalar'}</button></>}
      {selectedTemplate && <><p>{en ? 'Schema' : 'Şema'}: {selectedTemplate.version} · XLSX · {selectedTemplate.maxRows} {en ? 'rows maximum' : 'en çok satır'}</p>
        <p>{en ? 'Dates use YYYY-MM-DD text. Numeric declarations preserve exact digits. Controlled references still require resolution. Vessel rows update this selected vessel with its current base version.' : 'Tarihler YYYY-MM-DD metnidir. Sayısal beyanlarda basamaklar aynen korunur. Kontrollü referanslar çözüm bekler. Araç satırları seçili aracı geçerli temel sürümüyle günceller.'}</p>
        <button disabled={busy} onClick={() => void download(false)}>{en ? 'Download versioned template' : 'Sürümlü şablonu indir'}</button>
        <details><summary>{en ? 'Schema fields' : 'Şema alanları'}</summary><ul>{selectedTemplate.fields.map(f => <li key={f.key}>{api.label(f.key, language)} · {f.userWritable ? (en ? 'Declaration input' : 'Beyan girdisi') : (en ? 'System / controlled gate' : 'Sistem / kontrollü kapı')}</li>)}</ul></details></>}
      <label>{en ? 'Single-sheet XLSX source' : 'Tek sayfalı XLSX kaynağı'}<input type="file" accept=".xlsx" disabled={busy} onChange={e => setFile(e.target.files?.[0] ?? null)} /></label>
      <button disabled={busy || !file || !selectedTemplate || category === 'certificate' && !assignment} onClick={() => void upload()}>{en ? 'Upload for mapping' : 'Eşleme için yükle'}</button>
      {!busy && templates.length === 0 && !error && <p>{en ? 'No authorized import categories.' : 'Yetkili içe aktarma kategorisi yok.'}</p>}
    </section>}
    {batch && state !== 'denied' && <>
      <p>{en ? 'Source SHA-256' : 'Kaynak SHA-256'}: <bdi className="import-hash">{batch.sourceHash}</bdi></p><p>{en ? 'State' : 'Durum'}: {states[batch.state] ?? unknown}</p>
      {batch.state !== 'Committed' && !outcome && <ImportMappingEditor language={language} currentContextKey={contextKey} loadedContextKey={contextKey} state={state}
        template={api.mapping(batch.template, language)} headers={batch.headers} bindings={bindings} busy={busy || uncertain} failure={failure}
        onChange={b => { setBindings(b); setDirty(true); setPreview(null); attempt.current = null }} onSaveDraft={() => void save(false)} onPreview={() => void save(true)} />}
      {preview && !outcome && batch.state !== 'Committed' && <ImportValidationResults language={language} contextKey={contextKey} state={state === 'ready' ? 'ready' : 'unavailable'} page={resultPage}
        fieldLabels={batch.template.fields.map(f => api.label(f.key, language))} busy={busy || uncertain} canDownloadRejectReport={!dirty}
        onPage={offset => { if (!busy && !uncertain) navigate(route(batch, preview, offset)) }} onDownloadRejectReport={() => void download(true)} />}
      {uncertain && <p role="alert">{en ? 'Commit result uncertain. Retry the same attempt before editing; the server may already have committed.' : 'İçe aktarma sonucu belirsiz. Düzenlemeden önce aynı denemeyi tekrar deneyin; sunucu işlemi tamamlamış olabilir.'}</p>}
      {preview && !outcome && batch.state !== 'Committed' && <><p>{en ? 'Atomic trial import: every row must pass again; any conflict rolls the entire batch back. Production migration remains gated.' : 'Atomik deneme aktarımı: her satır yeniden geçmelidir; çakışma tüm paketi geri alır. Üretim göç kapısı ayrıdır.'}</p>
        <button disabled={busy || dirty || !resultPage || preview.rejectedRows > 0 || preview.totalRows === 0} onClick={() => void commit()}>{uncertain ? (en ? 'Retry same commit' : 'Aynı aktarımı tekrar dene') : (en ? 'Commit atomic trial import' : 'Atomik deneme aktarımını kaydet')}</button></>}
      <p>{en ? 'Reviewer: unassigned; human approval is a separate gate.' : 'İnceleyen atanmadı; insan onayı ayrı kapıdır.'}</p>
      {!outcome && batch.state === 'Committed' && <p role="status">{en ? 'Committed rows' : 'Kaydedilen satırlar'}: {batch.rowCount} · {en ? 'Unverified declarations' : 'Doğrulanmamış beyanlar'}</p>}
      {outcome && <p role="status">{en ? 'Committed rows' : 'Kaydedilen satırlar'}: {outcome.importedRows} · {en ? 'Unverified declarations' : 'Doğrulanmamış beyanlar'}</p>}
    </>}
    <section aria-label={en ? 'Import history' : 'İçe aktarma geçmişi'}><h2>{en ? 'Private session history' : 'Özel oturum geçmişi'}</h2>
      {!historyRows ? <p>{en ? 'History unavailable or loading.' : 'Geçmiş yükleniyor veya kullanılamıyor.'}</p> : historyRows.items.length === 0 ? <p>{en ? 'No import batches.' : 'İçe aktarma paketi yok.'}</p> : <ul>{historyRows.items.map(r => <li key={r.id}><button disabled={busy || uncertain} onClick={() => navigate(`/app/excel-migration/${vessel}?batch=${r.id}`)}>{categories[r.category] ?? unknown} · {states[r.state] ?? unknown} · {r.rowCount}</button></li>)}</ul>}
      <button disabled={busy || historyOffset === 0} onClick={() => setHistoryOffset(x => x - 25)}>{en ? 'Previous history' : 'Önceki geçmiş'}</button><button disabled={busy || !historyRows?.hasMore} onClick={() => setHistoryOffset(x => x + 25)}>{en ? 'Next history' : 'Sonraki geçmiş'}</button>
    </section>
  </>
}
