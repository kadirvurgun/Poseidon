import { expect, type Page } from '@playwright/test'
import { workbook } from './import-cases.ts'

// Real signed HTTPS session/CSRF/API/PostgreSQL only. Operator must explicitly provision
// the synthetic identity tenant/field/purpose/key policy and trial-import capabilities.
// No cookie minting, auth bypass, fake provider, protected DOM/error artifacts or setup here.
async function context(page: Page) {
  const directory = await page.request.get('/api/v1/vessels?limit=25'); expect(directory.status()).toBe(200)
  const vessel = (await directory.json()).items[0].id as string
  const root = `/api/v1/vessels/${vessel}/crew`
  const roster = await (await page.request.get(root)).json()
  expect(roster.canWriteIdentity === true && !!roster.identityPolicy).toBe(true)
  const session = await (await page.request.get('/api/v1/auth/session')).json()
  return { vessel, root, policy: roster.identityPolicy, csrf: session.csrfToken as string }
}
async function create(page: Page) {
  const c = await context(page), id = crypto.randomUUID(), name = 'FICTIONAL-ID-' + crypto.randomUUID()
  const response = await page.request.post(c.root, { headers: { 'X-CSRF-TOKEN': c.csrf, 'Idempotency-Key': crypto.randomUUID() },
    data: { id, personId: null, baseVersion: null, fullName: name, employeeNo: '000001', positionId: null,
      from: null, to: null, contractEndDate: null, watchGroup: null, sourceObservedAt: null,
      identityPolicyVersion: c.policy.version, identityPurpose: c.policy.purpose } })
  expect(response.status()).toBe(200)
  return { ...c, id, name }
}
async function identityEditorHistoryRetry(page: Page) {
  const c = await create(page)
  await page.goto(`/app/crew-roster/${c.vessel}/crew-profile/${c.id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  expect((await page.getByRole('heading', { level: 2 }).allTextContents()).includes(c.name)).toBe(true)
  await page.getByRole('button', { name: 'Edit person', exact: true }).click()
  const amended = 'FICTIONAL-AMENDED-' + crypto.randomUUID()
  await page.getByLabel('Full name', { exact: true }).fill(amended)
  const saved = page.waitForResponse(r => r.url().endsWith(`/crew/${c.id}/person`) && r.request().method() === 'PUT')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  const response = await saved; expect(response.status()).toBe(200)
  const original = response.request()
  const replay = await page.request.put(`${c.root}/${c.id}/person`, { headers: { 'X-CSRF-TOKEN': c.csrf,
    'Idempotency-Key': original.headers()['idempotency-key'] }, data: original.postDataJSON() })
  expect(replay.status()).toBe(200); expect((await replay.json()).replayed === true).toBe(true)
  const stale = await page.request.put(`${c.root}/${c.id}/person`, { headers: { 'X-CSRF-TOKEN': c.csrf, 'Idempotency-Key': crypto.randomUUID() }, data: original.postDataJSON() })
  expect(stale.status()).toBe(409)
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  expect((await page.getByRole('heading', { level: 2 }).allTextContents()).includes(amended)).toBe(true)
  await page.locator('summary').filter({ hasText: /^Identity change history$/ }).click()
  await page.getByRole('button', { name: 'Identity action history', exact: true }).click()
  await expect(page.getByText('Historical private values are unavailable.', { exact: true })).toBeVisible()
  const history = await page.request.get(`${c.root}/${c.id}/person/history`); expect(history.status()).toBe(200)
  const text = await history.text(); expect(!text.includes(amended) && !text.includes('000001')).toBe(true)
}
async function existingPersonImportParity(page: Page) {
  const c = await create(page), profile = await (await page.request.get(`${c.root}/${c.id}`)).json()
  const headers = ['record_key', 'base_version', 'person_id', 'person_base_version', 'identity_policy_version', 'identity_purpose', 'full_name']
  const name = 'FICTIONAL-IMPORTED-' + crypto.randomUUID()
  await page.goto(`/app/excel-migration/${c.vessel}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('combobox', { name: 'Category', exact: true }).selectOption('crew')
  await page.getByLabel('Single-sheet XLSX source').setInputFiles({ name: 'synthetic-identity.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: workbook(headers, [c.id, String(profile.assignment.version), profile.assignment.personId, String(profile.personVersion), c.policy.version, c.policy.purpose, name]) })
  await page.getByRole('button', { name: 'Upload for mapping', exact: true }).click()
  const labels = ['Record key (UUID) (required)', 'Base version', 'Person (authorized context)', 'Person base version', 'Identity policy version', 'Identity purpose', 'Full name']
  for (const [i, label] of labels.entries()) await page.getByLabel(label, { exact: true }).selectOption(String(i))
  await page.getByRole('button', { name: 'Validate and preview', exact: true }).click()
  await expect(page.getByRole('table', { name: 'Source row outcomes' })).toBeVisible()
  const batch = new URL(page.url()).searchParams.get('batch')!
  const saved = page.waitForResponse(r => r.url().endsWith(`/imports/${batch}/commit`) && r.request().method() === 'POST')
  await page.getByRole('button', { name: 'Commit atomic trial import', exact: true }).click(); expect((await saved).status()).toBe(200)
  await page.goto(`/app/crew-roster/${c.vessel}/crew-profile/${c.id}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  expect((await page.getByRole('heading', { level: 2 }).allTextContents()).includes(name)).toBe(true)
  const current = await (await page.request.get(`${c.root}/${c.id}`)).json()
  expect(current.assignment.employeeNo === '000001' && current.assignment.assignmentStatus === 'Draft').toBe(true)
}
export const crewIdentityCases = [
  { name: 'crew-protected-identity-editor-history-exact-retry', run: identityEditorHistoryRetry, restricted: false },
  { name: 'crew-existing-person-import-ordinary-parity', run: existingPersonImportParity, restricted: false },
] as const
