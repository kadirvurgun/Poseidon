// S10 WP-C DEV-066 attachment retry (18.01 sheet:Backlog/row:67 "attachment retry"). The secure file intent is
// staged with an Idempotency-Key DERIVED from the actor, vessel and exact content hash, so a retry after a
// dropped connection, reload or duplicate tap reaches the same server stage and never creates a second file
// record for the same bytes (SecureFileEndpoints: same key + same fingerprint returns the existing stage; an
// upload of an already CleanTransport stage returns the stored file). Ambiguous finalization is never shown as
// stored: a frozen stage is ReconciliationRequired. Bytes are held in memory only; durable offline binary
// storage stays closed until an authorized classification/retention decision exists (evidence files are online).
// This module is dependency-free so the browser harness can load the exact source.
export type AttachmentIntent = { vessel: string; name: string; mime: string; length: number; checksum: string }
export type StagedFile = { id: string; state: string; checksum: string | null }
export type AttachmentTransport = {
  stage(intent: AttachmentIntent, key: string): Promise<StagedFile>
  upload(intent: AttachmentIntent, stageId: string, bytes: Uint8Array<ArrayBuffer>): Promise<StagedFile>
}
export type AttachmentFailure = 'network' | 'retryable' | 'frozen' | 'rejected'
export type AttachmentResult = { result: 'Stored' | 'RetryPending' | 'ReconciliationRequired' | 'Rejected'; key: string; stageId: string | null; attempts: number; code: string | null }
export type AttachmentRetryOptions = { maxAttempts: number; online: () => boolean; wait: (attempt: number) => Promise<void> }

export async function sha256Hex(bytes: Uint8Array<ArrayBuffer>): Promise<string> {
  const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))
  return Array.from(digest, b => b.toString(16).padStart(2, '0')).join('').toUpperCase()
}
// 'att-' + 60 hex characters: inside the server key contract (16..80 ASCII letters/digits/hyphen).
export async function attachmentIdempotencyKey(actor: string, intent: AttachmentIntent): Promise<string> {
  const material = new TextEncoder().encode(JSON.stringify(['attachment-retry-v1', actor, intent.vessel, intent.checksum.toUpperCase(), intent.name, intent.mime, intent.length]))
  return 'att-' + (await sha256Hex(material)).slice(0, 60).toLowerCase()
}
export function classifyAttachmentFailure(error: unknown): AttachmentFailure {
  if (error instanceof TypeError) return 'network'
  const status = error && typeof error === 'object' && 'status' in error ? Number((error as { status: unknown }).status) : NaN
  const code = error && typeof error === 'object' && 'code' in error ? String((error as { code: unknown }).code) : ''
  if (!Number.isFinite(status) || status === 0) return 'network'
  if (code === 'file_attempt_frozen') return 'frozen'
  if (status === 503 || status === 502 || status === 504 || status === 429 || status === 409 && code === 'file_attempt_conflict') return 'retryable'
  return 'rejected'
}

export class AttachmentRetryQueue {
  // Explicit fields (no parameter properties) so the exact source stays type-strippable for the browser harness.
  private transport: AttachmentTransport; private actor: string; private options: AttachmentRetryOptions
  constructor(transport: AttachmentTransport, actor: string, options: AttachmentRetryOptions) { this.transport = transport; this.actor = actor; this.options = options }
  // Never invents success: Stored only after the server returns the CleanTransport file with the exact checksum.
  async send(intent: AttachmentIntent, bytes: Uint8Array<ArrayBuffer>): Promise<AttachmentResult> {
    if (bytes.byteLength !== intent.length || await sha256Hex(bytes) !== intent.checksum.toUpperCase()) return { result: 'Rejected', key: '', stageId: null, attempts: 0, code: 'file_checksum_invalid' }
    const key = await attachmentIdempotencyKey(this.actor, intent)
    let stageId: string | null = null, attempts = 0, code: string | null = null
    while (attempts < this.options.maxAttempts) {
      if (!this.options.online()) return { result: 'RetryPending', key, stageId, attempts, code: 'offline' }
      attempts++
      try {
        const staged = await this.transport.stage(intent, key)
        stageId = staged.id
        const stored = staged.state === 'CleanTransport' ? staged : await this.transport.upload(intent, staged.id, bytes)
        if (stored.id !== staged.id) return { result: 'ReconciliationRequired', key, stageId, attempts, code: 'stage_mismatch' }
        if (stored.state === 'CleanTransport' && stored.checksum?.toUpperCase() === intent.checksum.toUpperCase()) return { result: 'Stored', key, stageId, attempts, code: null }
        return { result: 'ReconciliationRequired', key, stageId, attempts, code: 'file_not_clean' }
      } catch (error) {
        const failure = classifyAttachmentFailure(error)
        code = error && typeof error === 'object' && 'code' in error ? String((error as { code: unknown }).code) : failure
        if (failure === 'frozen') return { result: 'ReconciliationRequired', key, stageId, attempts, code }
        if (failure === 'rejected') return { result: 'Rejected', key, stageId, attempts, code }
        await this.options.wait(attempts)
      }
    }
    return { result: 'RetryPending', key, stageId, attempts, code }
  }
}

// Default transport for /api/v1/vessels/{vessel}/files. The caller supplies the current CSRF token.
export function vesselFileTransport(csrf: string): AttachmentTransport {
  const fail = async (response: Response) => {
    let code = 'request_failed'; try { const body = await response.json() as { code?: unknown }; if (typeof body.code === 'string') code = body.code } catch { /* keep generic code */ }
    throw Object.assign(new Error(code), { status: response.status, code })
  }
  const file = (value: unknown): StagedFile => {
    if (!value || typeof value !== 'object') throw Object.assign(new Error('invalid_file_contract'), { status: 502, code: 'invalid_file_contract' })
    const v = value as Record<string, unknown>
    if (typeof v.id !== 'string' || typeof v.state !== 'string' || !(v.checksum === null || typeof v.checksum === 'string')) throw Object.assign(new Error('invalid_file_contract'), { status: 502, code: 'invalid_file_contract' })
    return { id: v.id, state: v.state, checksum: v.checksum as string | null }
  }
  return {
    async stage(intent, key) {
      const response = await fetch(`/api/v1/vessels/${encodeURIComponent(intent.vessel)}/files`, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key },
        body: JSON.stringify({ name: intent.name, mime: intent.mime, length: intent.length, checksum: intent.checksum }) })
      if (!response.ok) await fail(response)
      return file(await response.json())
    },
    async upload(intent, stageId, bytes) {
      const response = await fetch(`/api/v1/vessels/${encodeURIComponent(intent.vessel)}/files/${encodeURIComponent(stageId)}/bytes`, { method: 'PUT', credentials: 'same-origin',
        headers: { 'Content-Type': intent.mime, 'X-CSRF-TOKEN': csrf }, body: bytes as BodyInit })
      if (!response.ok) await fail(response)
      return file(await response.json())
    },
  }
}
