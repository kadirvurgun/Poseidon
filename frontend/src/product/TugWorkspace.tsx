import { EnterpriseChrome } from './EnterpriseChrome'
import { useCallback, useEffect, useRef, useState } from 'react'
import { EnterpriseActions } from './EnterprisePatterns'
import { isGuid } from './identifiers'
import { TugRequestFields } from './TugRequestFields'
import { TugProfileContext } from './TugProfileContext'
import { TugBoardView, tugBoardHref } from './TugBoardView'
import { TugRequestSummary } from './TugRequestSummary'
import { Button } from '../components/ui/button'
import { Plus } from 'lucide-react'
import './tug.css'
import { TugPlanningPanel } from './TugPlanningPanel'
import { DraftConfirmation } from './DraftConfirmation'
import { ApiError, session, type Session } from './api'
import { emptyTugInputs, tugApi, type TugAttempt, type TugBoard, type TugContext, type TugInputs, type TugRequest } from './tug-api'

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const text = {
  en: { title: 'Tug operations', context: 'Tug context', board: 'Job board', intake: 'Job intake', detail: 'Job detail', select: 'Select tug', loading: 'Loading…', empty: 'No draft requests.', unavailable: 'Data unavailable. Reconnect and retry.', denied: 'Access is not available.', retry: 'Retry', save: 'Save draft', edit: 'Edit draft', discard: 'Discard changes', stay: 'Stay', leave: 'Discard', dirty: 'Unsaved changes', uncertain: 'Save outcome uncertain. The exact attempt is retained; retry after reconnecting.', conflict: 'Conflict. Your draft is retained; discard and reload before a new attempt.', saved: 'Draft saved. Operational approval remains unavailable.', offline: 'Offline. Displayed data may be stale. Draft stays in this page.', unknown: 'Unknown', why: 'Why / next action', bp: 'Required BP (tonnes, exact decimal)', owner: 'Request owner', start: 'Requested start (UTC)', end: 'Requested end (UTC, conditional)', count: 'Required tug count', search: 'Search requests', all: 'All priorities', previous: 'Previous', next: 'Next', assignments: 'Assigned tugs', readiness: 'Readiness', conflictLabel: 'Operational conflict', window: 'Requested window', type: 'Job type', priority: 'Priority', required: 'Missing required fields', bpContext: 'Profile BP (unverified)', boundary: 'Draft declarations only. Draft planning is separate from operational assignment, availability, permits and safety approval.', equipment: 'Equipment context', freshness: 'Freshness', provenance: 'Provenance' },
  tr: { title: 'Römorkör operasyonları', context: 'Römorkör bağlamı', board: 'İş panosu', intake: 'İş talebi', detail: 'İş detayı', select: 'Römorkör seç', loading: 'Yükleniyor…', empty: 'Taslak talep yok.', unavailable: 'Veri alınamadı. Bağlanıp tekrar deneyin.', denied: 'Erişim kullanılamıyor.', retry: 'Tekrar dene', save: 'Taslağı kaydet', edit: 'Taslağı düzenle', discard: 'Değişiklikleri bırak', stay: 'Kal', leave: 'Bırak', dirty: 'Kaydedilmemiş değişiklikler', uncertain: 'Kaydın sonucu belirsiz. Aynı deneme korundu; bağlanınca tekrar deneyin.', conflict: 'Çakışma. Taslağınız korundu; yeni deneme için bırakıp yeniden yükleyin.', saved: 'Taslak kaydedildi. Operasyon onayı kullanılamıyor.', offline: 'Çevrimdışı. Veriler eski olabilir. Taslak bu sayfada korunur.', unknown: 'Bilinmiyor', why: 'Neden / sonraki adım', bp: 'Gerekli BP (ton, kesin ondalık)', owner: 'Talep sorumlusu', start: 'Talep başlangıcı (UTC)', end: 'Talep bitişi (UTC, koşullu)', count: 'Gerekli römorkör sayısı', search: 'Taleplerde ara', all: 'Tüm öncelikler', previous: 'Önceki', next: 'Sonraki', assignments: 'Atanan römorkörler', readiness: 'Hazırlık', conflictLabel: 'Operasyonel çakışma', window: 'Talep aralığı', type: 'İş tipi', priority: 'Öncelik', required: 'Eksik zorunlu alanlar', bpContext: 'Profil BP (doğrulanmamış)', boundary: 'Yalnız taslak beyanlar. Taslak planlama; operasyonel atama, uygunluk, izin ve emniyet onayından ayrıdır.', equipment: 'Ekipman bağlamı', freshness: 'Güncellik', provenance: 'Kaynak niteliği' },
}
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
  const [user, setUser] = useState<Session | null>(null)
  const [ctx, context] = useState<TugContext | null>(null), [vessels, setVessels] = useState<TugContext['vessel'][]>([])
  const [board, setBoard] = useState<TugBoard | null>(null), [record, setRecord] = useState<TugRequest | null>(null)
  const [inputs, setInputs] = useState<TugInputs>(emptyTugInputs), [csrf, setCsrf] = useState('')
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [notice, setNotice] = useState('')
  const [editing, setEditing] = useState(route.screen === 'tug-job-intake'), [dirty, setDirty] = useState(false), [busy, setBusy] = useState(false)
  const [attempt, setAttempt] = useState<TugAttempt | null>(null), [conflict, setConflict] = useState(false)
  const [pending, setPending] = useState<(() => void) | null>(null), [offline, setOffline] = useState(!navigator.onLine)
  const [planningDirty, setPlanningDirty] = useState(false)
  const locks = useRef({ dirty: false, planning: false, busy: false, attempt: false })
  const uncertain = useRef(false)
  const active = useRef(false), writer = useRef<AbortController | null>(null)
  const authorityGeneration = useRef(0), editorOwner = useRef<string | null>(null)
  const authorityReady = useRef(false)
  const [authorityAvailable, setAuthorityAvailable] = useState(false)
  const markAuthority = (value: boolean) => { authorityReady.current = value; setAuthorityAvailable(value) }
  const [authorityRetry, retryAuthority] = useState(0), [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const markDirty = (value: boolean) => { locks.current.dirty = value; setDirty(value) }
  const markPlanning = useCallback((value: boolean) => { locks.current.planning = value; setPlanningDirty(value) }, [])
  const markAttempt = (value: TugAttempt | null) => { locks.current.attempt = !!value; setAttempt(value) }
  const hasRetained = () => locks.current.dirty || locks.current.planning || locks.current.attempt
  const guard = (action: () => void) => { if (locks.current.busy) return; if (hasRetained()) setPending(() => action); else action() }
  const move = (href: string) => guard(() => { history.pushState(null, '', href); setRoute(location()); setReload(n => n + 1) })
  const href = (screen: string) => `/app/${screen}${route.vessel ? `?vessel=${route.vessel}` : ''}`
  useEffect(() => {
    if (!['tug-job-board', 'tug-command'].includes(route.screen ?? '') || dirty || planningDirty || busy || attempt) return
    const refresh = () => { if (hasRetained() || locks.current.busy) return; setBoard(null); if (navigator.onLine) setReload(n => n + 1) }
    const timer = window.setInterval(refresh, 30000)
    window.addEventListener('focus', refresh); window.addEventListener('online', refresh); window.addEventListener('offline', refresh)
    return () => { window.clearInterval(timer); window.removeEventListener('focus', refresh); window.removeEventListener('online', refresh); window.removeEventListener('offline', refresh) }
  }, [route.screen, dirty, planningDirty, busy, attempt])
  useEffect(() => {
    const pop = () => { const target = window.location.href; if (locks.current.busy || hasRetained()) { history.pushState(null, '', `/app/${route.screen}${route.id ? '/' + route.id : ''}?${new URLSearchParams({ vessel: route.vessel, priority: route.priority, q: route.q, offset: String(route.offset) })}`); if (!locks.current.busy) setPending(() => () => { history.pushState(null, '', target); setRoute(location()); setReload(n => n + 1) }) } else { setRoute(location()); setReload(n => n + 1) } }
    const unload = (e: BeforeUnloadEvent) => { if (hasRetained() || locks.current.busy) { e.preventDefault(); e.returnValue = '' } }
    const network = () => setOffline(!navigator.onLine)
    window.addEventListener('popstate', pop); window.addEventListener('beforeunload', unload); window.addEventListener('online', network); window.addEventListener('offline', network)
    return () => { window.removeEventListener('popstate', pop); window.removeEventListener('beforeunload', unload); window.removeEventListener('online', network); window.removeEventListener('offline', network) }
  }, [dirty, planningDirty, busy, attempt, route])
  useEffect(() => { active.current = true; return () => { active.current = false; writer.current?.abort(); authorityGeneration.current++ } }, [])
  useEffect(() => {
    document.documentElement.lang = lang; document.title = `POSEIDON · ${t.title}`
  }, [lang, t.title])
  const resetScope1 = [route, reload] as const
  const [previousScope1, setPreviousScope1] = useState(resetScope1)
  // Withdraw stale state in the same render that changes the request scope.
  if (!Object.is(previousScope1[0], route) || !Object.is(previousScope1[1], reload)) {
    setPreviousScope1(resetScope1)
    authorityGeneration.current++; editorOwner.current = null; locks.current.dirty = false; locks.current.attempt = false; uncertain.current = false
    setFieldErrors({})
    markAuthority(false); setLoading(true); setError(0); setUser(null); context(null); setBoard(null); setRecord(null); setEditing(route.screen === 'tug-job-intake'); setDirty(false); setAttempt(null); setConflict(false); setInputs(emptyTugInputs())
  }
  useEffect(() => {
    const c = new AbortController(); let active = true; const timeout = setTimeout(() => c.abort(), 10000)

    async function load() {
      if (route.invalid) throw new ApiError(400)
      const s = await session(c.signal)
      if (!s.authenticated || !s.canRead || !isGuid(s.actorId) || !isGuid(s.tenantId)) throw new ApiError(403)
      if (active) { setCsrf(s.csrfToken); setUser(s) }
      if (!route.vessel) { const list = await tugApi.vessels(c.signal); if (active) setVessels(list.vessels); return }
      const v = await tugApi.context(route.vessel, c.signal, s.tenantId ?? undefined); if (!active) return; context(v); editorOwner.current = `${s.tenantId}/${s.actorId}`; markAuthority(true)
      if (route.screen === 'tug-job-board') { const b = await tugApi.board(route.vessel, route.priority, route.q, route.offset, c.signal); if (active) setBoard(b) }
      if (route.screen === 'tug-job-detail') { const r = await tugApi.detail(route.id, route.vessel, c.signal); if (active) { setRecord(r); setInputs(r.inputs) } }
    }
    load().catch(e => { if (active) setError(e instanceof ApiError ? e.status : 503) }).finally(() => { if (active) setLoading(false); clearTimeout(timeout) })
    return () => { active = false; c.abort(); clearTimeout(timeout) }
  }, [route, reload])
  // Recheck identity without replacing the retained editor or its original base version.
  useEffect(() => {
    if (!route.vessel || loading || error || !editing) return
    const c = new AbortController()
    const refresh = async () => {
      if (locks.current.busy || !editorOwner.current) return
      const generation = ++authorityGeneration.current, expected = editorOwner.current
      markAuthority(false)
      try {
        if (!navigator.onLine) return
        const signal = AbortSignal.any([c.signal, AbortSignal.timeout(10000)])
        const s = await session(signal)
        if (!s.authenticated || !s.canRead || `${s.tenantId}/${s.actorId}` !== expected) throw new ApiError(403)
        const v = await tugApi.context(route.vessel, signal, s.tenantId ?? undefined)
        if (!c.signal.aborted && generation === authorityGeneration.current) { setUser(s); setCsrf(s.csrfToken); context(v); markAuthority(true) }
      } catch { /* Retain the original editor and command, but withdraw all private projections. */ }
    }
    const timer = window.setInterval(() => { void refresh() }, 30000)
    if (authorityRetry) void refresh()
    window.addEventListener('focus', refresh); window.addEventListener('online', refresh); window.addEventListener('offline', refresh)
    return () => { c.abort(); authorityGeneration.current++; window.clearInterval(timer); window.removeEventListener('focus', refresh); window.removeEventListener('online', refresh); window.removeEventListener('offline', refresh) }
  }, [route.vessel, loading, error, editing, authorityRetry])
  const change = (key: keyof TugInputs, value: string | number | null) => { if (locks.current.busy || locks.current.attempt || !authorityReady.current || conflict) return; setInputs(x => ({ ...x, [key]: value })); markDirty(true); setNotice(''); setFieldErrors({}) }
  const save = async () => {
    if (locks.current.busy || !ctx?.canWrite || !authorityReady.current || conflict || !editorOwner.current || locks.current.planning || !navigator.onLine) return
    const next = attempt ?? { key: crypto.randomUUID(), body: { clientId: record?.id ?? crypto.randomUUID(), baseVersion: record?.version ?? null, inputs: structuredClone(inputs) }, update: !!record }
    const expectedOwner = editorOwner.current, generation = authorityGeneration.current
    markAttempt(next); locks.current.busy = true; setBusy(true); setNotice('')
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000); writer.current = c
    let sent = false, responseReceived = false
    const checkIdentity = async () => {
      const s = await session(c.signal)
      if (!s.authenticated || !s.canRead || `${s.tenantId}/${s.actorId}` !== expectedOwner) throw new ApiError(403, 'identity_boundary_changed')
      const v = await tugApi.context(route.vessel, c.signal, s.tenantId ?? undefined)
      if (!v.canWrite || generation !== authorityGeneration.current || !active.current) throw new ApiError(403, 'write_scope_changed')
      return s.csrfToken
    }
    try {
      const currentCsrf = await checkIdentity(); sent = true
      const r = await tugApi.save(route.vessel, next, currentCsrf, c.signal)
      responseReceived = true
      // A successful HTTP response is shown only to the same currently authorized editor.
      await checkIdentity()
      if (!active.current) return
      uncertain.current = false; markAttempt(null); markDirty(false); setEditing(false); setRecord(r); setInputs(r.inputs); setNotice('saved')
      history.pushState(null, '', `/app/tug-job-detail/${r.id}?vessel=${route.vessel}`); setRoute(location())
    } catch (e) {
      if (!active.current) return
      const rejected = e instanceof ApiError && [400, 401, 403, 404, 409].includes(e.status)
      // A later denial/validation response cannot erase a prior potentially committed attempt.
      uncertain.current ||= responseReceived || sent && !rejected
      if (e instanceof ApiError && [401, 403, 404].includes(e.status)) markAuthority(false)
      if (uncertain.current) setNotice('uncertain')
      else if (e instanceof ApiError && e.status === 400) { markAttempt(null); setFieldErrors(e.errors); setNotice(e.code) }
      else if (e instanceof ApiError && e.status === 409) { setConflict(true); setNotice('conflict') }
      else setNotice('uncertain')
    } finally { clearTimeout(timeout); if (writer.current === c) writer.current = null; locks.current.busy = false; if (active.current) setBusy(false) }
  }
  const formOpen = ctx?.canWrite && editing
  const pageTitle = route.screen === 'tug-job-board' ? t.board : route.screen === 'tug-job-intake' ? t.intake : route.screen === 'tug-job-detail' ? t.detail : t.title
  return <div className="product-shell enterprise-shell tug-workspace">
    <EnterpriseChrome session={!route.vessel || authorityAvailable ? user : null} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={(href, committed) => guard(() => { committed?.(); window.location.assign(href) })} title={pageTitle} mainId="tug-main" />
    <main id="tug-main" tabIndex={-1}><div className="enterprise-page-heading"><div><p className="enterprise-page-eyebrow">{t.title}</p><h1>{pageTitle}</h1><p>{lang === 'en' ? 'Requests, draft plans and current source context.' : 'Talepler, taslak planlar ve güncel kaynak bağlamı.'}</p></div>
      {!loading && !error && ctx?.canWrite && ['tug-command', 'tug-job-board'].includes(route.screen ?? '') && <Button onClick={() => move(href('tug-job-intake'))}><Plus />{lang === 'en' ? 'New request' : 'Yeni talep'}</Button>}</div><p className="tug-planning-boundary">{t.boundary}</p>{offline && <p role="alert">{t.offline}</p>}
      {loading && <p role="status">{t.loading}</p>}{error > 0 && <div role="alert"><p>{[401, 403, 404].includes(error) ? t.denied : t.unavailable}</p><button onClick={() => guard(() => setReload(n => n + 1))}>{t.retry}</button></div>}
      {!loading && !error && !route.vessel && <section><h2>{t.select}</h2>{vessels.length === 0 && <p>{t.empty}</p>}{vessels.map(v => <p key={v.id}><a href={`/app/tug-command?vessel=${v.id}`} onClick={e => { e.preventDefault(); move(`/app/tug-command?vessel=${v.id}`) }}>{v.code} · {v.name}</a></p>)}</section>}
      {!loading && !error && route.vessel && !authorityAvailable && <div role="alert"><p>{lang === 'en' ? 'Current identity and vessel access must be checked. Retained inputs and commands are hidden until the original identity is authorized again.' : 'Güncel kimlik ve araç erişimi kontrol edilmeli. Korunan girdiler ve komutlar özgün kimlik yeniden yetkilendirilene kadar gizlidir.'}</p><Button variant="outline" disabled={busy || offline} onClick={() => retryAuthority(n => n + 1)}>{lang === 'en' ? 'Recheck access' : 'Erişimi tekrar kontrol et'}</Button></div>}
      {!loading && !error && authorityAvailable && ctx && <><p>{t.context}: <strong>{ctx.vessel.code} · {ctx.vessel.name}</strong> · {ctx.mode}</p>
        <nav aria-label={t.title}>{(['tug-command', 'tug-job-board', ...(ctx.canWrite ? ['tug-job-intake'] : [])] as const).map((s, i) => <a key={s} aria-current={route.screen === s ? 'page' : undefined} href={href(s)} onClick={e => { e.preventDefault(); move(href(s)) }}>{[t.context, t.board, t.intake][i]} </a>)}</nav>
        {route.screen === 'tug-command' && <TugProfileContext context={ctx} language={lang} offline={offline} />}
        {notice && <p role="alert">{notice === 'saved' ? t.saved : notice === 'uncertain' ? t.uncertain : notice === 'conflict' ? t.conflict : `${lang === 'en' ? 'Check fields' : 'Alanları kontrol edin'} (${notice})`}</p>}
        {route.screen === 'tug-job-board' && <TugBoardView board={board} priorities={ctx.priorities} filter={{ q: route.q, priority: route.priority, offset: route.offset }} language={lang} offline={offline}
          onFilter={filter => move(tugBoardHref(route.vessel, filter))} onNavigate={move} onRefresh={() => guard(() => setReload(n => n + 1))} />}
        {route.screen === 'tug-job-detail' && record && <TugRequestSummary record={record} language={lang} editing={editing} onEdit={ctx.canWrite ? () => guard(() => setEditing(true)) : undefined} />}
        {route.screen === 'tug-job-detail' && record && !editing && <TugPlanningPanel key={`${editorOwner.current}/${record.id}`} id={record.id} vessel={route.vessel} requestVersion={record.version} csrf={csrf} language={lang} onDirty={markPlanning} />}
        {route.screen === 'tug-job-intake' && !ctx.canWrite && <p role="alert">{t.denied}</p>}
        {formOpen && <form className="tug-intake-form" noValidate aria-busy={busy} onSubmit={e => { e.preventDefault(); void save() }} aria-label={t.intake}>
          <TugRequestFields inputs={inputs} context={ctx} language={lang} disabled={busy || !!attempt || conflict} errors={fieldErrors} onChange={change} />
          <EnterpriseActions aria-label={lang === 'en' ? 'Request actions' : 'Talep işlemleri'}>
            <Button type="button" variant="outline" disabled={busy} onClick={() => guard(() => { markDirty(false); markAttempt(null); uncertain.current = false; setReload(n => n + 1) })}>{t.discard}</Button>
            <Button type="submit" disabled={busy || offline || conflict}>{busy ? (lang === 'en' ? 'Saving…' : 'Kaydediliyor…') : attempt ? t.retry : t.save}</Button>
          </EnterpriseActions>
        </form>}
      </>}
      {pending && <DraftConfirmation language={lang} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); markDirty(false); markAttempt(null); uncertain.current = false; action() }} />}
    </main></div>
}
