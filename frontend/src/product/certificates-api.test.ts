import { describe, expect, it } from 'vitest'
import { decodeCertificate, decodeCertificateCapabilities, decodeCertificateContent, decodeCertificatePage, decodeRenewal } from './certificates-api'
import { certificateAssignment, certificateCapsFixture, certificateFixture, certificateId, certificatePageFixture, certificateVessel, renewalFixture } from './certificate-test-fixtures'
import { certificateLocationPath, readCertificateLocation } from './certificate-location'
describe('T-S6-001/002 strict scoped declaration contracts (authored; not executed)', () => {
  it('keeps declared calendar validity separate from qualification and rejects manufactured approval', () => {
    const row = certificateFixture()
    const dated = { ...row, content: { ...row.content, metadata: { ...row.content.metadata, declaration: { ...row.content.metadata.declaration, issueDate: '2026-01-01', expiryDate: '2027-11-05' } } }, calendar: { ...row.calendar, state: 'VALID', daysToDeclaredExpiry: 365, dates: { issue: 'BeforeReferenceDate', expiry: 'AfterReferenceDate', order: 'IssueBeforeExpiry' } } }
    expect(decodeCertificate(dated, certificateVessel).qualification).toBe('REVIEW_REQUIRED')
    expect(() => decodeCertificate({ ...row, calendar: { ...row.calendar, state: 'VALID' } }, certificateVessel)).toThrow()
    for (const change of [{ qualification: 'VERIFIED' }, { state: 'READY' }, { vesselId: certificateId }, { state: 'SUPERSEDED' }, { calendar: { ...row.calendar, semantics: 'LegalValidity' } }]) expect(() => decodeCertificate({ ...row, ...change }, certificateVessel)).toThrow()
  })
  it('requires vessel document id/revision/checksum agreement and never accepts crew-only metadata in vessel content', () => {
    const content = certificateFixture().content, pin = { id: certificateAssignment, revision: 1, checksum: 'C'.repeat(64) }
    expect(() => decodeCertificateContent({ ...content, document: pin }, 'Vessel')).toThrow()
    expect(decodeCertificateContent({ ...content, document: pin, metadata: { ...content.metadata, documentReference: pin.id } }, 'Vessel').document).toEqual(pin)
    expect(() => decodeCertificateContent({ ...content, metadata: { ...content.metadata, stcwReference: 'not-a-vessel-field' } }, 'Vessel')).toThrow()
  })
  it('keeps protected crew files on the actual crew chain without an ordinary vessel file pin', () => {
    const row = certificateFixture(), content = { ...row.content, documentName: null, metadata: { ...row.content.metadata, documentReference: certificateId, stcwReference: 'Synthetic declaration' } }
    const crew = { ...row, scope: 'Crew', assignmentId: certificateAssignment, personId: null, content, savedAt: null, hash: null, canEdit: false, href: '/app/crew-roster/' + certificateVessel + '/crew-profile/' + certificateAssignment }
    expect(decodeCertificate(crew, certificateVessel).content.document).toBeNull()
    expect(() => decodeCertificate({ ...crew, href: '/app/people/' + certificateId }, certificateVessel)).toThrow()
  })
  it('admits only page-limited counts and actual scoped capabilities', () => {
    const page = certificatePageFixture()
    expect(decodeCertificatePage(page, certificateVessel, 'Vessel', 0).criticalGapCount).toBeNull()
    expect(() => decodeCertificatePage({ ...page, criticalGapCount: 0 }, certificateVessel, 'Vessel', 0)).toThrow()
    expect(() => decodeCertificatePage({ ...page, coverage: 'FULL_FLEET_READY' }, certificateVessel, 'Vessel', 0)).toThrow()
    expect(() => decodeCertificateCapabilities({ ...certificateCapsFixture(), vesselId: certificateId }, certificateVessel)).toThrow()
  })
  it('does not turn a draft replacement link into supersession and preserves immutable receipt currentness', () => {
    const source = certificateFixture(), renewal = renewalFixture()
    const linked = { ...renewal, content: { ...renewal.content, newCertificateId: certificateId, newCertificateRevision: 2, newCertificateVersion: 25 } }
    expect(decodeRenewal(linked, certificateVessel).state).toBe('DRAFT')
    expect(source.state).toBe('DRAFT'); expect(source.supersededBy).toBeNull()
    const receipt = decodeCertificate({ ...source, currentRevision: false, canEdit: false }, certificateVessel)
    expect(receipt.currentRevision).toBe(false); expect(receipt.canEdit).toBe(false)
  })
  it('roundtrips strict scoped routes without accepting person/purpose or duplicate identifiers', () => {
    const path = '/app/renewal-workflow/' + certificateVessel + '?certificateId=' + certificateId
    expect(certificateLocationPath(readCertificateLocation(path))).toBe(path)
    for (const query of ['id=' + certificateId + '&id=' + certificateId, 'personId=' + certificateId, 'purpose=admin', 'certificateId=' + certificateId + '&id=' + certificateId]) expect(readCertificateLocation('/app/certificate-command-center/' + certificateVessel + '?' + query).invalid).toBe(true)
  })
})
