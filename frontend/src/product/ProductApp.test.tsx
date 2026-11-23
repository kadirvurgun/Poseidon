import { navigationTestIdentity, navigationTestRole, navigationTestWorkspace, openNavigationTestWorkView, openNavigationTestOverview } from './workspace-navigation.test-fixture';
vi.mock('./workspace-lens-api', async (original) => ({ ...await original<typeof import('./workspace-lens-api')>(),
    loadLenses: vi.fn(async (tenant: string, actor: string, vessel: string | null) => (await import('./workspace-navigation.test-fixture')).navigationTestLenses(tenant, actor, vessel)) }));
vi.mock('./workspace-context-api', async (original) => ({ ...await original<typeof import('./workspace-context-api')>(),
    loadWorkspaceContext: (await import('./workspace-navigation.test-fixture')).navigationTestContext }));
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ProductApp } from './ProductApp';
const anonymous = { authenticated: false, setupRequired: false, actorId: null, tenantId: null, canRead: false, csrfToken: '' };
const signedIn = { ...anonymous, authenticated: true, canRead: true, canReadCompanies: true, canCreate: false, ...navigationTestIdentity, csrfToken: 'synthetic-csrf' };
const reply = (data: unknown, status = 200) => ({ ok: status === 200, status, json: async () => data }) as Response;
beforeEach(() => window.history.replaceState(null, '', '/app'));
afterEach(() => vi.unstubAllGlobals());
describe('FOUNDATION-APP-01 product states (mocked HTTP, not backend acceptance)', () => {
    it('keeps loading accessible while the session request is pending', () => {
        vi.stubGlobal('fetch', vi.fn(() => new Promise(() => { })));
        render(<ProductApp />);
        expect(screen.getByRole('status')).toHaveTextContent('Yükleniyor');
        expect(screen.queryByRole('table')).not.toBeInTheDocument();
    });
    it('shows setup required without offering a login that cannot succeed', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply({ ...anonymous, setupRequired: true })));
        render(<ProductApp />);
        expect(await screen.findByText(/henüz yapılandırılmadı/)).toBeVisible();
        expect(screen.queryByRole('link', { name: 'Oturum aç' })).not.toBeInTheDocument();
    });
    it('links sign-in to the BFF and switches the full shell to English', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply(anonymous)));
        const user = userEvent.setup();
        render(<ProductApp />);
        expect(await screen.findByRole('link', { name: 'Oturum aç' })).toHaveAttribute('href', '/api/v1/auth/login?returnUrl=%2Fapp');
        await user.selectOptions(screen.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }), "en");
        expect(screen.getByRole('heading', { name: 'Workspace' })).toBeVisible();
        expect(document.documentElement.lang).toBe('en');
        expect(screen.getByRole('link', { name: 'Sign in' })).toBeVisible();
    });
    // INVITE-RATELIMIT-FOLLOWUPS: a rate-limited sign-in returns to /app?auth=request_limit with its own localized notice.
    it('shows the localized sign-in limit notice instead of the generic sign-in failure', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply(anonymous)));
        history.replaceState(null, '', '/app?auth=request_limit');
        try {
            render(<ProductApp />);
            const notice = await screen.findByText('Bu ağdan çok fazla oturum açma denemesi yapıldı. Birkaç dakika bekleyip yeniden deneyin.');
            expect(notice).toHaveAttribute('data-auth', 'request_limit');
            expect(screen.queryByText('Oturum açma tamamlanamadı. Lütfen tekrar deneyin.')).not.toBeInTheDocument();
            expect(screen.getByRole('link', { name: 'Oturum aç' })).toBeVisible();
        } finally { history.replaceState(null, '', '/app'); }
    });
    it.each([401, 403, 503])('distinguishes HTTP %s from an empty list', async (status) => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(reply(signedIn)).mockResolvedValue(reply({}, status)));
        render(<ProductApp />);
        const alert = await screen.findByRole('alert');
        expect(alert).toHaveTextContent(status === 401 ? 'oturum açın' : status === 403 ? 'erişim yetkiniz yok' : 'ulaşılamıyor');
        expect(screen.queryByText('Kayıt bulunamadı.')).not.toBeInTheDocument();
    });
    it('shows real empty results and retries a failed service request', async () => {
        let attempts = 0;
        const fetchMock = vi.fn(async (url: string) => {
            if (url.endsWith('auth/session'))
                return reply(signedIn);
            if (url.endsWith('workspace'))
                return reply(navigationTestWorkspace());
            if (++attempts === 1)
                throw new TypeError('offline');
            return reply({ items: [], offset: 0, limit: 25, hasMore: false });
        });
        vi.stubGlobal('fetch', fetchMock);
        const user = userEvent.setup();
        render(<ProductApp />);
        await user.click(await screen.findByRole('button', { name: 'Tekrar dene' }));
        expect(await screen.findByText('Kayıt bulunamadı.')).toBeVisible();
        expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
    it('loads company detail and requests ship/tug filters from the API', async () => {
        const fetchMock = vi.fn(async (url: string) => {
            if (url.endsWith('auth/session'))
                return reply(signedIn);
            if (url.endsWith('workspace'))
                return reply(navigationTestWorkspace());
            if (url.endsWith('companies/company-one'))
                return reply({ id: 'company-one', code: 'C1', name: 'Synthetic Company', version: 1, canEdit: false });
            if (url.includes('companies?'))
                return reply({ items: [{ id: 'company-one', code: 'C1', name: 'Synthetic Company', version: 1, canEdit: false }], offset: 0, limit: 25, hasMore: false });
            return reply({ items: [{ id: 'tug-one', companyId: 'company-one', code: 'T1', name: 'Synthetic Tug', kind: 'Tug', version: 1, canEdit: false }], offset: 0, limit: 25, hasMore: false });
        });
        vi.stubGlobal('fetch', fetchMock);
        const user = userEvent.setup();
        render(<ProductApp />);
        await user.click(await screen.findByRole('button', { name: 'Synthetic Company' }));
        expect(await screen.findByRole('region', { name: 'Ayrıntılar' })).toHaveTextContent('Synthetic Company');
        await openNavigationTestWorkView(user);
        await user.selectOptions(await screen.findByLabelText('Menü merceği'), navigationTestRole);
        await openNavigationTestOverview(user);
        await user.click(within(screen.getByRole('navigation', { name: 'POSEIDON' })).getByRole('button', { name: 'Filo' }));
        await user.selectOptions(screen.getByLabelText('Araç türü'), 'Tug');
        expect(await screen.findByRole('button', { name: 'Synthetic Tug' })).toBeVisible();
        await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/v1/vessels?offset=0&limit=25&kind=Tug', expect.objectContaining({ credentials: 'same-origin' })));
        expect(screen.getByRole('cell', { name: 'Römorkör' })).toBeVisible();
    });
    it.each([
        { tab: 'Şirketler', rowName: 'Synthetic Company', detailUrl: '/api/v1/companies/company-one', listUrl: '/api/v1/companies?offset=0&limit=25', item: { id: 'company-one', code: 'C1', name: 'Synthetic Company', version: 1, canEdit: false } },
        { tab: 'Filo', rowName: 'Synthetic Vessel', detailUrl: '/api/v1/vessels/vessel-one', listUrl: '/api/v1/vessels?offset=0&limit=25', item: { id: 'vessel-one', companyId: 'company-one', code: 'V1', name: 'Synthetic Vessel', kind: 'Ship', version: 1, canEdit: false } },
    ])('keeps the selected $tab detail visible after selecting its row again', async ({ tab, rowName, detailUrl, listUrl, item }) => {
        const fetchMock = vi.fn(async (url: string) => {
            if (url.endsWith('auth/session'))
                return reply(signedIn);
            if (url.endsWith('workspace'))
                return reply(navigationTestWorkspace());
            if (url === detailUrl)
                return reply(item);
            if (url === listUrl)
                return reply({ items: [item], offset: 0, limit: 25, hasMore: false });
            return reply({ items: [], offset: 0, limit: 25, hasMore: false });
        });
        vi.stubGlobal('fetch', fetchMock);
        const user = userEvent.setup();
        render(<ProductApp />);
        if (tab === 'Filo') {
            await openNavigationTestWorkView(user);
            await user.selectOptions(await screen.findByLabelText('Menü merceği'), navigationTestRole);
            await openNavigationTestOverview(user);
            await user.click(within(screen.getByRole('navigation', { name: 'POSEIDON' })).getByRole('button', { name: tab }));
        }
        const row = await screen.findByRole('button', { name: rowName });
        await user.click(row);
        const detail = await screen.findByRole('region', { name: 'Ayrıntılar' });
        expect(detail).toHaveTextContent(rowName);
        await user.click(row);
        expect(detail).toHaveTextContent(rowName);
        expect(detail).not.toHaveTextContent('Yükleniyor');
    });
    it('does not request business records for an authenticated identity without read permission', async () => {
        const fetchMock = vi.fn().mockResolvedValue(reply({ ...signedIn, canRead: false }));
        vi.stubGlobal('fetch', fetchMock);
        render(<ProductApp />);
        expect(await screen.findByRole('alert')).toHaveTextContent('erişim yetkiniz yok');
        expect(fetchMock).toHaveBeenCalledTimes(1);
    });
});
describe('S3 gate B1: per-tab UNKNOWN qualification envelopes (mocked HTTP)', () => {
    const own = `${navigationTestIdentity.tenantId}/${navigationTestIdentity.actorId}`;
    const ownKey = 'poseidon.qualification.unknown.v2:own-rule', foreignKey = 'poseidon.qualification.unknown.v2:foreign-rule';
    const seedEnvelopes = () => {
        window.sessionStorage.setItem(ownKey, JSON.stringify({ owner: own }));
        window.sessionStorage.setItem(foreignKey, JSON.stringify({ owner: '10000000-0000-0000-0000-000000000001/10000000-0000-0000-0000-000000000002' }));
        window.sessionStorage.setItem('poseidon.qualification.unknown.v1:legacy-rule', JSON.stringify({ owner: own, body: '{"content":{"sourceBytesBase64":"AQID"}}' }));
    };
    const fetchFor = (session: unknown) => vi.fn(async (url: string) => url.endsWith('auth/session') ? reply(session)
        : url.endsWith('workspace') ? reply(navigationTestWorkspace()) : reply({ items: [], offset: 0, limit: 25, hasMore: false }));
    it('removes another owner and legacy envelopes when the shell observes the session owner', async () => {
        seedEnvelopes();
        vi.stubGlobal('fetch', fetchFor(signedIn));
        render(<ProductApp />);
        await screen.findByRole('button', { name: 'Oturumu kapat' });
        expect(window.sessionStorage.getItem(foreignKey)).toBeNull();
        expect(window.sessionStorage.getItem('poseidon.qualification.unknown.v1:legacy-rule')).toBeNull();
        expect(window.sessionStorage.getItem(ownKey)).not.toBeNull();
    });
    it('removes every envelope for an anonymous session', async () => {
        seedEnvelopes();
        vi.stubGlobal('fetch', fetchFor(anonymous));
        render(<ProductApp />);
        await screen.findByRole('link', { name: 'Oturum aç' });
        expect([ownKey, foreignKey].map(k => window.sessionStorage.getItem(k))).toEqual([null, null]);
    });
    it('removes every envelope on sign-out before the logout navigation', async () => {
        seedEnvelopes();
        let atSubmit: (string | null)[] = [];
        const submit = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => { atSubmit = [ownKey, foreignKey].map(k => window.sessionStorage.getItem(k)); });
        try {
            vi.stubGlobal('fetch', fetchFor(signedIn));
            const user = userEvent.setup();
            render(<ProductApp />);
            await user.click(await screen.findByRole('button', { name: 'Oturumu kapat' }));
            await waitFor(() => expect(submit).toHaveBeenCalledTimes(1));
            expect(atSubmit).toEqual([null, null]);
        } finally { submit.mockRestore(); }
    });
});
