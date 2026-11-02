import { useMemo } from 'react'
import { CircleHelp, ClipboardList, Layers3, TriangleAlert } from 'lucide-react'
import { ScopeMetric } from './DirectoryOverview'
import type { Today } from './today-api'

export function TodayOverview({ data, language }: { data: Today; language: 'tr' | 'en' }) {
  const en = language === 'en'
  const counts = useMemo(() => ({ attention: data.cards.items.filter(c => c.priority === 'Attention').length,
    review: data.cards.items.filter(c => c.priority === 'Review').length, available: data.coverage.filter(c => c.status === 'Available').length }), [data])
  return <section className="enterprise-metrics enterprise-page-summary" aria-label={en ? 'Current alert page overview' : 'Geçerli uyarı sayfası özeti'}>
    <ScopeMetric title={en ? 'Returned cards' : 'Dönen kartlar'} value={data.cards.items.length} note={en ? 'This page only' : 'Yalnız bu sayfa'} icon={ClipboardList} />
    <ScopeMetric title={en ? 'Planning attention' : 'Planlama uyarıları'} value={counts.attention} note={en ? 'This page only' : 'Yalnız bu sayfa'} icon={TriangleAlert} />
    <ScopeMetric title={en ? 'Review required' : 'İnceleme gerekenler'} value={counts.review} note={en ? 'This page only' : 'Yalnız bu sayfa'} icon={CircleHelp} />
    <ScopeMetric title={en ? 'Available sources' : 'Kullanılabilir kaynaklar'} value={`${counts.available} / ${data.coverage.length}`} note={en ? 'Availability is not verification' : 'Kullanılabilirlik doğrulama değildir'} icon={Layers3} />
  </section>
}
