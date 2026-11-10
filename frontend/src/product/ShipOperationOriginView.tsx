import { Button } from '../components/ui/button'
import type { ShipOpsOriginContext } from './ship-operation-origin'
import { shipOpsFactLabel } from './ship-operation-copy'
const labels: Record<string, [string, string]> = { report_date: ['Report date', 'Rapor tarihi'], report_time_utc: ['Report time UTC', 'Rapor saati UTC'], activity: ['Recorded activity', 'Kayıtlı faaliyet'], fuel_summary: ['Fuel summary', 'Yakıt özeti'], rob_summary: ['ROB summary', 'ROB özeti'], log_date: ['Engine log date', 'Makine kayıt tarihi'], watch_period: ['Watch period', 'Vardiya dönemi'], fuel_consumption: ['Declared consumption (unit unresolved)', 'Beyan edilen tüketim (birim çözümlenmemiş)'], bunkering_activity: ['Recorded bunkering activity', 'Kayıtlı ikmal faaliyeti'] }
export function ShipOperationOriginView({ value, unavailable, language, onNavigate }: { value?: ShipOpsOriginContext; unavailable: boolean; language: 'en' | 'tr'; onNavigate: (href: string) => void }) {
  const en = language === 'en'
  return <aside className="ship-operations-origin" aria-label={en ? 'Read-only daily source context' : 'Salt okunur günlük kaynak bağlamı'}><h2>{en ? 'Linked daily declaration' : 'Bağlı günlük beyan'}</h2>
    {!value ? <p role={unavailable ? 'alert' : 'status'}>{unavailable ? (en ? 'The selected daily source or its current access is unavailable. No origin or fuel quantities were inferred.' : 'Seçili günlük kaynak veya güncel erişimi kullanılamıyor. Kaynak bağı veya yakıt miktarı varsayılmadı.') : (en ? 'Loading the exact authorized daily source…' : 'Kesin yetkili günlük kaynak yükleniyor…')}</p> : <>
      <p>{value.pin.kind === 'Noon' ? (en ? 'Noon report' : 'Öğle raporu') : (en ? 'Engine daily log' : 'Makine günlük kaydı')} · r{value.pin.revision} · <time dateTime={value.savedAt}>{value.savedAt}</time></p>
      <p>{value.current ? (en ? 'Current source revision at read time; still an unverified declaration.' : 'Okuma anında güncel kaynak sürümü; hâlâ doğrulanmamış beyan.') : (en ? 'Historical source revision retained. A newer daily record does not replace this pin automatically.' : 'Geçmiş kaynak sürümü korunuyor. Daha yeni günlük kayıt bu sabitlemenin yerine otomatik geçmez.')}</p>
      <dl className="ship-operations-summary-grid">{value.fields.map(field => <div key={field.key}><dt>{labels[field.key]?.[en ? 0 : 1] ?? field.key}</dt><dd>{field.value.state === 'Known' ? field.value.value : shipOpsFactLabel(field.value.state, en)}</dd></div>)}</dl>
      <p>{en ? 'This context is read-only. Summaries and unknown-unit consumption are never copied or converted into received, consumed or ROB mt fields.' : 'Bu bağlam salt okunur. Özetler ve birimi çözümlenmemiş tüketim; alınan, tüketilen veya ROB mt alanlarına kopyalanmaz ya da dönüştürülmez.'}</p>
      <Button variant="outline" onClick={() => onNavigate(value.href)}>{en ? 'Open owning daily record' : 'Sahip günlük kaydı aç'}</Button>
      <details><summary>{en ? 'Exact origin lineage' : 'Kesin kaynak bağı'}</summary><p>{value.pin.id} / {value.pin.revision} / {value.pin.version}</p><code>{value.pin.hash}</code><p><code>{value.pin.sourceVersion}</code></p></details>
    </>}
  </aside>
}
