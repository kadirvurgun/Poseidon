const labels: Record<string, [string, string]> = {
  PROFILE_MISSING: ['No saved vessel profile', 'Kayıtlı araç profili yok'],
  REQUIRED_DATA_MISSING: ['Required profile inputs are incomplete', 'Gerekli profil girdileri eksik'],
  REQUIRED_DATA_UNKNOWN: ['Required profile inputs need clarification', 'Gerekli profil girdileri açıklığa kavuşturulmalı'],
  VERIFICATION_NOT_ESTABLISHED: ['Source verification has not been established', 'Kaynak doğrulaması henüz kurulmadı'],
  QUALIFIED_EVALUATION_UNAVAILABLE: ['A qualified applicability evaluation is unavailable', 'Nitelendirilmiş uygulanabilirlik değerlendirmesi kullanılamıyor'],
  EVALUATION_UNAVAILABLE: ['The recorded evaluation cannot be read', 'Kayıtlı değerlendirme okunamıyor'],
  QUALIFIED_CATALOG_UNAVAILABLE: ['A current qualified rule catalog is unavailable', 'Güncel nitelendirilmiş kural kataloğu kullanılamıyor'],
  APPLICABILITY_REVIEW_REQUIRED: ['Applicability inputs or results require review', 'Uygulanabilirlik girdileri veya sonuçları incelenmeli'],
  CURRENT_APPLICABILITY_UNAVAILABLE: ['Current applicability has not been established', 'Güncel uygulanabilirlik henüz kurulmadı'],
  CONTROL_PLAN_MISSING: ['An applicable requirement has no linked control plan', 'Uygulanabilir bir gereksinimin bağlı kontrol planı yok'],
  CONTROL_LINKS_TRUNCATED: ['Only part of the linked control inventory is shown', 'Bağlı kontrol envanterinin yalnız bir bölümü gösteriliyor'],
  BOUNDED_PROJECTION: ['The returned view covers part of the available records', 'Dönen görünüm mevcut kayıtların bir bölümünü kapsıyor'],
  CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE: ['Critical requirement coverage is unavailable', 'Kritik gereksinim kapsamı kullanılamıyor'],
  CERTIFICATE_CATALOG_UNAVAILABLE: ['An authoritative vessel certificate catalog is unavailable', 'Yetkili araç sertifika kataloğu kullanılamıyor'],
  CERTIFICATE_READ_ACCESS_UNAVAILABLE: ['Current certificate read access is unavailable', 'Güncel sertifika okuma erişimi kullanılamıyor'],
  CERTIFICATE_QUALIFICATION_REQUIRED: ['Certificate declarations require source and human verification', 'Sertifika beyanları kaynak ve insan doğrulaması gerektiriyor'],
  CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED: ['Certificate inventory completeness has not been established', 'Sertifika envanterinin tamlığı henüz kurulmadı'],
  LEGAL_TRANSITION_SOURCE_UNAVAILABLE: ['An approved source for the legal transition is unavailable', 'Hukuki durum geçişi için onaylı kaynak kullanılamıyor'],
  RECORDED_ASSERTION_NOT_CURRENT: ['The recorded assertion context is historical or no longer current', 'Kayıtlı beyan bağlamı tarihsel veya artık güncel değil'],
}

// Keep unknown owning-source reasons inspectable without inventing their meaning.
// A readable summary never changes the server's actual blockers or authority.
export function ComplianceBlockers({ codes, language }: { codes: string[]; language: 'en' | 'tr' }) {
  const en = language === 'en'
  if (!codes.length) return null
  return <><ul>{codes.map((code, index) => <li key={`${code}/${index}`}>{labels[code]?.[en ? 0 : 1] ?? (en ? 'A source condition requires review' : 'Bir kaynak koşulu inceleme gerektiriyor')}</li>)}</ul>
    <details><summary>{en ? 'Source reason references' : 'Kaynak gerekçe referansları'}</summary><ul>{codes.map((code, index) => <li key={`${code}/${index}`}><code>{code}</code></li>)}</ul></details></>
}
