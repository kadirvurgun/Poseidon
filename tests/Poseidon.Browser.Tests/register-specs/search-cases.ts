import { expect, type Page } from '@playwright/test'

// Deferred real private HTTPS/signed-provider/PostgreSQL journeys. No fulfilled routes,
// fabricated authorization, skipped missing grants or authenticated DOM/trace artifacts.
async function openSearch(page: Page) {
  await page.goto('/app/fleet')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: /Search workspace/ }).click()
  return page.getByRole('dialog', { name: 'Search workspace', exact: true })
}

async function realDestinations(page: Page) {
  const cases = [
    { key: 'audit', href: '/app/audit', label: 'Action history' },
    { key: 'tug-command', href: '/app/tug-command', label: 'Tug operations' },
    { key: 'tug-job-board', href: '/app/tug-job-board', label: 'Tug job board' },
    { key: 'tug-job-intake', href: '/app/tug-job-intake', label: 'Tug job intake' },
  ]
  const search = await openSearch(page)
  for (const item of cases) {
    const reply = await page.request.get(`/api/v1/workspace/search?q=${item.key}&offset=0&limit=25`)
    expect(reply.status()).toBe(200)
    const actual = await reply.json()
    expect(actual.offset).toBe(0); expect(actual.limit).toBe(25)
    expect(actual.items.some((value: { key: string; href: string }) => value.key === item.key && value.href === item.href)).toBe(true)
    await search.getByRole('searchbox').fill(item.key)
    await search.getByRole('button', { name: 'Search', exact: true }).click()
    await expect(search.getByRole('link', { name: item.label, exact: true })).toHaveAttribute('href', item.href)
    await expect(search.getByRole('alert')).toHaveCount(0)
  }
  await search.getByRole('link', { name: 'Tug job intake', exact: true }).click()
  await expect(page).toHaveURL(/\/app\/tug-job-intake$/)
  const actualHost = await page.request.get('/api/v1/tug-command')
  expect(actualHost.status()).toBe(200)
}

async function realConnectivity(page: Page) {
  const search = await openSearch(page)
  await search.getByRole('searchbox').fill('audit')
  await search.getByRole('button', { name: 'Search', exact: true }).click()
  await expect(search.getByRole('link', { name: 'Action history', exact: true })).toBeVisible()
  try {
    await page.context().setOffline(true)
    await expect(search.getByRole('status')).toContainText('Offline')
    await expect(search.getByRole('link')).toHaveCount(0)
    await expect(search.getByRole('button', { name: 'Search', exact: true })).toBeDisabled()
    const freshRead = page.waitForResponse(reply => new URL(reply.url()).pathname === '/api/v1/workspace/search' && reply.status() === 200)
    await page.context().setOffline(false)
    await freshRead
    await expect(search.getByRole('link', { name: 'Action history', exact: true })).toBeVisible()
  } finally { await page.context().setOffline(false) }
}

async function actualDraftGuard(page: Page) {
  const reply = await page.request.get('/api/v1/vessels?offset=0&limit=25')
  expect(reply.status()).toBe(200)
  const vessel = (await reply.json()).items.find((value: { canEdit: boolean }) => value.canEdit === true)
  expect(vessel).toBeTruthy()
  await page.goto(`/app/fleet/${vessel.id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  const draftName = 'SYNTHETIC-UNSAVED-' + crypto.randomUUID()
  await page.getByLabel('Name *', { exact: true }).fill(draftName)
  await page.getByRole('button', { name: /Search workspace/ }).click()
  const search = page.getByRole('dialog', { name: 'Search workspace', exact: true })
  await search.getByRole('searchbox').fill('tug-job-board')
  await search.getByRole('button', { name: 'Search', exact: true }).click()
  await search.getByRole('link', { name: 'Tug job board', exact: true }).click()
  const guard = page.getByRole('dialog', { name: 'Discard the unsaved draft?', exact: true })
  await expect(guard).toBeVisible()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${vessel.id}$`))
  await guard.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await search.getByRole('button', { name: 'Close', exact: true }).click()
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue(draftName)
  const current = await page.request.get(`/api/v1/vessels/${vessel.id}`)
  expect(current.status()).toBe(200)
  expect((await current.json()).name).toBe(vessel.name)
}

export const searchCases = [
  { name: 'workspace-search-real-audit-and-tug-destinations', run: realDestinations, restricted: false },
  { name: 'workspace-search-real-offline-reauthorized-reconnect', run: realConnectivity, restricted: false },
  { name: 'workspace-search-real-cross-workspace-dirty-guard', run: actualDraftGuard, restricted: false },
] as const
