import { useState } from 'react'
import { Anchor, ArrowRight, ChevronLeft, ChevronRight, CircleHelp, ClipboardList, ListFilter, RefreshCw, Search, TriangleAlert } from 'lucide-react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { ScopeMetric } from './DirectoryOverview'
import { TugOwningEvidence } from './TugOwningEvidence'
import type { TugBoard } from './tug-api'
import { tugLabel } from './tug-labels'
import './tug-board.css'

export type TugBoardFilter = { q: string; priority: string; offset: number }
export function tugBoardHref(vessel: string, filter: TugBoardFilter) {
  const params = new URLSearchParams({ vessel })
  if (filter.q) params.set('q', filter.q)
  if (filter.priority) params.set('priority', filter.priority)
  if (filter.offset) params.set('offset', String(filter.offset))
  return `/app/tug-job-board?${params}`
}

type Props = { board: TugBoard | null; priorities: string[]; filter: TugBoardFilter; language: 'tr' | 'en'; offline: boolean;
  onFilter: (filter: TugBoardFilter) => void; onNavigate: (href: string) => void; onRefresh: () => void }

export function TugBoardView({ board, priorities, filter, language, offline, onFilter, onNavigate, onRefresh }: Props) {
  const en = language === 'en', label = (code: string) => tugLabel(code, language)
  const [query, setQuery] = useState(filter.q)
  const unknown = en ? 'Unknown' : 'Bilinmiyor'
  const format = new Intl.DateTimeFormat(en ? 'en-GB' : 'tr-TR', { timeZone: 'UTC', day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit', year: 'numeric', hourCycle: 'h23' })
  const time = (value: string | null) => value && Number.isFinite(Date.parse(value)) ? <time dateTime={value}>{format.format(new Date(value))}</time> : unknown
  const apply = (patch: Partial<TugBoardFilter>) => onFilter({ ...filter, q: query, offset: 0, ...patch })
  return <section className="tug-board-view" aria-label={en ? 'Job planning board' : 'İş planlama panosu'}>
    {board && <div className="enterprise-metrics enterprise-page-summary" aria-label={en ? 'Current job page overview' : 'Geçerli iş sayfası özeti'}>
      <ScopeMetric title={en ? 'Listed requests' : 'Listelenen talepler'} value={board.items.length} note={en ? 'This returned page' : 'Dönen bu sayfa'} icon={ClipboardList} />
      <ScopeMetric title={en ? 'Recorded blockers' : 'Kayıtlı engeller'} value={board.items.filter(row => row.readiness === 'Blocked').length} note={en ? 'Jobs with a current blocker' : 'Güncel engeli olan işler'} icon={TriangleAlert} />
      <ScopeMetric title={en ? 'Readiness review' : 'Hazırlık incelemesi'} value={board.items.filter(row => row.readiness === 'ReviewRequired').length} note={en ? 'This returned page' : 'Dönen bu sayfa'} icon={ListFilter} />
      <ScopeMetric title={en ? 'Readiness unknown' : 'Hazırlığı bilinmeyen'} value={board.items.filter(row => row.readiness === 'Unknown').length} note={en ? 'No readiness conclusion' : 'Hazırlık sonucu çıkarılmaz'} icon={CircleHelp} />
    </div>}
    <div className="tug-board-toolbar">
      <form role="search" aria-label={en ? 'Filter job requests' : 'İş taleplerini filtrele'} onSubmit={event => { event.preventDefault(); apply({}) }}>
        <label className="tug-board-search"><span>{en ? 'Search requests' : 'Taleplerde ara'}</span><div><Search aria-hidden="true" /><input type="search" maxLength={100} value={query} placeholder={en ? 'Job number or assisted vessel' : 'İş numarası veya hizmet verilecek gemi'} onChange={event => setQuery(event.target.value)} /></div></label>
        <label><span>{en ? 'Priority' : 'Öncelik'}</span><select value={filter.priority} onChange={event => apply({ priority: event.target.value })}><option value="">{en ? 'All priorities' : 'Tüm öncelikler'}</option>{priorities.map(code => <option key={code} value={code}>{label(code)}</option>)}</select></label>
        <Button type="submit" variant="secondary"><ListFilter />{en ? 'Apply filters' : 'Filtreleri uygula'}</Button>
        {(filter.q || filter.priority) && <Button variant="ghost" onClick={() => { setQuery(''); onFilter({ q: '', priority: '', offset: 0 }) }}>{en ? 'Clear filters' : 'Filtreleri temizle'}</Button>}
      </form>
      <Button variant="outline" disabled={offline} onClick={onRefresh}><RefreshCw />{en ? 'Refresh board' : 'Panoyu yenile'}</Button>
    </div>
    <div className="tug-board-list-heading"><h2>{en ? 'Current draft requests' : 'Güncel taslak talepler'}</h2><span>{en ? 'Planning view · UTC' : 'Planlama görünümü · UTC'}</span></div>
    {!board && <p role="status">{en ? 'Current board unavailable. Reconnect and refresh.' : 'Güncel pano kullanılamıyor. Bağlanıp yenileyin.'}</p>}
    {board?.items.length === 0 && <div className="tug-board-empty"><Anchor aria-hidden="true" /><h3>{en ? 'No draft requests.' : 'Taslak talep yok.'}</h3><p>{filter.q || filter.priority ? (en ? 'Try a different search or priority.' : 'Farklı arama veya öncelik deneyin.') : (en ? 'New requests will appear in this context.' : 'Yeni talepler bu bağlamda listelenir.')}</p></div>}
    <div className="tug-board-jobs">
      {board?.items.map(row => <article key={row.request.id} className="tug-board-job" aria-label={row.request.jobNumber} data-readiness={row.readiness}>
        <div className="tug-board-row-grid">
          <div className="tug-board-identity"><div><Badge>{en ? 'Draft' : 'Taslak'} · r{row.request.revision}</Badge><Badge tone={['Critical', 'Emergency'].includes(row.request.inputs.priority ?? '') ? 'danger' : row.request.inputs.priority === 'High' ? 'warning' : 'neutral'}>{row.request.inputs.priority ? label(row.request.inputs.priority) : unknown}</Badge></div>
            <h3><a href={row.detailHref} onClick={event => { event.preventDefault(); onNavigate(row.detailHref) }}>{row.request.jobNumber}</a></h3><p>{row.request.inputs.assistedVessel ?? unknown}</p><small>{row.request.inputs.jobType ? label(row.request.inputs.jobType) : unknown}{row.request.inputs.operationArea ? ` · ${row.request.inputs.operationArea}` : ''}</small>
          </div>
          <div className="tug-board-field"><span className="tug-board-field-label">{en ? 'Requested window · UTC' : 'Talep aralığı · UTC'}</span><strong>{time(row.request.inputs.requestedStart)}</strong><span>{time(row.request.inputs.requestedEnd)}</span><small>{label(row.windowState)}</small>
            {row.draftPlan && <details className="tug-board-window"><summary>{en ? 'Draft plan window' : 'Taslak plan aralığı'} · r{row.draftPlan.revision}</summary><p>{time(row.draftPlan.inputs.plannedStart)} / {time(row.draftPlan.inputs.plannedEnd)}</p></details>}
            <small>{label(row.plannedTimeSource)}</small>
          </div>
          <div className="tug-board-field"><span className="tug-board-field-label">{en ? 'Draft assignments' : 'Taslak atamalar'}</span><strong>{row.assignmentCoverage === 'CurrentDraftAssignmentsOnly' ? `${row.assignedTugs.length} ${en ? 'tugs' : 'römorkör'}` : unknown}</strong><small>{label(row.assignmentCoverage)}</small><span>{en ? 'Requested' : 'Talep edilen'}: {row.request.inputs.requiredTugCount ?? unknown}</span></div>
          <div className="tug-board-field"><span className="tug-board-field-label">{en ? 'Required BP' : 'Gerekli BP'}</span><strong>{row.request.inputs.requiredBollardPull === null ? unknown : `${row.request.inputs.requiredBollardPull} t`}</strong><span>{en ? 'Planning capacity' : 'Planlama kapasitesi'}: {row.availableBollardPull === null ? unknown : `${row.availableBollardPull} t`}</span></div>
          <div className="tug-board-field tug-board-states"><span className="tug-board-field-label">{en ? 'Readiness' : 'Hazırlık'}</span><Badge tone={row.readiness === 'Blocked' ? 'danger' : row.readiness === 'ReviewRequired' ? 'warning' : 'neutral'}>{label(row.readiness)}</Badge><span className="tug-board-field-label">{en ? 'Operational conflict' : 'Operasyonel çakışma'}</span><Badge tone={['HumanReviewRequired', 'BoundaryReviewRequired'].includes(row.conflict) ? 'warning' : 'neutral'}>{label(row.conflict)}</Badge></div>
        </div>
        <div className="tug-board-next"><div><ArrowRight aria-hidden="true" /><span>{en ? 'Next action' : 'Sonraki adım'}: <strong>{label(row.nextAction)}</strong></span></div><Button asChild variant="outline" size="sm"><a href={row.detailHref} onClick={event => { event.preventDefault(); onNavigate(row.detailHref) }}>{en ? 'Open job' : 'İşi aç'}<ArrowRight aria-hidden="true" /></a></Button></div>
        <details className="tug-board-why"><summary><CircleHelp aria-hidden="true" />{en ? 'Why / next action' : 'Neden / sonraki adım'}</summary><div>
          <p>{en ? 'Assigned tug references' : 'Atanan römorkör referansları'}: {row.assignedTugs.join(', ') || unknown}</p>
          <TugOwningEvidence value={row.planningEvidence ?? null} language={language} />
          <h4>{en ? 'Comparison scope' : 'Karşılaştırma kapsamı'}</h4><p>{label(row.comparisonCoverage)}</p><ul>{row.declaredComparisons.map(item => <li key={item.jobId}>{item.jobId}: {label(item.relation)}</li>)}</ul>
          <h4>{en ? 'Recorded reasons' : 'Kayıtlı nedenler'}</h4><ul>{row.blockingReasons.map((reason, i) => <li key={`${reason}/${i}`}>{label(reason)}</li>)}</ul>
        </div></details>
      </article>)}
    </div>
    {board && <nav className="product-pagination tug-board-pagination" aria-label={en ? 'Job board pagination' : 'İş panosu sayfaları'}><span>{en ? 'Page' : 'Sayfa'} {Math.floor(filter.offset / 25) + 1} · {en ? 'Counts cover this page only' : 'Sayılar yalnız bu sayfayı kapsar'}</span><div><Button variant="outline" size="sm" disabled={filter.offset === 0} onClick={() => onFilter({ ...filter, offset: Math.max(0, filter.offset - 25) })}><ChevronLeft />{en ? 'Previous' : 'Önceki'}</Button><Button variant="outline" size="sm" disabled={!board.hasMore} onClick={() => onFilter({ ...filter, offset: filter.offset + 25 })}>{en ? 'Next' : 'Sonraki'}<ChevronRight /></Button></div></nav>}
  </section>
}
