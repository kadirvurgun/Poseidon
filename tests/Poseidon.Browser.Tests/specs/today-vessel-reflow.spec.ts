import { expect, test } from '@playwright/test';

// TODAY-VESSEL-REFLOW-01: authored/deferred. Synthetic transport verifies layout
// and keyboard navigation only; it supplies no real authority/operational evidence.
test.describe('Today vessel selector reflow', () => {
    test.use({ trace: 'off', screenshot: 'off', video: 'off' });
    const tenant = '10000000-0000-0000-0000-000000000098';
    const actor = '10000000-0000-0000-0000-000000000099';
    const vessels = Array.from({ length: 6 }, (_, index) => ({
        id: `30000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
        companyId: tenant, code: `SYNTHETIC-${'X'.repeat(53)}${index}`,
        name: `FICTIONAL-${'Y'.repeat(189)}${index}`, kind: 'Ship', version: 1, canEdit: false,
    }));

    for (const scenario of [
        { locale: 'tr', width: 1365 }, { locale: 'tr', width: 580 },
        { locale: 'ar', width: 390 }, { locale: 'tr', width: 320 },
    ]) {
        test(`${scenario.locale} at ${scenario.width}px keeps long identities and controls inside the page`, async ({ page }) => {
            await page.setViewportSize({ width: scenario.width, height: 900 });
            await page.addInitScript(locale => localStorage.setItem('poseidon.locale', locale), scenario.locale);
            let writes = 0;
            await page.route('**/api/v1/**', async route => {
                const request = route.request(), path = new URL(request.url()).pathname;
                if (request.method() !== 'GET') { writes++; await route.fulfill({ status: 405, json: {} }); return; }
                if (path.endsWith('/auth/session')) {
                    await route.fulfill({ json: { authenticated: true, tenantId: tenant, actorId: actor,
                        tenantName: 'FICTIONAL workspace', canRead: true, canReadCompanies: false,
                        canCreate: false, canSearch: false, csrfToken: 'synthetic-browser-only' } }); return;
                }
                if (path === '/api/v1/system/status') {
                    await route.fulfill({ json: { service: 'poseidon', status: 'available' } }); return;
                }
                if (path === '/api/v1/workspace') {
                    await route.fulfill({ json: { destinations: [{ key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' }], canSearch: false, canWriteRegulatory: false } }); return;
                }
                if (path.startsWith('/api/v1/workspace/context')) {
                    const selected = vessels.find(vessel => path.endsWith('/' + vessel.id));
                    await route.fulfill({ json: { tenantId: tenant, actorId: actor,
                        roleContext: { state: 'Unavailable', catalogVersion: null, roles: [] },
                        vessel: selected ? { id: selected.id, code: selected.code, name: selected.name, kind: selected.kind } : null } }); return;
                }
                if (path.startsWith('/api/v1/workspace/lenses')) {
                    const selected = vessels.find(vessel => path.endsWith('/' + vessel.id));
                    await route.fulfill({ json: { tenantId: tenant, actorId: actor, vesselId: selected?.id ?? null,
                        mode: selected ? 'Ship' : 'Fleet', state: 'Unavailable', version: null, canSearch: false, lenses: [] } }); return;
                }
                if (path === '/api/v1/vessels') {
                    await route.fulfill({ json: { items: vessels, offset: 0, limit: 25, hasMore: false } }); return;
                }
                await route.fulfill({ status: 404, json: {} });
            });
            await page.goto('/app/today');
            await expect(page.locator('html')).toHaveAttribute('dir', scenario.locale === 'ar' ? 'rtl' : 'ltr');
            const cards = page.locator('.enterprise-vessel-grid > [data-slot="card"]');
            await expect(cards).toHaveCount(vessels.length);
            await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
            for (const vessel of vessels) {
                const identity = `${vessel.code} · ${vessel.name}`;
                const card = page.getByRole('group', { name: identity, exact: true });
                await expect(card.getByRole('heading', { name: vessel.code, exact: true })).toHaveText(vessel.code);
                await expect(card.getByText(vessel.name, { exact: true })).toHaveText(vessel.name);
                expect(await card.evaluate(node => node.scrollWidth <= node.clientWidth + 1)).toBe(true);
                const button = await card.getByRole('button', { name: identity, exact: true }).boundingBox();
                const bounds = await card.boundingBox();
                expect(button).not.toBeNull(); expect(bounds).not.toBeNull();
                expect(button!.x).toBeGreaterThanOrEqual(bounds!.x);
                expect(button!.x + button!.width).toBeLessThanOrEqual(bounds!.x + bounds!.width + 1);
            }
            const open = cards.first().getByRole('button');
            await open.focus(); await expect(open).toBeFocused(); await page.keyboard.press('Enter');
            await expect(page).toHaveURL(new RegExp(`/app/today/${vessels[0].id}$`));
            expect(writes).toBe(0);
        });
    }
});
