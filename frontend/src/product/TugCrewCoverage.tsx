import type { TugCrewCoverage as Coverage } from './tug-crew-contract'

const copy = {
  en: { title: 'Source-qualified minimum crew roles', boundary: 'Minimum role coverage only. Fitness, work/rest, job briefing and the Master decision remain separate.',
    protected: 'Current protected crew/source access is required.', review: 'Current source evidence requires review.', current: 'Qualified minimum role counts met',
    blocked: 'Qualified minimum role counts not met', allocation: 'A person occurs in more than one required role; human allocation review is required.',
    window: 'Declared job start / end', assessed: 'Server assessment time', document: 'Issued MSMD reference / revision', valid: 'Source validity limit', unknown: 'Unknown',
    role: 'Position reference', required: 'Required', declared: 'Declared distinct people', qualified: 'Source-qualified', unresolved: 'Unresolved',
    CurrentProtectedCrewSourceAccessRequired: 'Current protected crew/source access is required.',
    DeclaredFutureOrCurrentJobWindowRequired: 'A complete current or future declared job window is required.',
    CurrentAssignmentPolicyUnavailable: 'Current assignment and position policy is unavailable.',
    SourceQualifiedMsmdRequired: 'An issued, source-qualified MSMD is required.',
    CurrentMsmdReferencesAmbiguous: 'Competing current MSMD records require source review.',
    SourceQualifiedMsmdDoesNotCoverJobWindow: 'The independently verified MSMD does not cover the full job window.',
    CrewProjectionLimitExceeded: 'The bounded crew projection is incomplete; review the owning records.',
    SourceRoleProfileInvalid: 'The issued role profile is incomplete or invalid.',
    SourceQualifiedMinimumRolesOnly: 'Source-qualified role counts cover this window; other crew gates remain open.',
    SourceQualifiedRoleCountNotMet: 'The currently qualified distinct people do not meet the issued role count.',
    CurrentRoleQualificationIncomplete: 'Missing, changed or unqualified role inputs remain unresolved.' },
  tr: { title: 'Kaynağı nitelendirilmiş asgari mürettebat rolleri', boundary: 'Yalnız asgari rol kapsamıdır. Sağlık uygunluğu, çalışma/dinlenme, iş brifingi ve kaptan kararı ayrıdır.',
    protected: 'Güncel korumalı mürettebat/kaynak erişimi gerekir.', review: 'Güncel kaynak kanıtı incelenmeli.', current: 'Nitelendirilmiş asgari rol sayıları karşılanıyor',
    blocked: 'Nitelendirilmiş asgari rol sayıları karşılanmıyor', allocation: 'Bir kişi birden fazla zorunlu rolde yer alıyor; insan tarafından görev dağılımı incelemesi gerekir.',
    window: 'Beyan edilen iş başlangıcı / bitişi', assessed: 'Sunucu değerlendirme zamanı', document: 'Düzenlenmiş MSMD referansı / revizyonu', valid: 'Kaynak geçerlilik sınırı', unknown: 'Bilinmiyor',
    role: 'Pozisyon referansı', required: 'Gerekli', declared: 'Beyan edilen farklı kişiler', qualified: 'Kaynağı nitelendirilmiş', unresolved: 'Çözümlenmemiş',
    CurrentProtectedCrewSourceAccessRequired: 'Güncel korumalı mürettebat/kaynak erişimi gerekir.',
    DeclaredFutureOrCurrentJobWindowRequired: 'Eksiksiz güncel veya gelecekteki beyan edilen iş aralığı gerekir.',
    CurrentAssignmentPolicyUnavailable: 'Güncel atama ve pozisyon politikası kullanılamıyor.',
    SourceQualifiedMsmdRequired: 'Düzenlenmiş ve kaynağı nitelendirilmiş MSMD gerekir.',
    CurrentMsmdReferencesAmbiguous: 'Çelişen güncel MSMD kayıtları kaynak incelemesi gerektiriyor.',
    SourceQualifiedMsmdDoesNotCoverJobWindow: 'Bağımsız doğrulanmış MSMD tüm iş aralığını kapsamıyor.',
    CrewProjectionLimitExceeded: 'Sınırlandırılmış mürettebat görünümü eksik; kaynak kayıtlarını inceleyin.',
    SourceRoleProfileInvalid: 'Düzenlenmiş rol profili eksik veya geçersiz.',
    SourceQualifiedMinimumRolesOnly: 'Kaynağı nitelendirilmiş rol sayıları bu aralığı kapsıyor; diğer mürettebat koşulları açık.',
    SourceQualifiedRoleCountNotMet: 'Güncel nitelendirilmiş farklı kişiler düzenlenmiş rol sayısını karşılamıyor.',
    CurrentRoleQualificationIncomplete: 'Eksik, değişmiş veya nitelendirilmemiş rol girdileri çözümlenmemiş.' }
}

export function TugCrewCoverage({ items, language }: { items: Coverage[]; language: 'tr' | 'en' }) {
  const t = copy[language]
  return <section aria-label={t.title}><h4>{t.title}</h4><p>{t.boundary}</p>
    {items.map(c => <article key={c.tugId} aria-label={`${t.title} ${c.tugId}`}>
      <p>{c.tugId} · {c.state === 'CurrentMinimumRoleCoverage' ? t.current : c.state === 'Blocked' ? t.blocked : c.state === 'Unavailable' ? t.protected : t.review}</p>
      <p>{c.reason === 'CrossRoleAllocationReviewRequired' ? t.allocation : t[c.reason as keyof typeof t] ?? t.review}</p>
      <p>{t.window}: {c.windowStart ?? t.unknown} / {c.windowEnd ?? t.unknown}</p><p>{t.assessed}: {c.assessedAt}</p>
      {c.msmdId && <><p>{t.document}: {c.msmdId} / {c.msmdRevision}</p><p>{t.valid}: {c.validUntil}</p></>}
      {c.roles.length > 0 && <div className="product-table"><table><thead><tr><th scope="col">{t.role}</th><th scope="col">{t.required}</th><th scope="col">{t.declared}</th><th scope="col">{t.qualified}</th><th scope="col">{t.unresolved}</th></tr></thead>
        <tbody>{c.roles.map(r => <tr key={r.positionId}><th scope="row">{r.positionId}</th><td>{r.required}</td><td>{r.declared}</td><td>{r.qualified}</td><td>{r.unknown}</td></tr>)}</tbody></table></div>}
    </article>)}
  </section>
}
