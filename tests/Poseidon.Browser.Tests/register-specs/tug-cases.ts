import { expect, type Page } from '@playwright/test'

// DEFERRED. Existing private HTTPS, strict TLS, signed-provider, isolated PostgreSQL host.
// Coordinator provisions the disposable writer's mode.tug/mode.cross/tug.read/tug.write
// and trusted RoleReferences. Restricted user has no Tug capability. No auth bypass here.
async function context(page: Page) {
  const response = await page.request.get('/api/v1/tug-command')
  expect(response.status()).toBe(200)
  const body = await response.json(); expect(body.vessels.length > 0).toBe(true)
  return body.vessels[0].id as string
}
async function intakeBoard(page: Page) {
  const vessel = await context(page)
  await page.goto(`/app/tug-job-intake?vessel=${vessel}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const label = 'FICTIONAL-TUG-' + crypto.randomUUID()
  await page.getByLabel('Assisted vessel', { exact: true }).fill(label)
  await page.getByLabel('Required BP (tonnes, exact decimal)').fill('45.01')
  await page.getByLabel('Requested start (UTC)').fill('2026-11-01T12:00:00')
  await page.getByRole('link', { name: 'Job board', exact: true }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Stay', exact: true }).click()
  await expect(page.getByLabel('Assisted vessel', { exact: true })).toHaveValue(label)
  const saved = page.waitForResponse(r => r.url().includes('/api/v1/tug-jobs?') && r.request().method() === 'POST')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  const response = await saved; expect(response.status()).toBe(200); const row = await response.json()
  expect(row.inputs.requiredBollardPull).toBe('45.01'); expect(row.inputs.requestedEnd).toBeNull()
  await expect(page).toHaveURL(new RegExp(`/tug-job-detail/${row.id}\\?vessel=${vessel}`))
  await page.getByRole('link', { name: 'Job board', exact: true }).click()
  await page.getByLabel('Search requests', { exact: true }).fill(label)
  await page.getByRole('button', { name: 'Apply filters', exact: true }).click()
  await expect(page.getByRole('link', { name: row.jobNumber, exact: true })).toBeVisible()
  const actual = await (await page.request.get(`/api/v1/tug-job-board?vessel=${vessel}&q=${encodeURIComponent(label)}`)).json()
  expect(actual.items.length).toBe(1); expect(actual.items[0].assignedTugs).toEqual([])
  expect(actual.items[0].readiness).toBe('Unknown'); expect(actual.items[0].availableBollardPull).toBeNull()
  expect(actual.items[0].windowState).toBe('EndNotSupplied')
  const jobCard = page.getByRole('article', { name: row.jobNumber, exact: true })
  await expect(jobCard).toContainText('45.01 t')
  await expect(jobCard).toContainText('Requested window only; no approved plan')
  await expect(jobCard.getByText('Ready', { exact: true })).toHaveCount(0)
  const why = jobCard.locator('summary').filter({ hasText: 'Why / next action' })
  await why.focus(); await page.keyboard.press('Enter')
  await expect(jobCard.getByRole('heading', { name: 'Recorded reasons', exact: true })).toBeVisible()
  const originalViewport = page.viewportSize()
  try {
    await page.setViewportSize({ width: 390, height: 844 })
    await expect(jobCard.getByRole('link', { name: 'Open job', exact: true })).toBeVisible()
    expect(await jobCard.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true)
  } finally { if (originalViewport) await page.setViewportSize(originalViewport) }
  await page.getByRole('link', { name: row.jobNumber, exact: true }).click()
  await page.goBack(); await expect(page.getByRole('heading', { name: 'Job board', exact: true })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
}
async function denied(page: Page) {
  const r = await page.request.get('/api/v1/tug-command', { headers: { 'X-Role': 'Tug Master' } })
  expect(r.status()).toBe(403); expect(await r.text()).not.toContain('vessels')
  await page.goto('/app/tug-command'); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText('Access is not available.', { exact: true })).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
  const workspace = await (await page.request.get('/api/v1/workspace')).json()
  expect(workspace.destinations.some((d: { key: string }) => d.key.startsWith('tug-'))).toBe(false)
}
async function lossReplay(page: Page) {
  // Controlled transport fault after a real commit, separately labeled from normal journeys.
  const vessel = await context(page), attempts: { body: string | null; key: string | undefined }[] = []; let lost = false
  await page.route('**/api/v1/tug-jobs?*', async route => {
    if (route.request().method() !== 'POST') return route.continue()
    attempts.push({ body: route.request().postData(), key: route.request().headers()['idempotency-key'] })
    if (!lost) { lost = true; const committed = await route.fetch(); expect(committed.status()).toBe(200); return route.abort('failed') }
    return route.continue()
  })
  await page.goto(`/app/tug-job-intake?vessel=${vessel}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByLabel('Assisted vessel', { exact: true }).fill('FICTIONAL response loss')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByText(/Save outcome uncertain/)).toBeVisible()
  await expect(page.getByLabel('Assisted vessel', { exact: true })).toBeDisabled()
  await page.getByRole('button', { name: 'Retry', exact: true }).click()
  await expect(page.getByText('Draft saved. Operational approval remains unavailable.', { exact: true })).toBeVisible()
  expect(attempts).toHaveLength(2); expect(attempts[0]).toEqual(attempts[1])
  const body = JSON.parse(attempts[0].body!)
  const r = await (await page.request.get(`/api/v1/tug-jobs/${body.clientId}?vessel=${vessel}`)).json()
  expect(r.revision).toBe(1)
}
async function planningJourney(page: Page) {
  // Requires explicitly provisioned synthetic TugPlanning scope/type/source contract and
  // current tug.plan.write + editable membership. Missing setup fails; never skip/fake it.
  const vessel = await context(page)
  await page.goto(`/app/tug-job-intake?vessel=${vessel}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByLabel('Assisted vessel', { exact: true }).fill('FICTIONAL planning ' + crypto.randomUUID())
  const created = page.waitForResponse(r => r.url().includes('/api/v1/tug-jobs?') && r.request().method() === 'POST')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click(); const job = await (await created).json()
  const optionsResponse = await page.request.get(`/api/v1/tug-jobs/${job.id}/plan/options?vessel=${vessel}`)
  expect(optionsResponse.status()).toBe(200); const options = await optionsResponse.json()
  expect(options.canWrite).toBe(true); expect(options.tugs.length).toBeGreaterThanOrEqual(2)
  const panel = page.getByRole('region', { name: 'Draft planning', exact: true })
  await panel.getByLabel('Planned start (UTC)', { exact: true }).fill('2026-11-02T12:00:00')
  await page.getByRole('link', { name: 'Job board', exact: true }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Stay', exact: true }).click()
  await panel.getByRole('button', { name: 'Save planning window', exact: true }).click()
  await expect(panel.getByText(/r1 ·/).first()).toBeVisible()
  await panel.getByRole('combobox', { name: 'Eligible tug', exact: true }).selectOption(options.tugs[0].id)
  await panel.getByRole('button', { name: 'Assign to draft', exact: true }).click()
  await expect(panel.getByText(/r2 ·/).first()).toBeVisible()
  const crewResponse = await page.request.get(`/api/v1/tug-jobs/${job.id}/plan?vessel=${vessel}`)
  expect(crewResponse.status()).toBe(200)
  const crewProjection = await crewResponse.json()
  expect(crewProjection.evidence.crew).toHaveLength(1)
  expect(crewProjection.evidence.crew[0].tugId).toBe(options.tugs[0].id)
  expect(crewProjection.evidence.crew[0].coverage).toBe('SourceQualifiedMinimumRolesOnly;OtherCrewGatesAndMasterDecisionSeparate')
  // This existing journey deliberately omits a plan end. It can never establish
  // minimum-role coverage, even if a separate provider fixture supplied documents.
  expect(['Unavailable', 'ReviewRequired']).toContain(crewProjection.evidence.crew[0].state)
  expect(crewProjection.evidence.crew[0].roles).toEqual([])
  expect(crewProjection.evidence.crew[0].msmdId).toBeNull()
  await expect(panel.getByRole('heading', { name: 'Source-qualified minimum crew roles', exact: true })).toBeVisible()
  await panel.getByRole('combobox', { name: 'Replacement tug', exact: true }).selectOption(options.tugs[1].id)
  await panel.getByRole('button', { name: 'Reassign in draft', exact: true }).click()
  await expect(panel.getByText(/r3 ·/).first()).toBeVisible()
  await panel.getByRole('button', { name: 'Review exact revision 2', exact: true }).click()
  await expect(panel.getByRole('article')).toContainText(options.tugs[0].id)
  await panel.getByRole('combobox', { name: 'Eligible tug', exact: true }).selectOption(options.tugs[1].id)
  await panel.getByRole('button', { name: 'Remove from draft', exact: true }).click()
  await expect(panel.getByText(/r4 ·/).first()).toBeVisible()
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const detail = await (await page.request.get(`/api/v1/tug-jobs/${job.id}/plan?vessel=${vessel}`)).json()
  expect(detail.plan.revision).toBe(4); expect(detail.plan.inputs.plannedEnd).toBeNull(); expect(detail.plan.inputs.assignedTugs).toEqual([])
  expect(detail.evidence.availableBollardPull).toBeNull(); expect(detail.evidence.readiness).toBe('ReviewRequired')
  const board = await (await page.request.get(`/api/v1/tug-job-board?vessel=${vessel}&q=${encodeURIComponent(job.jobNumber)}`)).json()
  expect(board.items[0].draftPlan.revision).toBe(4); expect(board.items[0].request.inputs.requestedStart).toBeNull()
  await page.getByRole('link', { name: 'Job board', exact: true }).click()
  await page.getByLabel('Search requests', { exact: true }).fill(job.jobNumber); await page.getByRole('button', { name: 'Apply filters', exact: true }).click()
  await expect(page.getByRole('article', { name: job.jobNumber, exact: true })).toContainText('Draft plan')
}
async function planningLoss(page: Page) {
  await planningJourney(page)
  const link = page.getByRole('link', { name: /^TUG-/ }).first(); await link.click()
  const panel = page.getByRole('region', { name: 'Draft planning', exact: true })
  const attempts: { body: string | null; key: string | undefined }[] = []; let lost = false
  await page.route('**/api/v1/tug-jobs/*/plan?*', async route => {
    if (route.request().method() !== 'PUT') return route.continue()
    attempts.push({ body: route.request().postData(), key: route.request().headers()['idempotency-key'] })
    if (!lost) { lost = true; const committed = await route.fetch(); expect(committed.status()).toBe(200); return route.abort('failed') }
    return route.continue()
  })
  await panel.getByRole('button', { name: 'Save planning window', exact: true }).click()
  await expect(panel.getByText('Planning outcome uncertain. Retry the retained command.')).toBeVisible()
  await panel.getByRole('button', { name: 'Retry exact planning command', exact: true }).click()
  await expect(panel.getByRole('button', { name: 'Save planning window', exact: true })).toBeEnabled()
  expect(attempts).toHaveLength(2); expect(attempts[0]).toEqual(attempts[1])
}
async function offlineWindowJourney(page: Page) {
  // Real signed session/device key/IDB/host/PG. The explicit FICTIONAL window policy
  // must be provisioned by the later gate; missing enrollment support is a failure.
  await planningJourney(page)
  await page.getByRole('link', { name: /^TUG-/ }).first().click()
  const panel = page.getByRole('region', { name: 'Offline tug draft window', exact: true })
  await panel.getByRole('button', { name: 'Authorize encrypted capture', exact: true }).click()
  await expect(panel.getByText('Enrolled encrypted device capture available')).toBeVisible()
  const enrollment = await panel.getByLabel('Recover enrollment').count() // selector is absent while the handle is active
  expect(enrollment).toBe(0)
  const start = new Date(Date.now() + 86400000).toISOString().slice(0, 19)
  await page.context().setOffline(true)
  await panel.getByLabel('Offline planned start (UTC)', { exact: true }).fill(start)
  await panel.getByLabel('Offline planned end (UTC)', { exact: true }).fill('')
  await panel.getByRole('button', { name: 'Capture encrypted draft', exact: true }).click()
  await expect(panel.getByText(/Encrypted capture; not committed/)).toBeVisible()
  await expect(panel.getByRole('button', { name: 'Replay exact capture', exact: true })).toBeDisabled()
  const url = page.url()
  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.getByLabel('Offline planned start (UTC)', { exact: true })).toHaveCount(0)
  expect(await page.locator('body').innerText()).not.toContain(start)
  await page.context().setOffline(false); await page.goto(url)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const recovered = page.getByRole('region', { name: 'Offline tug draft window', exact: true })
  await expect(recovered.getByLabel('Recover enrollment').getByRole('option')).toHaveCount(2)
  const old = await recovered.getByLabel('Recover enrollment').getByRole('option').nth(1).getAttribute('value')
  await recovered.getByLabel('Recover enrollment').selectOption(old!)
  await recovered.getByRole('button', { name: 'Authorize encrypted capture', exact: true }).click()
  await expect(recovered.getByText(/Encrypted capture; not committed/)).toBeVisible()
  const replay = page.waitForResponse(r => r.url().includes('/replay?vessel=') && r.request().method() === 'POST')
  await recovered.getByRole('button', { name: 'Replay exact capture', exact: true }).click()
  const receipt = await replay; expect(receipt.status()).toBe(200); const result = await receipt.json(); expect(result.result).toBe('Committed')
  const again = page.waitForResponse(r => r.url().includes('/replay?vessel=') && r.request().method() === 'POST')
  await recovered.getByRole('button', { name: 'Replay exact capture', exact: true }).click()
  expect(await (await again).json()).toEqual(result)
  await recovered.getByRole('button', { name: 'Revoke device capture', exact: true }).click()
  await expect(recovered.getByLabel('Offline planned start (UTC)', { exact: true })).toHaveCount(0)
}
export const tugCases = [
  { name: 'tug-intake-board-exact-bp-null-end-dirty-url-responsive', run: intakeBoard, restricted: false },
  { name: 'tug-restricted-direct-url-and-navigation', run: denied, restricted: true },
  { name: 'tug-controlled-response-loss-real-commit', run: lossReplay, restricted: false },
  { name: 'tug-versioned-draft-plan-assign-reassign-remove-exact-history-board', run: planningJourney, restricted: false },
  { name: 'tug-plan-controlled-response-loss-real-commit-recovery', run: planningLoss, restricted: false },
  { name: 'tug-enrolled-encrypted-window-offline-restart-current-renew-replay-revoke', run: offlineWindowJourney, restricted: false },
] as const
