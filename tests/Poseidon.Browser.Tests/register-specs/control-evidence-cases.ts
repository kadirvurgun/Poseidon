import { expect, type Page, type Request, type Route } from '@playwright/test';
// Genuine signed host manifest; actual server responses forwarded, no test login or fabricated business response.
// Coordinator must provision explicit graph/file permissions and real owned transport.
async function writer(page: Page) {
    // Qualified positive consumers share this tenant. This negative needs its own
    // real command-created Ship with no authoritative evaluation, not the first row.
    const fixtureId = process.env.POSEIDON_BROWSER_NO_EVALUATION_VESSEL;
    if (!fixtureId || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(fixtureId) || fixtureId === '00000000-0000-0000-0000-000000000000')
        throw new Error('no_evaluation_ship_fixture_uuid_required');
    await page.goto('/app/sources');
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click();
    const region = page.getByRole('region', { name: 'Controls and evidence', exact: true });
    const response = await page.request.get('/api/v1/vessels?offset=0&limit=25');
    expect(response.status()).toBe(200);
    const vessel = (await response.json()).items.find((v: {
        id: string;
        kind: string;
    }) => v.id === fixtureId && v.kind === 'Ship');
    expect(!!vessel).toBe(true);
    const before = await page.request.get(`/api/v1/controls/orphans?vesselId=${vessel.id}&offset=0`);
    expect(before.status()).toBe(200);
    const initial = await before.json();
    expect(initial.available === false && initial.reason === 'AUTHORITATIVE_APPLICABILITY_UNAVAILABLE').toBe(true);
    await region.getByRole('combobox', { name: 'Vessel context', exact: true }).selectOption(vessel.id);
    await expect(region.getByText('Draft authoring available', { exact: true })).toBeVisible();
    await region.getByLabel('Title', { exact: true }).fill('FICTIONAL CONTROL-EVIDENCE signed-host draft');
    await region.getByRole('combobox', { name: 'Criticality', exact: true }).selectOption('true');
    const [saved] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/controls' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Save incomplete draft', exact: true }).click()]);
    expect(saved.status()).toBe(200);
    const first = await saved.json();
    expect(first.lifecycle === 'DRAFT' && first.verification === 'UNVERIFIED' && first.effectiveness === 'UNKNOWN' && !first.evidenceExists).toBe(true);
    await expect(region.getByText('Draft saved; verification and effectiveness remain unmet.', { exact: true })).toBeVisible();
    await expect(region.getByRole('region', { name: 'Immutable history', exact: true })).toBeVisible();
    const report = await page.request.get(`/api/v1/controls/orphans?vesselId=${vessel.id}&offset=0`);
    expect(report.status()).toBe(200);
    const value = await report.json();
    expect(value.available === false && value.reason === 'AUTHORITATIVE_APPLICABILITY_UNAVAILABLE').toBe(true);
    // A confirmed receipt stays read-only until a deliberate current-record
    // recovery. This retains its server ID rather than creating another draft.
    await region.getByRole('button', { name: 'Discard local changes', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Discard draft', exact: true }).click();
    await expect(region.getByLabel('Title', { exact: true })).toBeEnabled();
    await region.getByLabel('Title', { exact: true }).fill('retain this dirty draft');
    await region.getByRole('combobox', { name: 'Register', exact: true }).selectOption('Evidence');
    await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click();
    await expect(region.getByLabel('Title', { exact: true })).toHaveValue('retain this dirty draft');
    await region.getByLabel('Title', { exact: true }).focus();
    await expect(region.getByLabel('Title', { exact: true })).toBeFocused();
}
async function restricted(page: Page) {
    const v = '10000000-0000-4000-8000-000000000001';
    for (const path of ['controls', 'evidence', 'controls/owners', 'controls/orphans', 'evidence/10000000-0000-4000-8000-000000000002/versions'])
        expect((await page.request.get(`/api/v1/${path}?vesselId=${v}`)).status()).toBe(403);
}
// CEO07: genuine current host only. No provider/profile/policy fixture change.
async function controlledLoss(page: Page) {
    const vessel = process.env.POSEIDON_BROWSER_NO_EVALUATION_VESSEL;
    if (!vessel || vessel.length !== 36 || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(vessel))
        throw new Error('control_recovery_actual_ship_fixture_required');
    await page.goto('/app/sources');
    await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption('en');
    await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click();
    const region = page.getByRole('region', { name: 'Controls and evidence', exact: true });
    const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200);
    expect((await fleet.json()).items.some((row: { id: string; kind: string }) => row.id === vessel && row.kind === 'Ship')).toBe(true);
    await region.getByRole('combobox', { name: 'Vessel context', exact: true }).selectOption(vessel);
    await region.getByLabel('Title', { exact: true }).fill('FICTIONAL CEO controlled recovery ' + crypto.randomUUID());
    let sessions = 0, writes = 0, failedReads = 0, loseRead = false, savedId = '', savedRevision = 0;
    const attempts: { path: string; method: string; body: string; key: string; sessions: number }[] = [];
    const observe = (request: Request) => {
        const url = new URL(request.url());
        if (request.method() === 'GET' && url.pathname === '/api/v1/auth/session') sessions++;
        if (request.method() === 'POST' && url.pathname === '/api/v1/controls') attempts.push({
            path: url.pathname + url.search, method: request.method(), body: request.postData() ?? '', key: request.headers()['idempotency-key'] ?? '', sessions });
    };
    const postPattern = '**/api/v1/controls?**';
    const interceptPost = async (route: Route) => {
        if (route.request().method() !== 'POST') { await route.continue(); return; }
        const response = await route.fetch();
        try {
            expect(response.status()).toBe(200);
            const row = await response.json();
            expect(row.lifecycle === 'DRAFT' && row.verification === 'UNVERIFIED' && row.effectiveness === 'UNKNOWN').toBe(true);
            if (++writes === 1) {
                savedId = row.id; savedRevision = row.revision;
                expect(typeof savedId === 'string' && savedId.length === 36 && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(savedId) && savedRevision === 1).toBe(true);
                await route.abort('connectionreset'); // REAL commit, deliberately lost response.
            } else {
                expect(row.id === savedId && row.revision === savedRevision).toBe(true);
                loseRead = true; await route.fulfill({ response });
            }
        } finally { await response.dispose(); }
    };
    const versionsPattern = '**/api/v1/controls/*/versions?**';
    const interceptVersions = async (route: Route) => {
        if (loseRead && new URL(route.request().url()).pathname === `/api/v1/controls/${savedId}/versions`) {
            loseRead = false; failedReads++; await route.abort('connectionreset');
        } else await route.continue();
    };
    page.on('request', observe); await page.route(postPattern, interceptPost); await page.route(versionsPattern, interceptVersions);
    try {
        const beforeFirst = sessions;
        await region.getByRole('button', { name: 'Save incomplete draft', exact: true }).click();
        await expect(region.getByText('Outcome uncertain. The exact original attempt is locked until retry.', { exact: true })).toBeVisible();
        expect(writes === 1 && attempts.length === 1 && attempts[0].sessions > beforeFirst).toBe(true);
        await expect(region.getByRole('button', { name: 'Discard local changes', exact: true })).toHaveCount(0);
        await expect(region.getByRole('combobox', { name: 'Register', exact: true })).toBeDisabled();
        await region.getByRole('button', { name: 'Retry source read', exact: true }).first().click();
        await expect(region.getByRole('button', { name: 'Retry same command', exact: true })).toBeEnabled();
        expect(writes).toBe(1);
        const beforeRetry = sessions;
        await region.getByRole('button', { name: 'Retry same command', exact: true }).click();
        await expect(region.getByText('Draft saved; verification and effectiveness remain unmet.', { exact: true })).toBeVisible();
        await expect.poll(() => failedReads).toBe(1);
        expect(writes === 2 && attempts.length === 2 && attempts[1].sessions > beforeRetry).toBe(true);
        expect(attempts[0].body === attempts[1].body && attempts[0].key === attempts[1].key && attempts[0].path === attempts[1].path && attempts[1].method === 'POST').toBe(true);
        expect(!Object.hasOwn(JSON.parse(attempts[0].body), 'id') && attempts[0].key.length === 36).toBe(true);
        await region.getByRole('button', { name: 'Retry source read', exact: true }).first().click();
        await expect(region.getByLabel('Title', { exact: true })).toBeDisabled();
        await expect(region.getByRole('button', { name: 'Save incomplete draft', exact: true })).toBeDisabled();
        const actual = await page.request.get(`/api/v1/controls/${savedId}/versions?vesselId=${vessel}&offset=0`); expect(actual.status()).toBe(200);
        const rows = (await actual.json()).items;
        expect(rows.length === 1 && rows[0].id === savedId && rows[0].revision === savedRevision).toBe(true);
        expect(writes).toBe(2);
    } finally {
        page.off('request', observe); await page.unroute(postPattern, interceptPost); await page.unroute(versionsPattern, interceptVersions);
    }
}
export const controlEvidenceCases = [
    { name: 'control-evidence-draft-history-unavailable-report-dirty-guard', run: writer, restricted: false },
    { name: 'control-evidence-denied-before-graph-file-owners-counts', run: restricted, restricted: true },
    { name: 'control-evidence-controlled-response-loss-real-commit', run: controlledLoss, restricted: false },
] as const;
