import { test, expect, type Page } from '@playwright/test';
// DEV-025 / T-S2-006 (65.03 paragraphs 46-48, 71-90, 257-295, 570-578). Shell accessibility journey.
// Synthetic transport proves browser behavior only, never server authorization. No axe-core is
// installed in this repo (no new dependency allowed), so `scan` is a bounded in-page heuristic
// check: it is NOT a full WCAG audit and does not replace manual AT testing.
const tenantId = '10000000-0000-0000-0000-000000000098';
const actorId = '10000000-0000-0000-0000-000000000099';
const vessel = { id: '30000000-0000-0000-0000-000000000001', companyId: tenantId, code: 'SYN-A', name: 'FICTIONAL Alpha with a deliberately long synthetic vessel name', kind: 'Ship', version: 1, canEdit: true };
const roles = [
    { reference: '20000000-0000-0000-0000-000000000001', tr: 'KURGUSAL kaptan', en: 'FICTIONAL Master' },
    { reference: '20000000-0000-0000-0000-000000000002', tr: 'KURGUSAL başmühendis', en: 'FICTIONAL Engineer' },
];
async function mockShell(page: Page) {
    await page.route('**/api/v1/**', async (route) => {
        const request = route.request(), url = new URL(request.url()), path = url.pathname;
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
async function open(page: Page, locale = 'en') {
    await page.addInitScript(value => localStorage.setItem('poseidon.locale', value), locale);
    await mockShell(page);
    await page.goto(`/app/today/${vessel.id}`);
    await expect(page.locator('html')).toHaveAttribute('lang', locale);
    await expect(page.locator('.workspace-global-context .context-live')).toContainText(vessel.code);
}
type Scan = { unnamed: string[]; nestedLive: string[]; duplicateIds: string[]; mains: number; banners: number; navNames: string[]; overflow: number };
// Heuristic structural scan of the visible page (not axe-core).
async function scan(page: Page): Promise<Scan> {
    return page.evaluate(() => {
        const visible = (el: Element) => { const box = (el as HTMLElement).getBoundingClientRect(); const style = getComputedStyle(el); return style.visibility !== 'hidden' && style.display !== 'none' && (box.width > 0 || box.height > 0); };
        const name = (el: Element) => {
            const labelledby = el.getAttribute('aria-labelledby');
            const byRef = labelledby ? labelledby.split(/\s+/).map(id => document.getElementById(id)?.textContent ?? '').join(' ') : '';
            const labels = (el as HTMLInputElement).labels ? Array.from((el as HTMLInputElement).labels ?? []).map(label => label.textContent ?? '').join(' ') : '';
            return (el.getAttribute('aria-label') ?? '') + byRef + labels + (el.textContent ?? '') + (el.getAttribute('title') ?? '') + Array.from(el.querySelectorAll('img[alt]')).map(img => img.getAttribute('alt')).join('');
        };
        const describe = (el: Element) => `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''}.${(el.getAttribute('class') ?? '').split(' ').join('.')}`;
        const controls = Array.from(document.querySelectorAll('button, a[href], [role="button"], select, input:not([type="hidden"]), textarea, summary')).filter(visible);
        const unnamed = controls.filter(el => name(el).trim() === '').map(describe);
        const live = Array.from(document.querySelectorAll('[aria-live]:not([aria-live="off"]), [role="status"], [role="alert"], [role="log"]'));
        const nestedLive = live.filter(el => live.some(other => other !== el && other.contains(el))).map(describe);
        const ids = Array.from(document.querySelectorAll('[id]')).map(el => el.id);
        const duplicateIds = [...new Set(ids.filter((id, index) => ids.indexOf(id) !== index))];
        const mains = Array.from(document.querySelectorAll('main, [role="main"]')).filter(visible).length;
        const banners = Array.from(document.querySelectorAll('header')).filter(el => !el.closest('article, aside, main, nav, section, [role="dialog"], dialog') && visible(el)).length;
        const navNames = Array.from(document.querySelectorAll('nav, [role="navigation"]')).filter(visible).map(el => el.getAttribute('aria-label') ?? (el.getAttribute('aria-labelledby') ? document.getElementById(el.getAttribute('aria-labelledby')!)?.textContent ?? '' : ''));
        return { unnamed, nestedLive, duplicateIds, mains, banners, navNames, overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth };
    });
}
function expectClean(result: Scan) {
    expect(result.unnamed, 'every visible control has an accessible name').toEqual([]);
    expect(result.nestedLive, 'live regions are not nested').toEqual([]);
    expect(result.duplicateIds, 'ids are unique').toEqual([]);
    expect(result.mains).toBe(1);
    expect(result.banners).toBe(1);
    expect(new Set(result.navNames).size, 'repeated navigation landmarks have distinct names').toBe(result.navNames.length);
    expect(result.overflow, 'no horizontal page scroll').toBeLessThanOrEqual(0);
}

test.describe('DEV-025 shell keyboard and dialog journey', () => {
    test.use({ viewport: { width: 1280, height: 800 } });
    test('skip link, landmarks, names, theme builder inert background and focus return', async ({ page }) => {
        await open(page);
        expectClean(await scan(page));
        // DEV025-AC-01 skip link is the first tab stop, visible on focus, and moves focus to main.
        await page.keyboard.press('Tab');
        const skip = page.locator('.skip-link');
        await expect(skip).toBeFocused();
        const box = await skip.boundingBox();
        expect(box && box.y >= 0 && box.height > 0).toBe(true);
        await page.keyboard.press('Enter');
        await expect(page.locator('main')).toBeFocused();
        // DEV025-AC-04 theme builder: modal, background inert, Tab stays inside, Escape returns focus.
        const appearance = page.locator('.appearance-controls > summary');
        await appearance.focus();
        await page.keyboard.press('Enter');
        const customize = page.getByRole('button', { name: 'Customize…' });
        await customize.focus();
        await page.keyboard.press('Enter');
        const builder = page.getByRole('dialog', { name: 'Theme builder' });
        await expect(builder).toBeVisible();
        await expect(builder.getByRole('heading', { name: 'Theme builder' })).toBeFocused();
        expect(await page.evaluate(() => [document.querySelector('header.enterprise-topbar'), document.querySelector('main'), document.querySelector('#workspace-navigation')]
            .every(el => !!el && !!el.closest('[inert]')))).toBe(true);
        for (let i = 0; i < 60; i++) {
            await page.keyboard.press('Tab');
            expect(await page.evaluate(() => !!document.activeElement?.closest('[role="dialog"]'))).toBe(true);
        }
        expect(await page.evaluate(() => document.activeElement?.getAttribute('tabindex'))).not.toBe('-1');
        await page.keyboard.press('Escape');
        await expect(builder).toBeHidden();
        await expect(customize).toBeFocused();
        expect(await page.evaluate(() => document.querySelectorAll('[inert]').length)).toBe(0);
        // DEV025-AC-04 native modal chooser returns focus to its trigger.
        await page.keyboard.press('Escape');
        const trigger = page.getByRole('button', { name: 'Change vessel context', exact: true });
        await trigger.focus();
        await page.keyboard.press('Enter');
        const chooser = page.getByRole('dialog', { name: 'Change vessel context' });
        await expect(chooser).toBeVisible();
        await page.keyboard.press('Escape');
        await expect(chooser).toBeHidden();
        await expect(trigger).toBeFocused();
    });
    test('reduced motion disables the theme builder entrance animation', async ({ page }) => {
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await open(page);
        await page.locator('.appearance-controls > summary').click();
        await page.getByRole('button', { name: 'Customize…' }).click();
        const builder = page.locator('.theme-builder');
        await expect(builder).toBeVisible();
        const animation = await builder.evaluate(el => { const style = getComputedStyle(el); return { name: style.animationName, duration: parseFloat(style.animationDuration) }; });
        expect(animation.name === 'none' || animation.duration <= 0.00001).toBe(true);
    });
});

for (const locale of ['en', 'ar'] as const) {
    test.describe(`DEV-025 320px reflow (${locale})`, () => {
        test.use({ viewport: { width: 320, height: 640 } });
        test(`shell and context bar reflow at 320 CSS px with a narrow context summary (${locale})`, async ({ page }) => {
            await open(page, locale);
            await expect(page.locator('html')).toHaveAttribute('dir', locale === 'ar' ? 'rtl' : 'ltr');
            expectClean(await scan(page));
            // DEV025-AC-07 65.03 paragraph 48: the summary button's accessible name carries the full context.
            const summary = page.locator('.context-summary-toggle');
            await expect(summary).toBeVisible();
            await expect(summary).toHaveAttribute('aria-expanded', 'false');
            const name = await summary.getAttribute('aria-label');
            for (const part of [vessel.code, vessel.name, 'FICTIONAL company', locale === 'ar' ? 'KURGUSAL' : 'FICTIONAL Master'])
                if (!part.startsWith('KURGUSAL')) expect(name).toContain(part);
            await expect(page.locator('.context-details .context-vessel')).not.toBeInViewport();
            await summary.click();
            await expect(summary).toHaveAttribute('aria-expanded', 'true');
            await expect(page.locator('.context-details .context-vessel')).toBeVisible();
            expectClean(await scan(page));
            // Context switch stays reachable on the narrow screen.
            await expect(page.locator('.context-switch-trigger')).toBeVisible();
            // Drawer: opens as a modal navigation, anchored to the inline start edge, Escape returns focus.
            const menu = page.locator('.workspace-menu');
            await menu.focus();
            await page.keyboard.press('Enter');
            const drawer = page.locator('#workspace-navigation');
            await expect(drawer).toHaveAttribute('role', 'dialog');
            await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('#workspace-navigation'))).toBe(true);
            const box = await drawer.boundingBox();
            expect(box).not.toBeNull();
            if (locale === 'ar') expect(Math.round(box!.x + box!.width)).toBe(320);
            else expect(Math.round(box!.x)).toBe(0);
            expect((await scan(page)).overflow).toBeLessThanOrEqual(0);
            await page.keyboard.press('Escape');
            await expect(drawer).not.toHaveAttribute('role', 'dialog');
            await expect(menu).toBeFocused();
            // Appearance popover stays inside the viewport in both directions.
            await page.locator('.appearance-controls > summary').click();
            const panel = await page.locator('.appearance-panel').boundingBox();
            expect(panel).not.toBeNull();
            expect(panel!.x).toBeGreaterThanOrEqual(0);
            expect(panel!.x + panel!.width).toBeLessThanOrEqual(320);
        });
    });
}

test.describe('DEV-025 200% zoom equivalent', () => {
    // 1280x800 at 200% browser zoom lays out as a 640x400 CSS px viewport.
    test.use({ viewport: { width: 640, height: 400 }, deviceScaleFactor: 2 });
    test('no horizontal scroll and the sticky bar never covers the focused element', async ({ page }) => {
        await open(page);
        expectClean(await scan(page));
        const header = page.locator('header.enterprise-topbar');
        const position = await header.evaluate(el => getComputedStyle(el).position);
        // On a short viewport the bar is not sticky; otherwise scroll padding equals its height.
        if (position === 'sticky') {
            const height = (await header.boundingBox())!.height;
            const padding = await page.evaluate(() => parseFloat(getComputedStyle(document.documentElement).scrollPaddingTop));
            expect(padding).toBeGreaterThanOrEqual(height);
        }
        else expect(['static', 'relative']).toContain(position);
        // Tab through the page: no focused element is hidden behind the top bar.
        for (let i = 0; i < 25; i++) {
            await page.keyboard.press('Tab');
            const covered = await page.evaluate(() => {
                const active = document.activeElement as HTMLElement | null, bar = document.querySelector('header.enterprise-topbar');
                if (!active || !bar || bar.contains(active) || active.closest('#workspace-navigation')) return false;
                const style = getComputedStyle(bar);
                if (style.position !== 'sticky' && style.position !== 'fixed') return false;
                return active.getBoundingClientRect().top < bar.getBoundingClientRect().bottom - 1;
            });
            expect(covered).toBe(false);
        }
    });
});
