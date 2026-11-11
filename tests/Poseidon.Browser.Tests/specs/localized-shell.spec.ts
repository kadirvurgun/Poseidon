import { readFileSync } from 'node:fs';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { locales, localeDirection, localeStorageKey } from '../../../frontend/src/i18n/locales';

// L17-AC-SHELL. Authored for the full localization bundle gate; NOT RUN.
// All seventeen main catalogs must exist before this matrix can pass. This
// exercises the built UI and actual public heartbeat, not signed user authority.
async function fitsPage(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}

async function fitsVisibleContainer(child: Locator, container: Locator) {
  if (!await child.isVisible()) return;
  const content = await child.boundingBox(), bounds = await container.boundingBox();
  expect(content).not.toBeNull();
  expect(bounds).not.toBeNull();
  expect(content!.x).toBeGreaterThanOrEqual(bounds!.x);
  expect(content!.x + content!.width).toBeLessThanOrEqual(bounds!.x + bounds!.width);
}

for (const locale of locales) {
  const labels = JSON.parse(readFileSync(new URL(`../../../frontend/src/i18n/labels/${locale}.json`, import.meta.url), 'utf8')) as Record<string, string>;
  test(`L17-AC-SHELL: ${locale} keeps captions, focus, protocol and locale across route changes`, async ({ page }) => {
    let heartbeatRequests = 0;
    page.on('request', request => { if (new URL(request.url()).pathname === '/api/v1/system/status') heartbeatRequests++; });
    const response = page.waitForResponse(value => new URL(value.url()).pathname === '/api/v1/system/status');
    await page.goto('/');
    const heartbeat = await response;
    expect(heartbeat.status()).toBe(200);
    expect(await heartbeat.json()).toEqual({ service: 'poseidon', status: 'available' });
    await expect(page.getByTestId('connection-card')).toHaveAttribute('data-state', 'available');
    const selector = page.getByRole('combobox');
    await expect(selector.getByRole('option')).toHaveCount(17);
    await selector.focus();
    await selector.selectOption(locale);
    await expect(selector).toHaveValue(locale);
    await expect(selector).toHaveAttribute('aria-busy', 'false');
    await expect(selector).toBeFocused();
    await expect(page.locator('html')).toHaveAttribute('lang', locale);
    await expect(page.locator('html')).toHaveAttribute('dir', localeDirection(locale));
    await expect(page.locator('.brand small')).toHaveText(labels.maritimePlatform);
    await expect(page.locator('.sidebar-section')).toHaveText(labels.workspaceCaption);
    await expect(page.locator('.sidebar-bottom small')).toContainText(labels.developmentCaption);
    await expect(page.locator('.footer-marker')).toContainText(labels.foundationCaption);
    // The protocol literal remains unchanged in every language.
    await expect(page.locator('.contract code')).toHaveText('poseidon / available');
    await expect(page.locator('.endpoint code')).toHaveText('/api/v1/system/status');
    expect(heartbeatRequests).toBe(1);
    expect(await page.evaluate(key => localStorage.getItem(key), localeStorageKey)).toBe(locale);
    await fitsVisibleContainer(page.locator('.brand small'), page.locator('.sidebar'));
    await fitsPage(page);

    await page.goto('/agents');
    await expect(page.getByRole('combobox')).toHaveValue(locale);
    await expect(page.locator('.ops-brand small')).toHaveText(labels.localOperations);
    await fitsVisibleContainer(page.locator('.ops-brand small'), page.locator('.ops-nav'));
    await fitsPage(page);

    await page.goto('/app');
    await expect(page.getByRole('combobox')).toHaveValue(locale);
    await expect(page.locator('.enterprise-brand small')).toHaveText(labels.maritimeOperations);
    await fitsVisibleContainer(page.locator('.enterprise-brand small'), page.locator('.enterprise-sidebar'));
    await fitsPage(page);
    await page.reload();
    await expect(page.getByRole('combobox')).toHaveValue(locale);
    await expect(page.locator('html')).toHaveAttribute('dir', localeDirection(locale));
    await expect(page.locator('.enterprise-brand small')).toHaveText(labels.maritimeOperations);
  });
}
