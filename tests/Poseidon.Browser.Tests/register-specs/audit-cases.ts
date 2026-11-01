import { expect, type Page } from '@playwright/test'

// Real signed-provider session + actual PostgreSQL/API. Run only through the existing private safe runner.
// QA must explicitly provision audit.read on its disposable writer membership at the combined gate.
async function auditHistory(page: Page) {
  const workspace = await (await page.request.get('/api/v1/workspace')).json()
  expect(workspace.destinations.some((x: { key: string }) => x.key === 'audit')).toBe(true)
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json()
  expect(vessels.items.length > 0).toBe(true)
  const id = vessels.items[0].id
  const before = await (await page.request.get(`/api/v1/vessels/${id}/regulatory-profile`)).json()
  const session = await (await page.request.get('/api/v1/auth/session')).json()
  const saved = await page.request.put(`/api/v1/vessels/${id}/regulatory-profile`, {
    headers: { 'X-CSRF-TOKEN': session.csrfToken, 'Idempotency-Key': crypto.randomUUID() },
    data: { expectedVersion: before.version, content: before.content },
  })
  expect(saved.status() === 200).toBe(true)
  const current = await saved.json()
  const query = `vesselId=${id}&resourceId=${id}&entityType=RegulatoryDraft&action=Modified`
  const history = await (await page.request.get(`/api/v1/audit?${query}&limit=25`)).json()
  const record = history.items.find((x: { afterRevision: number }) => x.afterRevision === current.revision)
  expect(!!record && record.afterVersion === current.version).toBe(true)
  await page.goto(`/app/audit/${record.id}?${query}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Record details', exact: true })).toBeVisible()
  await expect(page.getByLabel('Vessel ID', { exact: true })).toHaveValue(id)
  const link = page.getByRole('link', { name: 'Open exact immutable revision', exact: true })
  expect(await link.getAttribute('href') === `/api/v1/audit/${record.id}/revision`).toBe(true)
  const evidence = await (await page.request.get(`/api/v1/audit/${record.id}/revision`)).json()
  expect(evidence.profile.revision === current.revision).toBe(true)
  await page.getByRole('button', { name: 'Close', exact: true }).click()
  expect(new URL(page.url()).searchParams.get('vesselId') === id).toBe(true)
  await page.goBack()
  await expect(page.getByRole('heading', { name: 'Record details', exact: true })).toBeVisible()
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Record details', exact: true })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  // Actual network outage, no fabricated runtime data or authentication response.
  await page.route('**/api/v1/audit?*', route => route.abort('connectionreset'))
  await page.getByRole('button', { name: 'Clear filters', exact: true }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'History is unavailable.' })).toBeVisible()
  await page.unroute('**/api/v1/audit?*')
  const retry = page.getByRole('button', { name: 'Retry', exact: true }); await retry.focus(); await page.keyboard.press('Enter')
  await expect(page.getByRole('table')).toBeVisible()
}

async function deniedAudit(page: Page) {
  const response = await page.request.get('/api/v1/audit')
  expect(response.status() === 403).toBe(true)
  const workspace = await (await page.request.get('/api/v1/workspace')).json()
  expect(workspace.destinations.some((x: { key: string }) => x.key === 'audit')).toBe(false)
  await page.goto('/app/audit'); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'Audit permission is required.' })).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
}

export const auditCases = [
  { name: 'audit-history', run: auditHistory, restricted: false },
  { name: 'audit-default-deny', run: deniedAudit, restricted: true },
] as const
