import { test, expect } from '@playwright/test'

// Real API host, no route mocking or synthetic production login.
test('product shell presents missing federation setup in both languages', async ({ page, request }) => {
  const session = await request.get('/api/v1/auth/session')
  expect(session.status()).toBe(200)
  expect(await session.json()).toMatchObject({ authenticated: false, setupRequired: true })
  await page.goto('/app')
  await expect(page.getByRole('heading', { name: 'Çalışma alanı' })).toBeVisible()
  await expect(page.getByText(/henüz yapılandırılmadı/)).toBeVisible()
  await expect(page.getByRole('link', { name: 'Oturum aç', exact: true })).toHaveCount(0)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText(/has not been configured/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Fleet', exact: true })).toHaveCount(0)
  await page.goto('/app/fleet')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Workspace', exact: true })).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
  expect((await request.get('/api/v1/vessels')).status()).toBe(401)
  expect((await request.get('/api/v1/companies')).status()).toBe(401)
  // Real anonymous profile boundary only; authenticated profile behavior has API/component coverage.
  const profile = '/api/v1/vessels/00000000-0000-0000-0000-000000000001/regulatory-profile'
  expect((await request.get(profile)).status()).toBe(401)
  expect((await request.get(`${profile}/history`)).status()).toBe(401)
  // Setup host has no audit database: mandatory denial audit fails closed with 503.
  expect((await request.put(profile, { data: { expectedVersion: 0, content: {} } })).status()).toBe(503)
})

test('product navigation is keyboard accessible and technical route is preserved', async ({ page }) => {
  await page.goto('/app')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Ana içeriğe geç' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.locator('#product-main')).toBeFocused()
  const menu = page.getByRole('button', { name: 'Menü', exact: true })
  if (await menu.isVisible()) await menu.click()
  await page.getByRole('link', { name: 'Sistem bağlantısı' }).click()
  await expect(page.getByTestId('connection-card')).toHaveAttribute('data-state', 'available')
})

test('directory setup exposes no management controls or invented operational status', async ({ page, request }) => {
  const session = await request.get('/api/v1/auth/session')
  expect(await session.json()).toMatchObject({ authenticated: false, setupRequired: true, canCreate: false, canReadCompanies: false })
  await page.goto('/app')
  await expect(page.getByText(/henüz yapılandırılmadı/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Yeni kayıt' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Düzenle' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Filo', exact: true })).toHaveCount(0)
  await page.goto('/app/fleet')
  await expect(page.getByRole('table')).toHaveCount(0)
  await expect(page.getByText('Ready', { exact: true })).toHaveCount(0)
  expect((await request.get('/api/v1/vessels?kind=Tug&q=synthetic')).status()).toBe(401)
  expect((await request.get('/health/live')).status()).toBe(200)
  // External IdP, authenticated management forms and policy acceptance are covered separately;
  // this real-browser setup test cannot claim an authenticated external login.
})

test('anonymous deep links and keyboard search do not expose destinations or records', async ({ page, request }) => {
  await page.goto('/app/fleet/10000000-0000-0000-0000-000000000001/profile')
  await expect(page.getByText(/henüz yapılandırılmadı/)).toBeVisible()
  await page.keyboard.press('Control+k')
  await expect(page.getByRole('dialog')).toHaveCount(0)
  await expect(page.getByRole('table')).toHaveCount(0)
  await expect(page.getByLabel('GT', { exact: true })).toHaveCount(0)
  await page.reload()
  await expect(page.getByText(/henüz yapılandırılmadı/)).toBeVisible()
  expect((await request.get('/api/v1/workspace')).status()).toBe(401)
  expect((await request.get('/api/v1/workspace/search?q=private')).status()).toBe(401)
})
