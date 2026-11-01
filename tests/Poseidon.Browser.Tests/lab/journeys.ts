import { expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const ship = '30000000-0000-0000-0000-000000000001';
const tug = '30000000-0000-0000-0000-000000000002';
const foreign = '30000000-0000-0000-0000-000000000003';
const profile = `/app/fleet/${ship}/profile`;
const credentials = JSON.parse(readFileSync(resolve(process.env.POSEIDON_LAB_PRIVATE!, 'credentials.json'), 'utf8'));

async function english(page: Page) {
  await page.getByRole('button', { name: 'EN', exact: true }).click();
}
export async function login(page: Page, account: string, destination: string) {
  await page.goto(destination);
  await english(page);
  await page.getByRole('link', { name: 'Sign in', exact: true }).click();
  await page.waitForURL(url => url.origin === 'https://localhost:8443');
  // Check origin without emitting the full OIDC login URL in assertion errors.
  expect(new URL(page.url()).origin === 'https://localhost:8443').toBe(true);
  await page.locator('#username').fill(credentials[account].username);
  await page.locator('#password').fill(credentials[account].password);
  await page.locator('#kc-login').click();
  await page.waitForURL('https://localhost:5443' + destination);
  await english(page);
  await expect(page.getByRole('button', { name: 'Sign out', exact: true })).toBeVisible();
  const session = await (await page.request.get('/api/v1/auth/session')).json();
  expect(session.authenticated).toBe(true);
  expect(session.setupRequired).toBe(false);
  // Protocol success above uses real Keycloak-signed tokens; no mocked route/issuer.
  const cookies = await page.context().cookies('https://localhost:5443');
  expect(cookies.some(c => c.name === '__Host-poseidon-session' && c.secure && c.httpOnly)).toBe(true);
  expect(await page.evaluate(() => Object.keys(localStorage).some(k => /token/i.test(k)))).toBe(false);
}

export async function managerJourney(page: Page) {
  expect((await page.request.get('/')).status()).toBe(200);
  expect((await page.request.get('/agents')).status()).toBe(200);
  expect((await page.request.get('/api/v1/unknown-lab-resource')).status()).toBe(404);
  await login(page, 'lab-manager', '/app/companies');
  const suffix = Date.now().toString();
  await page.getByRole('button', { name: 'New record', exact: true }).click();
  await page.getByLabel('Code *', { exact: true }).fill('LAB-' + suffix);
  await page.getByLabel('Name *', { exact: true }).fill('Synthetic Lab ' + suffix);
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Record saved. You can open its current details.')).toBeVisible();
  await page.getByRole('button', { name: 'Edit', exact: true }).click();
  await page.getByLabel('Name *', { exact: true }).fill('Synthetic Edited ' + suffix);
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.locator('.product-detail').getByText('Synthetic Edited ' + suffix, { exact: true })).toBeVisible();
  await page.goto(profile);
  await english(page);
  await expect(page.getByRole('heading', { name: 'Regulatory profile draft', exact: true })).toBeVisible();
  await page.getByLabel('GT — Data state', { exact: true }).selectOption('Known');
  await page.getByLabel('GT', { exact: true }).fill('9007199254740993');
  await page.getByLabel('NT — Data state', { exact: true }).selectOption('Known');
  await page.getByLabel('NT', { exact: true }).fill('0.1234567890123456789012345678');
  await page.getByLabel('Flag — Data state', { exact: true }).selectOption('Unknown');
  await page.getByRole('button', { name: 'Save draft', exact: true }).click();
  await expect(page.getByText('Draft saved.', { exact: true })).toBeVisible();
  const before = await (await page.request.get(`/api/v1/vessels/${ship}/regulatory-profile`)).json();
  expect(before.content.gt.value).toBe('9007199254740993');
  expect(before.content.nt.value).toBe('0.1234567890123456789012345678');
  expect(before.content.flag.state).toBe('Unknown');
  await page.reload(); await english(page);
  await expect(page.getByLabel('GT', { exact: true })).toHaveValue('9007199254740993');
  await page.getByLabel('Flag — Data state', { exact: true }).selectOption('Known');
  await page.getByLabel('Flag', { exact: true }).fill('Synthetic retained');
  await page.getByRole('button', { name: 'Switch vessel', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.getByRole('button', { name: 'Keep editing', exact: true }).click();
  await expect(page.getByLabel('Flag', { exact: true })).toHaveValue('Synthetic retained');
  await page.getByRole('button', { name: 'Save draft', exact: true }).click();
  await expect(page.getByText('Draft saved.', { exact: true })).toBeVisible();
  const history = await (await page.request.get(`/api/v1/vessels/${ship}/regulatory-profile/history?offset=0&limit=25`)).json();
  expect(history.items.some((r: { revision: number; content: unknown }) => r.revision === before.revision && JSON.stringify(r.content) === JSON.stringify(before.content))).toBe(true);
  await page.locator('summary').filter({ hasText: new RegExp(`^Version ${before.revision} ·`) }).click();
  await expect(page.locator('details[open]').getByText('9007199254740993', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: /Search workspace/ }).click();
  const search = page.getByRole('dialog', { name: 'Search workspace', exact: true });
  await search.getByLabel('Destinations and vessels').fill('DEMO-TUG');
  await search.getByRole('button', { name: 'Search', exact: true }).click();
  await search.getByRole('link', { name: /Synthetic Tug A/ }).click();
  expect(new URL(page.url()).pathname === `/app/fleet/${tug}`).toBe(true);
  await page.goBack();
  expect(new URL(page.url()).pathname === profile).toBe(true);
  await page.goto(`/app/today/${ship}`); await english(page);
  await expect(page.getByRole('heading', { name: /Current vessel: Ship/ })).toBeVisible();
  await page.getByRole('button', { name: 'Why?', exact: true }).click();
  await expect(page.getByText('Input freshness is unverified. Save/read time does not verify the source.', { exact: true })).toBeVisible();
  const why = await (await page.request.get(`/api/v1/why-engine?vesselId=${ship}&cardKey=regulatory-draft`)).json();
  expect(why.freshness === 'Unverified' && why.inputs.known > 0).toBe(true);
  await page.goto(profile); await english(page);
  // Existing antiforgery pipeline also applies to real signed-provider sessions.
  expect((await page.request.post('/api/v1/auth/logout')).status()).toBe(403);
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await page.waitForURL(url => url.origin === 'https://localhost:8443' ||
    (url.origin === 'https://localhost:5443' && url.pathname === '/app'));
  // No ID-token hint is stored. Keycloak requires user confirmation for this flow.
  if (new URL(page.url()).origin === 'https://localhost:8443')
    await page.locator('#kc-logout').click();
  await page.waitForURL('https://localhost:5443/app');
  expect((await (await page.request.get('/api/v1/auth/session')).json()).authenticated).toBe(false);
  expect((await page.request.get('/api/v1/vessels')).status()).toBe(401);
  // Provider session ended too: login returns to a credential prompt.
  await english(page);
  await page.getByRole('link', { name: 'Sign in', exact: true }).click();
  await expect(page.locator('#password')).toBeVisible();
}

export async function restrictedJourney(page: Page) {
  await login(page, 'lab-restricted', `/app/fleet/${ship}`);
  await expect(page.getByRole('button', { name: 'Companies', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Search workspace/ })).toHaveCount(0);
  const session = await (await page.request.get('/api/v1/auth/session')).json();
  expect(session.canCreate).toBe(false); expect(session.canSearch).toBe(false); expect(session.canReadCompanies).toBe(false);
  expect((await (await page.request.get('/api/v1/companies')).json()).items).toEqual([]);
  expect((await page.request.get('/api/v1/companies/20000000-0000-0000-0000-000000000001')).status()).toBe(404);
  expect((await page.request.get('/api/v1/workspace/search?q=DEMO')).status()).toBe(403);
  expect((await page.request.post('/api/v1/companies', { headers: { 'X-CSRF-TOKEN': session.csrfToken },
    data: { code: 'DENIED', name: 'Synthetic denied' } })).status()).toBe(403);
  expect((await page.request.put(`/api/v1/vessels/${ship}/regulatory-profile`, {
    headers: { 'X-CSRF-TOKEN': session.csrfToken }, data: {} })).status()).toBe(403);
  for (const id of [tug, foreign]) {
    expect((await page.request.get(`/api/v1/vessels/${id}`)).status()).toBe(404);
    expect((await page.request.get(`/api/v1/vessels/${id}/regulatory-profile`)).status()).toBe(404);
  }
  const vessels = await (await page.request.get('/api/v1/vessels')).json();
  expect(vessels.items.map((v: { id: string }) => v.id)).toEqual([ship]);
  await page.goto(profile); await english(page);
  await expect(page.getByRole('button', { name: 'Save draft', exact: true })).toHaveCount(0);
  await page.goto(`/app/fleet/${foreign}`); await english(page);
  await expect(page.getByText('Synthetic Ship B', { exact: true })).toHaveCount(0);
}
