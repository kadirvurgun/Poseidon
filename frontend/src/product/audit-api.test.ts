import { afterEach, describe, expect, it, vi } from 'vitest'
import { auditDetail, auditPage, changes, decodeAuditRecord } from './audit-api'

// Public synthetic record metadata only: no actual actor, role appointment,
// private response, authentication state or before/after personal value.
const id = '10000000-0000-4000-8000-000000000001'
const vessel = '10000000-0000-4000-8000-000000000002'
const actor = '10000000-0000-4000-8000-000000000003'
const reviewer = '10000000-0000-4000-8000-000000000004'
// One current producer context spanning the actual source/human/owning domains.
// It is audit metadata, never a source of browser permissions or human authority.
const capabilityContext = ['directory.read', 'audit.read', 'file.read', 'applicability.evaluate', 'applicability.read',
  'control.owner.eligible', 'control.read', 'control.write', 'crew.catalog.govern', 'crew.documents.read', 'crew.documents.write',
  'crew.profile.medical.read', 'crew.profile.medical.write', 'evidence.read', 'evidence.write', 'human.decide',
  'human.review.read', 'human.review.submit', 'human.verify', 'regulatory.facts.attest', 'regulatory.qualify',
  'rules.review', 'rules.write', 'crew.assessment.read', 'human.trace.read'].sort().join(',')
const wire = () => ({ id, occurredAt: '2026-11-03T00:00:00Z', actorReference: actor, actorType: 'User', action: 'Modified',
  entityType: 'RegulatoryDraft', resourceId: vessel, vesselId: vessel, capabilityContext, roleContext: `${actor},${reviewer}`,
  changeSummary: '[{"field":"ContentJson","before":null,"after":null,"evidence":"RevisionReference"}]',
  beforeVersion: 7, afterVersion: 8, beforeRevision: 1, afterRevision: 2, reasonStatus: 'NotRequiredForCommand',
  correlationId: 'a'.repeat(32), metadataStatus: 'Recorded', revisionHref: `/api/v1/audit/${id}/revision` })
const response = (value: unknown) => ({ ok: true, status: 200, json: async () => value }) as Response
const invalid = { status: 502, code: 'invalid_audit_contract' }
const signal = () => new AbortController().signal
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('current audit producer metadata remains closed', () => {
  it('accepts a current nineteen-field record across source, evidence, human and protected-field capabilities', async () => {
    const row = wire()
    expect(decodeAuditRecord(row)).toBe(row)
    expect(decodeAuditRecord(row)).toMatchObject({ capabilityContext, actorReference: actor,
      roleContext: `${actor},${reviewer}`, metadataStatus: 'Recorded', afterVersion: 8 })
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response({ items: [row], total: 1, limit: 25, nextCursor: null }))
      .mockResolvedValueOnce(response(row)))
    expect((await auditPage(`vesselId=${vessel}&resourceId=${vessel}&entityType=RegulatoryDraft&action=Modified`, signal())).items).toEqual([row])
    expect(await auditDetail(id, signal())).toEqual(row)
  })
  it('rejects forged capability names and extra private record fields instead of accepting arbitrary server strings', () => {
    for (const forged of ['external-admin', 'rules.publish', 'human.auto-approve', 'crew.profile.medical.admin', 'Regulatory.Qualify'])
      expect(() => decodeAuditRecord({ ...wire(), capabilityContext: `${capabilityContext},${forged}` })).toThrow(expect.objectContaining(invalid))
    expect(() => decodeAuditRecord({ ...wire(), privateSnapshot: 'FICTIONAL withheld' })).toThrow(expect.objectContaining(invalid))
    expect(() => decodeAuditRecord({ ...wire(), capabilityContext: [capabilityContext] })).toThrow(expect.objectContaining(invalid))
    const missing: Record<string, unknown> = { ...wire() }; delete missing.roleContext
    expect(() => decodeAuditRecord(missing)).toThrow(expect.objectContaining(invalid))
  })
  it('keeps capability, role, correlation, version and change-summary bounds and privacy rules', () => {
    const tooManyRoles = Array.from({ length: 17 }, (_, index) => `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`).join(',')
    for (const change of [
      { capabilityContext: Array(200).fill('directory.read').join(',') }, { roleContext: tooManyRoles },
      { roleContext: `${actor},${actor}` }, { roleContext: 'FICTIONAL role label' },
      { actorReference: 'fictional@example.invalid' }, { correlationId: 'a'.repeat(33) },
      { afterVersion: 4294967296 }, { beforeRevision: 0 }, { changeSummary: 'x'.repeat(2049) },
      { changeSummary: '[{"field":"Name","before":null,"after":"FICTIONAL private name","evidence":"ValuesWithheld"}]' },
    ]) expect(() => decodeAuditRecord({ ...wire(), ...change })).toThrow(expect.objectContaining(invalid))
    expect(changes('[{"field":"Name","before":null,"after":null,"evidence":"ValuesWithheld"}]'))
      .toEqual([{ field: 'Name', before: null, after: null, evidence: 'ValuesWithheld' }])
  })
  it('preserves historical unavailable context rather than inferring a permission or approval', () => {
    const old = { ...wire(), actorReference: null, actorType: 'Unavailable', capabilityContext: null, roleContext: null,
      changeSummary: null, beforeVersion: null, afterVersion: null, beforeRevision: null, afterRevision: null,
      correlationId: null, reasonStatus: null, metadataStatus: 'HistoricalMetadataUnavailable', revisionHref: null }
    expect(decodeAuditRecord(old)).toBe(old)
  })
  it('does not relabel an out-of-scope audit result as matching the requested vessel or resource', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ items: [wire()], total: 1, limit: 25, nextCursor: null })))
    await expect(auditPage(`vesselId=${reviewer}`, signal())).rejects.toMatchObject(invalid)
    await expect(auditPage(`resourceId=${reviewer}`, signal())).rejects.toMatchObject(invalid)
  })
})
