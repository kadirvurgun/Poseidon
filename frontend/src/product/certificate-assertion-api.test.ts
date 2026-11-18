import { describe, expect, it } from 'vitest'
import { decodeCertificate } from './certificates-api'
import { decodeCertificateAssertionCapabilities, decodeCertificateAssertionPage, decodeCertificateVerification } from './certificate-assertion-contract'
import { freezeCertificateAssertion } from './certificate-assertion-api'
import { assertionAuthority, assertionCaps, assertionCertificate, assertionReceipt } from './certificate-assertion-test-fixtures'
import { certificateActor, certificateId, certificateVessel } from './certificate-test-fixtures'
describe('S6 recorded human assertion strict contracts — sources only', () => {
  it('preserves the human claim without promoting top qualification or legal lifecycle', () => {
    const certificate = assertionCertificate(), verification = { ...assertionReceipt(), current: true }
    const decoded = decodeCertificate({ ...certificate, verification }, certificateVessel)
    expect(decoded.verification?.state).toBe('Verified')
    expect(decoded.verification?.qualification).toBe('RECORDED_UNASSESSED')
    expect(decoded.qualification).toBe('REVIEW_REQUIRED'); expect(decoded.state).toBe('DRAFT')
    expect(() => decodeCertificate({ ...certificate, currentRevision: false, verification }, certificateVessel)).toThrow()
    expect(() => decodeCertificate({ ...certificate, verification, qualification: 'VERIFIED' }, certificateVessel)).toThrow()
  })
  it('binds exact certificate, file, actor and immutable assignment lineage and rejects invented private fields', () => {
    const value = assertionReceipt()
    expect(decodeCertificateVerification(value, certificateId).current).toBe(false)
    for (const patch of [{ certificateId: certificateActor }, { authorityBindingHash: '' }, { qualification: 'APPROVED' }, { state: 'Expiring' }, { rationale: 'private history is not a public DTO field' }]) expect(() => decodeCertificateVerification({ ...value, ...patch }, certificateId)).toThrow()
    expect(() => decodeCertificate({ ...assertionCertificate(), verification: { ...value, fileRevision: value.fileRevision + 1 } }, certificateVessel)).toThrow()
    expect(decodeCertificate({ ...assertionCertificate(), verification: null }, certificateVessel).verification).toBeNull()
  })
  it('requires actual per-record Auditor assignments and retains default unavailable instead of inferring admin authority', () => {
    const caps = assertionCaps()
    expect(decodeCertificateAssertionCapabilities({ ...caps, canRecord: false, authorities: [] }, certificateId).canRecord).toBe(false)
    expect(() => decodeCertificateAssertionCapabilities({ ...caps, authorities: [] }, certificateId)).toThrow()
    expect(() => decodeCertificateAssertionCapabilities({ ...caps, authorities: [{ ...caps.authorities[0], duty: 'Administrator' }] }, certificateId)).toThrow()
    expect(() => decodeCertificateAssertionCapabilities({ ...caps, authorities: [{ ...caps.authorities[0], states: ['Verified', 'Ready'] }] }, certificateId)).toThrow()
  })
  it('freezes explicit reason, saved base and assertion concurrency without adding private authority inputs', () => {
    const command = freezeCertificateAssertion(assertionCertificate(), assertionCaps(), certificateVessel + '/' + certificateActor, assertionAuthority, 'Verified', 'Source compared by the assigned Human\nUnassessed claim only')
    expect(JSON.parse(command.body)).toEqual({ expectedVersion: 19, certificateRevision: 1, expectedAssertionVersion: 0, authorityId: assertionAuthority, state: 'Verified', rationale: 'Source compared by the assigned Human\nUnassessed claim only' })
    expect(() => freezeCertificateAssertion(assertionCertificate(), assertionCaps(), certificateVessel + '/' + certificateActor, assertionAuthority, 'Verified', ' ')).toThrow()
    expect(() => freezeCertificateAssertion({ ...assertionCertificate(), currentRevision: false }, assertionCaps(), certificateVessel + '/' + certificateActor, assertionAuthority, 'Verified', 'reason')).toThrow()
  })
  it('shows authorized historical assertions as recorded snapshots, never current from a history receipt', () => {
    const page = { items: [assertionReceipt()], offset: 0, limit: 25, hasMore: false }
    expect(decodeCertificateAssertionPage(page, certificateId, 0).items[0].qualification).toBe('RECORDED_UNASSESSED')
    expect(() => decodeCertificateAssertionPage({ ...page, items: [{ ...assertionReceipt(), current: true }] }, certificateId, 0)).toThrow()
  })
})
