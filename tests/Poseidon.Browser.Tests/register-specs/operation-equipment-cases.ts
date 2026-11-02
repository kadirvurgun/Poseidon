import { expect, type Page } from '@playwright/test'

// Registered genuine signed-host source. Coordinator provisions explicit current
// evidence/equipment/human-review rights. No fake provider, source approval or routes.
async function incompleteCargo(page: Page) {
  await page.goto('/app/source-register?lang=en')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click()
  const region = page.getByRole('region', { name: 'Controls and evidence', exact: true })
  const response = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(response.status()).toBe(200)
  const vessel = (await response.json()).items.find((v: { kind: string }) => v.kind === 'Ship'); expect(!!vessel).toBe(true)
  await region.getByLabel('Vessel context', { exact: true }).selectOption(vessel.id)
  await region.getByLabel('Register', { exact: true }).selectOption('Evidence')
  await region.getByLabel('Title', { exact: true }).fill('FICTIONAL cargo owning record; no qualification')
  await region.getByLabel('Document purpose', { exact: true }).selectOption('cargoPlan')
  await expect(region.getByLabel('Packaged dangerous goods declaration', { exact: true })).toHaveValue('')
  const [saved] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/evidence' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Save incomplete draft', exact: true }).click()])
  expect(saved.status()).toBe(200); const first = await saved.json()
  expect(first.content.cargoPlan.packagedDangerousGoods).toBe(null); expect(first.lifecycle).toBe('DRAFT'); expect(first.verification).toBe('UNVERIFIED')
  const history = await page.request.get(`/api/v1/evidence/${first.id}/versions?vesselId=${vessel.id}`); expect(history.status()).toBe(200)
  expect((await history.json()).items[0].content.cargoPlan.packagedDangerousGoods).toBe(null)
  await expect(region.getByRole('region', { name: 'Immutable history', exact: true })).toBeVisible()
}
async function qualifiedConsumer(page: Page, mode: 'Ship' | 'Tug') {
  // The gate must command-author and independently review these FICTIONAL records
  // first. Missing fixtures fail; this case never inserts verified rows or issues
  // actual service authorization. This signed actor consumes references only.
  const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200)
  const vessel = (await fleet.json()).items.find((v: { kind: string }) => v.kind === mode); expect(!!vessel).toBe(true)
  const response = await page.request.get(`/api/v1/evidence?vesselId=${vessel.id}&offset=0`); expect(response.status()).toBe(200)
  const rows = (await response.json()).items.filter((r: { content: { title?: string }; verification: string }) => r.content.title?.startsWith('FICTIONAL signed-host owning ') && r.verification === 'VERIFIED')
  const fields = mode === 'Ship' ? ['cargoPlan'] : ['operation', 'winchFitment']
  for (const field of fields) expect(rows.filter((r: { content: Record<string, unknown> }) => r.content[field]).length).toBe(1)
  const rules = await page.request.get('/api/v1/rules'); expect(rules.status()).toBe(200)
  const rule = (await rules.json()).items.find((r: { current: { rule: { ruleId: string } } }) => r.current.rule?.ruleId === `SYNTHETIC-FOUNDATION-OWNING-${mode.toUpperCase()}`); expect(!!rule).toBe(true)
  await page.goto('/app/source-register?lang=en')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Lifecycle and applicability', exact: true }).click()
  const region = page.getByRole('region', { name: 'Lifecycle and applicability preparation', exact: true })
  await region.getByLabel('Vessel context', { exact: true }).selectOption(vessel.id)
  await region.getByRole('checkbox', { name: new RegExp(rule.current.id) }).check()
  const selector = region.getByRole('group', { name: 'Owning input references', exact: true })
  for (const field of fields) {
    const row = rows.find((r: { content: Record<string, unknown> }) => r.content[field])
    await selector.getByLabel(field === 'cargoPlan' ? 'Cargo plan' : field === 'operation' ? 'Operation' : 'Winch fitment', { exact: true }).selectOption(row.id)
  }
  const [evaluated] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/applicability/evaluate' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Evaluate qualified current sources', exact: true }).click()])
  expect(evaluated.status()).toBe(200); const value = await evaluated.json()
  expect(value.authoritative).toBe(true); expect(value.results[0].prediction.result).toBe('APPLICABLE')
  const facts = value.snapshot.qualifications[0].facts
  for (const field of mode === 'Ship' ? ['cargo.packagedDangerousGoods'] : ['vessel.operatingArea', 'operation.jobType', 'operation.profile', 'operation.serviceContext', 'equipment.towingWinchFitted']) {
    expect(facts[field].knowledge).toBe('Known'); expect(facts[field].provenance).toBe('QualifiedOwningDocument')
  }
  const current = await page.request.get(`/api/v1/applicability/evaluations/${value.id}/current`); expect(current.status()).toBe(200); expect((await current.json()).current).toBe(true)
}
export const operationEquipmentCases = [
  { name: 'operation-equipment-incomplete-cargo-unknown-current-rights-history', run: incompleteCargo, restricted: false },
  { name: 'operation-equipment-qualified-ship-dg-command-fixture-consumer', run: (page: Page) => qualifiedConsumer(page, 'Ship'), restricted: false },
  { name: 'operation-equipment-qualified-tug-operation-winch-command-fixture-consumer', run: (page: Page) => qualifiedConsumer(page, 'Tug'), restricted: false },
] as const
