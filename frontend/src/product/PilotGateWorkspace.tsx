import { useCallback, useEffect, useState } from 'react'
import { localizedText } from '../i18n/messages'
import { useAppLanguage } from '../i18n'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { ApiError, session as readSession, type Session } from './api'
import type { Command } from './tug-readiness-api'
import { pilotGateApi, supportItems, supportStates, type PilotDecisionCode, type PilotGateView } from './pilot-gate-api'
import { CommandNotice, GateBadge, pilotCopy, pilotWord, useOnline, usePilotCommand, type PilotCopy } from './UatWorkspace'

// S11 WP-D DEV-075 pilot go/no-go review (18.01 row 76; DoR DoD Gates row 4; 23.03 §5). The review is advisory and
// never shows GO: UNKNOWN/DEFERRED gates and unknown/open P0 recommend NO-GO. Only the appointed Product Owner records
// a decision bound to the exact review hash; GO is offered only for a GO-eligible review.
export function PilotGateWorkspace() {
  const [lang, setLanguage] = useAppLanguage(), t = pilotCopy[lang]
  const title = localizedText(lang, 'pairs.m_5b1e7c9d3a2f4e61')
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true), [error, setError] = useState<ApiError | null>(null), [data, setData] = useState<PilotGateView | null>(null)
  const offline = useOnline()
  const refresh = useCallback(() => setReload(n => n + 1), [])
  const cmd = usePilotCommand(user, offline, refresh)
  useEffect(() => {
    const c = new AbortController()
    ;(async () => {
      await Promise.resolve()
      if (c.signal.aborted) return
      setLoading(true); setError(null); setData(null)
      if (!/^\/app\/pilot-gate\/?$/.test(window.location.pathname) || window.location.search) throw new ApiError(400, 'invalid_route')
      setUser(await readSession(c.signal))
      setData(await pilotGateApi.read(c.signal))
    })().then(() => { if (!c.signal.aborted) setLoading(false) })
      .catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e : new ApiError(0, 'network')); setLoading(false) })
    return () => c.abort()
  }, [reload])
  const move = useCallback((href: string) => { if (!cmd.retained && !cmd.busy) window.location.assign(href) }, [cmd])
  const disabled = cmd.busy || offline || !!cmd.retained && cmd.notice !== 'uncertain'
  return <div className="product-shell enterprise-shell pilot-gate-workspace">
    <EnterpriseChrome session={user} language={lang} onLanguageChange={setLanguage} onNavigate={move} title={title} mainId="pilot-gate-main"/>
    <main id="pilot-gate-main" tabIndex={-1} aria-busy={loading || cmd.busy} data-testid="pilot-gate">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span>{t.gateBoundary}</span>}
        actions={<Button variant="outline" disabled={offline || cmd.busy} onClick={refresh}>{t.refresh}</Button>}/>
      {offline && <p role="alert" data-state="offline">{t.offline}</p>}
      <CommandNotice t={t} {...cmd} offline={offline} reload={refresh}/>
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error && <div role="alert" data-state={error.status === 403 ? 'forbidden' : 'error'}>
        <p>{error.code === 'invalid_route' ? t.invalidRoute : error.status === 403 ? t.forbidden : error.status === 0 ? t.offline : t.error}</p>
        {error.code !== 'invalid_route' && <Button type="button" variant="outline" onClick={refresh}>{t.retry}</Button>}</div>}
      {!loading && !error && data && <Review t={t} data={data} disabled={disabled} run={cmd.run}/>}
    </main>
  </div>
}

function Review({ t, data, disabled, run }: { t: PilotCopy; data: PilotGateView; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const r = data.review
  return <>
    <section aria-label={t.review}>
      <h2>{t.review}</h2>
      <p><Badge tone={r.recommendation === 'NO_GO_RECOMMENDED' ? 'danger' : 'info'} data-recommendation={r.recommendation}>{pilotWord(t, 'rec_' + r.recommendation)}</Badge> <small>{t.advisory}</small></p>
      <p>{t.openP0}: <strong data-count="p0">{r.openP0 === null ? t.unknown : r.openP0}</strong> · <time dateTime={data.assessedAt}>{data.assessedAt}</time></p>
      <table><thead><tr><th scope="col">{t.gate}</th><th scope="col">{t.state}</th><th scope="col">{t.source}</th><th scope="col">{t.reasons}</th></tr></thead>
        <tbody>{r.gates.map(g => <tr key={g.gate} data-gate={g.gate}><td>{pilotWord(t, 'g_' + g.gate)}</td><td><GateBadge state={g.state} t={t}/></td><td><code>{g.source}</code></td>
          <td>{g.reasons.length === 0 ? '—' : <ul>{g.reasons.map(x => <li key={x}><code>{x}</code></li>)}</ul>}</td></tr>)}</tbody></table>
    </section>
    <section aria-label={t.support}>
      <h2>{t.support}</h2>
      <ul>{data.support.map(s => <SupportItem key={s.item + (s.latest?.sequence ?? 0)} t={t} item={s.item} latest={s.latest} canRecord={data.canRecordSupport} disabled={disabled} run={run}/>)}</ul>
    </section>
    <section aria-label={t.decisions}>
      <h2>{t.decisions}</h2>
      {data.decisions.length === 0 ? <p data-state="empty">{t.noDecisions}</p> :
        <ul>{data.decisions.map(d => <li key={d.id} data-decision={d.decision} data-validity={d.validity}>#{d.sequence} · {pilotWord(t, 'decision_' + d.decision)} · {pilotWord(t, 'rec_' + d.recommendation)}
          {' '}· <Badge tone={d.validity === 'STALE' ? 'warning' : 'neutral'}>{pilotWord(t, 'validity_' + d.validity)}</Badge> · {d.rationale}
          {d.conditions.length > 0 && <ul>{d.conditions.map(c => <li key={c}>{c}</li>)}</ul>}</li>)}</ul>}
      {data.canDecide ? <DecisionForm key={r.hash + data.sequence} t={t} data={data} disabled={disabled} run={run}/> : <p data-state="not-appointed">{t.notAppointed}</p>}
    </section>
  </>
}

function SupportItem({ t, item, latest, canRecord, disabled, run }: { t: PilotCopy; item: typeof supportItems[number]; latest: PilotGateView['support'][number]['latest']
  canRecord: boolean; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const [state, setState] = useState(''), [evidence, setEvidence] = useState(''), [note, setNote] = useState('')
  const ok = !!state && note.trim().length >= 3 && (state !== 'READY' || evidence.trim().length > 0)
  return <li data-support={item} data-support-state={latest?.state ?? 'UNMARKED'}>
    <code>{item}</code> · {latest ? <><Badge tone={latest.state === 'READY' ? 'info' : 'danger'}>{pilotWord(t, 'support_' + latest.state)}</Badge>{latest.evidenceReference && <> · <code>{latest.evidenceReference}</code></>}</>
      : <Badge tone="warning">{t.unmarked}</Badge>}
    {canRecord && <form aria-label={t.markSupport + ' ' + item} onSubmit={e => { e.preventDefault(); if (ok && !disabled)
      void run(pilotGateApi.support(item, state, evidence.trim() || null, note.trim(), latest?.sequence ?? 0)) }}>
      <select aria-label={t.state} value={state} onChange={e => setState(e.target.value)}><option value="">—</option>{supportStates.map(s => <option key={s} value={s}>{pilotWord(t, 'support_' + s)}</option>)}</select>
      <input aria-label={t.evidenceReference} maxLength={200} value={evidence} onChange={e => setEvidence(e.target.value)}/>
      <input aria-label={t.note} minLength={3} maxLength={500} value={note} onChange={e => setNote(e.target.value)}/>
      <Button type="submit" disabled={disabled || !ok}>{t.markSupport}</Button></form>}
  </li>
}

function DecisionForm({ t, data, disabled, run }: { t: PilotCopy; data: PilotGateView; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const [decision, setDecision] = useState<PilotDecisionCode | ''>(''), [rationale, setRationale] = useState(''), [conditions, setConditions] = useState('')
  const [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : '')
  const list = conditions.split('\n').map(x => x.trim()).filter(Boolean)
  const ok = !!decision && !!authority && rationale.trim().length >= 10 && (decision !== 'CONDITIONAL_GO' || list.length > 0) && (decision === 'CONDITIONAL_GO' || list.length === 0)
  return <form aria-label={t.decide} onSubmit={e => { e.preventDefault(); if (ok && !disabled && decision) void run(pilotGateApi.decide(data, decision, rationale.trim(), list, authority)) }}>
    <h3>{t.decide}</h3>
    <fieldset><legend>{t.decide}</legend>
      {(['NO_GO', 'CONDITIONAL_GO', 'GO'] as const).map(d => <label key={d}><input type="radio" name="pilot-decision" value={d} checked={decision === d}
        disabled={!data.allowedDecisions.includes(d)} onChange={() => setDecision(d)}/>{pilotWord(t, 'decision_' + d)}</label>)}
    </fieldset>
    {!data.allowedDecisions.includes('GO') && <p>{t.goBlocked}</p>}
    <label htmlFor="pilot-authority">{t.authority}</label>
    <select id="pilot-authority" required value={authority} onChange={e => setAuthority(e.target.value)}><option value="">—</option>
      {data.authorities.map(a => <option key={a.id} value={a.id}>{t.productOwner}</option>)}</select>
    <label htmlFor="pilot-rationale">{t.rationale}</label>
    <textarea id="pilot-rationale" required minLength={10} maxLength={2000} value={rationale} onChange={e => setRationale(e.target.value)}/>
    {decision === 'CONDITIONAL_GO' && <><label htmlFor="pilot-conditions">{t.conditions}</label>
      <textarea id="pilot-conditions" required value={conditions} onChange={e => setConditions(e.target.value)}/></>}
    <EnterpriseActions layout="form" aria-label={t.decide}><Button type="submit" disabled={disabled || !ok}>{t.record}</Button></EnterpriseActions>
  </form>
}
