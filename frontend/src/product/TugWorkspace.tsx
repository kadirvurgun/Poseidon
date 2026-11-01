import { useEffect, useState } from 'react'
import './tug.css'
import { DraftConfirmation } from './DraftConfirmation'
import { ApiError, session } from './api'
import { emptyTugInputs, tugApi, type TugAttempt, type TugBoard, type TugContext, type TugInputs, type TugRequest } from './tug-api'

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const text = {
  en: { title: 'Tug operations', context: 'Tug context', board: 'Job board', intake: 'Job intake', detail: 'Job detail', select: 'Select tug', loading: 'Loading…', empty: 'No draft requests.', unavailable: 'Data unavailable. Reconnect and retry.', denied: 'Access is not available.', retry: 'Retry', save: 'Save draft', edit: 'Edit draft', discard: 'Discard changes', stay: 'Stay', leave: 'Discard', dirty: 'Unsaved changes', uncertain: 'Save outcome uncertain. The exact attempt is retained; retry after reconnecting.', conflict: 'Conflict. Your draft is retained; discard and reload before a new attempt.', saved: 'Draft saved. Planning and dispatch approval remain unavailable.', offline: 'Offline. Displayed data may be stale. Draft stays in this page.', unknown: 'Unknown', why: 'Why / next action', bp: 'Required BP (tonnes, exact decimal)', owner: 'Request owner', start: 'Requested start (UTC)', end: 'Requested end (UTC, conditional)', count: 'Required tug count', search: 'Search requests', all: 'All priorities', previous: 'Previous', next: 'Next', assignments: 'Assigned tugs', readiness: 'Readiness', conflictLabel: 'Operational conflict', window: 'Requested window', type: 'Job type', priority: 'Priority', required: 'Missing required fields', bpContext: 'Profile BP (unverified)', boundary: 'Draft declarations only. No assignment, availability, permit or safety approval.', equipment: 'Equipment context', freshness: 'Freshness', provenance: 'Provenance' },
  tr: { title: 'Römorkör operasyonları', context: 'Römorkör bağlamı', board: 'İş panosu', intake: 'İş talebi', detail: 'İş detayı', select: 'Römorkör seç', loading: 'Yükleniyor…', empty: 'Taslak talep yok.', unavailable: 'Veri alınamadı. Bağlanıp tekrar deneyin.', denied: 'Erişim kullanılamıyor.', retry: 'Tekrar dene', save: 'Taslağı kaydet', edit: 'Taslağı düzenle', discard: 'Değişiklikleri bırak', stay: 'Kal', leave: 'Bırak', dirty: 'Kaydedilmemiş değişiklikler', uncertain: 'Kaydın sonucu belirsiz. Aynı deneme korundu; bağlanınca tekrar deneyin.', conflict: 'Çakışma. Taslağınız korundu; yeni deneme için bırakıp yeniden yükleyin.', saved: 'Taslak kaydedildi. Planlama ve sevk onayı kullanılamıyor.', offline: 'Çevrimdışı. Veriler eski olabilir. Taslak bu sayfada korunur.', unknown: 'Bilinmiyor', why: 'Neden / sonraki adım', bp: 'Gerekli BP (ton, kesin ondalık)', owner: 'Talep sorumlusu', start: 'Talep başlangıcı (UTC)', end: 'Talep bitişi (UTC, koşullu)', count: 'Gerekli römorkör sayısı', search: 'Taleplerde ara', all: 'Tüm öncelikler', previous: 'Önceki', next: 'Sonraki', assignments: 'Atanan römorkörler', readiness: 'Hazırlık', conflictLabel: 'Operasyonel çakışma', window: 'Talep aralığı', type: 'İş tipi', priority: 'Öncelik', required: 'Eksik zorunlu alanlar', bpContext: 'Profil BP (doğrulanmamış)', boundary: 'Yalnız taslak beyanlar. Atama, uygunluk, izin veya emniyet onayı oluşturmaz.', equipment: 'Ekipman bağlamı', freshness: 'Güncellik', provenance: 'Kaynak niteliği' },
}
const labels: Record<string, [string, string]> = { customerRef: ['Müşteri referansı', 'Customer reference'], assistedVessel: ['Hizmet verilecek gemi', 'Assisted vessel'], assistedVesselImo: ['Gemi IMO', 'Assisted vessel IMO'], operationArea: ['Operasyon alanı', 'Operation area'], pickupPoint: ['Alış noktası', 'Pickup point'], destinationPoint: ['Varış noktası', 'Destination point'], specialRequirements: ['Özel gereksinimler', 'Special requirements'], notes: ['Notlar', 'Notes'] }
function location() {
  const url = new URL(window.location.href), params = url.searchParams
  const match = /^\/app\/(tug-command|tug-job-board|tug-job-intake|tug-job-detail)(?:\/([0-9a-f-]+))?\/?$/i.exec(url.pathname)
  const vessel = params.get('vessel') ?? '', id = match?.[2] ?? ''
  const offset = Number(params.get('offset') ?? '0')
  const invalid = !match || (vessel !== '' && !uuid.test(vessel)) || (id !== '' && !uuid.test(id)) ||
    (match?.[1] === 'tug-job-detail' ? !id : !!id) || !Number.isSafeInteger(offset) || offset < 0 || offset > 100000 ||
    [...params.keys()].some(k => !['vessel', 'priority', 'q', 'offset'].includes(k) || params.getAll(k).length !== 1)
  return { screen: match?.[1], vessel, id, priority: params.get('priority') ?? '', q: params.get('q') ?? '', offset, invalid }
}
export function TugWorkspace() {
  const [lang, language] = useState<'tr' | 'en'>('tr'), t = text[lang]
  const [route, setRoute] = useState(location), [reload, setReload] = useState(0)
  const [ctx, context] = useState<TugContext | null>(null), [vessels, setVessels] = useState<TugContext['vessel'][]>([])
  const [board, setBoard] = useState<TugBoard | null>(null), [record, setRecord] = useState<TugRequest | null>(null)
  const [inputs, setInputs] = useState<TugInputs>(emptyTugInputs), [csrf, setCsrf] = useState('')
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [notice, setNotice] = useState('')
  const [editing, setEditing] = useState(false), [dirty, setDirty] = useState(false), [busy, setBusy] = useState(false)
  const [attempt, setAttempt] = useState<TugAttempt | null>(null), [conflict, setConflict] = useState(false)
  const [pending, setPending] = useState<(() => void) | null>(null), [offline, setOffline] = useState(!navigator.onLine)
  const [search, setSearch] = useState(route.q)
  const guard = (action: () => void) => { if (busy) return; if (dirty || attempt) setPending(() => action); else action() }
  const move = (href: string) => guard(() => { history.pushState(null, '', href); setRoute(location()); setReload(n => n + 1) })
  const href = (screen: string) => `/app/${screen}${route.vessel ? `?vessel=${route.vessel}` : ''}`
  useEffect(() => {
    const pop = () => { const target = window.location.href; if (busy || dirty || attempt) { history.pushState(null, '', `/app/${route.screen}${route.id ? '/' + route.id : ''}?${new URLSearchParams({ vessel: route.vessel, priority: route.priority, q: route.q, offset: String(route.offset) })}`); setPending(() => () => { history.pushState(null, '', target); setRoute(location()); setReload(n => n + 1) }) } else { setRoute(location()); setReload(n => n + 1) } }
    const unload = (e: BeforeUnloadEvent) => { if (dirty || busy || attempt) e.preventDefault() }
    const network = () => setOffline(!navigator.onLine)
    window.addEventListener('popstate', pop); window.addEventListener('beforeunload', unload); window.addEventListener('online', network); window.addEventListener('offline', network)
    return () => { window.removeEventListener('popstate', pop); window.removeEventListener('beforeunload', unload); window.removeEventListener('online', network); window.removeEventListener('offline', network) }
  }, [dirty, busy, attempt, route])
  useEffect(() => {
    document.documentElement.lang = lang; document.title = `POSEIDON · ${t.title}`
  }, [lang, t.title])
  useEffect(() => {
    const c = new AbortController(); let active = true; const timeout = setTimeout(() => c.abort(), 10000)
    setLoading(true); setError(0); context(null); setBoard(null); setRecord(null); setSearch(route.q); setEditing(route.screen === 'tug-job-intake'); setDirty(false); setAttempt(null); setConflict(false); setInputs(emptyTugInputs())
    async function load() {
      if (route.invalid) throw new ApiError(400)
      const s = await session(c.signal)
      if (!s.authenticated || !s.canRead) throw new ApiError(403)
      if (active) setCsrf(s.csrfToken)
      if (!route.vessel) { const list = await tugApi.vessels(c.signal); if (active) setVessels(list.vessels); return }
      const v = await tugApi.context(route.vessel, c.signal); if (!active) return; context(v)
      if (route.screen === 'tug-job-board') { const b = await tugApi.board(route.vessel, route.priority, route.q, route.offset, c.signal); if (active) setBoard(b) }
      if (route.screen === 'tug-job-detail') { const r = await tugApi.detail(route.id, route.vessel, c.signal); if (active) { setRecord(r); setInputs(r.inputs) } }
    }
    load().catch(e => { if (active) setError(e instanceof ApiError ? e.status : 503) }).finally(() => { if (active) setLoading(false); clearTimeout(timeout) })
    return () => { active = false; c.abort(); clearTimeout(timeout) }
  }, [route, reload])
  const change = (key: keyof TugInputs, value: string | number | null) => { setInputs(x => ({ ...x, [key]: value })); setDirty(true); setNotice('') }
  const save = async () => {
    const next = attempt ?? { key: crypto.randomUUID(), body: { clientId: record?.id ?? crypto.randomUUID(), baseVersion: record?.version ?? null, inputs: structuredClone(inputs) }, update: !!record }
    setAttempt(next); setBusy(true); setNotice('')
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000)
    try {
      const r = await tugApi.save(route.vessel, next, csrf, c.signal)
      setAttempt(null); setDirty(false); setEditing(false); setRecord(r); setInputs(r.inputs); setNotice('saved')
      history.pushState(null, '', `/app/tug-job-detail/${r.id}?vessel=${route.vessel}`); setRoute(location())
    } catch (e) {
      if (e instanceof ApiError && e.status === 400) { setAttempt(null); setNotice(e.code) }
      else if (e instanceof ApiError && [401, 403, 404, 409].includes(e.status)) { setConflict(true); setNotice('conflict') }
      else setNotice('uncertain')
    } finally { clearTimeout(timeout); setBusy(false) }
  }
  const formOpen = ctx?.canWrite && editing
  const nullText = (value: string | null | undefined) => value ?? t.unknown
  return <div className="product-shell tug-workspace"><a className="skip-link" href="#tug-main">{lang === 'en' ? 'Skip to content' : 'İçeriğe geç'}</a>
    <header className="product-header"><a className="product-brand" href="/app" onClick={e => { if (dirty || attempt || busy) { e.preventDefault(); guard(() => window.location.assign('/app')) } }}>POSEIDON</a><div role="group" aria-label="Language"><button aria-pressed={lang === 'tr'} onClick={() => language('tr')}>TR</button><button aria-pressed={lang === 'en'} onClick={() => language('en')}>EN</button></div></header>
    <main id="tug-main" tabIndex={-1}><h1>{t.title}</h1><p>{t.boundary}</p>{offline && <p role="alert">{t.offline}</p>}
      {loading && <p role="status">{t.loading}</p>}{error > 0 && <div role="alert"><p>{[401, 403, 404].includes(error) ? t.denied : t.unavailable}</p><button onClick={() => guard(() => setReload(n => n + 1))}>{t.retry}</button></div>}
      {!loading && !error && !route.vessel && <section><h2>{t.select}</h2>{vessels.length === 0 && <p>{t.empty}</p>}{vessels.map(v => <p key={v.id}><a href={`/app/tug-command?vessel=${v.id}`} onClick={e => { e.preventDefault(); move(`/app/tug-command?vessel=${v.id}`) }}>{v.code} · {v.name}</a></p>)}</section>}
      {!loading && !error && ctx && <><p>{t.context}: <strong>{ctx.vessel.code} · {ctx.vessel.name}</strong> · {ctx.mode}</p>
        <nav aria-label={t.title}>{(['tug-command', 'tug-job-board', ...(ctx.canWrite ? ['tug-job-intake'] : [])] as const).map((s, i) => <a key={s} aria-current={route.screen === s ? 'page' : undefined} href={href(s)} onClick={e => { e.preventDefault(); move(href(s)) }}>{[t.context, t.board, t.intake][i]} </a>)}</nav>
        {route.screen === 'tug-command' && <section><h2>{t.context}</h2><dl><dt>{lang === 'en' ? 'Role references' : 'Rol referansları'}</dt><dd>{ctx.roleReferences ?? t.unknown}</dd><dt>{lang === 'en' ? 'Tug type' : 'Römorkör tipi'}</dt><dd>{t.unknown}</dd><dt>{t.bpContext}</dt><dd>{nullText(ctx.bollardPull.value)}</dd><dt>{t.provenance}</dt><dd>{ctx.bollardPull.provenance} · {ctx.bollardPull.version ?? '—'}</dd><dt>{t.freshness}</dt><dd>{t.unknown}</dd><dt>{t.readiness}</dt><dd>{t.unknown}</dd><dt>{t.why}</dt><dd>{ctx.reason}</dd></dl><h3>{t.equipment}</h3><p>{ctx.equipmentCoverage}</p><ul>{ctx.equipment?.map(a => <li key={a.id}>{a.equipmentType ?? t.unknown} · {a.level} · r{a.revision} · {a.provenance}</li>)}</ul></section>}
        {notice && <p role="alert">{notice === 'saved' ? t.saved : notice === 'uncertain' ? t.uncertain : notice === 'conflict' ? t.conflict : `${lang === 'en' ? 'Check fields' : 'Alanları kontrol edin'} (${notice})`}</p>}
        {route.screen === 'tug-job-board' && <section><h2>{t.board}</h2><form onSubmit={e => { e.preventDefault(); move(`${href('tug-job-board')}&priority=${encodeURIComponent(route.priority)}&q=${encodeURIComponent(search)}`) }}><label>{t.search}<input type="search" maxLength={100} value={search} onChange={e => setSearch(e.target.value)} /></label><button>{t.search}</button></form><label>{t.priority}<select value={route.priority} onChange={e => move(`${href('tug-job-board')}&priority=${encodeURIComponent(e.target.value)}`)}><option value="">{t.all}</option>{ctx.priorities.map(p => <option key={p}>{p}</option>)}</select></label>
          {board?.items.length === 0 && <p>{t.empty}</p>}<div className="product-table"><table><thead><tr>{['Job ID', t.window, t.assignments, t.bp, t.readiness, t.conflictLabel, t.priority, t.why].map(h => <th key={h}>{h}</th>)}</tr></thead><tbody>{board?.items.map(row => <tr key={row.request.id}><td><a href={row.detailHref} onClick={e => { e.preventDefault(); move(row.detailHref) }}>{row.request.jobNumber}</a><p>{nullText(row.request.inputs.assistedVessel)}</p></td><td>{row.request.inputs.requestedStart ?? '—'} / {row.request.inputs.requestedEnd ?? '—'}<p>{row.windowState}</p><small>{row.plannedTimeSource}</small></td><td>{row.assignedTugs.join(', ') || '—'}<p>{row.assignmentCoverage}</p></td><td>{row.request.inputs.requiredBollardPull ?? t.unknown}<p>{lang === 'en' ? 'Available' : 'Kullanılabilir'}: {row.availableBollardPull ?? t.unknown}</p></td><td>{row.readiness === 'Unknown' ? t.unknown : row.readiness}</td><td>{row.conflict === 'Unknown' ? t.unknown : row.conflict}</td><td>{nullText(row.request.inputs.priority)}</td><td><details><summary>{t.why}</summary><p>{row.nextAction}</p><p>{row.comparisonCoverage}</p><ul>{row.declaredComparisons.map(x => <li key={x.jobId}>{x.jobId}: {x.relation}</li>)}</ul><ul>{row.blockingReasons.map(r => <li key={r}>{r}</li>)}</ul></details></td></tr>)}</tbody></table></div><button disabled={route.offset === 0} onClick={() => move(`${href('tug-job-board')}&offset=${Math.max(0, route.offset - 25)}&priority=${route.priority}&q=${encodeURIComponent(route.q)}`)}>{t.previous}</button><button disabled={!board?.hasMore} onClick={() => move(`${href('tug-job-board')}&offset=${route.offset + 25}&priority=${route.priority}&q=${encodeURIComponent(route.q)}`)}>{t.next}</button></section>}
        {route.screen === 'tug-job-detail' && record && <section><h2>{t.detail} · {record.jobNumber}</h2><p>Draft · r{record.revision} · {record.provenance}</p><p>{record.sourceVersion}</p><p>{t.required}: {record.missing.join(', ') || '—'}</p>{!editing && <><dl>{Object.entries(record.inputs).map(([key, value]) => <div key={key}><dt>{labels[key]?.[lang === 'en' ? 1 : 0] ?? key}</dt><dd>{value ?? t.unknown}</dd></div>)}</dl>{ctx.canWrite && <button onClick={() => setEditing(true)}>{t.edit}</button>}</>}</section>}
        {route.screen === 'tug-job-intake' && !ctx.canWrite && <p role="alert">{t.denied}</p>}
        {formOpen && <form onSubmit={e => { e.preventDefault(); void save() }} aria-label={t.intake}><fieldset disabled={busy || !!attempt}><legend>{t.intake}</legend>{Object.entries(labels).map(([key, label]) => <label key={key}>{label[lang === 'en' ? 1 : 0]}<input maxLength={['notes', 'specialRequirements'].includes(key) ? 2000 : 200} value={String(inputs[key as keyof TugInputs] ?? '')} onChange={e => change(key as keyof TugInputs, e.target.value || null)} /></label>)}
          <label>{lang === 'en' ? 'Request source' : 'Talep kaynağı'}<select value={inputs.requestSource ?? ''} onChange={e => change('requestSource', e.target.value || null)}><option value="">{t.unknown}</option>{ctx.requestSources.map(x => <option key={x}>{x}</option>)}</select></label>
          <label>{t.type}<select value={inputs.jobType ?? ''} onChange={e => change('jobType', e.target.value || null)}><option value="">{t.unknown}</option>{ctx.jobTypes.map(x => <option key={x}>{x}</option>)}</select></label>
          <label>{t.priority}<select value={inputs.priority ?? ''} onChange={e => change('priority', e.target.value || null)}><option value="">{t.unknown}</option>{ctx.priorities.map(x => <option key={x}>{x}</option>)}</select></label>
          <label>{t.owner}<select value={inputs.requestOwner ?? ''} onChange={e => change('requestOwner', e.target.value || null)}><option value="">{t.unknown}</option>{ctx.owners.map(x => <option key={x}>{x}</option>)}</select></label>
          {(['requestedStart', 'requestedEnd'] as const).map((key, i) => <label key={key}>{i ? t.end : t.start}<input type="datetime-local" step="1" value={inputs[key]?.replace(/Z$/, '').slice(0, 19) ?? ''} onChange={e => change(key, e.target.value ? `${e.target.value.length === 16 ? e.target.value + ':00' : e.target.value}Z` : null)} /></label>)}
          <label>{t.bp}<input inputMode="decimal" maxLength={11} value={inputs.requiredBollardPull ?? ''} onChange={e => change('requiredBollardPull', e.target.value || null)} /></label><label>{t.count}<input type="number" min="1" max="1000" step="1" value={inputs.requiredTugCount ?? ''} onChange={e => change('requiredTugCount', e.target.value ? Number(e.target.value) : null)} /></label>
        </fieldset><button disabled={busy || offline || conflict}>{attempt ? t.retry : t.save}</button><button type="button" disabled={busy} onClick={() => guard(() => { setDirty(false); setAttempt(null); setReload(n => n + 1) })}>{t.discard}</button></form>}
      </>}
      {pending && <DraftConfirmation language={lang} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); setDirty(false); setAttempt(null); action() }} />}
    </main></div>
}
