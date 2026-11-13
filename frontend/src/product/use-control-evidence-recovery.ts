import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import * as api from './control-evidence-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

type Binding = Readonly<{ owner: string; context: string }>
export type ControlAttempt = Readonly<Binding & { vessel: string; kind: api.Kind; id: string | null; method: 'POST' | 'PUT';
  action?: 'submit' | 'complete'; body: string; key: string; expectedRevision: number; unknown: boolean }>
type Confirmation = Binding & { row: api.Row }
type Props = { vessel: string; kind: api.Kind; owner?: string | null; id: string | null; offset: number; offline: boolean;
  blocked: () => boolean; onSaved: (row: api.Row) => void }
const code = (problem: unknown) => problem instanceof ApiError ? problem.status : 0

// Receipts are historical outcomes, not current admission/qualification grants.
// Body, nullable route ID and UUID key are immutable once a request is uncertain.
export function useControlEvidenceRecovery({ vessel, kind, owner: hint, id, offset, offline, blocked, onSaved }: Props) {
  const epoch = useWorkspaceRevalidation(), [reload, setReload] = useState(0), context = `${vessel}/${kind}`
  const available = !offline && navigator.onLine && document.visibilityState !== 'hidden' && hint !== null
  const scope = `${context}/${id ?? ''}/${hint ?? ''}/${available}/${offset}/${epoch}/${reload}`
  const [rows, setRows] = useState<api.Page | null>(null), [trusted, setTrusted] = useState<string | null>(null)
  const [observed, setObserved] = useState<{ scope: string; owner: string } | null>(null)
  const [binding, setBinding] = useState<Binding | null>(null), [attempt, setAttempt] = useState<ControlAttempt | null>(null)
  const [confirmed, setConfirmed] = useState<Confirmation | null>(null), [current, setCurrent] = useState<api.Row | null>(null)
  const [busy, setBusy] = useState(false), [error, setError] = useState<number | null>(null), [conflict, setConflict] = useState(false)
  const mounted = useRef(false), running = useRef(false), live = useRef(''), writer = useRef<AbortController | null>(null)
  const frozen = useRef<ControlAttempt | null>(null), draft = useRef<Binding | null>(null), receipt = useRef<Confirmation | null>(null)
  const editEpoch = useRef(0), acknowledgment = useRef<AbortController | null>(null), callbacks = useRef({ blocked, onSaved })
  const owner = observed?.scope === scope ? observed.owner : null
  const canShow = available && !!owner && trusted === scope && (!binding || binding.owner === owner && binding.context === context) && !!rows
  const locked = !canShow || !rows?.canWrite || busy || conflict || !!attempt || !!confirmed
  useLayoutEffect(() => {
    callbacks.current = { blocked, onSaved }
    if (live.current !== scope || !available) { editEpoch.current++; writer.current?.abort(); acknowledgment.current?.abort() }
    live.current = scope
  }, [scope, available, blocked, onSaved])
  const abortPending = useCallback(() => { editEpoch.current++; writer.current?.abort(); acknowledgment.current?.abort() }, [])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; abortPending() } }, [abortPending])
  const valid = useCallback((captured: string, signal: AbortSignal) => mounted.current && !signal.aborted && live.current === captured, [])
  const keep = (value: ControlAttempt | null) => { frozen.current = value; setAttempt(value) }
  const bind = (value: Binding | null) => { draft.current = value; setBinding(value) }
  const confirm = (value: Confirmation | null) => { receipt.current = value; setConfirmed(value) }
  const fresh = useCallback(async (captured: string, signal: AbortSignal, expected?: Binding | null) => {
    const user = await api.loadSession(signal)
    if (!valid(captured, signal)) throw new ApiError(0, 'obsolete_control_scope')
    setObserved({ scope: captured, owner: user.owner })
    if (hint !== undefined && hint !== user.owner || expected && (expected.owner !== user.owner || expected.context !== context))
      throw new ApiError(403, 'control_owner_changed')
    return user
  }, [hint, context, valid])
  const read = useCallback(async <T,>(request: (signal: AbortSignal) => Promise<T>, signal: AbortSignal) => {
    const captured = scope, expected = frozen.current ?? draft.current ?? receipt.current
    try {
      const user = await fresh(captured, signal, expected), value = await request(signal)
      await fresh(captured, signal, { owner: user.owner, context })
      if (!valid(captured, signal)) throw new ApiError(0, 'obsolete_control_scope')
      return value
    } catch (problem) {
      if (valid(captured, signal)) { setTrusted(null); setRows(null); setCurrent(null); setError(code(problem)) }
      throw problem
    }
  }, [scope, fresh, context, valid])
  useEffect(() => {
    if (!available) return
    const c = new AbortController(), captured = scope, timer = setTimeout(() => c.abort(), 15000)
    let active = true
    read(async signal => {
      const page = await api.page(vessel, kind, offset, signal)
      const known = receipt.current, resource = known?.row.id ?? id
      let head: api.Row | null = null
      if (resource) {
        const versions = await api.page(vessel, kind, 0, signal, resource)
        const visible = versions.items[0]
        head = await api.currentRecord(vessel, kind, resource, signal, page)
        if (!visible || visible.revision !== head.revision || visible.version !== head.version || visible.hash !== head.hash ||
            known && (head.revision < known.row.revision || head.revision === known.row.revision && head.hash !== known.row.hash))
          throw new ApiError(502, 'control_readback_unconfirmed')
      }
      return { page, head }
    }, c.signal).then(({ page, head }) => {
      if (!active || !valid(captured, c.signal)) return
      setRows(page); setCurrent(head); setTrusted(captured); setError(null)
      if (!frozen.current && !draft.current && !receipt.current) {
        setConflict(false)
        if (head && !callbacks.current.blocked()) callbacks.current.onSaved(head)
      }
    }).catch(problem => {
      if (active && mounted.current && live.current === captured) { setRows(null); setTrusted(null); setCurrent(null); setError(code(problem)) }
    }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [scope, available, vessel, kind, id, offset, read, valid])
  function edit() {
    if (locked || running.current || frozen.current || receipt.current || callbacks.current.blocked() || !owner) return false
    editEpoch.current++; acknowledgment.current?.abort()
    if (!draft.current) bind({ owner, context })
    return true
  }
  async function send(value: ControlAttempt) {
    if (!available || running.current || callbacks.current.blocked() || value.context !== context) return
    const captured = scope, c = new AbortController(), wasUnknown = value.unknown
    let dispatched = false, received = false
    editEpoch.current++; acknowledgment.current?.abort(); running.current = true; writer.current = c; keep(value); setBusy(true); setTrusted(null); setError(null)
    const timer = setTimeout(() => c.abort(), 15000)
    try {
      await fresh(captured, c.signal, value)
      const page = await api.page(value.vessel, value.kind, 0, c.signal, value.id ?? undefined)
      const user = await fresh(captured, c.signal, value)
      // Do not compare old editor tokens or require fresh qualified admission:
      // the actual backend intentionally permits original historical receipts.
      if (!page.canWrite || value.id !== null && !page.items.length || callbacks.current.blocked()) throw new ApiError(403, 'permission_denied')
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_control_scope')
      dispatched = true; keep({ ...value, unknown: true })
      const row = await api.save(value.vessel, value.kind, value.id, value, user.csrf, c.signal)
      received = true
      await fresh(captured, c.signal, value)
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_control_scope')
      confirm({ owner: value.owner, context, row }); keep(null); bind(null); setConflict(false)
      // Canonical server identity is retained before any optional list/versions read.
      callbacks.current.onSaved(row); setRows(null); setCurrent(null); setReload(number => number + 1)
    } catch (problem) {
      if (!mounted.current) return
      const status = code(problem)
      if (live.current === captured) { setError(status); setRows(null); setTrusted(null); setCurrent(null) }
      if (wasUnknown || received || dispatched && (!valid(captured, c.signal) || ![400, 401, 403, 404, 409].includes(status))) keep({ ...value, unknown: true })
      else keep(null)
      if (status === 409) setConflict(true)
    } finally {
      clearTimeout(timer); running.current = false
      if (writer.current === c) writer.current = null
      if (mounted.current) setBusy(false)
    }
  }
  function capture(command: api.Command, resource: api.Row | null, action?: 'submit' | 'complete') {
    if (locked || running.current || frozen.current || receipt.current || callbacks.current.blocked() || !owner) return
    const expected = draft.current ?? { owner, context }; bind(expected)
    void send(Object.freeze({ ...expected, vessel, kind, id: resource?.id ?? null, method: action || !resource ? 'POST' : 'PUT', action,
      body: JSON.stringify(command), key: crypto.randomUUID(), expectedRevision: (resource?.revision ?? 0) + 1, unknown: false }))
  }
  async function discard(): Promise<{ released: boolean; row: api.Row | null }> {
    if (!available || running.current || frozen.current || callbacks.current.blocked()) return { released: false, row: null }
    const originalDraft = draft.current, originalReceipt = receipt.current, expected = originalDraft ?? originalReceipt
    if (!expected || originalReceipt && (!canShow || !current)) return { released: false, row: null }
    const captured = scope, c = new AbortController(); running.current = true; writer.current = c; setBusy(true); editEpoch.current++; acknowledgment.current?.abort()
    const timer = setTimeout(() => c.abort(), 15000)
    try {
      await fresh(captured, c.signal, expected)
      if (!valid(captured, c.signal) || frozen.current || callbacks.current.blocked() || draft.current !== originalDraft || receipt.current !== originalReceipt) return { released: false, row: null }
      const row = originalReceipt || id ? current : null
      bind(null); confirm(null); setConflict(false); setRows(null); setTrusted(null); setReload(number => number + 1)
      return { released: true, row }
    } catch (problem) {
      if (mounted.current && live.current === captured) { setError(code(problem)); setRows(null); setTrusted(null) }
      return { released: false, row: null }
    } finally {
      clearTimeout(timer); running.current = false
      if (writer.current === c) writer.current = null
      if (mounted.current) setBusy(false)
    }
  }
  async function acknowledge<T>(request: (signal: AbortSignal) => Promise<T>, apply: (value: T) => void, allowed: () => boolean = () => true) {
    if (!edit()) return
    const generation = editEpoch.current, captured = scope, c = new AbortController()
    acknowledgment.current = c
    const usable = () => valid(captured, c.signal) && generation === editEpoch.current && !running.current && !frozen.current && !receipt.current && !callbacks.current.blocked() && allowed()
    try {
      const user = await fresh(captured, c.signal, draft.current)
      if (!usable()) return
      const value = await request(c.signal)
      if (!usable()) return
      await fresh(captured, c.signal, { owner: user.owner, context })
      if (usable()) apply(value)
    } catch (problem) {
      if (usable()) { setError(code(problem)); setRows(null); setTrusted(null) }
    } finally { if (acknowledgment.current === c) acknowledgment.current = null }
  }
  const observedDraft = binding && observed?.scope === scope && observed.owner === binding.owner && binding.context === context
  return { rows: canShow ? rows : null, current, confirmed, attempt, busy, error, conflict, canShow, locked, available, read,
    barrier: busy || !!attempt || !!binding && !canShow, dirty: !!binding || !!attempt,
    notice: !!confirmed && observed?.scope === scope && observed.owner === confirmed.owner && confirmed.context === context,
    canDiscard: !busy && !attempt && (confirmed ? canShow && !!current : !!observedDraft),
    edit, capture, discard, acknowledge, setError, recheck: () => { if (!running.current) { editEpoch.current++; acknowledgment.current?.abort(); setReload(value => value + 1) } },
    retry: () => { if (canShow && rows?.canWrite && attempt) void send(attempt) } }
}
