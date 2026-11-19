import { localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { ApiError, session as readSession, type Session } from './api'
import { isGuid } from './identifiers'
import { decodeRiskTemplate, emptyTemplate, failureKind, freezeRiskCommand, riskApi, riskWorkTypes, sendRiskCommand, splitList, textOrNull,
  type RiskAttempt, type RiskMethod, type RiskMethodContent, type RiskMethodList, type RiskTemplateContent, type RiskTemplateDetail, type RiskTemplatePage } from './risk-api'
import { RiskBanner, RiskField, RiskReason, RiskState, reasonValid, riskCopy, type RiskCopy, type RiskIssue } from './RiskWorkspaceParts'
import './risk-workspace.css'

// DEV-106 / T-RISK2-002 risk-template-library screen (18.01 row 107; 50.01 Rev3 §3-4).
// Publication is a separate HSEQ human record; superseded revisions stay listed.
export function readTemplateRoute(location: { pathname: string; search: string }) {
  if (!/^\/app\/risk-template-library\/?$/.test(location.pathname)) return { id: null as string | null, invalid: true }
  const id = new URLSearchParams(location.search).get('id')
  return { id: id && isGuid(id.toLowerCase()) ? id.toLowerCase() : null, invalid: false }
}

type Loaded = { user: Session; page: RiskTemplatePage; detail?: RiskTemplateDetail; methods: RiskMethodList | null }
type Draft = { id: string; code: string; revision: number | null; version: number | null; content: RiskTemplateContent }

export function RiskTemplateLibraryWorkspace() {
  const [language, setLanguage] = useAppLanguage()
  const [route, setRoute] = useState(() => readTemplateRoute(window.location))
  useEffect(() => { const pop = () => setRoute(readTemplateRoute(window.location)); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  const navigate = useCallback((target: string) => {
    if (target !== '/app' && !target.startsWith('/app/') || /[\\\p{Cc}]/u.test(target)) return
    if (/^\/app\/risk-template-library(?:\/|$|\?)/.test(target)) { window.history.pushState(null, '', target); setRoute(readTemplateRoute(window.location)) }
    else window.location.assign(target)
  }, [])
  return <TemplateSurface route={route} language={language} onLanguage={setLanguage} onNavigate={navigate}/>
}

function TemplateSurface({ route, language, onLanguage, onNavigate }: { route: { id: string | null; invalid: boolean }; language: AppLocale; onLanguage: (v: AppLocale) => void; onNavigate: (href: string) => void }) {
  const t = riskCopy[language], title = localizedText(language, 'pairs.m_c086860d120fa851')
  const [loaded, setLoaded] = useState<Loaded | null>(null), [failed, setFailed] = useState<ApiError | null>(null), [attempt, setAttempt] = useState(0)
  const [draft, setDraft] = useState<Draft | null>(null), [methodDraft, setMethodDraft] = useState(false), [issue, setIssue] = useState<RiskIssue>(null)
  const [busy, setBusy] = useState(false), [pending, setPending] = useState<RiskAttempt | null>(null), [offline, setOffline] = useState(typeof navigator !== 'undefined' && navigator.onLine === false)
  useEffect(() => { const on = () => setOffline(false), off = () => setOffline(true); window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) } }, [])
  useEffect(() => {
    const controller = new AbortController()
    ;(async () => {
      const [user, page, methods] = await Promise.all([readSession(controller.signal), riskApi.templates(0, controller.signal), riskApi.methods(controller.signal).catch(() => null)])
      const detail = route.id ? await riskApi.template(route.id, controller.signal) : undefined
      return { user, page, detail, methods }
    })().then(value => { if (!controller.signal.aborted) { setFailed(null); setLoaded(value) } })
      .catch(error => { if (!controller.signal.aborted) setFailed(error instanceof ApiError ? error : new ApiError(0, 'network')) })
    return () => controller.abort()
  }, [route.id, attempt])
  const reload = () => { setFailed(null); setIssue(null); setPending(null); setDraft(null); setMethodDraft(false); setAttempt(n => n + 1) }
  const execute = async (frozen: RiskAttempt, method = false) => {
    if (!loaded?.user.csrfToken) return
    setBusy(true); setPending(frozen); setIssue(null)
    try {
      const saved = await sendRiskCommand<unknown>(frozen, loaded.user.csrfToken, new AbortController().signal)
      setPending(null); setDraft(null); setMethodDraft(false)
      if (!method) { const detail = decodeRiskTemplate(saved); onNavigate('/app/risk-template-library?id=' + detail.id) }
      setAttempt(n => n + 1)
    } catch (error) { const kind = failureKind(error); setIssue(kind); if (kind !== 'offline') setPending(null) }
    finally { setBusy(false) }
  }
  const command = (method: 'POST' | 'PUT', path: string, body: unknown, isMethod = false) => execute(freezeRiskCommand(method, path, body, 'tenant'), isMethod)
  const detail = loaded?.detail
  return <>
    <EnterpriseChrome session={loaded?.user ?? null} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="risk-template-main"/>
    <main id="risk-template-main" tabIndex={-1} className="risk-workspace">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span className="risk-gate-notice">{t.gateNotice}</span>}
        actions={loaded?.page.canWrite && !draft && !offline ? <Button onClick={() => setDraft({ id: crypto.randomUUID(), code: '', revision: null, version: null, content: emptyTemplate() })}>{t.newTemplate}</Button> : undefined}/>
      {offline && <RiskBanner issue="offline" t={t}/>}
      {pending && issue === 'offline' && <Button variant="outline" disabled={busy || offline} onClick={() => execute(pending)}>{t.retry}</Button>}
      <RiskBanner issue={issue === 'offline' ? null : issue} t={t} onReload={reload}/>
      {route.invalid ? <p role="alert">{t.error}</p>
        : failed ? <div role="alert" className="risk-banner risk-banner-error"><p>{failed.status === 0 ? t.offline : t.error}</p><Button variant="outline" onClick={reload}>{t.retry}</Button></div>
        : !loaded ? <p role="status">{t.loading}</p>
        : draft ? <TemplateEditor draft={draft} t={t} busy={busy || offline} onCancel={() => setDraft(null)} onSave={next => next.revision === null
            ? command('POST', 'hse/risk-templates', { id: next.id, templateCode: next.code, content: next.content })
            : command('PUT', 'hse/risk-templates/' + next.id, { revision: next.revision, expectedVersion: next.version, content: next.content })}/>
        : <div className="risk-layout">
            <section className="risk-panel">{loaded.page.items.length === 0 ? <p role="status">{t.templateEmpty}</p> : <ul className="risk-list">{loaded.page.items.map(item =>
              <li key={item.id} aria-current={item.id === route.id ? 'true' : undefined}><Button variant="link" onClick={() => onNavigate('/app/risk-template-library?id=' + item.id)}>{item.templateCode} · {item.title ?? t.unknown}</Button>
                <RiskState value={item.latestStatus} t={t}/> {item.currentRevision ? <span className="risk-badge risk-badge-recorded">{t.st_CURRENT} r{item.currentRevision}</span> : null}
                <span> {t.suggestionCount.replace('{{count}}', String(item.suggestionCount))}</span></li>)}</ul>}</section>
            {detail && <TemplateDetail detail={detail} t={t} busy={busy || offline} onEdit={revision => setDraft({ id: detail.id, code: detail.templateCode, revision: revision.revision, version: revision.version, content: structuredClone(revision.content) })}
              onCommand={(suffix, body) => command('POST', 'hse/risk-templates/' + detail.id + suffix, body)}/>}
          </div>}
      {loaded && !draft && <MethodsPanel methods={loaded.methods} t={t} busy={busy || offline} open={methodDraft} onOpen={setMethodDraft}
        onCreate={content => command('POST', 'hse/risk-methods', { id: crypto.randomUUID(), content }, true)}
        onPublish={(m, reason) => command('POST', 'hse/risk-methods/' + m.id + '/publish', { expectedVersion: m.version, contentHash: m.hash, reason }, true)}/>}
    </main>
  </>
}

function TemplateDetail({ detail, t, busy, onEdit, onCommand }: { detail: RiskTemplateDetail; t: RiskCopy; busy: boolean; onEdit: (revision: RiskTemplateDetail['revisions'][number]) => void;
  onCommand: (suffix: string, body: unknown) => void }) {
  const [reason, setReason] = useState(''), [effective, setEffective] = useState(''), [suggestion, setSuggestion] = useState('')
  const latest = detail.revisions.find(r => r.revision === detail.latestRevision)!
  const publishedTarget = detail.revisions.find(r => r.effectiveState === 'CURRENT') ?? detail.revisions.find(r => r.status === 'PUBLISHED' || r.status === 'SUPERSEDED')
  const effectiveIso = effective ? new Date(effective).toISOString() : null
  return <section className="risk-panel risk-detail" aria-labelledby="risk-template-title">
    <h2 id="risk-template-title">{detail.templateCode}</h2>
    <p>{t.suggestionCount.replace('{{count}}', String(detail.suggestionCount))}</p>
    {detail.revisions.map(r => <article key={r.revision} className="risk-revision" data-status={r.status}>
      <h3>{t.revision} {r.revision} · <RiskState value={r.status} t={t}/> <RiskState value={r.effectiveState} t={t}/></h3>
      <dl className="risk-facts"><dt>{t.fieldTitle}</dt><dd>{r.content.title ?? t.unknown}</dd><dt>{t.workType}</dt><dd>{r.content.workType ?? t.unknown}</dd>
        <dt>{t.vesselKinds}</dt><dd>{r.content.vesselKinds.join(', ') || t.unknown}</dd><dt>{t.approvalOwner}</dt><dd>{r.content.approvalOwner ?? t.unknown}</dd>
        <dt>{t.effectiveFrom}</dt><dd>{r.effectiveFrom ?? t.unknown}</dd>{r.content.changeReason && <><dt>{t.changeReason}</dt><dd>{r.content.changeReason}</dd></>}</dl>
      <ol>{r.content.hazards.map(h => <li key={h.id}>{h.description}<ul>{h.defaultControls.map(x => <li key={x.id}>{x.description}</li>)}</ul></li>)}</ol>
      {r.blockers.length > 0 && ['DRAFT', 'RETURNED'].includes(r.status) && <ul aria-label={t.blockers}>{r.blockers.map(b => <li key={b}><code>{b}</code></li>)}</ul>}
      <EnterpriseActions>
        {r.canEdit && <Button variant="outline" disabled={busy} onClick={() => onEdit(r)}>{t.edit}</Button>}
        {r.canSubmit && <Button disabled={busy} onClick={() => onCommand('/submit', { revision: r.revision, expectedVersion: r.version, contentHash: r.hash })}>{t.submit}</Button>}
      </EnterpriseActions>
      {r.canPublish && <div className="risk-decision">
        <RiskField label={t.effectiveFrom}><input type="datetime-local" value={effective} onChange={e => setEffective(e.target.value)}/></RiskField>
        <RiskReason value={reason} onChange={setReason} t={t}/>
        <EnterpriseActions>
          <Button disabled={busy || !reasonValid(reason) || !effectiveIso} onClick={() => onCommand('/review', { revision: r.revision, expectedVersion: r.version, contentHash: r.hash, decision: 'PUBLISH', reason, effectiveFrom: effectiveIso })}>{t.publish}</Button>
          <Button variant="outline" disabled={busy || !reasonValid(reason)} onClick={() => onCommand('/review', { revision: r.revision, expectedVersion: r.version, contentHash: r.hash, decision: 'RETURN', reason, effectiveFrom: null })}>{t.returnAction}</Button>
        </EnterpriseActions></div>}
    </article>)}
    {detail.canRevise && <div className="risk-decision"><RiskReason value={reason} onChange={setReason} t={t}/>
      <Button variant="outline" disabled={busy || !reasonValid(reason)} onClick={() => onCommand('/revise', { baseRevision: latest.revision, expectedVersion: detail.version, reason })}>{t.revise}</Button></div>}
    <h3>{t.suggestions}</h3>
    {detail.suggestions.length === 0 ? <p>{t.empty}</p> : <ul>{detail.suggestions.map(s => <li key={s.id} data-status={s.status}>r{s.revision} · {s.text} · {s.status}
      {s.canReview && <span> <Button size="sm" variant="outline" disabled={busy || !reasonValid(reason)} onClick={() => onCommand(`/suggestions/${s.id}/review`, { expectedVersion: s.version, decision: 'ACCEPT', reason })}>{t.accept}</Button>
        <Button size="sm" variant="ghost" disabled={busy || !reasonValid(reason)} onClick={() => onCommand(`/suggestions/${s.id}/review`, { expectedVersion: s.version, decision: 'REJECT', reason })}>{t.reject}</Button></span>}</li>)}</ul>}
    {detail.canSuggest && publishedTarget && <div className="risk-decision">
      <RiskField label={t.suggest}><textarea rows={2} maxLength={4000} value={suggestion} onChange={e => setSuggestion(e.target.value)}/></RiskField>
      <Button disabled={busy || suggestion.trim().length === 0} onClick={() => onCommand('/suggestions', { id: crypto.randomUUID(), revision: publishedTarget.revision, text: suggestion })}>{t.suggest}</Button></div>}
  </section>
}

function TemplateEditor({ draft, t, busy, onCancel, onSave }: { draft: Draft; t: RiskCopy; busy: boolean; onCancel: () => void; onSave: (draft: Draft) => void }) {
  const [state, setState] = useState(draft)
  const c = state.content
  const set = (patch: Partial<RiskTemplateContent>) => setState(s => ({ ...s, content: { ...s.content, ...patch } }))
  // One hazard per line: "hazard | consequence; consequence | control; control".
  const hazardsText = c.hazards.map(h => [h.description, h.consequences.join('; '), h.defaultControls.map(x => x.description).join('; ')].join(' | ')).join('\n')
  const parseHazards = (text: string) => splitList(text).map((line, i) => {
    const [description, consequences = '', controls = ''] = line.split('|').map(x => x.trim()), previous = c.hazards[i]
    return { id: previous?.id ?? crypto.randomUUID(), step: previous?.step ?? null, description, consequences: consequences.split(';').map(x => x.trim()).filter(Boolean),
      defaultControls: controls.split(';').map(x => x.trim()).filter(Boolean).map((x, j) => ({ id: previous?.defaultControls[j]?.id ?? crypto.randomUUID(), description: x, kind: 'EXISTING', ownerRole: null, required: false, barrierRefs: [] })),
      barrierRefs: previous?.barrierRefs ?? [] }
  })
  const valid = (state.revision !== null || /^[A-Za-z0-9._-]{1,50}$/.test(state.code)) && c.hazards.every(h => h.description)
  return <section className="risk-panel risk-editor" aria-label={t.edit}>
    {state.revision === null && <RiskField label={t.templateCode}><input value={state.code} maxLength={50} required onChange={e => setState(s => ({ ...s, code: e.target.value }))}/></RiskField>}
    <RiskField label={t.fieldTitle}><input value={c.title ?? ''} maxLength={200} onChange={e => set({ title: textOrNull(e.target.value) })}/></RiskField>
    <RiskField label={t.workType}><select value={c.workType ?? ''} onChange={e => set({ workType: textOrNull(e.target.value) })}><option value="">{t.unknown}</option>{riskWorkTypes.map(w => <option key={w}>{w}</option>)}</select></RiskField>
    <fieldset><legend>{t.vesselKinds}</legend>{['SHIP', 'TUG'].map(k => <label key={k} className="risk-check"><input type="checkbox" checked={c.vesselKinds.includes(k)}
      onChange={e => set({ vesselKinds: e.target.checked ? [...c.vesselKinds, k] : c.vesselKinds.filter(x => x !== k) })}/>{k}</label>)}</fieldset>
    <RiskField label={t.approvalOwner}><input value={c.approvalOwner ?? ''} maxLength={200} onChange={e => set({ approvalOwner: textOrNull(e.target.value) })}/></RiskField>
    <RiskField label={t.changeReason}><textarea rows={2} value={c.changeReason ?? ''} maxLength={1000} onChange={e => set({ changeReason: textOrNull(e.target.value) })}/></RiskField>
    <RiskField label={t.requiredBarriers}><textarea rows={2} value={c.requiredBarriers.join('\n')} onChange={e => set({ requiredBarriers: splitList(e.target.value) })}/></RiskField>
    <RiskField label={t.sourceRefs}><textarea rows={2} value={c.sourceRefs.join('\n')} onChange={e => set({ sourceRefs: splitList(e.target.value) })}/></RiskField>
    <RiskField label={t.hazards} hint="hazard | consequence; … | control; …"><textarea rows={5} defaultValue={hazardsText} onBlur={e => set({ hazards: parseHazards(e.target.value) })}/></RiskField>
    <EnterpriseActions><Button variant="outline" disabled={busy} onClick={onCancel}>{t.cancel}</Button><Button disabled={busy || !valid} onClick={() => onSave(state)}>{t.save}</Button></EnterpriseActions>
  </section>
}

// Matrix configuration: no default is offered. Lines are "CODE label" and cells "L S RESULT RANK".
export function parseMethod(code: string, title: string, likelihoods: string, severities: string, cells: string, sources: string, reason: string): RiskMethodContent {
  const codes = (text: string) => splitList(text).map(line => { const [first, ...rest] = line.split(/\s+/); return { code: first, label: rest.join(' ') || first } })
  return { code: code.trim(), title: title.trim(), likelihoods: codes(likelihoods), severities: codes(severities), sourceRefs: splitList(sources), changeReason: reason,
    cells: splitList(cells).map(line => { const [likelihood = '', severity = '', result = '', rank = ''] = line.split(/\s+/); return { likelihood, severity, result, rank: Number.parseInt(rank, 10) } }) }
}
function MethodsPanel({ methods, t, busy, open, onOpen, onCreate, onPublish }: { methods: RiskMethodList | null; t: RiskCopy; busy: boolean; open: boolean; onOpen: (open: boolean) => void;
  onCreate: (content: RiskMethodContent) => void; onPublish: (method: RiskMethod, reason: string) => void }) {
  const [form, setForm] = useState({ code: '', title: '', likelihoods: '', severities: '', cells: '', sources: '', reason: '' }), [reason, setReason] = useState('')
  if (!methods) return null
  const content = parseMethod(form.code, form.title, form.likelihoods, form.severities, form.cells, form.sources, form.reason)
  const complete = content.code && content.title && content.likelihoods.length >= 2 && content.severities.length >= 2 && content.sourceRefs.length > 0 && reasonValid(form.reason) &&
    content.cells.length === content.likelihoods.length * content.severities.length && content.cells.every(c => Number.isInteger(c.rank) && c.rank >= 0 && c.result)
  return <section className="risk-panel" aria-labelledby="risk-methods-title"><h2 id="risk-methods-title">{t.methods}</h2>
    {methods.items.length === 0 ? <p className="risk-note">{t.methodUnconfigured}</p> : <ul>{methods.items.map(m => <li key={m.id}>{m.code} r{m.revision} · {m.content.title} · <RiskState value={m.status} t={t}/>
      {m.canPublish && <span> <Button size="sm" disabled={busy || !reasonValid(reason)} onClick={() => onPublish(m, reason)}>{t.publishMethod}</Button></span>}</li>)}</ul>}
    {methods.items.some(m => m.canPublish) && <RiskReason value={reason} onChange={setReason} t={t}/>}
    {methods.canWrite && !open && <Button variant="outline" disabled={busy} onClick={() => onOpen(true)}>{t.newMethod}</Button>}
    {open && <div className="risk-editor">
      <RiskField label={t.methodCode}><input value={form.code} maxLength={40} onChange={e => setForm({ ...form, code: e.target.value })}/></RiskField>
      <RiskField label={t.fieldTitle}><input value={form.title} maxLength={200} onChange={e => setForm({ ...form, title: e.target.value })}/></RiskField>
      <RiskField label={t.likelihoodCodes}><textarea rows={3} value={form.likelihoods} onChange={e => setForm({ ...form, likelihoods: e.target.value })}/></RiskField>
      <RiskField label={t.severityCodes}><textarea rows={3} value={form.severities} onChange={e => setForm({ ...form, severities: e.target.value })}/></RiskField>
      <RiskField label={t.matrixCells}><textarea rows={5} value={form.cells} onChange={e => setForm({ ...form, cells: e.target.value })}/></RiskField>
      <RiskField label={t.sourceRefs}><textarea rows={2} value={form.sources} onChange={e => setForm({ ...form, sources: e.target.value })}/></RiskField>
      <RiskField label={t.changeReason}><textarea rows={2} value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })}/></RiskField>
      <EnterpriseActions><Button variant="outline" onClick={() => onOpen(false)}>{t.cancel}</Button><Button disabled={busy || !complete} onClick={() => onCreate(content)}>{t.save}</Button></EnterpriseActions>
    </div>}
  </section>
}
