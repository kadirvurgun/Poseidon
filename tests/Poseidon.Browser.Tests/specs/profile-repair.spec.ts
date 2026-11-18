import { test, expect } from '@playwright/test';
// Browser editor/transport contract with synthetic routed API; no external IdP claim.
test('profile exact decimal save and malformed background history recovery retain edits', async ({ page }) => {
    const vessel = { id: '10000000-0000-0000-0000-000000000001', companyId: '10000000-0000-0000-0000-000000000002', code: 'S1', name: 'Synthetic Ship', kind: 'Ship', version: 1, canEdit: true };
    const keys = ['imo', 'flag', 'shipType', 'classSociety', 'classNumber', 'gt', 'nt', 'dwt', 'loa', 'breadth', 'draft', 'buildDate', 'tradingArea', 'cargoContext', 'declaration', 'sourceReference', 'sourceObservedAt', 'mainPropulsionKw', 'contractForConstructionDate', 'constructionRecordReference'];
    const content: Record<string, unknown> = { ...Object.fromEntries(keys.map(key => [key, { state: 'Missing', value: null }])), constructionRecord: null };
    content.gt = { state: 'Known', value: '9007199254740993' };
    content.nt = { state: 'Known', value: '0.1234567890123456789012345678' };
    const profile = { tenantId: '10000000-0000-0000-0000-000000000003', vessel, version: 19, revision: 1, savedAt: '2026-01-01T00:00:00Z', provenance: 'Synthetic', content };
    let release!: () => void;
    const held = new Promise<void>(resolve => { release = resolve; });
    let histories = 0;
    await page.route('**/api/v1/**', async (route) => {
        const url = route.request().url();
        let body: unknown;
        if (url.includes('/auth/session'))
            body = { authenticated: true, setupRequired: false, canRead: true, canReadCompanies: false, canCreate: false, csrfToken: 'synthetic-csrf-token-only', actorId: '10000000-0000-0000-0000-000000000004', tenantId: profile.tenantId };
        else if (new URL(url).pathname === '/api/v1/workspace')
            body = { destinations: [{ key: 'fleet', href: '/app/fleet' }], canSearch: false, canWriteRegulatory: false };
        else if (url.includes('/workspace/context')) {
            const selected = url.includes('/context/' + vessel.id);
            body = { tenantId: profile.tenantId, actorId: '10000000-0000-0000-0000-000000000004', roleContext: { state: 'Missing', catalogVersion: null, roles: [] },
                company: selected ? { id: vessel.companyId, code: 'SYN-CO', name: 'Synthetic company' } : null,
                vessel: selected ? { id: vessel.id, companyId: vessel.companyId, code: vessel.code, name: vessel.name, kind: vessel.kind } : null };
        }
        else if (url.includes('/workspace/lenses')) {
            const selected = url.includes('/lenses/' + vessel.id);
            body = { tenantId: profile.tenantId, actorId: '10000000-0000-0000-0000-000000000004', vesselId: selected ? vessel.id : null,
                mode: selected ? 'Ship' : 'Fleet', state: 'Missing', version: null, lenses: [], canSearch: false };
        }
        else if (url.includes('/files?'))
            body = { items: [], offset: 0, hasMore: false, available: true, canWrite: false, maxBytes: 1048576, allowedTypes: ['image/png'] };
        // DEV-020 field provenance endpoints are routed before the generic profile/history
        // branches. Synthetic, unconfigured policy, no declared sources and no positive state.
        else if (new URL(url).pathname.endsWith('/regulatory-profile/fields')) {
            const checkedAt = '2026-01-01T00:00:00Z';
            body = { tenantId: profile.tenantId, vesselId: vessel.id,
                owner: { legalEntityId: vessel.companyId, binding: 'LEGACY_COMPANY_BINDING', registeredOwnerState: 'NOT_ESTABLISHED' },
                profileRevision: profile.revision, profileSavedAt: profile.savedAt, checkedAt, policy: { status: 'UNCONFIGURED', policyRef: null },
                summary: { state: 'UNKNOWN', reasonCode: 'FIELDS_UNKNOWN', oldestObservedAt: null, checkedAt, fresh: 0, stale: 0, unknown: 1, unavailable: 0 },
                fields: [{ fieldPath: 'imo', kind: 'text', unit: null, value: { state: 'Missing', value: null }, valueRevision: 1, latestSequence: 0,
                    source: null, priorValueSource: null, freshness: { state: 'UNKNOWN', reasonCode: 'VALUE_NOT_RECORDED', observedAt: null, checkedAt, ageSeconds: null },
                    verificationState: 'NOT_VERIFIED' }],
                canDeclare: false };
        }
        else if (url.includes('/regulatory-profile/fields/'))
            body = { items: [], offset: Number(new URL(url).searchParams.get('offset') ?? 0), limit: 25, hasMore: false };
        else if (url.includes('/history?')) {
            if (++histories === 1) {
                await held;
                body = { items: [null], offset: 0, limit: 25, hasMore: false };
            }
            else
                body = { items: [{ revision: 1, savedAt: profile.savedAt, provenance: profile.provenance, content }], offset: 0, limit: 25, hasMore: false };
        }
        else if (url.includes('/regulatory-profile')) {
            if (route.request().method() === 'PUT') {
                const command = route.request().postDataJSON();
                expect(command.content.gt.value).toBe('9007199254740993');
                expect(command.content.nt.value).toBe('0.1234567890123456789012345678');
                // The server derives provenance from declaration/sourceReference states (RegulatoryDraftEndpoints).
                const states = [command.content.declaration.state, command.content.sourceReference.state];
                const provenance = states.includes('Known') ? 'DeclaredUnverified' : states.includes('Unknown') ? 'Unknown' : 'Missing';
                body = { ...profile, revision: 2, version: 20, provenance, content: command.content };
            }
            else
                body = profile;
        }
        else
            body = url.includes('/vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel;
        await route.fulfill({ json: body });
    });
    await page.goto('/app');
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await page.getByRole('button', { name: 'Synthetic Ship', exact: true }).click();
    await page.getByRole('button', { name: 'Regulatory profile', exact: true }).click();
    await expect(page.getByLabel('GT', { exact: true })).toHaveValue('9007199254740993');
    await page.getByLabel('Flag — Data state').selectOption('Known');
    await page.getByLabel('Flag', { exact: true }).fill('Local retained');
    release();
    await expect(page.getByRole('alert')).toContainText('Unable to load');
    await page.getByRole('button', { name: 'Retry loading', exact: true }).click();
    await expect(page.getByText(/Version 1 ·/)).toBeVisible();
    await expect(page.getByLabel('Flag', { exact: true })).toHaveValue('Local retained');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    await expect(page.getByText('Draft saved.', { exact: true })).toBeVisible();
    await page.getByText(/Version 1 ·/).click();
    await expect(page.getByText('9007199254740993', { exact: true })).toBeVisible();
});
