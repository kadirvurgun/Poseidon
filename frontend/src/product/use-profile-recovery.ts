import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import * as api from './profile-api'
import type { Profile, ProfileContent, ProfileHistory } from './profile-contract'
import { filePage } from './file-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

type Binding = Readonly<{ owner: string; vessel: string }>
type DraftBinding = Binding & Readonly<{ baseVersion: number; baseRevision: number }>
type Confirmation = Binding & { row: Profile }
type Props = { id: string; journey?: 'profile' | 'onboarding'; owner?: string | null; offline?: boolean; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void }
const problem = (value: unknown) => value instanceof ApiError ? value : new ApiError(0)

// A current read is permission evidence, never evidence that an earlier PUT failed.
// All command recovery is memory-only; profile identity is the existing vessel URL.
export function useProfileRecovery({ id, journey = 'profile', owner: hint, offline = false, onDirty, onSaving }: Props) {
  const epoch = useWorkspaceRevalidation(), [reload, setReload] = useState(0)
  const available = !offline && navigator.onLine && document.visibilityState !== 'hidden' && hint !== null
  const scope = `${id}/${journey}/${hint ?? ''}/${available}/${epoch}/${reload}`
  const [profile, setProfile] = useState<Profile | null>(null), [trusted, setTrusted] = useState<{ scope: string; owner: string; accessEpoch: number } | null>(null)
  const [observed, setObserved] = useState<{ scope: string; owner: string } | null>(null)
  const [binding, setBinding] = useState<DraftBinding | null>(null), [content, setContent] = useState<ProfileContent | null>(null)
  const [attempt, setAttempt] = useState<api.ProfileAttempt | null>(null), [confirmed, setConfirmed] = useState<Confirmation | null>(null)
  const [history, setHistory] = useState<ProfileHistory | null>(null), [historyOffset, setHistoryOffset] = useState(0), [historyAttempt, setHistoryAttempt] = useState(0)
  const [historyFailure, setHistoryFailure] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [conflict, setConflict] = useState(false)
  const [busy, setBusy] = useState(false)
  const mounted = useRef(false), live = useRef(''), running = useRef(false), writer = useRef<AbortController | null>(null)
  const frozen = useRef<api.ProfileAttempt | null>(null), draft = useRef<DraftBinding | null>(null), receipt = useRef<Confirmation | null>(null)
  const callbacks = useRef({ onDirty, onSaving })
  const privateEpoch = useRef(0), [accessEpoch, setAccessEpoch] = useState(0)
  const owner = observed?.scope === scope ? observed.owner : null
  const expected = attempt ?? binding ?? confirmed
  const canShow = available && !!owner && trusted?.scope === scope && trusted.owner === owner && trusted.accessEpoch === accessEpoch && !!profile &&
    (!expected || expected.owner === owner && expected.vessel === id)
  const locked = !canShow || busy || !!attempt || !!confirmed || profile?.vessel.canEdit !== true
  const notice = !!confirmed && available && owner === confirmed.owner && id === confirmed.vessel
  useLayoutEffect(() => {
    callbacks.current = { onDirty, onSaving }
    if (live.current !== scope) writer.current?.abort()
    live.current = scope
  }, [scope, onDirty, onSaving])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => { onDirty(!!binding || !!attempt) }, [binding, attempt, onDirty])
  useEffect(() => { onSaving(busy || !!attempt || !!binding && !canShow) }, [busy, attempt, binding, canShow, onSaving])
  const valid = useCallback((captured: string, signal: AbortSignal) => mounted.current && live.current === captured && !signal.aborted, [])
  const keep = (value: api.ProfileAttempt | null) => { frozen.current = value; setAttempt(value) }
  const bind = (value: DraftBinding | null) => { draft.current = value; setBinding(value) }
  const withdraw = useCallback((value: unknown) => {
    privateEpoch.current++; setAccessEpoch(privateEpoch.current)
    setTrusted(null); setHistory(null); setFailure(problem(value))
  }, [])
  const fresh = useCallback(async (captured: string, signal: AbortSignal, bound?: Binding | null) => {
    const user = await api.loadProfileSession(signal)
    if (!valid(captured, signal)) throw new ApiError(0, 'obsolete_profile_scope')
    setObserved({ scope: captured, owner: user.owner })
    if (hint !== undefined && hint !== user.owner || bound && (bound.owner !== user.owner || bound.vessel !== id)) throw new ApiError(403, 'profile_owner_changed')
    return user
  }, [hint, id, valid])
  useEffect(() => {
    if (!available) return
    const c = new AbortController(), captured = scope, readEpoch = privateEpoch.current
    let active = true
    const timer = setTimeout(() => c.abort(), 15000)
    async function read() {
      const bound = frozen.current ?? draft.current ?? receipt.current
      const before = await fresh(captured, c.signal, bound)
      const row = await api.loadProfile(id, c.signal)
      await fresh(captured, c.signal, { owner: before.owner, vessel: id })
      if (row.tenantId.toLowerCase() !== before.tenant) throw new ApiError(502, 'invalid_contract')
      if (!active || !valid(captured, c.signal)) return
      const known = receipt.current
      if (known && (row.revision < known.row.revision || row.revision === known.row.revision && !api.profileContentMatches(row.content, known.row.content))) throw new ApiError(502, 'invalid_contract')
      if (privateEpoch.current !== readEpoch) throw new ApiError(403, 'profile_access_recheck_required')
      setProfile(row); setTrusted({ scope: captured, owner: before.owner, accessEpoch: readEpoch }); setFailure(null)
      const unsent = draft.current
      if (!frozen.current && unsent && (row.version !== unsent.baseVersion || row.revision !== unsent.baseRevision)) {
        setConflict(true); setFailure(new ApiError(409, 'version_conflict'))
      }
      if (!bound) { setContent(null); setConflict(false) }
    }
    read().catch(error => { if (active && mounted.current && live.current === captured) withdraw(error) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [scope, id, available, fresh, valid, withdraw])
  const read = useCallback(async <T,>(load: (signal: AbortSignal) => Promise<T>, signal: AbortSignal): Promise<T> => {
    const captured = scope, bound = frozen.current ?? draft.current ?? receipt.current
    try {
      if (!available) throw new ApiError(0, 'profile_offline')
      const before = await fresh(captured, signal, bound)
      const value = await load(signal)
      await fresh(captured, signal, { owner: before.owner, vessel: id })
      if (!valid(captured, signal)) throw new ApiError(0, 'obsolete_profile_scope')
      return value
    } catch (error) {
      if (valid(captured, signal) && [401, 403, 404].includes(problem(error).status)) withdraw(error)
      throw error
    }
  }, [scope, available, fresh, id, valid, withdraw])
  const loadFiles = useCallback(async (vessel: string, offset: number, signal: AbortSignal) => {
    const captured = scope, bound = frozen.current ?? draft.current ?? receipt.current
    let before: Awaited<ReturnType<typeof fresh>>
    try { before = await fresh(captured, signal, bound) }
    catch (error) { if (valid(captured, signal)) withdraw(error); throw error }
    // An unselected optional file service can be unavailable while the profile
    // remains readable. Picker denial rules decide whether a selected pin is lost.
    const value = await filePage(vessel, offset, signal)
    try { await fresh(captured, signal, { owner: before.owner, vessel: id }) }
    catch (error) { if (valid(captured, signal)) withdraw(error); throw error }
    if (!valid(captured, signal)) throw new ApiError(0, 'obsolete_profile_scope')
    return value
  }, [scope, fresh, id, valid, withdraw])
  useEffect(() => {
    if (!canShow) return
    const c = new AbortController(), captured = scope, readEpoch = privateEpoch.current
    let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    read(s => api.loadProfileHistory(id, historyOffset, s), c.signal).then(value => {
      if (active && valid(captured, c.signal) && privateEpoch.current === readEpoch) { setHistory(value); setHistoryFailure(false) }
    }).catch(error => {
      if (active && mounted.current && live.current === captured) {
        setHistory(null); setHistoryFailure(true)
        if ([401, 403, 404].includes(problem(error).status)) withdraw(error)
      }
    }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [canShow, scope, id, historyOffset, historyAttempt, read, valid, withdraw])
  function edit(key: keyof ProfileContent, value: ProfileContent[typeof key]) {
    if (live.current !== scope || locked || running.current || frozen.current || receipt.current || !owner) return
    if (!draft.current) bind({ owner, vessel: id, baseVersion: profile!.version, baseRevision: profile!.revision })
    setContent(current => ({ ...(current ?? profile!.content), [key]: value }))
    if (!conflict) setFailure(null)
  }
  async function send(value: api.ProfileAttempt) {
    if (!available || running.current || value.vessel !== id) return
    const captured = scope, c = new AbortController(), wasUnknown = value.unknown, writeEpoch = privateEpoch.current
    let dispatched = false, received = false
    running.current = true; writer.current = c; keep(value); setBusy(true); setFailure(null); setHistory(null)
    callbacks.current.onSaving(true); callbacks.current.onDirty(true)
    const timer = setTimeout(() => c.abort(), 15000)
    try {
      await fresh(captured, c.signal, value)
      const current = await api.loadProfile(id, c.signal)
      const user = await fresh(captured, c.signal, value)
      if (current.tenantId.toLowerCase() !== user.tenant) throw new ApiError(502, 'invalid_contract')
      if (!current.vessel.canEdit) throw new ApiError(403, 'permission_denied')
      if (!wasUnknown && (current.version !== value.expectedVersion || current.revision+1 !== value.nextRevision)) throw new ApiError(409, 'version_conflict')
      // Live xmin may be newer precisely because the first PUT committed.
      // Let the server return its actor-bound original receipt before conflict.
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_profile_scope')
      if (privateEpoch.current !== writeEpoch) throw new ApiError(403, 'profile_access_recheck_required')
      dispatched = true; keep({ ...value, unknown: true })
      const row = await api.saveProfileAttempt(value, user.csrf, c.signal)
      received = true
      await fresh(captured, c.signal, value)
      if (!valid(captured, c.signal)) throw new ApiError(0, 'obsolete_profile_scope')
      const known = { owner: value.owner, vessel: id, row }
      receipt.current = known; setConfirmed(known); keep(null); bind(null); setContent(row.content); setProfile(row); setConflict(false)
      // A selected-file/history denial observed during this PUT remains a
      // private boundary even after a valid receipt and global session proof.
      // Keep the known outcome, requiring an explicit owning GET to restore UI.
      if (privateEpoch.current === writeEpoch) setTrusted({ scope: captured, owner: value.owner, accessEpoch: writeEpoch })
      else setTrusted(null)
      setHistoryOffset(0); setHistoryAttempt(number => number + 1)
    } catch (error) {
      if (!mounted.current) return
      const failure = problem(error)
      if (live.current === captured) {
        setFailure(failure)
        if (received || [401, 403, 404].includes(failure.status)) { setTrusted(null); setHistory(null) }
      }
      if (wasUnknown || received || dispatched && (!valid(captured, c.signal) || ![400, 401, 403, 404, 409].includes(failure.status))) keep({ ...value, unknown: true })
      else keep(null)
      if (failure.status === 409) setConflict(true)
      // A definite first rejection did not save a revision. Restore history via
      // its authorized read, without resetting the retained field errors/draft.
      if (!wasUnknown && !received && dispatched && failure.status === 400 &&
          valid(captured, c.signal) && privateEpoch.current === writeEpoch) setHistoryAttempt(number => number + 1)
    } finally {
      clearTimeout(timer); running.current = false
      if (writer.current === c) writer.current = null
      if (mounted.current) setBusy(false)
    }
  }
  function capture() {
    if (live.current !== scope || locked || conflict || running.current || frozen.current || receipt.current || !owner || !profile) return
    const bound = draft.current ?? { owner, vessel: id, baseVersion: profile.version, baseRevision: profile.revision }
    if (bound.baseVersion !== profile.version || bound.baseRevision !== profile.revision) {
      setConflict(true); setFailure(new ApiError(409, 'version_conflict')); return
    }
    bind(bound)
    void send(Object.freeze({ ...bound, route: `vessels/${encodeURIComponent(id)}/regulatory-profile`, method: 'PUT',
      body: JSON.stringify({ expectedVersion: bound.baseVersion, content: structuredClone(content ?? profile.content) }), key: crypto.randomUUID(),
      expectedVersion: bound.baseVersion, nextRevision: bound.baseRevision + 1, unknown: false }))
  }
  async function discard(canRelease: () => boolean = () => true) {
    if (!available || running.current || frozen.current || !canRelease()) return false
    const originalDraft = draft.current, originalReceipt = receipt.current, bound = originalDraft ?? originalReceipt
    if (!bound || originalReceipt && !canShow) return false
    const captured = scope, c = new AbortController(); running.current = true; writer.current = c; setBusy(true)
    const timer = setTimeout(() => c.abort(), 15000)
    try {
      // An unsent local deletion requires a NEW actual original-owner observation;
      // it requires no restored profile write authority and reveals no private data.
      await fresh(captured, c.signal, bound)
      if (!valid(captured, c.signal) || frozen.current || draft.current !== originalDraft || receipt.current !== originalReceipt || !canRelease()) return false
      bind(null); receipt.current = null; setConfirmed(null); setContent(null); setConflict(false); setTrusted(null); setHistory(null); setReload(n => n + 1)
      return true
    } catch (error) { if (valid(captured, c.signal)) withdraw(error); return false }
    finally {
      clearTimeout(timer); running.current = false; if (writer.current === c) writer.current = null
      if (mounted.current) { setBusy(false); callbacks.current.onSaving(!!frozen.current || !!draft.current) }
    }
  }
  const invalidateAccess = useCallback((error: ApiError) => {
    if (![401, 403, 404].includes(error.status)) return false
    withdraw(error); return true
  }, [withdraw])
  return { profile: canShow ? confirmed?.row ?? profile : null, content: canShow ? confirmed?.row.content ?? content ?? profile?.content ?? null : null,
    history: canShow ? history : null, historyOffset, historyFailure, failure, busy, locked, canShow, scope, dirty: !!binding, attempt, confirmed, notice,
    uncertain: !!attempt, canRetry: canShow && !!attempt && !busy && profile?.vessel.canEdit === true,
    canDiscard: !busy && !attempt && !!(binding || confirmed) && (confirmed ? canShow : available),
    canLeave: () => live.current === scope && !running.current && !frozen.current && !draft.current && !receipt.current,
    capture, retry: () => { if (canShow && frozen.current && profile?.vessel.canEdit) void send(frozen.current) },
    edit, discard, invalidateAccess, loadFiles,
    recheck: () => { if (!running.current) { setTrusted(null); setHistory(null); setReload(value => value + 1) } },
    historyRetry: () => { if (!running.current) setHistoryAttempt(value => value + 1) },
    historyPage: (offset: number) => { if (!running.current) { setHistory(null); setHistoryOffset(offset) } },
  }
}
