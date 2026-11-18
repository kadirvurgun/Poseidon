import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { assertionStateLabel } from './certificate-copy';
import { Badge } from '../components/ui/badge';
import type { CertificateVerification } from './certificate-assertion-contract';
export function CertificateAssertionSummary({ value, language }: {
    value: CertificateVerification;
    language: AppLocale;
}) {
    const en = language === 'en';
    return <section className="certificate-assertion-summary" aria-label={localizedText(en, "pairs.m_a2621028d083ae21")}>
    <div className="certificate-status-line"><Badge tone="neutral">{localizedText(en, "pairs.m_010b722a06c87690")}</Badge><Badge tone="neutral">{localizedText(en, "pairs.m_f930687dbdf141c8")}{assertionStateLabel(value.state, en)}</Badge></div>
    <p>{localizedText(en, "pairs.m_70808bcba3d0f369")}</p>
    <dl className="certificate-summary-grid">
      <div><dt>{localizedText(en, "pairs.m_f2a39ed5b42d23ee")}</dt><dd>{value.revision} / {value.certificateRevision}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_d7ddba8b10c848cd")}</dt><dd><time dateTime={value.savedAt}>{value.savedAt}</time></dd></div>
      <div><dt>{localizedText(en, "pairs.m_18f43d267bdd2f1b")}</dt><dd>{value.current ? (localizedText(en, "pairs.m_5b40e60497c14644")) : (localizedText(en, "pairs.m_38cacab04c612862"))}</dd></div>
    </dl>
    <details><summary>{localizedText(en, "pairs.m_7ff281411be6672a")}</summary><dl className="certificate-summary-grid">
      <div><dt>{localizedText(en, "pairs.m_184b9ac56d192ec0")}</dt><dd>{value.id} / {value.certificateId} / {value.certificateVersion}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_e4016d907a548939")}</dt><dd>{value.actorId} / {value.roleId} / {value.authorityId}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_326e578f33005a24")}</dt><dd>{value.fileId} / {value.fileRevision}<br /><code>{value.checksum}</code></dd></div>
      <div><dt>{localizedText(en, "pairs.m_ce7a5897cc8e15e8")}</dt><dd><code>{value.authorityHash}</code><br /><code>{value.authorityBindingHash}</code></dd></div>
      <div><dt>{localizedText(en, "pairs.m_7bb902c2524638fe")}</dt><dd><code>{value.sourceVersion}</code></dd></div>
    </dl><ul>{value.blockers.map(code => <li key={code}><code>{code}</code></li>)}</ul></details>
  </section>;
}
