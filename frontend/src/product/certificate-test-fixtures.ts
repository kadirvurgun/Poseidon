// Synthetic transport declarations only; no qualified issuer, approval or live policy.
import { certificateSourceVersion, type CertificateCapabilities, type CertificateDto, type CertificatePage, type RenewalCapabilities, type RenewalDto } from './certificates-api'
import { emptyCertificateRenewal, emptyVesselCertificate } from './certificate-content'
export const certificateVessel = '10000000-0000-4000-8000-000000000001'
export const certificateId = '10000000-0000-4000-8000-000000000002'
export const certificateActor = '10000000-0000-4000-8000-000000000003'
export const certificateAssignment = '10000000-0000-4000-8000-000000000004'
export const certificateFixture = (): CertificateDto => ({
  id: certificateId, vesselId: certificateVessel, scope: 'Vessel', assignmentId: null, personId: null, revision: 1, version: 19,
  content: { ...emptyVesselCertificate(), documentName: 'Synthetic certificate declaration' }, sourceVersion: certificateSourceVersion,
  provenance: 'SyntheticUnverified', savedAt: '2026-11-05T03:00:00Z', hash: 'A'.repeat(64),
  calendar: { state: 'REVIEW_REQUIRED', referenceDate: '2026-11-05', daysToDeclaredExpiry: null, surveyState: 'NOT_DECLARED', dates: { issue: 'NotDeclared', expiry: 'NotDeclared', order: 'Incomplete' }, semantics: 'DeclaredUtcCalendarNotLegalValidity' },
  qualification: 'REVIEW_REQUIRED', state: 'DRAFT', blockers: ['SOURCE_QUALIFICATION_REQUIRED', 'HUMAN_VERIFICATION_REQUIRED'], canEdit: true,
  href: '/app/certificate-command-center/' + certificateVessel + '?id=' + certificateId, currentRevision: true, supersededBy: null,
})
export const certificateCapsFixture = (): CertificateCapabilities => ({
  vesselId: certificateVessel, canReadVessel: true, canWriteVessel: true, canReadCrew: false, crewPurpose: null, sourceVersion: certificateSourceVersion,
  scopes: ['Vessel'], temporalSemantics: 'DeclaredUtcCalendarNotLegalValidity', availabilityOptions: ['Available', 'Missing', 'Review'],
  vesselMetadataMembers: ['declaration', 'documentReference', 'renewalLeadDaysNumeric', 'sourceSystem', 'notes'], canVerify: false, canCompleteRenewal: false,
})
export const certificatePageFixture = (items: CertificateDto[] = [certificateFixture()]): CertificatePage => ({
  items, offset: 0, limit: 25, hasMore: false, scope: 'Vessel', coverage: 'AUTHORIZED_PAGE_ONLY', crewCoverage: 'NOT_REQUESTED',
  counts: { valid: 0, due90: 0, due60: 0, due30: 0, expired: 0, surveyWindow: 0, reviewRequired: items.length }, criticalGapCount: null, state: 'REVIEW_REQUIRED', referenceDate: '2026-11-05',
})
export const renewalCapsFixture = (): RenewalCapabilities => ({ vesselId: certificateVessel, canWrite: true, canComplete: false, ownerOptions: [], sourceVersion: certificateSourceVersion })
export const renewalFixture = (): RenewalDto => ({
  provenance: 'SyntheticUnverified',
  id: certificateAssignment, vesselId: certificateVessel, certificateId, certificateRevision: 1, certificateVersion: 19,
  revision: 1, version: 23, content: emptyCertificateRenewal(), sourceVersion: certificateSourceVersion, hash: 'B'.repeat(64),
  savedAt: '2026-11-05T04:00:00Z', state: 'DRAFT', qualification: 'REVIEW_REQUIRED', canEdit: true, canComplete: false, blockers: ['HUMAN_VERIFICATION_REQUIRED'], currentRevision: true,
})
