import { type Locale as AppLocale } from '../i18n'
import { useEffect, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { validEvidence, type Evidence, type ExecutionState } from './work-order-execution-api'
import { projectWorkOrderQueue, WorkOrderOfflineApplication, type WorkOrderConflict, type WorkOrderPending } from './work-order-offline'
import { EvidenceEditor, type ExecutionCopy } from './WorkOrderExecutionPanel'

// DEV-057 / T-S8-006 offline evidence capture. Captures are sealed into the encrypted device queue
// BEFORE any network attempt; reconnect first reconciles receipts (DEV-034) and then replays the exact
// frozen bodies with their original keys. A material conflict is never auto-resolved here: it is listed
// in the conflict center and resolved only by an explicit human keep-server/use-captured choice.
export function WorkOrderOfflinePanel({ vessel, workOrder, state, language, t, onSynced }: { vessel: string; workOrder: string; state: ExecutionState | null
  language: AppLocale; t: ExecutionCopy; onSynced?: () => void }) {
  const [app, setApp] = useState<WorkOrderOfflineApplication | null>(null), [rows, setRows] = useState<WorkOrderPending[]>([])
  const [draft, setDraft] = useState<Evidence | null>(null), [status, setStatus] = useState<string | null>(null), [busy, setBusy] = useState(false)
  const [conflict, setConflict] = useState<WorkOrderConflict | null>(null), [online, setOnline] = useState(() => navigator.onLine)
  const handle = useRef<WorkOrderOfflineApplication | null>(null)
  useEffect(() => {
    const on = () => setOnline(true), off = () => setOnline(false)
    window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off); handle.current?.close() }
  }, [])
  const refresh = async (value: WorkOrderOfflineApplication) => setRows(await value.captures())
  const guard = async (action: () => Promise<void>) => {
    setBusy(true); setStatus(null)
    try { await action() }
    catch (error) {
      const message = error instanceof Error ? error.message : ''
      setStatus(message === 'reauthorization_required_data_retained' ? t.offlineReauthorize : message === 'key_lost_data_retained' ? t.offlineKeyLost : t.offlineFailed)
    } finally { setBusy(false) }
  }
  const prepare = () => guard(async () => {
    const c = new AbortController(); const value = await WorkOrderOfflineApplication.authorize(vessel, workOrder, null, c.signal)
    handle.current?.close(); handle.current = value; setApp(value); setDraft(structuredClone(value.context.baseEvidence)); await refresh(value); setStatus(t.offlinePrepared)
  })
  const capture = () => app && draft && guard(async () => { await app.capture(draft); await refresh(app); setStatus(t.offlineQueued) })
  const sync = () => app && guard(async () => {
    const c = new AbortController()
    await app.reconcile(c.signal)
    for (const row of (await app.captures()).filter(r => !r.outcome)) await app.replay(row, c.signal)
    await refresh(app); setStatus(t.offlineSynced); onSynced?.()
  })
  const review = (commandId: string) => app && guard(async () => { setConflict(await app.conflict(commandId, new AbortController().signal)) })
  const resolve = (choice: 'keepServer' | 'useCaptured') => app && conflict && guard(async () => {
    const id = await app.capture(null, { conflict, choice }); const row = (await app.captures()).find(r => r.commandId === id)
    if (row) await app.replay(row, new AbortController().signal)
    setConflict(null); await refresh(app); onSynced?.()
  })
  const queue = projectWorkOrderQueue(rows)
  return <section className="work-order-offline" aria-label={t.offlineHeading} aria-busy={busy}>
    <h3>{t.offlineHeading}</h3>
    <p className="work-order-hint">{t.offlineIntro}</p>
    {status && <p role="status">{status}</p>}
    {!app ? <EnterpriseActions align="start" aria-label={t.offlineHeading}>
      <Button type="button" variant="outline" disabled={busy || !online || state !== 'IN_PROGRESS'} onClick={() => void prepare()}>{t.offlinePrepare}</Button>
      {state !== 'IN_PROGRESS' && <span className="work-order-hint">{t.offlineRequiresInProgress}</span>}
    </EnterpriseActions> : <>
      <p><Badge tone={queue.pending ? 'warning' : 'neutral'}>{t.offlinePending}: {queue.pending}</Badge>{' '}
        {queue.conflicts.length > 0 && <Badge tone="danger">{t.offlineConflicts}: {queue.conflicts.length}</Badge>}</p>
      {draft && <form onSubmit={e => { e.preventDefault(); void capture() }}>
        <EvidenceEditor value={draft} onChange={setDraft} disabled={busy} vessel={vessel} owner={app.context.actorId}
          spares={app.context.spareOptions.map(s => ({ id: s.id, revision: s.revision, label: `${s.partNumber} · ${s.name} (${s.unit})` }))} language={language} t={t}/>
        <EnterpriseActions aria-label={t.offlineCapture}><Button type="submit" disabled={busy || !validEvidence(draft)}>{t.offlineCapture}</Button>
          <Button type="button" variant="outline" disabled={busy || !online || queue.pending === 0} onClick={() => void sync()}>{t.offlineSync}</Button></EnterpriseActions>
      </form>}
      {queue.conflicts.length > 0 && <div role="note"><p>{t.offlineConflictNote} <a href={`/app/offline-conflict-center/${vessel}`}>{t.offlineConflictCenter}</a></p>
        <ul>{queue.conflicts.map(id => <li key={id}><code>{id.slice(0, 8)}</code> <Button type="button" variant="outline" size="sm" disabled={busy || !online} onClick={() => void review(id)}>{t.offlineReview}</Button></li>)}</ul></div>}
      {conflict && <section aria-label={t.offlineReview} className="work-order-offline__conflict">
        <p>{t.offlineServerValue}: {conflict.server.readings.map(r => `${r.code}=${r.value}`).join(', ') || '—'}</p>
        <p>{t.offlineCapturedValue}: {conflict.captured.readings.map(r => `${r.code}=${r.value}`).join(', ') || '—'}</p>
        {conflict.choices.length === 0 ? <p>{t.offlineResolvedOrNotHuman}</p> : <EnterpriseActions align="start" aria-label={t.offlineReview}>
          {conflict.choices.includes('keepServer') && <Button type="button" variant="outline" disabled={busy} onClick={() => void resolve('keepServer')}>{t.offlineKeepServer}</Button>}
          {conflict.choices.includes('useCaptured') && <Button type="button" disabled={busy} onClick={() => void resolve('useCaptured')}>{t.offlineUseCaptured}</Button>}
        </EnterpriseActions>}
      </section>}
      <EnterpriseActions align="start" aria-label={t.offlineRevoke}><Button type="button" variant="ghost" disabled={busy || !online} onClick={() => void guard(async () => { await app.revoke(false, new AbortController().signal); setApp(null); handle.current = null; setStatus(t.offlineRevoked) })}>{t.offlineRevoke}</Button></EnterpriseActions>
    </>}
  </section>
}
