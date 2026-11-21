import { useCallback, useEffect, useRef, useState } from 'react'
import { localizedCopy, localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { ApiError, session as readSession, vessels as readVessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { send, type Command } from './tug-readiness-api'
import { closeOutcomes, regulatoryReviewApi, reviewAuthorities, reviewOwnerRoles, reviewScopes, reviewVerdicts, type ReviewDetail, type ReviewItem,
  type ReviewList, type ReviewScope, type ReviewVerdict } from './regulatory-review-api'

// S11 WP-C DEV-073 (pilot regulatory SME review) and DEV-093 (tug source/applicability review). Ship/Tug tabs, the source
// ownership matrix and the open-item count (red while any open item is untracked, amber while items stay open). A confirmed
// item is never shown with a "go" tone; the review never activates a rule or permit and never claims class/type approval.
const copy = localizedCopy('RegulatoryReview_tsx_copy')
type Copy = (typeof copy)[AppLocale]
type Notice = '' | 'done' | 'uncertain' | 'conflict' | 'refused' | 'denied' | 'failed'
const word = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key

export type ReviewRoute = { vessel: string | null; scope: ReviewScope; invalid: boolean }
export function readReviewRoute(href: string): ReviewRoute {
  const url = new URL(href, 'http://local'), params = url.searchParams
  const match = /^\/app\/regulatory-review(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(url.pathname)
  const vessel = match?.[1]?.toLowerCase() ?? null
  const scope = params.get('scope') ?? 'SHIP'
  const invalid = !match || !!match[1] && !isGuid(vessel) || [...params.keys()].some(k => k !== 'scope' || params.getAll(k).length !== 1) ||
    !(reviewScopes as readonly string[]).includes(scope)
  return { vessel: vessel && isGuid(vessel) ? vessel : null, scope: (reviewScopes as readonly string[]).includes(scope) ? scope as ReviewScope : 'SHIP', invalid }
}
export const reviewHref = (scope: ReviewScope, vessel?: string) => `/app/regulatory-review${vessel ? `/${vessel}` : ''}?scope=${scope}`

function VerdictBadge({ verdict, t }: { verdict: ReviewVerdict; t: Copy }) {
  return <Badge tone={verdict === 'AMBIGUOUS' ? 'danger' : verdict === 'REVIEW_REQUIRED' ? 'warning' : 'info'} data-verdict={verdict}>{word(t, 'verdict_' + verdict)}</Badge>
}

export function RegulatoryReviewWorkspace() {
  const [lang, setLanguage] = useAppLanguage(), t = copy[lang]
  const title = localizedText(lang, 'pairs.m_3c8e1f5a7b2d9e40')
  const [route, setRoute] = useState(() => readReviewRoute(window.location.href))
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true), [error, setError] = useState<ApiError | null>(null), [offline, setOffline] = useState(!navigator.onLine)
  const [fleet, setFleet] = useState<Vessel[] | null>(null), [list, setList] = useState<ReviewList | null>(null), [detail, setDetail] = useState<ReviewDetail | null>(null)
  const [notice, setNotice] = useState<Notice>(''), [code, setCode] = useState(''), [busy, setBusy] = useState(false), [retained, setRetained] = useState<Command | null>(null)
  const lock = useRef(false), mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => { setRoute(readReviewRoute(window.location.href)); setReload(n => n + 1) }
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop)
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop) }
  }, [])
  const move = useCallback((href: string) => {
    if (retained || busy) return
    if (!/^\/app\/regulatory-review(?:\/|\?|$)/.test(href)) { window.location.assign(href); return }
    history.pushState(null, '', href); setRoute(readReviewRoute(window.location.href)); setNotice(''); setReload(n => n + 1)
  }, [retained, busy])
  useEffect(() => {
    const c = new AbortController()
    ;(async () => {
      await Promise.resolve()
      if (c.signal.aborted) return
      setLoading(true); setError(null); setFleet(null); setList(null); setDetail(null)
      if (route.invalid) throw new ApiError(400, 'invalid_route')
      const s = await readSession(c.signal); setUser(s)
      if (!route.vessel) { setFleet((await readVessels(0, route.scope === 'TUG' ? 'Tug' : 'Ship', c.signal)).items); return }
      const l = await regulatoryReviewApi.list(route.vessel, route.scope, c.signal); setList(l)
      const latest = l.items[0]
      if (latest) setDetail(await regulatoryReviewApi.detail(route.vessel, latest.id, c.signal))
    })().then(() => { if (!c.signal.aborted) setLoading(false) })
      .catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e : new ApiError(0, 'network')); setLoading(false) })
    return () => c.abort()
  }, [route, reload])

  // One command at a time; an uncertain outcome keeps the exact key/body for an identical retry.
  const run = useCallback(async (command: Command) => {
    if (lock.current || offline || !navigator.onLine) return
    lock.current = true; setBusy(true); setNotice(''); setCode(''); setRetained(command)
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000)
    let sent = false
    try {
      const s = await readSession(c.signal)
      if (!s.authenticated || !s.csrfToken || user && `${s.tenantId}/${s.actorId}` !== `${user.tenantId}/${user.actorId}`) throw new ApiError(403, 'identity_boundary_changed')
      sent = true
      await send(command, s.csrfToken, c.signal)
      if (!mounted.current) return
      setRetained(null); setNotice('done'); setReload(n => n + 1)
    } catch (e) {
      if (!mounted.current) return
      const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status)
      if (!sent || definite) setRetained(null)
      const failure = e instanceof ApiError ? e.code : ''
      setCode(/^[a-z0-9_]{1,64}$/.test(failure) ? failure : '')
      setNotice(sent && !definite ? 'uncertain' : failure === 'version_conflict' || failure === 'idempotency_key_reused' ? 'conflict'
        : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : e instanceof ApiError && [400, 409].includes(e.status) ? 'refused' : 'failed')
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false) }
  }, [offline, user])

  const disabled = busy || offline || !!retained && notice !== 'uncertain'
  const message = notice ? word(t, 'notice_' + notice) : ''
  return <div className="product-shell enterprise-shell regulatory-review-workspace">
    <EnterpriseChrome session={user} vessel={route.vessel} language={lang} onLanguageChange={setLanguage} onNavigate={href => move(href)} title={title} mainId="regulatory-review-main"/>
    <main id="regulatory-review-main" tabIndex={-1} aria-busy={loading || busy} data-testid="regulatory-review">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span>{t.boundary}</span>}
        actions={route.vessel ? <Button variant="outline" disabled={offline || busy} onClick={() => { setNotice(''); setReload(n => n + 1) }}>{t.refresh}</Button> : undefined}/>
      <nav aria-label={t.scopeTabs} role="tablist">{reviewScopes.map(s => <Button key={s} role="tab" aria-selected={route.scope === s} variant={route.scope === s ? 'default' : 'outline'}
        disabled={busy} onClick={() => move(reviewHref(s))}>{word(t, 'tab_' + s)}</Button>)}</nav>
      {route.scope === 'TUG' && <p data-state="tug-boundary">{t.tugBoundary}</p>}
      {offline && <p role="alert" data-state="offline">{t.offline}</p>}
      {message && <div role={notice === 'done' ? 'status' : 'alert'} data-notice={notice}><p>{message}{code && notice !== 'done' ? <> <code>{code}</code></> : null}</p>
        {notice === 'uncertain' && retained && <Button type="button" disabled={busy || offline} onClick={() => void run(retained)}>{t.retry}</Button>}
        {notice === 'conflict' && <Button type="button" variant="outline" onClick={() => { setNotice(''); setReload(n => n + 1) }}>{t.reload}</Button>}</div>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error && <div role="alert" data-state={error.status === 403 ? 'forbidden' : 'error'}>
        <p>{error.code === 'invalid_route' ? t.invalidRoute : error.status === 403 ? t.forbidden : error.status === 404 ? t.notFound : error.status === 0 ? t.offline : t.error}</p>
        {error.code !== 'invalid_route' && <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.retry}</Button>}</div>}
      {!loading && !error && fleet && <section aria-label={t.chooseVessel}><h2>{t.chooseVessel}</h2>
        {fleet.length === 0 ? <p role="status" data-state="empty">{t.noVessels}</p> :
          <ul>{fleet.map(v => <li key={v.id}><Button variant="link" onClick={() => move(reviewHref(route.scope, v.id))}>{v.code} · {v.name}</Button></li>)}</ul>}</section>}
      {!loading && !error && list && !detail && <section aria-label={t.campaign}>
        <p role="status" data-state="empty">{t.noCampaign}</p>
        {list.canManage ? <EnterpriseActions layout="form" aria-label={t.openCampaign}>
          <Button type="button" disabled={disabled} onClick={() => void run(regulatoryReviewApi.create(route.vessel!, route.scope))}>{t.openCampaign}</Button></EnterpriseActions>
          : <p data-state="read-only">{t.cannotManage}</p>}</section>}
      {!loading && !error && detail && <CampaignPanel detail={detail} t={t} disabled={disabled} run={run}/>}
    </main>
  </div>
}

function CampaignPanel({ detail, t, disabled, run }: { detail: ReviewDetail; t: Copy; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const c = detail.campaign
  const [selected, setSelected] = useState<string>('')
  const item = detail.items.find(i => i.id === selected) ?? null
  const terminal = c.state === 'CLOSED' || c.state === 'CLOSED_WITH_OPEN_ITEMS'
  const openTone = c.untrackedOpenItems > 0 ? 'danger' : c.openItems > 0 ? 'warning' : 'neutral'
  return <section aria-label={t.campaign}>
    <h2>{t.campaign}</h2>
    <p><Badge tone={terminal ? 'neutral' : 'warning'} data-state={c.state}>{word(t, 'state_' + c.state)}</Badge>{' '}
      <Badge tone={openTone} data-open-items={c.openItems}>{t.openItems}: {c.openItems}</Badge>{' '}
      <Badge tone={c.untrackedOpenItems > 0 ? 'danger' : 'neutral'} data-untracked={c.untrackedOpenItems}>{t.untrackedItems}: {c.untrackedOpenItems}</Badge>{' '}
      <Badge tone="neutral">{t.confirmedItems}: {c.confirmedItems}</Badge>{c.staleItems > 0 && <> <Badge tone="warning" data-stale-items={c.staleItems}>{t.staleItems}: {c.staleItems}</Badge></>}</p>
    <p>{t.profileRevision}: {c.profileRevision ?? '—'}{c.profileStale && <> · <Badge tone="warning" data-state="profile-stale">{t.profileStale}</Badge></>}</p>
    {c.blockers.length > 0 && <p>{t.blockers}: {c.blockers.map(b => <code key={b}>{b} </code>)}</p>}
    <h3>{t.ownerMatrix}</h3>
    <table className="regulatory-owner-matrix"><thead><tr><th scope="col">{t.colAuthority}</th><th scope="col">{t.colCategory}</th><th scope="col">{t.openItems}</th>
      <th scope="col">{t.confirmedItems}</th><th scope="col">{t.colOwners}</th></tr></thead>
      <tbody>{detail.ownerMatrix.map(r => <tr key={r.authority} data-authority={r.authority}><td><code>{r.authority}</code></td><td>{word(t, 'cat_' + r.category)}</td>
        <td><Badge tone={r.open > 0 ? 'warning' : 'neutral'}>{r.open}</Badge></td><td>{r.confirmed}</td><td>{r.ownerRoles.map(o => <code key={o}>{o} </code>)}</td></tr>)}</tbody></table>
    <h3>{t.itemsTitle}</h3>
    <table className="regulatory-review-items"><thead><tr><th scope="col">{t.colSubject}</th><th scope="col">{t.colCategory}</th><th scope="col">{t.colVerdict}</th>
      <th scope="col">{t.colIssue}</th><th scope="col">{t.blockers}</th></tr></thead>
      <tbody>{detail.items.map(i => <tr key={i.id} data-item={i.subjectKey} data-kind={i.kind}>
        <td><label><input type="radio" name="review-item" value={i.id} checked={selected === i.id} disabled={!detail.canReview || terminal} onChange={() => setSelected(i.id)}/>
          <code>{i.subjectKey}</code></label>{i.candidateUnverified && <> <Badge tone="warning">{t.candidate}</Badge></>}</td>
        <td>{word(t, 'cat_' + i.authorityCategory)}</td>
        <td><VerdictBadge verdict={i.effectiveVerdict} t={t}/>{i.stale && <> <Badge tone="warning" data-state="stale">{t.stale}</Badge></>}</td>
        <td>{i.trackedIssueId ? <code>{i.trackedIssueId}</code> : '—'}</td>
        <td>{i.blockers.map(b => <code key={b}>{b} </code>)}</td></tr>)}</tbody></table>
    {!terminal && (detail.canReview ? item ? <VerdictForm key={item.id + item.version} campaign={c.id} item={item} detail={detail} t={t} disabled={disabled} run={run}/>
      : <p data-state="select-item">{t.selectItem}</p> : <p data-state="not-reviewer">{t.notReviewer}</p>)}
    {!terminal && detail.canManage && <CloseForm detail={detail} t={t} disabled={disabled} run={run}/>}
  </section>
}

function VerdictForm({ campaign, item, detail, t, disabled, run }: { campaign: string; item: ReviewItem; detail: ReviewDetail; t: Copy; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const grants = detail.reviewerAuthorities
  const [authority, setAuthority] = useState(grants.length === 1 ? grants[0].id : '')
  const grant = grants.find(g => g.id === authority)
  const [verdict, setVerdict] = useState<ReviewVerdict>('REVIEW_REQUIRED'), [label, setLabel] = useState(item.sourceAuthority ?? item.candidateAuthority ?? '')
  const [owner, setOwner] = useState<string>(item.ownerRole), [reference, setReference] = useState(item.sourceReference ?? ''), [edition, setEdition] = useState(item.sourceEdition ?? '')
  const [issue, setIssue] = useState(item.trackedIssueId ?? ''), [note, setNote] = useState('')
  const confirming = verdict === 'CONFIRMED' || verdict === 'NOT_APPLICABLE_CONFIRMED'
  const noteOk = note.trim().length >= 10 && note.trim().length <= 2000
  const ready = !!grant && noteOk && (confirming ? grant.qualificationVerified && !!label && owner !== 'UNASSIGNED' && !item.stale &&
    (item.sourceId !== null || reference.trim() !== '' && edition.trim() !== '') : issue.trim() !== '')
  const blank = (v: string) => v.trim() === '' ? null : v.trim()
  return <form aria-label={t.review} onSubmit={e => { e.preventDefault(); if (ready && !disabled) void run(regulatoryReviewApi.verdict(campaign, item, authority,
    { verdict, sourceAuthority: blank(label), ownerRole: owner, sourceReference: blank(reference), sourceEdition: blank(edition), trackedIssueId: blank(issue), note: note.trim() })) }}>
    <h3>{t.review}: <code>{item.subjectKey}</code></h3>
    {grant && !grant.qualificationVerified && <p role="note" data-state="unqualified">{t.unqualified}</p>}
    <label htmlFor="review-authority">{t.reviewer}</label>
    <select id="review-authority" required value={authority} onChange={e => setAuthority(e.target.value)}>
      <option value="">—</option>{grants.map(g => <option key={g.id} value={g.id}>{g.sourceAuthorities.join(', ')}</option>)}</select>
    <fieldset><legend>{t.fieldVerdict}</legend>
      {reviewVerdicts.map(v => <label key={v}><input type="radio" name="review-verdict" value={v} checked={verdict === v}
        disabled={(v === 'CONFIRMED' || v === 'NOT_APPLICABLE_CONFIRMED') && (!grant?.qualificationVerified || item.stale)} onChange={() => setVerdict(v)}/>{word(t, 'verdict_' + v)}</label>)}
    </fieldset>
    <label htmlFor="review-label">{t.fieldAuthority}</label>
    <select id="review-label" value={label} onChange={e => setLabel(e.target.value)}><option value="">—</option>
      {reviewAuthorities.map(a => <option key={a} value={a} disabled={confirming && !grant?.sourceAuthorities.includes(a)}>{a}</option>)}</select>
    <label htmlFor="review-owner">{t.fieldOwner}</label>
    <select id="review-owner" value={owner} onChange={e => setOwner(e.target.value)}>{reviewOwnerRoles.map(o => <option key={o} value={o}>{o}</option>)}</select>
    {item.sourceId === null && <>
      <label htmlFor="review-reference">{t.fieldReference}</label><input id="review-reference" maxLength={200} value={reference} onChange={e => setReference(e.target.value)}/>
      <label htmlFor="review-edition">{t.fieldEdition}</label><input id="review-edition" maxLength={80} value={edition} onChange={e => setEdition(e.target.value)}/></>}
    <label htmlFor="review-issue">{t.fieldIssue}</label><input id="review-issue" maxLength={64} value={issue} onChange={e => setIssue(e.target.value)}/>
    <label htmlFor="review-note">{t.fieldNote}</label>
    <textarea id="review-note" required minLength={10} maxLength={2000} aria-describedby="review-note-hint" value={note} onChange={e => setNote(e.target.value)}/>
    <p id="review-note-hint">{t.noteHint}</p>
    <EnterpriseActions layout="form" aria-label={t.review}><Button type="submit" disabled={disabled || !ready}>{t.record}</Button></EnterpriseActions>
  </form>
}

function CloseForm({ detail, t, disabled, run }: { detail: ReviewDetail; t: Copy; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const c = detail.campaign
  const [outcome, setOutcome] = useState<typeof closeOutcomes[number]>(c.openItems > 0 ? 'CLOSED_WITH_OPEN_ITEMS' : 'CLOSED'), [reason, setReason] = useState('')
  const allowed = (o: string) => o === 'CLOSED' ? c.openItems === 0 : c.openItems > 0 && c.untrackedOpenItems === 0
  const reasonOk = reason.trim().length >= 10 && reason.trim().length <= 2000
  return <form aria-label={t.close} onSubmit={e => { e.preventDefault(); if (allowed(outcome) && reasonOk && !disabled) void run(regulatoryReviewApi.close(c, outcome, reason.trim())) }}>
    <h3>{t.close}</h3>
    <fieldset><legend>{t.outcome}</legend>
      {closeOutcomes.map(o => <label key={o}><input type="radio" name="review-outcome" value={o} checked={outcome === o} disabled={!allowed(o)} onChange={() => setOutcome(o)}/>{word(t, 'state_' + o)}</label>)}
    </fieldset>
    <p id="review-close-hint">{t.closeHint}</p>
    <label htmlFor="review-close-reason">{t.closeReason}</label>
    <textarea id="review-close-reason" required minLength={10} maxLength={2000} aria-describedby="review-close-hint" value={reason} onChange={e => setReason(e.target.value)}/>
    <EnterpriseActions layout="form" aria-label={t.close}><Button type="submit" disabled={disabled || !allowed(outcome) || !reasonOk}>{t.close}</Button></EnterpriseActions>
  </form>
}
