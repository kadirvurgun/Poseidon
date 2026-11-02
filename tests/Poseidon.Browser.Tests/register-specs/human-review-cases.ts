import { expect, type Page } from '@playwright/test'

// Genuine signed-host journeys. No request interception, test login or synthetic
// successful authorization in runtime. Operator appointments are gate prerequisites.
async function scopedTrace(page: Page) {
  await page.goto('/app/source-register?lang=en')
  await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click()
  const region = page.getByRole('region', { name: 'Controls and evidence', exact: true })
  const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200)
  const vessel = (await fleet.json()).items.find((v: { kind: string }) => v.kind === 'Ship'); expect(!!vessel).toBe(true)
  await region.getByLabel('Vessel context', { exact: true }).selectOption(vessel.id)
  await region.getByLabel('Register', { exact: true }).selectOption('Evidence')
  const response = await page.request.get(`/api/v1/evidence?vesselId=${vessel.id}&offset=0`); expect(response.status()).toBe(200)
  const evidence = (await response.json()).items.find((e: { links: { kind: string }[] }) => e.links.some(l => l.kind === 'Control'))
  // Missing fixture is an unmet prerequisite, never a skip or fabricated green trace.
  expect(!!evidence).toBe(true)
  await region.getByRole('button', { name: new RegExp(evidence.content.title ?? evidence.id) }).first().click()
  const trace = page.getByRole('region', { name: 'Human review and decision trace', exact: true }).first()
  await expect(trace.getByText('External signature mechanism unavailable', { exact: true })).toBeVisible()
  await expect(trace.getByText('Authoritative evaluation unavailable', { exact: true })).toBeVisible()
  const reason = trace.getByLabel('Bound review reason', { exact: true })
  await reason.focus(); await expect(reason).toBeFocused(); await reason.fill('FICTIONAL deferred signed-host review draft')
  await region.getByLabel('Register', { exact: true }).selectOption('Control')
  await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(reason).toHaveValue('FICTIONAL deferred signed-host review draft')
  await page.context().setOffline(true)
  try { await expect(trace.getByText('Offline. Human review requires server connectivity.', { exact: true })).toBeVisible(); await expect(trace.getByRole('button', { name: 'Record pending request', exact: true })).toBeDisabled() }
  finally { await page.context().setOffline(false) }
  await trace.getByRole('button', { name: 'Discard local review draft', exact: true }).click()
  await expect(trace.getByRole('button', { name: /approve|release permit/i })).toHaveCount(0)
  return trace
}
async function assignedRecording(page: Page) {
  // Later gate only: disposable synthetic evidence + genuine signed-host assigned
  // human membership with exact recording policy. No operational decision fixture.
  const trace = await scopedTrace(page)
  const choose = trace.getByRole('button', { name: 'Record authorized human decision', exact: true }).first()
  await expect(choose).toBeVisible(); await choose.click()
  const authority = trace.getByLabel('Provisioned authority', { exact: true })
  const option = authority.locator('option').filter({ hasText: 'human.decide' }).first()
  await expect(option).toHaveCount(1) // Missing genuine assignment fails the gate.
  await authority.selectOption(await option.getAttribute('value') ?? '')
  await trace.getByLabel('Bound review reason', { exact: true }).fill('FICTIONAL bundle software journey only')
  await trace.getByLabel('Human decision text', { exact: true }).fill('FICTIONAL assigned recording; no safety approval')
  await trace.getByLabel('Decision context', { exact: true }).fill('Synthetic incomplete evidence, pending qualification')
  await trace.getByLabel('Trigger source', { exact: true }).fill('FICTIONAL foundation gate fixture')
  const saved = page.waitForResponse(r => r.request().method() === 'POST' && r.url().includes('/api/v1/decisions?'))
  await trace.getByRole('button', { name: 'Save human decision record', exact: true }).click()
  const response = await saved; expect(response.status()).toBe(200)
  const row = await response.json(); expect(row.state).toBe('RECORDED'); expect(row.recording.qualification).toBe('UNASSESSED')
  await expect(trace.getByText('Qualification pending. No legal signature, source adoption or permit activation.', { exact: true })).toBeVisible()
}
async function restrictedTrace(page: Page) {
  const query = '?vesselId=10000000-0000-4000-8000-000000000001&evidenceId=10000000-0000-4000-8000-000000000002&evidenceRevision=1&controlId=10000000-0000-4000-8000-000000000003&controlRevision=1'
  for (const route of ['regulatory-trace', 'verification-gates', 'audit-ready']) {
    const response = await page.request.get('/api/v1/' + route + query); expect(response.status()).toBe(403)
    const value = await response.json(); expect(value.items).toBeUndefined(); expect(value.count).toBeUndefined(); expect(value.authorities).toBeUndefined()
  }
}
export const humanReviewCases = [
  { name: 'assigned-human-recording-signed-host-pending-qualification', run: assignedRecording, restricted: false },
  { name: 'human-scoped-trace-source-signature-blockers-keyboard-dirty-offline', run: scopedTrace, restricted: false },
  { name: 'human-trace-denied-before-object-inference', run: restrictedTrace, restricted: true },
] as const
