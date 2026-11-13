import { expect, type Page, type Request } from '@playwright/test';
// Genuine signed-host journeys. No request interception, test login or synthetic
// successful authorization in runtime. Operator appointments are gate prerequisites.
async function scopedTrace(page: Page) {
    // A pending trace and the independently qualified evidence fixtures coexist.
    // Require this case's actual command-produced identity, never an arbitrary row.
    const fixtureId = process.env.POSEIDON_BROWSER_HUMAN_EVIDENCE;
    if (!fixtureId || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(fixtureId) || fixtureId === '00000000-0000-0000-0000-000000000000')
        throw new Error('human_evidence_fixture_uuid_required');
    await page.goto('/app/sources');
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click();
    const region = page.getByRole('region', { name: 'Controls and evidence', exact: true });
    const fleet = await page.request.get('/api/v1/vessels?offset=0&limit=25');
    expect(fleet.status()).toBe(200);
    const vessel = (await fleet.json()).items.find((v: {
        kind: string;
    }) => v.kind === 'Ship');
    expect(!!vessel).toBe(true);
    await region.getByRole('combobox', { name: 'Vessel context', exact: true }).selectOption(vessel.id);
    await region.getByRole('combobox', { name: 'Register', exact: true }).selectOption('Evidence');
    const response = await page.request.get(`/api/v1/evidence?vesselId=${vessel.id}&offset=0`);
    expect(response.status()).toBe(200);
    const evidence = (await response.json()).items.find((e: {
        id: string;
        links: {
            kind: string;
        }[];
    }) => e.id === fixtureId && e.links.some(l => l.kind === 'Control'));
    // Missing fixture is an unmet prerequisite, never a skip or fabricated green trace.
    expect(!!evidence).toBe(true);
    await region.getByRole('button', { name: new RegExp(evidence.content.title ?? evidence.id) }).first().click();
    const trace = page.getByRole('region', { name: 'Human review and decision trace', exact: true }).first();
    await expect(trace.getByText('External signature mechanism unavailable', { exact: true })).toHaveCount(0); // No blanket signature requirement is defined for this fixture type.
    // The same reason also explains the Applicability lineage card. Assert the
    // complete blocker item, retaining both truthful explanations in the UI.
    const evaluationBlocker = trace.getByRole('listitem').filter({ hasText: /^Authoritative evaluation unavailable$/ });
    await expect(evaluationBlocker).toHaveCount(1);
    await expect(evaluationBlocker).toBeVisible();
    const reason = trace.getByRole('textbox', { name: 'Bound review reason', exact: true });
    await reason.focus();
    await expect(reason).toBeFocused();
    await reason.fill('FICTIONAL deferred signed-host review draft');
    await region.getByRole('combobox', { name: 'Register', exact: true }).selectOption('Control');
    await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click();
    await expect(reason).toHaveValue('FICTIONAL deferred signed-host review draft');
    let humanMutationAttempts = 0;
    const observeMutation = (request: Request) => {
        if (request.method() === 'POST' && /^\/api\/v1\/(?:verifications|decisions)(?:\/|$)/.test(new URL(request.url()).pathname))
            humanMutationAttempts++;
    };
    page.on('request', observeMutation);
    try {
        await page.context().setOffline(true);
        try {
            await expect(trace.getByText('Offline. Human review requires server connectivity.', { exact: true })).toBeVisible();
            // Current authority is withdrawn offline (11.01 Q, 13.04 paragraphs23/25).
            await expect(trace.getByRole('button', { name: 'Record pending request', exact: true })).toHaveCount(0);
            await expect(trace.getByRole('button', { name: 'Record authorized human decision', exact: true })).toHaveCount(0);
            await expect(trace.getByRole('combobox', { name: 'Provisioned authority', exact: true })).toHaveCount(0);
            await expect(trace.getByRole('button', { name: 'Discard local review draft', exact: true })).toBeVisible();
            expect(humanMutationAttempts).toBe(0);
        }
        finally {
            await page.context().setOffline(false);
        }
        // The form returns only after the real trace/current-rights request completes.
        await expect(reason).toHaveValue('FICTIONAL deferred signed-host review draft');
        await expect(evaluationBlocker).toHaveCount(1);
        await expect(evaluationBlocker).toBeVisible();
        expect(humanMutationAttempts).toBe(0);
    }
    finally {
        page.off('request', observeMutation);
    }
    await trace.getByRole('button', { name: 'Discard local review draft', exact: true }).click();
    await expect(trace.getByRole('button', { name: /approve|release permit/i })).toHaveCount(0);
    return trace;
}
async function assignedRecording(page: Page) {
    // Later gate only: disposable synthetic evidence + genuine signed-host assigned
    // human membership with exact recording policy. No operational decision fixture.
    const trace = await scopedTrace(page);
    const choose = trace.getByRole('button', { name: 'Record authorized human decision', exact: true }).first();
    await expect(choose).toBeVisible();
    await choose.click();
    const authority = trace.getByRole('combobox', { name: 'Provisioned authority', exact: true });
    const option = authority.locator('option').filter({ hasText: 'human.decide' }).first();
    await expect(option).toHaveCount(1); // Missing genuine assignment fails the gate.
    await authority.selectOption(await option.getAttribute('value') ?? '');
    await trace.getByRole('textbox', { name: 'Bound review reason', exact: true }).fill('FICTIONAL bundle software journey only');
    await trace.getByRole('textbox', { name: 'Human decision text', exact: true }).fill('FICTIONAL assigned recording; no safety approval');
    await trace.getByRole('textbox', { name: 'Decision context', exact: true }).fill('Synthetic incomplete evidence, pending qualification');
    await trace.getByRole('textbox', { name: 'Trigger source', exact: true }).fill('FICTIONAL foundation gate fixture');
    const saved = page.waitForResponse(r => r.request().method() === 'POST' && r.url().includes('/api/v1/decisions?'));
    await trace.getByRole('button', { name: 'Save human decision record', exact: true }).click();
    const response = await saved;
    expect(response.status()).toBe(200);
    const row = await response.json();
    expect(row.state).toBe('RECORDED');
    expect(row.recording.qualification).toBe('UNASSESSED');
    await expect(trace.getByText('Qualification pending. No legal signature, source adoption or permit activation.', { exact: true })).toBeVisible();
}
async function restrictedTrace(page: Page) {
    const query = '?vesselId=10000000-0000-4000-8000-000000000001&evidenceId=10000000-0000-4000-8000-000000000002&evidenceRevision=1&controlId=10000000-0000-4000-8000-000000000003&controlRevision=1';
    for (const route of ['regulatory-trace', 'verification-gates', 'audit-ready']) {
        const response = await page.request.get('/api/v1/' + route + query);
        expect(response.status()).toBe(403);
        const value = await response.json();
        expect(value.items).toBeUndefined();
        expect(value.count).toBeUndefined();
        expect(value.authorities).toBeUndefined();
    }
}
// HRO07: actual assigned recording policy only. Missing evidence/appointment is
// a hard prerequisite failure, never a provider switch or fabricated success.
async function controlledRecordingLoss(page: Page) {
    const trace = await scopedTrace(page);
    const verification = trace.getByRole('article').filter({ has: page.getByRole('heading', { name: /^Verification ·/ }) }).first();
    await expect(verification).toBeVisible();
    await verification.getByRole('button', { name: 'Record authorized human decision', exact: true }).click();
    const authority = trace.getByRole('combobox', { name: 'Provisioned authority', exact: true });
    const option = authority.locator('option').filter({ hasText: 'human.decide' }).first();
    await expect(option).toHaveCount(1);
    await authority.selectOption(await option.getAttribute('value') ?? '');
    await trace.getByRole('textbox', { name: 'Bound review reason', exact: true }).fill('FICTIONAL controlled recovery software journey');
    await trace.getByRole('textbox', { name: 'Human decision text', exact: true }).fill('FICTIONAL retained text; no operational approval');
    await trace.getByRole('textbox', { name: 'Decision context', exact: true }).fill('Synthetic existing assignment, pending qualification');
    await trace.getByRole('textbox', { name: 'Trigger source', exact: true }).fill('FICTIONAL response-loss regression');
    let sessions = 0, postCount = 0, failOptionalRead = false, failedReads = 0, traceUrl = '';
    let savedId = '', savedRevision = 0;
    const attempts: { body: string; key: string; path: string; sessions: number }[] = [];
    const observe = (request: Request) => {
        const url = new URL(request.url());
        if (request.method() === 'GET' && url.pathname === '/api/v1/auth/session') sessions++;
        if (request.method() === 'POST' && /^\/api\/v1\/decisions(?:\/[0-9a-f-]+\/events)?$/.test(url.pathname))
            attempts.push({ body: request.postData() ?? '', key: request.headers()['idempotency-key'] ?? '', path: url.pathname + url.search, sessions });
    };
    const postPattern = '**/api/v1/decisions**', readPattern = '**/api/v1/regulatory-trace?**';
    const interceptPost = async (route: import('@playwright/test').Route) => {
        if (route.request().method() !== 'POST') { await route.continue(); return; }
        const response = await route.fetch(); // REAL authenticated transaction, including current CSRF/authority.
        try {
            expect(response.status()).toBe(200);
            const row = await response.json();
            expect(row.state === 'RECORDED' && row.recording?.qualification === 'UNASSESSED').toBe(true);
            if (++postCount === 1) {
                savedId = row.id; savedRevision = row.revision;
                expect(typeof savedId === 'string' && savedId.length === 36 && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(savedId) && savedRevision === 1).toBe(true);
                await route.abort('connectionreset'); // Committed response intentionally lost.
            } else {
                expect(row.id === savedId && row.revision === savedRevision).toBe(true);
                failOptionalRead = true;
                await route.fulfill({ response });
            }
        } finally { await response.dispose(); }
    };
    const interceptRead = async (route: import('@playwright/test').Route) => {
        traceUrl = route.request().url();
        if (failOptionalRead) { failOptionalRead = false; failedReads++; await route.abort('connectionreset'); }
        else await route.continue();
    };
    page.on('request', observe);
    await page.route(postPattern, interceptPost); await page.route(readPattern, interceptRead);
    try {
        const beforeFirst = sessions;
        await trace.getByRole('button', { name: 'Save human decision record', exact: true }).click();
        await expect(trace.getByText('Outcome uncertain. The exact original attempt is locked until retry.', { exact: true })).toBeVisible();
        expect(postCount).toBe(1); expect(attempts.length === 1 && attempts[0].sessions > beforeFirst).toBe(true);
        await expect(trace.getByRole('button', { name: 'Discard local review draft', exact: true })).toHaveCount(0);
        await expect(trace.getByRole('button', { name: 'Retry exact human command', exact: true })).toBeDisabled();
        await trace.getByRole('button', { name: 'Retry source read', exact: true }).click();
        await expect(trace.getByRole('button', { name: 'Retry exact human command', exact: true })).toBeEnabled();
        expect(postCount).toBe(1);
        await expect(trace.getByRole('textbox', { name: 'Human decision text', exact: true })).toBeDisabled();
        const beforeRetry = sessions;
        await trace.getByRole('button', { name: 'Retry exact human command', exact: true }).click();
        await expect(trace.getByText('Draft saved.', { exact: true })).toBeVisible();
        await expect.poll(() => failedReads).toBe(1);
        expect(postCount === 2 && attempts.length === 2 && attempts[1].sessions > beforeRetry).toBe(true);
        // Boolean comparisons keep private command/key/identity bytes out of diagnostics.
        expect(attempts[0].body === attempts[1].body && attempts[0].key === attempts[1].key && attempts[0].path === attempts[1].path).toBe(true);
        expect(!Object.hasOwn(JSON.parse(attempts[0].body), 'id') && attempts[0].key.length === 36).toBe(true);
        await expect(trace.getByRole('button', { name: 'Retry exact human command', exact: true })).toHaveCount(0);
        await trace.getByRole('button', { name: 'Retry source read', exact: true }).click();
        await expect(trace.getByRole('textbox', { name: 'Human decision text', exact: true })).toBeDisabled();
        await expect(trace.getByRole('button', { name: 'Save human decision record', exact: true })).toBeDisabled();
        const actual = await page.request.get(traceUrl);
        expect(actual.status()).toBe(200);
        const items = (await actual.json()).items.filter((row: { id: string }) => row.id === savedId);
        expect(items.length === 1 && items[0].revision === savedRevision && items[0].state === 'RECORDED').toBe(true);
        expect(postCount).toBe(2);
    } finally {
        page.off('request', observe);
        await page.unroute(postPattern, interceptPost); await page.unroute(readPattern, interceptRead);
    }
}
export const humanReviewCases = [
    { name: 'assigned-human-recording-signed-host-pending-qualification', run: assignedRecording, restricted: false },
    { name: 'human-scoped-trace-source-signature-blockers-keyboard-dirty-offline', run: scopedTrace, restricted: false },
    { name: 'human-trace-denied-before-object-inference', run: restrictedTrace, restricted: true },
    { name: 'human-recording-controlled-response-loss-real-commit', run: controlledRecordingLoss, restricted: false },
] as const;
