import { expect, type Page } from '@playwright/test'

// Actual HTTPS host/session/PostgreSQL only, using the existing private safe runner.
// Combined-gate operator must provision disposable crew.read/write + identity read/write;
// restricted membership must have directory scope but no crew permission. No production appointments.
async function vesselFor(page: Page) {
  const response = await page.request.get('/api/v1/vessels?limit=25')
  expect(response.status() === 200).toBe(true)
  const vessels = await response.json(); expect(vessels.items.length > 0).toBe(true)
  return vessels.items[0].id as string
}
async function rosterCreateProfile(page: Page) {
  const vessel = await vesselFor(page)
  await page.goto(`/app/crew-roster/${vessel}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const permitted = await (await page.request.get(`/api/v1/vessels/${vessel}/crew`)).json()
  expect(permitted.canWrite === true && permitted.canWriteIdentity === true).toBe(true)
  await page.getByRole('button', { name: 'Add person and assignment', exact: true }).click()
  const label = 'FICTIONAL-CREW-' + crypto.randomUUID()
  await page.getByLabel('Full name', { exact: true }).fill(label)
  await page.getByLabel('Sign-on date', { exact: true }).fill('2026-11-01')
  await page.getByLabel('Sign-off date (exclusive)', { exact: true }).fill('2026-12-01')
  const saved = page.waitForResponse(r => r.url().endsWith(`/vessels/${vessel}/crew`) && r.request().method() === 'POST')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  const result = await saved; expect(result.status() === 200).toBe(true); const committed = await result.json()
  await expect(page.getByText('Draft saved.', { exact: true })).toBeVisible()
  await page.goto(`/app/crew-roster/${vessel}/crew-profile/${committed.id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByRole('heading', { name: label, exact: true })).toBeVisible()
  const profile = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/${committed.id}`)).json()
  expect(profile.assignment.assignmentStatus === 'Draft' && profile.assignment.eligibilityState === 'Unknown').toBe(true)
  await page.getByRole('button', { name: 'Edit assignment', exact: true }).click()
  await page.getByLabel('Watch group', { exact: true }).fill('FICTIONAL local group')
  await page.getByRole('link', { name: 'Back to roster', exact: true }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.getByRole('dialog').getByRole('button', { name: 'Cancel', exact: true }).click()
  await expect(page.getByLabel('Watch group', { exact: true })).toHaveValue('FICTIONAL local group')
  await page.getByRole('button', { name: 'Cancel', exact: true }).click()
}
async function materialConflict(page: Page) {
  const vessel = await vesselFor(page)
  const list = await (await page.request.get(`/api/v1/vessels/${vessel}/crew`)).json()
  expect(list.canWrite === true && list.items.length > 0).toBe(true); const row = list.items[0]
  await page.goto(`/app/crew-roster/${vessel}/crew-profile/${row.id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Edit assignment', exact: true }).click()
  await page.getByLabel('Watch group', { exact: true }).fill('FICTIONAL retained local edit')
  const identity = await (await page.request.get('/api/v1/auth/session')).json()
  const policy = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/position-policy`)).json()
  const concurrent = await page.request.post(`/api/v1/vessels/${vessel}/crew`, {
    headers: { 'X-CSRF-TOKEN': identity.csrfToken, 'Idempotency-Key': crypto.randomUUID() },
    data: { id: row.id, personId: row.personId, baseVersion: row.version, fullName: null, employeeNo: null,
      positionId: row.positionId, positionPolicyVersion: policy.version, from: row.from, to: row.to, contractEndDate: row.contractEndDate,
      watchGroup: 'FICTIONAL concurrent edit', sourceObservedAt: row.sourceObservedAt },
  })
  expect(concurrent.status() === 200).toBe(true)
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByText('Conflict. Reload the current assignment and resolve the changes.', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Save draft', exact: true })).toBeDisabled()
  await page.getByRole('button', { name: 'Discard and reload', exact: true }).click()
}
async function restrictedCrew(page: Page) {
  const vessel = await vesselFor(page)
  const denied = await page.request.get(`/api/v1/vessels/${vessel}/crew`, { headers: { 'X-Role': 'Crewing' } })
  expect(denied.status() === 403).toBe(true)
  const body = await denied.text(); expect(!body.includes('items') && !body.includes(vessel)).toBe(true)
  await page.goto(`/app/crew-roster/${vessel}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText('Crew access denied.', { exact: true })).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
}
// Disposable medical field policy + dedicated current membership capabilities must
// be deliberately configured at the later private-host gate. Absence FAILS.
async function medicalDeclaration(page: Page) {
  const vessel = await vesselFor(page)
  const rosterResponse = await page.request.get(`/api/v1/vessels/${vessel}/crew`)
  expect(rosterResponse.status()).toBe(200)
  const roster = await rosterResponse.json(); expect(roster.items.length > 0).toBe(true)
  const assignment = roster.items[0].id
  const path = `/api/v1/vessels/${vessel}/crew/${assignment}`
  const current = await page.request.get(`${path}/personal-profile?purpose=crew-profile`)
  expect(current.status()).toBe(200)
  const projection = await current.json()
  expect(projection.readable.includes(15) && projection.writable.includes(15)).toBe(true)
  await page.goto(`/app/crew-roster/${vessel}/crew-profile/${assignment}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByLabel('Declared medical expiry', { exact: true }).fill('2031-02-03')
  const saved = page.waitForResponse(r => r.url().endsWith(`${path}/personal-profile`) && r.request().method() === 'PUT')
  await page.getByRole('button', { name: 'Save declaration', exact: true }).click()
  expect((await saved).status()).toBe(200)
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByLabel('Declared medical expiry', { exact: true })).toHaveValue('2031-02-03')
  const profile = await (await page.request.get(path)).json()
  expect(profile.fitnessState === 'Unknown' && profile.assignment.eligibilityState === 'Unknown').toBe(true)
  const history = await page.request.get(`${path}/personal-profile/history`)
  expect(history.status()).toBe(200)
  expect(!(await history.text()).includes('2031-02-03')).toBe(true)
}
export const crewCases = [
  { name: 'crew-assignment-policy-history-and-owning-input-boundary', run: assignmentInputHistory, restricted: false },
  { name: 'crew-restricted-medical-declaration-reload-no-fitness-grant', run: medicalDeclaration, restricted: false },
  { name: 'crew-roster-profile-dirty', run: rosterCreateProfile, restricted: false },
  { name: 'crew-period-version-conflict', run: materialConflict, restricted: false },
  { name: 'crew-restricted-direct-url', run: restrictedCrew, restricted: true },
] as const

// Real signed HTTPS host and ordinary commands. No API routes mocked, no provider started.
async function assignmentInputHistory(page: Page) {
  const vessel = await vesselFor(page), id = crypto.randomUUID()
  const session = await (await page.request.get('/api/v1/auth/session')).json()
  const created = await page.request.post(`/api/v1/vessels/${vessel}/crew`, {
    headers: { 'X-CSRF-TOKEN': session.csrfToken, 'Idempotency-Key': crypto.randomUUID() },
    data: { id, personId: null, baseVersion: null, fullName: null, employeeNo: null, positionId: null,
      from: null, to: null, contractEndDate: null, watchGroup: null, sourceObservedAt: null },
  })
  expect(created.status()).toBe(200)
  await page.goto(`/app/crew-roster/${vessel}/crew-profile/${id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText('Incomplete draft', { exact: true })).toBeVisible()
  const history = page.getByRole('region', { name: 'Assignment history', exact: true })
  await expect(history.getByText('Recorded at', { exact: true })).toBeVisible()
  await expect(history.getByText('Historical policy unknown', { exact: true })).toHaveCount(2)
  const inputs = page.getByRole('region', { name: 'Crew input status', exact: true })
  await expect(inputs.getByText('Qualified evaluation unavailable; remains Unknown', { exact: true })).toHaveCount(4)
  const raw = await page.request.get(`/api/v1/vessels/${vessel}/crew/${id}/history`)
  expect(raw.status()).toBe(200); const revisions = await raw.json()
  expect(revisions.items.length).toBe(1); expect(revisions.items[0].status).toBe('Draft')
  expect(revisions.items[0].assignmentPolicyVersion).toBeNull()
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText('Incomplete draft', { exact: true })).toBeVisible()
}
