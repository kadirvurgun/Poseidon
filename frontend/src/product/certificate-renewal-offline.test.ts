import { describe, expect, it } from 'vitest'
import { emptyCertificateRenewal } from './certificate-content'
import { decodeRenewalCapture, decodeRenewalConflict, decodeRenewalOfflineContext, decodeRenewalOutcome, projectRenewalQueue, renewalOfflineOperation, renewalOfflineRoot, sameRenewalLinks, type RenewalOfflineContext } from './certificate-renewal-offline'

// S10 WP-C DEV-066 flow 1 strict consumer of /api/v1/certificate-renewal-offline. Extra members, foreign scope,
// non-draft state or a changed linked reference are contract breaches, never ignored.
const vessel = '11111111-1111-4111-8111-111111111111', renewal = '22222222-2222-4222-8222-222222222222'
const guid = (n: number) => `${String(n).repeat(8)}-0000-4000-8000-${String(n).repeat(12)}`
const context = (patch: Record<string, unknown> = {}) => ({ id: guid(3), tenantId: guid(4), actorId: guid(5), vesselId: vessel, certificateId: guid(6), renewalId: renewal,
  deviceId: guid(7), keyId: guid(8), challenge: 'A'.repeat(43) + '=', policyVersion: 'fictional-v1', policyDigest: 'B'.repeat(64), authorityDigest: 'C'.repeat(64),
  sourceVersion: 'synthetic', operation: renewalOfflineOperation, allowedParts: ['renewalType', 'schedule', 'owner', 'requirements', 'application'], baseVersion: 12, baseRevision: 2,
  baseContent: { ...emptyCertificateRenewal(), plannedLocation: 'FICTIONAL base' }, state: 'DRAFT', ownerOptions: [], serverTime: '2026-11-21T08:00:00Z',
  expiresAt: '2026-11-21T08:10:00Z', retainUntil: '2026-11-21T10:00:00Z', maxRecords: 32, maxEncryptedBytes: 262144, maxDeviceRecords: 64, maxDeviceEncryptedBytes: 4194304, ...patch })

describe('certificate renewal offline contract', () => {
  it('accepts only an exact draft context for the requested vessel and renewal', () => {
    const value = decodeRenewalOfflineContext(context(), vessel, renewal)
    expect(value.baseContent.plannedLocation).toBe('FICTIONAL base')
    expect(() => decodeRenewalOfflineContext(context({ state: 'READY_TO_APPLY' }), vessel, renewal)).toThrow()
    expect(() => decodeRenewalOfflineContext(context({ vesselId: guid(9) }), vessel, renewal)).toThrow()
    expect(() => decodeRenewalOfflineContext({ ...context(), extra: true }, vessel, renewal)).toThrow()
    expect(() => decodeRenewalOfflineContext(context({ operation: 'work-order.execution-evidence.v1' }), vessel, renewal)).toThrow()
    expect(renewalOfflineRoot(vessel, renewal, '/x')).toBe(`certificate-renewal-offline/${renewal}/x?vesselId=${vessel}`)
  })
  it('rejects a captured draft that changes online-only links', () => {
    const c = decodeRenewalOfflineContext(context(), vessel, renewal) as RenewalOfflineContext, id = guid(1)
    const body = { commandId: id, renewalId: renewal, keyId: c.keyId, deviceId: c.deviceId, policyVersion: c.policyVersion, policyDigest: c.policyDigest, sourceVersion: c.sourceVersion,
      operation: c.operation, baseVersion: 12, capturedAt: '2026-11-21T08:01:00Z', offsetMinutes: 180, content: { ...c.baseContent, plannedLocation: 'FICTIONAL offline' } }
    expect(decodeRenewalCapture(body, c, id).content?.plannedLocation).toBe('FICTIONAL offline')
    expect(() => decodeRenewalCapture({ ...body, content: { ...c.baseContent, newCertificateId: guid(9), newCertificateRevision: 1, newCertificateVersion: 1 } }, c, id)).toThrow()
    expect(() => decodeRenewalCapture({ ...body, baseVersion: 13 }, c, id)).toThrow()
    expect(sameRenewalLinks(c.baseContent, { ...c.baseContent, dueDate: '2027-01-01' })).toBe(true)
  })
  it('decodes outcomes and conflicts and keeps a conflict pending until a terminal child', () => {
    const c = decodeRenewalOfflineContext(context(), vessel, renewal), conflictId = guid(1), childId = guid(2)
    const conflict = { commandId: conflictId, result: 'MaterialConflict', renewalId: renewal, baseVersion: 12, serverVersion: 14, revision: 3, parentCommandId: null, receivedAt: '2026-11-21T08:05:00Z' }
    expect(decodeRenewalOutcome(conflict, renewal).result).toBe('MaterialConflict')
    expect(() => decodeRenewalOutcome({ ...conflict, result: 'ServerRetained' }, renewal)).toThrow()
    const detail = { commandId: conflictId, enrollmentId: c.id, renewalId: renewal, currentVersion: 14, state: 'DRAFT', base: c.baseContent, captured: c.baseContent, server: c.baseContent,
      choices: ['keepServer', 'useCaptured'], resolved: null }
    expect(decodeRenewalConflict(detail, c, conflictId).choices).toEqual(['keepServer', 'useCaptured'])
    expect(() => decodeRenewalConflict({ ...detail, choices: ['approve'] }, c, conflictId)).toThrow()
    const rows = [{ commandId: conflictId, revision: 2, body: '{}', outcome: decodeRenewalOutcome(conflict, renewal) }]
    expect(projectRenewalQueue(rows)).toMatchObject({ pending: 1, conflicts: [conflictId], committed: 0 })
    const child = { commandId: childId, revision: 2, body: JSON.stringify({ parentCommandId: conflictId }),
      outcome: decodeRenewalOutcome({ ...conflict, commandId: childId, result: 'Committed', parentCommandId: conflictId }, renewal) }
    expect(projectRenewalQueue([...rows, child])).toMatchObject({ pending: 0, conflicts: [], committed: 1 })
  })
})
