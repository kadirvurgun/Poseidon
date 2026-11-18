import { Badge } from '../components/ui/badge'
import type { Segment } from './human-review-api'
import './regulatory-enterprise.css'

export function RegulatoryTraceLineage({ segments, language, kinds, states, reasons }: {
  segments: Segment[]; language: 'tr' | 'en'; kinds: Record<string, string>; states: Record<string, string>; reasons: Record<string, string>
}) {
  const en = language === 'en'; const unknown = en ? 'Unknown' : 'Bilinmiyor'
  return <ol className="regulatory-trace-lineage" aria-label={en ? 'Source-grounded trace' : 'Kaynağa dayalı iz'}>{segments.map((segment, index) => {
    const available = segment.availability === 'Available'
    return <li key={`${segment.kind}/${segment.id ?? index}/${segment.revision ?? ''}`}><article aria-label={kinds[segment.kind] ?? segment.kind}>
      <header><span className="regulatory-trace-index" aria-hidden="true">{index + 1}</span><h4>{kinds[segment.kind] ?? segment.kind}</h4><Badge tone="neutral">{available ? states[segment.state] ?? unknown : en ? 'Unavailable' : 'Kullanılamıyor'}</Badge></header>
      <p>{reasons[segment.why] ?? unknown}</p>
      <dl><div><dt>{en ? 'Exact record / revision' : 'Kesin kayıt / sürüm'}</dt><dd>{available ? `${segment.id ?? unknown} / ${segment.revision ?? unknown}` : en ? 'Unavailable' : 'Kullanılamıyor'}</dd></div><div><dt>{en ? 'Saved at' : 'Kayıt zamanı'}</dt><dd>{available && segment.savedAt ? <time dateTime={segment.savedAt}>{segment.savedAt}</time> : unknown}</dd></div></dl>
      {available && segment.hash && <details><summary>{en ? 'Integrity hash (not authority)' : 'Bütünlük özeti (yetki değil)'}</summary><code>{segment.hash}</code></details>}
    </article></li>
  })}</ol>
}
