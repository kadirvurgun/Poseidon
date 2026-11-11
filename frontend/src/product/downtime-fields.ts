import { codeOptionLabel, type PresentationLabel } from '../i18n/labels';
import { localizedText, sourceText } from "../i18n/messages";
// DEV-102 / T-OPS2-006. Source declarations retain uncertainty and never settle hire claims.
export const downtimeGroups = [
    { en: sourceText("en", "pairs.m_ec68495ac5051677"), tr: sourceText("tr", "pairs.m_ec68495ac5051677"), fields: [{ key: 'downtime_type', en: sourceText("en", "pairs.m_f26561a6d2042970"), tr: sourceText("tr", "pairs.m_f26561a6d2042970") }, { key: 'cause', en: sourceText("en", "pairs.m_0ba57fa3eff68bcb"), tr: sourceText("tr", "pairs.m_0ba57fa3eff68bcb") }, { key: 'operational_impact', en: sourceText("en", "pairs.m_144db25664af220d"), tr: sourceText("tr", "pairs.m_144db25664af220d") }] },
    { en: sourceText("en", "pairs.m_310dcdc4acaf2159"), tr: sourceText("tr", "pairs.m_310dcdc4acaf2159"), fields: [{ key: 'start_at', en: sourceText("en", "pairs.m_41234d7b4924b691"), tr: sourceText("tr", "pairs.m_41234d7b4924b691") }, { key: 'end_at', en: sourceText("en", "pairs.m_ca9946c8ae1f5299"), tr: sourceText("tr", "pairs.m_ca9946c8ae1f5299") }, { key: 'duration_minutes', en: sourceText("en", "pairs.m_d40cafe20b56da0d"), tr: sourceText("tr", "pairs.m_d40cafe20b56da0d") }] },
    { en: sourceText("en", "pairs.m_60fd3e510b341d52"), tr: sourceText("tr", "pairs.m_60fd3e510b341d52"), fields: [{ key: 'voyage_id', en: sourceText("en", "pairs.m_dc8d57b5db5355d8"), tr: sourceText("tr", "pairs.m_dc8d57b5db5355d8") }, { key: 'linked_defect', en: sourceText("en", "pairs.m_d4e80d5a8672e448"), tr: sourceText("tr", "pairs.m_d4e80d5a8672e448") }, { key: 'linked_work_order', en: sourceText("en", "pairs.m_98a842ec797594b2"), tr: sourceText("tr", "pairs.m_98a842ec797594b2") }, { key: 'linked_event', en: sourceText("en", "pairs.m_899f9fd36ec74bb1"), tr: sourceText("tr", "pairs.m_899f9fd36ec74bb1") }, { key: 'claim_evidence', en: sourceText("en", "pairs.m_ccd42c7271a019cb"), tr: sourceText("tr", "pairs.m_ccd42c7271a019cb") }] },
] as const;
export type DowntimeFieldKey = typeof downtimeGroups[number]['fields'][number]['key'];
export type DowntimeFact = {
    state: 'Known' | 'Missing' | 'Unknown';
    value: string | null;
};
export type DowntimeFields = Record<DowntimeFieldKey, DowntimeFact>;
export type DowntimeRecordFields = DowntimeFields & Record<'vessel_id' | 'loss_of_hire_flag' | 'closure_state', DowntimeFact>;
export type DowntimeFieldDefinition = {
    key: DowntimeFieldKey | 'vessel_id' | 'loss_of_hire_flag' | 'closure_state';
    type: 'Text' | 'Narrative' | 'Instant' | 'Number' | 'Code' | 'Reference';
    required: boolean;
    options: string[];
    readOnly: boolean;
    referenceUnavailable: boolean;
    sourceRow: number;
    unit: string | null;
    requiredWhen: 'SOURCE_CONDITIONAL_UNRESOLVED' | null;
};
export const downtimeSourceVersion = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2226-2239:v46.3';
export const downtimeTypeOptions = ['Technical Failure', 'Planned Maintenance', 'Weather', 'Port Delay', 'Crew', 'Regulatory', 'Commercial', 'Other'];
export function downtimeFieldInfo(): {
    key: DowntimeFieldKey;
    en: string;
    tr: string;
}[] { const result: {
    key: DowntimeFieldKey;
    en: string;
    tr: string;
}[] = []; for (const group of downtimeGroups)
    for (const field of group.fields)
        result.push(field); return result; }
export function emptyDowntimeFields(): DowntimeFields { return Object.fromEntries(downtimeFieldInfo().map(field => [field.key, { state: 'Missing', value: null }])) as DowntimeFields; }
export const downtimeFactLabel = (state: DowntimeFact['state'], en: boolean) => state === 'Known' ? (localizedText(en, "pairs.m_c2e9e0b3ab9f0e8c")) : state === 'Missing' ? (localizedText(en, "pairs.m_a5bd35d90de7c160")) : (localizedText(en, "pairs.m_a5a7675bd7000dbd"));
const labels: Readonly<Record<string, PresentationLabel>> = { 'Technical Failure': 'technicalFailure', 'Planned Maintenance': 'plannedMaintenance', Weather: 'weather', 'Port Delay': 'portDelay', Crew: 'crewCategory', Regulatory: 'regulatoryCategory', Commercial: 'commercialCategory', Other: 'other' };
export const downtimeCodeLabel = (value: string, en: boolean) => codeOptionLabel(value, en, labels);
export function downtimeSchema(): DowntimeFieldDefinition[] {
    const f = (key: DowntimeFieldDefinition['key'], type: DowntimeFieldDefinition['type'], sourceRow: number, required = false, readOnly = false): DowntimeFieldDefinition => ({ key, type, sourceRow, required, readOnly, options: key === 'downtime_type' ? [...downtimeTypeOptions] : key === 'loss_of_hire_flag' ? ['YES', 'NO', 'REVIEW_REQUIRED'] : key === 'closure_state' ? ['OPEN', 'VERIFIED', 'CLOSED'] : [], referenceUnavailable: type === 'Reference' && !readOnly, unit: key === 'duration_minutes' ? 'min' : null, requiredWhen: required ? null : 'SOURCE_CONDITIONAL_UNRESOLVED' });
    return [f('vessel_id', 'Reference', 2226, true, true), f('voyage_id', 'Reference', 2227), f('downtime_type', 'Code', 2228, true), f('start_at', 'Instant', 2229, true), f('end_at', 'Instant', 2230), f('duration_minutes', 'Number', 2231), f('cause', 'Narrative', 2232, true), f('linked_defect', 'Reference', 2233), f('linked_work_order', 'Reference', 2234), f('linked_event', 'Reference', 2235), f('operational_impact', 'Narrative', 2236, true), f('loss_of_hire_flag', 'Code', 2237, false, true), f('claim_evidence', 'Reference', 2238), f('closure_state', 'Code', 2239, true, true)];
}
export const downtimeMutableSchema = () => downtimeSchema().filter(field => !field.readOnly);
