import { expect, test, type Page, type Route } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'

// S10 WP-C DEV-066 (18.01 sheet:Backlog/row:67) full pilot offline journey, isolated browser harness.
// The exact source modules (offline-cipher, offline-record-store, pilot-offline-journey, attachment-retry) run in
// real Chromium with real IndexedDB/WebCrypto and real network loss (context.setOffline). The API is a scripted
// idempotent server: this is NOT signed-host/SSO journey evidence and grants no pilot acceptance.
// Three flows (certificate renewal draft, safety event capture, work order evidence): capture offline, reload,
// reconnect (context.setOffline plus an aborted scripted network), duplicate send, material conflict -> explicit human choice, attachment retry; data-loss count is 0.
const origin = 'https://poseidon-pilot-offline.test'
test.use({ trace: 'off', screenshot: 'off' })

type Server = { offline: boolean; effects: Record<string, number>; outcomes: Map<string, { body: string; outcome: unknown }>; stages: Map<string, { id: string; fingerprint: string; state: string; checksum: string | null }>; uploads: number }
async function install(page: Page): Promise<Server> {
  const server: Server = { offline: false, effects: { certificateRenewal: 0, safetyEvent: 0, workOrderEvidence: 0 }, outcomes: new Map(), stages: new Map(), uploads: 0 }
  await page.route(`${origin}/**`, async (route: Route) => {
    const request = route.request(), url = new URL(request.url())
    // Chromium offline emulation does not stop routed requests, so the scripted network mirrors context.setOffline.
    if (server.offline && url.pathname.startsWith('/api/')) return route.abort('internetdisconnected')
    const replay = /^\/api\/v1\/pilot-journey\/(certificateRenewal|safetyEvent|workOrderEvidence)\/replay$/.exec(url.pathname)
    if (replay && request.method() === 'POST') {
      const flow = replay[1], body = request.postData() ?? '', command = JSON.parse(body) as { commandId: string; baseVersion?: number; parentCommandId?: string }
      if (request.headers()['idempotency-key'] !== command.commandId) return route.fulfill({ status: 400, json: { code: 'invalid_idempotency_key' } })
      const recorded = server.outcomes.get(command.commandId)
      if (recorded) return recorded.body === body ? route.fulfill({ json: recorded.outcome }) : route.fulfill({ status: 409, json: { code: 'offline_command_reused' } })
      // Work order evidence head moved online to version 2 while the device was offline (base 1): material conflict.
      const conflict = flow === 'workOrderEvidence' && !command.parentCommandId && command.baseVersion !== 2
      if (!conflict) server.effects[flow]++
      const outcome = { commandId: command.commandId, result: conflict ? 'MaterialConflict' : 'Committed', parentCommandId: command.parentCommandId ?? null }
      server.outcomes.set(command.commandId, { body, outcome })
      return route.fulfill({ json: outcome })
    }
    const files = /^\/api\/v1\/vessels\/([0-9a-f-]{36})\/files(?:\/([0-9a-f-]{36})\/bytes)?$/.exec(url.pathname)
    if (files && request.method() === 'POST') {
      const key = request.headers()['idempotency-key'] ?? '', fingerprint = request.postData() ?? '', existing = server.stages.get(key)
      if (existing) return existing.fingerprint === fingerprint ? route.fulfill({ json: { id: existing.id, state: existing.state, checksum: existing.checksum } }) : route.fulfill({ status: 409, json: { code: 'idempotency_key_reused' } })
      const created = { id: crypto.randomUUID(), fingerprint, state: 'Pending', checksum: null }; server.stages.set(key, created)
      return route.fulfill({ json: { id: created.id, state: created.state, checksum: null } })
    }
    if (files && files[2] && request.method() === 'PUT') {
      const stage = [...server.stages.values()].find(s => s.id === files[2])
      if (!stage) return route.fulfill({ status: 404, json: { code: 'resource_not_found' } })
      if (stage.state !== 'CleanTransport') { server.uploads++; stage.state = 'CleanTransport'; stage.checksum = (JSON.parse(stage.fingerprint) as { checksum: string }).checksum }
      return route.fulfill({ json: { id: stage.id, state: stage.state, checksum: stage.checksum } })
    }
    return route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><title>Isolated pilot offline journey fixture</title>' })
  })
  await page.goto(origin)
  const projections: [string, string][] = [
    ['offline-record-store.ts', 'Object.defineProperty(globalThis,"__pilotStore",{value:OfflineEncryptedRecordStore});'],
    ['offline-cipher.ts', 'Object.defineProperty(globalThis,"__pilotCipher",{value:{sealOfflineRecord,openOfflineRecord}});'],
    ['pilot-offline-journey.ts', 'Object.defineProperty(globalThis,"__pilotJourney",{value:{syncPilotJourney}});'],
    ['attachment-retry.ts', 'Object.defineProperty(globalThis,"__pilotAttachment",{value:{AttachmentRetryQueue,vesselFileTransport,sha256Hex}});'],
  ]
  for (const [file, projection] of projections) {
    const source = await readFile(new URL(`../../../frontend/src/product/${file}`, import.meta.url), 'utf8')
    const javascript = stripTypeScriptTypes(source).replace(/^export\s+(?=(?:async\s+)?(?:class|function))/gm, '')
    await page.addScriptTag({ content: `(()=>{${javascript}\n${projection}})();` })
  }
  // In-page durable adapters: one encrypted IndexedDB namespace per flow, sealed BEFORE any network attempt.
  await page.evaluate(async () => {
    type G = Record<string, any>
    const g = globalThis as unknown as G, Store = g.__pilotStore, cipher = g.__pilotCipher
    const hex = async (text: string) => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text))), b => b.toString(16).padStart(2, '0')).join('')
    const limits = { maxRecords: 16, maxEncryptedBytes: 65536, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 1048576 }, sealLimits = { plaintextBytes: 8192, contextBytes: 2048 }
    g.__flows = {}
    for (const flow of ['certificateRenewal', 'safetyEvent', 'workOrderEvidence']) {
      const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
      const namespace = await hex(`pilot-journey/${flow}`), database = `poseidon.offline.pilot-journey.${flow.toLowerCase()}`
      const context = (commandId: string) => ({ tenantId: 'synthetic-tenant', actorId: 'synthetic-actor', vesselId: 'synthetic-vessel', deviceId: 'synthetic-device',
        sessionBinding: `pilot-journey/${flow}`, policyVersion: 'lab-only-v1', commandId })
      const state = { store: await Store.open(indexedDB, namespace, limits, database) }
      const write = async (id: string, value: unknown, revision: number | null) => state.store.put(id, await cipher.sealOfflineRecord(crypto, key, context(id), new TextEncoder().encode(JSON.stringify(value)), sealLimits), revision)
      const adapter = {
        flow,
        reopen: async () => { state.store.close(); state.store = await Store.open(indexedDB, namespace, limits, database) },
        capture: async (extra: object = {}) => { const commandId = crypto.randomUUID(); await write(commandId, { body: JSON.stringify({ commandId, baseVersion: 1, ...extra }) }, null); return commandId },
        captures: async () => {
          const rows = []
          for (const header of await state.store.listRetained(64)) {
            const stored = await state.store.read(header.commandId)
            const value = JSON.parse(new TextDecoder().decode(await cipher.openOfflineRecord(crypto, key, context(header.commandId), stored.sealed, sealLimits)))
            rows.push({ commandId: header.commandId, revision: header.revision, body: value.body, ...(value.outcome ? { outcome: value.outcome } : {}) })
          }
          return rows
        },
        replay: async (row: { commandId: string; revision: number; body: string }) => {
          const response = await fetch(`/api/v1/pilot-journey/${flow}/replay`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': row.commandId }, body: row.body })
          if (!response.ok) { const body = await response.json(); throw Object.assign(new Error(body.code), { status: response.status, code: body.code }) }
          const outcome = await response.json(); await write(row.commandId, { body: row.body, outcome }, row.revision); return outcome
        },
        raw: async () => { const out: string[] = []; for (const h of await state.store.listRetained(64)) out.push(JSON.stringify((await state.store.read(h.commandId)).sealed)); return out },
      }
      g.__flows[flow] = adapter
    }
    g.__sync = () => g.__pilotJourney.syncPilotJourney(Object.values(g.__flows), new AbortController().signal, () => navigator.onLine)
  })
  return server
}

test('three pilot flows survive network loss, reload, duplicate send and conflict with zero data loss', async ({ page, context }) => {
  const server = await install(page)
  const network = async (offline: boolean) => { server.offline = offline; await context.setOffline(offline) }
  await network(true)
  const offline = await page.evaluate(async () => {
    const g = globalThis as unknown as Record<string, any>
    const flows = g.__flows
    for (const flow of Object.values(flows) as { capture: (x?: object) => Promise<string> }[]) { await flow.capture({ text: 'SYNTHETIC offline draft A' }); await flow.capture({ text: 'SYNTHETIC offline draft B' }) }
    let networkError = ''
    try { await fetch('/api/v1/pilot-journey/safetyEvent/replay', { method: 'POST', body: '{}' }) } catch (error) { networkError = (error as Error).name }
    const ledger = await g.__sync()
    // Simulated reload: close and reopen every encrypted store; nothing is dropped.
    for (const flow of Object.values(flows) as { reopen: () => Promise<void> }[]) await flow.reopen()
    const retained = await Promise.all((Object.values(flows) as { captures: () => Promise<unknown[]> }[]).map(async f => (await f.captures()).length))
    const raw = (await Promise.all((Object.values(flows) as { raw: () => Promise<string[]> }[]).map(f => f.raw()))).flat().join('')
    return { online: navigator.onLine, networkError, ledger, retained, plaintextInStorage: raw.includes('SYNTHETIC offline draft') }
  })
  expect(offline.online).toBe(false); expect(offline.networkError).toBe('TypeError'); expect(offline.plaintextInStorage).toBe(false)
  expect(offline.retained).toEqual([2, 2, 2])
  expect(offline.ledger.map((l: { pending: number; attempted: number; lost: number }) => [l.pending, l.attempted, l.lost])).toEqual([[2, 0, 0], [2, 0, 0], [2, 0, 0]])
  expect(server.effects).toEqual({ certificateRenewal: 0, safetyEvent: 0, workOrderEvidence: 0 })

  await network(false)
  const reconnect = await page.evaluate(async () => {
    const g = globalThis as unknown as Record<string, any>
    const first = await g.__sync()
    // Duplicate send of an already recorded capture returns the same receipt and causes no second effect.
    const renewal = g.__flows.certificateRenewal, [row] = await renewal.captures()
    const duplicate = await renewal.replay(row)
    const second = await g.__sync()
    return { first, second, duplicate, row }
  })
  expect(reconnect.first.map((l: { flow: string; committed: number; conflicts: number; pending: number; lost: number }) => [l.flow, l.committed, l.conflicts, l.pending, l.lost]))
    .toEqual([['certificateRenewal', 2, 0, 0, 0], ['safetyEvent', 2, 0, 0, 0], ['workOrderEvidence', 0, 2, 0, 0]])
  expect(reconnect.duplicate).toEqual(reconnect.row.outcome)
  expect(reconnect.second.every((l: { attempted: number }) => l.attempted === 0)).toBe(true)
  expect(server.effects).toEqual({ certificateRenewal: 2, safetyEvent: 2, workOrderEvidence: 0 })

  // Material conflict resolved only by an explicit human keep-server choice recorded as a new child command.
  const resolved = await page.evaluate(async () => {
    const g = globalThis as unknown as Record<string, any>
    const orders = g.__flows.workOrderEvidence, conflicts = (await orders.captures()).filter((r: { outcome?: { result: string } }) => r.outcome?.result === 'MaterialConflict')
    for (const c of conflicts) await orders.capture({ parentCommandId: c.commandId, baseVersion: 2, choice: 'keepServer', reasonCode: 'CURRENT_SERVER_RETAINED' })
    return (await g.__sync())[2]
  })
  expect(resolved).toMatchObject({ flow: 'workOrderEvidence', conflicts: 0, committed: 2, pending: 0, retained: 4, lost: 0 })
})

test('attachment retry reuses one secure-file stage for the same hash across network loss and duplicate sends', async ({ page, context }) => {
  const server = await install(page)
  const network = async (offline: boolean) => { server.offline = offline; await context.setOffline(offline) }
  await network(true)
  const offline = await page.evaluate(async () => {
    const g = globalThis as unknown as Record<string, any>
    const { AttachmentRetryQueue, vesselFileTransport, sha256Hex } = g.__pilotAttachment
    const bytes = new TextEncoder().encode('SYNTHETIC evidence bytes')
    g.__intent = { vessel: '11111111-1111-4111-8111-111111111111', name: 'photo.png', mime: 'image/png', length: bytes.byteLength, checksum: await sha256Hex(bytes) }
    g.__bytes = bytes
    g.__queue = (online: () => boolean) => new AttachmentRetryQueue(vesselFileTransport('synthetic-csrf'), 'synthetic-actor', { maxAttempts: 3, online, wait: async () => undefined })
    const gated = await g.__queue(() => navigator.onLine).send(g.__intent, bytes)
    const forced = await g.__queue(() => true).send(g.__intent, bytes)
    return { gated, forced }
  })
  expect(offline.gated).toMatchObject({ result: 'RetryPending', attempts: 0 })
  expect(offline.forced).toMatchObject({ result: 'RetryPending', attempts: 3 })
  expect(server.stages.size).toBe(0)
  await network(false)
  const online = await page.evaluate(async () => {
    const g = globalThis as unknown as Record<string, any>
    const first = await g.__queue(() => navigator.onLine).send(g.__intent, g.__bytes)
    const duplicate = await g.__queue(() => navigator.onLine).send(g.__intent, g.__bytes)
    return { first, duplicate }
  })
  expect(online.first).toMatchObject({ result: 'Stored', attempts: 1 }); expect(online.duplicate).toMatchObject({ result: 'Stored', stageId: online.first.stageId, key: online.first.key })
  expect(server.stages.size).toBe(1); expect(server.uploads).toBe(1)
})
