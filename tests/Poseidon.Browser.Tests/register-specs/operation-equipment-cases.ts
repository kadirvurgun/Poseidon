import { expect, type Page } from '@playwright/test';
import { loadQualifiedFixture, requireQualifiedContext } from './qualified-owning-fixture.ts';
import { openQualifiedContext, selectQualifiedOwningReferences } from './qualified-owning-browser.ts';
// Registered genuine signed-host source. Coordinator provisions explicit current
// evidence/equipment/human-review rights. No fake provider, source approval or routes.
async function incompleteCargo(page: Page) {
    await page.goto('/app/sources');
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await page.getByRole('button', { name: 'Controls and evidence', exact: true }).click();
    const region = page.getByRole('region', { name: 'Controls and evidence', exact: true });
    const response = await page.request.get('/api/v1/vessels?offset=0&limit=25');
    expect(response.status()).toBe(200);
    const vessel = (await response.json()).items.find((v: {
        kind: string;
    }) => v.kind === 'Ship');
    expect(!!vessel).toBe(true);
    await region.getByRole('combobox', { name: 'Vessel context', exact: true }).selectOption(vessel.id);
    await region.getByRole('combobox', { name: 'Register', exact: true }).selectOption('Evidence');
    await region.getByLabel('Title', { exact: true }).fill('FICTIONAL cargo owning record; no qualification');
    await region.getByRole('combobox', { name: 'Document purpose', exact: true }).selectOption('cargoPlan');
    await expect(region.getByRole('combobox', { name: 'Packaged dangerous goods declaration', exact: true })).toHaveValue('');
    const [saved] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/evidence' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Save incomplete draft', exact: true }).click()]);
    expect(saved.status()).toBe(200);
    const first = await saved.json();
    expect(first.content.cargoPlan.packagedDangerousGoods).toBe(null);
    expect(first.lifecycle).toBe('DRAFT');
    expect(first.verification).toBe('UNVERIFIED');
    const history = await page.request.get(`/api/v1/evidence/${first.id}/versions?vesselId=${vessel.id}`);
    expect(history.status()).toBe(200);
    expect((await history.json()).items[0].content.cargoPlan.packagedDangerousGoods).toBe(null);
    await expect(region.getByRole('region', { name: 'Immutable history', exact: true })).toBeVisible();
}
async function qualifiedConsumer(page: Page, mode: 'Ship' | 'Tug') {
    // The gate must command-author and independently review these FICTIONAL records
    // first. Missing fixtures fail; this case never inserts verified rows or issues
    // actual service authorization. This signed actor consumes references only.
    const context = await requireQualifiedContext(page, loadQualifiedFixture(), mode);
    const region = await openQualifiedContext(page, context);
    const fields = mode === 'Ship' ? ['cargoPlan'] : ['operation', 'winchFitment'];
    await selectQualifiedOwningReferences(page, region, context, fields);
    const [evaluated] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/applicability/evaluate' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Evaluate qualified current sources', exact: true }).click()]);
    expect(evaluated.status()).toBe(200);
    const value = await evaluated.json();
    expect(value.authoritative).toBe(true);
    expect(value.results[0].prediction.result).toBe('APPLICABLE');
    const facts = value.snapshot.qualifications[0].facts;
    for (const field of mode === 'Ship' ? ['cargo.packagedDangerousGoods'] : ['vessel.operatingArea', 'operation.jobType', 'operation.profile', 'operation.serviceContext', 'equipment.towingWinchFitted']) {
        expect(facts[field].knowledge).toBe('Known');
        expect(facts[field].provenance).toBe('QualifiedOwningDocument');
    }
    const current = await page.request.get(`/api/v1/applicability/evaluations/${value.id}/current`);
    expect(current.status()).toBe(200);
    expect((await current.json()).current).toBe(true);
    await requireQualifiedContext(page, context.fixture, mode);
}
export const operationEquipmentCases = [
    { name: 'operation-equipment-incomplete-cargo-unknown-current-rights-history', run: incompleteCargo, restricted: false },
    { name: 'operation-equipment-qualified-ship-dg-command-fixture-consumer', run: (page: Page) => qualifiedConsumer(page, 'Ship'), restricted: false },
    { name: 'operation-equipment-qualified-tug-operation-winch-command-fixture-consumer', run: (page: Page) => qualifiedConsumer(page, 'Tug'), restricted: false },
] as const;
