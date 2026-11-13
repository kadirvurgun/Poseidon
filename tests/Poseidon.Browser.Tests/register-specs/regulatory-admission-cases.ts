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
// PRO07: ordinary command-created disposable Ship. Shared qualified fixtures,
// witnesses and their profiles are never mutated by these response-loss cases.
async function profileRecovery(page: Page, onboarding: boolean) {
    const sessionResponse = await page.request.get('/api/v1/auth/session'); expect(sessionResponse.status()).toBe(200);
    const session = await sessionResponse.json();
    expect(session.authenticated === true && session.canRead === true && session.canCreate === true && typeof session.csrfToken === 'string').toBe(true);
    const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(fleet.status()).toBe(200);
    const parent = (await fleet.json()).items.find((row: { kind: string; canEdit: boolean; companyId: string }) => row.kind === 'Ship' && row.canEdit && typeof row.companyId === 'string');
    expect(!!parent).toBe(true);
    const created = await page.request.post('/api/v1/vessels', { headers: { 'X-CSRF-TOKEN': session.csrfToken, 'Idempotency-Key': crypto.randomUUID() },
        data: { code: 'FICTIONAL-PRO-'+crypto.randomUUID(), name: 'FICTIONAL isolated profile recovery', kind: 'Ship', companyId: parent.companyId } });
    expect(created.status()).toBe(200);
    const vessel = await created.json();
    expect(typeof vessel.id === 'string' && vessel.id !== parent.id && vessel.kind === 'Ship' && vessel.canEdit === true).toBe(true);
    const path = `/api/v1/vessels/${vessel.id}/regulatory-profile`;
    const before = await page.request.get(path); expect(before.status()).toBe(200);
    const initial = await before.json(); expect(initial.revision === 0 && initial.version === 0 && initial.provenance === 'Missing').toBe(true);
    await page.goto(`/app/fleet/${vessel.id}/${onboarding ? 'onboarding' : 'profile'}`);
    await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption('en');
    const region = page.getByRole('region', { name: onboarding ? 'Pilot vessel onboarding' : 'Regulatory profile draft', exact: true });
    await region.getByLabel('Flag — Data state', { exact: true }).selectOption('Unknown');
    const writes: { body: string; key: string; path: string; method: string; sessions: number }[] = [];
    let sessions = 0, dropHistory = false, lostHistory = false, committedRevision = 0;
    const countSession = (request: Request) => { if (request.method() === 'GET' && new URL(request.url()).pathname === '/api/v1/auth/session') sessions++; };
    const matches = (url: URL) => url.pathname === path || url.pathname === path+'/history';
    const handler = async (route: Route) => {
        const request = route.request(), pathname = new URL(request.url()).pathname;
        if (request.method() === 'GET' && pathname === path+'/history' && dropHistory) { dropHistory = false; lostHistory = true; await route.abort('failed'); return; }
        if (request.method() !== 'PUT' || pathname !== path) { await route.continue(); return; }
        writes.push({ body: request.postData() ?? '', key: request.headers()['idempotency-key'] ?? '', path: pathname, method: request.method(), sessions });
        const actual = await route.fetch(); expect(actual.status()).toBe(200);
        const result = await actual.json();
        expect(result.vessel.id === vessel.id && result.tenantId === session.tenantId && result.revision === 1 && result.content.flag.state === 'Unknown' && result.provenance === 'Missing').toBe(true);
        if (writes.length === 1) { committedRevision = result.revision; await route.abort('failed'); }
        else {
            expect(result.revision === committedRevision && actual.headers()['idempotency-replayed'] === 'true').toBe(true);
            dropHistory = true; await route.fulfill({ response: actual }); // Forward ONLY the actual host replay response.
        }
    };
    page.on('request', countSession); await page.route(matches, handler);
    try {
        const location = page.url(), firstSession = sessions;
        await region.getByRole('button', { name: 'Save draft', exact: true }).click();
        await expect(region.getByText('The save outcome is unknown. Fields are frozen; retry the same command before leaving.', { exact: true })).toBeVisible();
        expect(writes.length === 1 && writes[0].sessions > firstSession).toBe(true);
        await expect(page.getByRole('button', { name: 'Back to vessel', exact: true })).toBeDisabled();
        await expect(region.getByRole('button', { name: 'Discard local changes', exact: true })).toHaveCount(0);
        await region.getByRole('button', { name: 'Retry source read', exact: true }).click();
        await expect(region.getByRole('button', { name: 'Retry same command', exact: true })).toBeEnabled();
        expect(writes.length).toBe(1);
        const beforeRetry = sessions;
        await region.getByRole('button', { name: 'Retry same command', exact: true }).click();
        await expect(region.getByText('Draft saved.', { exact: true })).toBeVisible();
        await expect(region.getByRole('alert')).toBeVisible();
        expect(lostHistory && writes.length === 2 && writes[1].sessions > beforeRetry).toBe(true);
        expect(writes[0].body === writes[1].body && writes[0].key === writes[1].key && writes[0].key.length === 36 && writes[0].path === writes[1].path && writes[1].method === 'PUT').toBe(true);
        await expect(region.getByLabel('Flag — Data state', { exact: true })).toBeDisabled();
        await expect(region.getByRole('button', { name: 'Save draft', exact: true })).toHaveCount(0);
        expect(page.url() === location).toBe(true);
        await region.getByRole('button', { name: 'Retry source read', exact: true }).click();
        await expect(region.getByRole('button', { name: 'Discard local changes', exact: true })).toBeEnabled();
        await expect(region.getByLabel('Flag — Data state', { exact: true })).toBeDisabled();
        const history = await page.request.get(path+'/history?offset=0&limit=25'); expect(history.status()).toBe(200);
        const rows = (await history.json()).items;
        expect(rows.length === 1 && rows[0].revision === committedRevision && rows[0].content.flag.state === 'Unknown' && writes.length === 2).toBe(true);
    } finally { page.off('request', countSession); await page.unroute(matches, handler); }
}
export const regulatoryAdmissionCases = [
    { name: 'profile-controlled-response-loss-real-commit', run: (page: Page) => profileRecovery(page, false), restricted: false },
    { name: 'onboarding-profile-controlled-response-loss-real-commit', run: (page: Page) => profileRecovery(page, true), restricted: false },
    { name: 'owning-power-and-independent-construction-date-draft-signed-host', run: owningProfileCapture, restricted: false },
    { name: 'synthetic-assigned-human-source-qualification-current-evaluation-signed-host', run: assignedQualification, restricted: false },
    { name: 'source-qualification-denied-before-lineage-or-owner-disclosure', run: deniedQualification, restricted: true },
] as const;
