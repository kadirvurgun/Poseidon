import { test, expect } from '@playwright/test';
test('Today Tug draft Why is scoped to its job and withdraws the feed after revocation', async ({ page }) => {
    const vesselId = '10000000-0000-0000-0000-000000000001', jobId = '20000000-0000-0000-0000-000000000001';
    const now = '2026-11-02T00:00:00Z';
    const card = { key: `tug-job:${jobId}`, priority: 'Review', source: 'TugJob', reason: 'TugPlanMissing', owner: null,
        nextAction: 'ReviewTugJob', href: `/app/tug-job-detail/${jobId}?vessel=${vesselId}`, sourceRef: { owner: 'TUG', kind: 'TugJob', id: jobId, revision: 1, savedAt: now } };
    const draft = { key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftAbsent', owner: null,
        nextAction: 'ViewDraft', href: `/app/fleet/${vesselId}/profile`, sourceRef: { owner: 'REGULATORY', kind: 'RegulatoryDraft', id: vesselId, revision: null, savedAt: null } };
    let denied = false;
    await page.route('**/api/v1/**', async (route) => {
        const url = new URL(route.request().url());
        if (url.pathname.endsWith('/auth/session')) {
            await route.fulfill({ json: { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false } });
            return;
        }
        if (url.pathname.endsWith('/why-engine')) {
            expect(url.searchParams.get('vesselId')).toBe(vesselId);
            expect(url.searchParams.get('cardKey')).toBe(card.key);
            if (denied) {
                await route.fulfill({ status: 403, json: {} });
                return;
            }
            await route.fulfill({ json: { vesselId, cardKey: card.key, rule: 'TW-TUG-01', reason: card.reason, inputs: null,
                    readAt: now, savedAt: now, sourceObservedAt: null, freshness: 'Unverified', provenance: 'UserDeclaredUnverified', owner: null,
                    blockers: ['DraftPlanningNotOperationalApproval', 'OwningReadinessCoverageIncomplete', 'MasterDecisionRequired'],
                    nextAction: card.nextAction, href: card.href,
                    tug: { jobId, requestRevision: 1, requestIncomplete: true, planRevision: null, plannedRequestRevision: null,
                        plannedStart: null, plannedEnd: null, assignedTugs: [], currentOwnerRoleId: null, evidence: null } } });
            return;
        }
        await route.fulfill({ json: { vessel: { id: vesselId, companyId: jobId, code: 'TT', name: 'FICTIONAL Tug', kind: 'Tug', version: 1, canEdit: false },
                readAt: now, coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
                    .map((source, i) => ({ source, status: i === 0 ? 'Missing' : i === 6 ? 'Available' : 'Unavailable' })),
                cards: { items: [card, draft], offset: 0, limit: 25, hasMore: false }, viewState: 'Partial' } });
    });
    await page.goto(`/app/today/${vesselId}`);
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    const article = page.getByRole('article', { name: `Tug job ${jobId}`, exact: true });
    await expect(article).toBeVisible();
    const why = article.getByRole('button', { name: 'Why?', exact: true });
    await why.focus();
    await page.keyboard.press('Enter');
    await expect(article.getByText('No draft plan is stored.', { exact: true })).toBeVisible();
    await expect(article.getByText('A draft plan does not authorize dispatch or operation.', { exact: true })).toBeVisible();
    await article.getByRole('button', { name: 'Close Why', exact: true }).click();
    denied = true;
    await why.click();
    await expect(page.getByRole('alert')).toBeFocused();
    await expect(page.getByRole('button', { name: 'Review tug job', exact: true })).toHaveCount(0);
    await expect(page.getByText(/Current vessel/)).toHaveCount(0);
});
// Mocked transport: UI/keyboard/mobile evidence only. Real authorization is in TodayTests.cs.
test('Today honest coverage, Why reauthorization, refresh and offline on desktop/mobile', async ({ page }) => {
    const id = '10000000-0000-0000-0000-000000000001';
    const vessel = { id, companyId: '10000000-0000-0000-0000-000000000002', code: 'T1', name: 'Synthetic Tug', kind: 'Tug', version: 1, canEdit: false };
    const now = '2026-11-01T00:00:00Z';
    const href = `/app/fleet/${id}/profile`;
    let reads = 0;
    let deny = false;
    await page.route('**/api/v1/**', async (route) => {
        const url = route.request().url();
        if (url.includes('why-engine')) {
            reads++;
            if (deny) {
                await route.fulfill({ status: 403, json: {} });
                return;
            }
            await route.fulfill({ json: { vesselId: id, cardKey: 'regulatory-draft', rule: 'TW-DRAFT-01', reason: 'DraftUnverified', inputs: { known: 0, missing: 20, unknown: 0, reason: 'DraftUnverified' }, readAt: now, savedAt: now, sourceObservedAt: null, freshness: 'Unverified', provenance: 'Synthetic', blockers: ['VerificationUnavailable', 'OperationalSourcesUnavailable'], owner: null, nextAction: 'ViewDraft', href } });
            return;
        }
        const body = url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, csrfToken: 'synthetic' }
            : { vessel, readAt: now, coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning'].map((source, i) => ({ source, status: i ? 'Unavailable' : 'Available' })), cards: { items: [{ key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftUnverified', owner: null, nextAction: 'ViewDraft', href, sourceRef: { owner: 'REGULATORY', kind: 'RegulatoryDraft', id, revision: 1, savedAt: now } }], offset: 0, limit: 25, hasMore: false }, viewState: 'Partial' };
        await route.fulfill({ json: body });
    });
    await page.goto(`/app/today/${id}`);
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await expect(page.getByText(/Current vessel/)).toContainText('Tug');
    await expect(page.getByText(/Unavailable in this context/)).toHaveCount(6);
    const why = page.getByRole('button', { name: 'Why?', exact: true });
    await why.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByText(/Input source observation: Unknown/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Close Why', exact: true })).toBeFocused();
    await page.getByRole('button', { name: 'Close Why' }).click();
    deny = true;
    await why.click();
    await expect(page.getByText('Access denied.', { exact: true })).toBeVisible();
    await expect(page.getByText(/Current vessel/)).toHaveCount(0);
    await expect(page.getByRole('alert')).toBeFocused();
    expect(reads).toBe(2);
    await expect(page.getByText(/Declared \/ missing/)).toHaveCount(0);
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await expect(page.getByText(/Stored regulatory draft is unverified/)).toBeVisible();
    // navigator.onLine is controlled by the browser context for the offline state.
    await page.context().setOffline(true);
    await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeDisabled();
    await expect(page.getByText('Offline. Displayed data is stale and unverified.', { exact: true })).toBeVisible();
    await page.context().setOffline(false);
});
test('Today maintenance explanation binds to its own card and clears the feed after current permission denial', async ({ page }) => {
    const id = '10000000-0000-0000-0000-000000000001', planId = '20000000-0000-0000-0000-000000000001';
    const assetId = '30000000-0000-0000-0000-000000000001', now = '2026-11-02T00:00:00Z';
    const vessel = { id, companyId: assetId, code: 'SYNTHETIC', name: 'FICTIONAL tug', kind: 'Tug', version: 1, canEdit: false };
    const card = { key: `maintenance-plan:${planId}`, priority: 'Review', source: 'MaintenancePlan', reason: 'MaintenanceReviewRequired', owner: null,
        nextAction: 'ReviewMaintenancePlan', href: `/app/maintenance-plan/${id}?id=${planId}`, sourceRef: { owner: 'PMS', kind: 'MaintenancePlan', id: planId, revision: 2, savedAt: now } };
    const draft = { key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftUnverified', owner: null,
        nextAction: 'ViewDraft', href: `/app/fleet/${id}/profile`, sourceRef: { owner: 'REGULATORY', kind: 'RegulatoryDraft', id, revision: 1, savedAt: now } };
    const common = { vesselId: id, readAt: now, savedAt: now, sourceObservedAt: null, freshness: 'Unverified', owner: null };
    let deny = false;
    await page.route('**/api/v1/**', async (route) => {
        const url = new URL(route.request().url());
        if (url.pathname.endsWith('/auth/session')) {
            await route.fulfill({ json: { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false } });
            return;
        }
        if (url.pathname.endsWith('/why-engine')) {
            if (url.searchParams.get('cardKey') === card.key) {
                if (deny) {
                    await route.fulfill({ status: 403, json: {} });
                    return;
                }
                await route.fulfill({ json: { ...common, cardKey: card.key, rule: 'TW-MAINTENANCE-01', reason: card.reason, inputs: null,
                        provenance: 'UserDeclaredUnverified', blockers: ['PlanningDraftNotVerified'], nextAction: card.nextAction, href: card.href,
                        maintenance: { planId, planRevision: 2, assetId, assetRevision: 1, currentAssetRevision: 1, jobAvailable: true, ownerEligible: false,
                            observationState: 'NotRequested', calculation: { state: 'ReviewRequired', reason: 'MaintenanceOwnerUnavailable', nextDueDate: '2026-11-01', nextDueMeter: null, algorithm: 'planning-current-v1' } } } });
                return;
            }
            await route.fulfill({ json: { ...common, cardKey: draft.key, rule: 'TW-DRAFT-01', reason: draft.reason, inputs: { known: 0, missing: 20, unknown: 0, reason: draft.reason },
                    provenance: 'Synthetic', blockers: ['VerificationUnavailable', 'OperationalSourcesUnavailable'], nextAction: draft.nextAction, href: draft.href } });
            return;
        }
        await route.fulfill({ json: { vessel, readAt: now,
                coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning'].map((source, i) => ({ source, status: i === 0 || i === 5 ? 'Available' : 'Unavailable' })),
                cards: { items: [card, draft], offset: 0, limit: 25, hasMore: false }, viewState: 'Partial' } });
    });
    await page.goto(`/app/today/${id}`);
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    const maintenance = page.locator('article').filter({ has: page.getByRole('heading', { name: 'Review required · Maintenance plan' }) });
    const regulatory = page.locator('article').filter({ has: page.getByRole('heading', { name: 'Advisory · Regulatory draft' }) });
    await maintenance.getByRole('button', { name: 'Why?', exact: true }).focus();
    await page.keyboard.press('Enter');
    await expect(page.getByText('Plan / equipment revision: 2 / 1', { exact: true })).toBeVisible();
    await expect(page.getByText(/Declared \/ missing/)).toHaveCount(0);
    await regulatory.getByRole('button', { name: 'Why?', exact: true }).click();
    await expect(page.getByText('Declared / missing / unknown inputs: 0 / 20 / 0', { exact: true })).toBeVisible();
    await expect(page.getByText(/Plan \/ equipment revision/)).toHaveCount(0);
    deny = true;
    await maintenance.getByRole('button', { name: 'Why?', exact: true }).click();
    await expect(page.getByRole('alert')).toBeFocused();
    await expect(page.getByRole('button', { name: 'Review maintenance plan' })).toHaveCount(0);
    await expect(page.getByText(/Current vessel/)).toHaveCount(0);
});
// DEV-023 / T-S2-004: mocked transport, UI evidence only. Real authorization/source refs: TodayItemContractTests.cs.
test('DEV-023 Today item names its owning source, authorized next action and deterministic partial/error states', async ({ page }) => {
    const id = '40000000-0000-0000-0000-000000000001', planId = '40000000-0000-0000-0000-000000000002';
    const now = '2026-11-18T08:00:00Z', saved = '2026-11-17T21:15:00Z';
    const vessel = { id, companyId: planId, code: 'SYN-23', name: 'FICTIONAL Today tug', kind: 'Tug', version: 1, canEdit: false };
    const card = { key: `maintenance-plan:${planId}`, priority: 'Attention', source: 'MaintenancePlan', reason: 'MaintenanceDue', owner: null,
        nextAction: 'ViewMaintenancePlan', href: `/app/maintenance-plan/${id}?id=${planId}`,
        sourceRef: { owner: 'PMS', kind: 'MaintenancePlan', id: planId, revision: 4, savedAt: saved } };
    let reads = 0;
    await page.route('**/api/v1/**', async (route) => {
        const url = new URL(route.request().url());
        if (url.pathname.endsWith('/auth/session')) {
            await route.fulfill({ json: { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false } });
            return;
        }
        if (url.pathname.endsWith('/poseidon-today')) {
            reads++;
            if (reads === 1) {
                await route.fulfill({ status: 429, json: {} });
                return;
            }
            await route.fulfill({ json: { vessel, readAt: now,
                    coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
                        .map((source, i) => ({ source, status: i === 5 ? 'Available' : 'Unavailable' })),
                    cards: { items: [card], offset: 0, limit: 25, hasMore: false }, viewState: 'Partial' } });
            return;
        }
        await route.fulfill({ status: 404, json: { code: 'unconfigured_synthetic_read' } });
    });
    await page.goto(`/app/today/${id}`);
    await page.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }).selectOption("en");
    await expect(page.getByRole('alert')).toContainText('Too many requests. Wait, then retry.');
    await expect(page.getByRole('alert')).toBeFocused();
    await page.getByRole('button', { name: 'Retry', exact: true }).click();
    const notice = page.locator('[data-view-state="Partial"]');
    await expect(notice).toContainText('Absent items do not mean all clear.');
    const article = page.getByRole('article', { name: 'Planning attention · Maintenance plan', exact: true });
    const source = article.locator('.today-card-source');
    await expect(source).toContainText('Planned maintenance');
    await expect(source).toContainText(`Maintenance plan · ${planId}`);
    await expect(source.locator('time')).toHaveAttribute('datetime', saved);
    await expect(article.getByRole('button', { name: 'Review maintenance plan' })).toHaveCount(0);
    const view = article.getByRole('button', { name: 'View maintenance plan', exact: true });
    await view.focus();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(new RegExp(`/app/maintenance-plan/${id}\\?id=${planId}$`));
    expect(reads).toBe(2);
});
