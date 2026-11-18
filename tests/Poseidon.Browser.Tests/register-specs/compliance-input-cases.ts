import { expect, type Page } from '@playwright/test';
import { loadQualifiedFixture, requireQualifiedContext } from './qualified-owning-fixture.ts';
import { openQualifiedContext } from './qualified-owning-browser.ts';
async function incompleteClassRecord(page: Page) {
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
    await region.getByLabel('Title', { exact: true }).fill('FICTIONAL incomplete class pack; no adoption');
    await region.getByRole('combobox', { name: 'Compliance record purpose', exact: true }).selectOption('classPack');
    await expect(region.getByRole('combobox', { name: 'M79 statement in source', exact: true })).toHaveValue('');
    const [response] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/evidence' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Save incomplete draft', exact: true }).click()]);
    expect(response.status()).toBe(200);
    const row = await response.json();
    expect(row.content.classPack.iacsM79).toBe(null);
    expect(row.lifecycle).toBe('DRAFT');
    expect(row.verification).toBe('UNVERIFIED');
    expect(row.blockers).toContain('OWNING_INPUT_INCOMPLETE');
    const history = await page.request.get(`/api/v1/evidence/${row.id}/versions?vesselId=${vessel.id}`);
    expect(history.status()).toBe(200);
    expect((await history.json()).items[0].content.classPack.iacsM79).toBe(null);
}
async function qualifiedComplianceConsumer(page: Page) {
    // The owned gate must provision the fictional source/evidence with ordinary
    // commands and separately signed reviewers. Missing setup fails, never skips.
    const context = await requireQualifiedContext(page, loadQualifiedFixture(), 'Compliance');
    const region = await openQualifiedContext(page, context);
    await region.getByLabel('Class pack evidence reference', { exact: true }).fill(context.rows.classPack.id);
    await region.getByLabel('MSMD evidence reference', { exact: true }).fill(context.rows.msmd.id);
    await region.getByLabel('Source evidence reference', { exact: true }).fill(context.rows.classPack.id);
    const [response] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/applicability/evaluate' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Evaluate qualified current sources', exact: true }).click()]);
    expect(response.status()).toBe(200);
    const value = await response.json();
    for (const field of ['class.rulePack.IACS_M79', 'regulatory.msmdVerified', 'evidence.sourceVerified'])
        expect(value.snapshot.qualifications[0].facts[field].knowledge).toBe('Known');
    const current = await page.request.get(`/api/v1/applicability/evaluations/${value.id}/current`);
    expect(current.status()).toBe(200);
    expect((await current.json()).current).toBe(true);
    await requireQualifiedContext(page, context.fixture, 'Compliance');
}
export const complianceInputCases = [
    { name: 'compliance-class-incomplete-unknown-command-history', run: incompleteClassRecord, restricted: false },
    { name: 'compliance-qualified-class-msmd-source-command-fixture-consumer', run: qualifiedComplianceConsumer, restricted: false },
] as const;
