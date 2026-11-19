import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import * as api from './qualification-api'
import { loadProfile } from './profile-api'
import type { Profile } from './profile-contract'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

type Binding = { owner: string; context: string; version: number }
type Confirmation = Binding & { minimumRevision: number; eventHash: string | null; eventState: string | null }
// Proof of the exact source bytes inside the command body: SHA-256 (hex) and length of the
// decoded bytes. The bytes themselves are never written to browser storage.
export type SourceProof = Readonly<{ sha256: string; bytes: number }>
export type QualificationAttempt = Readonly<Binding & {
  rule: string; action: 'events' | 'fact-attestations'; body: string; key: string; unknown: boolean; nextRevision: number
  // source: proof for content.sourceBytesBase64 (null when the body carries no source bytes).
  // withheld: restored from storage without the bytes; the exact retry needs the same file again.
  source: SourceProof | null; withheld: boolean
}>
type Props = {
  rule: string; ruleRevision: number; vessel: string; offline: boolean
  onDirty: (value: boolean) => void; onSaving: (value: boolean) => void
}

// S3-WPA / DEV-026 open finding QOR reload: an UNKNOWN-outcome envelope survives a hard
// reload in this tab's sessionStorage, so the same original attempt can only be explicitly
// retried and is never silently dropped, discarded or replaced by a new command.
// Privacy (docs/security-baseline.md; S3 gate B1): the stored record never contains the
// private source bytes (only their SHA-256 + length), CSRF or credentials; it carries a TTL;
// every record is purged on sign-out and whenever the app shell or this view observes a
// different authenticated owner (tenant/actor). A restored record whose bytes were withheld
// stays locked until the user re-supplies the identical file (hash + length match), so the
// retry is still the exact original command with its original Idempotency-Key.
const storageFamily = 'poseidon.qualification.unknown.'
const storagePrefix = `${storageFamily}v2:`
// Software retention choice (not a regulatory rule): one working shift.
export const unknownEnvelopeTtlMs = 8 * 60 * 60 * 1000
const clockSkewMs = 5 * 60 * 1000
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
function storage(): Storage | null { try { return window.sessionStorage } catch { return null } }
type BodyObject = Record<string, unknown> & { content?: unknown }
// undefined: no content object; null: no bytes; string: the base64 value (or '' placeholder).
const bodySource = (body: BodyObject): string | null | undefined => {
  const content = body.content
  if (!content || typeof content !== 'object' || Array.isArray(content)) return undefined
  const value = (content as Record<string, unknown>).sourceBytesBase64
  return typeof value === 'string' ? value : value == null ? null : undefined
}
const withSource = (body: string, base64: string) => {
  const parsed = JSON.parse(body) as BodyObject
  // Same key position as the original: JSON.parse/stringify keeps insertion order.
  ;(parsed.content as Record<string, unknown>).sourceBytesBase64 = base64
  return JSON.stringify(parsed)
}
async function sha256(bytes: Uint8Array<ArrayBuffer>) {
  return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)), b => b.toString(16).padStart(2, '0')).join('')
}
const decode = (base64: string) => Uint8Array.from(atob(base64), c => c.charCodeAt(0))
const encode = (bytes: Uint8Array) => { let binary = ''; bytes.forEach(value => { binary += String.fromCharCode(value) }); return btoa(binary) }
// Attaches the source proof before a command can become UNKNOWN; idempotent.
export async function proveSource(value: QualificationAttempt): Promise<QualificationAttempt> {
  if (value.source || value.withheld) return value
  const base64 = bodySource(JSON.parse(value.body) as BodyObject)
  if (!base64) return value
  const bytes = decode(base64)
  return { ...value, source: { sha256: await sha256(bytes), bytes: bytes.byteLength } }
}
export function persistUnknown(value: QualificationAttempt, now = Date.now()) {
  if (!value.unknown) return
  let body = value.body
  try {
    if (value.source) body = withSource(value.body, '')
    else if (bodySource(JSON.parse(value.body) as BodyObject)) return // Unproven bytes are never written.
  } catch { return }
  const record = { owner: value.owner, context: value.context, version: value.version, rule: value.rule, action: value.action,
    body, key: value.key, unknown: true, nextRevision: value.nextRevision, source: value.source, savedAt: now }
  try { storage()?.setItem(storagePrefix + value.rule, JSON.stringify(record)) } catch { /* Quota/denied: the in-memory envelope still locks the attempt. */ }
}
export function forgetUnknown(rule: string) { try { storage()?.removeItem(storagePrefix + rule) } catch { /* Unavailable storage holds nothing. */ } }
const ownerOf = (raw: string | null) => {
  try { const v = JSON.parse(raw ?? '') as { owner?: unknown } | null; return v && typeof v.owner === 'string' && v.owner ? v.owner : null } catch { return null }
}
// App shell / view hook: removes every stored UNKNOWN envelope (any format version) that
// does not belong to keepOwner; with no owner (sign-out, anonymous) removes all of them.
export function purgeQualificationRecovery(keepOwner?: string | null) {
  const s = storage(); if (!s) return
  try {
    const keys: string[] = []
    for (let i = 0; i < s.length; i++) { const k = s.key(i); if (k?.startsWith(storageFamily)) keys.push(k) }
    for (const k of keys) if (!keepOwner || !k.startsWith(storagePrefix) || ownerOf(s.getItem(k)) !== keepOwner) s.removeItem(k)
  } catch { /* Unavailable storage holds nothing. */ }
}
export type Restored = QualificationAttempt | Readonly<{ expired: string }> | null
export function restoreUnknown(rule: string, context: string, now = Date.now()): Restored {
  let raw: string | null
  try { raw = storage()?.getItem(storagePrefix + rule) ?? null } catch { return null }
  if (!raw) return null
  try {
    const v = JSON.parse(raw) as Record<string, unknown>
    const keys = ['owner', 'context', 'version', 'rule', 'action', 'body', 'key', 'unknown', 'nextRevision', 'source', 'savedAt']
    if (!v || typeof v !== 'object' || Array.isArray(v) || Object.keys(v).length !== keys.length || keys.some(k => !Object.hasOwn(v, k)) ||
      typeof v.owner !== 'string' || !v.owner || v.rule !== rule || typeof v.context !== 'string' || v.unknown !== true ||
      (v.action !== 'events' && v.action !== 'fact-attestations') || typeof v.body !== 'string' || typeof v.key !== 'string' || !uuid.test(v.key) ||
      typeof v.version !== 'number' || !Number.isSafeInteger(v.version) || v.version < 0 || typeof v.nextRevision !== 'number' || !Number.isSafeInteger(v.nextRevision) || v.nextRevision < 1 ||
      typeof v.savedAt !== 'number' || !Number.isSafeInteger(v.savedAt) || v.savedAt > now + clockSkewMs) {
      forgetUnknown(rule); return null
    }
    const proof = v.source as Record<string, unknown> | null
    if (proof !== null && (!proof || typeof proof !== 'object' || Array.isArray(proof) || Object.keys(proof).length !== 2 || typeof proof.sha256 !== 'string' ||
      !/^[0-9a-f]{64}$/.test(proof.sha256) || typeof proof.bytes !== 'number' || !Number.isSafeInteger(proof.bytes) || proof.bytes < 1 || proof.bytes > 2097152)) {
      forgetUnknown(rule); return null
    }
    const body = JSON.parse(v.body) as unknown
    if (!body || typeof body !== 'object' || Array.isArray(body)) { forgetUnknown(rule); return null }
    // Stored bytes are malformed either way: present bytes, or a placeholder without proof.
    const stored = bodySource(body as BodyObject)
    if (proof ? stored !== '' : typeof stored === 'string') { forgetUnknown(rule); return null }
    // TTL: an expired record is removed; only its owner is later told that an attempt expired.
    if (now - v.savedAt > unknownEnvelopeTtlMs) { forgetUnknown(rule); return { expired: v.owner } }
    // Another rule revision/vessel context keeps the stored envelope; it is simply not this view's attempt.
    if (v.context !== context) return null
    const { savedAt: _savedAt, ...attempt } = v
    void _savedAt
    return { ...attempt, withheld: proof !== null } as unknown as QualificationAttempt
  } catch { forgetUnknown(rule); return null }
}
const restoredContent = (value: QualificationAttempt | null): api.Content | null => {
  if (!value || value.action !== 'events') return null
  const body = JSON.parse(value.body) as { content?: unknown }
  if (!body.content || typeof body.content !== 'object' || Array.isArray(body.content)) return null
  const content = body.content as api.Content
  // A withheld placeholder is not displayed as content; the bytes are re-supplied explicitly.
  return content.sourceBytesBase64 === '' ? { ...content, sourceBytesBase64: null } : content
}

// The envelope belongs to one actual session and source context. Source reads
// may restore access but never prove whether an earlier unknown write committed.
export function useQualificationRecovery({ rule, ruleRevision, vessel, offline, onDirty, onSaving }: Props) {
  const epoch = useWorkspaceRevalidation(), [reload, setReload] = useState(0)
  const available = !offline && navigator.onLine && document.visibilityState !== 'hidden'
  const context = `${rule}:${ruleRevision}:${vessel}`, scope = `${context}:${epoch}:${reload}:${available}`
  const [initial] = useState(() => restoreUnknown(rule, context))
  const restored = initial && 'expired' in initial ? null : initial
  // Owner of an envelope whose TTL elapsed: shown only to that owner, never revived.
  const [expired] = useState(() => initial && 'expired' in initial ? initial.expired : null)
  const [sourceMismatch, setSourceMismatch] = useState(false)
  const [data, setData] = useState<api.Page | null>(null), [content, setContent] = useState(() => restoredContent(restored) ?? api.blank())
  const [state, setState] = useState('ADOPTED'), [until, setUntil] = useState('')
  const [profile, setProfile] = useState<Profile | null>(null)
  const [historyRows, setHistoryRows] = useState<api.Qualification[]>([]), [historyMore, setHistoryMore] = useState(false)
  const [trusted, setTrusted] = useState<{ scope: string; owner: string } | null>(null)
  const [observed, setObserved] = useState<{ scope: string; owner: string } | null>(null)
  const [binding, setBinding] = useState<Binding | null>(null), [dirty, setDirty] = useState(false)
  const [attempt, setAttempt] = useState<QualificationAttempt | null>(restored)
  const [confirmed, setConfirmed] = useState<Confirmation | null>(null), [notice, setNotice] = useState<Binding | null>(null)
  const [busy, setBusy] = useState(false), [error, setError] = useState<number | null>(null), [conflict, setConflict] = useState(false)
  const mounted = useRef(false), running = useRef(false), live = useRef('')
  const writer = useRef<AbortController | null>(null), frozen = useRef<QualificationAttempt | null>(restored)
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
  const keep = (value: QualificationAttempt | null) => {
    const prior = frozen.current
    frozen.current = value; setAttempt(value)
    if (value?.unknown) persistUnknown(value)
    else if (!value && prior) forgetUnknown(prior.rule)
  }
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
    // A different authenticated owner must not keep another actor's private envelope at rest.
    purgeQualificationRecovery(user.owner)
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
    if (!available || running.current || value.context !== context || value.withheld) return
    sourceEpoch.current++
    const captured = scope, c = new AbortController(), wasUnknown = value.unknown
    let dispatched = false, outcomeReceived = false
    running.current = true; writer.current = c; setBusy(true); setError(null); setTrusted(null)
    keep(value); callbacks.current.onDirty(true); callbacks.current.onSaving(true)
    const timer = setTimeout(() => c.abort(), 15000)
    try {
      // The proof exists before the command can become UNKNOWN, so no stored record holds bytes.
      value = await proveSource(value)
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
    void send({ ...expected, rule, action, body: JSON.stringify(body), key: crypto.randomUUID(), unknown: false, nextRevision: (current?.revision ?? 0) + 1, source: null, withheld: false })
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
      edit({ sourceBytesBase64: encode(bytes) })
    } catch { if (mayApply()) setError(400) }
  }
  // Re-supplies the bytes of a withheld UNKNOWN envelope: only the identical file (SHA-256 +
  // length) restores the exact original body; anything else keeps the attempt locked.
  async function resupply(file: File | undefined) {
    const value = frozen.current
    if (!file || !value?.withheld || !value.source || running.current || busy || !owner || value.owner !== owner || value.context !== context) return
    try {
      const bytes = new Uint8Array(await file.arrayBuffer())
      const matches = bytes.byteLength === value.source.bytes && await sha256(bytes) === value.source.sha256
      if (!mounted.current || frozen.current !== value) return
      setSourceMismatch(!matches)
      if (matches) keep({ ...value, body: withSource(value.body, encode(bytes)), withheld: false })
    } catch { if (mounted.current && frozen.current === value) setSourceMismatch(true) }
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
    available, canRetry: available && !busy && !!attempt && !attempt.withheld && attempt.owner === owner && attempt.context === context,
    withheld: !!attempt?.withheld && attempt.owner === owner && attempt.context === context, sourceMismatch,
    expired: !!expired && expired === owner && !attempt, resupply,
    edit, chooseState: (value: string) => choose(value, setState), chooseUntil: (value: string) => choose(value, setUntil),
    send, capture, transition, source, discard, recheck, more }
}
