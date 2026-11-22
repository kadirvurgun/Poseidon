import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useState } from 'react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import type { WorkOrderPlanContext, WorkOrderPlanPin, WorkOrderHistory, WorkOrderPage, WorkOrderRecord } from './work-order-api';
import { workOrderCodeLabel, workOrderFactLabel, workOrderFieldInfo } from './work-order-fields';
import { workOrderBlockerLabel } from './work-order-copy';
export function WorkOrderSourceSummary({ value, language }: {
    value: WorkOrderRecord;
    language: AppLocale;
}) {
    const en = language === 'en', phase = value.fields.execution_stage;
    return <section className="work-order-source" aria-label={localizedText(en, "pairs.m_f43a0a4d8ccef23e")}><div className="work-order-status"><Badge tone="neutral">{localizedText(en, "pairs.m_6f7b0af9992cd6bf")}</Badge><Badge tone="warning">{localizedText(en, "pairs.m_6edb7d9a4aca3fd7")}</Badge><Badge tone="neutral">{value.currentRevision ? (localizedText(en, "pairs.m_1a3d3c3350a777f0")) : (localizedText(en, "pairs.m_ff547086f1eac9a3"))}</Badge></div><p>r{value.revision} · <time dateTime={value.savedAt}>{value.savedAt}</time></p>
    <p>{localizedText(en, "pairs.m_e81e83068386a55c")}{phase.state === 'Known' ? workOrderCodeLabel(phase.value!, en) + (localizedText(en, "pairs.m_6c12608d1399f4ac")) : workOrderFactLabel(phase.state, en)}</p>
    <p className="work-order-hint">{localizedText(en, "pairs.m_d4ded3f8a1bec68c")}</p>
    <details><summary>{localizedText(en, "pairs.m_acaca6ac79bc7f42")}</summary><p>{value.id} / {value.revision} / {value.version} · {value.provenance}</p><code>{value.hash}</code><p><code>{value.sourceVersion}</code></p><dl className="work-order-summary-grid">{workOrderFieldInfo().map(field => <div key={field.key}><dt>{localizedLabel(field, en)}</dt><dd>{value.fields[field.key].state === 'Known' ? value.fields[field.key].value : workOrderFactLabel(value.fields[field.key].state, en)}</dd></div>)}</dl>{value.plan && <p>{localizedText(en, "pairs.m_a7baebb651ea8368")}: {value.plan.id} / {value.plan.revision} / {value.plan.version}<br /><code>{value.plan.hash}</code><br /><code>{value.plan.sourceVersion}</code></p>}</details>
    <details><summary>{localizedText(en, "pairs.m_f3756323508b1048")}</summary><ul>{value.blockers.map(code => <li key={code}>{workOrderBlockerLabel(code, en)}</li>)}</ul></details>
  </section>;
}
export function WorkOrderPlanAside({ value, retained, issue, language, onNavigate }: {
    value?: WorkOrderPlanContext;
    retained?: WorkOrderPlanPin | null;
    issue?: 'stale' | 'unavailable';
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    return <aside className="work-order-plan-context" aria-label={localizedText(en, "pairs.m_ab9741448c1d77ac")}><h2>{localizedText(en, "pairs.m_48675e18dff978b4")}</h2>{value ? <><p>{value.asset.code ?? '—'} · {value.asset.name ?? (localizedText(en, "pairs.m_d86a85c4434a7368"))} · {localizedText(en, "pairs.m_aa8965b14ede67f9")}{value.pin.revision}</p><p className="work-order-hint">{localizedText(en, "pairs.m_b4e2b0955a14c155")}</p><details><summary>{localizedText(en, "pairs.m_ab81d359e05794cd")}</summary><dl className="work-order-summary-grid">{(['level', 'criticality', 'maker', 'model', 'location'] as const).map(key => <div key={key}><dt>{({ level: localizedText(en, "pairs.m_565ed7408ff7fbf5"), criticality: localizedText(en, "pairs.m_091fe1ec03e6eb2c"), maker: localizedText(en, "pairs.m_ab795242cd8f1169"), model: localizedText(en, "pairs.m_f900b2cb121f8415"), location: localizedText(en, "pairs.m_541dc83c56397469") })[key]}</dt><dd>{value.asset[key] ?? (localizedText(en, "pairs.m_ee32b29512abd890"))}</dd></div>)}</dl><p>{localizedText(en, "pairs.m_67f8140be9b17d0f")}{value.plan.assetRevision}</p><p>{value.pin.id} / {value.pin.revision} / {value.pin.version}</p><code>{value.pin.hash}</code><p>{value.provenance} · {value.savedAt}</p><Button variant="outline" onClick={() => onNavigate(value.href)}>{localizedText(en, "pairs.m_40aba9265669a3f4")}</Button></details></> : <p role={issue ? 'alert' : 'status'}>{issue === 'stale' ? (retained ? (localizedText(en, "pairs.m_6009c2200a275f24")) : (localizedText(en, "pairs.m_2efe1158ec0dbf37"))) : (localizedText(en, "pairs.m_af6398fa24e34753"))}</p>}{retained && !value && <details><summary>{localizedText(en, "pairs.m_3877edfc15b6c738")}</summary><p>{retained.id} / {retained.revision} / {retained.version}</p><code>{retained.hash}</code><br /><code>{retained.sourceVersion}</code></details>}</aside>;
}
export function WorkOrderRecordList({ page, offset, language, onOffset, onOpen }: {
    page: WorkOrderPage;
    offset: number;
    language: AppLocale;
    onOffset: (value: number) => void;
    onOpen: (id: string) => void;
}) {
    const en = language === 'en', [filter, setFilter] = useState(''), items = page.items.filter(record => !filter || record.fields.work_order_no.value?.toLocaleLowerCase(language).includes(filter.toLocaleLowerCase(language)));
    return <section aria-label={localizedText(en, "pairs.m_b0b03468e035db5d")}><h2>{localizedText(en, "pairs.m_bb525dcfc8253236")}</h2><EnterpriseToolbar><label>{localizedText(en, "pairs.m_4eca1c0036ea51ca")}<input type="search" value={filter} onChange={event => setFilter(event.target.value)}/></label><p className="work-order-hint">{localizedText(en, "pairs.m_e5987a5875676715")}</p></EnterpriseToolbar>{!page.items.length ? <p>{localizedText(en, "pairs.m_cb3e179286d4cfa0")}</p> : !items.length ? <p>{localizedText(en, "pairs.m_1c5341334d9aa4fa")}</p> : <ul className="work-order-records">{items.map(record => <li key={record.id}><Button variant="outline" onClick={() => onOpen(record.id)}>{record.fields.work_order_no.state === 'Known' ? record.fields.work_order_no.value : (localizedText(en, "pairs.m_c8461a515bcceaa5"))} · r{record.revision}</Button><WorkOrderSourceSummary value={record} language={language}/></li>)}</ul>}<EnterprisePagination label={localizedText(en, "pairs.m_df336cb28639d011")} previous={localizedText(en, "pairs.m_5506b07ad0d5f6b1")} next={localizedText(en, "pairs.m_c0dcda183dc2085b")} previousDisabled={!offset} nextDisabled={!page.hasMore || offset >= 100000} onPrevious={() => { setFilter(''); onOffset(Math.max(0, offset - 25)); }} onNext={() => { setFilter(''); onOffset(offset + 25); }}/></section>;
}
export function WorkOrderRecordHistory({ value, offset, disabled, language, onOffset }: {
    value: WorkOrderHistory;
    offset: number;
    disabled: boolean;
    language: AppLocale;
    onOffset: (value: number) => void;
}) {
    const en = language === 'en';
    return <section aria-label={localizedText(en, "pairs.m_e57197451f015fc7")}><h2>{localizedText(en, "pairs.m_d1c06f2335fcdcea")}</h2><p>{localizedText(en, "pairs.m_1b9308f5ab86e2d3")}</p>{!value.items.length && <p>{localizedText(en, "pairs.m_722c14de890f0967")}</p>}{value.items.map(record => <details key={record.revision}><summary>r{record.revision} · {record.savedAt}</summary><WorkOrderSourceSummary value={record} language={language}/></details>)}<EnterprisePagination label={localizedText(en, "pairs.m_1f4da3b6b9c30d83")} previous={localizedText(en, "pairs.m_79ebddaec7d89db0")} next={localizedText(en, "pairs.m_a41fcf745e74b43d")} previousDisabled={!offset || disabled} nextDisabled={!value.hasMore || disabled || offset >= 100000} onPrevious={() => onOffset(Math.max(0, offset - 25))} onNext={() => onOffset(offset + 25)}/></section>;
}
