import { useId, useState } from 'react'
import { CalendarDays, ClipboardList, Filter, FileQuestion, ShieldAlert, Users } from 'lucide-react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { ScopeMetric } from './DirectoryOverview'
import type { CrewPage, CrewRow } from './crew-api'

type Language = 'tr' | 'en'
type Presence = CrewRow['presenceState']
const states: Presence[] = ['Draft', 'Unknown', 'Stale', 'OutsidePeriod', 'PlannedDeclaration', 'OnboardDeclaration']
const stateFilters: Record<Presence, [string, string]> = {
  Draft: ['Taslak', 'Draft'], Unknown: ['Bilinmeyen bağlam', 'Unknown context'], Stale: ['Değişen politika', 'Changed policy'],
  OutsidePeriod: ['Dönem dışı', 'Outside period'], PlannedDeclaration: ['Planlanan beyan', 'Planned declaration'], OnboardDeclaration: ['Gemide beyanı', 'Onboard declaration'],
}
export function crewPresenceLabel(state: Presence, language: Language) {
  const labels = {
    Draft: ['Eksik taslak', 'Incomplete draft'], Unknown: ['Atama bağlamı bilinmiyor', 'Assignment context unknown'],
    Stale: ['Beyan politikası değişti: inceleme gerekli', 'Declaration policy changed: review required'],
    OutsidePeriod: ['Güncel dönem dışında', 'Outside current period'],
    PlannedDeclaration: ['Planlanan · doğrulanmamış beyan', 'Planned · unverified declaration'],
    OnboardDeclaration: ['Gemide · doğrulanmamış beyan', 'Onboard · unverified declaration'],
  }
  return labels[state][language === 'tr' ? 0 : 1]
}

export function CrewEnterpriseRoster({ page, language, locked, rosterHref, onNavigate, onPage }: {
  page: CrewPage; language: Language; locked: boolean; rosterHref: string;
  onNavigate: (href: string) => void; onPage: (offset: number) => void;
}) {
  const en = language === 'en'
  const filterId = useId()
  const [state, setState] = useState<Presence | ''>('')
  const [department, setDepartment] = useState('')
  const departments = [...new Set(page.items.map(item => item.department).filter((value): value is string => value !== null))].sort((a, b) => a.localeCompare(b, language))
  const rows = page.items.filter(item => (!state || item.presenceState === state) && (!department || item.department === department))
  const unknown = en ? 'Unknown' : 'Bilinmiyor'
  const withheld = en ? 'Withheld' : 'Gizli'
  const scope = en ? 'Returned page only' : 'Yalnız dönen sayfa'
  const declared = page.items.filter(item => item.presenceState === 'OnboardDeclaration' || item.presenceState === 'PlannedDeclaration').length
  const review = page.items.filter(item => item.presenceState === 'Stale' || item.presenceState === 'Unknown').length
  return <section className="crew-enterprise-roster" aria-label={en ? 'Authorized roster' : 'Yetkili mürettebat listesi'}>
    <div className="enterprise-metrics crew-enterprise-metrics">
      <ScopeMetric title={en ? 'Listed assignments' : 'Listelenen atamalar'} value={page.items.length} note={scope} icon={Users} />
      <ScopeMetric title={en ? 'Unverified declarations' : 'Doğrulanmamış beyanlar'} value={declared} note={en ? 'Planned or onboard declaration' : 'Planlanan veya gemide beyanı'} icon={ClipboardList} />
      <ScopeMetric title={en ? 'Context to review' : 'İncelenecek bağlam'} value={review} note={en ? 'Unknown context or changed policy' : 'Bilinmeyen bağlam veya değişen politika'} icon={ShieldAlert} />
      <ScopeMetric title={en ? 'Incomplete drafts' : 'Eksik taslaklar'} value={page.items.filter(item => item.presenceState === 'Draft').length} note={scope} icon={FileQuestion} />
    </div>
    <Card>
      <CardHeader><div><CardTitle>{en ? 'Assignment register' : 'Atama kayıtları'}</CardTitle><CardDescription>{en ? 'Current access and returned page. Declarations do not establish qualification or minimum manning.' : 'Güncel erişim ve dönen sayfa. Beyanlar yeterlilik veya asgari donatım kararı oluşturmaz.'}</CardDescription></div><Badge tone="info">{en ? 'Page' : 'Sayfa'} {Math.floor(page.offset / 25) + 1}</Badge></CardHeader>
      <CardContent>
        <div className="crew-enterprise-filters" role="group" aria-label={en ? 'Filter this page' : 'Bu sayfayı filtrele'}>
          <span className="crew-enterprise-filter-label"><Filter size={16} aria-hidden="true" />{en ? 'This page' : 'Bu sayfa'}</span>
          <label htmlFor={`${filterId}-state`}>{en ? 'Assignment state' : 'Atama durumu'}<select id={`${filterId}-state`} value={state} disabled={locked} onChange={event => setState(event.target.value as Presence | '')}><option value="">{en ? 'All states' : 'Tüm durumlar'}</option>{states.map(value => <option value={value} key={value}>{stateFilters[value][en ? 1 : 0]}</option>)}</select></label>
          <label htmlFor={`${filterId}-department`}>{en ? 'Department' : 'Bölüm'}<select id={`${filterId}-department`} value={department} disabled={locked} onChange={event => setDepartment(event.target.value)}><option value="">{en ? 'All departments' : 'Tüm bölümler'}</option>{departments.map(value => <option key={value}>{value}</option>)}</select></label>
          {(state || department) && <Button variant="ghost" disabled={locked} onClick={() => { setState(''); setDepartment('') }}>{en ? 'Clear filters' : 'Filtreleri temizle'}</Button>}
        </div>
        <p className="crew-enterprise-result-count" role="status">{en ? `${rows.length} of ${page.items.length} assignments on this page` : `Bu sayfadaki ${page.items.length} atamadan ${rows.length} kayıt`}</p>
        {page.items.length === 0 ? <div className="crew-enterprise-empty"><Users aria-hidden="true" /><h3>{en ? 'No assignments on this page.' : 'Bu sayfada atama yok.'}</h3><p>{en ? 'Assignments will appear here when an authorized draft is saved.' : 'Yetkili bir taslak kaydedildiğinde atamalar burada görünür.'}</p></div> : rows.length === 0 ? <div className="crew-enterprise-empty"><Filter aria-hidden="true" /><h3>{en ? 'No matches on this page' : 'Bu sayfada eşleşme yok'}</h3><p>{en ? 'Clear filters or move to another page. These filters do not search the whole vessel.' : 'Filtreleri temizleyin veya başka sayfaya geçin. Bu filtreler aracın tamamında arama yapmaz.'}</p></div> : <div className="product-table crew-enterprise-table" tabIndex={0} role="region" aria-label={en ? 'Assignment table, open a profile for full particulars' : 'Atama tablosu, tüm bilgiler için profili açın'}><table><caption className="sr-only">{en ? 'Authorized assignments on the returned page' : 'Dönen sayfadaki yetkili atamalar'}</caption><thead><tr><th scope="col">{en ? 'Person' : 'Personel'}</th><th scope="col">{en ? 'Rank' : 'Rütbe'}</th><th scope="col">{en ? 'Capacity' : 'Kapasite'}</th><th scope="col">{en ? 'Department' : 'Bölüm'}</th><th scope="col">{en ? 'Nationality' : 'Uyruk'}</th><th scope="col">{en ? 'Period' : 'Dönem'}</th><th scope="col">{en ? 'Assignment state' : 'Atama durumu'}</th></tr></thead><tbody>{rows.map(item => <tr key={item.id}>
          <td><a href={`${rosterHref}/crew-profile/${item.id}`} onClick={event => { event.preventDefault(); onNavigate(`${rosterHref}/crew-profile/${item.id}`) }}>{item.fullName ?? withheld}</a></td>
          <td>{item.rank ?? unknown}</td><td>{item.capacity ?? unknown}</td><td>{item.department ?? unknown}</td><td>{item.nationality ?? (item.nationalityState === 'Withheld' ? withheld : unknown)}</td>
          <td className="crew-enterprise-period"><CalendarDays size={14} aria-hidden="true" /><span>{item.from ?? unknown}<span className="crew-enterprise-period-end">{item.to ?? unknown}</span></span></td>
          <td><Badge tone={item.presenceState === 'Stale' || item.presenceState === 'Unknown' ? 'warning' : 'neutral'}>{crewPresenceLabel(item.presenceState, language)}</Badge></td>
        </tr>)}</tbody></table></div>}
        <nav className="product-pagination crew-enterprise-pagination" aria-label={en ? 'Roster pages' : 'Mürettebat sayfaları'}><span>{en ? '25 assignments per server page' : 'Sunucu sayfası başına 25 atama'}</span><Button variant="outline" disabled={page.offset === 0 || locked} onClick={() => onPage(Math.max(0, page.offset - 25))}>{en ? 'Previous' : 'Önceki'}</Button><Button variant="outline" disabled={!page.hasMore || locked} onClick={() => onPage(page.offset + 25)}>{en ? 'Next' : 'Sonraki'}</Button></nav>
      </CardContent>
    </Card>
  </section>
}
