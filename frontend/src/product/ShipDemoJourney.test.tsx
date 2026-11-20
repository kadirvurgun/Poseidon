import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from './api'
import { ShipDemoJourney, originReuse, readShipDemoRoute, shipDemoHref, shipDemoSteps } from './ShipDemoJourney'
import { shipOperationsApi, type ShipOpsRecord } from './ship-operations-api'

// S10 WP-C DEV-120 (18.01 sheet:Backlog/row:121) ship buyer demo: ordered guided steps over existing screens,
// visible "Why", single-entry reuse proven only by server origin pins, no second entry form.
vi.mock('./EnterpriseChrome', () => ({ EnterpriseChrome: ({ onLanguageChange }: { onLanguageChange: (value: 'en' | 'tr') => void }) => <><button onClick={() => onLanguageChange('en')}>EN</button><button onClick={() => onLanguageChange('tr')}>TR</button></> }))
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), session: vi.fn(), vessels: vi.fn() }))
vi.mock('./ship-operations-api', async original => {
  const actual = await original<typeof import('./ship-operations-api')>()
  return { ...actual, shipOperationsApi: { ...actual.shipOperationsApi, list: vi.fn() } }
})
const vessel = '11111111-1111-4111-8111-111111111111', tenant = '10000000-0000-4000-8000-000000000002'
const record = (origin: boolean) => ({ origin: origin ? { kind: 'Noon', id: '22222222-2222-4222-8222-222222222222', revision: 1, version: 1, hash: 'A'.repeat(64), sourceVersion: 'x' } : null }) as unknown as ShipOpsRecord
const page = (items: ShipOpsRecord[]) => ({ items, offset: 0, limit: 25 as const, hasMore: false, canWrite: false, sourceVersion: 'x', offline: { supported: false as const, state: 'UNAVAILABLE' as const, operation: 'ShipOpsDraft' as const } })
beforeEach(() => {
  history.replaceState(null, '', `/app/ship-demo/${vessel}`)
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  vi.mocked(api.session).mockResolvedValue({ authenticated: true, canRead: true, actorId: tenant, tenantId: tenant, tenantName: 'FICTIONAL tenant', csrfToken: 'synthetic-only', setupRequired: false, canCreate: false, canReadCompanies: false, canSearch: false })
  vi.mocked(api.vessels).mockResolvedValue({ items: [{ id: vessel, companyId: tenant, code: 'SYN-1', name: 'FICTIONAL vessel', kind: 'Ship', version: 1, canEdit: false }], offset: 0, limit: 25, hasMore: false })
  vi.mocked(shipOperationsApi.list).mockImplementation(async kind => kind === 'Fuel' ? page([record(true), record(true), record(false)]) : page([record(true)]))
})
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

describe('ShipDemoJourney', () => {
  it('walks the six steps in order with Why panels and deep links to existing screens only', async () => {
    const user = userEvent.setup(); render(<ShipDemoJourney />); await user.click(screen.getByRole('button', { name: 'EN' }))
    const steps = await screen.findByRole('list', { name: 'Demo steps' })
    expect(within(steps).getAllByRole('listitem').map(li => li.getAttribute('data-step'))).toEqual(['departure', 'daily', 'fuel', 'sof', 'downtime', 'readiness'])
    expect(within(steps).getAllByText('Why')).toHaveLength(6)
    expect(within(steps).getAllByRole('link', { name: 'Open screen' }).map(a => a.getAttribute('href'))).toEqual(shipDemoSteps.map(s => shipDemoHref(s, vessel)))
    expect(screen.queryByRole('textbox')).toBeNull()
    expect(await screen.findByTestId('ship-demo-reuse-fuel')).toHaveTextContent('2 reuse a pinned origin, 1 entered directly')
    expect(screen.getByTestId('ship-demo-reuse-port')).toHaveTextContent('1 reuse a pinned origin, 0 entered directly')
    expect(screen.getByText(/Synthetic demo data/)).toBeInTheDocument()
  })
  it('shows forbidden and offline states instead of guessing reuse', async () => {
    vi.mocked(shipOperationsApi.list).mockRejectedValue(new api.ApiError(403, 'permission_denied'))
    const user = userEvent.setup(); const { unmount } = render(<ShipDemoJourney />); await user.click(screen.getByRole('button', { name: 'EN' }))
    expect(await screen.findByText('You do not have access to this information.')).toBeInTheDocument()
    unmount(); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false); render(<ShipDemoJourney />)
    expect(screen.getAllByRole('status').some(s => s.textContent !== '')).toBe(true)
    expect(screen.queryByRole('list', { name: 'Demo steps' })).toBeNull()
  })
  it('parses only canonical demo routes and counts reuse only from pins', () => {
    expect(readShipDemoRoute(`/app/ship-demo/${vessel}`, '')).toEqual({ vessel, invalid: false })
    expect(readShipDemoRoute('/app/ship-demo/not-a-guid', '').invalid).toBe(true)
    expect(readShipDemoRoute('/app/ship-demo', '?x=1').invalid).toBe(true)
    expect(originReuse([record(false)])).toEqual({ pinned: 0, direct: 1 })
    expect(shipDemoHref('fuel', vessel)).toBe(`/app/fuel-bunker-log/${vessel}`)
    expect(shipDemoHref('departure', vessel)).toBe(`/app/departure-signoff/${vessel}`)
  })
})
