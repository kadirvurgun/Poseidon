import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TechnicalWorkspace } from './TechnicalWorkspace';
const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222';
const child = '33333333-3333-3333-3333-333333333333';
const asset = { id, vesselId: vessel, parentId: null, name: 'FICTIONAL engine', code: 'SYNTHETIC', level: 'System', version: 7, revision: 1,
    systemFamily: null, equipmentType: null, criticality: null, maker: null, model: null, serialNumber: null, location: null,
    commissionDate: null, responsibleRoleId: null, notes: null, status: 'Draft', provenance: 'SyntheticUnverified' };
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
const fetcher = vi.fn();
let mutate: (init: RequestInit) => Promise<Response>;
let currentSession: { authenticated: boolean; tenantId: string; actorId: string; csrfToken: string };
beforeEach(() => {
    window.history.replaceState({}, '', `/app/equipment-hierarchy/${vessel}`);
    Object.defineProperty(navigator, 'onLine', { value: true, configurable: true });
    currentSession = { authenticated: true, tenantId: vessel, actorId: id, csrfToken: 'synthetic-only' };
    mutate = async init => json({ id: JSON.parse(init.body as string).id, version: 8, revision: 2, replayed: false });
    fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
        if (init?.method === 'POST')
            return mutate(init);
        if (url.endsWith('/auth/session'))
            return json(currentSession);
        if (url.endsWith('/owners'))
            return json([]);
        if (url.endsWith('/history'))
            return json([{ revision: 1, savedAt: '2026-11-01T00:00:00Z' }]);
        if (url.endsWith('/references'))
            return json({ jobs: [], owners: [], policy: 'DraftDeclarationsOnly' });
        if (url.includes('/maintenance-plan/'))
            return json({ items: [], canWrite: true, canReadHistory: true });
        return json({ items: [asset, { ...asset, id: child, parentId: id, level: 'Equipment', name: 'FICTIONAL pump' }], complete: true, canWrite: true, canReadHistory: true });
    });
    vi.stubGlobal('fetch', fetcher);
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset(); });
async function english() { await userEvent.selectOptions(screen.getByRole("combobox", { name: /^(Dil seçin|Choose language)$/ }), "en"); await screen.findByRole('button', { name: 'Add draft' }); }
const posts = () => fetcher.mock.calls.filter(([, init]) => init?.method === 'POST');
function deferred<T>() {
    let resolve!: (value: T) => void;
    const promise = new Promise<T>(done => { resolve = done; });
    return { promise, resolve };
}
async function addEquipment(name = 'FICTIONAL private draft') {
    await userEvent.click(screen.getByRole('button', { name: 'Add draft' }));
    await userEvent.type(screen.getByLabelText('Equipment name'), name);
}
async function reauthorize() {
    await userEvent.click(screen.getByRole('button', { name: 'Retry source read' }));
}
describe('DEV-052/053 authored product behavior (mock UI only; execution deferred)', () => {
    it('explains a current equipment revision conflict separately from historical planning results', async () => {
        const base = fetcher.getMockImplementation()!;
        fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
            if (!init?.method && url.endsWith('/history'))
                return json([{ revision: 1, savedAt: '2026-11-01T00:00:00Z',
                        referenceAt: '2026-11-01T00:00:00Z', due: { state: 'NotDue', reason: 'CalendarBoundary', nextDueDate: '2026-11-03', nextDueMeter: null, algorithm: 'planning-v1' } }]);
            if (!init?.method && url.endsWith(`/maintenance-plan/${vessel}`))
                return json({ items: [{ id: child, assetId: id,
                            jobId: null, ownerRoleId: null, version: 1, revision: 1, assetRevision: 1, status: 'Draft', provenance: 'SyntheticUnverified',
                            inputs: { triggerType: 'Calendar', timeZone: 'UTC' }, referenceAt: '2026-11-02T00:00:00Z', savedAt: '2026-11-01T00:00:00Z',
                            due: { state: 'ReviewRequired', reason: 'EquipmentRevisionChanged', nextDueDate: null, nextDueMeter: null, algorithm: 'planning-current-v1' }
                        }], canWrite: true, canReadHistory: true });
            return base(url, init);
        });
        window.history.replaceState({}, '', `/app/maintenance-plan/${vessel}?id=${child}`);
        render(<TechnicalWorkspace />);
        await english();
        await screen.findByText(/Equipment changed after this plan revision/);
        expect(screen.getByText('Current calculation reference clock')).toBeVisible();
        expect(screen.getByText(/Current planning calculation/)).toBeVisible();
        expect(screen.queryByText('2026-11-03')).not.toBeInTheDocument();
        await userEvent.click(screen.getByRole('button', { name: 'History' }));
        await screen.findByText(/Not due at the reference time.*Calculated from the declared date/);
        expect(screen.getByText(/Equipment changed after this plan revision/)).toBeVisible();
    });
    it('connects the actual forest to deep-link selection and reveals selected ancestry', async () => {
        window.history.replaceState({}, '', `/app/equipment-hierarchy/${vessel}?id=${child}`);
        render(<TechnicalWorkspace />);
        await english();
        expect(screen.getByRole('treeitem', { name: 'FICTIONAL pump' })).toHaveAttribute('aria-selected', 'true');
        expect(screen.getByRole('treeitem', { name: 'FICTIONAL engine' })).toHaveAttribute('aria-expanded', 'true');
        await userEvent.click(screen.getByRole('treeitem', { name: 'FICTIONAL engine' }));
        expect(location.search).toBe(`?id=${id}`);
    });
    it('keeps the complete forest when filtering and selects matching records', async () => {
        render(<TechnicalWorkspace />);
        await english();
        await userEvent.type(screen.getByLabelText('Filter list by name or code'), 'pump');
        await userEvent.click(screen.getByRole('button', { name: 'FICTIONAL pump' }));
        expect(screen.getByRole('treeitem', { name: 'FICTIONAL engine' })).toBeVisible();
        expect(location.search).toBe(`?id=${child}`);
    });
    it('preserves dirty drafts when navigation is declined', async () => {
        render(<TechnicalWorkspace />);
        await english();
        await userEvent.click(screen.getByRole('button', { name: 'Add draft' }));
        await userEvent.type(screen.getByLabelText('Equipment name'), 'FICTIONAL retained');
        await userEvent.click(screen.getByRole('button', { name: 'Plans' }));
        await userEvent.click(screen.getByRole('button', { name: 'Stay' }));
        expect(screen.getByLabelText('Equipment name')).toHaveValue('FICTIONAL retained');
    });
    it('closes the mobile chrome before presenting the dirty dialog and retains the draft when the user stays', async () => {
        render(<TechnicalWorkspace />);
        await english();
        await userEvent.click(screen.getByRole('button', { name: 'Add draft' }));
        await userEvent.type(screen.getByLabelText('Equipment name'), 'FICTIONAL mobile retained');
        await userEvent.click(screen.getByRole('button', { name: 'Menu' }));
        const navigation = screen.getByRole('dialog', { name: 'POSEIDON' });
        await userEvent.click(within(navigation).getByRole('link', { name: /^POSEIDON\b/ }));
        const confirmation = await screen.findByRole('dialog', { name: 'Unsaved changes' });
        expect(document.getElementById('technical-main')?.inert).toBe(false);
        expect(screen.queryByRole('dialog', { name: 'POSEIDON' })).not.toBeInTheDocument();
        await userEvent.click(within(confirmation).getByRole('button', { name: 'Stay' }));
        expect(screen.getByLabelText('Equipment name')).toHaveValue('FICTIONAL mobile retained');
    });
    it('freezes uncertain payload and reuses exact command ID and idempotency key', async () => {
        let attempts = 0;
        mutate = async init => { if (attempts++ === 0)
            throw new TypeError('synthetic transport loss'); return json({ id: JSON.parse(init.body as string).id, version: 8, revision: 2, replayed: true }); };
        render(<TechnicalWorkspace />);
        await english();
        await userEvent.click(screen.getByRole('button', { name: 'Add draft' }));
        await userEvent.type(screen.getByLabelText('Equipment name'), 'FICTIONAL new');
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Result uncertain. Retry the same attempt before editing.');
        expect(screen.getByLabelText('Equipment name')).toBeDisabled();
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        const posts = fetcher.mock.calls.filter(([, init]) => init?.method === 'POST');
        expect(posts).toHaveLength(2);
        expect(posts[0][1].body).toBe(posts[1][1].body);
        expect(posts[0][1].headers['Idempotency-Key']).toBe(posts[1][1].headers['Idempotency-Key']);
    });
    it('blocks stale or revoked writes and retains entered values until discard', async () => {
        mutate = async () => json({ code: 'version_conflict' }, 409);
        render(<TechnicalWorkspace />);
        await english();
        await userEvent.click(screen.getByRole('button', { name: 'Add draft' }));
        await userEvent.type(screen.getByLabelText('Equipment name'), 'FICTIONAL conflict');
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('The record changed. Reload and resolve your changes.');
        expect(screen.getByRole('button', { name: 'Save draft' })).toBeDisabled();
        expect(screen.getByLabelText('Equipment name')).toHaveValue('FICTIONAL conflict');
    });
    it('rejects incomplete/paged snapshots instead of fabricating a valid forest', async () => {
        fetcher.mockImplementation(async (url: string) => url.endsWith('/auth/session') ? json(currentSession) : json({ items: [asset], complete: false, canWrite: true }));
        render(<TechnicalWorkspace />);
        await screen.findByText('Ekipman yüklenemedi.');
        expect(screen.queryByRole('tree')).not.toBeInTheDocument();
    });
    it('uses exact strings for decimal declarations and does not fill missing readings with zero', async () => {
        window.history.replaceState({}, '', `/app/maintenance-plan/${vessel}`);
        render(<TechnicalWorkspace />);
        await english();
        await userEvent.click(screen.getByRole('button', { name: 'Add draft' }));
        await userEvent.selectOptions(screen.getByLabelText('Trigger', { exact: true }), 'Meter');
        await userEvent.type(screen.getByLabelText('Interval value (exact decimal)'), '0.100000000000000002');
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true));
        const post = fetcher.mock.calls.find(([, init]) => init?.method === 'POST')!;
        const body = JSON.parse(post[1].body);
        expect(body.inputs.intervalValue).toBe('0.100000000000000002');
        expect(body.inputs.currentMeter).toBeUndefined();
        expect(body.ownerRoleId).toBeNull();
        expect(body.jobId).toBeNull();
    });
    it('clears unauthorized snapshot data and shows operator load errors', async () => {
        fetcher.mockImplementation(async (url: string) => url.endsWith('/auth/session') ? json(currentSession) : json({ code: 'permission_denied' }, 403));
        render(<TechnicalWorkspace />);
        await screen.findByText('Erişim kullanılamıyor.');
        expect(screen.queryByText('FICTIONAL engine')).not.toBeInTheDocument();
        expect(screen.queryByRole('tree')).not.toBeInTheDocument();
    });
});

describe('TECHNICAL-COMMAND-OWNER-RECOVERY-01 (authored UI cases; execution deferred)', () => {
    it.each(['actor', 'tenant'] as const)('withdraws an unsent private editor after detected %s replacement and requires the original owner', async kind => {
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        const original = { ...currentSession };
        currentSession = { ...currentSession, ...(kind === 'actor' ? { actorId: child } : { tenantId: child }) };
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText(/Only the original account can continue/);
        expect(screen.queryByLabelText('Equipment name')).not.toBeInTheDocument();
        expect(screen.queryByText('FICTIONAL engine')).not.toBeInTheDocument();
        expect(posts()).toHaveLength(0);
        currentSession = { ...original, csrfToken: 'synthetic-refreshed-token' };
        await reauthorize();
        expect(await screen.findByLabelText('Equipment name')).toHaveValue('FICTIONAL private draft');
        expect(posts()).toHaveLength(0);
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(1);
        expect(posts()[0][1].headers['X-CSRF-TOKEN']).toBe('synthetic-refreshed-token');
    });

    it('retains the exact uncertain equipment command through a later403 and explicit source reauthorization', async () => {
        let count = 0;
        mutate = async init => {
            if (++count === 1) throw new TypeError('synthetic lost committed response');
            if (count === 2) return json({ code: 'permission_denied' }, 403);
            return json({ id: JSON.parse(init.body as string).id, revision: 1, version: 8, replayed: true });
        };
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Result uncertain. Retry the same attempt before editing.');
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await waitFor(() => expect(screen.queryByLabelText('Equipment name')).not.toBeInTheDocument());
        expect(posts()).toHaveLength(2);
        currentSession = { ...currentSession, csrfToken: 'synthetic-current-csrf' };
        await reauthorize();
        expect(await screen.findByLabelText('Equipment name')).toBeDisabled();
        expect(posts()).toHaveLength(2);
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(3);
        for (const [, init] of posts()) {
            expect(init.body).toBe(posts()[0][1].body);
            expect(init.headers['Idempotency-Key']).toBe(posts()[0][1].headers['Idempotency-Key']);
        }
        expect(posts()[2][1].headers['X-CSRF-TOKEN']).toBe('synthetic-current-csrf');
    });

    it('keeps an uncertain plan UUID, base and exact decimals through a session lookup failure', async () => {
        window.history.replaceState({}, '', `/app/maintenance-plan/${vessel}`);
        let failSession = false, count = 0;
        const base = fetcher.getMockImplementation()!;
        fetcher.mockImplementation(async (url: string, init?: RequestInit) => url.endsWith('/auth/session') && failSession ? json({}, 503) : base(url, init));
        mutate = async init => {
            if (++count === 1) throw new TypeError('synthetic plan acknowledgement loss');
            return json({ id: JSON.parse(init.body as string).id, revision: 1, version: 9, replayed: true });
        };
        render(<TechnicalWorkspace />); await english();
        await userEvent.click(screen.getByRole('button', { name: 'Add draft' }));
        await userEvent.type(screen.getByLabelText('Interval value (exact decimal)'), '0.100000000000000002');
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Result uncertain. Retry the same attempt before editing.');
        failSession = true;
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await waitFor(() => expect(screen.getByRole('button', { name: 'Retry same attempt' })).toBeEnabled());
        expect(posts()).toHaveLength(1);
        failSession = false;
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(2);
        expect(posts()[1][1].body).toBe(posts()[0][1].body);
        const command = JSON.parse(posts()[0][1].body);
        expect(command.baseVersion).toBeNull();
        expect(command.inputs.intervalValue).toBe('0.100000000000000002');
        expect(posts()[1][1].headers['Idempotency-Key']).toBe(posts()[0][1].headers['Idempotency-Key']);
    });

    it('does not consume a receipt after replacement identity and lets only the restored owner recover it', async () => {
        const original = { ...currentSession };
        let count = 0;
        mutate = async init => {
            if (++count === 1) currentSession = { ...original, actorId: child };
            return json({ id: JSON.parse(init.body as string).id, revision: 1, version: 8, replayed: count > 1 });
        };
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText(/Only the original account can continue/);
        expect(location.search).toBe('');
        expect(screen.queryByLabelText('Equipment name')).not.toBeInTheDocument();
        expect(screen.queryByText('Draft saved.')).not.toBeInTheDocument();
        currentSession = original; await reauthorize();
        expect(await screen.findByLabelText('Equipment name')).toBeDisabled();
        expect(posts()).toHaveLength(1);
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        const first = posts()[0][1];
        expect(location.search).toBe(`?id=${JSON.parse(first.body).id}`);
        expect(posts()[1][1].body).toBe(first.body);
        expect(posts()[1][1].headers['Idempotency-Key']).toBe(first.headers['Idempotency-Key']);
    });

    it('persists the confirmed canonical ID and read-only outcome before failed optional readback', async () => {
        let blocked = false, savedId = '';
        const base = fetcher.getMockImplementation()!;
        fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
            if (!init?.method && url.endsWith(`/equipment-hierarchy/${vessel}`) && savedId)
                return blocked ? json({}, 503) : json({ items: [{ ...asset, id: savedId }], complete: true, canWrite: true, canReadHistory: true });
            return base(url, init);
        });
        mutate = async init => { savedId = JSON.parse(init.body as string).id; blocked = true; return json({ id: savedId, revision: 1, version: 8, replayed: false }); };
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Equipment could not be loaded.');
        expect(location.search).toBe(`?id=${savedId}`);
        expect(screen.getByText(savedId)).toBeVisible();
        expect(screen.getByText('Draft saved.')).toBeVisible();
        expect(screen.queryByRole('button', { name: 'Add draft' })).not.toBeInTheDocument();
        expect(screen.queryByLabelText('Equipment name')).not.toBeInTheDocument();
        blocked = false; await reauthorize();
        await screen.findByRole('button', { name: 'Edit draft' });
        expect(location.search).toBe(`?id=${savedId}`);
        expect(posts()).toHaveLength(1);
    });

    it('preserves the job UUID/body through unknown response and owner replacement without inventing a command key', async () => {
        window.history.replaceState({}, '', `/app/maintenance-plan/${vessel}`);
        const original = { ...currentSession };
        let count = 0;
        mutate = async init => { if (++count === 1) throw new TypeError('synthetic job response loss'); return json({ id: JSON.parse(init.body as string).id }); };
        render(<TechnicalWorkspace />); await english();
        await userEvent.click(screen.getByRole('button', { name: 'Add maintenance job' }));
        await userEvent.type(screen.getByLabelText('Job code'), 'SYNTHETIC-JOB');
        await userEvent.type(screen.getByLabelText('Job title'), 'FICTIONAL owner job');
        await userEvent.click(screen.getByRole('button', { name: 'Save job' }));
        await screen.findByText('Result uncertain. Retry the same attempt before editing.');
        currentSession = { ...original, actorId: child };
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText(/Only the original account can continue/);
        expect(screen.queryByLabelText('Job title')).not.toBeInTheDocument();
        expect(posts()).toHaveLength(1);
        currentSession = { ...original, csrfToken: 'synthetic-new-job-csrf' }; await reauthorize();
        expect(await screen.findByLabelText('Job title')).toHaveValue('FICTIONAL owner job');
        expect(screen.getByLabelText('Job title')).toBeDisabled();
        expect(posts()).toHaveLength(1);
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(2);
        expect(posts()[1][0]).toBe(posts()[0][0]);
        expect(posts()[1][1].body).toBe(posts()[0][1].body);
        expect(posts()[1][1].headers['Idempotency-Key']).toBeUndefined();
        expect(posts()[1][1].headers['X-CSRF-TOKEN']).toBe('synthetic-new-job-csrf');
    });

    it('withdraws draft DOM on focus and resumes only after fresh original-owner source access without posting', async () => {
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        const original = { ...currentSession };
        currentSession = { ...original, tenantId: child };
        fireEvent(window, new Event('focus'));
        await screen.findByText(/Only the original account can continue/);
        expect(screen.queryByLabelText('Equipment name')).not.toBeInTheDocument();
        currentSession = original; await reauthorize();
        expect(await screen.findByLabelText('Equipment name')).toHaveValue('FICTIONAL private draft');
        expect(posts()).toHaveLength(0);
    });

    it('does not publish an old equipment read when identity changed during that read', async () => {
        const pending = deferred<Response>(), base = fetcher.getMockImplementation()!;
        fetcher.mockImplementation(async (url: string, init?: RequestInit) => url.endsWith(`/equipment-hierarchy/${vessel}`) && !init?.method ? pending.promise : base(url, init));
        render(<TechnicalWorkspace />);
        await waitFor(() => expect(fetcher.mock.calls.some(([url]) => url.endsWith(`/equipment-hierarchy/${vessel}`))).toBe(true));
        currentSession = { ...currentSession, actorId: child };
        await act(async () => pending.resolve(json({ items: [asset], complete: true, canWrite: true, canReadHistory: true })));
        expect(screen.queryByText('FICTIONAL engine')).not.toBeInTheDocument();
        expect(screen.queryByRole('tree')).not.toBeInTheDocument();
        expect(posts()).toHaveLength(0);
    });

    it('ignores a history completion from a previously selected resource', async () => {
        window.history.replaceState({}, '', `/app/equipment-hierarchy/${vessel}?id=${id}`);
        const pending = deferred<Response>(), base = fetcher.getMockImplementation()!;
        fetcher.mockImplementation(async (url: string, init?: RequestInit) => url.endsWith('/history') ? pending.promise : base(url, init));
        render(<TechnicalWorkspace />); await english();
        await userEvent.click(screen.getByRole('button', { name: 'History' }));
        const root = screen.getByRole('treeitem', { name: 'FICTIONAL engine' });
        await act(async () => root.focus());
        await userEvent.keyboard('{ArrowRight}');
        expect(root).toHaveAttribute('aria-expanded', 'true');
        await userEvent.click(screen.getByRole('treeitem', { name: 'FICTIONAL pump' }));
        await act(async () => pending.resolve(json([{ revision: 91, savedAt: '2026-11-01T00:00:00Z', asset: { name: 'FORMER PRIVATE HISTORY' } }])));
        expect(location.search).toBe(`?id=${child}`);
        expect(screen.queryByText('FORMER PRIVATE HISTORY')).not.toBeInTheDocument();
    });

    it('ignores a delayed receipt from an unmounted workspace', async () => {
        const pending = deferred<Response>();
        mutate = async () => pending.promise;
        const view = render(<TechnicalWorkspace />); await english(); await addEquipment();
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await waitFor(() => expect(posts()).toHaveLength(1));
        const command = JSON.parse(posts()[0][1].body);
        view.unmount();
        await act(async () => pending.resolve(json({ id: command.id, revision: 1, version: 8, replayed: false })));
        expect(location.search).toBe('');
        expect(posts()).toHaveLength(1);
        expect(screen.queryByText('Draft saved.')).not.toBeInTheDocument();
    });

    it('uses refreshed CSRF for the same canonical owner without treating GUID casing as another identity', async () => {
        currentSession = { ...currentSession, actorId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' };
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        currentSession = { ...currentSession, actorId: currentSession.actorId.toUpperCase(), csrfToken: 'synthetic-fresh-csrf' };
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(1);
        expect(posts()[0][1].headers['X-CSRF-TOKEN']).toBe('synthetic-fresh-csrf');
    });

    it('retains an unknown command while current vessel write access is revoked and recovers only after explicit refresh', async () => {
        let writable = true, count = 0;
        const base = fetcher.getMockImplementation()!;
        fetcher.mockImplementation(async (url: string, init?: RequestInit) => !init?.method && url.endsWith(`/equipment-hierarchy/${vessel}`)
            ? json({ items: [asset], complete: true, canWrite: writable, canReadHistory: true }) : base(url, init));
        mutate = async init => { if (++count === 1) throw new TypeError('synthetic response loss'); return json({ id: JSON.parse(init.body as string).id, revision: 1, version: 8, replayed: true }); };
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Result uncertain. Retry the same attempt before editing.');
        writable = false;
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await waitFor(() => expect(screen.queryByLabelText('Equipment name')).not.toBeInTheDocument());
        expect(posts()).toHaveLength(1);
        await reauthorize();
        expect(await screen.findByLabelText('Equipment name')).toBeDisabled();
        expect(screen.getByRole('button', { name: 'Retry same attempt' })).toBeDisabled();
        writable = true; await reauthorize();
        await waitFor(() => expect(screen.getByRole('button', { name: 'Retry same attempt' })).toBeEnabled());
        expect(posts()).toHaveLength(1);
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(2);
        expect(posts()[1][1].body).toBe(posts()[0][1].body);
        expect(posts()[1][1].headers['Idempotency-Key']).toBe(posts()[0][1].headers['Idempotency-Key']);
    });

    it('treats a success response for another UUID as unknown and never adopts its URL', async () => {
        let count = 0;
        mutate = async init => json({ id: ++count === 1 ? child : JSON.parse(init.body as string).id, revision: 1, version: 8, replayed: count > 1 });
        render(<TechnicalWorkspace />); await english(); await addEquipment();
        await userEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await screen.findByText('Result uncertain. Retry the same attempt before editing.');
        expect(location.search).toBe('');
        expect(screen.queryByText('Draft saved.')).not.toBeInTheDocument();
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(2);
        expect(posts()[1][1].body).toBe(posts()[0][1].body);
        expect(location.search).toBe(`?id=${JSON.parse(posts()[0][1].body).id}`);
    });

    it.each(['00000000-0000-0000-0000-000000000000', `${id}\n`])('does not establish owner context from an invalid session actor %j', async actorId => {
        currentSession = { ...currentSession, actorId };
        render(<TechnicalWorkspace />);
        await screen.findByRole('alert');
        expect(screen.queryByRole('tree')).not.toBeInTheDocument();
        expect(fetcher.mock.calls.some(([url]) => url.includes('/equipment-hierarchy/'))).toBe(false);
        expect(posts()).toHaveLength(0);
    });
});

// MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01: authored, execution deferred.
function observationFixture() {
    return { sources: [{ tenantId: vessel, vesselId: vessel, assetId: id, source: 'synthetic-meter', version: 'synthetic-v1', kind: 'Meter', unit: 'hours', freshnessSeconds: 3600, maxScale: 4 }], items: [], canWrite: true, state: 'ConfiguredDeclarationsOnly' };
}
function actualObservationTransport() {
    const base = fetcher.getMockImplementation()!;
    fetcher.mockImplementation(async (url: string, init?: RequestInit) => !init?.method && url.endsWith('/observations') ? json(observationFixture()) : base(url, init));
    window.history.replaceState({}, '', `/app/equipment-hierarchy/${vessel}?id=${id}`);
}
async function fillObservationDraft() {
    await userEvent.selectOptions(await screen.findByLabelText('Configured source'), 'synthetic-meter');
    await userEvent.type(screen.getByLabelText('Source revision'), '17');
    await userEvent.type(screen.getByLabelText('Observed value (exact declaration)'), '19.5000');
}
describe('owning observation recovery inside the actual technical workspace', () => {
    it('withdraws cached parent equipment after a child-only source refresh detects a denied session', async () => {
        actualObservationTransport();
        const base = fetcher.getMockImplementation()!; let deniedSession = false;
        fetcher.mockImplementation(async (url: string, init?: RequestInit) => url.endsWith('/auth/session') && deniedSession ? json({}, 401) : base(url, init));
        render(<TechnicalWorkspace />); await english(); await fillObservationDraft();
        deniedSession = true;
        await userEvent.click(screen.getByRole('button', { name: 'Retry source read' }));
        await waitFor(() => expect(screen.queryByText('FICTIONAL engine')).not.toBeInTheDocument());
        expect(screen.queryByLabelText('Observed value (exact declaration)')).not.toBeInTheDocument();
        expect(posts()).toHaveLength(0);
        deniedSession = false;
        await userEvent.click(screen.getByRole('button', { name: 'Retry source read' }));
        expect(await screen.findByLabelText('Observed value (exact declaration)')).toHaveValue('19.5000');
        expect(posts()).toHaveLength(0);
    });
    it('discards observation inputs only after explicit dirty-navigation confirmation and never resurrects them on return', async () => {
        actualObservationTransport();
        render(<TechnicalWorkspace />); await english(); await fillObservationDraft();
        await userEvent.click(screen.getByRole('button', { name: 'Plans' }));
        await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Discard' }));
        await screen.findByRole('heading', { name: 'Maintenance plan' });
        await userEvent.click(screen.getByRole('button', { name: 'Equipment' }));
        await userEvent.click(await screen.findByRole('treeitem', { name: 'FICTIONAL engine' }));
        expect(await screen.findByLabelText('Observed value (exact declaration)')).toHaveValue('');
        expect(screen.getByLabelText('Source revision')).toHaveValue('');
        expect(posts()).toHaveLength(0);
    });
    it('blocks parent navigation and discard while an observation POST is in flight', async () => {
        actualObservationTransport();
        const outcome = deferred<Response>();
        mutate = async () => outcome.promise;
        render(<TechnicalWorkspace />); await english(); await fillObservationDraft();
        await userEvent.click(screen.getByRole('button', { name: 'Record observation' }));
        await waitFor(() => expect(posts()).toHaveLength(1));
        expect(screen.getByRole('button', { name: 'Plans' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'Add draft' })).toBeDisabled();
        await userEvent.click(screen.getByRole('button', { name: 'Plans' }));
        expect(location.pathname).toBe(`/app/equipment-hierarchy/${vessel}`);
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
        const body = JSON.parse(posts()[0][1].body);
        await act(async () => outcome.resolve(json({ id: body.id, replayed: false })));
        await screen.findByText('Draft saved.');
        await waitFor(() => expect(screen.getByRole('button', { name: 'Plans' })).toBeEnabled());
    });
    it('withdraws the entire former-owner private workspace and restores the same uncertain observation only for that owner', async () => {
        actualObservationTransport();
        const original = { ...currentSession }; let count = 0;
        mutate = async init => { if (++count === 1) throw new TypeError('synthetic observation response loss'); return json({ id: JSON.parse(init.body as string).id, replayed: true }); };
        render(<TechnicalWorkspace />); await english(); await fillObservationDraft();
        await userEvent.click(screen.getByRole('button', { name: 'Record observation' }));
        await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.');
        currentSession = { ...original, actorId: child };
        await act(async () => fireEvent(window, new Event('focus')));
        await screen.findByText(/Only the original account can continue/);
        expect(screen.queryByLabelText('Observed value (exact declaration)')).not.toBeInTheDocument();
        expect(screen.queryByText('FICTIONAL engine')).not.toBeInTheDocument();
        expect(posts()).toHaveLength(1);
        currentSession = { ...original, csrfToken: 'synthetic-restored-observation-owner' };
        await userEvent.click(screen.getByRole('button', { name: 'Retry source read' }));
        expect(await screen.findByLabelText('Observed value (exact declaration)')).toHaveValue('19.5000');
        expect(screen.getByLabelText('Observed value (exact declaration)')).toBeDisabled();
        expect(posts()).toHaveLength(1);
        await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt' }));
        await screen.findByText('Draft saved.');
        expect(posts()).toHaveLength(2);
        expect(posts()[1][1].body).toBe(posts()[0][1].body);
        expect(posts()[1][1].headers['Idempotency-Key']).toBe(posts()[0][1].headers['Idempotency-Key']);
        expect(posts()[1][1].headers['X-CSRF-TOKEN']).toBe('synthetic-restored-observation-owner');
    });
    it('keeps a dirty observation through same-owner focus revalidation and rotated CSRF without sending it', async () => {
        actualObservationTransport();
        render(<TechnicalWorkspace />); await english(); await fillObservationDraft();
        currentSession = { ...currentSession, csrfToken: 'synthetic-rotated-observation-csrf' };
        await act(async () => fireEvent(window, new Event('focus')));
        expect(await screen.findByLabelText('Observed value (exact declaration)')).toHaveValue('19.5000');
        expect(screen.getByLabelText('Source revision')).toHaveValue('17');
        expect(posts()).toHaveLength(0);
    });
});
