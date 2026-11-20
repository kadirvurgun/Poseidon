import { localizedCopy } from '../i18n/messages'
import { type Locale } from '../i18n'
import { useEffect, useId, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'
import type { CertificateRenewalContent } from './certificate-content'
import { projectRenewalQueue, RenewalOfflineApplication, type RenewalConflict, type RenewalPending } from './certificate-renewal-offline'
import { syncPilotJourney, type PilotLedger } from './pilot-offline-journey'

export const s10Copy = localizedCopy('S10Wpc_tsx_copy')
type Copy = (typeof s10Copy)['en']['renewalOffline']
const textKeys = ['renewalType', 'plannedLocation', 'authorityReference', 'surveyWindowEnd'] as const
const dateKeys = ['triggerDate', 'dueDate', 'surveyWindowStart', 'applicationDate'] as const

// S10 WP-C DEV-066 flow 1. Drafts are sealed into the encrypted device queue BEFORE any network attempt;
// reconnect replays the exact frozen bodies with their original keys. A material conflict is never
// auto-resolved: it is listed in the conflict center and resolved here only by an explicit human choice.
// Replacement certificate/document links and every workflow transition remain online-only.
export function CertificateRenewalOfflinePanel({ vessel, renewal, language, onSynced }: { vessel: string; renewal: string; language: Locale; onSynced?: () => void }) {
  const t: Copy = s10Copy[language].renewalOffline, id = useId()
  const [app, setApp] = useState<RenewalOfflineApplication | null>(null), [rows, setRows] = useState<RenewalPending[]>([])
  const [draft, setDraft] = useState<CertificateRenewalContent | null>(null), [status, setStatus] = useState<string | null>(null), [busy, setBusy] = useState(false)
  const [conflict, setConflict] = useState<RenewalConflict | null>(null), [online, setOnline] = useState(() => navigator.onLine), [ledger, setLedger] = useState<PilotLedger | null>(null)
  const handle = useRef<RenewalOfflineApplication | null>(null)
  useEffect(() => {
    const on = () => setOnline(true), off = () => setOnline(false)
    window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off); handle.current?.close() }
  }, [])
  const refresh = async (value: RenewalOfflineApplication) => setRows(await value.captures())
  const guard = async (action: () => Promise<void>) => {
    setBusy(true); setStatus(null)
    try { await action() }
    catch (error) {
      const message = error instanceof Error ? error.message : ''
      setStatus(message === 'reauthorization_required_data_retained' ? t.reauthorize : message === 'key_lost_data_retained' ? t.keyLost
        : error && typeof error === 'object' && 'status' in error && (error as { status: number }).status === 403 ? t.unavailable : t.failed)
    } finally { setBusy(false) }
  }
  const prepare = () => guard(async () => {
    const c = new AbortController(); const value = await RenewalOfflineApplication.authorize(vessel, renewal, null, c.signal)
    handle.current?.close(); handle.current = value; setApp(value); setDraft(structuredClone(value.context.baseContent)); await refresh(value); setStatus(t.prepared)
  })
  const capture = () => app && draft && guard(async () => { await app.capture(draft); await refresh(app); setStatus(t.queued) })
  const sync = () => app && guard(async () => {
    const c = new AbortController()
    const [result] = await syncPilotJourney([{ flow: 'certificateRenewal', captures: () => app.captures(), replay: row => app.replay(row as RenewalPending, c.signal) }], c.signal, () => navigator.onLine)
    setLedger(result); await refresh(app); setStatus(result.conflicts > 0 ? t.conflictRecorded : result.pending > 0 ? t.stillPending : t.synced); onSynced?.()
  })
  const review = (commandId: string) => app && guard(async () => { const c = new AbortController(); setConflict(await app.conflict(commandId, c.signal)) })
  const resolve = (choice: 'keepServer' | 'useCaptured') => app && conflict && guard(async () => {
    const c = new AbortController(); const child = await app.capture(null, { conflict, choice }); await refresh(app)
    const row = (await app.captures()).find(r => r.commandId === child); if (row) await app.replay(row, c.signal)
    setConflict(null); await refresh(app); setStatus(t.resolutionRecorded); onSynced?.()
  })
  const queue = projectRenewalQueue(rows)
  return <section className="certificate-renewal-offline" aria-labelledby={`${id}-heading`} aria-busy={busy}>
    <h3 id={`${id}-heading`}>{t.heading}</h3>
    <p>{t.scope}</p>
    <p><Badge tone="warning">{t.unverified}</Badge> {online ? null : <Badge tone="warning" role="status">{t.offline}</Badge>}</p>
    {!app ? <EnterpriseActions><Button disabled={busy || !online} onClick={prepare}>{t.prepare}</Button></EnterpriseActions> : <>
      <p role="status" aria-live="polite">{t.queueSummary.replace('{pending}', String(queue.pending)).replace('{conflicts}', String(queue.conflicts.length)).replace('{committed}', String(queue.committed))}</p>
      {draft && <form noValidate aria-label={t.formLabel} onSubmit={event => { event.preventDefault(); void capture() }}>
        <EnterpriseFormGrid disabled={busy}><legend>{t.formLabel}</legend>
          {textKeys.map(key => <EnterpriseField key={key} htmlFor={`${id}-${key}`} label={t.fields[key]}>
            <input id={`${id}-${key}`} type="text" maxLength={4096} value={draft[key] ?? ''} onChange={event => setDraft({ ...draft, [key]: event.target.value || null })}/></EnterpriseField>)}
          {dateKeys.map(key => <EnterpriseField key={key} htmlFor={`${id}-${key}`} label={t.fields[key]}>
            <input id={`${id}-${key}`} type="date" value={draft[key] ?? ''} onChange={event => setDraft({ ...draft, [key]: event.target.value || null })}/></EnterpriseField>)}
          <p className="certificate-field-hint">{t.linksOnline}</p>
        </EnterpriseFormGrid>
        <EnterpriseActions><Button type="submit" disabled={busy}>{t.capture}</Button><Button variant="outline" type="button" disabled={busy || !online || queue.pending === 0} onClick={() => void sync()}>{t.sync}</Button></EnterpriseActions>
      </form>}
      {ledger && <p data-testid="renewal-offline-ledger">{t.ledger.replace('{committed}', String(ledger.committed)).replace('{conflicts}', String(ledger.conflicts)).replace('{pending}', String(ledger.pending)).replace('{lost}', String(ledger.lost))}</p>}
      {queue.conflicts.length > 0 && <ul aria-label={t.conflicts}>{queue.conflicts.map(command => <li key={command}><code>{command.slice(0, 8)}</code> <Button variant="outline" disabled={busy || !online} onClick={() => void review(command)}>{t.review}</Button></li>)}</ul>}
      {conflict && <div role="region" aria-label={t.conflictHeading}>
        <h4>{t.conflictHeading}</h4><p>{t.conflictHelp}</p>
        <table><thead><tr><th scope="col">{t.field}</th><th scope="col">{t.captured}</th><th scope="col">{t.server}</th></tr></thead>
          <tbody>{[...textKeys, ...dateKeys].filter(key => conflict.captured[key] !== conflict.server[key]).map(key =>
            <tr key={key}><th scope="row">{t.fields[key]}</th><td>{conflict.captured[key] ?? t.notSet}</td><td>{conflict.server[key] ?? t.notSet}</td></tr>)}</tbody></table>
        <EnterpriseActions>{conflict.choices.includes('keepServer') && <Button variant="outline" disabled={busy || !online} onClick={() => void resolve('keepServer')}>{t.keepServer}</Button>}
          {conflict.choices.includes('useCaptured') && <Button disabled={busy || !online} onClick={() => void resolve('useCaptured')}>{t.useCaptured}</Button>}
          {conflict.choices.length === 0 && <p>{t.noChoice}</p>}</EnterpriseActions>
      </div>}
    </>}
    {status && <p role="status" aria-live="polite">{status}</p>}
  </section>
}
