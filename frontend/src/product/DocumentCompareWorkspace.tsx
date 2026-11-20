import { localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterprisePageHeader } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { ApiError, session as readSession, vessels as readVessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { compareRevisions, freezeS9Command, s9Api, s9Failure, sendS9Command, type ContentAccess, type ControlledDocument, type ControlledDocumentList,
  type ControlledDocumentRevision, type S9Issue } from './s9-wpc-api'
import { s9Copy, s9Label, type S9Copy } from './VoiceEventDraftWorkspace'
import './technical-records.css'

// DEV-114 / T-DOC2-001 (18.01 row 115; 58.02 Rev4 §33 CMP-001..T20). A read-only, side-by-side view of
// two revisions of the same controlled document. No publish/approval command, no AI/OCR diff; a hash
// match states byte equality only and is never a content review. Content is loaded per panel only
// through the classification-aware access record; nothing is cached, exported or kept offline.
export type CompareRoute = { vessel: string | null; document: string | null }
export function readCompareRoute(location: { pathname: string; search: string }): CompareRoute {
  const match = /^\/app\/document-compare(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(location.pathname)
  const document = new URLSearchParams(location.search).get('document')
  const vessel = match?.[1] && isGuid(match[1].toLowerCase()) ? match[1].toLowerCase() : null
  return { vessel, document: vessel && document && isGuid(document.toLowerCase()) ? document.toLowerCase() : null }
}
const href = (route: CompareRoute) => '/app/document-compare' + (route.vessel ? '/' + route.vessel + (route.document ? '?document=' + route.document : '') : '')

export function DocumentCompareWorkspace() {
  const [language, setLanguage] = useAppLanguage()
  const [route, setRoute] = useState(() => readCompareRoute(window.location))
  useEffect(() => { const pop = () => setRoute(readCompareRoute(window.location)); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  const navigate = useCallback((target: string) => {
    if (target !== '/app' && !target.startsWith('/app/') || /[\\\p{Cc}]/u.test(target)) return
    if (/^\/app\/document-compare(?:\/|$|\?)/.test(target)) { window.history.pushState(null, '', target); setRoute(readCompareRoute(window.location)) }
    else window.location.assign(target)
  }, [])
  return <CompareSurface key={(route.vessel ?? 'fleet') + ':' + (route.document ?? '')} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate}/>
}

type Loaded = { user: Session; vessels?: Vessel[]; list?: ControlledDocumentList; document?: ControlledDocument }

function CompareSurface({ route, language, onLanguage, onNavigate }: { route: CompareRoute; language: AppLocale; onLanguage: (value: AppLocale) => void; onNavigate: (href: string) => void }) {
  const t = s9Copy[language]
  const title = localizedText(language, 'pairs.m_9a7c51e0d3b2f462')
  const [loaded, setLoaded] = useState<Loaded | null>(null), [failed, setFailed] = useState<ApiError | null>(null), [attempt, setAttempt] = useState(0)
  const [offline, setOffline] = useState(typeof navigator !== 'undefined' && navigator.onLine === false)
  useEffect(() => { const on = () => setOffline(false), off = () => setOffline(true); window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) } }, [])
  useEffect(() => {
    const controller = new AbortController()
    ;(async () => {
      const user = await readSession(controller.signal)
      if (!route.vessel) return { user, vessels: (await readVessels(0, '', controller.signal)).items } as Loaded
      const list = await s9Api.documents(route.vessel, controller.signal)
      const document = route.document ? await s9Api.document(route.vessel, route.document, controller.signal) : undefined
      return { user, list, document } as Loaded
    })().then(value => { if (!controller.signal.aborted) { setFailed(null); setLoaded(value) } })
      .catch(error => { if (!controller.signal.aborted) setFailed(error instanceof ApiError ? error : new ApiError(0, 'network')) })
    return () => controller.abort()
  }, [route.vessel, route.document, attempt])
  const reload = () => { setFailed(null); setAttempt(n => n + 1) }

  return <div className="product-shell enterprise-shell technical-workspace">
    <EnterpriseChrome session={loaded?.user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="compare-main"/>
    <main id="compare-main" tabIndex={-1} data-testid="document-compare">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span className="technical-note">{t.dcNotice}</span>}/>
      {offline && <div className="technical-banner" role="status" data-issue="offline"><p>{t.dcOffline}</p></div>}
      {failed ? <div role="alert" className="technical-banner"><p>{failed.status === 403 ? t.issue_forbidden : failed.status === 404 ? t.dcUnavailable : failed.status === 0 ? t.issue_offline : t.issue_error}</p>
          <Button variant="outline" onClick={reload}>{t.reload}</Button></div>
        : !loaded ? <p role="status">{t.loading}</p>
        : !route.vessel ? <section className="technical-panel"><p>{t.chooseVessel}</p>{!loaded.vessels?.length ? <p role="status">{t.noVessels}</p> :
            <ul className="technical-list">{loaded.vessels.map(v => <li key={v.id}><Button variant="link" onClick={() => onNavigate('/app/document-compare/' + v.id)}>{v.code} · {v.name}</Button></li>)}</ul>}</section>
        : <div className="technical-layout">
            <section className="technical-panel" aria-label={t.dcDocuments}>{!loaded.list?.items.length ? <p role="status">{t.dcEmpty}</p> :
              <ul className="technical-list">{loaded.list.items.map(d => <li key={d.id} aria-current={d.id === route.document ? 'true' : undefined}>
                <Button variant="link" onClick={() => onNavigate(href({ vessel: route.vessel, document: d.id }))}>{d.code} · {d.title}</Button>
                <Badge tone="neutral">{s9Label(t, 'cl_', d.classification)}</Badge>{d.ispsRestricted && <Badge tone="warning">{t.dcIsps}</Badge>}</li>)}</ul>}</section>
            {loaded.document && <ComparePanel key={loaded.document.version} document={loaded.document} vessel={route.vessel} csrf={loaded.user.csrfToken ?? ''} t={t} offline={offline} onReload={reload}/>}
          </div>}
    </main>
  </div>
}

type Panel = { state: 'IDLE' | 'LOADING' | 'AVAILABLE' | 'UNAVAILABLE'; url?: string; mime?: string; access?: ContentAccess }

function ComparePanel({ document, vessel, csrf, t, offline, onReload }: { document: ControlledDocument; vessel: string; csrf: string; t: S9Copy; offline: boolean; onReload: () => void }) {
  const revisions = document.revisions
  const latest = revisions.at(-1)?.revision
  // Initial proposal only: right = latest, left = the one before it; no imaginary empty base.
  const [left, setLeft] = useState<number | ''>(revisions.length > 1 ? revisions[revisions.length - 2].revision : '')
  const [right, setRight] = useState<number | ''>(latest ?? '')
  const [compared, setCompared] = useState<{ left: number; right: number } | null>(null)
  const [panels, setPanels] = useState<Record<'left' | 'right', Panel>>({ left: { state: 'IDLE' }, right: { state: 'IDLE' } })
  const [issue, setIssue] = useState<S9Issue>(null), [statement, setStatement] = useState(false), [busy, setBusy] = useState(false)
  const generation = useRef(0)
  const urls = useRef<string[]>([])
  const revoke = () => { urls.current.forEach(u => URL.revokeObjectURL(u)); urls.current = [] }
  useEffect(() => () => revoke(), [])
  useEffect(() => { if (offline) { generation.current++; revoke(); setPanels({ left: { state: 'IDLE' }, right: { state: 'IDLE' } }) } }, [offline])
  const pick = (n: number | '') => revisions.find(r => r.revision === n)
  const l = compared ? pick(compared.left) : undefined, r = compared ? pick(compared.right) : undefined
  const comparison = l && r ? compareRevisions(l, r) : null
  const policy = document.contentPolicy!
  const compare = () => {
    if (left === '' || right === '') return
    generation.current++; revoke(); setPanels({ left: { state: 'IDLE' }, right: { state: 'IDLE' } }); setCompared({ left, right })
  }
  const load = async (side: 'left' | 'right', revision: ControlledDocumentRevision) => {
    const fence = generation.current
    setPanels(p => ({ ...p, [side]: { state: 'LOADING' } }))
    try {
      const { access, blob } = await s9Api.content(vessel, document.id, revision.revision, csrf, new AbortController().signal)
      if (fence !== generation.current) return // a late answer for an older pair is dropped
      const url = URL.createObjectURL(blob); urls.current.push(url)
      setPanels(p => ({ ...p, [side]: { state: 'AVAILABLE', url, mime: access.mime, access } }))
    } catch (error) {
      if (fence !== generation.current) return
      setIssue(s9Failure(error)); setPanels(p => ({ ...p, [side]: { state: 'UNAVAILABLE' } }))
    }
  }
  const acknowledge = async (revision: ControlledDocumentRevision) => {
    setBusy(true); setIssue(null)
    try {
      await sendS9Command(freezeS9Command('POST', s9Api.acknowledgePath(vessel, document.id, revision.revision), { displayedContentSha256: revision.contentSha256, statementAccepted: true }, document.version),
        csrf, new AbortController().signal)
      onReload()
    } catch (error) { setIssue(s9Failure(error)) } finally { setBusy(false) }
  }
  const option = (rev: ControlledDocumentRevision) => <option key={rev.revision} value={rev.revision}>
    r{rev.revision} · {s9Label(t, 'ps_', rev.publicationState)} · {s9Label(t, 'is_', rev.intakeState)}{rev.revision === document.currentRevision ? ' · ' + t.dcCurrent : ''}</option>
  const panel = (side: 'left' | 'right', revision: ControlledDocumentRevision) => {
    const p = panels[side]
    const history = document.custody.filter(c => c.revision === revision.revision)
    return <section className="technical-panel" aria-label={(side === 'left' ? t.dcLeft : t.dcRight) + ' r' + revision.revision} data-testid={'compare-' + side}>
      <h3>{side === 'left' ? t.dcLeft : t.dcRight} · r{revision.revision}</h3>
      <div className="technical-status"><Badge tone="neutral">{s9Label(t, 'ps_', revision.publicationState)}</Badge>
        <Badge tone={revision.verificationState === 'VALID' ? 'neutral' : 'warning'}>{s9Label(t, 'ver_', revision.verificationState)}</Badge>
        {revision.publicationState !== 'EFFECTIVE' && <Badge tone="warning">{t.dcNotOperational}</Badge>}</div>
      <h4>{t.dcHistory}</h4>
      <ol>{history.map(c => <li key={c.position}>{s9Label(t, 'ev_', c.eventType)} · {new Date(c.occurredAt).toISOString().slice(0, 16).replace('T', ' ')}Z</li>)}
        {revision.verifications.map(v => <li key={v.id}>{t.dcVerification.replace('{result}', v.result).replace('{method}', v.method)}</li>)}</ol>
      {policy.download !== 'ALLOWED' ? <p role="status" data-testid={'restricted-' + side}>{t.dcRestricted}</p>
        : p.state === 'AVAILABLE' && p.url ? <div className="technical-viewer" data-testid={'viewer-' + side}>
            <p className="technical-note" data-testid={'watermark-' + side} aria-label={t.dcWatermark}>{p.access?.attribution}</p>
            {p.mime === 'application/pdf' ? <object data={p.url} type="application/pdf" aria-label={'r' + revision.revision} width="100%" height="480"/>
              : <img src={p.url} alt={'r' + revision.revision} style={{ maxWidth: '100%' }}/>}
          </div>
        : <Button variant="outline" disabled={offline || p.state === 'LOADING'} onClick={() => load(side, revision)}>{p.state === 'LOADING' ? t.loading : t.dcLoad}</Button>}
      {revision.acknowledgedByMeAt ? <p role="status">{t.dcAcknowledged.replace('{at}', revision.acknowledgedByMeAt.slice(0, 16).replace('T', ' '))}</p>
        : revision.canAcknowledge && <div><label><input type="checkbox" checked={statement} onChange={e => setStatement(e.target.checked)}/> {t.dcAckStatement}</label>
            <Button disabled={busy || offline || !statement} onClick={() => acknowledge(revision)}>{t.dcAcknowledge}</Button></div>}
    </section>
  }
  return <section className="technical-panel" aria-labelledby="compare-title">
    <h2 id="compare-title">{document.code} · {document.title}</h2>
    <div className="technical-status"><Badge tone="neutral">{s9Label(t, 'cl_', document.classification)}</Badge>{document.ispsRestricted && <Badge tone="warning">{t.dcIsps}</Badge>}</div>
    <p className="technical-note" data-testid="content-policy">{s9Label(t, 'dl_', policy.download)} · {s9Label(t, 'pr_', policy.print)}</p>
    <Banner issue={issue} t={t}/>
    {revisions.length === 0 ? <p role="status">{t.dcNoRevisions}</p> : <form aria-label={t.dcSelect} onSubmit={e => { e.preventDefault(); compare() }}>
      <label className="technical-field"><span>{t.dcLeft}</span><select value={left} onChange={e => setLeft(e.target.value === '' ? '' : Number(e.target.value))}>
        <option value="">{t.notSet}</option>{revisions.map(option)}</select></label>
      <label className="technical-field"><span>{t.dcRight}</span><select value={right} onChange={e => setRight(e.target.value === '' ? '' : Number(e.target.value))}>
        <option value="">{t.notSet}</option>{revisions.map(option)}</select></label>
      <Button type="submit" disabled={left === '' || right === '' || offline}>{t.dcCompare}</Button>
      <Button type="button" variant="outline" onClick={() => { setLeft(right); setRight(left); setCompared(null) }}>{t.dcSwap}</Button>
    </form>}
    {comparison && l && r && <>
      {comparison.state === 'SAME_REVISION' ? <p role="status">{t.dcSameRevision}</p> : <>
        <p role="status" data-testid="byte-statement">{comparison.bytes === 'BYTE_IDENTICAL' ? t.dcBytesSame : t.dcBytesDifferent}</p>
        <table className="technical-table" aria-label={t.dcMetadata}><thead><tr><th>{t.dcField}</th><th>{t.dcLeft} r{l.revision}</th><th>{t.dcRight} r{r.revision}</th><th>{t.dcStatus}</th></tr></thead>
          <tbody>{comparison.rows.map(row => <tr key={row.field} data-status={row.status}><td>{s9Label(t, 'mf_', row.field)}</td><td>{row.left ?? '—'}</td><td>{row.right ?? '—'}</td>
            <td>{s9Label(t, 'cs_', row.status)}</td></tr>)}</tbody></table></>}
      <div className="technical-layout">{panel('left', l)}{comparison.state !== 'SAME_REVISION' && panel('right', r)}</div>
    </>}
  </section>
}

function Banner({ issue, t }: { issue: S9Issue; t: S9Copy }) {
  if (!issue) return null
  return <div className={`technical-banner technical-banner-${issue}`} role="alert" data-issue={issue}><p>{(t as Record<string, string>)['issue_' + issue]}</p></div>
}
