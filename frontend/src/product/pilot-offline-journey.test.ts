import { describe, expect, it } from 'vitest'
import { classifyPilotReplayFailure, projectPilotRows, syncPilotJourney, type PilotFlow, type PilotFlowAdapter, type PilotOutcome, type PilotRow } from './pilot-offline-journey'
import { AttachmentRetryQueue, attachmentIdempotencyKey, classifyAttachmentFailure, sha256Hex, type AttachmentIntent, type AttachmentTransport, type StagedFile } from './attachment-retry'

// S10 WP-C DEV-066 (18.01 sheet:Backlog/row:67) full pilot offline journey; extends the DEV-038 chaos pack
// (offline-chaos.test.ts) across the three pilot flows: certificate renewal draft, safety event capture and
// work order evidence. Deterministic scripted faults only: network drop, reconnect, duplicate send, lost
// response, material conflict, session expiry and attachment retry. Each case asserts no data loss, no
// duplicate server effect and no false "saved" receipt.
type Fault = 'drop' | 'lostResponse' | 'conflict' | 'expired' | null
class ScriptedFlow implements PilotFlowAdapter {
  rows: PilotRow[] = []; effects = 0; faults: Fault[] = []; server = new Map<string, PilotOutcome>()
  constructor(readonly flow: PilotFlow) {}
  capture(body: object = {}) { const commandId = crypto.randomUUID(); this.rows.push({ commandId, body: JSON.stringify({ commandId, ...body }) }); return commandId }
  async captures() { return this.rows.map(r => ({ ...r })) }
  async replay(row: PilotRow) {
    const fault = this.faults.shift() ?? null
    if (fault === 'drop') throw new TypeError('Failed to fetch')
    if (fault === 'expired') throw Object.assign(new Error('unauthenticated'), { status: 401, code: 'unauthenticated' })
    // Idempotent server: the same command id returns the first immutable outcome without a second effect.
    let outcome = this.server.get(row.commandId)
    if (!outcome) { this.effects += fault === 'conflict' ? 0 : 1; outcome = { result: fault === 'conflict' ? 'MaterialConflict' : 'Committed' }; this.server.set(row.commandId, outcome) }
    if (fault === 'lostResponse') throw new TypeError('network error after commit')
    const stored = this.rows.find(r => r.commandId === row.commandId)!; stored.outcome = outcome
    return outcome
  }
}
const three = () => [new ScriptedFlow('certificateRenewal'), new ScriptedFlow('safetyEvent'), new ScriptedFlow('workOrderEvidence')]
const signal = () => new AbortController().signal

describe('DEV-066 pilot offline journey across three flows', () => {
  it('keeps every capture pending while offline and commits each exactly once on reconnect', async () => {
    const flows = three(); for (const f of flows) { f.capture(); f.capture() }
    const offline = await syncPilotJourney(flows, signal(), () => false)
    expect(offline.map(l => [l.flow, l.pending, l.committed, l.attempted, l.lost])).toEqual([['certificateRenewal', 2, 0, 0, 0], ['safetyEvent', 2, 0, 0, 0], ['workOrderEvidence', 2, 0, 0, 0]])
    const online = await syncPilotJourney(flows, signal(), () => true)
    expect(online.every(l => l.committed === 2 && l.pending === 0 && l.lost === 0)).toBe(true)
    expect(flows.map(f => f.effects)).toEqual([2, 2, 2])
    // A second reconnect never replays a recorded capture.
    const again = await syncPilotJourney(flows, signal(), () => true)
    expect(again.every(l => l.attempted === 0)).toBe(true); expect(flows.map(f => f.effects)).toEqual([2, 2, 2])
  })
  it('a network drop mid-sync or a lost response leaves the capture pending and the retry yields one server effect', async () => {
    const [renewal, safety, order] = three(); renewal.capture(); safety.capture(); order.capture()
    renewal.faults = ['drop']; safety.faults = ['lostResponse']
    const first = await syncPilotJourney([renewal, safety, order], signal(), () => true)
    expect(first.map(l => l.pending)).toEqual([1, 1, 0]); expect(first[0].failures.offline).toBe(1); expect(first.every(l => l.lost === 0))
    const second = await syncPilotJourney([renewal, safety, order], signal(), () => true)
    expect(second.map(l => l.committed)).toEqual([1, 1, 1]); expect([renewal.effects, safety.effects, order.effects]).toEqual([1, 1, 1])
  })
  it('records a material conflict for human review instead of last-write-wins and keeps it open until a terminal child', async () => {
    const order = new ScriptedFlow('workOrderEvidence'); const conflicted = order.capture(); order.faults = ['conflict']
    const [ledger] = await syncPilotJourney([order], signal(), () => true)
    expect(ledger).toMatchObject({ conflicts: 1, committed: 0, pending: 0, lost: 0 }); expect(order.effects).toBe(0)
    order.capture({ parentCommandId: conflicted, choice: 'keepServer' })
    const [resolved] = await syncPilotJourney([order], signal(), () => true)
    expect(resolved).toMatchObject({ conflicts: 0, committed: 1, retained: 2 })
  })
  it('stops a flow on session expiry without discarding captures and classifies server answers honestly', async () => {
    const renewal = new ScriptedFlow('certificateRenewal'); renewal.capture(); renewal.capture(); renewal.faults = ['expired']
    const [ledger] = await syncPilotJourney([renewal], signal(), () => true)
    expect(ledger).toMatchObject({ attempted: 1, pending: 2, lost: 0 }); expect(ledger.failures.reauthorize).toBe(1)
    expect(classifyPilotReplayFailure(Object.assign(new Error(), { status: 409, code: 'offline_conflict_changed' }))).toBe('conflict')
    expect(classifyPilotReplayFailure(Object.assign(new Error(), { status: 409, code: 'offline_command_reused' }))).toBe('rejected')
    expect(classifyPilotReplayFailure(Object.assign(new Error(), { status: 503, code: 'offline_retry' }))).toBe('retry')
    expect(classifyPilotReplayFailure(new Error('reauthorization_required_data_retained'))).toBe('reauthorize')
    expect(projectPilotRows('safetyEvent', [{ commandId: 'a', body: 'not-json' }])).toMatchObject({ pending: 1, committed: 0 })
  })
})

// Fake SecureFile server mirroring SecureFileEndpoints idempotency: same Idempotency-Key + same intent returns the
// existing stage; an upload to a CleanTransport stage returns the stored file.
class FileServer implements AttachmentTransport {
  stages = new Map<string, StagedFile & { fingerprint: string }>(); uploads = 0; faults: ('drop' | 'dropAfterUpload' | 'frozen')[] = []
  async stage(intent: AttachmentIntent, key: string) {
    if (this.faults[0] === 'drop') { this.faults.shift(); throw new TypeError('Failed to fetch') }
    const fingerprint = JSON.stringify(intent), existing = this.stages.get(key)
    if (existing) { if (existing.fingerprint !== fingerprint) throw Object.assign(new Error(), { status: 409, code: 'idempotency_key_reused' }); return { ...existing } }
    const created = { id: crypto.randomUUID(), state: 'Pending', checksum: null, fingerprint }; this.stages.set(key, created); return { ...created }
  }
  async upload(intent: AttachmentIntent, id: string) {
    const stage = [...this.stages.values()].find(s => s.id === id)!
    if (this.faults[0] === 'frozen') { this.faults.shift(); throw Object.assign(new Error(), { status: 409, code: 'file_attempt_frozen' }) }
    if (stage.state !== 'CleanTransport') { this.uploads++; stage.state = 'CleanTransport'; stage.checksum = intent.checksum }
    if (this.faults[0] === 'dropAfterUpload') { this.faults.shift(); throw new TypeError('connection reset') }
    return { id: stage.id, state: stage.state, checksum: stage.checksum }
  }
}
const bytes = new TextEncoder().encode('FICTIONAL attachment bytes')
const intentFor = async (): Promise<AttachmentIntent> => ({ vessel: '11111111-1111-4111-8111-111111111111', name: 'photo.png', mime: 'image/png', length: bytes.byteLength, checksum: await sha256Hex(bytes) })
const queue = (server: FileServer, online = () => true) => new AttachmentRetryQueue(server, 'actor-1', { maxAttempts: 3, online, wait: async () => undefined })

describe('DEV-066 attachment retry', () => {
  it('derives a stable server-valid key so the same hash never creates a second file record', async () => {
    const intent = await intentFor(), key = await attachmentIdempotencyKey('actor-1', intent)
    expect(key).toMatch(/^att-[0-9a-f]{60}$/); expect(await attachmentIdempotencyKey('actor-1', intent)).toBe(key)
    expect(await attachmentIdempotencyKey('actor-2', intent)).not.toBe(key)
    const server = new FileServer(); server.faults = ['drop', 'dropAfterUpload']
    const result = await queue(server).send(intent, bytes)
    expect(result).toMatchObject({ result: 'Stored', attempts: 3 }); expect(server.stages.size).toBe(1); expect(server.uploads).toBe(1)
    // A duplicate send after success reuses the same stage and uploads nothing again.
    expect(await queue(server).send(intent, bytes)).toMatchObject({ result: 'Stored', stageId: result.stageId, attempts: 1 }); expect(server.uploads).toBe(1)
  })
  it('never reports stored while offline, for a frozen stage or for mismatched bytes', async () => {
    const intent = await intentFor(), server = new FileServer()
    expect(await queue(server, () => false).send(intent, bytes)).toMatchObject({ result: 'RetryPending', attempts: 0 }); expect(server.stages.size).toBe(0)
    server.faults = ['frozen']; expect(await queue(server).send(intent, bytes)).toMatchObject({ result: 'ReconciliationRequired', code: 'file_attempt_frozen' })
    expect(await queue(server).send({ ...intent, checksum: 'A'.repeat(64) }, bytes)).toMatchObject({ result: 'Rejected', code: 'file_checksum_invalid' })
    expect(classifyAttachmentFailure(Object.assign(new Error(), { status: 403, code: 'permission_denied' }))).toBe('rejected')
    expect(classifyAttachmentFailure(Object.assign(new Error(), { status: 503, code: 'file_settings_unavailable' }))).toBe('retryable')
  })
})
