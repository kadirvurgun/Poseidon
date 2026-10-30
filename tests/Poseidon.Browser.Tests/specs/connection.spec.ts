import { test, expect } from '@playwright/test';

test('T-S0-002: built React reads the real API through its proxy', async ({ page }, testInfo) => {
  const status = page.waitForResponse(response => response.url().endsWith('/api/v1/system/status'));
  await page.goto('/');
  const response = await status;
  expect(response.status()).toBe(200);
  expect(await response.json()).toEqual({ service: 'poseidon', status: 'available' });
  await expect(page.getByTestId('connection-card')).toHaveAttribute('data-state', 'available');
  await expect(page.getByRole('heading', { name: 'API bağlantısı kuruldu' })).toBeVisible();
  // Public technical availability must not be presented as a safety sign-off.
  await expect(page.getByRole('button', { name: /onayla|approve|release/i })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('connection.png'), fullPage: true });
});

test('T-S0-002: transport failure stays visible and retry reconnects to the real API', async ({ page }) => {
  await page.route('**/api/v1/system/status', route => route.abort('failed'));
  await page.goto('/');
  await expect(page.getByTestId('connection-card')).toHaveAttribute('data-state', 'error');
  await expect(page.getByRole('heading', { name: 'API bağlantısı kurulamadı' })).toBeVisible();
  await page.unroute('**/api/v1/system/status');
  await page.getByTestId('connection-retry').click();
  await expect(page.getByTestId('connection-card')).toHaveAttribute('data-state', 'available');
});
