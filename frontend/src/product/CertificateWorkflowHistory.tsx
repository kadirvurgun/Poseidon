import { EnterprisePagination } from './EnterprisePatterns'
import { CertificateSummary } from './CertificateSummary'
import type { CertificateWorkspaceSource } from './certificate-workspace-model'

export function CertificateWorkflowHistory({ source, language, offset, disabled, onOffset }: { source: CertificateWorkspaceSource; language: 'en' | 'tr'; offset: number; disabled: boolean; onOffset: (value: number) => void }) {
  const en = language === 'en', page = source.certificateHistory ?? source.renewalHistory
  return <section aria-label={en ? 'Immutable certificate workflow history' : 'Değişmez sertifika iş akışı geçmişi'}>
    <h2>{en ? 'Recorded revision history' : 'Kayıtlı sürüm geçmişi'}</h2>
    {!page?.items.length && <p>{en ? 'No authorized revisions returned on this page.' : 'Bu sayfada yetkili sürüm dönmedi.'}</p>}
    {source.certificateHistory?.items.map(row => <details key={row.revision}>
      <summary>{en ? 'Certificate revision' : 'Sertifika sürümü'} {row.revision} · {row.savedAt ?? '—'}</summary>
      <CertificateSummary row={row} language={language} />
    </details>)}
    {source.renewalHistory?.items.map(row => <details key={row.revision}>
      <summary>{en ? 'Renewal revision' : 'Yenileme sürümü'} {row.revision} · {row.savedAt}</summary>
      <p>{row.state === 'COMPLETED' ? (en ? 'Recorded workflow completion; qualification is separate.' : 'Kayıtlı iş akışı tamamlaması; nitelendirme ayrıdır.') : (en ? 'Recorded preparation draft; no replacement approval inferred.' : 'Kayıtlı hazırlık taslağı; değiştirme onayı varsayılmaz.')}</p>
      <dl className="certificate-summary-grid">
        <div><dt>{en ? 'Original certificate revision' : 'Özgün sertifika sürümü'}</dt><dd>{row.certificateRevision}</dd></div>
        <div><dt>{en ? 'Declared planning type' : 'Beyan edilen planlama türü'}</dt><dd>{row.content.renewalType ?? (en ? 'Not declared' : 'Beyan edilmedi')}</dd></div>
        <div><dt>{en ? 'Trigger / application date' : 'Tetikleme / başvuru tarihi'}</dt><dd>{row.content.triggerDate ?? '—'} / {row.content.applicationDate ?? '—'}</dd></div>
        <div><dt>{en ? 'Declared replacement issue / expiry' : 'Beyan edilen değiştirme düzenleme / bitiş'}</dt><dd>{row.content.newIssueDate ?? '—'} / {row.content.newExpiryDate ?? '—'}</dd></div>
      </dl>
      <details><summary>{en ? 'Exact immutable planning references' : 'Kesin değişmez planlama referansları'}</summary>
        <p>{en ? 'Original certificate / version' : 'Özgün sertifika / versiyon'}: {row.certificateId} / {row.certificateVersion}</p>
        <p>{en ? 'Owner role reference' : 'Sorumlu rol referansı'}: {row.content.ownerRoleId ?? '—'}</p>
        <p>{en ? 'Replacement candidate / revision / version' : 'Değiştirme adayı / sürüm / versiyon'}: {row.content.newCertificateId ?? '—'} / {row.content.newCertificateRevision ?? '—'} / {row.content.newCertificateVersion ?? '—'}</p>
        <code>{row.hash}</code><p><code>{row.sourceVersion}</code></p>
      </details>
    </details>)}
    <EnterprisePagination label={en ? 'Certificate workflow history pages' : 'Sertifika iş akışı geçmiş sayfaları'} previous={en ? 'Previous revisions' : 'Önceki sürümler'} next={en ? 'More revisions' : 'Diğer sürümler'} previousDisabled={!offset || disabled} nextDisabled={!page?.hasMore || disabled} onPrevious={() => onOffset(Math.max(0, offset - 25))} onNext={() => onOffset(offset + 25)} />
  </section>
}
