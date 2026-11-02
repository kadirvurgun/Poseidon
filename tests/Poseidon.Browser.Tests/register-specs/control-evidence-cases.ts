import { expect, type Page } from '@playwright/test'

// Genuine signed host manifest; no route.fulfill, test login or fake success.
// Coordinator must provision explicit graph/file permissions and real owned transport.
async function writer(page: Page) {
  await page.goto('/app/source-register?lang=en')
  await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click()
  const region = page.getByRole('region', { name: 'Controls and evidence', exact: true })
  const response = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(response.status()).toBe(200)
  const vessel = (await response.json()).items.find((v: { kind: string }) => v.kind === 'Ship'); expect(!!vessel).toBe(true)
  await region.getByLabel('Vessel context', { exact: true }).selectOption(vessel.id)
  await expect(region.getByText('Draft authoring available', { exact: true })).toBeVisible()
  await region.getByLabel('Title', { exact: true }).fill('FICTIONAL CONTROL-EVIDENCE signed-host draft')
  await region.getByLabel('Criticality', { exact: true }).selectOption('true')
  const [saved] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/controls' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Save incomplete draft', exact: true }).click()])
  expect(saved.status()).toBe(200); const first = await saved.json()
  expect(first.lifecycle === 'DRAFT' && first.verification === 'UNVERIFIED' && first.effectiveness === 'UNKNOWN' && !first.evidenceExists).toBe(true)
  await expect(region.getByText('Draft saved; verification and effectiveness remain unmet.', { exact: true })).toBeVisible()
  await expect(region.getByRole('region', { name: 'Immutable history', exact: true })).toBeVisible()
  const report = await page.request.get(`/api/v1/controls/orphans?vesselId=${vessel.id}&offset=0`); expect(report.status()).toBe(200)
  const value = await report.json(); expect(value.available === false && value.reason === 'AUTHORITATIVE_APPLICABILITY_UNAVAILABLE').toBe(true)
  await region.getByLabel('Title', { exact: true }).fill('retain this dirty draft')
  await region.getByLabel('Register', { exact: true }).selectOption('Evidence')
  await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(region.getByLabel('Title', { exact: true })).toHaveValue('retain this dirty draft')
  await region.getByLabel('Title', { exact: true }).focus(); await expect(region.getByLabel('Title', { exact: true })).toBeFocused()
}
async function restricted(page: Page) {
  const v = '10000000-0000-4000-8000-000000000001'
  for (const path of ['controls', 'evidence', 'controls/owners', 'controls/orphans', 'evidence/10000000-0000-4000-8000-000000000002/versions'])
    expect((await page.request.get(`/api/v1/${path}?vesselId=${v}`)).status()).toBe(403)
}
export const controlEvidenceCases = [
  { name: 'control-evidence-draft-history-unavailable-report-dirty-guard', run: writer, restricted: false },
  { name: 'control-evidence-denied-before-graph-file-owners-counts', run: restricted, restricted: true },
] as const
