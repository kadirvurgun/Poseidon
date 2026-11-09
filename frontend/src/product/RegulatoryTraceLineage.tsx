import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Badge } from '../components/ui/badge';
import type { Segment } from './human-review-api';
import './regulatory-enterprise.css';
export function RegulatoryTraceLineage({ segments, language, kinds, states, reasons }: {
    segments: Segment[];
    language: AppLocale;
    kinds: Record<string, string>;
    states: Record<string, string>;
    reasons: Record<string, string>;
}) {
    const en = language === 'en';
    const unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd");
    return <ol className="regulatory-trace-lineage" aria-label={localizedText(en, "pairs.m_19d3dcc0bc918b99")}>{segments.map((segment, index) => {
            const available = segment.availability === 'Available';
            return <li key={`${segment.kind}/${segment.id ?? index}/${segment.revision ?? ''}`}><article aria-label={kinds[segment.kind] ?? segment.kind}>
      <header><span className="regulatory-trace-index" aria-hidden="true">{index + 1}</span><h4>{kinds[segment.kind] ?? segment.kind}</h4><Badge tone="neutral">{available ? states[segment.state] ?? unknown : localizedText(en, "pairs.m_49138f2831bf4262")}</Badge></header>
      <p>{reasons[segment.why] ?? unknown}</p>
      <dl><div><dt>{localizedText(en, "pairs.m_9191e3ccc71e5f1a")}</dt><dd>{available ? `${segment.id ?? unknown} / ${segment.revision ?? unknown}` : localizedText(en, "pairs.m_49138f2831bf4262")}</dd></div><div><dt>{localizedText(en, "pairs.m_99bf579926929ccb")}</dt><dd>{available && segment.savedAt ? <time dateTime={segment.savedAt}>{segment.savedAt}</time> : unknown}</dd></div></dl>
      {available && segment.hash && <details><summary>{localizedText(en, "pairs.m_be0dd17f19ef1dd5")}</summary><code>{segment.hash}</code></details>}
    </article></li>;
        })}</ol>;
}
