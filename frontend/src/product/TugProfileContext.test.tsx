import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TugProfileContext } from './TugProfileContext'
import { decodeTugContext, type TugContext } from './tug-api'
const vessel = '10000000-0000-4000-8000-000000000001', tenant = '10000000-0000-4000-8000-000000000002', other = '10000000-0000-4000-8000-000000000003'
const source = (): TugContext => ({ vessel: { id: vessel, code: 'FICTIONAL', name: 'Synthetic tug' }, mode: 'Tug', crossMode: false, roleReferences: null, roleLens: { roleState: 'Missing', roleCatalogVersion: null, roles: [], lensState: 'Missing', lensVersion: null, lenses: [] }, shipModeDestinations: [], canWrite: false, owners: [], jobTypes: ['Harbour Towage'], priorities: ['Normal'], requestSources: ['Customer'], tugType: 'Unavailable', tugTypeContext: null, tugTypeOptions: [], tugTypePolicyVersion: null, bollardPull: { value: '45.01000000000000000000001', version: 7, provenance: 'LegacyMeasurementUnverified', freshness: 'Unknown', observedAt: null, source: 'TugProfile' }, equipment: null, equipmentCoverage: 'Unavailable', readiness: 'Unknown', reason: 'VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable' })
describe('T-TUG-001 scoped profile source (authored; execution deferred)', () => {
  it('rejects foreign scope, manufactured readiness and numeric coercion of BP', () => {
    const c = source(); expect(decodeTugContext(c, vessel, tenant).bollardPull.value).toBe(c.bollardPull.value)
    for (const mutation of [{ vessel: { ...c.vessel, id: other } }, { mode: 'Ship' }, { crossMode: 'true' }, { readiness: 'Ready' }, { bollardPull: { ...c.bollardPull, value: 45.01 } }, { bollardPull: { ...c.bollardPull, freshness: 'Current' } }, { equipment: [], equipmentCoverage: 'Unavailable' }]) expect(() => decodeTugContext({ ...c, ...mutation }, vessel, tenant)).toThrow()
  })
  it('binds configured type to its actual vessel/tenant/source revision and controlled options', () => {
    const c = { ...source(), tugType: 'SYNTHETIC', tugTypeContext: { tenantId: tenant, vesselId: vessel, code: 'SYNTHETIC', sourceReference: 'fixture/source', sourceRevision: 3, observedAt: '2026-11-04T08:00:00Z' }, tugTypeOptions: [{ code: 'SYNTHETIC', en: 'Synthetic type', tr: 'Sentetik tip' }], tugTypePolicyVersion: 'fixture-v1' }
    expect(decodeTugContext(c, vessel, tenant).tugTypeContext?.sourceRevision).toBe(3)
    for (const binding of [{ ...c.tugTypeContext, vesselId: other }, { ...c.tugTypeContext, tenantId: other }, { ...c.tugTypeContext, sourceRevision: 0 }, { ...c.tugTypeContext, code: 'UNCONFIGURED' }]) expect(() => decodeTugContext({ ...c, tugTypeContext: binding }, vessel, tenant)).toThrow()
    render(<TugProfileContext context={decodeTugContext(c, vessel, tenant)} language="en" offline={false} />)
    expect(screen.getByText('fixture/source / 3')).toBeVisible()
    expect(screen.getByText('2026-11-04T08:00:00Z')).toHaveAttribute('datetime', '2026-11-04T08:00:00Z')
    expect(screen.getByText('Synthetic type')).toBeVisible()
    expect(screen.getByText(/Ship operations require an explicitly authorized cross-mode role/)).toBeVisible()
    expect(screen.queryByRole('link', { name: /Ship/ })).not.toBeInTheDocument()
  })
  it('distinguishes unavailable inventory from zero declarations and withdraws source content offline', () => {
    const c = source(), view = render(<TugProfileContext context={c} language="en" offline={false} />)
    expect(screen.getByText(/Equipment source unavailable or not granted/)).toBeVisible()
    expect(screen.getByText(/45.01000000000000000000001/)).toBeVisible()
    view.rerender(<TugProfileContext context={{ ...c, equipment: [], equipmentCoverage: 'DraftDeclarationsOnly' }} language="en" offline={false} />)
    expect(screen.getByText(/No equipment declarations returned/)).toBeVisible()
    view.rerender(<TugProfileContext context={c} language="en" offline />)
    expect(screen.getByRole('status')).toHaveTextContent('Current tug profile sources unavailable offline')
    expect(screen.queryByText(/45.01000000000000000000001/)).not.toBeInTheDocument()
  })
})
const role = '20000000-0000-4000-8000-000000000001'
const configured = (crossMode: boolean): TugContext => ({ ...source(), crossMode, roleReferences: role,
  roleLens: { roleState: 'Configured', roleCatalogVersion: 'synthetic-label-v1', roles: [{ reference: role, tr: 'KURGUSAL römorkör operasyon sorumlusu', en: 'FICTIONAL tug operations lead' }], lensState: 'Configured', lensVersion: 'synthetic-menu-v1', lenses: [{ reference: role, tr: 'KURGUSAL römorkör operasyon sorumlusu', en: 'FICTIONAL tug operations lead' }] },
  shipModeDestinations: crossMode ? [{ key: 'engine-daily-log', href: `/app/engine-daily-log/${vessel}` }] : [] })
describe('DEV-080 / T-TUG-001 role lens and Ship Mode visibility (authored)', () => {
  it('shows the actual server role and lens labels instead of a literal unknown placeholder', () => {
    render(<TugProfileContext context={decodeTugContext(configured(false), vessel, tenant)} language="en" offline={false} />)
    expect(screen.getAllByText('FICTIONAL tug operations lead')).toHaveLength(2)
    expect(screen.getByText(/synthetic-label-v1/)).toBeVisible()
    expect(screen.queryByText('Unknown — no role assignment')).not.toBeInTheDocument()
  })
  it('shows localized UNKNOWN only when the server reports no role assignment', () => {
    const view = render(<TugProfileContext context={decodeTugContext(source(), vessel, tenant)} language="en" offline={false} />)
    expect(screen.getAllByText('Unknown — no role assignment').length).toBeGreaterThan(0)
    view.rerender(<TugProfileContext context={decodeTugContext(source(), vessel, tenant)} language="tr" offline={false} />)
    expect(screen.getAllByText('Bilinmiyor — rol ataması yok').length).toBeGreaterThan(0)
  })
  it('hides Ship Mode screens in Tug mode unless the server declares the cross-mode role', () => {
    const view = render(<TugProfileContext context={decodeTugContext(configured(false), vessel, tenant)} language="en" offline={false} />)
    const ship = screen.getByRole('region', { name: 'Ship Mode screens' })
    expect(ship).toHaveTextContent('Hidden in Tug mode. A cross-mode role is required.')
    expect(screen.queryByRole('link', { name: 'Engine daily log' })).not.toBeInTheDocument()
    view.rerender(<TugProfileContext context={decodeTugContext(configured(true), vessel, tenant)} language="en" offline={false} />)
    expect(screen.getByRole('link', { name: 'Engine daily log' })).toHaveAttribute('href', `/app/engine-daily-log/${vessel}`)
    expect(screen.queryByRole('link', { name: 'Noon report' })).not.toBeInTheDocument()
  })
  it('rejects a context that lists Ship Mode screens without cross-mode, fabricated role labels or foreign links', () => {
    expect(() => decodeTugContext({ ...configured(false), shipModeDestinations: [{ key: 'engine-daily-log', href: `/app/engine-daily-log/${vessel}` }] }, vessel, tenant)).toThrow()
    expect(() => decodeTugContext({ ...configured(true), shipModeDestinations: [{ key: 'fuel-bunker-log', href: `/app/fuel-bunker-log/${vessel}` }] }, vessel, tenant)).toThrow()
    expect(() => decodeTugContext({ ...configured(true), shipModeDestinations: [{ key: 'engine-daily-log', href: `/app/engine-daily-log/${other}` }] }, vessel, tenant)).toThrow()
    expect(() => decodeTugContext({ ...configured(false), roleReferences: null }, vessel, tenant)).toThrow()
    expect(() => decodeTugContext({ ...source(), roleLens: { ...source().roleLens, roles: [{ reference: role, tr: 'x', en: 'x' }] } }, vessel, tenant)).toThrow()
    const c = configured(false)
    expect(() => decodeTugContext({ ...c, roleLens: { ...c.roleLens, lenses: [{ reference: other, tr: 'x', en: 'x' }] } }, vessel, tenant)).toThrow()
  })
  it('shows bollard pull type and equipment from the server profile with server freshness', () => {
    const c = { ...configured(false), equipment: [{ id: other, level: 'System', equipmentType: 'FICTIONAL towing winch', revision: 2, provenance: 'UserDeclaredUnverified' }], equipmentCoverage: 'DraftDeclarationsOnly' }
    render(<TugProfileContext context={decodeTugContext(c, vessel, tenant)} language="en" offline={false} />)
    expect(screen.getByText(/45.01000000000000000000001/)).toBeVisible()
    expect(screen.getByText('FICTIONAL towing winch')).toBeVisible()
  })
})
