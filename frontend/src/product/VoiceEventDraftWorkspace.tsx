import { localizedCopy, localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterprisePageHeader } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { ApiError, session as readSession, vessels as readVessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { decodeVoiceDraft, freezeS9Command, s9Api, s9Failure, sendS9Command, textOrNull, voiceFieldKeys, voicePath,
  type S9Attempt, type S9Issue, type VoiceDraft, type VoiceDraftCapabilities, type VoiceDraftFields, type VoiceDraftMetrics, type VoiceDraftPage, type VoiceFieldKey } from './s9-wpc-api'
import './technical-records.css'
import { pilotAiOutcome, pilotForVessel } from './pilot-telemetry'

// S11 gate B1 (DEV-072, 19.01 p157-158): a confirmed draft whose fields the user edited or entered is
// a CORRECTED suggestion, an unchanged confirmation ACCEPTED, a rejection REJECTED. Codes only.
function reportVoiceOutcome(path: string, saved: VoiceDraft) {
  const outcome = /\/confirm\?/.test(path) && saved.state === 'CONFIRMED'
    ? (Object.values(saved.fieldOrigins).some(o => o === 'USER_EDITED' || o === 'USER_ENTERED') ? 'CORRECTED' as const : 'ACCEPTED' as const)
    : /\/reject\?/.test(path) && saved.state === 'REJECTED' ? 'REJECTED' as const : null
  if (outcome) pilotForVessel(saved.vesselId, area => pilotAiOutcome(area, 'VOICE', outcome))
}

// DEV-062 / T-S9-005 voice-to-event draft and DEV-063 / T-S9-006 measured usability (18.01 rows 63-64).
// Suggestions are traced to the user's own words; the user edits and confirms. Nothing is submitted,
// closed or turned into an event here. Recording appears only when the server reports a configured
// LOCAL engine; the browser Web Speech API (commonly cloud-backed) is never used.
export const s9Copy = localizedCopy('S9Wpc_tsx_copy')
export type S9Copy = (typeof s9Copy)[AppLocale]
export const s9Label = (t: S9Copy, prefix: string, code: string | null | undefined) =>
  code == null ? t.notSet : (t as Record<string, string>)[prefix + code] ?? code

export type VoiceRoute = { vessel: string | null; id: string | null }
export function readVoiceRoute(location: { pathname: string; search: string }): VoiceRoute {
  const match = /^\/app\/voice-event-draft(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(location.pathname)
  const id = new URLSearchParams(location.search).get('id')
  const vessel = match?.[1] && isGuid(match[1].toLowerCase()) ? match[1].toLowerCase() : null
  return { vessel, id: vessel && id && isGuid(id.toLowerCase()) ? id.toLowerCase() : null }
}
const href = (route: VoiceRoute) => '/app/voice-event-draft' + (route.vessel ? '/' + route.vessel + (route.id ? '?id=' + route.id : '') : '')
const today = () => new Date().toISOString().slice(0, 10)

export function VoiceEventDraftWorkspace() {
  const [language, setLanguage] = useAppLanguage()
  const [route, setRoute] = useState(() => readVoiceRoute(window.location))
  useEffect(() => { const pop = () => setRoute(readVoiceRoute(window.location)); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  const navigate = useCallback((target: string) => {
    if (target !== '/app' && !target.startsWith('/app/') || /[\\\p{Cc}]/u.test(target)) return
    if (/^\/app\/voice-event-draft(?:\/|$|\?)/.test(target)) { window.history.pushState(null, '', target); setRoute(readVoiceRoute(window.location)) }
    else window.location.assign(target)
  }, [])
  return <VoiceSurface key={route.vessel ?? 'fleet'} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate}/>
}

type Loaded = { user: Session; caps?: VoiceDraftCapabilities; page?: VoiceDraftPage; metrics?: VoiceDraftMetrics; record?: VoiceDraft; vessels?: Vessel[] }
type NewNote = { id: string; transcript: string; referenceDate: string; attestation: string | null }

function Banner({ issue, t, onReload, missing }: { issue: S9Issue; t: S9Copy; onReload?: () => void; missing?: string[] }) {
  if (!issue) return null
  return <div className={`technical-banner technical-banner-${issue}`} role={issue === 'offline' ? 'status' : 'alert'} data-issue={issue}>
    <p>{(t as Record<string, string>)['issue_' + issue]}</p>
    {missing && missing.length > 0 && <ul>{missing.map(code => <li key={code}>{s9Label(t, 'f_', code)}</li>)}</ul>}
    {onReload && issue !== 'offline' && <Button variant="outline" size="sm" onClick={onReload}>{t.reload}</Button>}
  </div>
}

function VoiceSurface({ route, language, onLanguage, onNavigate }: { route: VoiceRoute; language: AppLocale; onLanguage: (value: AppLocale) => void; onNavigate: (href: string) => void }) {
  const t = s9Copy[language]
  const title = localizedText(language, 'pairs.m_9a7c51e0d3b2f461')
  const [loaded, setLoaded] = useState<Loaded | null>(null), [failed, setFailed] = useState<ApiError | null>(null), [attempt, setAttempt] = useState(0)
  const [note, setNote] = useState<NewNote | null>(null), [issue, setIssue] = useState<S9Issue>(null), [busy, setBusy] = useState(false)
  const [pending, setPending] = useState<S9Attempt | null>(null), [offline, setOffline] = useState(typeof navigator !== 'undefined' && navigator.onLine === false)
  const [missing, setMissing] = useState<string[]>([])
  useEffect(() => { const on = () => setOffline(false), off = () => setOffline(true); window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) } }, [])
  useEffect(() => {
    const controller = new AbortController()
    ;(async () => {
      const user = await readSession(controller.signal)
      if (!route.vessel) return { user, vessels: (await readVessels(0, '', controller.signal)).items } as Loaded
      const [caps, page, metrics] = await Promise.all([s9Api.voiceCapabilities(route.vessel, controller.signal), s9Api.voiceList(route.vessel, 0, controller.signal),
        s9Api.voiceMetrics(route.vessel, controller.signal)])
      const record = route.id ? await s9Api.voiceRead(route.vessel, route.id, controller.signal) : undefined
      return { user, caps, page, metrics, record } as Loaded
    })().then(value => { if (!controller.signal.aborted) { setFailed(null); setLoaded(value) } })
      .catch(error => { if (!controller.signal.aborted) setFailed(error instanceof ApiError ? error : new ApiError(0, 'network')) })
    return () => controller.abort()
  }, [route.vessel, route.id, attempt])
  const reload = () => { setFailed(null); setIssue(null); setPending(null); setNote(null); setMissing([]); setAttempt(n => n + 1) }

  const execute = async (frozen: S9Attempt) => {
    if (!loaded?.user.csrfToken || !route.vessel) return
    setBusy(true); setPending(frozen); setIssue(null); setMissing([])
    try {
      const saved = decodeVoiceDraft(await sendS9Command<unknown>(frozen, loaded.user.csrfToken, new AbortController().signal), route.vessel)
      setPending(null); setNote(null)
      reportVoiceOutcome(frozen.path, saved)
      // Receipt confirmed: keep the canonical resource identity in the URL before any re-read.
      onNavigate(href({ vessel: route.vessel, id: saved.id })); setLoaded(current => current ? { ...current, record: saved } : current); setAttempt(n => n + 1)
    } catch (error) {
      const kind = s9Failure(error); setIssue(kind)
      if (kind === 'incomplete' && error instanceof ApiError) setMissing(loaded.record?.missing ?? [])
      if (kind !== 'offline') setPending(null)
    } finally { setBusy(false) }
  }
  const command = (method: 'POST' | 'PUT', suffix: string, body: unknown) => route.vessel && execute(freezeS9Command(method, voicePath(route.vessel, suffix), body))
  const caps = loaded?.caps, record = loaded?.record

  return <div className="product-shell enterprise-shell technical-workspace">
    <EnterpriseChrome session={loaded?.user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="voice-main"/>
    <main id="voice-main" tabIndex={-1} data-testid="voice-event-draft">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span className="technical-note">{t.vdNotice}</span>}
        actions={route.vessel && caps?.canWrite && !note && !offline ? <Button onClick={() => setNote({ id: crypto.randomUUID(), transcript: '', referenceDate: today(), attestation: null })}>{t.vdNew}</Button> : undefined}/>
      <p className="technical-note" data-testid="voice-privacy">{t.vdPrivacy}</p>
      {offline && <Banner issue="offline" t={t}/>}
      {pending && issue === 'offline' && <Button variant="outline" disabled={busy || offline} onClick={() => execute(pending)}>{t.retry}</Button>}
      <Banner issue={issue === 'offline' ? null : issue} t={t} onReload={reload} missing={issue === 'incomplete' ? missing : undefined}/>
      {failed ? <div role="alert" className="technical-banner"><p>{failed.status === 403 ? t.issue_forbidden : failed.status === 0 ? t.issue_offline : t.issue_error}</p><Button variant="outline" onClick={reload}>{t.reload}</Button></div>
        : !loaded ? <p role="status">{t.loading}</p>
        : !route.vessel ? <section className="technical-panel"><p>{t.chooseVessel}</p>{!loaded.vessels?.length ? <p role="status">{t.noVessels}</p> :
            <ul className="technical-list">{loaded.vessels.map(v => <li key={v.id}><Button variant="link" onClick={() => onNavigate('/app/voice-event-draft/' + v.id)}>{v.code} · {v.name}</Button></li>)}</ul>}</section>
        : <div className="technical-layout">
            <section className="technical-panel" aria-label={t.vdList}>
              {loaded.metrics && <VoiceMetrics metrics={loaded.metrics} t={t}/>}
              {!loaded.page?.items.length ? <p role="status">{t.vdEmpty}</p> :
              <ul className="technical-list">{loaded.page.items.map(i => <li key={i.id} aria-current={i.id === route.id ? 'true' : undefined}>
                <Button variant="link" onClick={() => onNavigate(href({ vessel: route.vessel, id: i.id }))}>{s9Label(t, 'et_', i.eventType)} · {s9Label(t, 'loc_', i.location)}</Button>
                <Badge tone="neutral" data-state={i.state}>{s9Label(t, 'vs_', i.state)}</Badge>{i.missingCount > 0 && <small> {t.vdMissingCount.replace('{n}', String(i.missingCount))}</small>}</li>)}</ul>}
            </section>
            {note && caps ? <NoteForm note={note} caps={caps} t={t} busy={busy || offline} vessel={route.vessel} csrf={loaded.user.csrfToken ?? ''} onChange={setNote} onCancel={() => setNote(null)}
                onCreate={n => command('POST', '', { id: n.id, transcript: n.transcript, origin: n.attestation ? 'LOCAL_SPEECH' : 'TYPED', referenceDate: n.referenceDate, speechAttestation: n.attestation })}/>
              : record && caps ? <VoiceDetail key={record.id + ':' + record.version} record={record} caps={caps} t={t} busy={busy || offline} onCommand={command}/> : null}
          </div>}
    </main>
  </div>
}

function VoiceMetrics({ metrics, t }: { metrics: VoiceDraftMetrics; t: S9Copy }) {
  const pct = (v: number | null | undefined) => v == null ? t.notMeasured : `${Math.round(v * 100)}%`
  return <dl className="technical-status" data-testid="voice-metrics" aria-label={t.vdMetrics}>
    <dt>{t.vdMetricCompleteness}</dt><dd>{pct(metrics.fieldCompleteness)}</dd>
    <dt>{t.vdMetricCorrection}</dt><dd>{pct(metrics.correctionRate)}</dd>
    <dt>{t.vdMetricUsability}</dt><dd>{pct(metrics.usability)}</dd>
    <dt>{t.vdMetricRejected}</dt><dd>{metrics.rejected}</dd>
    <dd><small>{t.vdMetricNote}</small></dd>
  </dl>
}

function NoteForm({ note, caps, t, busy, vessel, csrf, onChange, onCancel, onCreate }: { note: NewNote; caps: VoiceDraftCapabilities; t: S9Copy; busy: boolean; vessel: string; csrf: string;
  onChange: (n: NewNote) => void; onCancel: () => void; onCreate: (n: NewNote) => void }) {
  const [recording, setRecording] = useState(false), [speechIssue, setSpeechIssue] = useState<string | null>(null)
  const recorder = useRef<MediaRecorder | null>(null)
  const canRecord = caps.speechAvailable && typeof window !== 'undefined' && 'MediaRecorder' in window && !!navigator.mediaDevices
  const start = async () => {
    setSpeechIssue(null)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      const chunks: Blob[] = []; const r = new MediaRecorder(stream); recorder.current = r
      r.ondataavailable = e => chunks.push(e.data)
      r.onstop = async () => {
        stream.getTracks().forEach(track => track.stop()); setRecording(false)
        try {
          const result = await s9Api.transcribe(vessel, new Blob(chunks, { type: r.mimeType || 'audio/webm' }), csrf, new AbortController().signal)
          onChange({ ...note, transcript: result.transcript, attestation: result.attestation })
        } catch { setSpeechIssue(t.vdSpeechFailed) }
      }
      r.start(); setRecording(true)
    } catch { setSpeechIssue(t.vdMicDenied) }
  }
  const valid = note.transcript.trim().length > 0 && note.transcript.length <= 4000 && /^\d{4}-\d{2}-\d{2}$/.test(note.referenceDate)
  return <form className="technical-panel" aria-label={t.vdNew} onSubmit={e => { e.preventDefault(); if (valid) onCreate(note) }}>
    <h2>{t.vdNew}</h2>
    {canRecord ? <div>{recording ? <Button type="button" variant="outline" onClick={() => recorder.current?.stop()}>{t.vdStop}</Button>
        : <Button type="button" variant="outline" disabled={busy} onClick={start}>{t.vdRecord}</Button>}
        <small> {t.vdLocalEngine.replace('{engine}', caps.speechEngineVersion ?? '')}</small></div>
      : <p className="technical-note" data-testid="speech-unavailable">{t.vdSpeechUnavailable}</p>}
    {speechIssue && <p role="alert">{speechIssue}</p>}
    <label className="technical-field"><span>{t.vdTranscript}</span>
      <textarea value={note.transcript} maxLength={4000} rows={6} onChange={e => onChange({ ...note, transcript: e.target.value, attestation: null })}/>
      <small>{note.attestation ? t.vdFromLocalSpeech : t.vdTypedHint}</small></label>
    <label className="technical-field"><span>{t.vdReferenceDate}</span>
      <input type="date" value={note.referenceDate} onChange={e => onChange({ ...note, referenceDate: e.target.value })}/><small>{t.vdReferenceHint}</small></label>
    <div className="technical-actions"><Button type="submit" disabled={busy || !valid}>{t.vdDraft}</Button><Button type="button" variant="outline" onClick={onCancel}>{t.cancel}</Button></div>
  </form>
}

function VoiceDetail({ record, caps, t, busy, onCommand }: { record: VoiceDraft; caps: VoiceDraftCapabilities; t: S9Copy; busy: boolean;
  onCommand: (method: 'POST' | 'PUT', suffix: string, body: unknown) => void }) {
  const [fields, setFields] = useState<VoiceDraftFields>(() => ({ ...record.fields }))
  const [reason, setReason] = useState(''), [rejectNote, setRejectNote] = useState('')
  const dirty = voiceFieldKeys.some(k => (fields[k] ?? null) !== (record.fields[k] ?? null))
  const set = (k: VoiceFieldKey, v: string) => setFields(f => ({ ...f, [k]: textOrNull(v) }))
  const candidate = (k: VoiceFieldKey) => record.candidate.fields.find(f => f.key === k)
  const excerpt = (k: VoiceFieldKey) => candidate(k)?.spans.map(s => record.transcript.slice(s.start, s.end)).join(' … ')
  const editable = record.canEdit
  const input = (k: VoiceFieldKey) => k === 'eventType' ? <select aria-label={s9Label(t, 'f_', k)} value={fields.eventType ?? ''} disabled={!editable} onChange={e => set(k, e.target.value)}>
      <option value="">{t.notSet}</option>{caps.eventTypes.map(c => <option key={c} value={c}>{s9Label(t, 'et_', c)}</option>)}</select>
    : k === 'location' ? <select aria-label={s9Label(t, 'f_', k)} value={fields.location ?? ''} disabled={!editable} onChange={e => set(k, e.target.value)}>
      <option value="">{t.notSet}</option>{caps.locations.map(c => <option key={c} value={c}>{s9Label(t, 'loc_', c)}</option>)}</select>
    : k === 'occurredAt' ? <input aria-label={s9Label(t, 'f_', k)} type="datetime-local" value={fields.occurredAt ?? ''} disabled={!editable} onChange={e => set(k, e.target.value)}/>
    : k === 'locationDetail' ? <input aria-label={s9Label(t, 'f_', k)} value={fields.locationDetail ?? ''} maxLength={200} disabled={!editable} onChange={e => set(k, e.target.value)}/>
    : <textarea aria-label={s9Label(t, 'f_', k)} value={fields[k] ?? ''} rows={3} maxLength={2000} disabled={!editable} onChange={e => set(k, e.target.value)}/>
  return <section className="technical-panel" aria-labelledby="voice-detail-title" data-testid="voice-detail">
    <h2 id="voice-detail-title">{s9Label(t, 'et_', record.fields.eventType)}</h2>
    <div className="technical-status"><Badge tone={record.state === 'DRAFT' ? 'warning' : 'neutral'} data-state={record.state}>{s9Label(t, 'vs_', record.state)}</Badge>
      <Badge tone="neutral">{s9Label(t, 'vo_', record.origin)}</Badge></div>
    <p className="technical-note">{t.vdVersions.replace('{extractor}', record.extractorVersion).replace('{rules}', record.ruleSetVersion)}
      {record.speech && <> · {t.vdSpeechVersion.replace('{engine}', record.speech.engineVersion).replace('{model}', record.speech.modelVersion)}</>}</p>
    <blockquote className="technical-note" data-testid="voice-transcript">{record.transcript}</blockquote>
    {record.candidate.hints.length > 0 && <ul aria-label={t.vdHints}>{record.candidate.hints.map((h, i) => <li key={i}>{s9Label(t, 'hint_', h.code)}: “{record.transcript.slice(h.span.start, h.span.end)}”</li>)}</ul>}
    <div className="technical-grid">{voiceFieldKeys.map(k => <div key={k} className="technical-field" data-field={k} data-origin={record.fieldOrigins[k]}>
      <span>{s9Label(t, 'f_', k)}{caps.requiredFields.includes(k) ? ' *' : ''}</span>{input(k)}
      <small><Badge tone={record.fieldOrigins[k] === 'EXTRACTED' ? 'warning' : 'neutral'}>{s9Label(t, 'fo_', record.fieldOrigins[k])}</Badge>
        {candidate(k) && <> {t.vdTrace.replace('{confidence}', String(Math.round(candidate(k)!.confidence * 100))).replace('{rule}', candidate(k)!.rule)} “{excerpt(k)}”</>}</small>
    </div>)}</div>
    {record.missing.length > 0 && <div role="status"><p>{t.vdMissing}</p><ul>{record.missing.map(m => <li key={m}>{s9Label(t, 'f_', m)}</li>)}</ul></div>}
    {record.state === 'CONFIRMED' && record.handoff === 'EVENT_DRAFT_CREATED' && record.safetyEventId && <p role="status" data-testid="voice-handoff" data-handoff="EVENT_DRAFT_CREATED">
      {t.vdHandoffCreated} <a href={`/app/safety-events?vessel=${encodeURIComponent(record.vesselId)}&event=${encodeURIComponent(record.safetyEventId)}`}>{t.vdOpenEventDraft}</a></p>}
    {record.state === 'CONFIRMED' && record.handoff !== 'EVENT_DRAFT_CREATED' && <p role="status" data-testid="voice-handoff" data-handoff="PENDING_EVENT_DRAFT_INTEGRATION">
      {t.vdConfirmed} {record.handoffReason === 'SAFETY_EVENT_REPORT_PERMISSION_REQUIRED' && t.vdHandoffPermission}</p>}
    {record.state === 'REJECTED' && record.rejection && <p role="status">{t.vdRejected.replace('{reason}', s9Label(t, 'rr_', record.rejection.reasonCode))}</p>}
    {editable && <div className="technical-actions">
      <Button disabled={busy || !dirty} onClick={() => onCommand('PUT', '/' + record.id, { expectedVersion: record.version, fields, evidenceFileIds: record.evidence.map(e => e.id) })}>{t.save}</Button>
      <Button disabled={busy || dirty || !record.canConfirm} onClick={() => onCommand('POST', `/${record.id}/confirm`, { expectedVersion: record.version, contentHash: record.hash })}>{t.vdConfirm}</Button>
    </div>}
    {editable && <p className="technical-note">{t.vdConfirmNote}</p>}
    {editable && <form aria-label={t.vdReject} onSubmit={e => { e.preventDefault(); onCommand('POST', `/${record.id}/reject`, { expectedVersion: record.version, reasonCode: reason, note: textOrNull(rejectNote) }) }}>
      <label className="technical-field"><span>{t.vdRejectReason}</span><select value={reason} onChange={e => setReason(e.target.value)}>
        <option value="">{t.notSet}</option>{caps.rejectionReasons.map(r => <option key={r} value={r}>{s9Label(t, 'rr_', r)}</option>)}</select></label>
      <label className="technical-field"><span>{t.vdRejectNote}</span><input value={rejectNote} maxLength={500} onChange={e => setRejectNote(e.target.value)}/></label>
      <Button type="submit" variant="outline" disabled={busy || !reason || reason === 'OTHER' && !rejectNote.trim()}>{t.vdReject}</Button>
    </form>}
  </section>
}
