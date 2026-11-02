import { expect, type Page } from '@playwright/test'

// OA-BROWSER01..05: actual signed host, PostgreSQL, ordinary commands, product UI,
// WebCrypto, IndexedDB and the static-only worker. No fabricated business responses.
// Later gate MUST provision explicit fictional offline policy for the writer's Ship
// scope with SYNTHETIC-A/B. Missing policy fails; these cases never skip it.
async function setup(page: Page, medical = false) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25&kind=Ship')).json()
  expect(vessels.items.length > 0).toBe(true); const vessel = vessels.items[0].id as string
  const user = await (await page.request.get('/api/v1/auth/session')).json()
  const assignment = crypto.randomUUID()
  const response = await page.request.post(`/api/v1/vessels/${vessel}/crew`, { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': crypto.randomUUID() }, data: {
    id: assignment, personId: null, baseVersion: null, fullName: null, employeeNo: null, positionId: null, from: null, to: null,
    contractEndDate: null, watchGroup: null, sourceObservedAt: null,
  } })
  expect(response.status() === 200).toBe(true)
  if (medical) {
    // An actual ordinary protected declaration on this journey's new person,
    // never seed data or an offline health-field permission.
    const path = `/api/v1/vessels/${vessel}/crew/${assignment}/personal-profile`
    const current = await page.request.get(`${path}?purpose=crew-profile`)
    expect(current.status() === 200).toBe(true)
    const profile = await current.json()
    expect(profile.readable.includes(15) && profile.writable.includes(15)).toBe(true)
    const saved = await page.request.put(path, { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': crypto.randomUUID() }, data: {
      baseVersion: profile.version, policyVersion: profile.policyVersion, purpose: profile.purpose,
      fields: [15], data: { medicalExpiry: '2031-02-03' },
    } })
    expect(saved.status() === 200).toBe(true)
    const read = await page.request.get(`${path}?purpose=crew-profile`)
    expect(read.status() === 200 && (await read.json()).data.medicalExpiry === '2031-02-03').toBe(true)
  }
  const url = `/app/crew-roster/${vessel}/crew-profile/${assignment}`
  await page.goto(url); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const region = page.getByRole('region', { name: 'Durable offline drafts', exact: true })
  const enrolled = page.waitForResponse(r => r.url().endsWith(`/crew/${assignment}/offline`) && r.request().method() === 'POST')
  await region.getByRole('button', { name: 'Authorize / renew / recover', exact: true }).click()
  const enrollmentResponse = await enrolled; expect(enrollmentResponse.status() === 200).toBe(true)
  const context = await enrollmentResponse.json()
  expect(!JSON.stringify(context).includes('2031-02-03') && !/medical/i.test(JSON.stringify(context))).toBe(true)
  await expect(region.getByText('Encrypted capture available for this authorization.', { exact: true })).toBeVisible()
  return { vessel, assignment, url, region, context }
}
async function recover(page: Page, enrollment: string) {
  const region = page.getByRole('region', { name: 'Durable offline drafts', exact: true })
  await region.getByRole('button', { name: 'Find retained contexts', exact: true }).click()
  await expect(region.getByRole('option', { name: 'Retained context 1', exact: true })).toBeAttached()
  await region.getByRole('combobox', { name: 'Offline context', exact: true }).selectOption(enrollment)
  await region.getByRole('button', { name: 'Authorize / renew / recover', exact: true }).click()
  await expect(region.getByText('Encrypted capture available for this authorization.', { exact: true })).toBeVisible()
  return region
}
async function offlineCapture(page: Page) {
  const region = page.getByRole('region', { name: 'Durable offline drafts', exact: true })
  await page.context().setOffline(true)
  await region.getByRole('combobox', { name: 'Offline draft watch group', exact: true }).selectOption('SYNTHETIC-A')
  await region.getByRole('button', { name: 'Capture encrypted draft', exact: true }).click()
  await expect(region.getByRole('list', { name: 'Stored capture status' })).toContainText('Unsent / outcome unknown')
  await expect(region.getByRole('button', { name: 'Replay / check original outcome', exact: true })).toBeDisabled()
}
async function restartAndDuplicate(page: Page) {
  const { context, url, vessel, assignment } = await setup(page)
  await offlineCapture(page)
  // Genuine disconnected app bootstrap from static shell; all data APIs remain uncached.
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText('Stored drafts are locked. Connect and explicitly authorize recovery.', { exact: true })).toBeVisible()
  expect(new URL(page.url()).pathname === url).toBe(true)
  await page.context().setOffline(false); const region = await recover(page, context.id)
  let body = '', digest = '', same = false, deliveries = 0
  await page.route(`**/crew/${assignment}/offline/${context.id}/replay`, async route => {
    deliveries++
    if (deliveries === 1) {
      body = route.request().postData()!; digest = route.request().headers()['x-offline-hash']
      const response = await route.fetch(); expect(response.status() === 200).toBe(true)
      await route.abort('connectionreset') // response loss after the REAL transaction committed
    } else {
      same = route.request().postData() === body && route.request().headers()['x-offline-hash'] === digest
      await route.continue()
    }
  })
  await region.getByRole('button', { name: 'Replay / check original outcome', exact: true }).click()
  await expect(region.getByText(/Request failed or outcome unknown/)).toBeVisible()
  await region.getByRole('button', { name: 'Replay / check original outcome', exact: true }).click()
  await expect(region.getByRole('list', { name: 'Stored capture status' })).toContainText('Committed')
  expect(same && deliveries === 2).toBe(true)
  const history = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}/offline/${context.id}/history`)).json()
  expect(history.items.length === 1 && history.items[0].result === 'Committed').toBe(true)
  const profile = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}`)).json()
  expect(profile.assignment.revision === 2 && profile.assignment.watchGroup === 'SYNTHETIC-A' && profile.assignment.assignmentStatus === 'Draft').toBe(true)
  expect(profile.assignment.eligibilityState === 'Unknown').toBe(true)
  const publicCacheOnly = await page.evaluate(async () => {
    // Inspect the actual content-addressed build caches. Opening the historical
    // v1 name would create an empty cache and inspect none of the current worker.
    const names = (await caches.keys()).filter(name => /^poseidon\.offline\.static\.[a-f0-9]{64}$/.test(name))
    if (!names.length) return false
    for (const name of names) {
      const paths = (await (await caches.open(name)).keys()).map(r => new URL(r.url).pathname)
      if (!paths.includes('/index.html') || !paths.some(p => p.startsWith('/assets/')) ||
        paths.some(p => p !== '/index.html' && p !== '/__poseidon_offline_public_builds__' && !p.startsWith('/assets/'))) return false
    }
    return true
  })
  expect(publicCacheOnly).toBe(true)
}
async function materialConflict(page: Page, medical = false) {
  const { vessel, assignment, context, region, url } = await setup(page, medical)
  await offlineCapture(page); await page.context().setOffline(false); await recover(page, context.id)
  if (medical) {
    await assertDevicePrivacy(page, context, 1)
    // Restart requires genuine current authorization before decrypting again.
    await page.goto(url); await page.getByRole('button', { name: 'EN', exact: true }).click()
    await recover(page, context.id); await assertDevicePrivacy(page, context, 1)
  }
  const current = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}`)).json()
  const user = await (await page.request.get('/api/v1/auth/session')).json()
  const write = await page.request.post(`/api/v1/vessels/${vessel}/crew`, { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': crypto.randomUUID() }, data: {
    id: assignment, personId: current.assignment.personId, baseVersion: current.assignment.version, fullName: null, employeeNo: null, positionId: null,
    from: null, to: null, contractEndDate: null, watchGroup: 'SYNTHETIC-B', sourceObservedAt: null,
  } })
  expect(write.status() === 200).toBe(true)
  await region.getByRole('button', { name: 'Replay / check original outcome', exact: true }).click()
  await expect(region.getByRole('list', { name: 'Stored capture status' })).toContainText('MaterialConflict')
  await region.getByRole('button', { name: 'Review material conflict', exact: true }).click()
  if (medical) {
    const detail = await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}/offline/${context.id}/conflicts/${
      (await (await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}/offline/${context.id}/history`)).json()).items[0].commandId}`)
    expect(detail.status() === 200).toBe(true)
    const material = await detail.text()
    expect(!material.includes('2031-02-03') && !/medical/i.test(material)).toBe(true)
    await assertDevicePrivacy(page, context, 1)
  }
  const review = region.getByRole('region', { name: 'Synchronization conflict review', exact: true })
  await expect(review).toContainText('SYNTHETIC-A'); await expect(review).toContainText('SYNTHETIC-B')
  await expect(review.getByRole('button', { name: 'Submit resolution', exact: true })).toBeDisabled()
  await review.getByRole('radio', { name: 'Use captured value', exact: true }).check()
  await review.getByLabel('Resolution reason (required)', { exact: true }).fill('Explicit fictional draft reconciliation')
  await review.getByRole('button', { name: 'Submit resolution', exact: true }).click()
  await expect(region.getByRole('list', { name: 'Stored capture status' })).toContainText('Committed')
  await region.getByRole('button', { name: 'Load synchronization history', exact: true }).click()
  await expect(region.getByRole('list', { name: 'Synchronization history' })).toContainText('explicit resolution lineage')
  const history = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}/offline/${context.id}/history`)).json()
  expect(history.items.length === 2 && history.items.some((o: { result: string }) => o.result === 'MaterialConflict') && history.items.some((o: { parentCommandId: string | null }) => !!o.parentCommandId)).toBe(true)
  if (medical) {
    expect(!JSON.stringify(history).includes('2031-02-03') && !/medical/i.test(JSON.stringify(history))).toBe(true)
    await assertDevicePrivacy(page, context, 2)
    const profile = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}/personal-profile?purpose=crew-profile`)).json()
    expect(profile.data.medicalExpiry === '2031-02-03').toBe(true)
  }
}
type DeviceContext = { tenantId: string; actorId: string; vesselId: string; assignmentId: string; id: string;
  deviceId: string; keyId: string; sessionBinding: string; policyVersion: string; maxRecords: number }
async function assertDevicePrivacy(page: Page, context: DeviceContext, expectedCaptures: number) {
  // Only this disposable browser's genuinely enrolled device key decrypts its
  // actual IndexedDB records. No key/plaintext/ciphertext is returned to Node or
  // written to diagnostics. Do not add a product export/decrypt test endpoint.
  const result = await page.evaluate(async ctx => {
    const hash = async (text: string) => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text))), b => b.toString(16).padStart(2, '0')).join('')
    const namespace = await hash(JSON.stringify([ctx.tenantId, ctx.actorId, ctx.vesselId, ctx.assignmentId, ctx.id, ctx.deviceId, ctx.keyId, ctx.policyVersion]))
    async function read<T>(database: string, store: string, key?: string): Promise<T> {
      return new Promise((resolve, reject) => {
        const open = indexedDB.open(database, 1)
        open.onupgradeneeded = () => open.transaction?.abort() // absence must fail, never initialize a substitute
        open.onerror = open.onblocked = () => reject(new Error('synthetic_device_inspection_unavailable'))
        open.onsuccess = () => {
          const db = open.result; const tx = db.transaction(store, 'readonly')
          const request = key ? tx.objectStore(store).get(key) : tx.objectStore(store).getAll()
          let value: T
          request.onsuccess = () => { value = request.result as T }
          tx.oncomplete = () => { db.close(); resolve(value) }
          tx.onabort = tx.onerror = () => { db.close(); reject(new Error('synthetic_device_inspection_unavailable')) }
        }
      })
    }
    const device = await read<{ key: CryptoKey }>('poseidon.offline.keys.v1', 'keys', namespace)
    const records = (await read<{ namespace: string; commandId: string; sealed: {
      iv: Uint8Array<ArrayBuffer>; ciphertext: ArrayBuffer; contextHash: string; version: number
    } }[]>('poseidon.offline.capture.v1', 'encryptedRecords')).filter(r => r.namespace === namespace)
    if (!device?.key || device.key.extractable || records.length > ctx.maxRecords) return { private: false, snapshot: false, captures: 0 }
    let snapshot = false, captures = 0, privateDataAbsent = true
    const exact = (v: Record<string, unknown>, required: string[], optional: string[] = []) =>
      !!v && typeof v === 'object' && !Array.isArray(v) && required.every(k => Object.hasOwn(v, k)) && Object.keys(v).every(k => required.includes(k) || optional.includes(k))
    for (const record of records) {
      const additional = JSON.stringify(['poseidon.offline.capture.v1', ctx.tenantId, ctx.actorId, ctx.vesselId,
        ctx.deviceId, ctx.sessionBinding, ctx.policyVersion, record.commandId])
      if (record.sealed.version !== 1 || record.sealed.contextHash !== await hash(additional)) return { private: false, snapshot: false, captures: 0 }
      const bytes = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: record.sealed.iv, additionalData: new TextEncoder().encode(additional), tagLength: 128 }, device.key, record.sealed.ciphertext)
      const text = new TextDecoder().decode(bytes)
      privateDataAbsent &&= !text.includes('2031-02-03') && !/medical/i.test(text)
      const envelope = JSON.parse(text), value = JSON.parse(envelope.body)
      privateDataAbsent &&= exact(envelope, ['body'], ['outcome'])
      if (record.commandId === ctx.id) {
        snapshot = value.kind === 'snapshot' && exact(value, ['kind', 'watchGroup', 'baseVersion', 'revision'])
      } else {
        captures++
        privateDataAbsent &&= exact(value, ['commandId', 'keyId', 'deviceId', 'policyVersion', 'sourceVersion', 'operation', 'baseVersion', 'capturedAt', 'offsetMinutes', 'watchGroup'],
          ['parentCommandId', 'expectedServerVersion', 'choice', 'reason']) && value.operation === 'crew.draft.watch-group.v1'
        if (envelope.outcome) privateDataAbsent &&= exact(envelope.outcome, ['commandId', 'result', 'baseVersion', 'serverVersion', 'revision', 'capturedAt', 'offsetMinutes', 'receivedAt', 'parentCommandId'])
      }
    }
    return { private: privateDataAbsent, snapshot, captures }
  }, context)
  expect(result.private && result.snapshot && result.captures === expectedCaptures).toBe(true)
}
async function medicalOfflinePrivacy(page: Page) {
  let privateReplayAbsent = true, deliveries = 0
  await page.route('**/offline/*/replay', async route => {
    const body = route.request().postData() ?? ''
    deliveries++; privateReplayAbsent &&= !body.includes('2031-02-03') && !/medical/i.test(body)
    await route.continue() // real host transactions, no fabricated business response
  })
  await materialConflict(page, true)
  expect(privateReplayAbsent && deliveries === 2).toBe(true)
}
async function reloginRightsWithdrawal(page: Page) {
  const { vessel, assignment, context, url } = await setup(page)
  await offlineCapture(page); await page.context().setOffline(false)
  // Real provider-issued sessions; no role header, forged cookie or application test-login endpoint.
  // Same-actor live membership revocation additionally has actual API/PG coverage.
  const { login } = await import('../lab/journeys.ts')
  await page.context().clearCookies(); await login(page, 'lab-restricted', '/app/today')
  await page.goto(url); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const region = page.getByRole('region', { name: 'Durable offline drafts', exact: true })
  await region.getByRole('button', { name: 'Find retained contexts', exact: true }).click()
  await expect(region.getByText('Current access denied. Local encrypted data retained.', { exact: true })).toBeVisible()
  await expect(region.getByRole('list', { name: 'Stored capture status' })).toHaveCount(0)
  expect((await page.request.get(`/api/v1/vessels/${vessel}/crew/${assignment}/offline/${context.id}/history`)).status() === 404).toBe(true)
  await page.context().clearCookies(); await login(page, 'lab-manager', '/app/today')
  await page.goto(url); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await recover(page, context.id)
  await expect(region.getByRole('list', { name: 'Stored capture status' })).toContainText('Unsent / outcome unknown')
}
async function lostKeyRetainsUntilDiscard(page: Page) {
  const { context, url, region } = await setup(page)
  await offlineCapture(page); await page.context().setOffline(false)
  // Controlled browser fault: erase ONLY the test browser's nonextractable key vault.
  await page.evaluate(() => new Promise<void>((resolve, reject) => {
    const open = indexedDB.open('poseidon.offline.keys.v1', 1)
    open.onerror = () => reject(new Error('synthetic_key_loss_failed'))
    open.onsuccess = () => {
      const db = open.result, tx = db.transaction('keys', 'readwrite'); tx.objectStore('keys').clear()
      tx.oncomplete = () => { db.close(); resolve() }; tx.onabort = () => { db.close(); reject(new Error('synthetic_key_loss_failed')) }
    }
  }))
  await page.goto(url); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await region.getByRole('button', { name: 'Find retained contexts', exact: true }).click()
  await region.getByRole('option', { name: 'Retained context 1', exact: true }).waitFor({ state: 'attached' })
  await region.getByRole('combobox', { name: 'Offline context', exact: true }).selectOption(context.id)
  await region.getByRole('button', { name: 'Authorize / renew / recover', exact: true }).click()
  await expect(region.getByText(/Local key unavailable. Data retained/)).toBeVisible()
  const discard = region.getByRole('button', { name: 'Discard retained ciphertext and report key loss', exact: true })
  await expect(discard).toBeDisabled(); await region.getByRole('checkbox').check(); await discard.click()
  await expect(region.getByRole('combobox', { name: 'Offline context', exact: true })).toHaveValue('')
}
export const offlineApplicationCases = [
  { name: 'offline-protected-medical-excluded-from-device-snapshot-conflict-replay', run: medicalOfflinePrivacy, restricted: false },
  { name: 'offline-application-restart-reconnect-response-loss-idempotent-replay', run: restartAndDuplicate, restricted: false },
  { name: 'offline-application-current-material-conflict-explicit-lineage', run: materialConflict, restricted: false },
  { name: 'offline-application-signed-relogin-current-rights-withdrawal-recovery', run: reloginRightsWithdrawal, restricted: false },
  { name: 'offline-application-key-loss-retention-explicit-discard', run: lostKeyRetainsUntilDiscard, restricted: false },
] as const
