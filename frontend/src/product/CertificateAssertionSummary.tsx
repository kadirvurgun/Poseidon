import { assertionStateLabel } from './certificate-copy'
import { Badge } from '../components/ui/badge'
import type { CertificateVerification } from './certificate-assertion-contract'
export function CertificateAssertionSummary({ value, language }: { value: CertificateVerification; language: 'en' | 'tr' }) {
  const en = language === 'en'
  return <section className="certificate-assertion-summary" aria-label={en ? 'Recorded human certificate assertion' : 'Kayıtlı insan sertifika beyanı'}>
    <div className="certificate-status-line"><Badge tone="neutral">{en ? 'Recorded · unassessed' : 'Kayıtlı · değerlendirilmemiş'}</Badge><Badge tone="neutral">{en ? 'Human assertion: ' : 'İnsan beyanı: '}{assertionStateLabel(value.state, en)}</Badge></div>
    <p>{en ? 'An assigned person recorded this claim. It does not establish legal validity, accepted qualification, renewal completion or operational approval.' : 'Atanmış kişi bu beyanı kaydetti. Hukuki geçerlilik, kabul edilmiş nitelendirme, yenileme tamamlaması veya operasyon onayı oluşturmaz.'}</p>
    <dl className="certificate-summary-grid">
      <div><dt>{en ? 'Assertion / certificate revision' : 'Beyan / sertifika sürümü'}</dt><dd>{value.revision} / {value.certificateRevision}</dd></div>
      <div><dt>{en ? 'Recorded at' : 'Kayıt zamanı'}</dt><dd><time dateTime={value.savedAt}>{value.savedAt}</time></dd></div>
      <div><dt>{en ? 'Source and appointment binding' : 'Kaynak ve atama bağı'}</dt><dd>{value.current ? (en ? 'Current recorded binding; qualification still requires review' : 'Güncel kayıtlı bağ; nitelendirme incelemesi sürüyor') : (en ? 'Recorded outcome; current binding is not established by this snapshot' : 'Kayıtlı sonuç; bu anlık kayıt güncel bağı kanıtlamaz')}</dd></div>
    </dl>
    <details><summary>{en ? 'Exact human, authority and file lineage' : 'Kesin kişi, yetki ve dosya bağı'}</summary><dl className="certificate-summary-grid">
      <div><dt>{en ? 'Assertion / certificate / version' : 'Beyan / sertifika / versiyon'}</dt><dd>{value.id} / {value.certificateId} / {value.certificateVersion}</dd></div>
      <div><dt>{en ? 'Recorded actor / role / appointment' : 'Kayıtlı aktör / rol / atama'}</dt><dd>{value.actorId} / {value.roleId} / {value.authorityId}</dd></div>
      <div><dt>{en ? 'Evidence file / revision' : 'Kanıt dosyası / sürüm'}</dt><dd>{value.fileId} / {value.fileRevision}<br /><code>{value.checksum}</code></dd></div>
      <div><dt>{en ? 'Captured authority / assignment binding' : 'Yakalanan yetki / atama bağı'}</dt><dd><code>{value.authorityHash}</code><br /><code>{value.authorityBindingHash}</code></dd></div>
      <div><dt>{en ? 'Controlled source' : 'Kontrollü kaynak'}</dt><dd><code>{value.sourceVersion}</code></dd></div>
    </dl><ul>{value.blockers.map(code => <li key={code}><code>{code}</code></li>)}</ul></details>
  </section>
}
