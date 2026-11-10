import type { OfflineSealedRecord } from './offline-cipher'

export type OfflineStoreLimits = Readonly<{
  maxRecords: number; maxEncryptedBytes: number
  maxDeviceRecords: number; maxDeviceEncryptedBytes: number
}>
export type OfflineStoredHeader = Readonly<{ commandId: string; revision: number; encryptedBytes: number }>
export type OfflineStoredRecord = OfflineStoredHeader & Readonly<{ sealed: OfflineSealedRecord }>
type Stored = OfflineStoredRecord & { key: string; namespace: string }
type Failure = 'invalid_configuration' | 'invalid_record' | 'storage_unavailable' | 'storage_blocked' |
  'storage_closed' | 'revision_conflict' | 'quota_exceeded'

export class OfflineStoreError extends Error {
  readonly code: Failure
  constructor(code: Failure) { super(code); this.name = 'OfflineStoreError'; this.code = code }
}
const storeName = 'encryptedRecords'
const digest = (value: string) => typeof value === 'string' && /^[a-f0-9]{64}$/.test(value)
function identifier(value: string) {
  if (typeof value !== 'string' || value.length !== 36 ||
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value) ||
    value === '00000000-0000-0000-0000-000000000000') throw new OfflineStoreError('invalid_record')
  // Canonicalize at capture creation BEFORE encryption; never rewrite an ID
  // after it has already been bound into authenticated additional data.
  return value
}
function length(value: unknown): number | null {
  try { return Object.getOwnPropertyDescriptor(ArrayBuffer.prototype, 'byteLength')!.get!.call(value) as number }
  catch { return null }
}
function encryptedSize(sealed: OfflineSealedRecord) {
  const bytes = sealed ? length(sealed.ciphertext) : null
  if (!sealed || sealed.version !== 1 || !digest(sealed.contextHash) || !ArrayBuffer.isView(sealed.iv) ||
    sealed.iv.byteLength !== 12 || length(sealed.iv.buffer) === null || bytes === null || bytes < 16)
    throw new OfflineStoreError('invalid_record')
  // Ciphertext (including tag), nonce and binary SHA256 fingerprint. Fixed-format
  // identifiers/metadata are additionally bounded by maxRecords, not a disk estimate.
  return bytes + 12 + 32
}

// Structural inventory validation contains no authorization or plaintext access.
// Global budgeting inspects opaque sizes only and never returns foreign headers.
function storedRecord(value: Stored) {
  if (!value || !digest(value.namespace) || value.key !== `${value.namespace}/${identifier(value.commandId)}` ||
    !Number.isSafeInteger(value.revision) || value.revision <= 0 || value.encryptedBytes !== encryptedSize(value.sealed))
    throw new OfflineStoreError('invalid_record')
  return value
}

// Actual IndexedDB storage, not an authorization or crypto-key store. namespace is
// an opaque SHA256 of the current authorized scope, NEVER a token or raw actor ID.
export class OfflineEncryptedRecordStore {
  private closed = false
  private db: IDBDatabase
  private namespace: string
  private limits: OfflineStoreLimits
  private constructor(db: IDBDatabase, namespace: string, limits: OfflineStoreLimits) {
    this.db = db; this.namespace = namespace; this.limits = limits
    db.onversionchange = () => this.close()
  }

  static open(factory: IDBFactory, namespace: string, limits: OfflineStoreLimits,
    databaseName = 'poseidon.offline.capture.v1'): Promise<OfflineEncryptedRecordStore> {
    if (!factory || !digest(namespace) || !limits ||
      !Number.isSafeInteger(limits.maxRecords) || limits.maxRecords <= 0 ||
      !Number.isSafeInteger(limits.maxEncryptedBytes) || limits.maxEncryptedBytes <= 0 ||
      !Number.isSafeInteger(limits.maxDeviceRecords) || limits.maxDeviceRecords <= 0 ||
      !Number.isSafeInteger(limits.maxDeviceEncryptedBytes) || limits.maxDeviceEncryptedBytes <= 0 ||
      !/^poseidon\.offline\.[a-z0-9.-]{1,100}$/.test(databaseName))
      return Promise.reject(new OfflineStoreError('invalid_configuration'))
    const snapshot = { ...limits }
    return new Promise((resolve, reject) => {
      let settled = false
      const fail = (code: Failure) => { if (!settled) { settled = true; reject(new OfflineStoreError(code)) } }
      let request: IDBOpenDBRequest
      try { request = factory.open(databaseName, 1) } catch { fail('storage_unavailable'); return }
      request.onblocked = () => fail('storage_blocked')
      request.onerror = () => fail('storage_unavailable')
      request.onupgradeneeded = () => {
        if (settled) { request.transaction?.abort(); return }
        if (!request.result.objectStoreNames.contains(storeName)) request.result.createObjectStore(storeName, { keyPath: 'key' })
      }
      request.onsuccess = () => {
        if (settled) { request.result.close(); return }
        if (!request.result.objectStoreNames.contains(storeName)) { request.result.close(); fail('storage_unavailable'); return }
        settled = true; resolve(new OfflineEncryptedRecordStore(request.result, namespace, snapshot))
      }
    })
  }

  private key(commandId: string) { return `${this.namespace}/${identifier(commandId)}` }
  private transaction(mode: IDBTransactionMode) {
    if (this.closed) throw new OfflineStoreError('storage_closed')
    try { return this.db.transaction(storeName, mode) }
    catch { throw new OfflineStoreError('storage_unavailable') }
  }
  private validate(value: Stored) {
    const record = storedRecord(value)
    if (record.namespace !== this.namespace) throw new OfflineStoreError('invalid_record')
    return record
  }
  private range(afterCommandId?: string) {
    const prefix = `${this.namespace}/`
    return IDBKeyRange.bound(afterCommandId ? this.key(afterCommandId) : prefix, prefix + '\uffff', Boolean(afterCommandId), false)
  }

  async read(commandId: string): Promise<OfflineStoredRecord | null> {
    const key = this.key(commandId), tx = this.transaction('readonly')
    return new Promise((resolve, reject) => {
      let result: OfflineStoredRecord | null = null, failure: Failure = 'storage_unavailable'
      const request = tx.objectStore(storeName).get(key)
      request.onsuccess = () => {
        if (request.result === undefined) return
        try {
          const record = this.validate(request.result as Stored)
          result = { commandId: record.commandId, revision: record.revision, encryptedBytes: record.encryptedBytes, sealed: record.sealed }
        } catch { failure = 'invalid_record'; tx.abort() }
      }
      tx.oncomplete = () => resolve(result)
      tx.onabort = tx.onerror = () => reject(new OfflineStoreError(failure))
    })
  }

  async list(limit: number, afterCommandId?: string): Promise<readonly OfflineStoredHeader[]> {
    if (!Number.isSafeInteger(limit) || limit <= 0 || limit > this.limits.maxRecords) throw new OfflineStoreError('invalid_configuration')
    const range = this.range(afterCommandId), tx = this.transaction('readonly')
    return new Promise((resolve, reject) => {
      const headers: OfflineStoredHeader[] = []; let failure: Failure = 'storage_unavailable'
      const request = tx.objectStore(storeName).openCursor(range)
      request.onsuccess = () => {
        const cursor = request.result
        if (!cursor || headers.length >= limit) return
        try {
          const record = this.validate(cursor.value as Stored)
          headers.push({ commandId: record.commandId, revision: record.revision, encryptedBytes: record.encryptedBytes })
          if (headers.length < limit) cursor.continue()
        } catch { failure = 'invalid_record'; tx.abort() }
      }
      tx.oncomplete = () => resolve(headers)
      tx.onabort = tx.onerror = () => reject(new OfflineStoreError(failure))
    })
  }

  // A lower write quota does not truncate the inventory of retained captures.
  // Bound the full read by the caller's protocol ceiling, including an extra
  // sentinel read at that ceiling; never return a falsely complete first page.
  async listRetained(maximumRecords: number): Promise<readonly OfflineStoredHeader[]> {
    if (!Number.isSafeInteger(maximumRecords) || maximumRecords < 1 || maximumRecords > 512)
      throw new OfflineStoreError('invalid_configuration')
    const headers: OfflineStoredHeader[] = []
    let after: string | undefined
    for (;;) {
      const requested = Math.min(this.limits.maxRecords, maximumRecords - headers.length + 1)
      const page = await this.list(requested, after)
      if (headers.length + page.length > maximumRecords) throw new OfflineStoreError('invalid_record')
      headers.push(...page)
      if (page.length < requested) return headers
      after = page[page.length - 1].commandId
    }
  }

  async put(commandId: string, sealed: OfflineSealedRecord, expectedRevision: number | null): Promise<OfflineStoredHeader> {
    const canonicalId = identifier(commandId), key = this.key(canonicalId)
    if (expectedRevision !== null && (!Number.isSafeInteger(expectedRevision) || expectedRevision < 1 || expectedRevision >= Number.MAX_SAFE_INTEGER))
      throw new OfflineStoreError('invalid_record')
    const encryptedBytes = encryptedSize(sealed)
    if (encryptedBytes > this.limits.maxEncryptedBytes || encryptedBytes > this.limits.maxDeviceEncryptedBytes)
      throw new OfflineStoreError('quota_exceeded')
    // Copy before async storage so callers cannot mutate the captured buffers/size.
    const copy = structuredClone({ version: sealed.version, contextHash: sealed.contextHash, iv: sealed.iv, ciphertext: sealed.ciphertext })
    const tx = this.transaction('readwrite'), store = tx.objectStore(storeName)
    return new Promise((resolve, reject) => {
      let count = 0, bytes = 0, deviceCount = 0, deviceBytes = 0
      let existing: Stored | null = null, failure: Failure = 'storage_unavailable'
      const header = { commandId: canonicalId, revision: expectedRevision === null ? 1 : expectedRevision + 1, encryptedBytes }
      const request = store.openCursor()
      request.onsuccess = () => {
        const cursor = request.result
        if (cursor) {
          try {
            const record = storedRecord(cursor.value as Stored)
            if (record.key === key) existing = record
            else {
              deviceCount++; deviceBytes += record.encryptedBytes
              if (record.namespace === this.namespace) { count++; bytes += record.encryptedBytes }
            }
            cursor.continue()
          } catch { failure = 'invalid_record'; tx.abort() }
          return
        }
        if ((existing?.revision ?? null) !== expectedRevision) { failure = 'revision_conflict'; tx.abort(); return }
        if (count >= this.limits.maxRecords || encryptedBytes > this.limits.maxEncryptedBytes - bytes ||
            deviceCount >= this.limits.maxDeviceRecords || encryptedBytes > this.limits.maxDeviceEncryptedBytes - deviceBytes) {
          failure = 'quota_exceeded'; tx.abort(); return
        }
        // Device and namespace inventory, expected revision and write are atomic.
        store.put({ key, namespace: this.namespace, ...header, sealed: copy } satisfies Stored)
      }
      tx.oncomplete = () => resolve(header)
      tx.onerror = () => { if (tx.error?.name === 'QuotaExceededError') failure = 'quota_exceeded' }
      tx.onabort = () => reject(new OfflineStoreError(tx.error?.name === 'QuotaExceededError' ? 'quota_exceeded' : failure))
    })
  }

  // Only explicit confirmed-receipt removal or authorized user discard should call
  // this. Expiry, logout, quota pressure and key loss NEVER automatically delete.
  async remove(commandId: string, expectedRevision: number): Promise<void> {
    if (!Number.isSafeInteger(expectedRevision) || expectedRevision < 1) throw new OfflineStoreError('invalid_record')
    const key = this.key(commandId), tx = this.transaction('readwrite'), store = tx.objectStore(storeName)
    return new Promise((resolve, reject) => {
      let failure: Failure = 'storage_unavailable'
      const request = store.get(key)
      request.onsuccess = () => {
        try {
          if (!request.result || this.validate(request.result as Stored).revision !== expectedRevision) {
            failure = 'revision_conflict'; tx.abort(); return
          }
          store.delete(key)
        } catch { failure = 'invalid_record'; tx.abort() }
      }
      tx.oncomplete = () => resolve()
      tx.onabort = tx.onerror = () => reject(new OfflineStoreError(failure))
    })
  }

  close() { this.closed = true; this.db.close() }
}
