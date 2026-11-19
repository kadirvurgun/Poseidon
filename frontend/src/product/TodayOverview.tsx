import { localizedCopy, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useMemo } from 'react';
import { CircleHelp, ClipboardList, Layers3, TriangleAlert } from 'lucide-react';
import { ScopeMetric } from './DirectoryOverview';
import type { Today, TodayViewState } from './today-api';
const copy = localizedCopy("TodayWorkspace_tsx_copy");
// DEV-023: under any non-Ready composition the item counters (total, attention, review) carry a caveat
// so a visible zero is never read as all clear (61.03 §33 / 55.01: page counters, not owner metrics).
// Empty is an authorized empty result (61.03 paragraph:1061) and gets its own note, not the partial one.
export function todayCounterNote(state: TodayViewState, language: AppLocale): string | undefined {
    const t = copy[language];
    return state === 'Ready' ? undefined : state === 'Empty' ? t.emptyCounters : t.partialCounters;
}
export function TodayOverview({ data, language }: {
    data: Today;
    language: AppLocale;
}) {
    const en = language === 'en';
    const counts = useMemo(() => ({ attention: data.cards.items.filter(c => c.priority === 'Attention').length,
        review: data.cards.items.filter(c => c.priority === 'Review').length, available: data.coverage.filter(c => c.status === 'Available').length }), [data]);
    const pageOnly = localizedText(en, "pairs.m_738a2f08cd443b4a");
    const note = todayCounterNote(data.viewState, language) ?? pageOnly;
    return <section className="enterprise-metrics enterprise-page-summary" aria-label={localizedText(en, "pairs.m_ea0a1686863d5709")}>
    <ScopeMetric title={localizedText(en, "pairs.m_0fbc6d3d6400b38f")} value={data.cards.items.length} note={note} icon={ClipboardList}/>
    <ScopeMetric title={localizedText(en, "pairs.m_e33d51673162a1ab")} value={counts.attention} note={note} icon={TriangleAlert}/>
    <ScopeMetric title={localizedText(en, "pairs.m_8c4a941699614abb")} value={counts.review} note={note} icon={CircleHelp}/>
    <ScopeMetric title={localizedText(en, "pairs.m_20a864600d8d23eb")} value={`${counts.available} / ${data.coverage.length}`} note={localizedText(en, "pairs.m_4e269ec06d16bb2d")} icon={Layers3}/>
  </section>;
}
