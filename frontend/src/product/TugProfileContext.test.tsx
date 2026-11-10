import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TugProfileContext } from './TugProfileContext'
import { decodeTugContext, type TugContext } from './tug-api'
const vessel = '10000000-0000-4000-8000-000000000001', tenant = '10000000-0000-4000-8000-000000000002', other = '10000000-0000-4000-8000-000000000003'
const source = (): TugContext => ({ vessel: { id: vessel, code: 'FICTIONAL', name: 'Synthetic tug' }, mode: 'Tug', crossMode: false, roleReferences: null, canWrite: false, owners: [], jobTypes: ['Harbour Towage'], priorities: ['Normal'], requestSources: ['Customer'], tugType: 'Unavailable', tugTypeContext: null, tugTypeOptions: [], tugTypePolicyVersion: null, bollardPull: { value: '45.01000000000000000000001', version: 7, provenance: 'LegacyMeasurementUnverified', freshness: 'Unknown', observedAt: null, source: 'TugProfile' }, equipment: null, equipmentCoverage: 'Unavailable', readiness: 'Unknown', reason: 'VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable' })
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
