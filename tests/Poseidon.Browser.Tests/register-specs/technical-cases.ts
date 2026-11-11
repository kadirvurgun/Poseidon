import { expect, type Page, type Request, type Route } from '@playwright/test';
// Real private HTTPS/signed-provider/PostgreSQL runner. The explicitly named
// response-loss case injects transport faults only after actual host responses.
// At the bundle gate, provision the disposable writer's explicit equipment/maintenance
// read/write/history and maintenance.owner.eligible capabilities. Restricted user has none.
async function context(page: Page) {
    const response = await page.request.get('/api/v1/vessels?limit=25');
    expect(response.status() === 200).toBe(true);
    const rows = await response.json();
    expect(rows.items.length > 0).toBe(true);
    return rows.items[0].id as string;
}
async function hierarchyPlan(page: Page) {
    const vessel = await context(page);
    await page.goto(`/app/equipment-hierarchy/${vessel}`);
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await page.getByRole('button', { name: 'Add draft', exact: true }).click();
    const label = 'FICTIONAL-EQUIPMENT-' + crypto.randomUUID();
    await page.getByLabel('Equipment name', { exact: true }).fill(label);
    const save = page.waitForResponse(r => r.url().endsWith(`/equipment-hierarchy/${vessel}`) && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    const response = await save;
    expect(response.status() === 200).toBe(true);
    const root = await response.json();
    await expect(page.getByRole('treeitem', { name: label, exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Add draft', exact: true }).click();
    await page.getByLabel('Equipment name', { exact: true }).fill(label + '-CHILD');
    await page.getByRole('combobox', { name: 'Parent', exact: true }).selectOption(root.id);
    const childSave = page.waitForResponse(r => r.url().endsWith(`/equipment-hierarchy/${vessel}`) && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    const childResponse = await childSave;
    expect(childResponse.status() === 200).toBe(true);
    const child = await childResponse.json();
    await expect(page).toHaveURL(new RegExp(`id=${child.id}$`));
    const parentItem = page.getByRole('treeitem', { name: label, exact: true }), childItem = page.getByRole('treeitem', { name: label + '-CHILD', exact: true });
    await expect(parentItem).toHaveAttribute('aria-expanded', 'true');
    const groupId = await parentItem.getAttribute('aria-owns'), childDomId = await childItem.getAttribute('id');
    expect(groupId !== null && childDomId !== null).toBe(true);
    await expect(page.locator(`[id="${groupId}"]`)).toHaveAttribute('aria-owns', new RegExp(childDomId!));
    await childItem.focus();
    await page.keyboard.press('ArrowLeft');
    await expect(parentItem).toBeFocused();
    await page.keyboard.press('End');
    await page.keyboard.type('F');
    await expect(page.locator('[role=treeitem]:focus')).toHaveCount(1);
    await page.getByRole('button', { name: 'Plans', exact: true }).click();
    await page.getByRole('button', { name: 'Add draft', exact: true }).click();
    await page.getByRole('combobox', { name: 'Equipment', exact: true }).selectOption(child.id);
    await page.getByRole('combobox', { name: 'Trigger', exact: true }).selectOption('Meter');
    await page.getByLabel('Interval value (exact decimal)', { exact: true }).fill('0.100000000000000002');
    await page.getByLabel('Time zone (IANA)', { exact: true }).fill('UTC');
    await page.getByLabel('Input provenance', { exact: true }).fill('FICTIONAL manual declaration');
    const planSave = page.waitForResponse(r => r.url().endsWith(`/maintenance-plan/${vessel}`) && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    const planResponse = await planSave;
    expect(planResponse.status() === 200).toBe(true);
    const plan = await planResponse.json();
    const persisted = await (await page.request.get(`/api/v1/maintenance-plan/${vessel}`)).json();
    const row = persisted.items.find((p: {
        id: string;
    }) => p.id === plan.id);
    expect(row.inputs.intervalValue === '0.100000000000000002' && row.inputs.currentMeter === null && row.due.state === 'ReviewRequired').toBe(true);
    const currentDetail = page.locator('.technical-read-detail');
    await expect(currentDetail).toContainText('Current planning calculation.');
    const inputsDisclosure = currentDetail.locator('summary').filter({ hasText: 'All recorded plan inputs' });
    await inputsDisclosure.focus();
    await page.keyboard.press('Enter');
    await expect(currentDetail.getByText('0.100000000000000002', { exact: true })).toBeVisible();
    const previousViewport = page.viewportSize();
    try {
        await page.setViewportSize({ width: 390, height: 844 });
        await expect(page.getByRole('button', { name: 'Edit draft', exact: true })).toBeVisible();
        expect(await currentDetail.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    }
    finally {
        if (previousViewport)
            await page.setViewportSize(previousViewport);
    }
    await page.getByRole('button', { name: 'Edit draft', exact: true }).click();
    await page.getByLabel('Interval value (exact decimal)', { exact: true }).fill('1.2');
    await page.getByRole('button', { name: 'Equipment', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Stay', exact: true }).click();
    await expect(page.getByLabel('Interval value (exact decimal)', { exact: true })).toHaveValue('1.2');
    await page.getByRole('button', { name: 'Discard changes', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Discard', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Maintenance plan', exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.getByRole('button', { name: 'Equipment', exact: true }).click();
    await page.goBack();
    await expect(page.getByRole('heading', { name: 'Maintenance plan', exact: true })).toBeVisible();
}
async function denied(page: Page) {
    const vessel = await context(page);
    const r = await page.request.get(`/api/v1/equipment-hierarchy/${vessel}`, { headers: { 'X-Role': 'Chief Engineer' } });
    expect(r.status() === 403).toBe(true);
    const body = await r.text();
    expect(!body.includes('items') && !body.includes(vessel)).toBe(true);
    await page.goto(`/app/equipment-hierarchy/${vessel}`);
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await expect(page.getByText('Access is not available.', { exact: true })).toBeVisible();
    await expect(page.getByRole('tree')).toHaveCount(0);
}
async function controlledResponseLoss(page: Page) {
    // Controlled transport fault, explicitly separate from ordinary real-host journeys.
    // route.fetch still commits through the actual host, TLS, authorization and PostgreSQL.
    const vessel = await context(page), attempts: {
        key: string | undefined;
        body: string | null;
        sessionReads: number;
    }[] = [];
    let lost = false, replayConfirmed = false, readbackLost = false;
    let committedId = '', sessionReads = 0;
    const label = 'FICTIONAL lost response ' + crypto.randomUUID();
    const observeSession = (request: Request) => {
        if (new URL(request.url()).pathname === '/api/v1/auth/session') sessionReads++;
    };
    const routeUrl = `**/api/v1/equipment-hierarchy/${vessel}`;
    const equipmentTransport = async (route: Route) => {
        if (route.request().method() !== 'POST') {
            if (route.request().method() === 'GET' && replayConfirmed && !readbackLost) {
                // Optional readback fails after the known original-owner receipt.
                // No fabricated server row, session or authorization is supplied.
                readbackLost = true;
                return route.abort('connectionreset');
            }
            return route.continue();
        }
        attempts.push({ key: route.request().headers()['idempotency-key'], body: route.request().postData(), sessionReads });
        const actual = await route.fetch();
        expect(actual.status() === 200).toBe(true);
        const result = await actual.json();
        if (!lost) {
            lost = true;
            committedId = result.id;
            expect(result.replayed === false).toBe(true);
            return route.abort('failed');
        }
        expect(result.id === committedId && result.replayed === true).toBe(true);
        replayConfirmed = true;
        return route.fulfill({ response: actual });
    };
    page.on('request', observeSession);
    try {
        await page.route(routeUrl, equipmentTransport);
        await page.goto(`/app/equipment-hierarchy/${vessel}`);
        await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
        await page.getByRole('button', { name: 'Add draft', exact: true }).click();
        await page.getByLabel('Equipment name', { exact: true }).fill(label);
        await page.getByRole('button', { name: 'Save draft', exact: true }).click();
        await expect(page.getByText('Result uncertain. Retry the same attempt before editing.', { exact: true })).toBeVisible();
        await expect(page.getByLabel('Equipment name', { exact: true })).toBeDisabled();
        const beforeRetrySessionReads = sessionReads;
        await page.getByRole('button', { name: 'Retry same attempt', exact: true }).click();
        await expect(page.getByText('Draft saved.', { exact: true })).toBeVisible();
        expect(attempts.length === 2 && attempts[0].key === attempts[1].key && attempts[0].body === attempts[1].body).toBe(true);
        // Capture the counter at POST dispatch: a later post-write identity check
        // cannot satisfy this requirement for a fresh pre-send session read.
        expect(attempts[1].sessionReads > beforeRetrySessionReads).toBe(true);
        const id = JSON.parse(attempts[0].body!).id;
        expect(id === committedId).toBe(true);
        await expect(page).toHaveURL(new RegExp(`/app/equipment-hierarchy/${vessel}\\?id=${id}$`));
        const retryRead = page.getByRole('button', { name: 'Retry source read', exact: true });
        await expect(retryRead).toBeVisible();
        expect(readbackLost).toBe(true);
        // A receipt plus failed current read must not unlock editing or a new UUID.
        await expect(page.getByRole('button', { name: 'Save draft', exact: true })).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Edit draft', exact: true })).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Add draft', exact: true })).toHaveCount(0);
        const snapshot = await (await page.request.get(`/api/v1/equipment-hierarchy/${vessel}`)).json();
        expect(snapshot.items.filter((a: {
            id: string;
        }) => a.id === id).length === 1).toBe(true);
        const historyResponse = await page.request.get(`/api/v1/equipment-hierarchy/${vessel}/${id}/history`);
        expect(historyResponse.status() === 200).toBe(true);
        const history = await historyResponse.json();
        expect(history.length === 1 && history[0].revision === 1).toBe(true);
        await retryRead.click();
        await expect(page.getByRole('treeitem', { name: label, exact: true })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Edit draft', exact: true })).toBeVisible();
        await expect(page).toHaveURL(new RegExp(`/app/equipment-hierarchy/${vessel}\\?id=${id}$`));
        expect(attempts.length).toBe(2); // Explicit source recovery never resends the mutation.
    } finally {
        page.off('request', observeSession);
        await page.unroute(routeUrl, equipmentTransport);
    }
}
export const technicalCases = [
    { name: 'equipment-plan-tree-deep-url-keyboard-dirty-responsive', run: hierarchyPlan, restricted: false },
    { name: 'equipment-restricted-direct-url', run: denied, restricted: true },
    { name: 'equipment-controlled-response-loss-real-commit', run: controlledResponseLoss, restricted: false },
] as const;
