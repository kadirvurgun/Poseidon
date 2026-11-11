import { localizedText } from '../i18n/messages'
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ApiError, session } from './api'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import { isGuid } from './identifiers'
import { observationOwner, observationPath, readObservations, sendObservation, validObservationScope,
  type ObservationAttempt, type ObservationScope, type ObservationSnapshot } from './observation-api'

type Source = { scope: string; kind: 'ready'; snapshot: ObservationSnapshot } | { scope: string; kind: 'loading' | 'denied' | 'unavailable' | 'other-owner' }
export function MaintenanceObservations({ vessel, asset, owner, en, onDirty, onUncertain, onBusy, onAccessLost, suspended = false }: {
  vessel: string; asset: string; owner: string; en: boolean
  onDirty: (value: boolean) => void; onUncertain: (value: boolean) => void
  onBusy?: (value: boolean) => void; onAccessLost?: () => void; suspended?: boolean
}) {
  // This instance retains its first owning scope. A caller must never rebind a
  // private draft by changing props; the parent keys clean context changes.
  const [bound] = useState<ObservationScope>(() => ({ owner: owner.toLowerCase(), vessel: vessel.toLowerCase(), asset: asset.toLowerCase() }))
  const epoch = useWorkspaceRevalidation(), [refresh, setRefresh] = useState(0)
  const [source, setSource] = useState(''), [value, setValue] = useState(''), [time, setTime] = useState(''), [revision, setRevision] = useState('')
  const [projection, setProjection] = useState<Source | null>(null), [dirty, setDirty] = useState(false)
  const [busy, setBusy] = useState(false), [unknown, setUnknown] = useState(false), [denied, setDenied] = useState(false)
  const [confirmed, setConfirmed] = useState<{ id: string; owner: string } | null>(null)
  const frozen = useRef<ObservationAttempt | null>(null), uncertain = useRef(false), running = useRef(false)
  const mounted = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef('')
  const callbacks = useRef({ onDirty, onUncertain, onBusy, onAccessLost })
  const ownerLossNotified = useRef(false)
  const sameScope = validObservationScope(bound) && owner.toLowerCase() === bound.owner && vessel.toLowerCase() === bound.vessel && asset.toLowerCase() === bound.asset
  const available = sameScope && !suspended && navigator.onLine && document.visibilityState !== 'hidden'
  const scope = `${bound.owner}/${bound.vessel}/${bound.asset}/${epoch}/${refresh}/${available}`
  const current = available && projection?.scope === scope ? projection : null
  const snapshot = current?.kind === 'ready' ? current.snapshot : null
  const editable = !!snapshot?.canWrite && !busy && !unknown && !denied && !confirmed
  useLayoutEffect(() => {
    callbacks.current = { onDirty, onUncertain, onBusy, onAccessLost }
    if (committed.current !== scope || !available) writer.current?.abort()
    committed.current = scope
  }, [scope, available, onDirty, onUncertain, onBusy, onAccessLost])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  const changeDirty = (next: boolean) => { setDirty(next); callbacks.current.onDirty(next) }
  const changeUnknown = (next: boolean) => { uncertain.current = next; setUnknown(next); callbacks.current.onUncertain(next) }

  useEffect(() => {
    if (!available) return
    const c = new AbortController(), timer = setTimeout(() => c.abort(), 15000)
    let active = true
    const freshOwner = async () => {
      const user = await session(c.signal)
      if (observationOwner(user) !== bound.owner) throw new ApiError(403, 'identity_changed')
    }
    async function load() {
      await freshOwner()
      const snapshot = await readObservations(bound, c.signal)
      await freshOwner()
      if (!active || c.signal.aborted || committed.current !== scope) return
      setProjection({ scope, kind: 'ready', snapshot })
      setDenied(false)
      ownerLossNotified.current = false
    }
    load().catch(problem => {
      if (!active || c.signal.aborted || committed.current !== scope) return
      const code = problem instanceof ApiError ? problem.status : 0
      setProjection({ scope, kind: problem instanceof ApiError && problem.code === 'identity_changed' ? 'other-owner' : [401, 403, 404].includes(code) ? 'denied' : 'unavailable' })
      if (problem instanceof ApiError && problem.code === 'identity_changed' && !ownerLossNotified.current) {
        ownerLossNotified.current = true
        callbacks.current.onAccessLost?.()
      }
    }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [available, bound, scope])

  const run = async (revokeId?: string) => {
    if (!available || !snapshot?.canWrite || running.current || busy || denied || confirmed || !frozen.current && !editable) return
    let command = frozen.current
    if (!command) {
      const selected = snapshot.sources.find(item => item.source === source)
      if (revokeId ? !isGuid(revokeId) || !snapshot.items.some(item => item.id === revokeId && !item.revoked)
        : !selected || !/^[1-9][0-9]*$/.test(revision) || !Number.isSafeInteger(Number(revision))) return
      const key = crypto.randomUUID(), id = revokeId ?? key
      command = { ...bound, key, id, kind: revokeId ? 'revoke' : 'capture',
        path: revokeId ? `${observationPath(bound)}/${encodeURIComponent(revokeId)}/revoke` : observationPath(bound),
        body: JSON.stringify(revokeId ? {} : { id, source, policyVersion: selected!.version, sourceRevision: Number(revision),
          kind: selected!.kind, unit: selected!.unit, value: value || null, observedAt: time || null }) }
    }
    const originalScope = scope, priorUnknown = uncertain.current
    const active = () => mounted.current && committed.current === originalScope
    const c = new AbortController(), timer = setTimeout(() => c.abort(), 15000)
    writer.current = c; running.current = true; frozen.current = command
    setBusy(true); callbacks.current.onBusy?.(true); changeDirty(true); setDenied(false)
    let sent = false, received = false, accepted = false
    const ensureOwner = async () => {
      const user = await session(c.signal)
      if (!active() || c.signal.aborted) throw new ApiError(0, 'obsolete_scope')
      if (observationOwner(user) !== command.owner) throw new ApiError(403, 'identity_changed')
      return user
    }
    try {
      await ensureOwner()
      const access = await readObservations(bound, c.signal)
      const fresh = await ensureOwner()
      if (!access.canWrite) throw new ApiError(403, 'permission_denied')
      // Only a frozen retry may bypass changed *local* form inputs. The server
      // still rechecks its current policy before UUID/body equality replay.
      sent = true
      const result = await sendObservation(command, fresh.csrfToken, c.signal)
      received = true
      await ensureOwner()
      if (!active()) throw new ApiError(0, 'obsolete_scope')
      accepted = true; frozen.current = null; changeUnknown(false); changeDirty(false)
      setConfirmed({ id: result.id, owner: command.owner })
      // Keep the confirmation read-only until a fresh owning GET completes.
      setProjection(null); setRefresh(n => n + 1)
    } catch (problem) {
      if (!mounted.current) return
      const status = problem instanceof ApiError ? problem.status : 0
      const outcomeUnknown = !accepted && (priorUnknown || received || sent && (status === 0 || status >= 500))
      frozen.current = outcomeUnknown ? command : null
      changeUnknown(outcomeUnknown)
      if (active()) {
        setDenied(!outcomeUnknown)
        setProjection({ scope: originalScope, kind: problem instanceof ApiError && problem.code === 'identity_changed' ? 'other-owner' : [401, 403, 404].includes(status) ? 'denied' : 'unavailable' })
        if (problem instanceof ApiError && problem.code === 'identity_changed' || [401, 403, 404].includes(status)) {
          callbacks.current.onAccessLost?.()
        }
      }
    } finally {
      clearTimeout(timer); running.current = false; writer.current = null
      if (mounted.current) { setBusy(false); callbacks.current.onBusy?.(false) }
    }
  }
  const reload = () => { if (!running.current && available) setRefresh(n => n + 1) }
  const discard = () => {
    if (running.current || uncertain.current || !available) return
    setSource(''); setValue(''); setTime(''); setRevision(''); setDenied(false); setConfirmed(null); frozen.current = null
    changeDirty(false); reload()
  }
  // No private fields or outcome IDs survive into a withdrawn/replacement view.
  if (!available) return null
  const accessLost = current?.kind === 'denied' || current?.kind === 'other-owner'
  const actions = <EnterpriseActions align="start" aria-label={localizedText(en, 'pairs.m_79b34eda155292bd')}>
    <Button variant="outline" disabled={busy} onClick={reload}>{localizedText(en, 'pairs.m_542619365a11df9d')}</Button>
    <Button variant="outline" disabled={busy || unknown || !dirty && !confirmed} onClick={discard}>{localizedText(en, 'pairs.m_301d18c5de7d235b')}</Button>
  </EnterpriseActions>
  if (!snapshot) return <section aria-label={localizedText(en, 'pairs.m_e08597ac7f99df73')}>
    {current?.kind === 'other-owner' ? <p role="alert">{localizedText(en, 'pairs.m_1110a2b47b9eb7dd')}</p>
      : <p role={accessLost || current?.kind === 'unavailable' ? 'alert' : 'status'}>{localizedText(en, accessLost ? 'pairs.m_c12f4ab7b5b4e04d' : current?.kind === 'unavailable' ? 'pairs.m_598312c777f972b4' : 'pairs.m_2eb8858991de8dd9')}</p>}
    {confirmed && !accessLost && current?.kind === 'unavailable' && <p role="status">{localizedText(en, 'pairs.m_79054af08f971230')}</p>}
    {unknown && <p role="alert">{localizedText(en, 'pairs.m_62930366e47b200e')}</p>}
    {actions}
  </section>
  const selected = snapshot.sources.find(item => item.source === source)
  // A current same-owner GET alone restores editability after confirmation;
  // clearing a confirmed form is an explicit action, never a fresh POST.
  return <section aria-label={localizedText(en, 'pairs.m_e08597ac7f99df73')}><h3>{localizedText(en, 'pairs.m_e08597ac7f99df73')}</h3>
    <p>{localizedText(en, 'pairs.m_be21a46099a0c3f2')}</p>
    {confirmed && <div role="status"><p>{localizedText(en, 'pairs.m_79054af08f971230')}</p><code>{confirmed.id}</code></div>}
    {!snapshot.sources.length && <p>{localizedText(en, 'pairs.m_0c6beadce286b699')}</p>}
    <ul>{snapshot.items.map(item => <li key={item.id}>{item.source} · {localizedText(en, item.kind === 'Meter' ? 'pairs.m_896af24280cadb66' : 'pairs.m_6867907ad1d51eb8')} · {item.value ?? localizedText(en, 'pairs.m_a5bd35d90de7c160')} {item.unit} · {item.observedAt ?? localizedText(en, 'pairs.m_691ebb5fec500494')} · {localizedText(en, item.revoked ? 'pairs.m_ed83bf5770ddaa2a' : 'pairs.m_b980704b1fa89cd9')}
      <code>{item.id}</code>{snapshot.canWrite && !item.revoked && <Button variant="outline" size="sm" disabled={!editable} onClick={() => void run(item.id)}>{localizedText(en, 'pairs.m_3c2e6369288f5c4c')}</Button>}</li>)}</ul>
    {snapshot.canWrite && snapshot.sources.length > 0 && <form onSubmit={event => { event.preventDefault(); void run() }}><fieldset disabled={!editable}>
      <label>{localizedText(en, 'pairs.m_aa06fe636f46b76e')}<select required value={source} onChange={event => { setSource(event.target.value); setValue(''); changeDirty(true) }}><option value="">{localizedText(en, 'pairs.m_01b23fcd15621f8f')}</option>{snapshot.sources.map(item => <option key={item.source} value={item.source}>{item.source} · {item.unit} · {item.version}</option>)}</select></label>
      <label>{localizedText(en, 'pairs.m_b307fec7870b283f')}<input required inputMode="numeric" value={revision} onChange={event => { setRevision(event.target.value); changeDirty(true) }}/></label>
      <label>{localizedText(en, 'pairs.m_fb01fb6b8a066134')}{selected?.kind === 'Condition' ? <select value={value} onChange={event => { setValue(event.target.value); changeDirty(true) }}><option value="">{localizedText(en, 'pairs.m_a5a7675bd7000dbd')}</option><option value="True">{localizedText(en, 'pairs.m_129a7406352aa95e')}</option><option value="False">{localizedText(en, 'pairs.m_f2b1c53b82a3e806')}</option></select> : <input maxLength={1024} value={value} onChange={event => { setValue(event.target.value); changeDirty(true) }}/>}</label>
      <label>{localizedText(en, 'pairs.m_1ba7a501acd86ead')}<input value={time} onChange={event => { setTime(event.target.value); changeDirty(true) }}/></label>
    </fieldset><EnterpriseActions layout="form" aria-label={localizedText(en, 'pairs.m_79b34eda155292bd')}><Button type="submit" disabled={!editable}>{localizedText(en, 'pairs.m_c57f0b5c072259d7')}</Button></EnterpriseActions></form>}
    {unknown && <p role="alert">{localizedText(en, 'pairs.m_62930366e47b200e')}</p>}
    {denied && <p role="alert">{localizedText(en, 'pairs.m_c12f4ab7b5b4e04d')}</p>}
    {unknown && <EnterpriseActions align="start" aria-label={localizedText(en, 'pairs.m_79b34eda155292bd')}><Button variant="outline" disabled={busy || !snapshot.canWrite} onClick={() => void run()}>{localizedText(en, 'pairs.m_7231b768ded13bd5')}</Button></EnterpriseActions>}
    {actions}
  </section>
}
