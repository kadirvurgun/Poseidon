import { localizedCopy } from '../i18n/messages'
import { pilotForVessel, pilotSyncConflict } from './pilot-telemetry'
import { useAppLanguage, type Locale } from '../i18n'
import { useEffect, useId, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns'
import * as api from './api'
import { isGuid } from './identifiers'
import { decodeTodayVessels } from './today-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import { conflictPageSize, loadSyncConflicts, type SyncConflictItem, type SyncConflictPage } from './sync-conflict-center-api'
import { classifyReviewFailure, openCrewConflictReview, type ConflictReviewFailure, type ConflictReviewSession, type ResolutionResult } from './conflict-center-resolver'
import { OfflineConflictReview, type OfflineConflictResolution, type OfflineConflictValue } from './OfflineConflictReview'
import './offline-enterprise.css'
import './product.css'
import './conflict-center.css'

// DEV-035 / T-S4-003 screen offline-conflict-center. Lists the actor's own recorded material conflicts
// for one selected vessel and opens an explicit, reasoned human resolution. Nothing is auto-resolved,
// no choice is preselected, a local change is never silently overwritten or discarded, and no
// "saved" message appears before an exact decoded server receipt.
const copy = localizedCopy('ConflictCenter_tsx_copy')
type Copy = (typeof copy)[Locale]
export const conflictCenterPath = '/app/offline-conflict-center'
function selection() {
  const query = new URLSearchParams(window.location.search), values = query.getAll('vessel')
  if ([...query.keys()].some(key => key !== 'vessel') || values.length > 1 || values.length === 1 && !isGuid(values[0])) return { invalid: true, vessel: '' }
  return { invalid: false, vessel: values[0]?.toLowerCase() ?? '' }
}
function useConnectivity() {
  const [state, setState] = useState(() => ({ online: navigator.onLine, visible: document.visibilityState !== 'hidden' }))
  useEffect(() => {
    const update = () => setState({ online: navigator.onLine, visible: document.visibilityState !== 'hidden' })
    window.addEventListener('online', update); window.addEventListener('offline', update); document.addEventListener('visibilitychange', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update); document.removeEventListener('visibilitychange', update) }
  }, [])
  return state
}

export function ConflictCenterWorkspace() {
  const [language, setLanguage] = useAppLanguage(), t = copy[language]
  const [route] = useState(selection), [vessel, setVessel] = useState(route.vessel), [attempt, retry] = useState(0)
  const epoch = useWorkspaceRevalidation(), { online, visible } = useConnectivity()
  const [identity, setIdentity] = useState<{ epoch: number; attempt: number; user?: api.Session; error?: boolean } | null>(null)
  const [vessels, setVessels] = useState<{ epoch: number; rows?: api.Page<api.Vessel>; error?: boolean } | null>(null)
  const currentIdentity = identity?.epoch === epoch && identity.attempt === attempt ? identity : null
  const user = currentIdentity?.user ?? null
  // The last authorized identity keys the conflict list so a background revalidation does not
  // withdraw an in-progress human review; a different actor/tenant gets a fresh component scope.
  const [owner, setOwner] = useState<api.Session | null>(null)
  if (user?.authenticated && user.canRead && owner !== user) setOwner(user)
  useEffect(() => {
    if (route.invalid || !online || !visible) return
    const c = new AbortController(); let active = true
    api.session(c.signal).then(value => { if (active) setIdentity({ epoch, attempt, user: value }) })
      .catch(() => { if (active) setIdentity({ epoch, attempt, error: true }) })
    return () => { active = false; c.abort() }
  }, [route.invalid, epoch, attempt, online, visible])
  useEffect(() => {
    if (!user?.authenticated || !user.canRead || !online || !visible) return
    const c = new AbortController(); let active = true
    api.vessels(0, '', c.signal).then(rows => decodeTodayVessels(rows, 0)).then(rows => { if (active) setVessels({ epoch, rows }) })
      .catch(() => { if (active) setVessels({ epoch, error: true }) })
    return () => { active = false; c.abort() }
  }, [user, epoch, online, visible])
  useEffect(() => { document.title = `POSEIDON · ${t.title}` }, [t.title])
  const stable = owner
  const authorized = !!stable && (!user || user.actorId === stable.actorId && user.tenantId === stable.tenantId) && (!currentIdentity || !!user?.authenticated && !!user.canRead)
  return <div className="product-shell enterprise-shell">
    <EnterpriseChrome session={user} vessel={vessel || null} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => { committed?.(); window.location.assign(href) }} title={t.title} mainId="conflict-center-main"/>
    <main id="conflict-center-main" tabIndex={-1}>
      <EnterprisePageHeader title={t.title} eyebrow={t.eyebrow} description={t.description} actions={<Button variant="outline" disabled={!online} onClick={() => retry(n => n + 1)}>{t.refresh}</Button>}/>
      {route.invalid ? <p role="alert">{t.invalidRoute}</p>
        : !online ? <p role="status">{t.offline}</p>
        : !visible ? <p role="status">{t.hidden}</p>
        : currentIdentity?.error && !stable ? <p role="alert">{t.sessionError}</p>
        : !stable && !currentIdentity ? <p role="status">{t.sessionLoading}</p>
        : !authorized ? <p role="alert">{t.denied}</p>
        : <>
          <EnterpriseToolbar><label>{t.vessel}<select value={vessel} onChange={event => { const id = event.target.value; setVessel(id); history.replaceState(null, '', `${conflictCenterPath}${id ? '?vessel=' + id : ''}`) }}>
            <option value="">{t.chooseVessel}</option>
            {vessel && !vessels?.rows?.items.some(row => row.id === vessel) && <option value={vessel}>{vessel}</option>}
            {vessels?.rows?.items.filter(row => isGuid(row.id)).map(row => <option key={row.id} value={row.id}>{row.code} · {row.name}</option>)}
          </select></label></EnterpriseToolbar>
          {vessels?.error && <p role="alert">{t.vesselsError}</p>}
          {vessel ? <ConflictList key={`${stable!.tenantId}:${stable!.actorId}:${vessel}`} vessel={vessel} language={language} refresh={attempt} online={online}/> : <p role="status">{t.selectVessel}</p>}
        </>}
    </main>
  </div>
}

function when(value: string | null, t: Copy, language: Locale) {
  if (value === null) return t.unknownTime
  const parsed = Date.parse(value)
  return Number.isFinite(parsed) ? <time dateTime={value}>{new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(parsed)} UTC</time> : t.unknownTime
}

function ConflictList({ vessel, language, refresh, online }: { vessel: string; language: Locale; refresh: number; online: boolean }) {
  const t = copy[language], [offset, setOffset] = useState(0), [reload, setReload] = useState(0)
  const [page, setPage] = useState<{ key: string; data?: SyncConflictPage; error?: number } | null>(null)
  const [review, setReview] = useState<SyncConflictItem | null>(null)
  const key = `${offset}:${refresh}:${reload}`
  useEffect(() => {
    if (!online) return
    const c = new AbortController(); let active = true
    loadSyncConflicts(vessel, offset, c.signal).then(data => { if (active) setPage({ key, data }) })
      .catch(error => { if (active) setPage({ key, error: error instanceof api.ApiError ? error.status : 0 }) })
    return () => { active = false; c.abort() }
  }, [vessel, offset, key, online])
  const current = page?.key === key ? page : null
  const label = (item: SyncConflictItem) => item.source === 'dailyRecord' && item.resourceKind ? `${t.sources[item.source]} · ${t.dailyKinds[item.resourceKind as keyof Copy['dailyKinds']] ?? item.resourceKind}` : t.sources[item.source]
  if (!current) return <p role="status" aria-live="polite">{t.loading}</p>
  if (!current.data) return <div role="alert"><p>{[401, 403, 404].includes(current.error ?? 0) ? t.listDenied : t.listError}</p><Button variant="outline" disabled={!online} onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>
  const data = current.data, excluded = data.sources.filter(s => s.state !== 'Included')
  return <section aria-label={t.listLabel} className="conflict-center">
    <p className="conflict-center__meta">{t.readAt}: {when(data.readAt, t, language)}</p>
    {excluded.length > 0 && <p role="note">{t.notIncluded} {excluded.map(s => `${t.sources[s.source]} (${t.sourceStates[s.state]})`).join(', ')}</p>}
    {data.items.length === 0 ? <p role="status">{offset ? t.emptyPage : t.empty}</p> : <div className="conflict-center__table" tabIndex={0}><table>
      <caption className="sr-only">{t.listLabel}</caption>
      <thead><tr><th scope="col">{t.columns.source}</th><th scope="col">{t.columns.resource}</th><th scope="col">{t.columns.versions}</th><th scope="col">{t.columns.captured}</th><th scope="col">{t.columns.received}</th><th scope="col">{t.columns.state}</th><th scope="col">{t.columns.action}</th></tr></thead>
      <tbody>{data.items.map(item => <tr key={`${item.enrollmentId}/${item.commandId}`} data-state={item.state}>
        <th scope="row">{label(item)}</th>
        <td><code>{item.resourceId}</code></td>
        <td>{t.baseVersion} {item.baseVersion} → {t.serverVersion} {item.serverVersionAtConflict}</td>
        <td>{when(item.capturedAt, t, language)}</td>
        <td>{when(item.receivedAt, t, language)}</td>
        <td><Badge tone={item.state === 'Open' ? 'warning' : 'neutral'}>{t.states[item.state]}</Badge></td>
        <td>{item.state === 'Open' ? <Button variant="outline" size="sm" disabled={!online || review !== null} aria-label={`${t.review}: ${label(item)} ${item.commandId}`} onClick={() => setReview(item)}>{t.review}</Button> : <span>{t.noAction}</span>}</td>
      </tr>)}</tbody>
    </table></div>}
    <EnterprisePagination label={t.pages} previous={t.previous} next={t.next} previousDisabled={!offset || review !== null} nextDisabled={!data.hasMore || offset >= 1000 || review !== null}
      onPrevious={() => setOffset(n => Math.max(0, n - conflictPageSize))} onNext={() => setOffset(n => n + conflictPageSize)}/>
    {review && <ConflictReviewPane key={`${review.enrollmentId}/${review.commandId}`} vessel={vessel} item={review} language={language} online={online}
      onClose={changed => { setReview(null); if (changed) setReload(n => n + 1) }}/>}
  </section>
}

type Operation = 'idle' | 'submitting' | 'outcomeUnknown' | 'failed'
type Receipt = ResolutionResult | { kind: 'discarded' } | { kind: 'serverChanged' }
const asValue = (value: { kind: string; text?: string } | undefined): OfflineConflictValue =>
  value?.kind === 'known' && typeof value.text === 'string' ? { kind: 'known', text: value.text } : value?.kind === 'withheld' ? { kind: 'withheld' } : value?.kind === 'missing' ? { kind: 'missing' } : { kind: 'unknown' }

function ConflictReviewPane({ vessel, item, language, online, onClose }: { vessel: string; item: SyncConflictItem; language: Locale; online: boolean; onClose: (changed: boolean) => void }) {
  const t = copy[language], heading = useId(), confirmId = useId()
  const [state, setState] = useState<{ session?: ConflictReviewSession; failure?: ConflictReviewFailure } | null>(null)
  const [operation, setOperation] = useState<Operation>('idle'), [receipt, setReceipt] = useState<Receipt | null>(null)
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const session = useRef<ConflictReviewSession | null>(null), region = useRef<HTMLElement>(null)
  const supported = item.source === 'crewAssignment'
  useEffect(() => {
    if (!supported) return
    const c = new AbortController(); let active = true
    openCrewConflictReview(vessel, item, c.signal).then(value => {
      if (!active) { value.close(); return }
      session.current = value; setState({ session: value })
      // A retained, unconfirmed resolution from an earlier attempt or reload is never re-decided or duplicated.
      if (value.pendingResolution) setOperation('outcomeUnknown')
    }).catch(error => { if (active) setState({ failure: classifyReviewFailure(error) }) })
    return () => { active = false; c.abort(); session.current?.close(); session.current = null }
  }, [vessel, item, supported])
  useEffect(() => { region.current?.focus() }, [])
  const run = async (action: (s: ConflictReviewSession, signal: AbortSignal) => Promise<Receipt>) => {
    const s = session.current; if (!s || operation === 'submitting') return
    const c = new AbortController(); setOperation('submitting')
    try { setReceipt(await action(s, c.signal)); setOperation('idle') }
    catch (error) {
      if (error instanceof api.ApiError && error.status === 409 && error.code === 'offline_conflict_changed') {
        setOperation('idle'); setReceipt({ kind: 'serverChanged' }); return
      }
      // Lost response / dropped network after the frozen command was persisted: outcome is UNKNOWN, not failed or saved.
      setOperation(s.pendingResolution ? 'outcomeUnknown' : 'failed')
      if (classifyReviewFailure(error) === 'denied') setState({ failure: 'denied' })
    }
  }
  // S11 gate B1 (DEV-072, 19.01 p154-155): one SYNC_CONFLICT observation per conflict a human resolved here
  // (committed resolution or explicit local discard). Only the closed code and the vessel's mode are reported.
  const observed = <T extends Receipt>(receipt: T): T => { if (receipt.kind === 'committed' || receipt.kind === 'discarded') pilotForVessel(vessel, pilotSyncConflict); return receipt }
  const resolve = (resolution: OfflineConflictResolution) => void run(async (s, signal) => observed(await s.resolve(resolution, signal)))
  const recheck = () => void run((s, signal) => s.recheck(signal))
  const discard = () => void run(async s => { await s.discardLocal(confirmDiscard); return observed({ kind: 'discarded' } as const) })
  const detail = state?.session?.detail
  const conflict = detail ? { ...detail, fields: detail.fields.map(field => field.key === 'watch_group' ? { ...field, before: asValue(detail.base) } : field) } : null
  return <section ref={region} tabIndex={-1} className="conflict-center__review" aria-labelledby={heading} onKeyDown={event => { if (event.key === 'Escape' && operation !== 'submitting' && operation !== 'outcomeUnknown') onClose(receipt !== null) }}>
    <h2 id={heading}>{t.reviewHeading}</h2>
    {!supported ? <p role="status">{t.ownerOnly}</p>
      : !state ? <p role="status" aria-live="polite">{t.opening}</p>
      : state.failure ? <p role="alert">{state.failure === 'otherDevice' ? t.otherDevice : state.failure === 'denied' ? t.reviewDenied : t.reviewError}</p>
      : receipt ? <div role="status" aria-live="polite">
          {receipt.kind === 'committed' ? <p>{t.resolved} <code>{receipt.outcome.commandId}</code>. {t.receiptNotApproval}</p>
            : receipt.kind === 'conflictAgain' ? <p>{t.conflictAgain}</p> : receipt.kind === 'serverChanged' ? <p>{t.serverChanged}</p> : <p>{t.discarded}</p>}
        </div>
      : conflict && <>
        <OfflineConflictReview language={language} contextKey={conflict.contextKey} state="ready" conflict={conflict}
          canResolve={detail!.resolved === null && detail!.currentVersion === Number(detail!.revision) && !state.session!.pendingResolution}
          online={online} operation={operation} reasonLimit={500} onResolve={resolve} onCheckOutcome={recheck}/>
        <fieldset className="conflict-center__discard" disabled={!online || operation === 'submitting' || operation === 'outcomeUnknown' || !state.session!.local}>
          <legend>{t.discardHeading}</legend>
          <p>{state.session!.local ? t.discardHelp : t.noLocal}</p>
          <label htmlFor={confirmId} className="offline-conflict__choice"><input id={confirmId} type="checkbox" checked={confirmDiscard} onChange={event => setConfirmDiscard(event.target.checked)}/>{t.discardConfirm}</label>
          <Button variant="outline" disabled={!confirmDiscard || !state.session!.local} onClick={discard}>{t.discard}</Button>
        </fieldset>
      </>}
    <EnterpriseActions><Button variant="ghost" disabled={operation === 'submitting' || operation === 'outcomeUnknown'} onClick={() => onClose(receipt !== null)}>{t.close}</Button></EnterpriseActions>
  </section>
}
