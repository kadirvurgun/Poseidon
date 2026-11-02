import { expect, test, type Page } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import type { OfflineEncryptedRecordStore } from '../../../frontend/src/product/offline-record-store.ts'
import type { openOfflineRecord, sealOfflineRecord } from '../../../frontend/src/product/offline-cipher.ts'

// Isolated driver-level browser tests. The fulfilled HTML is a technical harness,
// NOT ordinary signed-host UI/SSO/offline journey evidence. Actual source modules,
// real IndexedDB/WebCrypto and two real DB connections run in the browser.
type Fixture = { __poseidonOfflineStore: typeof OfflineEncryptedRecordStore;
  __poseidonOfflineCipher: { sealOfflineRecord: typeof sealOfflineRecord; openOfflineRecord: typeof openOfflineRecord } }
const origin = 'https://poseidon-offline-store.test'
test.use({ trace: 'off', screenshot: 'off' })

async function install(page: Page) {
  await page.route(`${origin}/**`, route => route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><title>Isolated offline storage fixture</title>' }))
  await page.goto(origin)
  for (const [file, projection] of [
    ['offline-record-store.ts', 'Object.defineProperty(globalThis,"__poseidonOfflineStore",{value:OfflineEncryptedRecordStore});'],
    ['offline-cipher.ts', 'Object.defineProperty(globalThis,"__poseidonOfflineCipher",{value:{sealOfflineRecord,openOfflineRecord}});'],
  ]) {
    const source = await readFile(new URL(`../../../frontend/src/product/${file}`, import.meta.url), 'utf8')
    const javascript = stripTypeScriptTypes(source).replace(/^export\s+(?=(?:async\s+)?(?:class|function))/gm, '')
    await page.addScriptTag({ content: `(()=>{${javascript}\n${projection}})();` })
  }
}

test('encrypted capture survives connection close/reopen with exact bytes and no raw payload in storage', async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async () => {
    const fixture = globalThis as unknown as Fixture, Store = fixture.__poseidonOfflineStore, cipher = fixture.__poseidonOfflineCipher
    const id = crypto.randomUUID(), databaseName = `poseidon.offline.test.${crypto.randomUUID()}`
    const context = { tenantId: 'synthetic-tenant', actorId: 'synthetic-actor', vesselId: 'synthetic-vessel',
      deviceId: 'synthetic-device', sessionBinding: 'synthetic-noncredential-context', policyVersion: 'lab-only-v1', commandId: id }
    const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    const text = 'SYNTHETIC protected draft: İ ⚓ 000.1000 Unknown', limits = { plaintextBytes: 2048, contextBytes: 2048 }
    const sealed = await cipher.sealOfflineRecord(crypto, key, context, new TextEncoder().encode(text), limits)
    const first = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 4, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, databaseName)
    const saved = await first.put(id, sealed, null); first.close()
    const second = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 4, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, databaseName)
    const record = (await second.read(id))!, headers = await second.list(4)
    const rawHasText = JSON.stringify(record.sealed).includes('SYNTHETIC protected draft')
    const wrongKey = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    let wrongKeyCode = ''
    try { await cipher.openOfflineRecord(crypto, wrongKey, context, record.sealed, limits) }
    catch (error) { wrongKeyCode = (error as { code: string }).code }
    const retainedAfterWrongKey = (await second.read(id))?.revision
    const decoded = new TextDecoder().decode(await cipher.openOfflineRecord(crypto, key, context, record.sealed, limits))
    second.close()
    return { decoded, expected: text, rawHasText, revision: record.revision, count: headers.length,
      same: saved.commandId === record.commandId, wrongKeyCode, retainedAfterWrongKey }
  })
  expect(result).toEqual({ decoded: result.expected, expected: result.expected, rawHasText: false, revision: 1, count: 1,
    same: true, wrongKeyCode: 'authentication_failed', retainedAfterWrongKey: 1 })
})

test('two connections serialize create/update expected revisions without a lost update', async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async () => {
    const { __poseidonOfflineStore: Store, __poseidonOfflineCipher: cipher } = globalThis as unknown as Fixture
    const id = crypto.randomUUID(), databaseName = `poseidon.offline.test.${crypto.randomUUID()}`
    const policy = { maxRecords: 4, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }
    const first = await Store.open(indexedDB, 'a'.repeat(64), policy, databaseName)
    const second = await Store.open(indexedDB, 'a'.repeat(64), policy, databaseName)
    const context = { tenantId: 'synthetic-tenant', actorId: 'synthetic-actor', vesselId: 'synthetic-vessel',
      deviceId: 'synthetic-device', sessionBinding: 'synthetic-context', policyVersion: 'lab-only-v1', commandId: id }
    const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    const limits = { plaintextBytes: 2048, contextBytes: 2048 }
    const a = await cipher.sealOfflineRecord(crypto, key, context, new TextEncoder().encode('synthetic-A'), limits)
    const b = await cipher.sealOfflineRecord(crypto, key, context, new TextEncoder().encode('synthetic-B'), limits)
    const creates = await Promise.allSettled([first.put(id, a, null), second.put(id, b, null)])
    const updates = await Promise.allSettled([first.put(id, a, 1), second.put(id, b, 1)])
    const final = (await first.read(id))!, count = (await first.list(4)).length
    const value = new TextDecoder().decode(await cipher.openOfflineRecord(crypto, key, context, final.sealed, limits))
    const summarize = (results: PromiseSettledResult<unknown>[]) => results.map(item => item.status === 'fulfilled' ? 'saved' : item.reason.code).sort()
    first.close(); second.close()
    return { creates: summarize(creates), updates: summarize(updates), revision: final.revision, count, value }
  })
  expect(result.creates).toEqual(['revision_conflict', 'saved'])
  expect(result.updates).toEqual(['revision_conflict', 'saved'])
  expect(result.revision).toBe(2); expect(result.count).toBe(1)
  expect(['synthetic-A', 'synthetic-B']).toContain(result.value)
})

for (const quota of ['records', 'bytes'] as const) test(`concurrent ${quota} quota is atomic and another namespace remains isolated`, async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async quota => {
    const { __poseidonOfflineStore: Store } = globalThis as unknown as Fixture
    // Opaque fixed-format storage fixture, not a claim of authenticated ciphertext.
    const envelope = { version: 1 as const, contextHash: 'b'.repeat(64), iv: new Uint8Array(12), ciphertext: new ArrayBuffer(32) }
    const byteCost = 32 + 12 + 32
    const policy = { maxRecords: quota === 'records' ? 1 : 8, maxEncryptedBytes: quota === 'bytes' ? byteCost : 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }
    const databaseName = `poseidon.offline.test.${crypto.randomUUID()}`, firstId = crypto.randomUUID(), secondId = crypto.randomUUID()
    const first = await Store.open(indexedDB, 'a'.repeat(64), policy, databaseName)
    const second = await Store.open(indexedDB, 'a'.repeat(64), policy, databaseName)
    const results = await Promise.allSettled([first.put(firstId, envelope, null), second.put(secondId, envelope, null)])
    const headers = await first.list(policy.maxRecords)
    const other = await Store.open(indexedDB, 'c'.repeat(64), policy, databaseName)
    const foreign = await other.read(headers[0].commandId), before = (await other.list(policy.maxRecords)).length
    await other.put(firstId, envelope, null)
    const original = (await first.list(policy.maxRecords)).length
    first.close(); second.close(); other.close()
    return { outcomes: results.map(item => item.status === 'fulfilled' ? 'saved' : item.reason.code).sort(), count: headers.length, foreign, before, original }
  }, quota)
  expect(result).toEqual({ outcomes: ['quota_exceeded', 'saved'], count: 1, foreign: null, before: 0, original: 1 })
})

test('reduced quota retains captures; removal requires the exact stored revision', async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async () => {
    const { __poseidonOfflineStore: Store } = globalThis as unknown as Fixture
    const databaseName = `poseidon.offline.test.${crypto.randomUUID()}`, id = crypto.randomUUID()
    const envelope = { version: 1 as const, contextHash: 'b'.repeat(64), iv: new Uint8Array(12), ciphertext: new ArrayBuffer(32) }
    const first = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 2, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, databaseName)
    await first.put(id, envelope, null); first.close()
    const reduced = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 1, maxEncryptedBytes: 1, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, databaseName)
    const code = async (operation: Promise<unknown>) => { try { await operation; return 'unexpected_success' } catch (error) { return (error as { code: string }).code } }
    const quotaError = await code(reduced.put(crypto.randomUUID(), envelope, null))
    const staleRemoval = await code(reduced.remove(id, 2))
    const retained = await reduced.read(id)
    await reduced.remove(id, 1)
    const absent = await reduced.read(id); reduced.close()
    return { quotaError, staleRemoval, retainedRevision: retained?.revision, absent }
  })
  expect(result).toEqual({ quotaError: 'quota_exceeded', staleRemoval: 'revision_conflict', retainedRevision: 1, absent: null })
})

test('old captures remain paginatable after a lower record quota and closed stores reject new access', async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async () => {
    const { __poseidonOfflineStore: Store } = globalThis as unknown as Fixture
    const databaseName = `poseidon.offline.test.${crypto.randomUUID()}`
    const ids = [crypto.randomUUID(), crypto.randomUUID()].sort()
    const envelope = { version: 1 as const, contextHash: 'b'.repeat(64), iv: new Uint8Array(12), ciphertext: new ArrayBuffer(32) }
    const first = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 2, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, databaseName)
    for (const id of ids) await first.put(id, envelope, null)
    first.close()
    let closedCode = ''
    try { await first.read(ids[0]) } catch (error) { closedCode = (error as { code: string }).code }
    const reduced = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 1, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, databaseName)
    const firstPage = await reduced.list(1), secondPage = await reduced.list(1, firstPage[0].commandId)
    const end = await reduced.list(1, secondPage[0].commandId)
    reduced.close()
    return { ids, paged: [...firstPage, ...secondPage].map(row => row.commandId), end: end.length, closedCode }
  })
  expect(result.paged).toEqual(result.ids); expect(result.end).toBe(0); expect(result.closedCode).toBe('storage_closed')
})

test('a schema upgrade closes stale connections and never resets the captured records', async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async () => {
    const { __poseidonOfflineStore: Store } = globalThis as unknown as Fixture
    const databaseName = `poseidon.offline.test.${crypto.randomUUID()}`, id = crypto.randomUUID()
    const first = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 2, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, databaseName)
    await first.put(id, { version: 1, contextHash: 'b'.repeat(64), iv: new Uint8Array(12), ciphertext: new ArrayBuffer(32) }, null)
    const upgraded = await new Promise<IDBDatabase>((resolve, reject) => {
      const request = indexedDB.open(databaseName, 2)
      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(new Error('fixture upgrade failed'))
    })
    let staleCode = ''
    try { await first.read(id) } catch (error) { staleCode = (error as { code: string }).code }
    const count = await new Promise<number>((resolve, reject) => {
      const request = upgraded.transaction('encryptedRecords').objectStore('encryptedRecords').count()
      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(new Error('fixture count failed'))
    })
    upgraded.close()
    return { staleCode, count }
  })
  expect(result).toEqual({ staleCode: 'storage_closed', count: 1 })
})

test('noncanonical UUID casing is rejected instead of rewriting an authenticated binding', async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async () => {
    const { __poseidonOfflineStore: Store } = globalThis as unknown as Fixture
    const store = await Store.open(indexedDB, 'a'.repeat(64), { maxRecords: 2, maxEncryptedBytes: 8192, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 65536 }, `poseidon.offline.test.${crypto.randomUUID()}`)
    const id = 'aaaaaaaa-0000-0000-0000-000000000001'
    const envelope = { version: 1 as const, contextHash: 'b'.repeat(64), iv: new Uint8Array(12), ciphertext: new ArrayBuffer(32) }
    await store.put(id, envelope, null)
    let aliasCode = ''
    try { await store.put(id.toUpperCase(), envelope, null) } catch (error) { aliasCode = (error as { code: string }).code }
    const records = await store.list(2); store.close()
    return { aliasCode, count: records.length, commandId: records[0].commandId }
  })
  expect(result).toEqual({ aliasCode: 'invalid_record', count: 1, commandId: 'aaaaaaaa-0000-0000-0000-000000000001' })
})

for (const quota of ['records', 'bytes'] as const) test(`device ${quota} quota cannot be bypassed by concurrent different namespaces`, async ({ page }) => {
  await install(page)
  const result = await page.evaluate(async quota => {
    const { __poseidonOfflineStore: Store } = globalThis as unknown as Fixture
    const databaseName = `poseidon.offline.test.${crypto.randomUUID()}`
    const envelope = { version: 1 as const, contextHash: 'd'.repeat(64), iv: new Uint8Array(12), ciphertext: new ArrayBuffer(32) }
    const policy = { maxRecords: 8, maxEncryptedBytes: 8192,
      maxDeviceRecords: quota === 'records' ? 1 : 8, maxDeviceEncryptedBytes: quota === 'bytes' ? 76 : 8192 }
    const first = await Store.open(indexedDB, 'a'.repeat(64), policy, databaseName)
    const second = await Store.open(indexedDB, 'b'.repeat(64), policy, databaseName)
    const results = await Promise.allSettled([first.put(crypto.randomUUID(), envelope, null), second.put(crypto.randomUUID(), envelope, null)])
    const firstRows = await first.list(8), secondRows = await second.list(8)
    const namespacesWithRecords = [firstRows.length, secondRows.length].filter(count => count > 0).length
    const originalNamespace = firstRows.length ? first : second
    const stored = (await originalNamespace.list(8))[0]
    await originalNamespace.remove(stored.commandId, stored.revision)
    // Explicit discard releases device capacity without exposing foreign headers.
    await second.put(crypto.randomUUID(), envelope, null)
    const afterDiscard = (await second.list(8)).length
    first.close(); second.close()
    return { outcomes: results.map(item => item.status === 'fulfilled' ? 'saved' : item.reason.code).sort(),
      total: firstRows.length + secondRows.length, namespacesWithRecords, afterDiscard }
  }, quota)
  expect(result).toEqual({ outcomes: ['quota_exceeded', 'saved'], total: 1, namespacesWithRecords: 1, afterDiscard: 1 })
})
