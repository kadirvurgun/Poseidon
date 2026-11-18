import type { Profile } from './profile-contract'
import type { Locale } from '../i18n'
import './profile-source-summary.css'

// Show only the saved, authorized representation. Form edits are not source facts.
// Only TR/EN copy is authored; every non-Turkish locale falls back to English.
export function ProfileSourceSummary({ profile, language }: { profile: Profile; language: Locale }) {
  const en = language !== 'tr'
  const missing = en ? 'Not recorded' : 'Kaydedilmemiş'
  const unknown = en ? 'Unknown' : 'Bilinmiyor'
  const text = (field: Profile['content']['sourceReference']) => field.state === 'Known' && field.value != null ? field.value : field.state === 'Unknown' ? unknown : missing
  const observed = profile.content.sourceObservedAt
  return <section className="profile-source-summary" aria-label={en ? 'Saved source context' : 'Kayıtlı kaynak bağlamı'}>
    <h3>{en ? 'Saved source context' : 'Kayıtlı kaynak bağlamı'}</h3>
    <dl>
      <div><dt>{en ? 'Saved source reference' : 'Kayıtlı kaynak referansı'}</dt><dd>{text(profile.content.sourceReference)}</dd></div>
      <div><dt>{en ? 'Source observation recorded at' : 'Kayıtlı kaynak gözlem zamanı'}</dt><dd>{observed.state === 'Known' && observed.value != null ? <time dateTime={observed.value}>{observed.value}</time> : text(observed)}</dd></div>
      <div><dt>{en ? 'Draft recorded at' : 'Taslak kayıt zamanı'}</dt><dd>{profile.savedAt ? <time dateTime={profile.savedAt}>{profile.savedAt}</time> : missing}</dd></div>
      <div><dt>{en ? 'Current-use freshness' : 'Güncel kullanım tazeliği'}</dt><dd>{en ? 'Not assessed' : 'Değerlendirilmedi'}</dd></div>
    </dl>
    <p>{en ? 'Observation time and save time are separate. This draft has no applied freshness policy; neither timestamp verifies the source or authorizes operations.' : 'Gözlem zamanı ve kayıt zamanı ayrıdır. Bu taslağa uygulanmış tazelik politikası yoktur; bu zamanlar kaynağı doğrulamaz veya operasyon yetkisi vermez.'}</p>
  </section>
}
