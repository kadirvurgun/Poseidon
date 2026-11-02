import { useMemo } from 'react'
import { Anchor, Building2, Eye, ListFilter, Ship } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { Badge } from '../components/ui/badge'
import { ScopedDistribution } from './ScopedDistribution'
import type { DirectoryRecord } from './directory-contract'

export function ScopeMetric({ title, value, note, icon: Icon }: { title: string; value: string | number; note: string; icon: LucideIcon }) {
  return <Card className="enterprise-metric"><span className="enterprise-metric-label">{title}</span><span className="enterprise-metric-icon"><Icon aria-hidden="true" /></span><strong className="enterprise-metric-value">{value}</strong><span className="enterprise-metric-note">{note}</span></Card>
}

export function DirectoryOverview({ records, fleet, language, offset, hasMore }: { records: DirectoryRecord[]; fleet: boolean; language: 'tr' | 'en'; offset: number; hasMore: boolean }) {
  const en = language === 'en'
  const ships = records.filter(row => 'kind' in row && row.kind === 'Ship').length
  const tugs = records.filter(row => 'kind' in row && row.kind === 'Tug').length
  const editable = records.filter(row => row.canEdit).length
  const distribution = useMemo(() => [
    { name: en ? 'Ships' : 'Gemiler', value: ships, color: '#4381da' },
    { name: en ? 'Tugs' : 'Römorkörler', value: tugs, color: '#74a4c4' }
  ], [en, ships, tugs])
  const scope = en ? 'On this page' : 'Bu sayfada'
  return <section className="enterprise-page-summary" aria-label={en ? 'Current page overview' : 'Geçerli sayfa özeti'}>
    <div className="enterprise-metrics">
      <ScopeMetric title={fleet ? (en ? 'Listed vessels' : 'Listelenen araçlar') : (en ? 'Listed companies' : 'Listelenen şirketler')} value={records.length} note={scope} icon={fleet ? Ship : Building2} />
      {fleet ? <><ScopeMetric title={en ? 'Ships' : 'Gemiler'} value={ships} note={scope} icon={Ship} /><ScopeMetric title={en ? 'Tugs' : 'Römorkörler'} value={tugs} note={scope} icon={Anchor} /></> : <>
        <ScopeMetric title={en ? 'Page' : 'Sayfa'} value={Math.floor(offset / 25) + 1} note={hasMore ? (en ? 'More records available' : 'Sonraki sayfa var') : (en ? 'Last returned page' : 'Dönen son sayfa')} icon={ListFilter} />
        <ScopeMetric title={en ? 'Read-only' : 'Salt okunur'} value={records.length - editable} note={scope} icon={Eye} />
      </>}
      <ScopeMetric title={en ? 'Editable records' : 'Düzenlenebilir kayıtlar'} value={editable} note={en ? 'Current access on this page' : 'Bu sayfadaki güncel erişim'} icon={ListFilter} />
    </div>
    {fleet && records.length > 0 && <div className="enterprise-overview-grid">
      <Card><CardHeader><div><CardTitle>{en ? 'Vessel composition' : 'Araç dağılımı'}</CardTitle><CardDescription>{en ? 'Vessel types in the returned list' : 'Dönen listedeki araç türleri'}</CardDescription></div><Badge>{records.length} {en ? 'records' : 'kayıt'}</Badge></CardHeader>
        <CardContent className="enterprise-chart-content"><ScopedDistribution data={distribution} label={en ? 'Vessel types on this page' : 'Bu sayfadaki araç türleri'} /></CardContent>
        <p className="enterprise-chart-scope">{en ? 'This page only. Operational readiness is evaluated separately.' : 'Yalnız bu sayfa. Operasyonel hazırlık ayrıca değerlendirilir.'}</p>
      </Card>
      <Card><CardHeader><div><CardTitle>{en ? 'Workspace scope' : 'Çalışma kapsamı'}</CardTitle><CardDescription>{en ? 'Current list and access context' : 'Güncel liste ve erişim bağlamı'}</CardDescription></div><Eye size={16} aria-hidden="true" /></CardHeader>
        <CardContent><ul className="enterprise-summary-list"><li><span>{en ? 'Ships' : 'Gemiler'}</span><strong>{ships}</strong></li><li><span>{en ? 'Tugs' : 'Römorkörler'}</span><strong>{tugs}</strong></li><li><span>{en ? 'Read-only records' : 'Salt okunur kayıtlar'}</span><strong>{records.length - editable}</strong></li><li><span>{en ? 'Next page' : 'Sonraki sayfa'}</span><Badge tone="info">{hasMore ? (en ? 'Available' : 'Var') : (en ? 'End of list' : 'Liste sonu')}</Badge></li></ul></CardContent>
      </Card>
    </div>}
  </section>
}
