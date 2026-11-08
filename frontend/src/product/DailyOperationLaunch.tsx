import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import type { DailyCapabilities, DailyRecord } from './daily-operations-api'

// Launch metadata contains only an existing resource/revision. Private values,
// session identifiers and command keys never enter the destination URL.
export function DailyOperationLaunch({ record, destinations, disabled, language, onNavigate }: { record: DailyRecord; destinations: DailyCapabilities['destinations']; disabled: boolean; language: 'en' | 'tr'; onNavigate: (href: string) => void }) {
  const en = language === 'en'
  const routes = destinations.filter(item => item.key === 'fuel-bunker-log' && (record.kind === 'Noon' || record.kind === 'Engine') || item.key === 'port-activity-sof' && record.kind === 'Noon' || item.key === 'operational-downtime' && record.kind === 'Engine')
  if (!routes.length) return null
  return <section aria-label={en ? 'Use saved operational source' : 'Kayıtlı operasyon kaynağını kullan'}><h3>{en ? 'Use saved operational source' : 'Kayıtlı operasyon kaynağını kullan'}</h3>
    <p>{en ? 'Open a separate operational draft with an exact reference to this saved declaration. Quantities, units and approvals are not inferred or copied.' : 'Bu kayıtlı beyana kesin referansla ayrı bir operasyon taslağı açın. Miktar, birim veya onay türetilmez ve kopyalanmaz.'}</p>
    {disabled && <p>{en ? 'Save or explicitly discard the pending form before opening another operational draft.' : 'Başka bir operasyon taslağı açmadan önce bekleyen formu kaydedin veya açıkça bırakın.'}</p>}
    <EnterpriseActions align="start">{routes.map(item => <Button key={item.key} variant="outline" disabled={disabled} onClick={() => onNavigate(`${item.href}?${new URLSearchParams({ originKind: record.kind, originId: record.id, originRevision: String(record.revision) })}`)}>{item.key === 'fuel-bunker-log' ? (en ? 'Open fuel record from saved source' : 'Kayıtlı kaynaktan yakıt kaydı aç') : item.key === 'operational-downtime' ? (en ? 'Open downtime record from saved source' : 'Kayıtlı kaynaktan duruş kaydı aç') : (en ? 'Open port event from saved source' : 'Kayıtlı kaynaktan liman olayı aç')}</Button>)}</EnterpriseActions>
  </section>
}
