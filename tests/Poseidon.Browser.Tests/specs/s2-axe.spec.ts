import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
// DEV025-AC-12 / T-S2-006 (65.03 paragraphs 46-48, 71-90, 257-295, 570-578). Automated axe-core
// rule-engine gate over the key S2 shell states. It complements, and does not replace, the
// in-page structural checks in s2-accessibility.spec.ts and a real assistive-technology pass.
// Synthetic transport proves browser behavior only, never server authorization.
const tenantId = '10000000-0000-0000-0000-000000000098';
const actorId = '10000000-0000-0000-0000-000000000099';
const vessel = { id: '30000000-0000-0000-0000-000000000001', companyId: tenantId, code: 'SYN-A', name: 'FICTIONAL Alpha with a deliberately long synthetic vessel name', kind: 'Ship', version: 1, canEdit: true };
const roles = [
    { reference: '20000000-0000-0000-0000-000000000001', tr: 'KURGUSAL kaptan', en: 'FICTIONAL Master' },
    { reference: '20000000-0000-0000-0000-000000000002', tr: 'KURGUSAL başmühendis', en: 'FICTIONAL Engineer' },
];
// WCAG 2.0/2.1 A+AA and WCAG 2.2 AA rule tags. No rule is disabled: an exclusion must be explicit,
// minimal, justified here and tracked in docs/tasks/s2-dev025-01.md.
const wcagTags = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

async function mockShell(page: Page) {
    await page.route('**/api/v1/**', async (route) => {
        const request = route.request(), path = new URL(request.url()).pathname;
        if (request.method() !== 'GET') {
            await route.fulfill({ status: 405, json: { code: 'unexpected_mutation' } });
            return;
        }
        if (path.endsWith('/auth/session')) {
            await route.fulfill({ json: { authenticated: true, tenantId, actorId, tenantName: 'FICTIONAL workspace',
                    canRead: true, canReadCompanies: false, canCreate: false, canSearch: true, csrfToken: 'synthetic-browser-only' } });
            return;
        }
        if (path === '/api/v1/workspace') {
            await route.fulfill({ json: { destinations: [{ key: 'fleet', href: '/app/fleet' }, { key: 'today', href: '/app/today' }], canSearch: true, canWriteRegulatory: false } });
            return;
        }
        if (path === '/api/v1/workspace/search') {
            await route.fulfill({ json: { items: [], offset: 0, limit: 25, hasMore: false } });
            return;
        }
        if (path.startsWith('/api/v1/workspace/context')) {
            const selected = path.endsWith('/' + vessel.id);
            await route.fulfill({ json: { tenantId, actorId, roleContext: { state: 'Configured', catalogVersion: 'synthetic-a11y-v1', roles },
                    company: selected ? { id: vessel.companyId, code: 'SYN-CO', name: 'FICTIONAL company' } : null,
                    vessel: selected ? { id: vessel.id, companyId: vessel.companyId, code: vessel.code, name: vessel.name, kind: vessel.kind } : null } });
            return;
        }
        if (path.startsWith('/api/v1/workspace/lenses')) {
            const vesselId = path.endsWith('/' + vessel.id) ? vessel.id : null;
            await route.fulfill({ json: { tenantId, actorId, vesselId, mode: vesselId ? 'Ship' : 'Fleet', state: 'Configured', version: 'synthetic-a11y-v1', canSearch: true,
                    lenses: [{ ...roles[0], items: [{ key: 'today', href: '/app/today' + (vesselId ? '/' + vesselId : ''), group: 'daily' }, { key: 'fleet', href: '/app/fleet', group: 'daily' }] }] } });
            return;
        }
        if (path === '/api/v1/vessels') {
            await route.fulfill({ json: { items: [vessel], offset: 0, limit: 25, hasMore: false } });
            return;
        }
        if (path === '/api/v1/vessels/' + vessel.id) {
            await route.fulfill({ json: vessel });
            return;
        }
        if (path === '/api/v1/poseidon-today') {
            await route.fulfill({ json: { vessel, readAt: '2026-11-04T12:00:00Z',
                    coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
                        .map((source, i) => ({ source, status: i === 0 ? 'Missing' : 'Unavailable' })),
                    cards: { items: [], offset: 0, limit: 25, hasMore: false } } });
            return;
        }
        await route.fulfill({ status: 404, json: { code: 'unconfigured_synthetic_read' } });
    });
}

async function open(page: Page, locale = 'en', mode: 'light' | 'dark' = 'light', route: string = `/app/today/${vessel.id}`) {
    await page.addInitScript(([value, appearance]) => {
        localStorage.setItem('poseidon.locale', value);
        localStorage.setItem('poseidon.appearance.v2', JSON.stringify({ v: 2, mode: appearance, palette: 'emerald' }));
    }, [locale, mode] as const);
    // Animations would let axe sample mid-transition colours; the reduced-motion surface is covered elsewhere.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await mockShell(page);
    await page.goto(route);
    await expect(page.locator('html')).toHaveAttribute('lang', locale);
    if (route.endsWith('/' + vessel.id))
        await expect(page.locator('.workspace-global-context .context-live')).toContainText(vessel.code);
    else
        // Fleet list and the no-vessel Today chooser both render the synthetic vessel row once loaded.
        await expect(page.locator('#product-main')).toContainText(vessel.code);
}

async function expectNoViolations(page: Page, state: string) {
    const results = await new AxeBuilder({ page }).withTags(wcagTags).analyze();
    // "incomplete" = axe could not decide (e.g. contrast over gradients); recorded for manual review, never a pass or a failure.
    if (results.incomplete.length > 0)
        await test.info().attach(`axe-incomplete-${state.replace(/[^a-z0-9]+/gi, '-')}.json`, { contentType: 'application/json',
            body: JSON.stringify(results.incomplete.map(v => ({ id: v.id, impact: v.impact, help: v.help,
                nodes: v.nodes.slice(0, 5).map(n => ({ target: n.target.join(' '), summary: n.failureSummary })) })), null, 2) });
    const summary = results.violations.map(v => ({ id: v.id, impact: v.impact, help: v.help,
        nodes: v.nodes.slice(0, 5).map(n => ({ target: n.target.join(' '), summary: n.failureSummary })) }));
    expect(summary, `axe WCAG A/AA violations in ${state}`).toEqual([]);
}

test.describe('DEV-025 axe-core WCAG gate', () => {
    test('default desktop shell', async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await open(page);
        await expectNoViolations(page, 'default desktop');
    });

    test('320 px narrow shell with the context summary expanded', async ({ page }) => {
        await page.setViewportSize({ width: 320, height: 640 });
        await open(page);
        await expectNoViolations(page, '320 px collapsed summary');
        const summary = page.locator('.context-summary-toggle');
        await expect(summary).toBeVisible();
        await summary.click();
        await expect(summary).toHaveAttribute('aria-expanded', 'true');
        await expectNoViolations(page, '320 px expanded summary');
    });

    test('Arabic RTL shell', async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await open(page, 'ar');
        await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
        await expectNoViolations(page, 'Arabic RTL');
    });

    test('theme builder dialog open', async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await open(page);
        await page.locator('.appearance-controls > summary').click();
        await page.getByRole('button', { name: 'Customize…' }).click();
        await expect(page.getByRole('dialog', { name: 'Theme builder' })).toBeVisible();
        await expectNoViolations(page, 'theme builder open');
    });

    test('fleet list', async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await open(page, 'en', 'light', '/app/fleet');
        await expectNoViolations(page, 'fleet list');
    });

    test('Today with no vessel selected', async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await open(page, 'en', 'light', '/app/today');
        await expect(page.locator('.workspace-global-context .context-live')).not.toContainText(vessel.code);
        await expectNoViolations(page, 'no vessel selected');
    });

    test('workspace search dialog open', async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await open(page);
        await page.getByRole('button', { name: 'Search workspace', exact: true }).first().click();
        const dialog = page.getByRole('dialog', { name: 'Search workspace', exact: true });
        await expect(dialog).toBeVisible();
        await expectNoViolations(page, 'search dialog open');
    });

    test('dark mode shell', async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await open(page, 'en', 'dark');
        await expect(page.locator('html')).toHaveClass(/\bdark\b/);
        await expectNoViolations(page, 'dark mode');
    });
});
