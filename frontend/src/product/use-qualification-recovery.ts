import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import * as api from './qualification-api'
import { loadProfile } from './profile-api'
import type { Profile } from './profile-contract'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

type Binding = { owner: string; context: string; version: number }
type Confirmation = Binding & { minimumRevision: number; eventHash: string | null; eventState: string | null }
export type QualificationAttempt = Readonly<Binding & {
  rule: string; action: 'events' | 'fact-attestations'; body: string; key: string; unknown: boolean; nextRevision: number
}>
type Props = {
  rule: string; ruleRevision: number; vessel: string; offline: boolean
  onDirty: (value: boolean) => void; onSaving: (value: boolean) => void
}

// The envelope belongs to one actual session and source context. Source reads
// may restore access but never prove whether an earlier unknown write committed.
export function useQualificationRecovery({ rule, ruleRevision, vessel, offline, onDirty, onSaving }: Props) {
  const epoch = useWorkspaceRevalidation(), [reload, setReload] = useState(0)
  const available = !offline && navigator.onLine && document.visibilityState !== 'hidden'
  const context = `${rule}:${ruleRevision}:${vessel}`, scope = `${context}:${epoch}:${reload}:${available}`
  const [data, setData] = useState<api.Page | null>(null), [content, setContent] = useState(api.blank)
  const [state, setState] = useState('ADOPTED'), [until, setUntil] = useState('')
  const [profile, setProfile] = useState<Profile | null>(null)
  const [historyRows, setHistoryRows] = useState<api.Qualification[]>([]), [historyMore, setHistoryMore] = useState(false)
  const [trusted, setTrusted] = useState<{ scope: string; owner: string } | null>(null)
  const [observed, setObserved] = useState<{ scope: string; owner: string } | null>(null)
  const [binding, setBinding] = useState<Binding | null>(null), [dirty, setDirty] = useState(false)
  const [attempt, setAttempt] = useState<QualificationAttempt | null>(null)
  const [confirmed, setConfirmed] = useState<Confirmation | null>(null), [notice, setNotice] = useState<Binding | null>(null)
  const [busy, setBusy] = useState(false), [error, setError] = useState<number | null>(null), [conflict, setConflict] = useState(false)
  const mounted = useRef(false), running = useRef(false), live = useRef('')
  const writer = useRef<AbortController | null>(null), frozen = useRef<QualificationAttempt | null>(null)
  const sourceEpoch = useRef(0)
  const draft = useRef<Binding | null>(null), receipt = useRef<Confirmation | null>(null)
  const callbacks = useRef({ onDirty, onSaving })
  const owner = observed?.scope === scope ? observed.owner : null
  const ready = available && trusted?.scope === scope && trusted.owner === owner && !!owner
  const sameBinding = (!binding || binding.context === context && binding.owner === owner) &&
    (!attempt || attempt.context === context && attempt.owner === owner)
  const canShow = ready && sameBinding && !!data
  const locked = !canShow || busy || conflict || !!attempt || !!confirmed
  const current = canShow ? data.items[0] : undefined
  useLayoutEffect(() => {
    callbacks.current = { onDirty, onSaving }
    if (live.current !== scope || !available) { sourceEpoch.current++; writer.current?.abort() }
    live.current = scope
  }, [scope, available, onDirty, onSaving])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => { onDirty(dirty || !!binding || !!attempt || conflict) }, [dirty, binding, attempt, conflict, onDirty])
  // Existing parent navigation treats onSaving as write protection. An unknown
  // envelope or an inaccessible private draft must not be discarded by leaving
  // this component, even while its own explicit recovery controls remain usable.
  useEffect(() => { onSaving(busy || !!attempt || !!binding && !canShow) }, [busy, attempt, binding, canShow, onSaving])
  const keep = (value: QualificationAttempt | null) => { frozen.current = value; setAttempt(value) }
  const bind = (value: Binding | null) => { draft.current = value; setBinding(value) }
  const confirm = useCallback((value: Confirmation | null) => { receipt.current = value; setConfirmed(value) }, [])
  const valid = useCallback((captured: string, signal: AbortSignal) => mounted.current && !signal.aborted && live.current === captured, [])
  const fresh = useCallback(async (captured: string, signal: AbortSignal, expected?: Binding | null) => {
    // A pending file belongs to the authorization/edit generation in which it
    // was selected, not to a later session or a completed/uncertain command.
    sourceEpoch.current++
    const user = await api.loadSession(signal)
    if (!valid(captured, signal)) throw new ApiError(0, 'obsolete_qualification_scope')
    setObserved({ scope: captured, owner: user.owner })
    if (expected && (expected.owner !== user.owner || expected.context !== context)) throw new ApiError(403, 'qualification_owner_changed')
    return user
  }, [context, valid])

  useEffect(() => {
    if (!available) return
    const c = new AbortController(), captured = scope
    let active = true
    const timer = setTimeout(() => c.abort(), 15000)
    async function read() {
      const expected = frozen.current ?? draft.current ?? receipt.current
      const before = await fresh(captured, c.signal, expected)
      if (!active || !valid(captured, c.signal)) return
      setError(null); setProfile(null)
      const page = await api.load(rule, vessel, c.signal)
      const committed = receipt.current, head = page.items[0]
      if (committed && (!head || head.revision < committed.minimumRevision || head.revision === committed.minimumRevision &&
          (committed.eventHash !== null && head.hash !== committed.eventHash || committed.eventState !== null && head.state !== committed.eventState)))
        throw new ApiError(502, 'qualification_readback_unconfirmed')
      await fresh(captured, c.signal, { owner: before.owner, context, version: 0 })
      if (!active || !valid(captured, c.signal)) return
      let selectedProfile: Profile | null = null
      if (vessel) {
        try { selectedProfile = await loadProfile(vessel, c.signal) } catch { /* Optional owning profile is independently permission gated. */ }
        await fresh(captured, c.signal, { owner: before.owner, context, version: 0 })
      }
      if (!active || !valid(captured, c.signal)) return
      setData(page); setTrusted({ scope: captured, owner: before.owner }); setProfile(selectedProfile)
      setHistoryRows(page.items); setHistoryMore(page.hasMore); setError(null)
      if (!frozen.current && draft.current && draft.current.version !== (page.items[0]?.version ?? 0)) setConflict(true)
      if (!frozen.current && !draft.current) {
        setContent(page.items[0]?.content ?? api.blank()); setState('ADOPTED'); setUntil(''); confirm(null)
      }
    }
    read().catch(problem => {
      if (!active || !mounted.current || live.current !== captured) return
      setTrusted(null); setData(null); setProfile(null)
      setError(problem instanceof ApiError ? problem.status : 0)
    }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [rule, vessel, scope, available, context, confirm, fresh, valid])

  const edit = (patch: Partial<api.Content>) => {
    if (locked || !owner || running.current || frozen.current || receipt.current) return
    if (!binding) bind({ owner, context, version: current?.version ?? 0 })
    setContent(value => ({ ...value, ...patch })); setDirty(true)
  }
  function choose(value: string, setter: (value: string) => void) {
    if (locked || !owner || running.current || frozen.current || receipt.current) return
    if (!binding) bind({ owner, context, version: current?.version ?? 0 })
    setter(value)
  }
  async function send(value: QualificationAttempt) {
    if (!available || running.current || value.context !== context) return
    sourceEpoch.current++
    const captured = scope, c = new AbortController(), wasUnknown = value.unknown
    let dispatched = false, outcomeReceived = false
    running.current = true; writer.current = c; setBusy(true); setError(null); setTrusted(null)
    keep(value); callbacks.current.onDirty(true); callbacks.current.onSaving(true)
    const timer = setTimeout(() => c.abort(), 15000)
    try {
      await fresh(captured, c.signal, value)
      const page = await api.load(value.rule, vessel, c.signal)
      const user = await fresh(captured, c.signal, value)
      const body = JSON.parse(value.body) as { state?: string; authorityId?: string }
      const action = value.action === 'fact-attestations' ? 'attest' : body.state === 'DRAFT' ? 'draft' : body.state === 'IN_REVIEW' ? 'review' : 'qualify'
      const grants = page.authorities.filter(grant => grant.action === action)
      if (!page.actions.includes(action) || grants.length !== 1 || grants[0].authorityId !== body.authorityId) throw new ApiError(403, 'human_authority_required')
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_qualification_scope')
      dispatched = true
      keep({ ...value, unknown: true })
      const outcome = await api.send(value.rule, value, user.csrf, c.signal)
      outcomeReceived = true
      if (value.action === 'events' && (!('actorId' in outcome) || outcome.actorId !== value.owner.split('/')[1] || outcome.revision !== value.nextRevision))
        throw new ApiError(502, 'invalid_qualification_outcome')
      await fresh(captured, c.signal, value)
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_qualification_scope')
      const known: Confirmation = { owner: value.owner, context: value.context, version: value.version,
        minimumRevision: 'revision' in outcome ? outcome.revision as number : outcome.qualificationRevision as number,
        eventHash: 'revision' in outcome ? outcome.hash as string : null, eventState: 'state' in outcome ? outcome.state as string : null }
      confirm(known); setNotice(known); keep(null); bind(null); setDirty(false); setConflict(false)
      setData(null); setProfile(null); setReload(revision => revision + 1)
    } catch (problem) {
      if (!mounted.current) return
      const code = problem instanceof ApiError ? problem.status : 0
      if (live.current === captured) { setError(code); setTrusted(null); setData(null); setProfile(null) }
      // A later denied/conflicting retry cannot prove that the earlier lost
      // request rolled back. Only an initial definite rejection is releasable.
      if (wasUnknown || outcomeReceived || dispatched && ![400, 401, 403, 404, 409].includes(code)) keep({ ...value, unknown: true })
      else if (dispatched && live.current !== captured) keep({ ...value, unknown: true })
      else keep(null)
      if (code === 409) setConflict(true)
    } finally {
      clearTimeout(timer); running.current = false
      if (writer.current === c) writer.current = null
      if (mounted.current) setBusy(false)
    }
  }
  function capture(action: QualificationAttempt['action'], body: object) {
    if (locked || !owner || running.current) return
    const expected = binding ?? { owner, context, version: current?.version ?? 0 }
    void send({ ...expected, rule, action, body: JSON.stringify(body), key: crypto.randomUUID(), unknown: false, nextRevision: (current?.revision ?? 0) + 1 })
  }
  function transition(next: string) {
    if (locked) return
    const action = next === 'DRAFT' ? 'draft' : next === 'IN_REVIEW' ? 'review' : 'qualify'
    const authorityId = data?.authorities.find(grant => grant.action === action)?.authorityId
    if (!authorityId) return
    capture('events', { expectedVersion: binding?.version ?? current?.version ?? 0, ruleRevision, authorityId, state: next, content: next === 'DRAFT' ? content : null })
  }
  async function source(file: File | undefined) {
    if (locked || running.current || frozen.current || receipt.current) return
    const selected = ++sourceEpoch.current, captured = scope
    if (!file) return
    if (file.size < 1 || file.size > 2097152) { setError(400); return }
    const mayApply = () => mounted.current && live.current === captured && sourceEpoch.current === selected &&
      !running.current && !frozen.current && !receipt.current
    try {
      const bytes = new Uint8Array(await file.arrayBuffer())
      if (!mayApply()) return
      let binary = ''; bytes.forEach(value => { binary += String.fromCharCode(value) })
      edit({ sourceBytesBase64: btoa(binary) })
    } catch { if (mayApply()) setError(400) }
  }
  function recheck() { if (!busy) setReload(value => value + 1) }
  function discard() {
    if (busy || frozen.current) return
    sourceEpoch.current++
    bind(null); confirm(null); setDirty(false); setConflict(false); setNotice(null); setContent(api.blank()); setState('ADOPTED'); setUntil(''); setReload(value => value + 1)
  }
  async function more() {
    if (!canShow || locked || !historyMore || !historyRows.length || !owner) return
    const captured = scope, c = new AbortController(), expected = { owner, context, version: 0 }
    try {
      await fresh(captured, c.signal, expected)
      const page = await api.load(rule, vessel, c.signal, historyRows[historyRows.length - 1].revision)
      await fresh(captured, c.signal, expected)
      if (valid(captured, c.signal)) { setHistoryRows(rows => [...rows, ...page.items]); setHistoryMore(page.hasMore) }
    } catch (problem) {
      if (valid(captured, c.signal)) { setTrusted(null); setData(null); setProfile(null); setError(problem instanceof ApiError ? problem.status : 0) }
    }
  }
  return { data, content, profile, historyRows, historyMore, dirty, busy, error, attempt, conflict, confirmed, state, until,
    notice: !!notice && notice.owner === owner && notice.context === context, locked, current, canShow,
    available, canRetry: available && !busy && !!attempt && attempt.owner === owner && attempt.context === context,
    edit, chooseState: (value: string) => choose(value, setState), chooseUntil: (value: string) => choose(value, setUntil),
    send, capture, transition, source, discard, recheck, more }
}
