import { sourceText } from "../i18n/messages";
// Shared existing Domain CertificateMetadata. Labels/capture bounds do not
// qualify issuer identities, catalogs, evidence, eligibility or legal validity.
export type CertificateDeclaration = {
    certificateTypeReference: string | null;
    certificateNumber: string | null;
    issuer: string | null;
    issueDate: string | null;
    expiryDate: string | null;
    limitations: string | null;
};
export type CertificateMetadata = {
    declaration: CertificateDeclaration;
    issuingCountryNumeric: string | null;
    endorsementRequired: boolean | null;
    endorsementNumber: string | null;
    endorsementExpiry: string | null;
    stcwReference: string | null;
    tankerRelevance: string | null;
    vesselTypeScope: string | null;
    documentReference: string | null;
    renewalLeadDaysNumeric: string | null;
    sourceSystem: string | null;
    notes: string | null;
};
export const emptyCertificateMetadata = (): CertificateMetadata => ({ declaration: { certificateTypeReference: null, certificateNumber: null, issuer: null, issueDate: null, expiryDate: null, limitations: null }, issuingCountryNumeric: null, endorsementRequired: null, endorsementNumber: null, endorsementExpiry: null, stcwReference: null, tankerRelevance: null, vesselTypeScope: null, documentReference: null, renewalLeadDaysNumeric: null, sourceSystem: null, notes: null });
export const declarationLabels: Record<keyof CertificateDeclaration, readonly [
    string,
    string
]> = { certificateTypeReference: [sourceText("en", "pairs.m_d2a732e7f971f354"), sourceText("tr", "pairs.m_d2a732e7f971f354")], certificateNumber: [sourceText("en", "pairs.m_c7de1be23dbdbf03"), sourceText("tr", "pairs.m_c7de1be23dbdbf03")], issuer: [sourceText("en", "pairs.m_81fe62ee43fb95dd"), sourceText("tr", "pairs.m_81fe62ee43fb95dd")], issueDate: [sourceText("en", "pairs.m_e27857429af7d7c6"), sourceText("tr", "pairs.m_e27857429af7d7c6")], expiryDate: [sourceText("en", "pairs.m_ccb4b1b86cd4a34b"), sourceText("tr", "pairs.m_ccb4b1b86cd4a34b")], limitations: [sourceText("en", "pairs.m_7ef5b8c33337f15a"), sourceText("tr", "pairs.m_7ef5b8c33337f15a")] };
export const certificateMetadataLabels: Record<Exclude<keyof CertificateMetadata, 'declaration' | 'endorsementRequired' | 'documentReference'>, readonly [
    string,
    string
]> = { issuingCountryNumeric: [sourceText("en", "pairs.m_c267d3b8acb012ab"), sourceText("tr", "pairs.m_c267d3b8acb012ab")], endorsementNumber: [sourceText("en", "pairs.m_a77f72e2bb29223e"), sourceText("tr", "pairs.m_a77f72e2bb29223e")], endorsementExpiry: [sourceText("en", "pairs.m_524e376639415836"), sourceText("tr", "pairs.m_524e376639415836")], stcwReference: [sourceText("en", "pairs.m_02c9abf91de62fcb"), sourceText("tr", "pairs.m_02c9abf91de62fcb")], tankerRelevance: [sourceText("en", "pairs.m_ba4378b6a06da36a"), sourceText("tr", "pairs.m_ba4378b6a06da36a")], vesselTypeScope: [sourceText("en", "pairs.m_42612d539e33c1c2"), sourceText("tr", "pairs.m_42612d539e33c1c2")], renewalLeadDaysNumeric: [sourceText("en", "pairs.m_d39b96347120f045"), sourceText("tr", "pairs.m_d39b96347120f045")], sourceSystem: [sourceText("en", "pairs.m_060474e5699b0710"), sourceText("tr", "pairs.m_060474e5699b0710")], notes: [sourceText("en", "pairs.m_faff184e116c7d84"), sourceText("tr", "pairs.m_faff184e116c7d84")] };
