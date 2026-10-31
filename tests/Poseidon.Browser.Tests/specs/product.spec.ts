import { test, expect } from '@playwright/test'

// Real API host, no route mocking or synthetic production login.
test('product shell presents missing federation setup in both languages', async ({ page, request }) => {
  const session = await request.get('/api/v1/auth/session')
  expect(session.status()).toBe(200)
  expect(await session.json()).toMatchObject({ authenticated: false, setupRequired: true })
  await page.goto('/app')
  await expect(page.getByRole('heading', { name: 'Şirketler' })).toBeVisible()
  await expect(page.getByText(/henüz yapılandırılmadı/)).toBeVisible()
  await expect(page.getByRole('link', { name: 'Oturum aç', exact: true })).toHaveCount(0)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText(/has not been configured/)).toBeVisible()
  await page.getByRole('button', { name: 'Fleet', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Fleet', exact: true })).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
  expect((await request.get('/api/v1/vessels')).status()).toBe(401)
  expect((await request.get('/api/v1/companies')).status()).toBe(401)
})

test('product navigation is keyboard accessible and technical route is preserved', async ({ page }) => {
  await page.goto('/app')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Ana içeriğe geç' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.locator('#product-main')).toBeFocused()
  await page.getByRole('link', { name: 'Sistem bağlantısı' }).click()
  await expect(page.getByTestId('connection-card')).toHaveAttribute('data-state', 'available')
})
