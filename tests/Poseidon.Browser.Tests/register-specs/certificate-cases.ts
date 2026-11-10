import { expect, type Page } from '@playwright/test'
import { certificateFixture, certificateLane, certificateSession, prepareCertificate } from './certificate-fixture.ts'

// Source-only genuine disposable-host cases. Real signed principal and scoped
// clean synthetic files are prerequisites. No actor injection, mocked business
// result, invented owner or external activation. Missing setup fails, not skips.
async function declarationExactReceipt(page: Page) {
  const fixture = certificateFixture(), lane = certificateLane(page), { id, base, row } = await prepareCertificate(page, fixture, lane, 'declaration')
  await page.goto('/app/certificate-command-center/' + fixture.vesselId + '?id=' + id)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const form = page.getByRole('form', { name: 'Certificate declaration form', exact: true })
  await expect(form).toBeVisible()
  await expect(form.getByLabel('Issue date', { exact: true })).toHaveValue('')
  await expect(form.getByLabel('Expiry date', { exact: true })).toHaveValue('')
  await form.getByLabel('Declared issuer', { exact: true }).fill('SYNTHETIC UNVERIFIED ISSUER')
  await form.getByLabel('Declared renewal lead days', { exact: true }).fill('9007199254740993.123456789')
  let originalBody = '', originalKey = '', receiptText = '', writes = 0
  page.on('request', request => { if (request.url().endsWith('/certificates/' + id) && request.method() === 'PUT') writes++ })
  await page.route('**/certificates/' + id, async route => {
    if (route.request().method() !== 'PUT') { await route.continue(); return }
    originalBody = route.request().postData() ?? ''; originalKey = route.request().headers()['idempotency-key'] ?? ''
    const actual = await route.fetch(); expect(actual.status()).toBe(200); receiptText = await actual.text()
    // Forward the genuine command, then lose only its response transport.
    await route.abort('connectionfailed'); await page.unroute('**/certificates/' + id)
  })
  await form.getByRole('button', { name: 'Save unverified draft', exact: true }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'Outcome unknown.' })).toBeVisible()
  expect(writes).toBe(1)
  expect(JSON.parse(originalBody)).toMatchObject({ id, expectedVersion: row.version, content: { metadata: { declaration: { issueDate: null, expiryDate: null }, renewalLeadDaysNumeric: '9007199254740993.123456789' } } })
  await expect(form.getByLabel('Declared issuer', { exact: true })).toBeDisabled()
  const replay = page.waitForResponse(response => response.url().endsWith('/certificates/' + id) && response.request().method() === 'PUT')
  await form.getByRole('button', { name: 'Retry exact certificate command', exact: true }).click()
  const outcome = await replay; expect(outcome.status()).toBe(200)
  expect(outcome.request().postData()).toBe(originalBody); expect(outcome.request().headers()['idempotency-key']).toBe(originalKey); expect(await outcome.text()).toBe(receiptText)
  const saved = await outcome.json()
  expect(saved.currentRevision === false && saved.canEdit === false && saved.qualification === 'REVIEW_REQUIRED' && saved.calendar.state === 'REVIEW_REQUIRED').toBe(true)
  await expect(form.getByLabel('Declared issuer', { exact: true })).toBeEnabled()
  const currentRead = await page.request.get(base + '/' + id); expect(currentRead.status()).toBe(200)
  const current = await currentRead.json()
  expect(current.currentRevision === true && current.revision === 2 && current.content.metadata.declaration.issuer === 'SYNTHETIC UNVERIFIED ISSUER').toBe(true)
  const historyRead = await page.request.get(base + '/' + id + '/history'); expect(historyRead.status()).toBe(200)
  const history = await historyRead.json(); expect(history.items).toHaveLength(2)
  expect(new Set(history.items.map((item: { revision: number }) => item.revision)).size).toBe(2); expect(writes).toBe(2)
  const geometry = await form.getByRole('group', { name: 'Certificate form actions', exact: true }).getByRole('button').evaluateAll(buttons => buttons.map(button => { const r = button.getBoundingClientRect(); return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, height: r.height } }))
  expect(geometry).toHaveLength(2)
  const viewport = page.viewportSize()!
  for (const button of geometry) expect(button.left >= 0 && button.right <= viewport.width && button.height >= 24).toBe(true)
  expect(geometry[0].right <= geometry[1].left || geometry[0].bottom <= geometry[1].top || geometry[1].right <= geometry[0].left || geometry[1].bottom <= geometry[0].top).toBe(true)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
}

async function renewalRetainsOriginal(page: Page) {
  const fixture = certificateFixture(), lane = certificateLane(page)
  const original = await prepareCertificate(page, fixture, lane, 'renewal-original'), candidate = await prepareCertificate(page, fixture, lane, 'renewal-candidate')
  const capabilities = await page.request.get('/api/v1/renewal-workflow/capabilities?vesselId=' + fixture.vesselId); expect(capabilities.status()).toBe(200)
  const cap = await capabilities.json(); expect(cap.canWrite).toBe(true)
  if (fixture.ownerRoleId === null) expect(cap.ownerOptions).toHaveLength(0)
  else expect(cap.ownerOptions.some((option: { roleId: string }) => option.roleId === fixture.ownerRoleId)).toBe(true)
  await page.goto('/app/certificate-command-center/' + fixture.vesselId + '?id=' + original.id)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Prepare renewal', exact: true }).click()
  const form = page.getByRole('form', { name: 'Certificate renewal form', exact: true }); await expect(form).toBeVisible()
  await expect(form.getByLabel('Survey required declaration', { exact: true })).toHaveValue('')
  await expect(form.getByLabel('Declared replacement issue date', { exact: true })).toHaveValue('')
  await form.getByLabel('Declared renewal due date', { exact: true }).fill('2027-01-15')
  await form.getByLabel('Declared renewal survey window start', { exact: true }).fill('2027-01-01')
  await form.getByLabel('Declared renewal survey window end (source text)', { exact: true }).fill('source window requires review')
  if (fixture.ownerRoleId) await form.getByLabel('Assigned renewal owner role', { exact: true }).selectOption(fixture.ownerRoleId)
  const replacement = form.getByLabel('Declared replacement certificate', { exact: true })
  for (let pages = 0; pages < 32 && await replacement.locator('option[value="' + candidate.id + '"]').count() === 0; pages++) {
    const next = form.getByRole('button', { name: 'More candidates', exact: true }); await expect(next).toBeEnabled(); await next.click()
    await expect(form.getByText('Loading authorized candidates…', { exact: true })).not.toBeVisible()
  }
  await replacement.selectOption(candidate.id)
  await expect(form.getByLabel('Declared replacement issue date', { exact: true })).toHaveValue('')
  await expect(form.getByLabel('Declared replacement expiry date', { exact: true })).toHaveValue('')
  const saving = page.waitForResponse(response => response.url().includes('/api/v1/renewal-workflow?vesselId=') && response.request().method() === 'POST')
  await form.getByRole('button', { name: 'Save unverified draft', exact: true }).click()
  const response = await saving; expect(response.status()).toBe(200); const saved = await response.json()
  expect(saved.state === 'DRAFT' && saved.qualification === 'REVIEW_REQUIRED' && saved.currentRevision === false && saved.canComplete === false).toBe(true)
  expect(saved.certificateId === original.id && saved.certificateRevision === original.row.revision && saved.certificateVersion === original.row.version).toBe(true)
  expect(saved.content).toMatchObject({ ownerRoleId: fixture.ownerRoleId, dueDate: '2027-01-15', surveyWindowStart: '2027-01-01', surveyWindowEnd: 'source window requires review', requiredSurvey: null, requiredDocuments: null, requiredActions: null, newCertificateId: candidate.id, newCertificateRevision: candidate.row.revision, newCertificateVersion: candidate.row.version, newIssueDate: null, newExpiryDate: null })
  const stillOriginal = await page.request.get(original.base + '/' + original.id); expect(stillOriginal.status()).toBe(200)
  const unchanged = await stillOriginal.json(); expect(unchanged.version === original.row.version && unchanged.state === 'DRAFT' && unchanged.supersededBy === null && unchanged.currentRevision === true).toBe(true)
  const renewalHistory = await page.request.get('/api/v1/renewal-workflow/' + saved.id + '/history?vesselId=' + fixture.vesselId); expect(renewalHistory.status()).toBe(200)
  expect((await renewalHistory.json()).items).toHaveLength(1)
  await certificateSession(page, fixture)
  await expect(page.getByRole('button', { name: 'Controlled renewal completion unavailable', exact: true })).toBeDisabled()
}
export const certificateCases = [
  { name: 'certificate-signed-incomplete-draft-loss-exact-receipt', run: declarationExactReceipt, restricted: false },
  { name: 'certificate-signed-renewal-plan-retains-original', run: renewalRetainsOriginal, restricted: false },
] as const
