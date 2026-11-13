import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { ApiError } from './api'
import * as api from './human-review-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

type Binding = Readonly<{ owner: string; context: string; authority?: api.Authority }>
export type HumanAttempt = Readonly<Binding & Parameters<typeof api.save>[1] & {
  unknown: boolean; authority: api.Authority; nextRevision: number
}>
type Confirmed = Binding & { row: api.Review; authority: api.Authority }
type Props = { anchor: api.Anchor; owner?: string | null; offline: boolean; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void }
const status = (problem: unknown) => problem instanceof ApiError ? problem.status : 0
const sameAuthority = (left: api.Authority, right: api.Authority) => left.id === right.id && left.roleId === right.roleId &&
  left.operationId === right.operationId && left.capability === right.capability && left.validUntil === right.validUntil &&
  (left.recording === null ? right.recording === null : right.recording !== null &&
    (Object.keys(left.recording) as (keyof api.RecordingPolicy)[]).every(key => left.recording![key] === right.recording![key]))

// In-memory recovery only. A denied replay cannot disprove an earlier commit.
// UUID belongs to the idempotency key; new stream identity comes from the server.
export function useHumanReviewRecovery({ anchor, owner: currentOwner, offline, onDirty, onSaving }: Props) {
  const { vesselId, evidenceId, evidenceRevision, controlId, controlRevision } = anchor
  const stableAnchor = useMemo(() => ({ vesselId, evidenceId, evidenceRevision, controlId, controlRevision }), [vesselId, evidenceId, evidenceRevision, controlId, controlRevision])
  const epoch = useWorkspaceRevalidation(), context = api.tracePath(stableAnchor)
  const [reload, setReload] = useState(0), [offset, setOffset] = useState(0), [history, setHistory] = useState('')
  const available = !offline && navigator.onLine && document.visibilityState !== 'hidden' && currentOwner !== null
  const scope = `${context}/${currentOwner ?? ''}/${available}/${epoch}/${reload}/${offset}/${history}`
  const [data, setData] = useState<api.Trace | null>(null), [trusted, setTrusted] = useState<{ scope: string; owner: string } | null>(null)
  const [observed, setObserved] = useState<{ scope: string; owner: string } | null>(null)
  const [binding, setBinding] = useState<Binding | null>(null), [attempt, setAttempt] = useState<HumanAttempt | null>(null)
  const [confirmed, setConfirmed] = useState<Confirmed | null>(null)
  const [busy, setBusy] = useState(false), [error, setError] = useState<number | null>(null), [conflict, setConflict] = useState(false)
  const mounted = useRef(false), running = useRef(false), live = useRef(''), writer = useRef<AbortController | null>(null)
  const frozen = useRef<HumanAttempt | null>(null), draft = useRef<Binding | null>(null), receipt = useRef<Confirmed | null>(null)
  const callbacks = useRef({ onDirty, onSaving })
  const owner = observed?.scope === scope ? observed.owner : null
  const bound = !binding || binding.context === context && binding.owner === owner
  const ownerReady = available && !!owner && trusted?.scope === scope && trusted.owner === owner && bound && !!data
  const authority = attempt?.authority ?? binding?.authority ?? confirmed?.authority
  const canShow = ownerReady && (!authority || !!data?.authorities.some(grant => sameAuthority(grant, authority)))
  const canDiscard = !busy && !attempt && (ownerReady || !available && !!binding && currentOwner === binding.owner)
  const locked = !canShow || busy || conflict || !!history || !!attempt || !!confirmed
  useLayoutEffect(() => {
    callbacks.current = { onDirty, onSaving }
    if (live.current !== scope || !available) writer.current?.abort()
    live.current = scope
  }, [scope, available, onDirty, onSaving])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => { onDirty(!!binding || !!attempt) }, [binding, attempt, onDirty])
  // Existing parents treat onSaving as a navigation barrier. Keep it after a
  // lost response and while a private draft cannot be shown to its original owner.
  useEffect(() => { onSaving(busy || !!attempt || !!binding && !canShow) }, [busy, attempt, binding, canShow, onSaving])
  const valid = useCallback((captured: string, signal: AbortSignal) => mounted.current && live.current === captured && !signal.aborted, [])
  const keep = (value: HumanAttempt | null) => { frozen.current = value; setAttempt(value) }
  const bind = (value: Binding | null) => { draft.current = value; setBinding(value) }
  const confirm = useCallback((value: Confirmed | null) => { receipt.current = value; setConfirmed(value) }, [])
  const fresh = useCallback(async (captured: string, signal: AbortSignal, expected?: Binding | null) => {
    const user = await api.loadSession(signal)
    if (!valid(captured, signal)) throw new ApiError(0, 'obsolete_human_scope')
    setObserved({ scope: captured, owner: user.owner })
    if (currentOwner !== undefined && currentOwner !== user.owner || expected && (expected.owner !== user.owner || expected.context !== context))
      throw new ApiError(403, 'human_owner_changed')
    return user
  }, [context, currentOwner, valid])
  useEffect(() => {
    if (!available) return
    const c = new AbortController(), captured = scope
    let active = true
    const timer = setTimeout(() => c.abort(), 15000)
    async function read() {
      const expected = frozen.current ?? draft.current ?? receipt.current
      const before = await fresh(captured, c.signal, expected)
      const page = await api.trace(stableAnchor, offset, history, c.signal)
      await fresh(captured, c.signal, { owner: before.owner, context })
      if (!active || !valid(captured, c.signal)) return
      if (expected?.authority && !page.authorities.some(grant => sameAuthority(grant, expected.authority!)))
        throw new ApiError(403, 'human_authority_required')
      setData(page); setTrusted({ scope: captured, owner: before.owner }); setError(null)
      // Reading a current projection does not change the retained command or
      // replace a captured confirmation with a new editable request.
      if (!frozen.current && !draft.current && !receipt.current) setConflict(false)
    }
    read().catch(problem => {
      if (active && mounted.current && live.current === captured) { setData(null); setTrusted(null); setError(status(problem)) }
    }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [stableAnchor, context, scope, available, offset, history, fresh, valid])
  function edit(authority?: api.Authority) {
    if (locked || running.current || frozen.current || receipt.current || !owner) return false
    if (!draft.current || authority) bind({ owner, context, ...(authority ? { authority } : draft.current?.authority ? { authority: draft.current.authority } : {}) })
    return true
  }
  async function send(value: HumanAttempt) {
    if (!available || running.current || value.context !== context) return
    const captured = scope, c = new AbortController(), wasUnknown = value.unknown
    let dispatched = false, outcomeReceived = false
    running.current = true; writer.current = c; keep(value); setBusy(true); setTrusted(null); setError(null)
    callbacks.current.onDirty(true); callbacks.current.onSaving(true)
    const timer = setTimeout(() => c.abort(), 15000)
    try {
      await fresh(captured, c.signal, value)
      const page = await api.trace(stableAnchor, 0, '', c.signal)
      const user = await fresh(captured, c.signal, value)
      if (!page.authorities.some(grant => sameAuthority(grant, value.authority))) throw new ApiError(403, 'human_authority_required')
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_human_scope')
      dispatched = true; keep({ ...value, unknown: true })
      const row = await api.save(stableAnchor, value, user.csrf, c.signal)
      outcomeReceived = true
      if (row.actorId.toLowerCase() !== user.actorId.toLowerCase() || row.roleId !== value.authority.roleId || row.revision !== value.nextRevision ||
          row.recording !== null && (!value.authority.recording ||
            (Object.keys(row.recording.policy) as (keyof api.RecordingPolicy)[]).some(key => row.recording!.policy[key] !== value.authority.recording![key])))
        throw new ApiError(502, 'invalid_human_outcome')
      await fresh(captured, c.signal, value)
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_human_scope')
      confirm({ owner: value.owner, context, row, authority: value.authority }); keep(null); bind(null); setConflict(false); setData(null)
      setReload(version => version + 1)
    } catch (problem) {
      if (!mounted.current) return
      const code = status(problem)
      if (live.current === captured) { setError(code); setData(null); setTrusted(null) }
      if (wasUnknown || outcomeReceived || dispatched && (!valid(captured, c.signal) || ![400, 401, 403, 404, 409].includes(code))) keep({ ...value, unknown: true })
      else keep(null)
      if (code === 409) setConflict(true)
    } finally {
      clearTimeout(timer); running.current = false
      if (writer.current === c) writer.current = null
      if (mounted.current) setBusy(false)
    }
  }
  function capture(command: Omit<Parameters<typeof api.save>[1], 'key'>, authority: api.Authority, nextRevision: number) {
    if (locked || running.current || frozen.current || receipt.current || !owner) return
    const expected = draft.current ?? { owner, context }
    bind(expected)
    void send(Object.freeze({ ...expected, ...command, key: crypto.randomUUID(), authority, nextRevision, unknown: false }))
  }
  function recheck() { if (!running.current) setReload(value => value + 1) }
  function discard() {
    if (running.current || frozen.current || !canDiscard) return false
    bind(null); confirm(null); setConflict(false); setData(null); setTrusted(null); setReload(value => value + 1)
    return true
  }
  const eligibleRetry = canShow && !!attempt && attempt.owner === owner && attempt.context === context &&
    !!data?.authorities.some(grant => sameAuthority(grant, attempt.authority))
  return { data: canShow ? data : null, error, busy, conflict, attempt, confirmed,
    notice: !!confirmed && available && observed?.scope === scope && observed.owner === confirmed.owner && confirmed.context === context,
    canShow, canDiscard, locked, dirty: !!binding || !!attempt, canRetry: eligibleRetry && !busy,
    offset, history, edit, capture, retry: () => { if (eligibleRetry && attempt) void send(attempt) }, recheck, discard,
    setOffset: (value: number) => { if (!binding && !attempt && !busy && !confirmed) setOffset(value) },
    setHistory: (value: string) => { if (!binding && !attempt && !busy && !confirmed) { setHistory(value); setOffset(0) } } }
}
