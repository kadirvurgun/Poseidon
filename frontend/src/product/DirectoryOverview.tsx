import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useMemo } from 'react';
import { Anchor, Building2, Eye, ListFilter, Ship } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { ScopedDistribution } from './ScopedDistribution';
import type { DirectoryRecord } from './directory-contract';
export function ScopeMetric({ title, value, note, icon: Icon }: {
    title: string;
    value: string | number;
    note: string;
    icon: LucideIcon;
}) {
    return <Card className="enterprise-metric"><span className="enterprise-metric-label">{title}</span><span className="enterprise-metric-icon"><Icon aria-hidden="true"/></span><strong className="enterprise-metric-value">{value}</strong><span className="enterprise-metric-note">{note}</span></Card>;
}
export function DirectoryOverview({ records, fleet, language, offset, hasMore }: {
    records: DirectoryRecord[];
    fleet: boolean;
    language: AppLocale;
    offset: number;
    hasMore: boolean;
}) {
    const en = language === 'en';
    const ships = records.filter(row => 'kind' in row && row.kind === 'Ship').length;
    const tugs = records.filter(row => 'kind' in row && row.kind === 'Tug').length;
    const editable = records.filter(row => row.canEdit).length;
    const distribution = useMemo(() => [
        { name: localizedText(en, "pairs.m_f4e590c972fc1627"), value: ships },
        { name: localizedText(en, "pairs.m_8bf3e321f0d6edc1"), value: tugs }
    ], [en, ships, tugs]);
    const scope = localizedText(en, "pairs.m_f3cb3356a44532c8");
    return <section className="enterprise-page-summary" aria-label={localizedText(en, "pairs.m_99e0941a15c137c4")}>
    <div className="enterprise-metrics">
      <ScopeMetric title={fleet ? (localizedText(en, "pairs.m_13c1c329dbdcb0fc")) : (localizedText(en, "pairs.m_af1a88f8f243cfaa"))} value={records.length} note={scope} icon={fleet ? Ship : Building2}/>
      {fleet ? <><ScopeMetric title={localizedText(en, "pairs.m_f4e590c972fc1627")} value={ships} note={scope} icon={Ship}/><ScopeMetric title={localizedText(en, "pairs.m_8bf3e321f0d6edc1")} value={tugs} note={scope} icon={Anchor}/></> : <>
        <ScopeMetric title={localizedText(en, "pairs.m_184525c5f4abe995")} value={Math.floor(offset / 25) + 1} note={hasMore ? (localizedText(en, "pairs.m_ed2c5d27758e18fe")) : (localizedText(en, "pairs.m_8ee456fcec98556d"))} icon={ListFilter}/>
        <ScopeMetric title={localizedText(en, "pairs.m_276bc61166adea3c")} value={records.length - editable} note={scope} icon={Eye}/>
      </>}
      <ScopeMetric title={localizedText(en, "pairs.m_04392c58f63f6832")} value={editable} note={localizedText(en, "pairs.m_89a929ec7e547db9")} icon={ListFilter}/>
    </div>
    {fleet && records.length > 0 && <div className="enterprise-overview-grid">
      <Card><CardHeader><div><CardTitle>{localizedText(en, "pairs.m_7d2d62d6480cdcbe")}</CardTitle><CardDescription>{localizedText(en, "pairs.m_7a2472dae5f55a5f")}</CardDescription></div><Badge>{records.length} {localizedText(en, "pairs.m_74fc092e92255cbb")}</Badge></CardHeader>
        <CardContent className="enterprise-chart-content"><ScopedDistribution data={distribution} label={localizedText(en, "pairs.m_453ae8941e42257c")}/></CardContent>
        <p className="enterprise-chart-scope">{localizedText(en, "pairs.m_3eb20afe07e1d0d8")}</p>
      </Card>
      <Card><CardHeader><div><CardTitle>{localizedText(en, "pairs.m_40c236bb614f1745")}</CardTitle><CardDescription>{localizedText(en, "pairs.m_23d9e25438bb082a")}</CardDescription></div><Eye size={16} aria-hidden="true"/></CardHeader>
        <CardContent><ul className="enterprise-summary-list"><li><span>{localizedText(en, "pairs.m_f4e590c972fc1627")}</span><strong>{ships}</strong></li><li><span>{localizedText(en, "pairs.m_8bf3e321f0d6edc1")}</span><strong>{tugs}</strong></li><li><span>{localizedText(en, "pairs.m_947ed82ccb696a5f")}</span><strong>{records.length - editable}</strong></li><li><span>{localizedText(en, "pairs.m_480a882a980c64b5")}</span><Badge tone="info">{hasMore ? (localizedText(en, "pairs.m_768fffe241c2294d")) : (localizedText(en, "pairs.m_8b37ed189404ccfa"))}</Badge></li></ul></CardContent>
      </Card>
    </div>}
  </section>;
}
