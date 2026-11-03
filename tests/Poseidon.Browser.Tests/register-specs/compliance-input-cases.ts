import { expect, type Page } from '@playwright/test'

async function incompleteClassRecord(page: Page) {
  await page.goto('/app/sources')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click()
  const region = page.getByRole('region', { name: 'Controls and evidence', exact: true })
  const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200)
  const vessel = (await fleet.json()).items.find((v: { kind: string }) => v.kind === 'Ship'); expect(!!vessel).toBe(true)
  await region.getByRole('combobox', { name: 'Vessel context', exact: true }).selectOption(vessel.id)
  await region.getByRole('combobox', { name: 'Register', exact: true }).selectOption('Evidence')
  await region.getByLabel('Title', { exact: true }).fill('FICTIONAL incomplete class pack; no adoption')
  await region.getByRole('combobox', { name: 'Compliance record purpose', exact: true }).selectOption('classPack')
  await expect(region.getByRole('combobox', { name: 'M79 statement in source', exact: true })).toHaveValue('')
  const [response] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/evidence' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Save incomplete draft', exact: true }).click()])
  expect(response.status()).toBe(200); const row = await response.json()
  expect(row.content.classPack.iacsM79).toBe(null); expect(row.lifecycle).toBe('DRAFT'); expect(row.verification).toBe('UNVERIFIED')
  expect(row.blockers).toContain('OWNING_INPUT_INCOMPLETE')
  const history = await page.request.get(`/api/v1/evidence/${row.id}/versions?vesselId=${vessel.id}`); expect(history.status()).toBe(200)
  expect((await history.json()).items[0].content.classPack.iacsM79).toBe(null)
}

async function qualifiedComplianceConsumer(page: Page) {
  // The owned gate must provision the fictional source/evidence with ordinary
  // commands and separately signed reviewers. Missing setup fails, never skips.
  const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200)
  const vessel = (await fleet.json()).items.find((v: { kind: string }) => v.kind === 'Ship'); expect(!!vessel).toBe(true)
  const evidence = await page.request.get(`/api/v1/evidence?vesselId=${vessel.id}&offset=0`); expect(evidence.status()).toBe(200)
  const rows = (await evidence.json()).items.filter((r: { content: { title?: string }; verification: string }) => r.content.title?.startsWith('FICTIONAL signed-host compliance ') && r.verification === 'VERIFIED')
  const pack = rows.filter((r: { content: { classPack?: unknown } }) => r.content.classPack), msmd = rows.filter((r: { content: { msmd?: unknown } }) => r.content.msmd)
  expect(pack.length).toBe(1); expect(msmd.length).toBe(1)
  const rules = await page.request.get('/api/v1/rules'); expect(rules.status()).toBe(200)
  const rule = (await rules.json()).items.find((r: { current: { rule: { ruleId: string } } }) => r.current.rule?.ruleId === 'SYNTHETIC-FOUNDATION-COMPLIANCE'); expect(!!rule).toBe(true)
  await page.goto('/app/sources')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Lifecycle and applicability', exact: true }).click()
  const region = page.getByRole('region', { name: 'Lifecycle and applicability preparation', exact: true })
  await region.getByRole('combobox', { name: 'Vessel context', exact: true }).selectOption(vessel.id)
  await region.getByRole('checkbox', { name: new RegExp(rule.current.id) }).check()
  await region.getByLabel('Class pack evidence reference', { exact: true }).fill(pack[0].id)
  await region.getByLabel('MSMD evidence reference', { exact: true }).fill(msmd[0].id)
  await region.getByLabel('Source evidence reference', { exact: true }).fill(pack[0].id)
  const [response] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/applicability/evaluate' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Evaluate qualified current sources', exact: true }).click()])
  expect(response.status()).toBe(200); const value = await response.json()
  for (const field of ['class.rulePack.IACS_M79', 'regulatory.msmdVerified', 'evidence.sourceVerified']) expect(value.snapshot.qualifications[0].facts[field].knowledge).toBe('Known')
  const current = await page.request.get(`/api/v1/applicability/evaluations/${value.id}/current`); expect(current.status()).toBe(200); expect((await current.json()).current).toBe(true)
}
export const complianceInputCases = [
  { name: 'compliance-class-incomplete-unknown-command-history', run: incompleteClassRecord, restricted: false },
  { name: 'compliance-qualified-class-msmd-source-command-fixture-consumer', run: qualifiedComplianceConsumer, restricted: false },
] as const
