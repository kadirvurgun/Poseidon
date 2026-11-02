import { expect, type Page } from '@playwright/test'

// LA-08/10; genuine signed host, actual PostgreSQL/API. No route mocks or login
// fabrication. Explicit synthetic writer capabilities must be provisioned at the
// later REGULATORY-CHAIN-BUNDLE-01 gate; absence fails, never skips/passes.
async function writer(page: Page) {
  const capabilities = await page.request.get('/api/v1/rules/capabilities'); expect(capabilities.status()).toBe(200)
  const c = await capabilities.json(); expect(c.canRead && c.canWrite && c.canReview && c.canEvaluate && c.canActivate === false).toBe(true)
  await page.goto('/app/sources'); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Lifecycle and applicability', exact: true }).click()
  const region = page.getByRole('region', { name: 'Lifecycle and applicability preparation', exact: true })
  const [saved] = await Promise.all([
    page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/rules' && r.request().method() === 'POST'),
    region.getByRole('button', { name: 'Save draft revision', exact: true }).click(),
  ])
  expect(saved.status()).toBe(200); const first = await saved.json(); expect(first.current.rule === null && first.current.lifecycle === 'DRAFT').toBe(true)
  const rule = {
    ruleId: 'FICTIONAL-BROWSER-' + crypto.randomUUID(), version: 2,
    source: { sourceId: 'UNADMITTED-SYNTHETIC', locator: 'synthetic/authoring-only', authorityLayer: 'PRODUCT_CONTROL' },
    lifecycle: { state: 'IN_FORCE', effectiveFrom: '2000-01-01' }, scope: {},
    when: { field: 'context.mode', operator: 'EXISTS' }, then: { result: 'APPLICABLE', activate: [{ type: 'CONTROL', id: 'FICTIONAL-ONLY' }] }, unknownPolicy: 'REVIEW_REQUIRED',
  }
  await region.getByLabel('Rule document', { exact: true }).fill(JSON.stringify(rule))
  await region.getByRole('button', { name: 'Save draft revision', exact: true }).click()
  await expect(region.getByText('Saved as unverified preparation. Current obligations remain unavailable.', { exact: true })).toBeVisible()
  await region.getByLabel('Requested lifecycle', { exact: true }).selectOption('IN_FORCE')
  await region.getByRole('button', { name: 'Record review request', exact: true }).click()
  await expect(region.getByText('Review request recorded: BLOCKED. Source admission and a qualified independent reviewer are pending.', { exact: true })).toBeVisible()
  const vesselResponse = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(vesselResponse.status()).toBe(200)
  const vessel = (await vesselResponse.json()).items.find((v: { kind: string }) => v.kind === 'Ship'); expect(!!vessel).toBe(true)
  await region.getByLabel('Vessel context', { exact: true }).selectOption(vessel.id)
  await region.getByRole('checkbox', { name: first.current.id, exact: true }).check()
  const [simulated] = await Promise.all([
    page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/applicability/simulate' && r.request().method() === 'POST'),
    region.getByRole('button', { name: 'Run preview without saving', exact: true }).click(),
  ])
  expect(simulated.status()).toBe(200); const preview = await simulated.json()
  expect(preview.preview === true && preview.authoritative === false && preview.currentResult === 'REVIEW_REQUIRED').toBe(true)
  await expect(region.getByRole('region', { name: 'Preview result', exact: true })).toBeVisible()
  expect((await page.request.get(`/api/v1/applicability/evaluations/${preview.id}`)).status()).toBe(404)
  await region.getByLabel('Rule document', { exact: true }).fill('retained unsaved authoring')
  await page.getByRole('button', { name: 'Back to register', exact: true }).click()
  const dialog = page.getByRole('dialog', { name: 'Discard the unsaved draft?', exact: true }); await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(region.getByLabel('Rule document', { exact: true })).toHaveValue('retained unsaved authoring')
  await region.getByLabel('Rule document', { exact: true }).focus(); await expect(region.getByLabel('Rule document', { exact: true })).toBeFocused()
}
async function restricted(page: Page) {
  const caps = await page.request.get('/api/v1/rules/capabilities'); expect(caps.status()).toBe(200)
  const value = await caps.json(); expect(!value.canRead && !value.canWrite && !value.canReview && !value.canEvaluate && !value.canActivate).toBe(true)
  expect((await page.request.get('/api/v1/rules')).status()).toBe(403)
  expect((await page.request.get('/api/v1/rules/10000000-0000-4000-8000-000000000001/versions')).status()).toBe(403)
  expect((await page.request.get('/api/v1/applicability/evaluations/10000000-0000-4000-8000-000000000002')).status()).toBe(403)
}
export const lifecycleApplicabilityCases = [
  { name: 'lifecycle-applicability-draft-review-nonmutating-preview-dirty-guard', run: writer, restricted: false },
  { name: 'lifecycle-applicability-restricted-no-detail-or-why', run: restricted, restricted: true },
] as const
