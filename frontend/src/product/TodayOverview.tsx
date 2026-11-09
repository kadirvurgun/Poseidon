import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useMemo } from 'react';
import { CircleHelp, ClipboardList, Layers3, TriangleAlert } from 'lucide-react';
import { ScopeMetric } from './DirectoryOverview';
import type { Today } from './today-api';
export function TodayOverview({ data, language }: {
    data: Today;
    language: AppLocale;
}) {
    const en = language === 'en';
    const counts = useMemo(() => ({ attention: data.cards.items.filter(c => c.priority === 'Attention').length,
        review: data.cards.items.filter(c => c.priority === 'Review').length, available: data.coverage.filter(c => c.status === 'Available').length }), [data]);
    return <section className="enterprise-metrics enterprise-page-summary" aria-label={localizedText(en, "pairs.m_ea0a1686863d5709")}>
    <ScopeMetric title={localizedText(en, "pairs.m_0fbc6d3d6400b38f")} value={data.cards.items.length} note={localizedText(en, "pairs.m_738a2f08cd443b4a")} icon={ClipboardList}/>
    <ScopeMetric title={localizedText(en, "pairs.m_e33d51673162a1ab")} value={counts.attention} note={localizedText(en, "pairs.m_738a2f08cd443b4a")} icon={TriangleAlert}/>
    <ScopeMetric title={localizedText(en, "pairs.m_8c4a941699614abb")} value={counts.review} note={localizedText(en, "pairs.m_738a2f08cd443b4a")} icon={CircleHelp}/>
    <ScopeMetric title={localizedText(en, "pairs.m_20a864600d8d23eb")} value={`${counts.available} / ${data.coverage.length}`} note={localizedText(en, "pairs.m_4e269ec06d16bb2d")} icon={Layers3}/>
  </section>;
}
