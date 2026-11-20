import { expect, test, type Page } from '@playwright/test';
// DEV-119 / T-V463-005 (18.01 Backlog row 120) tug buyer demo journey, with DEV-091 history and DEV-092 offline
// boundaries. Synthetic transport: browser behaviour and deep links only, not real server authorization; FICTIONAL data.
// Authored; execution is deferred to the larger actual PR gate (no local backend/browser run in this delivery).
const vessel = '11111111-1111-4111-8111-111111111111', job = '22222222-2222-4222-8222-222222222222', actor = '44444444-4444-4444-8444-444444444444';
const T = '2026-11-20T08:00:00Z';

async function transport(page: Page) {
    const writes: string[] = [];
    await page.route('**/api/v1/**', async (route) => {
        const request = route.request(), url = new URL(request.url()), path = url.pathname;
        if (request.method() !== 'GET') { writes.push(path); await route.fulfill({ status: 405, json: {} }); return; }
        if (path.endsWith('/auth/session')) {
            await route.fulfill({ json: { authenticated: true, setupRequired: false, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false,
                csrfToken: 'synthetic-tug-csrf', actorId: actor, tenantId: vessel, tenantName: 'FICTIONAL tenant' } });
            return;
        }
        if (path === '/api/v1/tug-operation-history') {
            await route.fulfill({ json: { items: [{ jobId: job, vesselId: vessel, jobNumber: 'FICTIONAL-JOB-1', jobStatus: 'Cancelled', jobType: null, assistedVessel: null,
                operationArea: null, at: T, cancelledAt: T, outcome: 'CANCELLED', evidence: 'UNKNOWN', planState: null, briefingState: null, executions: [], dailyLogs: [],
                safetyEvents: 'UNKNOWN:SafetyEventJobLinkUnavailable' }], nextCursor: null, outcomes: ['CANCELLED'], readAt: T, deletion: 'NoSilentDeletion', sourceVersion: 'x' } });
            return;
        }
        if (path === '/api/v1/tug-operations-offline') {
            await route.fulfill({ json: { vesselId: vessel, state: 'UNAVAILABLE_POLICY', operations: [], onlineOnly: ['MASTER_RELEASE', 'EXECUTION_START'], snapshots: [] } });
            return;
        }
        await route.fulfill({ status: 403, json: {} });
    });
    return writes;
}

test('demo journey deep-links every gate in order with a Why panel and writes nothing', async ({ page }) => {
    const writes = await transport(page);
    await page.goto(`/app/tug-demo-journey?vessel=${vessel}&job=${job}`);
    await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption('en');
    const links = page.getByRole('link', { name: 'Open screen' });
    await expect(links).toHaveCount(10);
    await expect(links.nth(0)).toHaveAttribute('href', `/app/tug-job-intake?vessel=${vessel}`);
    await expect(links.nth(5)).toHaveAttribute('href', `/app/tug-readiness?vessel=${vessel}&job=${job}`);
    await expect(links.nth(6)).toHaveAttribute('href', `/app/towage-execution?vessel=${vessel}&job=${job}`);
    await page.getByText('Why').nth(5).click();
    await expect(page.getByText(/GO is a human record bound to the current readiness assessment/)).toBeVisible();
    await expect(page.getByText(/Demo data is fictional and unverified/)).toBeVisible();
    expect(writes).toEqual([]);
});

test('history keeps a cancelled job visible and the offline screen never offers a snapshot without an operator policy', async ({ page }) => {
    const writes = await transport(page);
    await page.goto(`/app/tug-operation-history?vessel=${vessel}`);
    await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption('en');
    await expect(page.getByRole('table').getByText('Cancelled', { exact: true })).toBeVisible();
    await expect(page.getByText(/No silent deletion/)).toBeVisible();
    await page.goto(`/app/tug-operations-offline?vessel=${vessel}&job=${job}`);
    await expect(page.getByText(/Online only: Master release, readiness GO/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Prefetch job snapshot' })).toBeDisabled();
    await page.context().setOffline(true);
    await expect(page.getByText(/Captures are sealed on this device/)).toBeVisible();
    await page.context().setOffline(false);
    expect(writes).toEqual([]);
});
