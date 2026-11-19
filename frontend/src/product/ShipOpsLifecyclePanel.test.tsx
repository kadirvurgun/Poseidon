import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { changeAppLanguage } from '../i18n';
import { DowntimeLifecyclePanel, SofFinalizationPanel } from './ShipOpsLifecyclePanel';
import { shipOpsActor, shipOpsRecordFixture, shipOpsRecordId, shipOpsTenant, shipOpsVessel } from './ship-operation-test-fixtures';
import { downtimeLifecycleFixture, sofFinalizationFixture } from './ship-ops-lifecycle-test-fixtures';
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status });
function transport(view: unknown, receipt: unknown, status = 200) {
    const fetch = vi.fn(async (url: string, init?: RequestInit) => {
        const path = new URL(url, 'https://synthetic.invalid').pathname;
        if (path === '/api/v1/auth/session') return json({ authenticated: true, canRead: true, actorId: shipOpsActor, tenantId: shipOpsTenant, csrfToken: 'synthetic-csrf' });
        if (init?.method === 'POST') return status === 200 ? json(receipt) : json({ code: 'separation_of_duties' }, status);
        if (path.endsWith('/finalization') || path.endsWith('/lifecycle')) return json(view);
        return json({ code: 'permission_denied' }, 403);
    });
    vi.stubGlobal('fetch', fetch);
    return fetch;
}
const posts = (fetch: ReturnType<typeof transport>) => fetch.mock.calls.filter(([, init]) => init?.method === 'POST');
beforeEach(async () => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); await changeAppLanguage('en', false); });
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
describe('T-OPS2-005 SOF finalization panel', () => {
    it('finalizes only after every unresolved reference is acknowledged and an appointment chosen, without claiming a signature', async () => {
        const final = { ...shipOpsRecordFixture('Port'), revision: 4, version: 8, currentRevision: false, canEdit: false, fields: { ...shipOpsRecordFixture('Port').fields, sof_state: { state: 'Known', value: 'FINAL' } } };
        const fetch = transport(sofFinalizationFixture(), final), onCommitted = vi.fn(), user = userEvent.setup();
        render(<SofFinalizationPanel vessel={shipOpsVessel} id={shipOpsRecordId} revision={3} language="en" onCommitted={onCommitted}/>);
        const finalize = await screen.findByRole('button', { name: 'Finalize SOF' });
        expect(screen.getByText('Signature not recorded')).toBeInTheDocument();
        expect(finalize).toBeDisabled();
        for (const box of screen.getAllByRole('checkbox')) await user.click(box);
        expect(finalize).toBeDisabled();
        await user.selectOptions(screen.getByLabelText('Appointment'), 'Master');
        await user.click(finalize);
        await screen.findByText('Command recorded.');
        expect(onCommitted).toHaveBeenCalledOnce();
        const [path, init] = posts(fetch)[0];
        expect(path).toContain('/port-activity-sof/' + shipOpsRecordId + '/finalize');
        expect(JSON.parse(init!.body as string).acknowledgedUnresolved).toEqual(['port', 'port_call_id', 'voyage_id']);
        expect((init!.headers as Record<string, string>)['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/);
    });
    it('shows that no appointment exists instead of a finalize action', async () => {
        transport({ ...sofFinalizationFixture(), authorities: [], canFinalize: false }, null);
        render(<SofFinalizationPanel vessel={shipOpsVessel} id={shipOpsRecordId} revision={3} language="en" onCommitted={vi.fn()}/>);
        expect(await screen.findByText('No current finalization appointment for this vessel.')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Finalize SOF' })).toBeNull();
    });
});
describe('T-OPS2-006 downtime lifecycle panel', () => {
    it('shows the Loss of Hire candidate as review required and reports a separation-of-duties refusal without committing', async () => {
        const fetch = transport(downtimeLifecycleFixture(), null, 403), onCommitted = vi.fn(), user = userEvent.setup();
        render(<DowntimeLifecyclePanel vessel={shipOpsVessel} id={shipOpsRecordId} revision={2} language="en" onCommitted={onCommitted}/>);
        expect(await screen.findByText('Review required: candidate only, no entitlement or amount')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Verify' })).toBeDisabled();
        expect(screen.queryByRole('button', { name: 'Close' })).toBeNull();
        await user.selectOptions(screen.getByLabelText('Appointment'), 'ChiefEngineer');
        await user.click(screen.getByRole('button', { name: 'Verify' }));
        expect(await screen.findByRole('alert')).toHaveTextContent('separation_of_duties');
        expect(onCommitted).not.toHaveBeenCalled();
        expect(posts(fetch)).toHaveLength(1);
    });
    it('tells the recorder that another appointed person must verify', async () => {
        transport({ ...downtimeLifecycleFixture(), actorIsRecorder: true, allowedActions: ['RETURN_FOR_CORRECTION'] }, null);
        render(<DowntimeLifecyclePanel vessel={shipOpsVessel} id={shipOpsRecordId} revision={2} language="en" onCommitted={vi.fn()}/>);
        expect(await screen.findByText(/another appointed person must verify and close it/)).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Verify' })).toBeNull();
    });
});
