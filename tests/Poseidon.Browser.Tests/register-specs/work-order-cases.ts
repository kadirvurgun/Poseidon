import { expect, type Page } from '@playwright/test'
import { workOrderSignedId, requireWorkOrderContext, requireWorkOrderFixture } from './work-order-fixture.ts'
// Genuine disposable-host source only: real signed principal, current plan and
// audited owning API. Missing prerequisites fail; no fabricated business JSON,
// actor injection, Human approval, provider edit or source version is supplied.
async function exactWorkOrderReceipt(page: Page) {
  const fixture = requireWorkOrderFixture(), { user, fields, plan, sourceVersion } = await requireWorkOrderContext(page, fixture), id = workOrderSignedId(fixture, page, 'receipt'), query = '?vessel=' + fixture.vesselId, root = '/api/v1/work-orders'
  expect((await page.request.get(root + '/' + id + query)).status()).toBe(404)
  fields.work_order_no = { state: 'Known', value: 'SYNTHETIC-CRITICAL-DECLARATION' }; fields.execution_stage = { state: 'Known', value: 'RELEASED' }; fields.work_done = { state: 'Known', value: 'SYNTHETIC execution declaration; not verified' }; fields.actual_finish = { state: 'Unknown', value: null }
  const initial = await page.request.post(root + query, { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': workOrderSignedId(fixture, page, 'initial') }, data: { id, expectedVersion: 0, fields, plan, sourceVersion } }); expect(initial.status()).toBe(200); const original = await initial.json()
  expect(original.state === 'DRAFT' && original.qualification === 'REVIEW_REQUIRED' && original.currentRevision === false && original.canEdit === false).toBe(true)
  await page.goto('/app/work-order-center/' + fixture.vesselId + '?id=' + id); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const form = page.getByRole('form', { name: 'Work order draft form', exact: true }); await expect(form).toBeVisible()
  for (const name of ['Release work order', 'Submit evidence', 'Verify work order', 'Close work order']) expect(await form.getByRole('button', { name, exact: true }).count()).toBe(0)
  await form.getByLabel('Work description value state', { exact: true }).selectOption('Known'); await form.getByLabel('Work description', { exact: true }).fill('SYNTHETIC correction\nSource review remains open')
  let body = '', key = '', returned: Record<string, unknown> | undefined, writes = 0
  const path = root + '/' + id, lostRoute = new RegExp('/api/v1/work-orders/' + id + '\\?vessel=' + fixture.vesselId + '$')
  page.on('request', request => { if (new URL(request.url()).pathname === path && request.method() === 'PUT') writes++ })
  await page.route(lostRoute, async route => { if (route.request().method() !== 'PUT') { await route.continue(); return } body = route.request().postData() ?? ''; key = route.request().headers()['idempotency-key'] ?? ''; const actual = await route.fetch(); expect(actual.status()).toBe(200); returned = await actual.json(); await route.abort('connectionfailed'); await page.unroute(lostRoute) })
  await form.getByRole('button', { name: 'Save unassessed draft', exact: true }).click(); await expect(page.getByRole('alert').filter({ hasText: 'The save outcome is unknown.' })).toBeVisible(); expect(writes).toBe(1)
  await expect(form.getByLabel('Work description', { exact: true })).toBeDisabled()
  const replay = page.waitForResponse(response => new URL(response.url()).pathname === path && response.request().method() === 'PUT')
  await form.getByRole('button', { name: 'Retry exact work order command', exact: true }).click(); const actual = await replay; expect(actual.status()).toBe(200); expect(actual.request().postData()).toBe(body); expect(actual.request().headers()['idempotency-key']).toBe(key)
  const saved = await actual.json(); expect(returned).toBeDefined()
  // The exact immutable owning receipt, including retained plan pin, must replay.
  for (const property of ['id', 'vesselId', 'revision', 'version', 'fields', 'plan', 'sourceVersion', 'hash', 'provenance', 'savedAt', 'state', 'qualification', 'blockers', 'currentRevision', 'canEdit']) expect(saved[property]).toEqual(returned![property])
  expect(saved.state === 'DRAFT' && saved.qualification === 'REVIEW_REQUIRED' && saved.fields.actual_finish.state === 'Unknown' && saved.fields.execution_stage.value === 'RELEASED' && saved.revision === original.revision + 1).toBe(true)
  await expect(form.getByLabel('Work description', { exact: true })).toBeEnabled()
  const history = await page.request.get(path + '/history' + query); expect(history.status()).toBe(200); const rows = (await history.json()).items
  expect(rows).toHaveLength(2); expect(rows.every((row: { canEdit: boolean }) => row.canEdit === false)).toBe(true); expect(writes).toBe(2)
}
async function confirmedWorkOrderReadbackRecovery(page: Page) {
  const fixture = requireWorkOrderFixture(), { plan } = await requireWorkOrderContext(page, fixture), root = '/api/v1/work-orders'
  await page.goto('/app/work-order-center/' + fixture.vesselId + '?planId=' + fixture.planId + '&planRevision=' + fixture.planRevision); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const form = page.getByRole('form', { name: 'Work order draft form', exact: true }); await expect(form).toBeVisible()
  await expect(form.getByLabel('Criticality value state', { exact: true })).toHaveValue('Missing')
  await form.getByLabel('Work done declaration value state', { exact: true }).selectOption('Known'); await form.getByLabel('Work done declaration', { exact: true }).fill('SYNTHETIC unassessed execution declaration')
  await form.getByLabel('Declared actual finish value state', { exact: true }).selectOption('Unknown')
  await page.getByRole('button', { name: 'Back to work orders', exact: true }).click(); await page.getByRole('button', { name: 'Keep editing', exact: true }).click()
  let savedId = '', posts = 0, failReadback = true
  await page.route(new RegExp('/api/v1/work-orders\\?vessel=' + fixture.vesselId + '$'), async route => { if (route.request().method() === 'POST') { savedId = route.request().postDataJSON().id; posts++ } await route.continue() })
  await page.route(new RegExp('/api/v1/work-orders/[0-9a-f-]{36}\\?vessel=' + fixture.vesselId + '$'), async route => { if (failReadback && route.request().method() === 'GET' && new URL(route.request().url()).pathname === root + '/' + savedId) await route.abort('connectionfailed'); else await route.continue() })
  const result = page.waitForResponse(response => new URL(response.url()).pathname === root && response.request().method() === 'POST')
  await form.getByRole('button', { name: 'Save unassessed draft', exact: true }).click(); const response = await result; expect(response.status()).toBe(200); const receipt = await response.json(); expect(receipt.plan).toEqual(plan)
  await expect(page.getByRole('alert').filter({ hasText: 'Work order records or current access are unavailable.' })).toBeVisible(); await expect(page).toHaveURL(new RegExp('/app/work-order-center/' + fixture.vesselId + '\\?id=' + savedId + '$')); expect(posts).toBe(1)
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click(); await expect(page.getByRole('alert').filter({ hasText: 'Work order records or current access are unavailable.' })).toBeVisible(); expect(posts).toBe(1)
  failReadback = false; await page.getByRole('button', { name: 'Retry source read', exact: true }).click(); await expect(form.getByLabel('Work done declaration', { exact: true })).toBeEnabled()
  await expect(page).toHaveURL(new RegExp('/app/work-order-center/' + fixture.vesselId + '\\?id=' + savedId + '$')); expect(posts).toBe(1)
  const actions = form.getByRole('group', { name: 'Work order form actions', exact: true }); await actions.scrollIntoViewIfNeeded()
  const boxes = await actions.getByRole('button').evaluateAll(buttons => buttons.map(button => { const r = button.getBoundingClientRect(); return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, height: r.height } }))
  const width = page.viewportSize()!.width; expect(boxes).toHaveLength(2); for (const box of boxes) expect(box.left >= 0 && box.right <= width && box.height >= (width <= 420 ? 44 : 40)).toBe(true)
  expect(boxes[0].right <= boxes[1].left || boxes[0].bottom <= boxes[1].top || boxes[1].right <= boxes[0].left || boxes[1].bottom <= boxes[0].top).toBe(true); expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
}
export const workOrderCases = [
  { name: 'work-order-signed-unassessed-phase-exact-receipt', run: exactWorkOrderReceipt, restricted: false },
  { name: 'work-order-signed-confirmed-id-reopen-after-readback-loss', run: confirmedWorkOrderReadbackRecovery, restricted: false },
] as const
