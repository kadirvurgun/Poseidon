import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useState } from 'react';
import { Anchor, ArrowRight, ChevronLeft, ChevronRight, CircleHelp, ClipboardList, ListFilter, RefreshCw, Search, TriangleAlert } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { ScopeMetric } from './DirectoryOverview';
import { TugOwningEvidence } from './TugOwningEvidence';
import { tugBoardWindows, tugJobStates, type TugBoard, type TugBoardWindow, type TugJobState } from './tug-api';
import { localizedCopy } from '../i18n/messages';
import { TugJobStateBadge, tugLifecycleCodeLabel } from './TugJobLifecycle';
import { tugLabel } from './tug-labels';
import './tug-board.css';
export type TugBoardFilter = {
    q: string;
    priority: string;
    offset: number;
    state?: TugJobState | '';
    window?: TugBoardWindow | '';
};
const lifecycleCopy = localizedCopy('TugJobLifecycle_tsx_copy');
export function tugBoardHref(vessel: string, filter: TugBoardFilter) {
    const params = new URLSearchParams({ vessel });
    if (filter.q)
        params.set('q', filter.q);
    if (filter.priority)
        params.set('priority', filter.priority);
    if (filter.state)
        params.set('state', filter.state);
    if (filter.window)
        params.set('window', filter.window);
    if (filter.offset)
        params.set('offset', String(filter.offset));
    return `/app/tug-job-board?${params}`;
}
type Props = {
    board: TugBoard | null;
    priorities: string[];
    filter: TugBoardFilter;
    language: AppLocale;
    offline: boolean;
    onFilter: (filter: TugBoardFilter) => void;
    onNavigate: (href: string) => void;
    onRefresh: () => void;
};
export function TugBoardView({ board, priorities, filter, language, offline, onFilter, onNavigate, onRefresh }: Props) {
    const en = language === 'en', label = (code: string) => tugLabel(code, language), lt = lifecycleCopy[language];
    const filtered = !!(filter.q || filter.priority || filter.state || filter.window);
    const [query, setQuery] = useState(filter.q);
    const unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd");
    const format = new Intl.DateTimeFormat(language, { calendar: 'gregory', timeZone: 'UTC', day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit', year: 'numeric', hourCycle: 'h23' });
    const time = (value: string | null) => value && Number.isFinite(Date.parse(value)) ? <time dateTime={value}>{format.format(new Date(value))}</time> : unknown;
    const apply = (patch: Partial<TugBoardFilter>) => onFilter({ ...filter, q: query, offset: 0, ...patch });
    return <section className="tug-board-view" aria-label={localizedText(en, "pairs.m_882498b813c3ee10")}>
    {board && <div className="enterprise-metrics enterprise-page-summary" aria-label={localizedText(en, "pairs.m_38ca145408f79bbd")}>
      <ScopeMetric title={localizedText(en, "pairs.m_8572c4b07b1cb1d4")} value={board.items.length} note={localizedText(en, "pairs.m_0551e6e1e16705b6")} icon={ClipboardList}/>
      <ScopeMetric title={localizedText(en, "pairs.m_18e0f8be8f3ce1d7")} value={board.items.filter(row => row.readiness === 'Blocked').length} note={localizedText(en, "pairs.m_16dca253f1a0a2c4")} icon={TriangleAlert}/>
      <ScopeMetric title={localizedText(en, "pairs.m_211b7b9439ea1270")} value={board.items.filter(row => row.readiness === 'ReviewRequired').length} note={localizedText(en, "pairs.m_0551e6e1e16705b6")} icon={ListFilter}/>
      <ScopeMetric title={localizedText(en, "pairs.m_aa11c05f169fb31c")} value={board.items.filter(row => row.readiness === 'Unknown').length} note={localizedText(en, "pairs.m_0ce334c85d9975a8")} icon={CircleHelp}/>
    </div>}
    <div className="tug-board-toolbar">
      <form role="search" aria-label={localizedText(en, "pairs.m_baa1e03308a031cb")} onSubmit={event => { event.preventDefault(); apply({}); }}>
        <label className="tug-board-search"><span>{localizedText(en, "pairs.m_97dcdc21667d7e3d")}</span><div><Search aria-hidden="true"/><input type="search" maxLength={100} value={query} placeholder={localizedText(en, "pairs.m_98826b809918039d")} onChange={event => setQuery(event.target.value)}/></div></label>
        <label><span>{localizedText(en, "pairs.m_9077f204711545d1")}</span><select value={filter.priority} onChange={event => apply({ priority: event.target.value })}><option value="">{localizedText(en, "pairs.m_f204a4310ec4c7f7")}</option>{priorities.map(code => <option key={code} value={code}>{label(code)}</option>)}</select></label>
        <label><span>{lt.filterState}</span><select value={filter.state ?? ''} onChange={event => apply({ state: event.target.value as TugJobState | '' })}><option value="">{lt.allActive}</option>{tugJobStates.map(code => <option key={code} value={code}>{lt[code]}</option>)}</select></label>
        <label><span>{lt.filterWindow}</span><select value={filter.window ?? ''} aria-describedby="tug-board-window-note" onChange={event => apply({ window: event.target.value as TugBoardWindow | '' })}><option value="">{lt.allWindows}</option>{tugBoardWindows.map(code => <option key={code} value={code}>{lt[code]}</option>)}</select></label>
        <Button type="submit" variant="secondary"><ListFilter />{localizedText(en, "pairs.m_9380e49fa0242572")}</Button>
        {filtered && <Button variant="ghost" onClick={() => { setQuery(''); onFilter({ q: '', priority: '', state: '', window: '', offset: 0 }); }}>{localizedText(en, "pairs.m_82b8bb60a3e95b62")}</Button>}
      </form>
      <Button variant="outline" disabled={offline} onClick={onRefresh}><RefreshCw />{localizedText(en, "pairs.m_fba5714b17917643")}</Button>
    </div>
    {filter.window && <p id="tug-board-window-note" className="tug-board-window-note">{lt.windowNote}</p>}
    <div className="tug-board-list-heading"><h2>{localizedText(en, "pairs.m_bc718e50fdbddd6f")}</h2><span>{localizedText(en, "pairs.m_1c2b67981c7d2e04")}</span></div>
    {!board && <p role="status">{localizedText(en, "pairs.m_3fd6b01ccf073275")}</p>}
    {board?.items.length === 0 && <div className="tug-board-empty"><Anchor aria-hidden="true"/><h3>{localizedText(en, "pairs.m_7d58bc173a49f8f6")}</h3><p>{filtered ? (localizedText(en, "pairs.m_6e31e81818a62d3b")) : (localizedText(en, "pairs.m_f8778ae41fa1d7f6"))}</p></div>}
    <div className="tug-board-jobs">
      {board?.items.map(row => <article key={row.request.id} className="tug-board-job" aria-label={row.request.jobNumber} data-readiness={row.readiness} data-job-state={row.request.state}>
        <div className="tug-board-row-grid">
          <div className="tug-board-identity"><div><TugJobStateBadge state={row.request.state} language={language}/><Badge>r{row.request.revision}</Badge><Badge tone={['Critical', 'Emergency'].includes(row.request.inputs.priority ?? '') ? 'danger' : row.request.inputs.priority === 'High' ? 'warning' : 'neutral'}>{row.request.inputs.priority ? label(row.request.inputs.priority) : unknown}</Badge></div>
            <h3><a href={row.detailHref} title={row.request.jobNumber} onClick={event => { event.preventDefault(); onNavigate(row.detailHref); }}>{row.request.jobNumber}</a></h3><p>{row.request.inputs.assistedVessel ?? unknown}</p><small>{row.request.inputs.jobType ? label(row.request.inputs.jobType) : unknown}{row.request.inputs.operationArea ? ` · ${row.request.inputs.operationArea}` : ''}</small>
          </div>
          <div className="tug-board-field"><span className="tug-board-field-label">{localizedText(en, "pairs.m_753ca9607cedf917")}</span><strong>{time(row.request.inputs.requestedStart)}</strong><span>{time(row.request.inputs.requestedEnd)}</span><small>{label(row.windowState)}</small>
            {row.draftPlan && <details className="tug-board-window"><summary>{localizedText(en, "pairs.m_ac0a6341c0dba1aa")} · r{row.draftPlan.revision}</summary><p>{time(row.draftPlan.inputs.plannedStart)} / {time(row.draftPlan.inputs.plannedEnd)}</p></details>}
            <small>{label(row.plannedTimeSource)}</small>
          </div>
          <div className="tug-board-field"><span className="tug-board-field-label">{localizedText(en, "pairs.m_3c23b0bc10f8a986")}</span><strong>{row.assignmentCoverage === 'CurrentDraftAssignmentsOnly' ? `${row.assignedTugs.length} ${localizedText(en, "pairs.m_306d3dbdc4e4a2ea")}` : unknown}</strong><small>{label(row.assignmentCoverage)}</small><span>{localizedText(en, "pairs.m_717ebffef9f810a8")}: {row.request.inputs.requiredTugCount ?? unknown}</span></div>
          <div className="tug-board-field"><span className="tug-board-field-label">{localizedText(en, "pairs.m_89c45d84da6e344e")}</span><strong>{row.request.inputs.requiredBollardPull === null ? unknown : `${row.request.inputs.requiredBollardPull} t`}</strong><span>{localizedText(en, "pairs.m_bc8c44ebbaad523d")}: {row.availableBollardPull === null ? unknown : `${row.availableBollardPull} t`}</span></div>
          <div className="tug-board-field tug-board-states"><span className="tug-board-field-label">{localizedText(en, "pairs.m_c9bf59553a022909")}</span><Badge tone={row.readiness === 'Blocked' ? 'danger' : row.readiness === 'ReviewRequired' ? 'warning' : 'neutral'}>{label(row.readiness)}</Badge><span className="tug-board-field-label">{localizedText(en, "pairs.m_96184f8755a4727e")}</span><Badge tone={['HumanReviewRequired', 'BoundaryReviewRequired'].includes(row.conflict) ? 'warning' : 'neutral'}>{label(row.conflict)}</Badge></div>
        </div>
        <div className="tug-board-next"><div><ArrowRight data-inline-navigation-icon aria-hidden="true"/><span>{localizedText(en, "pairs.m_c18649e05ab6fbd2")}: <strong>{tugLifecycleCodeLabel(row.nextAction, language)}</strong></span></div><Button asChild variant="outline" size="sm"><a href={row.detailHref} onClick={event => { event.preventDefault(); onNavigate(row.detailHref); }}>{localizedText(en, "pairs.m_a596c03a02f8b957")}<ArrowRight data-inline-navigation-icon aria-hidden="true"/></a></Button></div>
        <details className="tug-board-why"><summary><CircleHelp aria-hidden="true"/>{localizedText(en, "pairs.m_423d1061b74e5d2b")}</summary><div>
          <p>{localizedText(en, "pairs.m_74e29f04505cc651")}: {row.assignedTugs.join(', ') || unknown}</p>
          <TugOwningEvidence value={row.planningEvidence ?? null} language={language}/>
          <h4>{localizedText(en, "pairs.m_dfabd926a864635d")}</h4><p>{label(row.comparisonCoverage)}</p><ul>{row.declaredComparisons.map(item => <li key={item.jobId}>{item.jobId}: {label(item.relation)}</li>)}</ul>
          <h4>{localizedText(en, "pairs.m_7f22360fce53f7c8")}</h4><ul>{row.blockingReasons.map((reason, i) => <li key={`${reason}/${i}`}>{label(reason)}</li>)}</ul>
        </div></details>
      </article>)}
    </div>
    {board && <nav className="product-pagination tug-board-pagination" aria-label={localizedText(en, "pairs.m_555c7490f1202dba")}><span>{localizedText(en, "pairs.m_184525c5f4abe995")} {Math.floor(filter.offset / 25) + 1} · {localizedText(en, "pairs.m_99cd0479345b71c8")}</span><div><Button variant="outline" size="sm" disabled={filter.offset === 0} onClick={() => onFilter({ ...filter, offset: Math.max(0, filter.offset - 25) })}><ChevronLeft data-inline-navigation-icon aria-hidden="true"/>{localizedText(en, "pairs.m_2f7d24c724255a10")}</Button><Button variant="outline" size="sm" disabled={!board.hasMore} onClick={() => onFilter({ ...filter, offset: filter.offset + 25 })}>{localizedText(en, "pairs.m_93a493ffda9a1959")}<ChevronRight data-inline-navigation-icon aria-hidden="true"/></Button></div></nav>}
  </section>;
}
