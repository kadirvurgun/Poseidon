import { codeOptionLabel, type PresentationLabel } from '../i18n/labels';
import { localizedText, sourceText } from "../i18n/messages";
// DEV-054 / T-S8-003. Source declarations only; execution literals never grant authority.
export const workOrderGroups = [
    { en: sourceText("en", "pairs.m_4a67d8cf2cf94194"), tr: sourceText("tr", "pairs.m_4a67d8cf2cf94194"), fields: [
            { key: 'work_order_no', en: sourceText("en", "pairs.m_e354d090e981aa67"), tr: sourceText("tr", "pairs.m_e354d090e981aa67") }, { key: 'description', en: sourceText("en", "pairs.m_7ee87280b3a33af5"), tr: sourceText("tr", "pairs.m_7ee87280b3a33af5") },
            { key: 'work_order_type', en: sourceText("en", "pairs.m_3add116e33ab9703"), tr: sourceText("tr", "pairs.m_3add116e33ab9703") }, { key: 'priority', en: sourceText("en", "pairs.m_9077f204711545d1"), tr: sourceText("tr", "pairs.m_9077f204711545d1") }, { key: 'criticality', en: sourceText("en", "pairs.m_53cb407a681f5bfd"), tr: sourceText("tr", "pairs.m_53cb407a681f5bfd") },
            { key: 'execution_stage', en: sourceText("en", "pairs.m_8f7eaa808f9001d5"), tr: sourceText("tr", "pairs.m_8f7eaa808f9001d5") },
        ] },
    { en: sourceText("en", "pairs.m_8f97aee0d4249d88"), tr: sourceText("tr", "pairs.m_8f97aee0d4249d88"), fields: [
            { key: 'equipment_id', en: sourceText("en", "pairs.m_8dd47e82f3b62583"), tr: sourceText("tr", "pairs.m_8dd47e82f3b62583") }, { key: 'job_id', en: sourceText("en", "pairs.m_6fe5fc29c5956743"), tr: sourceText("tr", "pairs.m_6fe5fc29c5956743") },
            { key: 'assigned_to', en: sourceText("en", "pairs.m_563a8ef4f276ff50"), tr: sourceText("tr", "pairs.m_563a8ef4f276ff50") }, { key: 'executed_by', en: sourceText("en", "pairs.m_674383d89dde916d"), tr: sourceText("tr", "pairs.m_674383d89dde916d") },
        ] },
    { en: sourceText("en", "pairs.m_e32c5f19d2d3db43"), tr: sourceText("tr", "pairs.m_e32c5f19d2d3db43"), fields: [
            { key: 'planned_start', en: sourceText("en", "pairs.m_30d2e6e7628a2599"), tr: sourceText("tr", "pairs.m_30d2e6e7628a2599") }, { key: 'due_date', en: sourceText("en", "pairs.m_0320b67d378f439d"), tr: sourceText("tr", "pairs.m_0320b67d378f439d") }, { key: 'planned_finish', en: sourceText("en", "pairs.m_447a8901aa5d298b"), tr: sourceText("tr", "pairs.m_447a8901aa5d298b") },
            { key: 'actual_start', en: sourceText("en", "pairs.m_da2f74383eba6581"), tr: sourceText("tr", "pairs.m_da2f74383eba6581") }, { key: 'actual_finish', en: sourceText("en", "pairs.m_0f859f41d6490030"), tr: sourceText("tr", "pairs.m_0f859f41d6490030") },
        ] },
    { en: sourceText("en", "pairs.m_0ddc97cb7534c22e"), tr: sourceText("tr", "pairs.m_0ddc97cb7534c22e"), fields: [
            { key: 'meter_before', en: sourceText("en", "pairs.m_5a40fbb7262bfb0a"), tr: sourceText("tr", "pairs.m_5a40fbb7262bfb0a") }, { key: 'meter_after', en: sourceText("en", "pairs.m_24bbd576e7791ddc"), tr: sourceText("tr", "pairs.m_24bbd576e7791ddc") },
            { key: 'findings', en: sourceText("en", "pairs.m_515d575bb23c0ba2"), tr: sourceText("tr", "pairs.m_515d575bb23c0ba2") }, { key: 'work_done', en: sourceText("en", "pairs.m_eccf24d8c9e33351"), tr: sourceText("tr", "pairs.m_eccf24d8c9e33351") }, { key: 'spares_used', en: sourceText("en", "pairs.m_a0f6201b22b020a5"), tr: sourceText("tr", "pairs.m_a0f6201b22b020a5") }, { key: 'measurements', en: sourceText("en", "pairs.m_1f9a6ddf1ec04302"), tr: sourceText("tr", "pairs.m_1f9a6ddf1ec04302") },
        ] },
    { en: sourceText("en", "pairs.m_e3f339236ca50147"), tr: sourceText("tr", "pairs.m_e3f339236ca50147"), fields: [
            { key: 'photo_evidence', en: sourceText("en", "pairs.m_816f23617dcb9294"), tr: sourceText("tr", "pairs.m_816f23617dcb9294") }, { key: 'document_evidence', en: sourceText("en", "pairs.m_c71786c9cd3729f8"), tr: sourceText("tr", "pairs.m_c71786c9cd3729f8") },
            { key: 'verification_result', en: sourceText("en", "pairs.m_87cdceee6d5dff37"), tr: sourceText("tr", "pairs.m_87cdceee6d5dff37") }, { key: 'verified_by', en: sourceText("en", "pairs.m_ec124f81a056d92c"), tr: sourceText("tr", "pairs.m_ec124f81a056d92c") },
        ] },
] as const;
export type WorkOrderFieldKey = typeof workOrderGroups[number]['fields'][number]['key'];
export type WorkOrderFact = {
    state: 'Known' | 'Missing' | 'Unknown';
    value: string | null;
};
export type WorkOrderFields = Record<WorkOrderFieldKey, WorkOrderFact>;
export type WorkOrderFieldDefinition = {
    key: WorkOrderFieldKey;
    type: 'Text' | 'Narrative' | 'Date' | 'Number' | 'Code' | 'Reference' | 'SourceConflict';
    required: boolean;
    options: string[];
    sourceLocator: string;
    requiredWhen: 'SOURCE_CONDITIONAL_UNRESOLVED' | null;
    knownUnavailable: boolean;
};
export const workOrderSourceVersion = '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:55;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:1426-1463:v46.3';
export function workOrderFieldInfo(): {
    key: WorkOrderFieldKey;
    en: string;
    tr: string;
}[] { const fields: {
    key: WorkOrderFieldKey;
    en: string;
    tr: string;
}[] = []; for (const group of workOrderGroups)
    for (const field of group.fields)
        fields.push(field); return fields; }
export const emptyWorkOrderFields = (): WorkOrderFields => Object.fromEntries(workOrderFieldInfo().map(field => [field.key, { state: 'Missing', value: null }])) as WorkOrderFields;
const phaseLabels: Readonly<Record<string, PresentationLabel>> = { RELEASED: 'releasedPhaseDeclaration', IN_PROGRESS: 'inProgressPhaseDeclaration', EVIDENCE_SUBMITTED: 'evidenceSubmittedPhaseDeclaration' };
export const workOrderCodeLabel = (value: string, en: boolean) => codeOptionLabel(value, en, phaseLabels);
export const workOrderFactLabel = (state: WorkOrderFact['state'], en: boolean) => state === 'Known' ? (localizedText(en, "pairs.m_c2e9e0b3ab9f0e8c")) : state === 'Missing' ? (localizedText(en, "pairs.m_a5bd35d90de7c160")) : (localizedText(en, "pairs.m_a5a7675bd7000dbd"));
export function workOrderSchema(): WorkOrderFieldDefinition[] {
    const f = (key: WorkOrderFieldKey, type: WorkOrderFieldDefinition['type'], row: number, required = false, knownUnavailable = false): WorkOrderFieldDefinition => ({ key, type, required, options: [], sourceLocator: '14.02/sheet:Data Dictionary 2316/row:' + row, requiredWhen: required ? null : 'SOURCE_CONDITIONAL_UNRESOLVED', knownUnavailable });
    return [f('work_order_no', 'Text', 1427, true), f('equipment_id', 'Reference', 1428, true, true), f('job_id', 'Reference', 1429, false, true), f('work_order_type', 'Code', 1430, false, true), f('priority', 'Code', 1431, false, true), f('criticality', 'Code', 1432, false, true), f('planned_start', 'Date', 1433), f('due_date', 'Date', 1434), f('assigned_to', 'SourceConflict', 1435, false, true), f('description', 'Narrative', 1444), f('planned_finish', 'Date', 1448), f('actual_start', 'Date', 1449), f('actual_finish', 'Date', 1450), f('executed_by', 'Reference', 1452, false, true), f('meter_before', 'Number', 1453), f('meter_after', 'Number', 1454), f('findings', 'Narrative', 1455), f('work_done', 'Narrative', 1456), f('spares_used', 'Text', 1457), f('measurements', 'Text', 1458), f('photo_evidence', 'Reference', 1459, false, true), f('document_evidence', 'Reference', 1460, false, true), f('verification_result', 'Text', 1461), f('verified_by', 'Reference', 1462, false, true), { key: 'execution_stage', type: 'Code', required: false, options: ['RELEASED', 'IN_PROGRESS', 'EVIDENCE_SUBMITTED'], sourceLocator: '18.01/sheet:Backlog/row:55', requiredWhen: null, knownUnavailable: false }];
}
