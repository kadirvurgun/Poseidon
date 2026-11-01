import { test, expect } from '@playwright/test'

// REG-SOURCE-DRAFT-01. Real HTTP/host only. No response fixtures or fake login. AUTHORED / NOT RUN.
test('source draft, explicit states, dirty navigation, immutable history and exact requirement citation', async ({ page }) => {
  await page.goto('/app/sources'); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const capability = await (await page.request.get('/api/v1/workspace')).json()
  expect(capability.canWriteRegulatory === true).toBe(true)
  await page.getByRole('button', { name: 'New draft', exact: true }).click()
  await page.getByLabel('Title · State', { exact: true }).selectOption('Known')
  const label = 'FICTIONAL-REG-' + crypto.randomUUID()
  await page.getByLabel('Title', { exact: true }).fill(label)
  await page.getByLabel('Declared authority · State', { exact: true }).selectOption('Unknown')
  await page.getByLabel('Source observed at (with UTC offset) · State', { exact: true }).selectOption('Unknown')
  await page.getByRole('button', { name: 'Requirement drafts', exact: true }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page.getByLabel('Title', { exact: true })).toHaveValue(label)
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByText('Draft saved. Unverified.', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Open saved draft and history', exact: true }).click()
  const sourceId = new URL(page.url()).pathname.split('/').at(-1)!
  const before = await (await page.request.get(`/api/v1/source-drafts/${sourceId}`)).json()
  expect(before.current.lifecycle === 'DRAFT').toBe(true)
  expect(before.current.content.authority.state === 'Unknown').toBe(true)
  await page.getByLabel('Title', { exact: true }).fill(label + '-edited')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByText('Draft saved. Unverified.', { exact: true })).toBeVisible()
  const history = await (await page.request.get(`/api/v1/source-drafts/${sourceId}/history`)).json()
  expect(history.items.length === 2 && history.items[1].content.title.value === label).toBe(true)
  await page.getByRole('button', { name: 'Requirement drafts', exact: true }).click()
  await page.getByRole('button', { name: 'New draft', exact: true }).click()
  await page.getByLabel('Find a source', { exact: true }).fill(label)
  await page.getByRole('button', { name: 'Search sources', exact: true }).click()
  await page.getByLabel('Source draft', { exact: true }).selectOption(sourceId)
  await page.getByLabel('Exact revision', { exact: true }).selectOption(String(before.current.revision))
  await page.getByLabel('Instrument · State', { exact: true }).selectOption('Unknown')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByText('Draft saved. Unverified.', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Open saved draft and history', exact: true }).click()
  const requirementId = new URL(page.url()).pathname.split('/').at(-1)!
  const requirement = await (await page.request.get(`/api/v1/requirement-drafts/${requirementId}`)).json()
  expect(requirement.current.sourceId === sourceId && requirement.current.sourceRevision === before.current.revision).toBe(true)
  expect(requirement.current.lifecycle === 'DRAFT' && requirement.current.provenance === 'DeclaredUnverified').toBe(true)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
})

test('response loss after real commit retries frozen bytes and a concurrent edit produces a retained conflict', async ({ page }) => {
  await page.goto('/app/sources'); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'New draft', exact: true }).click()
  // A network fault only: the intercepted command actually reaches the real API and commits.
  // No fabricated business response or runtime mock is returned.
  let committedId = ''; let firstBody = ''; let firstKey = ''; let retryMatches = false
  await page.route('**/api/v1/source-drafts', async route => {
    if (route.request().method() !== 'POST') { await route.continue(); return }
    if (!firstBody) {
      firstBody = route.request().postData()!; firstKey = route.request().headers()['idempotency-key']
      const actual = await route.fetch(); expect(actual.status()).toBe(200)
      committedId = (await actual.json()).current.id
      await route.abort('connectionreset')
    } else {
      retryMatches = route.request().postData() === firstBody && route.request().headers()['idempotency-key'] === firstKey
      await route.continue()
    }
  })
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Retry frozen command', exact: true })).toBeVisible()
  await expect(page.getByLabel('Title · State', { exact: true })).toBeDisabled()
  await page.getByRole('button', { name: 'Retry frozen command', exact: true }).click()
  await expect(page.getByText('Draft saved. Unverified.', { exact: true })).toBeVisible(); expect(retryMatches).toBe(true)
  expect((await (await page.request.get(`/api/v1/source-drafts/${committedId}/history`)).json()).items.length === 1).toBe(true)
  await page.getByRole('button', { name: 'Open saved draft and history', exact: true }).click()
  const old = await (await page.request.get(`/api/v1/source-drafts/${committedId}`)).json()
  await page.getByLabel('Title · State', { exact: true }).selectOption('Known')
  await page.getByLabel('Title', { exact: true }).fill('Retained local declaration')
  const session = await (await page.request.get('/api/v1/auth/session')).json()
  const concurrent = await page.request.put(`/api/v1/source-drafts/${committedId}`, { headers: { 'X-CSRF-TOKEN': session.csrfToken, 'Idempotency-Key': crypto.randomUUID() }, data: { expectedVersion: old.version, content: old.current.content } })
  expect(concurrent.status()).toBe(200)
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Reload saved draft', exact: true })).toBeVisible()
  await expect(page.getByLabel('Title', { exact: true })).toHaveValue('Retained local declaration')
  await expect(page.getByRole('button', { name: 'Save draft', exact: true })).toBeDisabled()
})

test.describe('restricted real server membership', () => {
  test.use({ storageState: process.env.POSEIDON_REGISTER_RESTRICTED_STATE })
  test('vessel user sees no source metadata or regulatory destinations', async ({ page }) => {
    await page.goto('/app/today'); await page.getByRole('button', { name: 'EN', exact: true }).click()
    expect((await page.request.get('/api/v1/source-drafts')).status()).toBe(403)
    expect((await page.request.get('/api/v1/requirement-drafts')).status()).toBe(403)
    const workspace = await (await page.request.get('/api/v1/workspace')).json()
    expect(workspace.destinations.some((v: { key: string }) => ['sources', 'requirements'].includes(v.key))).toBe(false)
    await expect(page.getByRole('button', { name: 'Source drafts', exact: true })).toHaveCount(0)
    expect((await page.request.post('/api/v1/source-drafts', { data: { expectedVersion: 0, content: {} } })).status()).toBe(403)
  })
})
