import { expect, type Page } from '@playwright/test'

// AUTHORED / DEFERRED. The existing real signed-provider HTTPS host must provision
// the disposable writer's Tug mode and scoped ordinary write capabilities.
// All product requests use the actual API and its CSRF/idempotency contract.
async function currentTugToday(page: Page) {
  const contextResponse = await page.request.get('/api/v1/tug-command')
  expect(contextResponse.status()).toBe(200)
  const context = await contextResponse.json()
  expect(context.vessels.length).toBeGreaterThan(0)
  const vessel = context.vessels[0].id as string
  const session = await (await page.request.get('/api/v1/auth/session')).json()
  expect(typeof session.csrfToken).toBe('string')
  const id = crypto.randomUUID()
  const create = await page.request.post(`/api/v1/tug-jobs?vessel=${vessel}`, {
    headers: { 'X-CSRF-TOKEN': session.csrfToken, 'Idempotency-Key': crypto.randomUUID() },
    data: { clientId: id, baseVersion: null, inputs: { assistedVessel: 'FICTIONAL Today draft ' + id } }
  })
  expect(create.status()).toBe(200)
  const original = await create.json()
  await page.goto(`/app/today/${vessel}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText(/Tug draft planning: Available/)).toBeVisible()
  const article = page.getByRole('article', { name: `Tug job ${id}`, exact: true })
  // The same server-owned mixed feed is genuinely paged. No route mocking or
  // direct DOM injection supplies the card; an excessive fixture fails explicitly.
  for (let index = 0; index < 100; index++) {
    await expect(page.getByRole('button', { name: 'Next alerts', exact: true })).toBeVisible()
    if (await article.count()) break
    const next = page.getByRole('button', { name: 'Next alerts', exact: true })
    await expect(next).toBeEnabled()
    await next.click()
  }
  await expect(article).toBeVisible()
  const why = article.getByRole('button', { name: 'Why?', exact: true })
  await why.focus(); await page.keyboard.press('Enter')
  await expect(article.getByText('No draft plan is stored.', { exact: true })).toBeVisible()
  await expect(article.getByText('The authorized Master decision remains separate.', { exact: true })).toBeVisible()
  const current = await page.request.get(`/api/v1/why-engine?vesselId=${vessel}&cardKey=tug-job:${id}`)
  expect(current.status()).toBe(200)
  const explanation = await current.json()
  expect(explanation.tug.requestRevision).toBe(original.revision)
  expect(explanation.tug.planRevision).toBeNull()
  expect(explanation.tug.evidence).toBeNull()
  expect(explanation.freshness).toBe('Unverified')
  const plan = await page.request.get(`/api/v1/tug-jobs/${id}/plan?vessel=${vessel}`)
  expect(plan.status()).toBe(200); expect((await plan.json()).state).toBe('Missing')
  await article.getByRole('button', { name: 'Review tug job', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/tug-job-detail/${id}\\?vessel=${vessel}$`))
  const unchanged = await page.request.get(`/api/v1/tug-jobs/${id}?vessel=${vessel}`)
  expect(unchanged.status()).toBe(200); expect((await unchanged.json()).revision).toBe(original.revision)
}

export const todayTugCases = [
  { name: 'today-tug-current-source-and-why', run: currentTugToday, restricted: false }
] as const
