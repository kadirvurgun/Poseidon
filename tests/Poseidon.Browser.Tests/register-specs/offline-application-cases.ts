import { expect, type Page } from '@playwright/test'

// OA-BROWSER01..04: actual signed host, PostgreSQL, ordinary commands, product UI,
// WebCrypto, IndexedDB and the static-only worker. No fabricated business responses.
// Later gate MUST provision explicit fictional offline policy for the writer's Ship
// scope with SYNTHETIC-A/B. Missing policy fails; these cases never skip it.
async function setup(page: Page) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25&kind=Ship')).json()
  expect(vessels.items.length > 0).toBe(true); const vessel = vessels.items[0].id as string
  const user = await (await page.request.get('/api/v1/auth/session')).json()
  const assignment = crypto.randomUUID()
  const response = await page.request.post(`/api/v1/vessels/${vessel}/crew`, { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': crypto.randomUUID() }, data: {
    id: assignment, personId: null, baseVersion: null, fullName: null, employeeNo: null, positionId: null, from: null, to: null,
    contractEndDate: null, watchGroup: null, sourceObservedAt: null,
  } })
  expect(response.status() === 200).toBe(true)
  const url = `/app/crew-roster/${vessel}/crew-profile/${assignment}`
  await page.goto(url); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const region = page.getByRole('region', { name: 'Durable offline drafts', exact: true })
  const enrolled = page.waitForResponse(r => r.url().endsWith(`/crew/${assignment}/offline`) && r.request().method() === 'POST')
  await region.getByRole('button', { name: 'Authorize / renew / recover', exact: true }).click()
  const enrollmentResponse = await enrolled; expect(enrollmentResponse.status() === 200).toBe(true)
  const context = await enrollmentResponse.json()
  await expect(region.getByText('Encrypted capture available for this authorization.', { exact: true })).toBeVisible()
  return { vessel, assignment, url, region, context }
}
async function recover(page: Page, enrollment: string) {
  const region = page.getByRole('region', { name: 'Durable offline drafts', exact: true })
  await region.getByRole('button', { name: 'Find retained contexts', exact: true }).click()
  await expect(region.getByRole('option', { name: 'Retained context 1', exact: true })).toBeAttached()
  await region.getByLabel('Offline context', { exact: true }).selectOption(enrollment)
  await region.getByRole('button', { name: 'Authorize / renew / recover', exact: true }).click()
  await expect(region.getByText('Encrypted capture available for this authorization.', { exact: true })).toBeVisible()
  return region
}
async function offlineCapture(page: Page) {
  const region = page.getByRole('region', { name: 'Durable offline drafts', exact: true })
  await page.context().setOffline(true)
  await region.getByLabel('Offline draft watch group', { exact: true }).selectOption('SYNTHETIC-A')
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
  const cached = await page.evaluate(async () => {
    const entries = await caches.open('poseidon.offline.static.v1'); return (await entries.keys()).map(r => new URL(r.url).pathname)
  })
  expect(cached.length > 0 && cached.every(p => p === '/index.html' || p.startsWith('/assets/'))).toBe(true)
}
async function materialConflict(page: Page) {
  const { vessel, assignment, context, region } = await setup(page)
  await offlineCapture(page); await page.context().setOffline(false); await recover(page, context.id)
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
  await region.getByLabel('Offline context', { exact: true }).selectOption(context.id)
  await region.getByRole('button', { name: 'Authorize / renew / recover', exact: true }).click()
  await expect(region.getByText(/Local key unavailable. Data retained/)).toBeVisible()
  const discard = region.getByRole('button', { name: 'Discard retained ciphertext and report key loss', exact: true })
  await expect(discard).toBeDisabled(); await region.getByRole('checkbox').check(); await discard.click()
  await expect(region.getByRole('combobox', { name: 'Offline context', exact: true })).toHaveValue('')
}
export const offlineApplicationCases = [
  { name: 'offline-application-restart-reconnect-response-loss-idempotent-replay', run: restartAndDuplicate, restricted: false },
  { name: 'offline-application-current-material-conflict-explicit-lineage', run: materialConflict, restricted: false },
  { name: 'offline-application-signed-relogin-current-rights-withdrawal-recovery', run: reloginRightsWithdrawal, restricted: false },
  { name: 'offline-application-key-loss-retention-explicit-discard', run: lostKeyRetainsUntilDiscard, restricted: false },
] as const
