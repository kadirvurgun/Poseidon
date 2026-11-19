import { useEffect, useId, useRef, useState } from 'react'
import { CircleAlert, CircleHelp, Clock, EyeOff, Info, Octagon } from 'lucide-react'
import { localizedCopy, localizedLabel } from '../i18n/messages'
import type { Locale } from '../i18n'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { ApiError } from './api'
import { profileFields } from './profile-contract'
import { loadCriticalStates, type CriticalStateExplanation, type CriticalStateKey, type CriticalStates } from './critical-state-api'
import './critical-state.css'

// DEV-024 / T-S2-005. Reusable critical-state explanation (source, reason codes, freshness,
// blockers). DEV-023 Why panels can render CriticalStateExplanationView for any explanation
// the server returns. No state is ever rendered as a green ready/verified result; a STATE_ONLY
// entry shows only that the state exists and is not positive.
const copy = localizedCopy('CriticalStatePanel_tsx_copy')
type Copy = ReturnType<typeof text>
const text = (language: Locale) => copy[language]
const keyLabel = (t: Copy, key: string) => key === 'vessel.onboarding' ? t.keys.onboarding : key === 'vessel.profile.freshness' ? t.keys.profileFreshness : t.keys.applicability
const pick = (map: Record<string, string>, value: string) => map[value] ?? value
const tone = (state: string) => state === 'BLOCKED' || state === 'UNAVAILABLE' ? 'danger' : state === 'DECLARED_UNVERIFIED' || state === 'QUALIFIED_CURRENT_UNVERIFIED' ? 'info' : 'warning'
function StateIcon({ state, restricted }: { state: string; restricted: boolean }) {
  const props = { 'aria-hidden': true, size: 14 } as const
  if (restricted) return <EyeOff {...props}/>
  if (state === 'BLOCKED') return <Octagon {...props}/>
  if (state === 'STALE') return <Clock {...props}/>
  if (state === 'UNKNOWN') return <CircleHelp {...props}/>
  return state === 'DECLARED_UNVERIFIED' || state === 'QUALIFIED_CURRENT_UNVERIFIED' ? <Info {...props}/> : <CircleAlert {...props}/>
}

function fieldLabel(path: string, t: Copy, language: Locale) {
  if (path === 'profile') return t.fields.profile
  if (path === 'applicability') return t.fields.applicability
  if (path === 'vessel.code') return t.fields.vesselCode
  if (path === 'vessel.name') return t.fields.vesselName
  const entry = profileFields.find(([key]) => `profile.${key}` === path)
  return entry ? localizedLabel({ en: entry[3], tr: entry[2] }, language) : path
}

export function CriticalStateExplanationView({ state, language, headingLevel = 4 }: { state: CriticalStateExplanation; language: Locale; headingLevel?: 3 | 4 | 5 }) {
  const t = text(language), heading = useId(), Heading = `h${headingLevel}` as const
  const restricted = state.disclosure !== 'FULL'
  return <article className="critical-state" aria-labelledby={heading} data-state={state.state} data-disclosure={state.disclosure}>
    <header className="critical-state-header">
      <Heading id={heading}>{keyLabel(t, state.key)}</Heading>
      <Badge tone={restricted ? 'neutral' : tone(state.state)}><StateIcon state={state.state} restricted={restricted}/>{pick(t.states, state.state)}</Badge>
    </header>
    {restricted ? <p className="critical-state-restricted">{t.restricted}</p> : <>
      <dl className="critical-state-meta">
        <div><dt>{t.source}</dt><dd>{state.source ? <>
          {pick(t.sourceKinds, state.source.kind)} · {t.revision}: {state.source.revision ?? t.noRevision}
          {state.source.recordedAt && <> · {t.recorded}: <time dateTime={state.source.recordedAt}>{state.source.recordedAt}</time></>}
        </> : t.noSource}</dd></div>
        <div><dt>{t.verification}</dt><dd>{t.notVerified}</dd></div>
        {state.freshness && <div><dt>{t.freshness}</dt><dd>
          <span className="critical-state-freshness" data-state={state.freshness.state}>{pick(t.freshnessStates, state.freshness.state)}</span>
          {' · '}{pick(t.reasons, state.freshness.reasonCode)}
          {state.freshness.observedAt && <> · {t.observed}: <time dateTime={state.freshness.observedAt}>{state.freshness.observedAt}</time></>}
          {' · '}{t.checked}: <time dateTime={state.freshness.checkedAt}>{state.freshness.checkedAt}</time>
        </dd></div>}
      </dl>
      {!!state.reasonCodes?.length && <section aria-label={t.reasonsTitle}><h5>{t.reasonsTitle}</h5><ul className="critical-state-reasons">
        {state.reasonCodes.map(reason => <li key={reason}>{pick(t.reasons, reason)} <code>{reason}</code></li>)}</ul></section>}
      <section aria-label={t.blockersTitle}><h5>{t.blockersTitle}</h5>{state.blockers?.length ? <ul className="critical-state-blockers">
        {state.blockers.map(b => <li key={`${b.fieldPath}\0${b.reasonCode}`}>
          <strong>{fieldLabel(b.fieldPath, t, language)}</strong>
          <span>{pick(t.reasons, b.reasonCode)}</span>
          <span>{t.resolution}: {pick(t.resolutions, b.resolutionType)}</span>
        </li>)}</ul> : <p>{t.noBlockers}</p>}</section>
      {state.href && <a className="critical-state-link" href={state.href}>{t.open}</a>}
    </>}
  </article>
}

type Load = { key: string; data?: CriticalStates; status?: number }
type Props = { vessel: string; language: Locale; revision?: number | null; dirty?: boolean; only?: CriticalStateKey; onDenied?: (error: ApiError) => void }

export function CriticalStatesPanel({ vessel, language, revision = null, dirty = false, only, onDenied }: Props) {
  const t = text(language), heading = useId()
  const [nonce, setNonce] = useState(0)
  const [online, setOnline] = useState(() => navigator.onLine)
  const loadKey = `${vessel}/${revision ?? ''}/${only ?? ''}/${nonce}`
  const [result, setResult] = useState<Load | null>(null)
  // Callers may pass an unstable callback; it must not restart the read.
  const denied = useRef(onDenied)
  useEffect(() => { denied.current = onDenied }, [onDenied])
  useEffect(() => {
    // 61.03 p1064-1072: going offline drops the held explanations from memory; after reconnect
    // the panel shows loading until a fresh server read replaces them.
    const update = () => { const on = navigator.onLine; if (!on) setResult(null); setOnline(on) }
    window.addEventListener('online', update); window.addEventListener('offline', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update) }
  }, [])
  useEffect(() => {
    if (!online) return
    const abort = new AbortController()
    loadCriticalStates(vessel, abort.signal, only).then(data => { if (!abort.signal.aborted) setResult({ key: loadKey, data }) }, (error: unknown) => {
      if (abort.signal.aborted) return
      const status = error instanceof ApiError ? error.status : 0
      // A failed or withdrawn read removes previously shown explanations (no stale cached state).
      setResult({ key: loadKey, status })
      if (status === 403 && error instanceof ApiError) denied.current?.(error)
    })
    return () => abort.abort()
  }, [vessel, only, loadKey, online])
  // Only a response for the current vessel/revision/reload key is ever shown.
  const current = result?.key === loadKey ? result : null
  const failure = current?.status !== undefined ? (current.status === 401 ? t.errors.e401 : current.status === 403 ? t.errors.e403 : current.status === 404 ? t.errors.e404 : t.errors.other) : null
  return <section className="critical-states" aria-labelledby={heading}>
    <h3 id={heading}>{t.title}</h3>
    {dirty && <p role="status">{t.dirty}</p>}
    {!online ? <p role="status">{t.offline}</p> : !current ? <p role="status">{t.loading}</p> : failure ? <p role="alert">{failure}</p> :
      current.data!.states.length === 0 ? <p>{t.empty}</p> : <>
        <p className="critical-states-note">{t.scope}</p>
        {current.data!.states.map(state => <CriticalStateExplanationView key={state.key} state={state} language={language}/>)}
      </>}
    <Button variant="outline" disabled={!online} onClick={() => { setResult(null); setNonce(n => n + 1) }}>{t.reload}</Button>
  </section>
}
