import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { changes, decodeAuditRecord } from './audit-api'
import { AuditWorkspace } from './AuditWorkspace'

const id = '10000000-0000-4000-8000-000000000001'
const vessel = '10000000-0000-4000-8000-000000000002'
const actor = '10000000-0000-4000-8000-000000000003'
const change = (field: string, after: string | null, evidence = 'Allowlisted') => ({ field, before: null, after, evidence })
const summary = () => [change('Name', null, 'ValuesWithheld'), change('CommandName', 'UpdateVessel'),
  change('TechnicalReasonCode', 'PREPARATION_COMMAND'), change('ScopeKind', 'Vessel')]
const wire = () => ({ id, occurredAt: '2026-11-03T06:00:00Z', actorReference: actor, actorType: 'User',
  action: 'Modified', entityType: 'Vessel', resourceId: vessel, vesselId: vessel,
  capabilityContext: 'audit.read,directory.read,directory.write', roleContext: actor,
  changeSummary: JSON.stringify(summary()), beforeVersion: 3, afterVersion: 4,
  beforeRevision: null, afterRevision: null, reasonStatus: 'RecordedControlledCode',
  correlationId: 'a'.repeat(32), metadataStatus: 'Recorded', revisionHref: null })
const invalid = { status: 502, code: 'invalid_audit_contract' }
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('S1 current audit command context', () => {
  it('retains actual closed command purpose, role and vessel/object provenance without private values', () => {
    const value = wire()
    expect(decodeAuditRecord(value)).toBe(value)
    expect(changes(value.changeSummary)).toEqual(summary())
    expect(value.changeSummary).not.toContain('private vessel name')
  })
  it.each(['CommandName', 'TechnicalReasonCode', 'ScopeKind'])('rejects incomplete current context without %s', field => {
    expect(() => decodeAuditRecord({ ...wire(), changeSummary: JSON.stringify(summary().filter(x => x.field !== field)) }))
      .toThrow(expect.objectContaining(invalid))
  })
  it.each([
    { actorReference: null }, { roleContext: null }, { capabilityContext: null }, { correlationId: null },
    { vesselId: null }, { resourceId: null }, { actorType: 'System' }, { metadataStatus: 'HistoricalMetadataUnavailable' },
  ])('rejects complete-context claims with unavailable actual attribution %j', patch => {
    expect(() => decodeAuditRecord({ ...wire(), ...patch })).toThrow(expect.objectContaining(invalid))
  })
  it('rejects an unrelated valid purpose, forged operation, unresolved scope and duplicate shadow fields', () => {
    for (const list of [
      summary().map(x => x.field === 'TechnicalReasonCode' ? change(x.field, 'FILE_TRANSPORT') : x),
      summary().map(x => x.field === 'CommandName' ? change(x.field, 'AutomaticSafetyApproval') : x),
      summary().map(x => x.field === 'ScopeKind' ? change(x.field, 'Unresolved') : x),
      [...summary(), change('CommandName', 'CreateCompany')],
      [...summary(), change('Reason', 'private human rationale', 'ValuesWithheld')],
    ]) expect(() => decodeAuditRecord({ ...wire(), changeSummary: JSON.stringify(list) })).toThrow(expect.objectContaining(invalid))
  })
  it('preserves tenant-level scope and honest old metadata without inventing a vessel or a command reason', () => {
    const tenant = { ...wire(), entityType: 'Company', vesselId: null,
      changeSummary: JSON.stringify([change('CommandName', 'UpdateCompany'), change('TechnicalReasonCode', 'PREPARATION_COMMAND'), change('ScopeKind', 'Tenant')]) }
    expect(decodeAuditRecord(tenant)).toBe(tenant)
    const historical = { ...wire(), actorReference: null, roleContext: null, capabilityContext: null, correlationId: null,
      changeSummary: null, reasonStatus: null, metadataStatus: 'HistoricalMetadataUnavailable' }
    expect(decodeAuditRecord(historical)).toBe(historical)
  })
  it('accepts an attributed system record without fabricating an operator role and rejects system relabeling', () => {
    const system = { ...wire(), actorReference: 'system:seed-import', actorType: 'System', roleContext: null,
      capabilityContext: null, reasonStatus: 'SystemOperation', metadataStatus: 'CapabilityContextUnavailable',
      changeSummary: JSON.stringify([change('ScopeKind', 'Vessel')]) }
    expect(decodeAuditRecord(system)).toBe(system)
    expect(() => decodeAuditRecord({ ...wire(), reasonStatus: 'SystemOperation' })).toThrow(expect.objectContaining(invalid))
  })
  it.each(['tr', 'en'] as const)('shows the actual controlled purpose in %s without claiming a human decision', async language => {
    const row = wire()
    vi.stubGlobal('fetch', vi.fn(async (url: string) => ({ ok: true, status: 200,
      json: async () => url.includes('?') ? { items: [row], total: 1, limit: 25, nextCursor: null } : row }) as Response))
    render(<AuditWorkspace language={language} selected={id} allowed onNavigate={vi.fn()} />)
    expect(await screen.findByRole('heading', { name: language === 'en' ? 'Record details' : 'Kayıt ayrıntıları' })).toBeVisible()
    expect(screen.getByText(language === 'en' ? 'Draft and record preparation' : 'Taslak ve kayıt düzenleme')).toBeVisible()
    expect(screen.queryByText(/does not require a decision reason/)).not.toBeInTheDocument()
  })
})
