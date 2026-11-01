import { test, expect } from '@playwright/test'

// Synthetic browser transport only. API/PostgreSQL authorization is verified in WorkspaceTests.cs.
test('workspace URL history, dirty cancel/discard and responsive drawer work with keyboard', async ({ page }) => {
  const vessel = { id: '10000000-0000-0000-0000-000000000001', companyId: '10000000-0000-0000-0000-000000000002', code: 'T1', name: 'Synthetic Tug', kind: 'Tug', version: 1, canEdit: true }
  await page.route('**/api/v1/**', async route => {
    const url = route.request().url()
    const body = url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: true, csrfToken: 'synthetic' }
      : url.includes('workspace/search') ? { items: [{ key: vessel.id, href: `/app/fleet/${vessel.id}`, label: vessel.name, kind: 'Tug' }], hasMore: false }
      : url.includes('vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel
    await route.fulfill({ json: body })
  })
  await page.goto('/app/fleet')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: vessel.name, exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${vessel.id}$`))
  await expect(page.getByText(/Current vessel/)).toContainText('Tug')
  await page.reload()
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  await page.getByLabel('Name *', { exact: true }).fill('Retained draft')
  await page.goBack()
  await expect(page.getByRole('dialog', { name: 'Discard the unsaved draft?' })).toBeVisible()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${vessel.id}$`))
  await page.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue('Retained draft')
  await page.keyboard.press('Control+k')
  const search = page.getByRole('dialog', { name: 'Search workspace' })
  await expect(search).toBeVisible()
  await search.getByRole('button', { name: 'Close', exact: true }).click()
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue('Retained draft')
  await page.getByRole('button', { name: 'Switch vessel', exact: true }).click()
  await page.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue('Retained draft')
  const menu = page.getByRole('button', { name: 'Menu', exact: true })
  if (await menu.isVisible()) {
    await menu.click()
    await expect(page.getByRole('dialog', { name: 'POSEIDON' })).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(menu).toBeFocused()
  }
  // A fresh explicit discard creates the route used to exercise native browser Back/Forward.
  await page.getByRole('button', { name: 'Switch vessel', exact: true }).click()
  await page.getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(/\/app\/fleet$/)
  await page.goBack()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${vessel.id}$`))
  await expect(page.getByRole('region', { name: 'Details' })).toContainText(vessel.name)
  await page.goForward()
  await expect(page).toHaveURL(/\/app\/fleet$/)
})
