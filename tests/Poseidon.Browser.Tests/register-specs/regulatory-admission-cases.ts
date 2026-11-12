import { expect, type Locator, type Page, type Request, type Route } from '@playwright/test';

// The existing assigned synthetic source is already independent and exact. Keep
// its final qualification/evaluation journey, adding real transport loss without
// provisioning another grant or borrowing another case's mutable source.
async function qualificationRecovery(page: Page, review: Locator, rule: string, previousRevision: number) {
    const path = `/api/v1/regulatory-source-governance/${rule}`;
    const matches = (url: URL) => url.pathname === path || url.pathname === path + '/events';
    const posts: { key: string; body: string; sessionReads: number }[] = [];
    let sessionReads = 0, dropReadback = false, readbackLost = false;
    let committedRevision = 0, committedHash = '';
    const countSession = (request: Request) => {
        if (request.method() === 'GET' && new URL(request.url()).pathname === '/api/v1/auth/session') sessionReads++;
    };
    const handler = async (route: Route) => {
        const request = route.request(), pathname = new URL(request.url()).pathname;
        if (request.method() === 'GET' && pathname === path) {
            if (dropReadback) { dropReadback = false; readbackLost = true; await route.abort('failed'); }
            else await route.continue();
            return;
        }
        if (request.method() !== 'POST' || pathname !== path + '/events') { await route.continue(); return; }
        posts.push({ key: request.headers()['idempotency-key'] ?? '', body: request.postData() ?? '', sessionReads });
        const actual = await route.fetch(); // Actual audited host commit/replay, never a fulfilled fake success.
        expect(actual.status()).toBe(200);
        const outcome = await actual.json();
        expect(outcome.ruleId === rule && outcome.state === 'IN_FORCE' && Number.isSafeInteger(outcome.revision)).toBe(true);
        if (posts.length === 1) {
            committedRevision = outcome.revision; committedHash = outcome.hash;
            await route.abort('failed'); // The real first write committed; only its response is lost.
        }
        else {
            expect(outcome.revision === committedRevision && outcome.hash === committedHash).toBe(true);
            dropReadback = true;
            await route.fulfill({ response: actual });
        }
    };
    page.on('request', countSession);
    await page.route(matches, handler);
    try {
        const location = page.url(), beforeFirst = sessionReads;
        await review.getByRole('button', { name: 'Record assigned human qualification', exact: true }).click();
        await expect(review.getByText('Outcome uncertain. The exact original attempt is locked until retry.', { exact: true })).toBeVisible();
        expect(posts.length).toBe(1);
        await expect(page.getByRole('region', { name: 'Lifecycle and applicability preparation', exact: true }).getByRole('combobox', { name: 'Vessel context', exact: true })).toBeDisabled();
        expect(posts[0].sessionReads > beforeFirst).toBe(true);
        await expect(review.getByRole('button', { name: 'Discard local changes and reload', exact: true })).toBeDisabled();
        await review.getByRole('button', { name: 'Retry source read', exact: true }).click();
        await expect(review.getByRole('button', { name: 'Record assigned human qualification', exact: true })).toBeDisabled();
        expect(posts.length).toBe(1); // A current source read cannot resend or settle the unknown command.
        const beforeRetry = sessionReads;
        await review.getByRole('button', { name: 'Retry exact qualification command', exact: true }).click();
        await expect(review.getByText('Human record saved. No operational release follows.', { exact: true })).toBeVisible();
        await expect(review.getByRole('alert')).toBeVisible();
        expect(readbackLost).toBe(true);
        expect(posts.length).toBe(2);
        expect(posts[1].sessionReads > beforeRetry).toBe(true); // Pre-send freshness, not only the post-write fence.
        expect(posts[0].body === posts[1].body && posts[0].key === posts[1].key && posts[0].key.length === 36).toBe(true);
        expect(page.url() === location).toBe(true);
        await expect(review.getByRole('button', { name: 'Record assigned human qualification', exact: true })).toHaveCount(0);
        await expect(review.getByRole('button', { name: 'Retry exact qualification command', exact: true })).toHaveCount(0);
        await review.getByRole('button', { name: 'Retry source read', exact: true }).click();
        await expect(review.getByRole('button', { name: 'Record assigned human qualification', exact: true })).toBeEnabled();
        const actual = await page.request.get(path);
        expect(actual.status()).toBe(200);
        const current = await actual.json();
        expect(current.items[0].revision === committedRevision && committedRevision === previousRevision + 1).toBe(true);
        expect(current.items.filter((row: { revision: number }) => row.revision === committedRevision).length).toBe(1);
        expect(posts.length).toBe(2);
    }
    finally {
        page.off('request', countSession);
        await page.unroute(matches, handler);
    }
}
// Genuine signed-host gate source only. The disposable fixture must have been
// authored by a different synthetic human THROUGH the normal commands, and this
// signed human must have a real exact reviewer assignment. Missing input fails.
async function assignedQualification(page: Page) {
    // The coordinator command-authors a distinct pending fixture per case. Other
    // qualified source fixtures deliberately coexist and cannot stand in for it.
    const fixtureId = process.env.POSEIDON_BROWSER_QUALIFICATION_RULE;
    if (!fixtureId || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(fixtureId) || fixtureId === '00000000-0000-0000-0000-000000000000')
        throw new Error('qualification_fixture_uuid_required');
    const rules = await page.request.get('/api/v1/rules');
    expect(rules.status()).toBe(200);
    const rows = (await rules.json()).items;
    const selected = rows.find((r: {
        current: {
            id: string;
            rule?: {
                ruleId?: string;
            };
        };
    }) => r.current.id === fixtureId && r.current.rule?.ruleId?.startsWith('SYNTHETIC-FOUNDATION-'));
    expect(!!selected).toBe(true);
    const source = await page.request.get(`/api/v1/regulatory-source-governance/${selected.current.id}`);
    expect(source.status()).toBe(200);
    const q = await source.json();
    expect(q.items[0].state).toBe('IN_REVIEW');
    expect(q.actions).toContain('qualify');
    const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25');
    expect(fleet.status()).toBe(200);
    const vessel = (await fleet.json()).items.find((v: {
        kind: string;
    }) => v.kind === 'Ship');
    expect(!!vessel).toBe(true);
    await page.goto('/app/sources');
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await page.getByRole('button', { name: 'Lifecycle and applicability', exact: true }).click();
    const lifecycle = page.getByRole('region', { name: 'Lifecycle and applicability preparation', exact: true });
    await lifecycle.getByRole('button', { name: new RegExp(selected.current.id) }).first().click();
    const review = page.getByRole('region', { name: 'Source qualification and policy review', exact: true });
    await review.getByRole('combobox', { name: 'Record lifecycle state', exact: true }).selectOption('IN_FORCE');
    await qualificationRecovery(page, review, selected.current.id, q.items[0].revision);
    await lifecycle.getByRole('combobox', { name: 'Vessel context', exact: true }).selectOption(vessel.id);
    await lifecycle.getByRole('checkbox', { name: new RegExp(selected.current.id) }).check();
    const evaluated = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/applicability/evaluate'));
    await lifecycle.getByRole('button', { name: 'Evaluate qualified current sources', exact: true }).click();
    const response = await evaluated;
    expect(response.status()).toBe(200);
    const result = await response.json();
    expect(result.authoritative).toBe(true);
    expect(result.simulation).toBe(false);
    await lifecycle.getByRole('button', { name: 'Check current validity', exact: true }).click();
    await expect(lifecycle.getByText('Inputs remain current. This is not safety release.', { exact: true })).toBeVisible();
    await page.context().setOffline(true);
    try {
        await expect(lifecycle.getByRole('button', { name: 'Evaluate qualified current sources', exact: true })).toBeDisabled();
    }
    finally {
        await page.context().setOffline(false);
    }
}
async function deniedQualification(page: Page) {
    const response = await page.request.get('/api/v1/regulatory-source-governance/10000000-0000-4000-8000-000000000003');
    expect(response.status()).toBe(403);
    const body = await response.json();
    expect(body.items).toBeUndefined();
    expect(body.owners).toBeUndefined();
}
// Genuine signed-host ordinary profile capture. Requires a disposable scoped
// human editor; never changes account/authority configuration or qualifies a record.
async function owningProfileCapture(page: Page) {
    const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25');
    expect(fleet.status()).toBe(200);
    const vessel = (await fleet.json()).items.find((v: {
        kind: string;
        canEdit: boolean;
    }) => v.kind === 'Ship' && v.canEdit);
    expect(!!vessel).toBe(true);
    await page.goto(`/app/fleet/${vessel.id}/profile`);
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    const profile = page.getByRole('region', { name: 'Regulatory profile draft', exact: true });
    await profile.getByLabel('Main propulsion power (kW) — Data state', { exact: true }).selectOption('Known');
    await profile.getByLabel('Main propulsion power (kW)', { exact: true }).fill('3600.000001');
    await profile.getByLabel('Construction contract date (YYYY-MM-DD) — Data state', { exact: true }).selectOption('Unknown');
    const waiting = page.waitForResponse(r => r.request().method() === 'PUT' && r.url().endsWith(`/vessels/${vessel.id}/regulatory-profile`));
    await profile.getByRole('button', { name: 'Save draft', exact: true }).click();
    const saved = await waiting;
    expect(saved.status()).toBe(200);
    const result = await saved.json();
    expect(result.content.mainPropulsionKw).toEqual({ state: 'Known', value: '3600.000001' });
    expect(result.content.contractForConstructionDate).toEqual({ state: 'Unknown', value: null });
    await page.reload();
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await expect(profile.getByLabel('Main propulsion power (kW)', { exact: true })).toHaveValue('3600.000001');
    await expect(profile.getByLabel('Construction contract date (YYYY-MM-DD) — Data state', { exact: true })).toHaveValue('Unknown');
    const history = await page.request.get(`/api/v1/vessels/${vessel.id}/regulatory-profile/history?offset=0&limit=25`);
    expect(history.status()).toBe(200);
    expect((await history.json()).items[0].content.mainPropulsionKw.value).toBe('3600.000001');
}
export const regulatoryAdmissionCases = [
    { name: 'owning-power-and-independent-construction-date-draft-signed-host', run: owningProfileCapture, restricted: false },
    { name: 'synthetic-assigned-human-source-qualification-current-evaluation-signed-host', run: assignedQualification, restricted: false },
    { name: 'source-qualification-denied-before-lineage-or-owner-disclosure', run: deniedQualification, restricted: true },
] as const;
