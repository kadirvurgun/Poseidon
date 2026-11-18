import { expect, type Page } from '@playwright/test'
import { requiredShipOpsFixture, requireShipOpsCaps, requireShipOpsSession, shipOpsLane, shipOpsSignedId } from './ship-operations-fixture.ts'

// Source-only disposable-host journeys. The principal and declared Noon origin
// must already be genuinely provisioned by the operator. No business fulfillment,
// injected actor, fake catalog, verified evidence, finalization or provider edit.
async function fuelExactReceipt(page: Page) {
  const fixture = requiredShipOpsFixture(), lane = shipOpsLane(page), user = await requireShipOpsSession(page, fixture), { root, fields } = await requireShipOpsCaps(page, fixture, 'Fuel')
  const id = shipOpsSignedId(fixture, lane, 'fuel'), query = '?vesselId=' + fixture.vesselId
  expect((await page.request.get(root + '/' + id + query)).status()).toBe(404)
  fields.record_type = { state: 'Known', value: 'Delivery/BDN' }; fields.fuel_type = { state: 'Known', value: 'MGO' }; fields.bdn_no = { state: 'Known', value: 'SYNTHETIC-SOFTWARE-REGRESSION' }; fields.evidence_ref = { state: 'Unknown', value: null }
  const initial = await page.request.post(root + query, { headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': shipOpsSignedId(fixture, lane, 'fuel-create') }, data: { id, expectedVersion: 0, fields, reasonCode: 'INITIAL_AUTHORING', origin: null } }); expect(initial.status()).toBe(200)
  const original = await initial.json(); expect(original.currentRevision === false && original.canEdit === false && original.state === 'DRAFT').toBe(true)
  await page.goto('/app/fuel-bunker-log/' + fixture.vesselId + '?id=' + id); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const form = page.getByRole('form', { name: 'Fuel draft form', exact: true }); await expect(form).toBeVisible()
  await expect(form.getByLabel('BDN / evidence reference value state', { exact: true }).locator('option[value="Known"]')).toBeDisabled()
  await form.getByLabel('Quantity received (mt) value state', { exact: true }).selectOption('Known'); await form.getByLabel('Quantity received (mt)', { exact: true }).fill('9007199254740993.123456789')
  let writes = 0, body = '', key = '', receipt = ''
  page.on('request', request => { if (new URL(request.url()).pathname === root + '/' + id && request.method() === 'PUT') writes++ })
  const lostRoute = new RegExp('/fuel-bunker-logs/' + id + '\\?vesselId=' + fixture.vesselId + '$')
  await page.route(lostRoute, async route => {
    if (route.request().method() !== 'PUT') { await route.continue(); return }
    body = route.request().postData() ?? ''; key = route.request().headers()['idempotency-key'] ?? ''
    const actual = await route.fetch(); expect(actual.status()).toBe(200); receipt = await actual.text()
    await route.abort('connectionfailed'); await page.unroute(lostRoute)
  })
  await form.getByRole('button', { name: 'Save unverified draft', exact: true }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'Outcome unknown.' })).toBeVisible(); expect(writes).toBe(1)
  await expect(form.getByLabel('Quantity received (mt)', { exact: true })).toBeDisabled()
  const replay = page.waitForResponse(response => new URL(response.url()).pathname === root + '/' + id && response.request().method() === 'PUT')
  await form.getByRole('button', { name: 'Retry exact operational command', exact: true }).click()
  const repeated = await replay; expect(repeated.status()).toBe(200); expect(repeated.request().postData()).toBe(body); expect(repeated.request().headers()['idempotency-key']).toBe(key); expect(await repeated.text()).toBe(receipt)
  const saved = await repeated.json(); expect(saved.currentRevision === false && saved.canEdit === false && saved.revision === original.revision + 1).toBe(true)
  expect(saved.fields.quantity_received_mt).toEqual({ state: 'Known', value: '9007199254740993.123456789' })
  expect(saved.fields.compliance_state).toEqual({ state: 'Known', value: 'REVIEW_REQUIRED' }); expect(saved.fields.verification_state).toEqual({ state: 'Known', value: 'PENDING' })
  await expect(form.getByLabel('Quantity received (mt)', { exact: true })).toBeEnabled()
  const current = await page.request.get(root + '/' + id + query); expect(current.status()).toBe(200); expect((await current.json()).currentRevision).toBe(true)
  const history = await page.request.get(root + '/' + id + '/history' + query); expect(history.status()).toBe(200); expect((await history.json()).items).toHaveLength(2)
  expect(writes).toBe(2)
}

async function portDeclaredTimeline(page: Page) {
  const fixture = requiredShipOpsFixture(); await requireShipOpsSession(page, fixture); const { root } = await requireShipOpsCaps(page, fixture, 'Port')
  const origin = await page.request.get('/api/v1/vessels/' + fixture.vesselId + '/ship-operation-origins/Noon/' + fixture.origin.id + '?revision=' + fixture.origin.revision); expect(origin.status()).toBe(200)
  const context = await origin.json(); expect(context.pin).toEqual(fixture.origin); expect(context.current === true && context.state === 'DRAFT' && context.qualification === 'REVIEW_REQUIRED').toBe(true)
  await page.goto('/app/port-activity-sof/' + fixture.vesselId + '?originKind=Noon&originId=' + fixture.origin.id + '&originRevision=' + fixture.origin.revision); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const form = page.getByRole('form', { name: 'Port activity draft form', exact: true }); await expect(form).toBeVisible()
  await expect(form.getByLabel('Port reference value state', { exact: true }).locator('option[value="Known"]')).toBeDisabled()
  await form.getByLabel('Activity type value state', { exact: true }).selectOption('Known'); await form.getByLabel('Activity type', { exact: true }).selectOption('Arrival')
  await form.getByLabel('Event start value state', { exact: true }).selectOption('Known'); await form.getByLabel('Event start', { exact: true }).fill('2026-11-05T15:30:00.1234567+03:00')
  await form.getByLabel('Event end value state', { exact: true }).selectOption('Known'); await form.getByLabel('Event end', { exact: true }).fill('2026-11-05T12:30:00.1234568Z')
  await form.getByLabel('Activity remarks value state', { exact: true }).selectOption('Known'); await form.getByLabel('Activity remarks', { exact: true }).fill('SYNTHETIC recorded event\nNo instructions or finalization')
  await expect(form.getByLabel('Cargo quantity value state', { exact: true })).toHaveValue('Missing')
  expect(await form.getByRole('button', { name: /finalize|approve|verify/i }).count()).toBe(0)
  let writes = 0
  page.on('request', request => { if (new URL(request.url()).pathname === root && request.method() === 'POST') writes++ })
  await page.getByRole('button', { name: 'Back to records', exact: true }).click(); await page.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(form.getByLabel('Event start', { exact: true })).toHaveValue('2026-11-05T15:30:00.1234567+03:00'); expect(writes).toBe(0)
  const response = page.waitForResponse(result => new URL(result.url()).pathname === root && result.request().method() === 'POST')
  await form.getByRole('button', { name: 'Save unverified draft', exact: true }).click()
  const actual = await response; expect(actual.status()).toBe(200); const saved = await actual.json(), command = actual.request().postDataJSON()
  expect(Object.keys(command.fields)).toHaveLength(17); expect(command.origin).toEqual(fixture.origin); expect(command.fields.sof_state).toBeUndefined()
  expect(saved.fields.event_start.value).toBe('2026-11-05T15:30:00.1234567+03:00'); expect(saved.fields.remarks.value).toBe('SYNTHETIC recorded event\nNo instructions or finalization'); expect(saved.fields.cargo_quantity).toEqual({ state: 'Missing', value: null }); expect(saved.fields.sof_state).toEqual({ state: 'Known', value: 'DRAFT' })
  expect(saved.currentRevision === false && saved.canEdit === false && saved.state === 'DRAFT').toBe(true)
  await expect(page).toHaveURL(new RegExp('/app/port-activity-sof/' + fixture.vesselId + '\\?id=' + saved.id + '$'))
  await expect(form.getByLabel('Activity remarks', { exact: true })).toBeEnabled(); expect(writes).toBe(1)
  const history = await page.request.get(root + '/' + saved.id + '/history?vesselId=' + fixture.vesselId); expect(history.status()).toBe(200); expect((await history.json()).items).toHaveLength(1)
  const actions = form.getByRole('group', { name: 'Operational form actions', exact: true }); await actions.scrollIntoViewIfNeeded()
  const boxes = await actions.getByRole('button').evaluateAll(buttons => buttons.map(button => { const r = button.getBoundingClientRect(); return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, height: r.height } }))
  expect(boxes).toHaveLength(2); const width = page.viewportSize()!.width
  for (const box of boxes) expect(box.left >= 0 && box.right <= width && box.height >= 24).toBe(true)
  expect(boxes[0].right <= boxes[1].left || boxes[0].bottom <= boxes[1].top || boxes[1].right <= boxes[0].left || boxes[1].bottom <= boxes[0].top).toBe(true)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
}
export const shipOperationsCases = [
  { name: 'ship-operations-signed-fuel-loss-exact-draft-receipt', run: fuelExactReceipt, restricted: false },
  { name: 'ship-operations-signed-port-origin-timeline-draft', run: portDeclaredTimeline, restricted: false },
] as const
