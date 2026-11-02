import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { base64, hash, vault, type KeySlot } from './offline-application'
import { openOfflineRecord, sealOfflineRecord } from './offline-cipher'
import { OfflineEncryptedRecordStore } from './offline-record-store'
import { offlineKeys as exact, offlineInteger as integer } from './offline-capture-contract'

export const tugOfflineSource = '14.02:2316:1951-1952;11.01:2316:1951-1952:v46.3'
export const tugOfflineOperation = 'tug.draft.window.v1'
export type WindowValues = { plannedStart: string | null; plannedEnd: string | null }
export type TugOfflineContext = WindowValues & { id: string; tenantId: string; actorId: string; vesselId: string; jobId: string;
  deviceId: string; keyId: string; challenge: string; policyVersion: string; sourceVersion: string; requestVersion: number;
  baseVersion: number; baseRevision: number; serverTime: string; expiresAt: string }
export type TugCapture = WindowValues & { commandId: string; keyId: string; deviceId: string; policyVersion: string; sourceVersion: string;
  operation: string; baseVersion: number; requestVersion: number; capturedAt: string; offsetMinutes: number;
  parentCommandId?: string; expectedServerVersion?: number; choice?: 'keepServer' | 'useCaptured'; reason?: string }
export type TugOfflineResult = { commandId: string; result: 'Committed' | 'MaterialConflict'; serverVersion: number; revision: number; parentCommandId: string | null }
export type TugPending = { commandId: string; revision: number; body: string; outcome?: TugOfflineResult }
export type TugConflict = { commandId: string; currentVersion: number; base: WindowValues; captured: WindowValues; server: WindowValues; choices: string[] }
const bad = (): never => { throw new ApiError(502, 'invalid_tug_offline_contract') }
const utc = (v: unknown) => typeof v === 'string' && timestamp(v) && /(?:Z|\+00:00)$/.test(v)
export const windowValues = (v: unknown): v is WindowValues => exact(v, ['plannedStart', 'plannedEnd']) &&
  [v.plannedStart, v.plannedEnd].every(x => x === null || utc(x)) &&
  (v.plannedStart === null || v.plannedEnd === null || Date.parse(String(v.plannedStart)) <= Date.parse(String(v.plannedEnd)))
export function decodeTugOfflineContext(v: unknown, job: string, vessel: string): TugOfflineContext {
  if (!exact(v, ['id', 'tenantId', 'actorId', 'vesselId', 'jobId', 'deviceId', 'keyId', 'challenge', 'policyVersion', 'sourceVersion',
    'requestVersion', 'baseVersion', 'baseRevision', 'plannedStart', 'plannedEnd', 'serverTime', 'expiresAt']) ||
    ['id', 'tenantId', 'actorId', 'deviceId', 'keyId'].some(k => !isGuid(v[k])) || v.jobId !== job || v.vesselId !== vessel ||
    !windowValues({ plannedStart: v.plannedStart, plannedEnd: v.plannedEnd }) || v.sourceVersion !== tugOfflineSource ||
    typeof v.challenge !== 'string' || !/^[A-Za-z0-9+/]{43}=$/.test(v.challenge) ||
    typeof v.policyVersion !== 'string' || !v.policyVersion.length || v.policyVersion.length > 80 ||
    !integer(v.requestVersion, 1, 4294967295) || !integer(v.baseVersion, 1, 4294967295) || !integer(v.baseRevision, 1, Number.MAX_SAFE_INTEGER) ||
    !utc(v.serverTime) || !utc(v.expiresAt) || Date.parse(String(v.expiresAt)) <= Date.parse(String(v.serverTime))) return bad()
  return v as TugOfflineContext
}
export function decodeTugCapture(v: unknown, ctx: TugOfflineContext, id: string): TugCapture {
  if (!exact(v, ['commandId', 'keyId', 'deviceId', 'policyVersion', 'sourceVersion', 'operation', 'baseVersion', 'requestVersion', 'capturedAt', 'offsetMinutes', 'plannedStart', 'plannedEnd'],
    ['parentCommandId', 'expectedServerVersion', 'choice', 'reason']) || v.commandId !== id || !isGuid(id) ||
    v.keyId !== ctx.keyId || v.deviceId !== ctx.deviceId || v.policyVersion !== ctx.policyVersion || v.sourceVersion !== tugOfflineSource ||
    v.operation !== tugOfflineOperation || v.requestVersion !== ctx.requestVersion || !utc(v.capturedAt) || !integer(v.offsetMinutes, -840, 840) ||
    !windowValues({ plannedStart: v.plannedStart, plannedEnd: v.plannedEnd }) ||
    (v.parentCommandId === undefined ? v.baseVersion !== ctx.baseVersion || ['expectedServerVersion', 'choice', 'reason'].some(k => Object.hasOwn(v, k)) :
      !isGuid(v.parentCommandId) || !integer(v.baseVersion, 1, 4294967295) || v.expectedServerVersion !== v.baseVersion ||
      !['keepServer', 'useCaptured'].includes(String(v.choice)) || !['INPUT_CORRECTED', 'CURRENT_SERVER_RETAINED'].includes(String(v.reason)))) return bad()
  return v as TugCapture
}
function outcome(v: unknown, c?: TugCapture): TugOfflineResult {
  if (!exact(v, ['commandId', 'result', 'serverVersion', 'revision', 'parentCommandId']) || !isGuid(v.commandId) ||
    !['Committed', 'MaterialConflict'].includes(String(v.result)) || !integer(v.serverVersion, 1, 4294967295) ||
    !integer(v.revision, 1, Number.MAX_SAFE_INTEGER) || v.parentCommandId !== null && !isGuid(v.parentCommandId) ||
    c && (v.commandId !== c.commandId || v.parentCommandId !== (c.parentCommandId ?? null))) return bad()
  return v as TugOfflineResult
}
export const tugOfflineRoot = (job: string, vessel: string, suffix = '') => {
  if (!isGuid(job) || !isGuid(vessel)) return bad()
  return `tug-jobs/${job}/plan/offline${suffix}?vessel=${vessel}`
}
export async function tugOfflineInventory(job: string, vessel: string, signal: AbortSignal) {
  const v = await request<unknown>(tugOfflineRoot(job, vessel), signal)
  if (!exact(v, ['items']) || !Array.isArray(v.items) || v.items.length > 32 || v.items.some(e =>
    !exact(e, ['id', 'expiresAt', 'revokedAt']) || !isGuid(e.id) || !utc(e.expiresAt) || e.revokedAt !== null && !utc(e.revokedAt))) return bad()
  return v.items as { id: string; expiresAt: string; revokedAt: string | null }[]
}
export async function reportTugLostKey(job: string, vessel: string, enrollment: string, signal: AbortSignal) {
  if (!isGuid(enrollment)) return bad()
  const user = await session(signal); if (!user.authenticated) throw new ApiError(401)
  return request(tugOfflineRoot(job, vessel, `/${enrollment}/lost`), signal,
    { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
}
const quotas = { maxRecords: 64, maxEncryptedBytes: 262144, maxDeviceRecords: 512, maxDeviceEncryptedBytes: 4194304 }
const limits = { plaintextBytes: 4096, contextBytes: 2048 }
// Shares the existing nonextractable-key vault, authenticated cipher and atomic encrypted store.
// A restart has no authority until online renewal; no personal/protected snapshot is cached.
export class TugOfflineApplication {
  private closed = false
  private channel: BroadcastChannel
  private constructor(readonly context: TugOfflineContext, private keys: KeySlot, private slot: string,
    private store: OfflineEncryptedRecordStore, private started: number, private boundary: string) {
    this.channel = new BroadcastChannel('poseidon.offline.authorization.v1')
    this.channel.onmessage = e => { if (e.data?.boundary === boundary && e.data?.type === 'withdraw') this.close(false) }
    this.channel.postMessage({ boundary, type: 'withdraw' })
  }
  static async authorize(job: string, vessel: string, enrollment: string | null, signal: AbortSignal) {
    const user = await session(signal); if (!user.authenticated || !user.actorId || !user.tenantId) throw new ApiError(401)
    const started = performance.now()
    const ctx = decodeTugOfflineContext(await request<unknown>(tugOfflineRoot(job, vessel, enrollment ? `/${enrollment}/renew` : ''), signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } }), job, vessel)
    if (ctx.actorId !== user.actorId || ctx.tenantId !== user.tenantId || signal.aborted) throw new ApiError(403)
    const slot = await hash(JSON.stringify(['tug-window-v1', ctx.tenantId, ctx.actorId, vessel, job, ctx.id, ctx.deviceId, ctx.keyId, ctx.policyVersion]))
    let keys = await vault<KeySlot | null>((s, done) => { const q = s.get(slot); q.onsuccess = () => done(q.result ?? null) })
    if (!keys && enrollment) throw new Error('key_lost_data_retained')
    if (!keys) {
      const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
      const signing = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify'])
      keys = await vault<KeySlot>((s, done) => {
        const q = s.get(slot); q.onsuccess = () => {
          if (q.result) { done(q.result); return }
          const n = s.count(); n.onsuccess = () => { if (n.result >= 512) { s.transaction.abort(); return } const row = { id: slot, key, signing, invocations: 0 }; s.add(row); done(row) }
        }
      })
    }
    if (!keys.key || keys.key.extractable || !keys.signing?.privateKey || keys.signing.privateKey.extractable ||
      keys.signing.privateKey.algorithm.name !== 'ECDSA' || (keys.signing.privateKey.algorithm as EcKeyAlgorithm).namedCurve !== 'P-256') throw new Error('key_lost_data_retained')
    const store = await OfflineEncryptedRecordStore.open(indexedDB, slot, quotas)
    const app = new TugOfflineApplication(ctx, keys, slot, store, started, await hash(JSON.stringify(['tug', ctx.tenantId, ctx.actorId, vessel, job])))
    try {
      await app.reserve()
      const publicKey = base64(await crypto.subtle.exportKey('spki', keys.signing.publicKey))
      const proof = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, keys.signing.privateKey, new TextEncoder().encode(`${ctx.id}/${ctx.keyId}/${ctx.challenge}`)))
      const status = await request<unknown>(tugOfflineRoot(job, vessel, `/${ctx.id}/key`), signal,
        { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Content-Type': 'application/json' }, body: JSON.stringify({ publicKey, proof }) })
      if (!exact(status, ['state']) || status.state !== 'KeyEnrolled') return bad()
      if (signal.aborted) throw new Error('authorization_withdrawn')
      app.requireActive(); return app
    } catch (e) { app.close(); throw e }
  }
  active() { return !this.closed && performance.now() >= this.started && performance.now() - this.started < Date.parse(this.context.expiresAt) - Date.parse(this.context.serverTime) }
  private requireActive() { if (!this.active()) throw new Error('reauthorization_required_data_retained') }
  private async reserve() {
    this.requireActive()
    await vault<void>((s, done) => { const q = s.get(this.slot); q.onsuccess = () => {
      const row = q.result as KeySlot | undefined
      if (!row || row.invocations >= 1024) { s.transaction.abort(); return } s.put({ ...row, invocations: row.invocations + 1 }); done(undefined)
    } })
  }
  private aad(commandId: string) { return { ...this.context, sessionBinding: this.context.id, commandId } }
  private async write(commandId: string, body: string, revision: number | null, result?: TugOfflineResult) {
    await this.reserve()
    const sealed = await sealOfflineRecord(crypto, this.keys.key, this.aad(commandId), new TextEncoder().encode(JSON.stringify({ body, ...(result ? { outcome: result } : {}) })), limits)
    this.requireActive(); await this.store.put(commandId, sealed, revision)
  }
  async captures(): Promise<TugPending[]> {
    this.requireActive(); const result: TugPending[] = []
    for (const row of await this.store.list(64)) {
      const stored = await this.store.read(row.commandId); if (!stored) continue
      const v: unknown = JSON.parse(new TextDecoder().decode(await openOfflineRecord(crypto, this.keys.key, this.aad(row.commandId), stored.sealed, limits)))
      if (!exact(v, ['body'], ['outcome']) || typeof v.body !== 'string') return bad()
      const body = decodeTugCapture(JSON.parse(v.body), this.context, row.commandId)
      result.push({ commandId: row.commandId, revision: stored.revision, body: v.body, ...(v.outcome === undefined ? {} : { outcome: outcome(v.outcome, body) }) })
    }
    this.requireActive(); return result
  }
  async capture(values: WindowValues, resolution?: { conflict: TugConflict; choice: 'keepServer' | 'useCaptured' }) {
    this.requireActive(); if (!windowValues(values)) return bad()
    const commandId = crypto.randomUUID()
    const body = JSON.stringify({ ...values, commandId, keyId: this.context.keyId, deviceId: this.context.deviceId, policyVersion: this.context.policyVersion,
      sourceVersion: tugOfflineSource, operation: tugOfflineOperation, requestVersion: this.context.requestVersion,
      baseVersion: resolution?.conflict.currentVersion ?? this.context.baseVersion, capturedAt: new Date().toISOString(), offsetMinutes: -new Date().getTimezoneOffset(),
      ...(resolution ? { parentCommandId: resolution.conflict.commandId, expectedServerVersion: resolution.conflict.currentVersion, choice: resolution.choice,
        reason: resolution.choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED' } : {}) })
    decodeTugCapture(JSON.parse(body), this.context, commandId); await this.write(commandId, body, null); return this.captures()
  }
  async replay(capture: TugPending, signal: AbortSignal) {
    this.requireActive()
    const stored = (await this.captures()).find(x => x.commandId === capture.commandId)
    if (!stored || stored.revision !== capture.revision || stored.body !== capture.body) return bad()
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.context.actorId || user.tenantId !== this.context.tenantId) { this.close(); throw new ApiError(403) }
    await this.reserve(); const digest = (await hash(capture.body)).toUpperCase()
    const proof = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, this.keys.signing.privateKey,
      new TextEncoder().encode(`${this.context.id}/${capture.commandId}/${digest}`)))
    this.requireActive()
    const result = outcome(await request<unknown>(tugOfflineRoot(this.context.jobId, this.context.vesselId, `/${this.context.id}/replay`), signal,
      { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken, 'X-Offline-Proof': proof }, body: capture.body }), decodeTugCapture(JSON.parse(capture.body), this.context, capture.commandId))
    if (signal.aborted) throw new Error('response_withdrawn')
    await this.write(capture.commandId, capture.body, capture.revision, result); return result
  }
  async conflict(id: string, signal: AbortSignal): Promise<TugConflict> {
    this.requireActive()
    const v = await request<unknown>(tugOfflineRoot(this.context.jobId, this.context.vesselId, `/${this.context.id}/conflicts/${id}`), signal)
    if (!exact(v, ['commandId', 'currentVersion', 'base', 'captured', 'server', 'choices']) || v.commandId !== id ||
      !integer(v.currentVersion, 1, 4294967295) || !windowValues(v.base) || !windowValues(v.captured) || !windowValues(v.server) ||
      !Array.isArray(v.choices) || v.choices.length > 2 || new Set(v.choices).size !== v.choices.length || v.choices.some(x => !['keepServer', 'useCaptured'].includes(String(x)))) return bad()
    this.requireActive(); return v as TugConflict
  }
  async lifecycle(action: 'revoke' | 'lost', signal: AbortSignal) {
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.context.actorId || user.tenantId !== this.context.tenantId) { this.close(); throw new ApiError(403) }
    await request(tugOfflineRoot(this.context.jobId, this.context.vesselId, `/${this.context.id}/${action}`), signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } }); this.close()
  }
  close(announce = true) {
    if (this.closed) return
    this.closed = true; this.store.close()
    if (announce) this.channel.postMessage({ boundary: this.boundary, type: 'withdraw' })
    this.channel.close()
  }
}
