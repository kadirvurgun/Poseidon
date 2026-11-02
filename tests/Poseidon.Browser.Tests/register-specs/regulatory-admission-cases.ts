import { expect, type Page } from '@playwright/test'

// Genuine signed-host gate source only. The disposable fixture must have been
// authored by a different synthetic human THROUGH the normal commands, and this
// signed human must have a real exact reviewer assignment. Missing input fails.
async function assignedQualification(page: Page) {
  const rules = await page.request.get('/api/v1/rules'); expect(rules.status()).toBe(200)
  const rows = (await rules.json()).items
  const selected = rows.find((r: { current: { rule?: { ruleId?: string } } }) => r.current.rule?.ruleId?.startsWith('SYNTHETIC-FOUNDATION-'))
  expect(!!selected).toBe(true)
  const source = await page.request.get(`/api/v1/regulatory-source-governance/${selected.current.id}`); expect(source.status()).toBe(200)
  const q = await source.json(); expect(q.items[0].state).toBe('IN_REVIEW'); expect(q.actions).toContain('qualify')
  const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200)
  const vessel = (await fleet.json()).items.find((v: { kind: string }) => v.kind === 'Ship'); expect(!!vessel).toBe(true)
  await page.goto('/app/sources'); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Lifecycle and applicability', exact: true }).click()
  const lifecycle = page.getByRole('region', { name: 'Lifecycle and applicability preparation', exact: true })
  await lifecycle.getByRole('button', { name: new RegExp(selected.current.id) }).first().click()
  const review = page.getByRole('region', { name: 'Source qualification and policy review', exact: true })
  await review.getByLabel('Record lifecycle state', { exact: true }).selectOption('IN_FORCE')
  const saved = page.waitForResponse(r => r.request().method() === 'POST' && r.url().includes(`/regulatory-source-governance/${selected.current.id}/events`))
  await review.getByRole('button', { name: 'Record assigned human qualification', exact: true }).click()
  expect((await saved).status()).toBe(200)
  await lifecycle.getByLabel('Vessel context', { exact: true }).selectOption(vessel.id)
  await lifecycle.getByRole('checkbox', { name: new RegExp(selected.current.id) }).check()
  const evaluated = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/applicability/evaluate'))
  await lifecycle.getByRole('button', { name: 'Evaluate qualified current sources', exact: true }).click()
  const response = await evaluated; expect(response.status()).toBe(200); const result = await response.json()
  expect(result.authoritative).toBe(true); expect(result.simulation).toBe(false)
  await lifecycle.getByRole('button', { name: 'Check current validity', exact: true }).click()
  await expect(lifecycle.getByText('Inputs remain current. This is not safety release.', { exact: true })).toBeVisible()
  await page.context().setOffline(true)
  try { await expect(lifecycle.getByRole('button', { name: 'Evaluate qualified current sources', exact: true })).toBeDisabled() }
  finally { await page.context().setOffline(false) }
}
async function deniedQualification(page: Page) {
  const response = await page.request.get('/api/v1/regulatory-source-governance/10000000-0000-4000-8000-000000000003')
  expect(response.status()).toBe(403)
  const body = await response.json(); expect(body.items).toBeUndefined(); expect(body.owners).toBeUndefined()
}
// Genuine signed-host ordinary profile capture. Requires a disposable scoped
// human editor; never changes account/authority configuration or qualifies a record.
async function owningProfileCapture(page: Page) {
  const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200)
  const vessel = (await fleet.json()).items.find((v: { kind: string; canEdit: boolean }) => v.kind === 'Ship' && v.canEdit)
  expect(!!vessel).toBe(true)
  await page.goto(`/app/fleet/${vessel.id}/profile`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const profile = page.getByRole('region', { name: 'Regulatory profile draft', exact: true })
  await profile.getByLabel('Main propulsion power (kW) — Data state', { exact: true }).selectOption('Known')
  await profile.getByLabel('Main propulsion power (kW)', { exact: true }).fill('3600.000001')
  await profile.getByLabel('Construction contract date (YYYY-MM-DD) — Data state', { exact: true }).selectOption('Unknown')
  const waiting = page.waitForResponse(r => r.request().method() === 'PUT' && r.url().endsWith(`/vessels/${vessel.id}/regulatory-profile`))
  await profile.getByRole('button', { name: 'Save draft', exact: true }).click()
  const saved = await waiting; expect(saved.status()).toBe(200)
  const result = await saved.json(); expect(result.content.mainPropulsionKw).toEqual({ state: 'Known', value: '3600.000001' })
  expect(result.content.contractForConstructionDate).toEqual({ state: 'Unknown', value: null })
  await page.reload()
  await expect(profile.getByLabel('Main propulsion power (kW)', { exact: true })).toHaveValue('3600.000001')
  await expect(profile.getByLabel('Construction contract date (YYYY-MM-DD) — Data state', { exact: true })).toHaveValue('Unknown')
  const history = await page.request.get(`/api/v1/vessels/${vessel.id}/regulatory-profile/history?offset=0&limit=25`)
  expect(history.status()).toBe(200)
  expect((await history.json()).items[0].content.mainPropulsionKw.value).toBe('3600.000001')
}
export const regulatoryAdmissionCases = [
  { name: 'owning-power-and-independent-construction-date-draft-signed-host', run: owningProfileCapture, restricted: false },
  { name: 'synthetic-assigned-human-source-qualification-current-evaluation-signed-host', run: assignedQualification, restricted: false },
  { name: 'source-qualification-denied-before-lineage-or-owner-disclosure', run: deniedQualification, restricted: true },
] as const
