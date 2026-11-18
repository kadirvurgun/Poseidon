// Synthetic assertion declarations. No actual Human assignment or approval.
import { certificateActor, certificateFixture, certificateId } from './certificate-test-fixtures'
import { assertionStates, type CertificateAssertionCapabilities, type CertificateVerification } from './certificate-assertion-contract'
import { certificateSourceVersion } from './certificate-content'
export const assertionAuthority = '10000000-0000-4000-8000-000000000006'
export const assertionRole = '10000000-0000-4000-8000-000000000007'
export const assertionFile = { id: '10000000-0000-4000-8000-000000000008', revision: 2, checksum: 'C'.repeat(64) }
export function assertionCertificate() { const row = certificateFixture(); return { ...row, content: { ...row.content, document: assertionFile, metadata: { ...row.content.metadata, documentReference: assertionFile.id } }, verification: null } }
export const assertionCaps = (): CertificateAssertionCapabilities => ({ certificateId, certificateRevision: 1, certificateVersion: 19, assertionVersion: 0, canRecord: true, authorities: [{ id: assertionAuthority, roleId: assertionRole, duty: 'Auditor', states: [...assertionStates] }], blockers: ['RECORDED_ASSERTION_ONLY', 'LEGAL_TRANSITION_SOURCE_UNAVAILABLE'], sourceVersion: certificateSourceVersion })
export const assertionReceipt = (): CertificateVerification => ({ id: '10000000-0000-4000-8000-000000000009', certificateId, certificateRevision: 1, certificateVersion: 19, fileId: assertionFile.id, fileRevision: assertionFile.revision, checksum: assertionFile.checksum, revision: 1, version: 73, state: 'Verified', actorId: certificateActor, roleId: assertionRole, authorityId: assertionAuthority, authorityHash: 'A'.repeat(64), authorityBindingHash: 'B'.repeat(64), sourceVersion: certificateSourceVersion, savedAt: '2026-11-05T06:00:00Z', current: false, blockers: ['LEGAL_TRANSITION_SOURCE_UNAVAILABLE'], qualification: 'RECORDED_UNASSESSED' })
