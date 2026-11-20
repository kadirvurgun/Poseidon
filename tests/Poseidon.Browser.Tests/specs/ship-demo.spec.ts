import { expect, test } from '@playwright/test';
import { shipOpsOriginPinFixture, shipOpsRecordFixture, shipOpsTenant, shipOpsVessel } from '../../../frontend/src/product/ship-operation-test-fixtures.ts';
// S10 WP-C DEV-120 (18.01 sheet:Backlog/row:121) ship buyer demo journey. Synthetic transport: the real UI
// bundle with a scripted read-only API; it proves step order, visible Why, deep links to existing screens,
// pinned single-entry reuse and the absence of any second entry form or write. Not server authorization evidence.
test('ship demo walks Departure -> Noon/Daily -> Fuel/BDN -> SOF -> Downtime -> Readiness without a second entry form', async ({ page }) => {
    let writes = 0;
    const vessel = { id: shipOpsVessel, companyId: shipOpsTenant, code: 'SYN', name: 'FICTIONAL demo vessel', kind: 'Ship', version: 1, canEdit: false };
    const pinned = { ...shipOpsRecordFixture('Fuel'), origin: shipOpsOriginPinFixture() };
    await page.route('**/api/v1/**', async (route) => {
        const request = route.request(), path = new URL(request.url()).pathname;
        if (request.method() !== 'GET') { writes++; await route.fulfill({ status: 405, json: {} }); return; }
        if (path.endsWith('/auth/session')) {
            await route.fulfill({ json: { authenticated: true, setupRequired: false, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false,
                csrfToken: 'synthetic-demo-csrf', actorId: shipOpsTenant, tenantId: shipOpsTenant, tenantName: 'FICTIONAL tenant' } });
            return;
        }
        if (path === '/api/v1/vessels') { await route.fulfill({ json: { items: [vessel], offset: 0, limit: 25, hasMore: false } }); return; }
        if (path === '/api/v1/fuel-bunker-logs') { await route.fulfill({ json: { items: [pinned], offset: 0, limit: 25, hasMore: false, canWrite: false, sourceVersion: pinned.sourceVersion, offline: { supported: false, state: 'UNAVAILABLE', operation: 'ShipOpsDraft' } } }); return; }
        if (path === '/api/v1/port-activity-sof') { await route.fulfill({ json: { items: [], offset: 0, limit: 25, hasMore: false, canWrite: false, sourceVersion: pinned.sourceVersion, offline: { supported: false, state: 'UNAVAILABLE', operation: 'ShipOpsDraft' } } }); return; }
        await route.fulfill({ status: 403, json: {} });
    });
    await page.goto(`/app/ship-demo/${shipOpsVessel}`);
    await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption('en');
    const steps = page.getByRole('list', { name: 'Demo steps' });
    await expect(steps.getByRole('listitem')).toHaveCount(6);
    await expect(steps.getByRole('heading')).toHaveText(['1. Departure readiness', '2. Noon and daily reports', '3. Fuel and bunker (BDN)', '4. Port activity (SOF)', '5. Operational downtime', '6. Readiness review']);
    await steps.getByText('Why').first().click();
    await expect(steps.getByText(/never appear ready/)).toBeVisible();
    await expect(steps.getByRole('link', { name: 'Open screen' }).nth(2)).toHaveAttribute('href', `/app/fuel-bunker-log/${shipOpsVessel}`);
    await expect(page.getByTestId('ship-demo-reuse-fuel')).toContainText('1 reuse a pinned origin, 0 entered directly');
    await expect(page.getByTestId('ship-demo-reuse-port')).toContainText('0 reuse a pinned origin, 0 entered directly');
    await expect(page.getByRole('main').getByRole('textbox')).toHaveCount(0);
    expect(writes).toBe(0);
});
