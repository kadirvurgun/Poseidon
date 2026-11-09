import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import { defectTemporalStates } from './defect-api';
import type { DefectAssetContext, DefectAssetPin, DefectHistory, DefectPage, DefectRecord } from './defect-api';
import { defectCodeLabel, defectFactLabel, defectFieldInfo } from './defect-fields';
import { defectBlockerLabel, defectTemporalLabel } from './defect-copy';
export function DefectSourceSummary({ value, language }: {
    value: DefectRecord;
    language: AppLocale;
}) {
    const en = language === 'en', criticality = value.fields.criticality, phase = value.fields.lifecycle_stage;
    return <section className="defect-source" aria-label={localizedText(en, "pairs.m_289bcc682e7d9f10")}><div className="defect-status"><Badge tone="neutral">{localizedText(en, "pairs.m_074e9aea13e833df")}</Badge><Badge tone="warning">{localizedText(en, "pairs.m_6edb7d9a4aca3fd7")}</Badge><Badge tone="neutral">{value.currentRevision ? (localizedText(en, "pairs.m_1a3d3c3350a777f0")) : (localizedText(en, "pairs.m_ff547086f1eac9a3"))}</Badge></div><p>r{value.revision} · <time dateTime={value.savedAt}>{value.savedAt}</time></p>
    <dl className="defect-summary-grid"><div><dt>{localizedText(en, "pairs.m_442bb3be7a6d4b52")}</dt><dd>{criticality.state === 'Known' ? defectCodeLabel(criticality.value!, en) + (localizedText(en, "pairs.m_6c12608d1399f4ac")) : defectFactLabel(criticality.state, en)}</dd></div><div><dt>{localizedText(en, "pairs.m_cbc2daebbf53f126")}</dt><dd>{phase.state === 'Known' ? defectCodeLabel(phase.value!, en) + (localizedText(en, "pairs.m_6c12608d1399f4ac")) : defectFactLabel(phase.state, en)}</dd></div></dl>
    <details><summary>{localizedText(en, "pairs.m_169261526212a0b5")}</summary><p>{value.id} / {value.revision} / {value.version} · {value.provenance}</p><code>{value.hash}</code><p><code>{value.sourceVersion}</code></p><dl className="defect-summary-grid">{defectFieldInfo().map(field => <div key={field.key}><dt>{localizedLabel(field, en)}</dt><dd>{value.fields[field.key].state === 'Known' ? value.fields[field.key].value : defectFactLabel(value.fields[field.key].state, en)}</dd></div>)}</dl>{value.asset && <p>{localizedText(en, "pairs.m_6122a260d22d86da")}: {value.asset.id} / {value.asset.revision} / {value.asset.version}<br /><code>{value.asset.hash}</code></p>}<ul>{value.blockers.map(code => <li key={code}>{defectBlockerLabel(code, en)} · <code>{code}</code></li>)}</ul></details>
  </section>;
}
export function DefectTemporalTimeline({ value, language, dirty = false }: {
    value: DefectRecord;
    language: AppLocale;
    dirty?: boolean;
}) {
    const en = language === 'en', record = value.fields.record_datetime, restriction = value.fields.restriction, expiry = value.fields.temporary_measure_expires_at;
    return <section className="defect-timeline" aria-label={localizedText(en, "pairs.m_31c75b45a594b7ac")}><h2>{localizedText(en, "pairs.m_f5324b8cf07b2181")}</h2>{dirty && <p>{localizedText(en, "pairs.m_21ef81e7478a5bc5")}{value.revision}{localizedText(en, "pairs.m_297b46fb73488a52")}</p>}
    <div className="defect-status"><Badge tone={value.temporal.temporaryMeasureState === 'EXPIRED_DECLARED' ? 'danger' : 'warning'}><AlertTriangle size={14} aria-hidden="true"/>{defectTemporalLabel(value.temporal.temporaryMeasureState, en)}</Badge></div>
    <ol><li><strong>{localizedText(en, "pairs.m_2ed72dbf1f367b17")}</strong><p>{record.state === 'Known' ? <time dateTime={record.value!}>{record.value}</time> : defectFactLabel(record.state, en)}</p></li><li><strong>{localizedText(en, "pairs.m_8179b11612fbf88e")}</strong><p className="defect-declared-text">{restriction.state === 'Known' ? restriction.value : defectFactLabel(restriction.state, en)}</p></li><li><strong>{localizedText(en, "pairs.m_a4164e1e47d7e3ac")}</strong><p>{expiry.state === 'Known' ? <time dateTime={expiry.value!}>{expiry.value}</time> : defectFactLabel(expiry.state, en)}</p></li><li><strong>{localizedText(en, "pairs.m_ecc1eca1143c92ba")}</strong><p><time dateTime={value.temporal.assessedAt}>{value.temporal.assessedAt}</time></p></li></ol>
    <p>{localizedText(en, "pairs.m_af4fbb5356bab53c")}</p>
  </section>;
}
export function DefectAssetAside({ value, retained, issue, language, onNavigate }: {
    value?: DefectAssetContext;
    retained?: DefectAssetPin | null;
    issue?: 'stale' | 'unavailable';
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    return <aside className="defect-asset-context" aria-label={localizedText(en, "pairs.m_69ac6f6f8298c944")}><h2>{localizedText(en, "pairs.m_b6360c94e1f68047")}</h2>{value ? <><p>{value.asset.code ?? '—'} · {value.asset.name ?? (localizedText(en, "pairs.m_d86a85c4434a7368"))} · r{value.pin.revision}</p><p className="defect-hint">{localizedText(en, "pairs.m_9240a60706b6e005")}</p><details><summary>{localizedText(en, "pairs.m_34abc35a5c35b69f")}</summary><dl className="defect-summary-grid">{(['level', 'criticality', 'maker', 'model', 'location'] as const).map(key => <div key={key}><dt>{({ level: localizedText(en, "pairs.m_565ed7408ff7fbf5"), criticality: localizedText(en, "pairs.m_091fe1ec03e6eb2c"), maker: localizedText(en, "pairs.m_ab795242cd8f1169"), model: localizedText(en, "pairs.m_f900b2cb121f8415"), location: localizedText(en, "pairs.m_541dc83c56397469") })[key]}</dt><dd>{value.asset[key] ?? (localizedText(en, "pairs.m_ee32b29512abd890"))}</dd></div>)}</dl><p>{value.pin.id} / {value.pin.revision} / {value.pin.version}</p><code>{value.pin.hash}</code><p>{value.provenance} · {value.savedAt}</p><Button variant="outline" onClick={() => onNavigate(value.href)}>{localizedText(en, "pairs.m_b886e79deff0f364")}</Button></details></> : <p role={issue ? 'alert' : 'status'}>{issue === 'stale' ? (retained ? (localizedText(en, "pairs.m_3166eb02536d9d3d")) : (localizedText(en, "pairs.m_e827eac4c86477a9"))) : (localizedText(en, "pairs.m_917ae8d36ff37cfe"))}</p>}{retained && !value && <details><summary>{localizedText(en, "pairs.m_031708e774d36e78")}</summary><p>{retained.id} / {retained.revision} / {retained.version}</p><code>{retained.hash}</code></details>}</aside>;
}
export function DefectRecordList({ page, offset, language, onOffset, onOpen }: {
    page: DefectPage;
    offset: number;
    language: AppLocale;
    onOffset: (value: number) => void;
    onOpen: (id: string) => void;
}) {
    const en = language === 'en', [filter, setFilter] = useState(''), items = page.items.filter(record => !filter || record.temporal.temporaryMeasureState === filter);
    return <section aria-label={localizedText(en, "pairs.m_76229d24dc6095a1")}><h2>{localizedText(en, "pairs.m_b92ab11f37ccd3d0")}</h2><EnterpriseToolbar><label>{localizedText(en, "pairs.m_c29864def5699524")}<select value={filter} onChange={event => setFilter(event.target.value)}><option value="">{localizedText(en, "pairs.m_11f23c6df7ce7e0e")}</option>{defectTemporalStates.map(state => <option key={state} value={state}>{defectTemporalLabel(state, en)}</option>)}</select></label><p className="defect-hint">{localizedText(en, "pairs.m_408760c0c11934af")}</p></EnterpriseToolbar>{!page.items.length ? <p>{localizedText(en, "pairs.m_e6b2dc6a86013cbc")}</p> : !items.length ? <p>{localizedText(en, "pairs.m_b7fc80b4f235675c")}</p> : <ul className="defect-records">{items.map(record => <li key={record.id}><Button variant="outline" onClick={() => onOpen(record.id)}>{record.fields.defect_no.state === 'Known' ? record.fields.defect_no.value : (localizedText(en, "pairs.m_bdae892b8070cb60"))} · r{record.revision}</Button><DefectSourceSummary value={record} language={language}/><p>{defectTemporalLabel(record.temporal.temporaryMeasureState, en)} · <time dateTime={record.temporal.assessedAt}>{record.temporal.assessedAt}</time></p></li>)}</ul>}<EnterprisePagination label={localizedText(en, "pairs.m_9a26c73a2d964971")} previous={localizedText(en, "pairs.m_c7cdae51adacbd41")} next={localizedText(en, "pairs.m_7e044748863d841e")} previousDisabled={!offset} nextDisabled={!page.hasMore || offset >= 100000} onPrevious={() => { setFilter(''); onOffset(Math.max(0, offset - 25)); }} onNext={() => { setFilter(''); onOffset(offset + 25); }}/></section>;
}
export function DefectRecordHistory({ value, offset, disabled, language, onOffset }: {
    value: DefectHistory;
    offset: number;
    disabled: boolean;
    language: AppLocale;
    onOffset: (value: number) => void;
}) {
    const en = language === 'en';
    return <section aria-label={localizedText(en, "pairs.m_c0e23b90cd3b9a7e")}><h2>{localizedText(en, "pairs.m_b2ca4be235c151e1")}</h2><p>{localizedText(en, "pairs.m_f4a5c0a7bd241cfb")}</p>{!value.items.length && <p>{localizedText(en, "pairs.m_722c14de890f0967")}</p>}{value.items.map(record => <details key={record.revision}><summary>r{record.revision} · {record.savedAt}</summary><DefectSourceSummary value={record} language={language}/><DefectTemporalTimeline value={record} language={language}/></details>)}<EnterprisePagination label={localizedText(en, "pairs.m_0f92a788a935fd18")} previous={localizedText(en, "pairs.m_79ebddaec7d89db0")} next={localizedText(en, "pairs.m_a41fcf745e74b43d")} previousDisabled={!offset || disabled} nextDisabled={!value.hasMore || disabled} onPrevious={() => onOffset(Math.max(0, offset - 25))} onNext={() => onOffset(offset + 25)}/></section>;
}
