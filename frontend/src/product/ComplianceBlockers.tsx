import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
const labels: Record<string, [
    string,
    string
]> = {
    PROFILE_MISSING: [sourceText("en", "pairs.m_907e6049145bf2f3"), sourceText("tr", "pairs.m_907e6049145bf2f3")],
    REQUIRED_DATA_MISSING: [sourceText("en", "pairs.m_9361d5003fb44156"), sourceText("tr", "pairs.m_9361d5003fb44156")],
    REQUIRED_DATA_UNKNOWN: [sourceText("en", "pairs.m_4e43f5dbee5f1ea8"), sourceText("tr", "pairs.m_4e43f5dbee5f1ea8")],
    VERIFICATION_NOT_ESTABLISHED: [sourceText("en", "pairs.m_ad5dbbb43e0d364c"), sourceText("tr", "pairs.m_ad5dbbb43e0d364c")],
    QUALIFIED_EVALUATION_UNAVAILABLE: [sourceText("en", "pairs.m_6bf80ab5268fb1ba"), sourceText("tr", "pairs.m_6bf80ab5268fb1ba")],
    EVALUATION_UNAVAILABLE: [sourceText("en", "pairs.m_10af39db978f7b2b"), sourceText("tr", "pairs.m_10af39db978f7b2b")],
    QUALIFIED_CATALOG_UNAVAILABLE: [sourceText("en", "pairs.m_bfb1e1abcf84ce56"), sourceText("tr", "pairs.m_bfb1e1abcf84ce56")],
    APPLICABILITY_REVIEW_REQUIRED: [sourceText("en", "pairs.m_cf0a131706026b69"), sourceText("tr", "pairs.m_cf0a131706026b69")],
    CURRENT_APPLICABILITY_UNAVAILABLE: [sourceText("en", "pairs.m_1a743038f56f2849"), sourceText("tr", "pairs.m_1a743038f56f2849")],
    CONTROL_PLAN_MISSING: [sourceText("en", "pairs.m_4aab1c69ca2de04a"), sourceText("tr", "pairs.m_4aab1c69ca2de04a")],
    CONTROL_LINKS_TRUNCATED: [sourceText("en", "pairs.m_48a3e6388086aa75"), sourceText("tr", "pairs.m_48a3e6388086aa75")],
    BOUNDED_PROJECTION: [sourceText("en", "pairs.m_9d8e1dbcdf6a2d9e"), sourceText("tr", "pairs.m_9d8e1dbcdf6a2d9e")],
    CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE: [sourceText("en", "pairs.m_064b71f9b86c5733"), sourceText("tr", "pairs.m_064b71f9b86c5733")],
    CERTIFICATE_CATALOG_UNAVAILABLE: [sourceText("en", "pairs.m_076468b56584151c"), sourceText("tr", "pairs.m_076468b56584151c")],
    CERTIFICATE_READ_ACCESS_UNAVAILABLE: [sourceText("en", "pairs.m_41e3ae4c93edda03"), sourceText("tr", "pairs.m_41e3ae4c93edda03")],
    CERTIFICATE_QUALIFICATION_REQUIRED: [sourceText("en", "pairs.m_ac08c32acba5d143"), sourceText("tr", "pairs.m_ac08c32acba5d143")],
    CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED: [sourceText("en", "pairs.m_e537cad89eee930f"), sourceText("tr", "pairs.m_e537cad89eee930f")],
    LEGAL_TRANSITION_SOURCE_UNAVAILABLE: [sourceText("en", "pairs.m_f7b600e3f8f999a5"), sourceText("tr", "pairs.m_f7b600e3f8f999a5")],
    RECORDED_ASSERTION_NOT_CURRENT: [sourceText("en", "pairs.m_0a0c5af9433b0d9a"), sourceText("tr", "pairs.m_0a0c5af9433b0d9a")],
};
// Keep unknown owning-source reasons inspectable without inventing their meaning.
// A readable summary never changes the server's actual blockers or authority.
export function ComplianceBlockers({ codes, language }: {
    codes: string[];
    language: AppLocale;
}) {
    const en = language === 'en';
    if (!codes.length)
        return null;
    return <><ul>{codes.map((code, index) => <li key={`${code}/${index}`}>{localizedTuple(labels[code], en, 0) ?? (localizedText(en, "pairs.m_358d9dc1325e7e11"))}</li>)}</ul>
    <details><summary>{localizedText(en, "pairs.m_53af72b88e7f397c")}</summary><ul>{codes.map((code, index) => <li key={`${code}/${index}`}><code>{code}</code></li>)}</ul></details></>;
}
