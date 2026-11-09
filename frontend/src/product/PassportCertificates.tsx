import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseActions } from './EnterprisePatterns';
import { ComplianceBlockers } from './ComplianceBlockers';
import type { PassportCertificates as CertificatePage } from './compliance-certificate-contract';
const temporal = {
    VALID: [sourceText("en", "pairs.m_230013e88e373294"), sourceText("tr", "pairs.m_230013e88e373294")], DUE: [sourceText("en", "pairs.m_2e004254558bfec0"), sourceText("tr", "pairs.m_2e004254558bfec0")],
    SURVEY_WINDOW: [sourceText("en", "pairs.m_a50be58831abcfa5"), sourceText("tr", "pairs.m_a50be58831abcfa5")], EXPIRED: [sourceText("en", "pairs.m_7e5a988f7c5274f5"), sourceText("tr", "pairs.m_7e5a988f7c5274f5")], REVIEW_REQUIRED: [sourceText("en", "pairs.m_a565e293da34c2d9"), sourceText("tr", "pairs.m_a565e293da34c2d9")],
};
const assertionLabels = { Verified: [sourceText("en", "pairs.m_152f5a5a9afe4f09"), sourceText("tr", "pairs.m_152f5a5a9afe4f09")], 'Not Verified': [sourceText("en", "pairs.m_f899c3cfe79185d5"), sourceText("tr", "pairs.m_f899c3cfe79185d5")], Suspicious: [sourceText("en", "pairs.m_b9cf97458794f6d3"), sourceText("tr", "pairs.m_b9cf97458794f6d3")], 'Suspended / Revoked': [sourceText("en", "pairs.m_8c6e33ac26fc22b7"), sourceText("tr", "pairs.m_8c6e33ac26fc22b7")] };
export function PassportCertificates({ page, language, onNavigate }: {
    page: CertificatePage;
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en', unknown = localizedText(en, "pairs.m_634cf70095d27738");
    return <section className="passport-certificates" aria-label={localizedText(en, "pairs.m_9d901903ac465008")}><h3>{localizedText(en, "pairs.m_9d901903ac465008")}</h3>
    <p>{page.coverage === 'UNAVAILABLE' ? (localizedText(en, "pairs.m_91f9342436145af4")) : (localizedText(en, "pairs.m_7b74d9410175d7a7"))}</p>
    {page.coverage === 'AUTHORIZED_PAGE_ONLY' && <><p>{localizedText(en, "pairs.m_57d3b4433cf26951")}: <time dateTime={page.referenceDate}>{page.referenceDate}</time></p>{!page.items.length && <p>{localizedText(en, "pairs.m_71df5948ee0a49f0")}</p>}
      <div className="passport-certificate-list">{page.items.map(row => <article key={row.id} className="passport-certificate-card"><div className="passport-certificate-heading"><h4>{row.typeReference?.trim() || row.number?.trim() || (localizedText(en, "pairs.m_abc4ba67b33ababb"))}</h4><Badge tone="warning">{localizedText(en, "pairs.m_051032fd7df3965e")}</Badge></div>
        <p>{localizedTuple(temporal[row.temporalState], en, 0)}</p><dl className="onboarding-review-values"><div><dt>{localizedText(en, "pairs.m_c7de1be23dbdbf03")}</dt><dd>{row.number || unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_81fe62ee43fb95dd")}</dt><dd>{row.issuer || unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_505490256f9a2703")}</dt><dd>{row.issueDate || unknown} / {row.expiryDate || unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_f585514022c6a865")}</dt><dd>{row.document ? (localizedText(en, "pairs.m_94f8185bae7ca96f")) : (localizedText(en, "pairs.m_456380bab6445365"))}</dd></div></dl>
        {row.assertion && <section aria-label={localizedText(en, "pairs.m_5893b4e706cd6c47")}><h5>{localizedText(en, "pairs.m_5893b4e706cd6c47")}</h5><Badge tone="warning">{localizedText(en, "pairs.m_e744ae8365f26c4f")}</Badge><dl className="onboarding-review-values"><div><dt>{localizedText(en, "pairs.m_b1d023d8a892731b")}</dt><dd>{localizedTuple(assertionLabels[row.assertion.state], en, 0)}</dd></div><div><dt>{localizedText(en, "pairs.m_d7ddba8b10c848cd")}</dt><dd><time dateTime={row.assertion.recordedAt}>{row.assertion.recordedAt}</time></dd></div><div><dt>{localizedText(en, "pairs.m_41b51c46b9a5715c")}</dt><dd>{row.assertion.current ? (localizedText(en, "pairs.m_3d20d517375ac03d")) : (localizedText(en, "pairs.m_38201d419920d223"))}</dd></div></dl><p>{localizedText(en, "pairs.m_d70807fbe2e220af")}</p><ComplianceBlockers codes={row.assertion.blockers} language={language}/></section>}
        <EnterpriseActions align="start"><Button variant="outline" onClick={() => onNavigate(row.href)}>{localizedText(en, "pairs.m_b05b2a6f546a3120")}</Button></EnterpriseActions><details><summary>{localizedText(en, "pairs.m_635bf344cd63d2df")}</summary><p>{localizedText(en, "pairs.m_5cf7feeda047e695")}: {row.revision}</p><code>{row.sourceVersion}</code><p><code>{row.hash}</code></p>{row.document && <p>{row.document.id} / {row.document.revision} · <code>{row.document.checksum}</code></p>}</details>
      </article>)}</div></>}
    <ComplianceBlockers codes={page.blockers} language={language}/>
  </section>;
}
