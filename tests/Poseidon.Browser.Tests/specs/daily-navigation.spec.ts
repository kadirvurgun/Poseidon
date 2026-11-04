import { expect, test, type Page } from '@playwright/test'
import { dailyActor, dailyCapsFixture, dailyPageFixture, dailyVessel } from '../../../frontend/src/product/daily-test-fixtures.ts'

// DEV-097 / T-OPS2-001 / Backlog row98. Synthetic transport: browser history,
// draft protection and Chrome navigation only, not real server authorization.
// Authored; execution is deferred to the larger actual PR gate.
async function openDraft(page: Page) {
  let writes = 0
  const vessel = { id: dailyVessel, companyId: dailyActor, code: 'SYN', name: 'FICTIONAL daily vessel', kind: 'Ship', version: 1, canEdit: true }
  await page.route('**/api/v1/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname
    if (request.method() !== 'GET') { writes++; await route.fulfill({ status: 405, json: {} }); return }
    if (path.endsWith('/auth/session')) {
      await route.fulfill({ json: { authenticated: true, setupRequired: false, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false,
        csrfToken: 'synthetic-daily-csrf', actorId: dailyActor, tenantId: dailyVessel, tenantName: 'FICTIONAL tenant' } }); return
    }
    if (path === '/api/v1/noon-reports/capabilities') { await route.fulfill({ json: dailyCapsFixture('Noon') }); return }
    if (path === '/api/v1/noon-reports') { await route.fulfill({ json: dailyPageFixture('Noon') }); return }
    if (path === '/api/v1/vessels') { await route.fulfill({ json: { items: [vessel], offset: 0, limit: 25, hasMore: false } }); return }
    if (path === `/api/v1/vessels/${dailyVessel}`) { await route.fulfill({ json: vessel }); return }
    await route.fulfill({ status: 403, json: {} })
  })
  await page.goto('/app/noon-report')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'SYN · FICTIONAL daily vessel', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/noon-report/${dailyVessel}$`))
  await page.getByRole('button', { name: 'New draft', exact: true }).click()
  const form = page.getByRole('form', { name: 'Daily declaration form' })
  await form.getByLabel('Weather / sea summary — Data state').selectOption('Known')
  await form.getByLabel('Weather / sea summary', { exact: true }).fill('Retained daily declaration')
  return { form, protectedUrl: page.url(), writes: () => writes }
}

test('daily Chrome navigation keeps edits or discards once without a second native unload prompt', async ({ page }) => {
  const { form, writes } = await openDraft(page), unexpected: string[] = []
  page.on('dialog', async dialog => { unexpected.push(dialog.type()); await dialog.dismiss() })
  const back = page.getByRole('link', { name: 'Back to vessel', exact: true })
  await back.click()
  await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(form.getByLabel('Weather / sea summary', { exact: true })).toHaveValue('Retained daily declaration')
  await back.click()
  await page.getByRole('dialog').getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${dailyVessel}$`))
  expect(unexpected).toEqual([]); expect(writes()).toBe(0)
})

test('daily browser Back restores the guarded entry and confirms keep or discard exactly once', async ({ page }) => {
  const { form, protectedUrl, writes } = await openDraft(page), unexpected: string[] = []
  page.on('dialog', async dialog => { unexpected.push(dialog.type()); await dialog.dismiss() })
  await page.goBack({ waitUntil: 'commit' })
  await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page).toHaveURL(protectedUrl)
  await expect(form.getByLabel('Weather / sea summary', { exact: true })).toHaveValue('Retained daily declaration')
  await page.goBack({ waitUntil: 'commit' })
  await page.getByRole('dialog').getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(/\/app\/noon-report$/)
  await expect(page.getByRole('heading', { name: 'Select a ship', exact: true })).toBeVisible()
  await expect(page.getByRole('dialog')).toHaveCount(0)
  expect(unexpected).toEqual([]); expect(writes()).toBe(0)
})
