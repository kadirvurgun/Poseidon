import { localizedText } from "../i18n/messages";
import { presentationLabel } from '../i18n/labels'
import type { CertificateAssertionState } from './certificate-assertion-contract';
import type { CertificateTemporalState } from './certificates-api';
export function certificateTemporal(state: CertificateTemporalState, en: boolean) { return ({ VALID: localizedText(en, "pairs.m_07e88d0ee775c042"), DUE: localizedText(en, "pairs.m_77a9e5c3b8b51c31"), SURVEY_WINDOW: localizedText(en, "pairs.m_6b0eead74fe78b84"), EXPIRED: localizedText(en, "pairs.m_f29c0ac9f15a1c47"), REVIEW_REQUIRED: localizedText(en, "pairs.m_4bd59dd603c00386") })[state]; }
export function certificateBlocker(code: string, en: boolean) { const labels: Record<string, string> = { CERTIFICATE_TYPE_UNRESOLVED: localizedText(en, "pairs.m_77847090517e9870"), ISSUER_NOT_DECLARED: localizedText(en, "pairs.m_33ba59f665f12e92"), CERTIFICATE_EVIDENCE_NOT_LINKED: localizedText(en, "pairs.m_ba8a91633f7ee5fc"), SOURCE_QUALIFICATION_REQUIRED: localizedText(en, "pairs.m_8ab569d3974485b9"), HUMAN_VERIFICATION_REQUIRED: localizedText(en, "pairs.m_cf66b652b6f64a9a"), RENEWAL_VERIFICATION_REQUIRED: localizedText(en, "pairs.m_44b65e07ac644d83"), SUPERSESSION_NOT_ESTABLISHED: localizedText(en, "pairs.m_2b77eac36b55d970") }; return labels[code] ?? (localizedText(en, "pairs.m_d5590a6423a72353")); }
export function assertionStateLabel(state: CertificateAssertionState, en: boolean) {
    return presentationLabel(en, ({ Verified: 'verifiedDeclaration', 'Not Verified': 'notVerifiedDeclaration', Suspicious: 'suspiciousDeclaration', 'Suspended / Revoked': 'suspendedDeclaration' } as const)[state]);
}
