import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { presentationLabel } from "../i18n/labels";
import { type Locale as AppLocale } from "../i18n";
import { useId, useState } from 'react';
import { CalendarDays, ClipboardList, Filter, FileQuestion, ShieldAlert, Users } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card';
import { ScopeMetric } from './DirectoryOverview';
import type { CrewPage, CrewRow } from './crew-api';
type Language = AppLocale;
type Presence = CrewRow['presenceState'];
const states: Presence[] = ['Draft', 'Unknown', 'Stale', 'OutsidePeriod', 'PlannedDeclaration', 'OnboardDeclaration'];
const stateFilters: Record<Presence, [
    string,
    string
]> = {
    Draft: [sourceText("tr", "pairs.m_6f7b0af9992cd6bf"), sourceText("en", "pairs.m_6f7b0af9992cd6bf")], Unknown: [sourceText("tr", "pairs.m_a71381b94e75035f"), sourceText("en", "pairs.m_a71381b94e75035f")], Stale: [sourceText("tr", "pairs.m_e7f1bcca9edad96b"), sourceText("en", "pairs.m_e7f1bcca9edad96b")],
    OutsidePeriod: [sourceText("tr", "pairs.m_7f450dec4fcb9b86"), sourceText("en", "pairs.m_7f450dec4fcb9b86")], PlannedDeclaration: [sourceText("tr", "pairs.m_2c89421188f82b00"), sourceText("en", "pairs.m_2c89421188f82b00")], OnboardDeclaration: [sourceText("tr", "pairs.m_342dfad50607d19c"), sourceText("en", "pairs.m_342dfad50607d19c")],
};
export function crewPresenceLabel(state: Presence, language: Language) {
    const labels = {
        Draft: [sourceText("tr", "pairs.m_1ebef739276de753"), sourceText("en", "pairs.m_1ebef739276de753")], Unknown: [sourceText("tr", "pairs.m_30e8d4e0d97ffc31"), sourceText("en", "pairs.m_30e8d4e0d97ffc31")],
        Stale: [sourceText("tr", "pairs.m_dd561645c38feea1"), sourceText("en", "pairs.m_dd561645c38feea1")],
        OutsidePeriod: [sourceText("tr", "pairs.m_e0929289f109a824"), sourceText("en", "pairs.m_e0929289f109a824")],
        PlannedDeclaration: [sourceText("tr", "pairs.m_ba800f3f3457ea46"), sourceText("en", "pairs.m_ba800f3f3457ea46")],
        OnboardDeclaration: [sourceText("tr", "pairs.m_44383f531ac169ca"), sourceText("en", "pairs.m_44383f531ac169ca")],
    };
    return localizedTuple(labels[state], language, 1);
}
export function CrewEnterpriseRoster({ page, language, locked, rosterHref, onNavigate, onPage }: {
    page: CrewPage;
    language: Language;
    locked: boolean;
    rosterHref: string;
    onNavigate: (href: string) => void;
    onPage: (offset: number) => void;
}) {
    const filterId = useId();
    const [state, setState] = useState<Presence | ''>('');
    const [department, setDepartment] = useState('');
    const departments = [...new Set(page.items.map(item => item.department).filter((value): value is string => value !== null))].sort((a, b) => a.localeCompare(b, language));
    const rows = page.items.filter(item => (!state || item.presenceState === state) && (!department || item.department === department));
    const unknown = localizedText(language, "pairs.m_a5a7675bd7000dbd");
    const withheld = localizedText(language, "pairs.m_1b161b9f4d3717c7");
    const scope = localizedText(language, "pairs.m_499a0d9cbb362233");
    const declared = page.items.filter(item => item.presenceState === 'OnboardDeclaration' || item.presenceState === 'PlannedDeclaration').length;
    const review = page.items.filter(item => item.presenceState === 'Stale' || item.presenceState === 'Unknown').length;
    return <section className="crew-enterprise-roster" aria-label={localizedText(language, "pairs.m_4fef42ab5d172992")}>
    <div className="enterprise-metrics crew-enterprise-metrics">
      <ScopeMetric title={localizedText(language, "pairs.m_3ba2e2a37994fbb2")} value={page.items.length} note={scope} icon={Users}/>
      <ScopeMetric title={localizedText(language, "pairs.m_d866166a42c4849e")} value={declared} note={localizedText(language, "pairs.m_bc2f44ad20dd253a")} icon={ClipboardList}/>
      <ScopeMetric title={localizedText(language, "pairs.m_2664e6b67c520ea6")} value={review} note={localizedText(language, "pairs.m_dc384e3af775ef58")} icon={ShieldAlert}/>
      <ScopeMetric title={localizedText(language, "pairs.m_4201d757ecd5f136")} value={page.items.filter(item => item.presenceState === 'Draft').length} note={scope} icon={FileQuestion}/>
    </div>
    <Card>
      <CardHeader><div><CardTitle>{localizedText(language, "pairs.m_cc07030608276d8a")}</CardTitle><CardDescription>{localizedText(language, "pairs.m_32b1d7038ddf9b02")}</CardDescription></div><Badge tone="info">{localizedText(language, "pairs.m_184525c5f4abe995")} {Math.floor(page.offset / 25) + 1}</Badge></CardHeader>
      <CardContent>
        <div className="crew-enterprise-filters" role="group" aria-label={localizedText(language, "pairs.m_39ea59efefa44acf")}>
          <span className="crew-enterprise-filter-label"><Filter size={16} aria-hidden="true"/>{localizedText(language, "pairs.m_c80a7db9d8d44ced")}</span>
          <label htmlFor={`${filterId}-state`}>{localizedText(language, "pairs.m_ce23a5bf1cb267e3")}<select id={`${filterId}-state`} value={state} disabled={locked} onChange={event => setState(event.target.value as Presence | '')}><option value="">{localizedText(language, "pairs.m_7e1de9557e6b30fd")}</option>{states.map(value => <option value={value} key={value}>{localizedTuple(stateFilters[value], language, 1)}</option>)}</select></label>
          <label htmlFor={`${filterId}-department`}>{localizedText(language, "pairs.m_3b1cbbb26d2a528c")}<select id={`${filterId}-department`} value={department} disabled={locked} onChange={event => setDepartment(event.target.value)}><option value="">{localizedText(language, "pairs.m_1d0caa31652eab45")}</option>{departments.map(value => <option key={value}>{value}</option>)}</select></label>
          {(state || department) && <Button variant="ghost" disabled={locked} onClick={() => { setState(''); setDepartment(''); }}>{localizedText(language, "pairs.m_82b8bb60a3e95b62")}</Button>}
        </div>
        <p className="crew-enterprise-result-count" role="status">{presentationLabel(language, 'crewRosterCount', { visible: new Intl.NumberFormat(language).format(rows.length), total: new Intl.NumberFormat(language).format(page.items.length) })}</p>
        {page.items.length === 0 ? <div className="crew-enterprise-empty"><Users aria-hidden="true"/><h3>{localizedText(language, "pairs.m_16af712d0e2ac44f")}</h3><p>{localizedText(language, "pairs.m_24435511a9d92dca")}</p></div> : rows.length === 0 ? <div className="crew-enterprise-empty"><Filter aria-hidden="true"/><h3>{localizedText(language, "pairs.m_f18d932d4e6ce405")}</h3><p>{localizedText(language, "pairs.m_e7e4b65319267171")}</p></div> : <div className="product-table crew-enterprise-table" tabIndex={0} role="region" aria-label={localizedText(language, "pairs.m_5fee12d42e0d584a")}><table><caption className="sr-only">{localizedText(language, "pairs.m_e84eb503ee16759a")}</caption><thead><tr><th scope="col">{localizedText(language, "pairs.m_f04dc1ac65b41999")}</th><th scope="col">{localizedText(language, "pairs.m_2a71ee9d575cc57d")}</th><th scope="col">{localizedText(language, "pairs.m_be47e945be197f43")}</th><th scope="col">{localizedText(language, "pairs.m_3b1cbbb26d2a528c")}</th><th scope="col">{localizedText(language, "pairs.m_772cc086671705f3")}</th><th scope="col">{localizedText(language, "pairs.m_20c8fd7c94a7ce75")}</th><th scope="col">{localizedText(language, "pairs.m_ce23a5bf1cb267e3")}</th></tr></thead><tbody>{rows.map(item => <tr key={item.id}>
          <td><a href={`${rosterHref}/crew-profile/${item.id}`} onClick={event => { event.preventDefault(); onNavigate(`${rosterHref}/crew-profile/${item.id}`); }}>{item.fullName ?? withheld}</a></td>
          <td>{item.rank ?? unknown}</td><td>{item.capacity ?? unknown}</td><td>{item.department ?? unknown}</td><td>{item.nationality ?? (item.nationalityState === 'Withheld' ? withheld : unknown)}</td>
          <td className="crew-enterprise-period"><CalendarDays size={14} aria-hidden="true"/><span>{item.from ?? unknown}<span className="crew-enterprise-period-end">{item.to ?? unknown}</span></span></td>
          <td><Badge tone={item.presenceState === 'Stale' || item.presenceState === 'Unknown' ? 'warning' : 'neutral'}>{crewPresenceLabel(item.presenceState, language)}</Badge></td>
        </tr>)}</tbody></table></div>}
        <nav className="product-pagination crew-enterprise-pagination" aria-label={localizedText(language, "pairs.m_daf5378382795670")}><span>{localizedText(language, "pairs.m_787c0ab9ac46bc37")}</span><Button variant="outline" disabled={page.offset === 0 || locked} onClick={() => onPage(Math.max(0, page.offset - 25))}>{localizedText(language, "pairs.m_2f7d24c724255a10")}</Button><Button variant="outline" disabled={!page.hasMore || locked} onClick={() => onPage(page.offset + 25)}>{localizedText(language, "pairs.m_93a493ffda9a1959")}</Button></nav>
      </CardContent>
    </Card>
  </section>;
}
