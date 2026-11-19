import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { changeAppLanguage } from '../i18n';
import { ShipOperationsLedgerPanel } from './ShipOperationsLedgerPanel';
import { shipOpsRecordId, shipOpsVessel } from './ship-operation-test-fixtures';
import { fuelLedgerFixture, portTimelineFixture } from './ship-operations-ledger-test-fixtures';
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status });
function transport(ledger: unknown, status = 200) {
    const fetch = vi.fn(async (url: string, init?: RequestInit) => {
        if (init?.method && init.method !== "GET") return json({ code: "unexpected_write" }, 500);
        const path = new URL(url, 'https://synthetic.invalid').pathname;
        if (path === '/api/v1/fuel-bunker-logs/ledger' || path === '/api/v1/port-activity-sof/timeline') return json(ledger, status);
        return json({ code: 'permission_denied' }, 403);
    });
    vi.stubGlobal('fetch', fetch);
    return fetch;
}
beforeEach(async () => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); await changeAppLanguage('en', false); });
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
describe('T-OPS2-004/005 ledger review aid panel', () => {
    it('shows a ROB gap as review required with the exact mt difference and no verified state', async () => {
        const fetch = transport(fuelLedgerFixture()), onOpen = vi.fn();
        render(<ShipOperationsLedgerPanel kind="Fuel" vessel={shipOpsVessel} language="en" onOpen={onOpen}/>);
        expect(await screen.findByText(/Difference: -0.5 mt/)).toBeInTheDocument();
        expect(screen.getAllByText('Review required').length).toBeGreaterThan(0);
        expect(screen.getByRole('list', { name: 'Review flags' })).toHaveTextContent('ROB_CONTINUITY_GAP');
        expect(screen.queryByText(/verified/i)).toBeNull();
        // Noon reviewed context is optional: a denied Noon read does not hide the ledger.
        expect(screen.getByText('Reviewed Noon context is unavailable for your access.')).toBeInTheDocument();
        await userEvent.setup().click(screen.getByRole('button', { name: 'Open record' }));
        expect(onOpen).toHaveBeenCalledWith(shipOpsRecordId);
        expect(fetch.mock.calls.every(([, init]) => !init?.method || init.method === 'GET')).toBe(true);
    });
    it('shows the SOF timeline interval in exact minutes and states that no signature is recorded', async () => {
        transport(portTimelineFixture());
        render(<ShipOperationsLedgerPanel kind="Port" vessel={shipOpsVessel} language="en" onOpen={vi.fn()}/>);
        expect(await screen.findByText('60 min')).toBeInTheDocument();
        expect(screen.getByText(/No Statement of Facts signature or finalization is recorded/)).toBeInTheDocument();
    });
    it('fails closed with a retry on a denied or malformed ledger read', async () => {
        transport({ ...fuelLedgerFixture(), qualifications: [] });
        render(<ShipOperationsLedgerPanel kind="Fuel" vessel={shipOpsVessel} language="en" onOpen={vi.fn()}/>);
        expect(await screen.findByRole('alert')).toHaveTextContent('The review aid is unavailable for your current access.');
        expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
    });
});
