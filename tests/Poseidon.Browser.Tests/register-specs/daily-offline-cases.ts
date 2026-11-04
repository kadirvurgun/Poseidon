import { expect, type Page } from '@playwright/test'
import { dailyRecordId, dailySignedSession, dailySourceVersion, requiredDailyFixture, type DailyKind } from './daily-fixture.ts'

// Actual signed disposable host, UI, WebCrypto/IndexedDB and PostgreSQL source.
// Missing real operator classification/retention/key/storage fixture FAILS.
// No mocked/fulfilled business responses, injected principals, skips or rollout.
const families = [
  { kind: 'Noon', api: 'noon-reports', screen: 'noon-report', field: 'position_lat', label: 'Latitude', value: '-0.000', outside: 'weather_summary' },
  { kind: 'Deck', api: 'deck-daily-logs', screen: 'deck-daily-log', field: 'handover_note', label: 'Watch handover note', value: 'FICTIONAL deck handover', outside: 'weather' },
  { kind: 'Engine', api: 'engine-daily-logs', screen: 'engine-daily-log', field: 'alarms', label: 'Alarms / trips', value: 'FICTIONAL unverified alarm declaration', outside: 'critical_measurements' },
] as const
async function dailyOfflineJourney(page: Page, family: typeof families[number]) {
  const fixture = requiredDailyFixture(page)
  let user = await dailySignedSession(page, fixture)
  const id = dailyRecordId(fixture, family.kind, 'offline'), root = `/api/v1/daily-offline/${family.kind}/${id}?vesselId=${fixture.vesselId}`
  const ordinary = (suffix = '') => `/api/v1/${family.api}${suffix}?vesselId=${fixture.vesselId}`
  expect((await page.request.get(ordinary(`/${id}`))).status()).toBe(404)
  const capResponse = await page.request.get(ordinary('/capabilities')); expect(capResponse.status()).toBe(200)
  const caps = await capResponse.json()
  expect(caps.sourceVersion === dailySourceVersion && caps.canWrite === true && caps.offline.supported === true && caps.offline.state === 'CONFIGURED_UNVERIFIED').toBe(true)
  const url = `/app/${family.screen}/${fixture.vesselId}?draft=${id}`
  await page.goto(url); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const panel = page.getByRole('region', { name: 'Encrypted daily capture', exact: true })
  await expect(panel.getByText('Locked · online authorization required', { exact: true })).toBeVisible()
  const enrolled = page.waitForResponse(r => r.request().method() === 'POST' && new URL(r.url()).pathname === `/api/v1/daily-offline/${family.kind}/${id}`)
  await panel.getByRole('button', { name: 'Authorize encrypted capture', exact: true }).click()
  const response = await enrolled; expect(response.status()).toBe(200); const context = await response.json()
  expect(context.baseVersion === 0 && context.baseRevision === 0 && context.recordId === id && context.kind === family.kind).toBe(true)
  expect(context.actorId === fixture.writerActorId && context.tenantId === fixture.tenantId && context.sourceVersion === dailySourceVersion).toBe(true)
  expect(JSON.stringify(context.allowedFields) === JSON.stringify([family.field])).toBe(true)
  expect(!Object.hasOwn(context.baseFields, family.outside) && !Object.hasOwn(context.baseFields, 'master_review') && !Object.hasOwn(context.baseFields, 'chief_engineer_review')).toBe(true)
  await expect(panel.getByText('Explicit bounded authorization', { exact: true })).toBeVisible()
  await page.context().setOffline(true)
  await panel.getByRole('combobox', { name: `${family.label} — Offline data state`, exact: true }).selectOption('Known')
  await panel.getByRole('textbox', { name: family.label, exact: true }).fill(family.value)
  await panel.getByRole('button', { name: 'Save classified fields to encrypted queue', exact: true }).click()
  await expect(panel.getByText('Retained · server outcome unconfirmed', { exact: true })).toBeVisible()
  await expect(panel.getByRole('button', { name: 'Send exact queued command', exact: true })).toBeDisabled()
  page.once('dialog', dialog => { expect(dialog.type()).toBe('beforeunload'); void dialog.accept() })
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  expect(new URL(page.url()).searchParams.get('draft')).toBe(id)
  await expect(page.getByRole('button', { name: 'Save classified fields to encrypted queue', exact: true })).toHaveCount(0)
  await expect(page.getByRole('textbox', { name: family.label, exact: true })).toHaveCount(0)
  await page.context().setOffline(false)
  user = await dailySignedSession(page, fixture)
  await expect(panel.getByText('Locked · online authorization required', { exact: true })).toBeVisible()
  await panel.getByRole('button', { name: 'Find retained contexts', exact: true }).click()
  await panel.getByRole('combobox', { name: 'Retained daily context', exact: true }).selectOption(context.id)
  await panel.getByRole('button', { name: 'Reauthorize retained context', exact: true }).click()
  await expect(panel.getByText('Retained · server outcome unconfirmed', { exact: true })).toBeVisible()
  let firstBody = '', firstHash = '', firstKey = '', firstSignature = '', exactRetry = false, deliveries = 0, commandId = ''
  const replayPattern = `**/api/v1/daily-offline/${family.kind}/${id}/${context.id}/replay?vesselId=${fixture.vesselId}`
  // Actual commit followed by real response loss; retry uses the exact persisted
  // body/key/hash. Signatures may differ while proving those same retained bytes.
  await page.route(replayPattern, async route => {
    deliveries++
    const headers = route.request().headers()
    if (deliveries === 1) {
      firstBody = route.request().postData()!; firstHash = headers['x-offline-hash']; firstKey = headers['idempotency-key']; firstSignature = headers['x-offline-signature']; commandId = JSON.parse(firstBody).commandId
      const actual = await route.fetch(); expect(actual.status()).toBe(200); expect((await actual.json()).result).toBe('Committed')
      await route.abort('connectionreset')
    } else { exactRetry = route.request().postData() === firstBody && headers['x-offline-hash'] === firstHash && headers['idempotency-key'] === firstKey; await route.continue() }
  })
  try {
    await panel.getByRole('button', { name: 'Send exact queued command', exact: true }).click()
    await expect(panel.getByText(/Submission outcome unknown/)).toBeVisible()
    await panel.getByRole('button', { name: 'Send exact queued command', exact: true }).click()
    await expect(panel.getByText('Server committed', { exact: true })).toBeVisible()
    expect(exactRetry && deliveries === 2).toBe(true)
  } finally { await page.unroute(replayPattern) }
  const outcome = await page.request.get(`/api/v1/daily-offline/${family.kind}/${id}/${context.id}/history?vesselId=${fixture.vesselId}`); expect(outcome.status()).toBe(200)
  const history = await outcome.json(); expect(history.items.length === 1 && history.items[0].result === 'Committed' && history.items[0].commandId === commandId).toBe(true)
  const savedResponse = await page.request.get(ordinary(`/${id}`)); expect(savedResponse.status()).toBe(200); const saved = await savedResponse.json()
  expect(saved.revision === 1 && saved.fields[family.field].value === family.value && saved.state === 'DRAFT' && saved.reviewState === 'PENDING').toBe(true)
  const revisions = await (await page.request.get(ordinary(`/${id}/history`))).json(); expect(revisions.items.length === 1).toBe(true)
  // A second dedicated grant captures against revision1. A real owning update
  // outside its classified subset still creates a material whole-head conflict.
  const e2response = await page.request.post(root, { headers: { 'X-CSRF-TOKEN': user.csrfToken }, data: {} }); expect(e2response.status()).toBe(200); const e2 = await e2response.json()
  await panel.getByRole('button', { name: 'Lock retained data', exact: true }).click()
  await panel.getByRole('button', { name: 'Find retained contexts', exact: true }).click()
  await panel.getByRole('combobox', { name: 'Retained daily context', exact: true }).selectOption(e2.id)
  await panel.getByRole('button', { name: 'Reauthorize retained context', exact: true }).click()
  await expect(panel.getByText('Explicit bounded authorization', { exact: true })).toBeVisible()
  await panel.getByRole('button', { name: 'Save classified fields to encrypted queue', exact: true }).click()
  const mutable = Object.fromEntries(caps.schema.filter((f: { readOnly: boolean }) => !f.readOnly).map((f: { key: string }) => [f.key, saved.fields[f.key]]))
  mutable[family.outside] = { state: 'Known', value: 'FICTIONAL outside classified subset update' }
  const changed = await page.request.put(ordinary(`/${id}`), { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': crypto.randomUUID() }, data: { id, expectedVersion: saved.version, fields: mutable, reasonCode: 'AUTHORING_CORRECTION' } }); expect(changed.status()).toBe(200); const beforeKeep = await changed.json()
  await panel.getByRole('button', { name: 'Send exact queued command', exact: true }).click()
  await expect(panel.getByRole('region', { name: 'Daily material conflict', exact: true })).toBeVisible()
  await panel.getByRole('button', { name: 'Retain server · record reason', exact: true }).click()
  await panel.getByRole('button', { name: 'Send exact queued command', exact: true }).click()
  await expect(panel.getByText('Server retained · no report revision added', { exact: true })).toBeVisible()
  const afterKeep = await (await page.request.get(ordinary(`/${id}`))).json()
  expect(afterKeep.version === beforeKeep.version && afterKeep.revision === beforeKeep.revision && afterKeep.hash === beforeKeep.hash && afterKeep.savedAt === beforeKeep.savedAt).toBe(true)
  const revoke = await page.request.post(`/api/v1/daily-offline/${family.kind}/${id}/${e2.id}/revoke?vesselId=${fixture.vesselId}`, { headers: { 'X-CSRF-TOKEN': user.csrfToken } }); expect(revoke.status()).toBe(200)
  const lost = await page.request.post(`/api/v1/daily-offline/${family.kind}/${id}/${context.id}/lost?vesselId=${fixture.vesselId}`, { headers: { 'X-CSRF-TOKEN': user.csrfToken } }); expect(lost.status()).toBe(200)
  const denied = await page.request.post(`/api/v1/daily-offline/${family.kind}/${id}/${context.id}/replay?vesselId=${fixture.vesselId}`, { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Content-Type': 'application/json', 'Idempotency-Key': firstKey, 'X-Offline-Hash': firstHash, 'X-Offline-Signature': firstSignature }, data: firstBody }); expect(denied.status()).toBe(403)
  const cleanup = await page.request.post(`/api/v1/daily-offline/${family.kind}/${id}/${context.id}/cleanup?vesselId=${fixture.vesselId}`, { headers: { 'X-CSRF-TOKEN': user.csrfToken } }); expect(cleanup.status()).toBe(200)
  expect(!Object.hasOwn(await cleanup.json(), 'baseFields')).toBe(true)
  const forbidden = await page.request.post(`/api/v1/daily-offline/${family.kind}/${id}?vesselId=${fixture.foreignVesselId}`, { headers: { 'X-CSRF-TOKEN': user.csrfToken }, data: {} }); expect(forbidden.status()).toBe(404)
  expect((await (await page.request.get(ordinary(`/${id}/history`))).json()).items.length).toBe(2)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
}
export const dailyOfflineCases = families.map(family => ({ name: `daily-${family.kind.toLowerCase()}-encrypted-restart-exact-retry-human-conflict-scope`, run: (page: Page) => dailyOfflineJourney(page, family), restricted: false }))
export const dailyOfflineKinds: readonly DailyKind[] = families.map(f => f.kind)
