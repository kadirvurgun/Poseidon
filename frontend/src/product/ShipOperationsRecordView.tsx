import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useState } from 'react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import { mutableShipOpsFields, shipOpsFields, shipOpsCodeOptions, type ShipOpsKind } from './ship-operation-fields';
import { shipOpsBlockerLabel, shipOpsFactLabel, shipOpsOptionLabel } from './ship-operation-copy';
import type { ShipOpsHistory, ShipOpsPage, ShipOpsRecord } from './ship-operations-api';
// DEV-101: sof_state is server-owned per revision (DRAFT, or FINAL only from a recorded human finalization).
// Show the recorded lifecycle state of this exact revision; anything else stays its fact state, never FINAL.
export function sofStateLabel(fact: ShipOpsRecord['fields'][string] | undefined, en: boolean) {
    if (fact?.state === 'Known' && fact.value === 'FINAL') return localizedText(en, "pairs.m_081f7dc1f7c9881a");
    if (fact?.state === 'Known' && fact.value === 'DRAFT') return localizedText(en, "pairs.m_9c5fd87ade98df4b");
    return shipOpsFactLabel(fact?.state ?? 'Unknown', en);
}
export function ShipOpsRecordSummary({ value, language, compact = false, contextOnly = false }: {
    value: ShipOpsRecord;
    language: AppLocale;
    compact?: boolean;
    contextOnly?: boolean;
}) {
    const en = language === 'en', keys = value.kind === 'Fuel' ? ['record_date', 'record_type', 'fuel_type', 'bdn_no', 'quantity_received_mt', 'quantity_consumed_mt', 'rob_before_mt', 'rob_after_mt'] : ['activity_type', 'event_start', 'event_end', 'terminal', 'cargo_quantity', 'remarks'];
    const definitions = contextOnly ? [] : shipOpsFields(value.kind).filter(field => keys.includes(field.key));
    return <section aria-label={localizedText(en, "pairs.m_53dc66072a9f60ec")} className="ship-operations-record-summary">
    <div className="ship-operations-status-line"><Badge tone="neutral">{localizedText(en, "pairs.m_c01bd20fcdef047d")}</Badge><Badge tone="warning">{localizedText(en, "pairs.m_6edb7d9a4aca3fd7")}</Badge><Badge tone="neutral">{value.currentRevision ? (localizedText(en, "pairs.m_1a3d3c3350a777f0")) : (localizedText(en, "pairs.m_ff547086f1eac9a3"))}</Badge></div>
    <p>r{value.revision} · <time dateTime={value.savedAt}>{value.savedAt}</time> · {value.provenance === 'SyntheticUnverified' ? (localizedText(en, "pairs.m_30fec0acc3d134b1")) : (localizedText(en, "pairs.m_f80308c1376523e6"))}</p>
    <dl className="ship-operations-summary-grid">{definitions.map(field => { const fact = value.fields[field.key]; return <div key={field.key}><dt>{localizedLabel(field, en)}{field.key.endsWith('_mt') ? ' (mt)' : ''}</dt><dd>{fact.state === 'Known' ? field.type === 'Code' ? shipOpsOptionLabel(fact.value!, en) : field.type === 'Instant' ? <time dateTime={fact.value!}>{fact.value}</time> : fact.value : shipOpsFactLabel(fact.state, en)}</dd></div>; })}</dl>
    {!compact && <>{!contextOnly && <p>{localizedText(en, "pairs.m_8164fd4adb2de542")}</p>}
      <dl className="ship-operations-summary-grid">{shipOpsFields(value.kind).filter(field => field.readOnly && field.key !== 'vessel_id').map(field => <div key={field.key}><dt>{localizedLabel(field, en)}</dt><dd>{field.key === 'compliance_state' ? (localizedText(en, "pairs.m_6edb7d9a4aca3fd7")) : field.key === 'verification_state' ? (localizedText(en, "pairs.m_c74b3cb63ce89b56")) : sofStateLabel(value.fields[field.key], en)}</dd></div>)}</dl>
    </>}
    <details><summary>{localizedText(en, "pairs.m_1cf31dc53d715270")}</summary><p>{value.id} / {value.revision} / {value.version}</p><code>{value.hash}</code><p><code>{value.sourceVersion}</code></p><dl className="ship-operations-summary-grid">{mutableShipOpsFields(value.kind).map(field => <div key={field.key}><dt>{localizedLabel(field, en)}</dt><dd className="ship-operations-declared-text">{value.fields[field.key].state === 'Known' ? value.fields[field.key].value : shipOpsFactLabel(value.fields[field.key].state, en)}</dd></div>)}</dl><ul>{value.blockers.map(code => <li key={code}>{shipOpsBlockerLabel(code, en)} · <code>{code}</code></li>)}</ul></details>
  </section>;
}
export function ShipOpsRecordList({ kind, page, language, offset, onOffset, onOpen }: {
    kind: ShipOpsKind;
    page: ShipOpsPage;
    language: AppLocale;
    offset: number;
    onOffset: (value: number) => void;
    onOpen: (id: string) => void;
}) {
    const en = language === 'en', key = kind === 'Fuel' ? 'record_type' : 'activity_type', [filter, setFilter] = useState('');
    const rows = page.items.filter(row => !filter || row.fields[key].state === 'Known' && row.fields[key].value === filter);
    return <section aria-label={localizedText(en, "pairs.m_c96c8fa43f9abb83")}>
    <h2>{kind === 'Fuel' ? (localizedText(en, "pairs.m_f9e49e5d9319cf49")) : (localizedText(en, "pairs.m_15f2146de90429cd"))}</h2>
    <EnterpriseToolbar><label>{localizedText(en, "pairs.m_b59084248b5fd813")}<select value={filter} onChange={event => setFilter(event.target.value)}><option value="">{localizedText(en, "pairs.m_332972a9b6d8bd37")}</option>{shipOpsCodeOptions[key].map(option => <option key={option} value={option}>{shipOpsOptionLabel(option, en)}</option>)}</select></label><p className="ship-operations-hint">{localizedText(en, "pairs.m_cca85d04e6837847")}</p></EnterpriseToolbar>
    {!page.items.length ? <p>{localizedText(en, "pairs.m_1f7376772c106f18")}</p> : !rows.length ? <p>{localizedText(en, "pairs.m_0d389759fd2d55c5")}</p> : <ol className={'ship-operations-record-list' + (kind === 'Port' ? ' ship-operations-timeline' : '')}>{rows.map(row => <li key={row.id}><Button variant="outline" onClick={() => onOpen(row.id)}>{row.fields[key].state === 'Known' ? shipOpsOptionLabel(row.fields[key].value!, en) : (localizedText(en, "pairs.m_2d109a0c78fcc35b"))} · r{row.revision}</Button><ShipOpsRecordSummary value={row} language={language} compact/></li>)}</ol>}
    <EnterprisePagination label={localizedText(en, "pairs.m_fdf3424927288d26")} previous={localizedText(en, "pairs.m_4935c0022d8aa09c")} next={localizedText(en, "pairs.m_408fc41e575d1974")} previousDisabled={!offset} nextDisabled={!page.hasMore || offset >= 100000} onPrevious={() => { setFilter(''); onOffset(Math.max(0, offset - 25)); }} onNext={() => { setFilter(''); onOffset(offset + 25); }}/>
  </section>;
}
export function ShipOpsRecordHistory({ value, language, offset, disabled, onOffset }: {
    value: ShipOpsHistory;
    language: AppLocale;
    offset: number;
    disabled: boolean;
    onOffset: (value: number) => void;
}) {
    const en = language === 'en';
    return <section aria-label={localizedText(en, "pairs.m_bb67f5de7855a586")}><h2>{localizedText(en, "pairs.m_edf58271348a6c9d")}</h2>{!value.items.length && <p>{localizedText(en, "pairs.m_722c14de890f0967")}</p>}{value.items.map(row => <details key={row.revision}><summary>r{row.revision} · {row.savedAt}</summary><ShipOpsRecordSummary value={row} language={language}/></details>)}<EnterprisePagination label={localizedText(en, "pairs.m_4b778d3c26440476")} previous={localizedText(en, "pairs.m_79ebddaec7d89db0")} next={localizedText(en, "pairs.m_a41fcf745e74b43d")} previousDisabled={!offset || disabled} nextDisabled={!value.hasMore || disabled} onPrevious={() => onOffset(Math.max(0, offset - 25))} onNext={() => onOffset(offset + 25)}/></section>;
}
