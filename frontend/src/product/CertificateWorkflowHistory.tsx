import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { EnterprisePagination } from './EnterprisePatterns';
import { CertificateSummary } from './CertificateSummary';
import type { CertificateWorkspaceSource } from './certificate-workspace-model';
export function CertificateWorkflowHistory({ source, language, offset, disabled, onOffset }: {
    source: CertificateWorkspaceSource;
    language: AppLocale;
    offset: number;
    disabled: boolean;
    onOffset: (value: number) => void;
}) {
    const en = language === 'en', page = source.certificateHistory ?? source.renewalHistory;
    return <section aria-label={localizedText(en, "pairs.m_74d33d751160c82e")}>
    <h2>{localizedText(en, "pairs.m_edf58271348a6c9d")}</h2>
    {!page?.items.length && <p>{localizedText(en, "pairs.m_50fbb627be069c56")}</p>}
    {source.certificateHistory?.items.map(row => <details key={row.revision}>
      <summary>{localizedText(en, "pairs.m_7612d7752aac8b93")} {row.revision} · {row.savedAt ?? '—'}</summary>
      <CertificateSummary row={row} language={language}/>
    </details>)}
    {source.renewalHistory?.items.map(row => <details key={row.revision}>
      <summary>{localizedText(en, "pairs.m_848ee04537e9381b")} {row.revision} · {row.savedAt}</summary>
      <p>{row.state === 'COMPLETED' ? (localizedText(en, "pairs.m_baaf089168abc1ba")) : (localizedText(en, "pairs.m_f7cffbc5939f0938"))}</p>
      <dl className="certificate-summary-grid">
        <div><dt>{localizedText(en, "pairs.m_c0bf7fe707eb558b")}</dt><dd>{row.certificateRevision}</dd></div>
        <div><dt>{localizedText(en, "pairs.m_9c2013fa6196b1bd")}</dt><dd>{row.content.renewalType ?? (localizedText(en, "pairs.m_ee32b29512abd890"))}</dd></div>
        <div><dt>{localizedText(en, "pairs.m_3c3526ae956f6a98")}</dt><dd>{row.content.triggerDate ?? '—'} / {row.content.applicationDate ?? '—'}</dd></div>
        <div><dt>{localizedText(en, "pairs.m_3881f1dbd60ead0b")}</dt><dd>{row.content.newIssueDate ?? '—'} / {row.content.newExpiryDate ?? '—'}</dd></div>
      </dl>
      <details><summary>{localizedText(en, "pairs.m_7c4dda3009921d57")}</summary>
        <p>{localizedText(en, "pairs.m_660d69bbaaf16961")}: {row.certificateId} / {row.certificateVersion}</p>
        <p>{localizedText(en, "pairs.m_aad8c1474d4ec372")}: {row.content.ownerRoleId ?? '—'}</p>
        <p>{localizedText(en, "pairs.m_5a3c8f36f9f3c6a2")}: {row.content.newCertificateId ?? '—'} / {row.content.newCertificateRevision ?? '—'} / {row.content.newCertificateVersion ?? '—'}</p>
        <code>{row.hash}</code><p><code>{row.sourceVersion}</code></p>
      </details>
    </details>)}
    <EnterprisePagination label={localizedText(en, "pairs.m_038f548cfe700410")} previous={localizedText(en, "pairs.m_79ebddaec7d89db0")} next={localizedText(en, "pairs.m_a41fcf745e74b43d")} previousDisabled={!offset || disabled} nextDisabled={!page?.hasMore || disabled} onPrevious={() => onOffset(Math.max(0, offset - 25))} onNext={() => onOffset(offset + 25)}/>
  </section>;
}
