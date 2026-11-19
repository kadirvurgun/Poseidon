import { expect, type Page } from '@playwright/test';
// DEV-032 / T-S3-007 (18.01 Backlog row:33). Real signed-provider session + actual PostgreSQL/API,
// run only through the existing private safe runner. Prerequisite: QA provisions, in disposable
// synthetic scope, a Human trace-reader appointment (audit.read + human.trace.read) for one
// evidence/control chain. A missing appointment is an unmet prerequisite and FAILS; it is never
// skipped, and no authorization, verification or decision is fabricated by this journey.
async function language(page: Page, locale: 'en' | 'tr') {
    await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption(locale);
}
async function appointedChain(page: Page) {
    const workspace = await (await page.request.get('/api/v1/workspace')).json();
    expect(workspace.destinations.some((x: { key: string; href: string }) => x.key === 'audit-ready' && x.href === '/app/audit-ready'), 'audit-ready reader prerequisite').toBe(true);
    const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json();
    for (const vessel of vessels.items as { id: string }[]) {
        const response = await page.request.get(`/api/v1/audit-ready/chains?vesselId=${vessel.id}`);
        expect(response.status() === 200).toBe(true);
        const body = await response.json();
        if (body.items.length > 0) return { vessel: vessel.id as string, chain: body.items[0] };
    }
    throw new Error('unmet_prerequisite: no appointed audit-ready chain');
}
async function drillDown(page: Page, locale: 'en' | 'tr') {
    const { vessel, chain } = await appointedChain(page);
    const text = locale === 'en'
        ? { title: 'Audit-ready trace', requirement: 'Requirement', decision: 'Decision', locator: 'Source locator (declared, unverified)', review: 'Review required', recorded: 'Recorded chain — human record only, not an approval' }
        : { title: 'Denetime hazır iz', requirement: 'Gereksinim', decision: 'Karar', locator: 'Kaynak konumu (beyan, doğrulanmamış)', review: 'İnceleme gerekli', recorded: 'Kaydedilmiş zincir — yalnızca insan kaydı, onay değildir' };
    await page.goto(`/app/audit-ready/${vessel}`);
    await language(page, locale);
    await expect(page.getByRole('heading', { level: 1, name: text.title })).toBeVisible();
    const open = page.getByRole('button', { name: new RegExp(chain.evidenceId) });
    await open.focus(); await page.keyboard.press('Enter');
    const params = new URL(page.url()).searchParams;
    expect(params.get('evidenceId') === chain.evidenceId && params.get('controlId') === chain.controlId).toBe(true);
    const trace = page.getByTestId('audit-ready-trace');
    await expect(trace).toBeVisible();
    // Never green/ready: the disposition is review-required or an explicitly non-approval human record.
    await expect(trace.getByText(new RegExp(`^(${text.review}|${text.recorded})$`))).toBeVisible();
    expect(await page.locator('[data-tone="success"], [data-tone="info"]').count()).toBe(0);
    const stages = await trace.locator('[data-stage]').evaluateAll(items => items.map(item => item.getAttribute('data-stage')));
    expect(stages).toEqual(['requirement', 'control', 'evidence', 'verification', 'decision']);
    const requirement = trace.getByRole('button', { name: new RegExp(`^${text.requirement}`) });
    await requirement.focus(); await page.keyboard.press('Enter');
    await expect(requirement).toHaveAttribute('aria-expanded', 'true');
    await expect(page.getByText(text.locator).first()).toBeVisible();
    const decision = trace.getByRole('button', { name: new RegExp(`^${text.decision}`) });
    await decision.focus(); await page.keyboard.press('Space');
    await expect(decision).toHaveAttribute('aria-expanded', 'true');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    // Actual network outage on the trace read: the error state appears, then retry restores it.
    await page.route('**/api/v1/audit-ready?*', route => route.abort('connectionreset'));
    await page.reload(); await language(page, locale);
    await expect(page.getByRole('alert').first()).toBeVisible();
    await page.unroute('**/api/v1/audit-ready?*');
    await page.getByRole('button', { name: locale === 'en' ? 'Retry' : 'Yeniden dene', exact: true }).click();
    await expect(page.getByTestId('audit-ready-trace')).toBeVisible();
}
async function deniedAuditReady(page: Page) {
    const workspace = await (await page.request.get('/api/v1/workspace')).json();
    expect(workspace.destinations.some((x: { key: string }) => x.key === 'audit-ready')).toBe(false);
    const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json();
    if (vessels.items?.length) expect([403, 404]).toContain((await page.request.get(`/api/v1/audit-ready/chains?vesselId=${vessels.items[0].id}`)).status());
    await page.goto('/app/audit-ready');
    await language(page, 'en');
    await expect(page.getByTestId('audit-ready-trace')).toHaveCount(0);
}
export const auditReadyCases = [
    { name: 'audit-ready-drill-down-en', run: (page: Page) => drillDown(page, 'en'), restricted: false },
    { name: 'audit-ready-drill-down-tr', run: (page: Page) => drillDown(page, 'tr'), restricted: false },
    { name: 'audit-ready-default-deny', run: deniedAuditReady, restricted: true },
] as const;
