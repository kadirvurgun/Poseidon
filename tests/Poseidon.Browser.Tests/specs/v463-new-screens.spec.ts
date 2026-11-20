import { expect, test } from '@playwright/test';
// DEV-115 (18.01 Backlog) v46.3 competitor-delta regression smoke for the 11
// "ADD / v46.3" screens of 11.01 sheet "v46.3 Delta" rows 2-12 (11.01 "v46.3 Özet"
// B15 = 11 screens, B16 = 190 fields). Synthetic transport only: the session is
// FICTIONAL and every other API read is refused with 403, so this proves the
// canonical URL dispatches, renders its localized heading, survives reload and
// stays read-only. Server authorization and field behavior are covered by the
// owning API/component tests, not claimed here. scripts/regression_inventory.py
// and tests/fixtures/regression/route-snapshot.v1.json pin the same URLs.
const screens: ReadonlyArray<{ id: string; tr: string; en: string }> = [
    { id: 'noon-report', tr: 'Öğle raporu', en: 'Noon report' },
    { id: 'deck-daily-log', tr: 'Güverte günlük kaydı', en: 'Deck daily log' },
    { id: 'engine-daily-log', tr: 'Makine günlük kaydı', en: 'Engine daily log' },
    { id: 'fuel-bunker-log', tr: 'Yakıt, ikmal ve ROB', en: 'Fuel, bunker and ROB' },
    { id: 'port-activity-sof', tr: 'Liman faaliyeti ve SOF', en: 'Port activity and SOF' },
    { id: 'operational-downtime', tr: 'Operasyon kesintisi merkezi', en: 'Downtime center' },
    { id: 'service-engineer-report', tr: 'Servis ve mühendis raporları', en: 'Service and engineer reports' },
    { id: 'pre-survey-checklist', tr: 'Sörvey öncesi kontrol listesi', en: 'Pre-survey checklist' },
    { id: 'risk-assessment', tr: 'Risk değerlendirmesi', en: 'Risk assessment' },
    { id: 'risk-template-library', tr: 'Risk şablon kütüphanesi', en: 'Risk template library' },
    { id: 'high-risk-work', tr: 'Yüksek riskli işler', en: 'High-risk work' },
];
const tenant = '20000000-0000-4000-8000-000000000001';
const actor = '20000000-0000-4000-8000-000000000002';

for (const screen of screens) {
    test(`v46.3 ${screen.id} keeps its canonical URL, localized heading and read-only smoke`, async ({ page }) => {
        const errors: string[] = [];
        const writes: string[] = [];
        page.on('pageerror', (error) => errors.push(error.message));
        await page.route('**/api/v1/**', async (route) => {
            const request = route.request(), path = new URL(request.url()).pathname;
            if (request.method() !== 'GET') {
                writes.push(`${request.method()} ${path}`);
                await route.fulfill({ status: 405, json: {} });
                return;
            }
            if (path === '/api/v1/auth/session') {
                await route.fulfill({ json: { authenticated: true, setupRequired: false, canRead: true, canReadCompanies: false, canCreate: false,
                        canSearch: false, csrfToken: 'synthetic-v463-csrf', actorId: actor, tenantId: tenant, tenantName: 'FICTIONAL tenant' } });
                return;
            }
            await route.fulfill({ status: 403, json: {} });
        });
        const canonical = `/app/${screen.id}`;
        await page.goto(canonical);
        await expect(page.getByRole('heading', { name: screen.tr, exact: true })).toBeVisible();
        await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption('en');
        await expect(page.getByRole('heading', { name: screen.en, exact: true })).toBeVisible();
        expect(new URL(page.url()).pathname).toBe(canonical);
        await page.reload();
        await expect(page.getByRole('heading', { name: new RegExp(`^(${escape(screen.tr)}|${escape(screen.en)})$`) })).toBeVisible();
        expect(new URL(page.url()).pathname).toBe(canonical);
        expect(errors).toEqual([]);
        expect(writes).toEqual([]);
    });
}

function escape(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
