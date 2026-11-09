import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import type { ShipOpsOriginContext } from './ship-operation-origin';
import { shipOpsFactLabel } from './ship-operation-copy';
const labels: Record<string, [
    string,
    string
]> = { report_date: [sourceText("en", "pairs.m_212c2f33830c7012"), sourceText("tr", "pairs.m_212c2f33830c7012")], report_time_utc: [sourceText("en", "pairs.m_72d3355be87b0a55"), sourceText("tr", "pairs.m_72d3355be87b0a55")], activity: [sourceText("en", "pairs.m_21dbaa084da80a65"), sourceText("tr", "pairs.m_21dbaa084da80a65")], fuel_summary: [sourceText("en", "pairs.m_0ef03a216e6e89b4"), sourceText("tr", "pairs.m_0ef03a216e6e89b4")], rob_summary: [sourceText("en", "pairs.m_cc34c50827773b4a"), sourceText("tr", "pairs.m_cc34c50827773b4a")], log_date: [sourceText("en", "pairs.m_01d16c65e6b111d3"), sourceText("tr", "pairs.m_01d16c65e6b111d3")], watch_period: [sourceText("en", "pairs.m_12490b717f80692d"), sourceText("tr", "pairs.m_12490b717f80692d")], fuel_consumption: [sourceText("en", "pairs.m_22c67f947e1fb3f3"), sourceText("tr", "pairs.m_22c67f947e1fb3f3")], bunkering_activity: [sourceText("en", "pairs.m_daaf0c7a013c8040"), sourceText("tr", "pairs.m_daaf0c7a013c8040")] };
export function ShipOperationOriginView({ value, unavailable, language, onNavigate }: {
    value?: ShipOpsOriginContext;
    unavailable: boolean;
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    return <aside className="ship-operations-origin" aria-label={localizedText(en, "pairs.m_1a7658b935f40287")}><h2>{localizedText(en, "pairs.m_bff852d1f85890f8")}</h2>
    {!value ? <p role={unavailable ? 'alert' : 'status'}>{unavailable ? (localizedText(en, "pairs.m_68e79f5655ad2115")) : (localizedText(en, "pairs.m_95b6fb2621b97ca8"))}</p> : <>
      <p>{value.pin.kind === 'Noon' ? (localizedText(en, "pairs.m_ff485806c57476ca")) : (localizedText(en, "pairs.m_21eb6ede9f2b37f9"))} · r{value.pin.revision} · <time dateTime={value.savedAt}>{value.savedAt}</time></p>
      <p>{value.current ? (localizedText(en, "pairs.m_f0a252a572dd54e7")) : (localizedText(en, "pairs.m_e2e5729b720b8207"))}</p>
      <dl className="ship-operations-summary-grid">{value.fields.map(field => <div key={field.key}><dt>{localizedTuple(labels[field.key], en, 0) ?? field.key}</dt><dd>{field.value.state === 'Known' ? field.value.value : shipOpsFactLabel(field.value.state, en)}</dd></div>)}</dl>
      <p>{localizedText(en, "pairs.m_13e99a6c0fc0f30e")}</p>
      <Button variant="outline" onClick={() => onNavigate(value.href)}>{localizedText(en, "pairs.m_e8bbac410bc91e53")}</Button>
      <details><summary>{localizedText(en, "pairs.m_12b128c0bdee14f6")}</summary><p>{value.pin.id} / {value.pin.revision} / {value.pin.version}</p><code>{value.pin.hash}</code><p><code>{value.pin.sourceVersion}</code></p></details>
    </>}
  </aside>;
}
