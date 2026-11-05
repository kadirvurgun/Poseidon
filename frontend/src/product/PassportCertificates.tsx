import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { ComplianceBlockers } from './ComplianceBlockers'
import type { PassportCertificates as CertificatePage } from './compliance-certificate-contract'

const temporal = {
  VALID: ['Declared dates within calendar window', 'Beyan tarihleri takvim aralığında'], DUE: ['Declared expiry approaching', 'Beyan edilen son tarih yaklaşıyor'],
  SURVEY_WINDOW: ['Declared survey window', 'Beyan edilen sörvey aralığı'], EXPIRED: ['Declared expiry passed', 'Beyan edilen son tarih geçmiş'], REVIEW_REQUIRED: ['Calendar data requires review', 'Takvim verileri incelenmeli'],
}
export function PassportCertificates({ page, language, onNavigate }: { page: CertificatePage; language: 'en' | 'tr'; onNavigate: (href: string) => void }) {
  const en = language === 'en', unknown = en ? 'Not declared' : 'Beyan edilmemiş'
  return <section className="passport-certificates" aria-label={en ? 'Vessel certificate declarations' : 'Araç sertifika beyanları'}><h3>{en ? 'Vessel certificate declarations' : 'Araç sertifika beyanları'}</h3>
    <p>{page.coverage === 'UNAVAILABLE' ? (en ? 'Certificate reading is unavailable for this session. No certificate completeness or readiness score is declared.' : 'Bu oturum için sertifika okuma kullanılamıyor. Sertifika tamlığı veya hazırlık puanı beyan edilmedi.') : (en ? 'Only authorized vessel declarations on this page are shown. Crew records, inventory completeness and legal verification are assessed separately.' : 'Yalnız bu sayfadaki yetkili araç beyanları gösterilir. Personel kayıtları, envanter tamlığı ve hukuki doğrulama ayrıca değerlendirilir.')}</p>
    {page.coverage === 'AUTHORIZED_PAGE_ONLY' && <><p>{en ? 'Calendar observation · UTC' : 'Takvim gözlemi · UTC'}: <time dateTime={page.referenceDate}>{page.referenceDate}</time></p>{!page.items.length && <p>{en ? 'No authorized certificate declarations returned on this page. This does not establish an empty obligation set.' : 'Bu sayfada yetkili sertifika beyanı dönmedi. Bu, yükümlülük kümesinin boş olduğunu belirtmez.'}</p>}
      <div className="passport-certificate-list">{page.items.map(row => <article key={row.id} className="passport-certificate-card"><div className="passport-certificate-heading"><h4>{row.typeReference?.trim() || row.number?.trim() || (en ? 'Untitled certificate declaration' : 'Başlıksız sertifika beyanı')}</h4><Badge tone="warning">{en ? 'Verification required' : 'Doğrulama gerekli'}</Badge></div>
        <p>{temporal[row.temporalState][en ? 0 : 1]}</p><dl className="onboarding-review-values"><div><dt>{en ? 'Certificate number' : 'Sertifika numarası'}</dt><dd>{row.number || unknown}</dd></div><div><dt>{en ? 'Declared issuer' : 'Beyan edilen düzenleyen'}</dt><dd>{row.issuer || unknown}</dd></div><div><dt>{en ? 'Declared issue / expiry' : 'Beyan edilen düzenleme / son tarih'}</dt><dd>{row.issueDate || unknown} / {row.expiryDate || unknown}</dd></div><div><dt>{en ? 'Document evidence' : 'Belge kanıtı'}</dt><dd>{row.document ? (en ? 'Exact document revision linked · unverified' : 'Kesin belge sürümü bağlı · doğrulanmamış') : (en ? 'No document linked' : 'Bağlı belge yok')}</dd></div></dl>
        <EnterpriseActions align="start"><Button variant="outline" onClick={() => onNavigate(row.href)}>{en ? 'Open certificate record' : 'Sertifika kaydını aç'}</Button></EnterpriseActions><details><summary>{en ? 'Certificate revision and source references' : 'Sertifika sürümü ve kaynak referansları'}</summary><p>{en ? 'Revision' : 'Sürüm'}: {row.revision}</p><code>{row.sourceVersion}</code><p><code>{row.hash}</code></p>{row.document && <p>{row.document.id} / {row.document.revision} · <code>{row.document.checksum}</code></p>}</details>
      </article>)}</div></>}
    <ComplianceBlockers codes={page.blockers} language={language} />
  </section>
}
