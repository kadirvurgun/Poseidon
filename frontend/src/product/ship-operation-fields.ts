import { localizedLabel } from "../i18n/messages";
import { sourceText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
// Source-owned presentation metadata: 14.02 Data Dictionary2316 rows2186–2225.
// Source labels/options do not establish approved catalogs or legal transitions.
export const shipOpsSourceVersion = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2186-2225:v46.3';
export const shipOpsKinds = ['Fuel', 'Port'] as const;
export type ShipOpsKind = typeof shipOpsKinds[number];
export type ShipOpsFact = {
    state: 'Known' | 'Missing' | 'Unknown';
    value: string | null;
};
export type ShipOpsFields = Record<string, ShipOpsFact>;
export type ShipOpsFieldType = 'Instant' | 'Reference' | 'Code' | 'Text' | 'Narrative' | 'Number';
export type ShipOpsFieldDefinition = {
    key: string;
    type: ShipOpsFieldType;
    required: boolean;
    options: string[];
    readOnly: boolean;
    referenceUnavailable: boolean;
    sourceRow: number;
    unit: string | null;
    requiredWhen: string | null;
};
export type ShipOpsFieldInfo = {
    key: string;
    en: string;
    tr: string;
    type: ShipOpsFieldType;
    sourceRow: number;
    readOnly?: boolean;
};
export const shipOpsGroups: Record<ShipOpsKind, {
    en: string;
    tr: string;
    fields: ShipOpsFieldInfo[];
}[]> = {
    Fuel: [
        { en: sourceText("en", "pairs.m_2348b3a93c18d2f9"), tr: sourceText("tr", "pairs.m_2348b3a93c18d2f9"), fields: [
                { key: 'record_date', en: sourceText("en", "pairs.m_3481f859a29507c0"), tr: sourceText("tr", "pairs.m_3481f859a29507c0"), type: 'Instant', sourceRow: 2186 },
                { key: 'vessel_id', en: sourceText("en", "pairs.m_9d42847c6fc73a45"), tr: sourceText("tr", "pairs.m_9d42847c6fc73a45"), type: 'Reference', sourceRow: 2187, readOnly: true },
                { key: 'voyage_id', en: sourceText("en", "pairs.m_dc8d57b5db5355d8"), tr: sourceText("tr", "pairs.m_dc8d57b5db5355d8"), type: 'Reference', sourceRow: 2188 },
                { key: 'record_type', en: sourceText("en", "pairs.m_e62cf48ba717f732"), tr: sourceText("tr", "pairs.m_e62cf48ba717f732"), type: 'Code', sourceRow: 2189 },
                { key: 'fuel_type', en: sourceText("en", "pairs.m_5790dd26a9b3f7e3"), tr: sourceText("tr", "pairs.m_5790dd26a9b3f7e3"), type: 'Code', sourceRow: 2190 },
                { key: 'supplier', en: sourceText("en", "pairs.m_caac20a7ea49e35a"), tr: sourceText("tr", "pairs.m_caac20a7ea49e35a"), type: 'Reference', sourceRow: 2191 },
                { key: 'bdn_no', en: sourceText("en", "pairs.m_399ec933d54eff53"), tr: sourceText("tr", "pairs.m_399ec933d54eff53"), type: 'Text', sourceRow: 2192 },
                { key: 'delivery_port', en: sourceText("en", "pairs.m_b1bf0ed30747c625"), tr: sourceText("tr", "pairs.m_b1bf0ed30747c625"), type: 'Reference', sourceRow: 2193 },
            ] },
        { en: sourceText("en", "pairs.m_955ad0dfb23979e0"), tr: sourceText("tr", "pairs.m_955ad0dfb23979e0"), fields: [
                { key: 'quantity_received_mt', en: sourceText("en", "pairs.m_d78f79fc3fa41efe"), tr: sourceText("tr", "pairs.m_d78f79fc3fa41efe"), type: 'Number', sourceRow: 2194 },
                { key: 'quantity_consumed_mt', en: sourceText("en", "pairs.m_f8f1bd5c26827d14"), tr: sourceText("tr", "pairs.m_f8f1bd5c26827d14"), type: 'Number', sourceRow: 2195 },
                { key: 'rob_before_mt', en: sourceText("en", "pairs.m_abb352f878883cac"), tr: sourceText("tr", "pairs.m_abb352f878883cac"), type: 'Number', sourceRow: 2196 },
                { key: 'rob_after_mt', en: sourceText("en", "pairs.m_11083a288d5ee6e4"), tr: sourceText("tr", "pairs.m_11083a288d5ee6e4"), type: 'Number', sourceRow: 2197 },
                { key: 'tank_id', en: sourceText("en", "pairs.m_45e8b5da8857c32f"), tr: sourceText("tr", "pairs.m_45e8b5da8857c32f"), type: 'Reference', sourceRow: 2198 },
                { key: 'tank_capacity', en: sourceText("en", "pairs.m_49c034002266664e"), tr: sourceText("tr", "pairs.m_49c034002266664e"), type: 'Number', sourceRow: 2199 },
            ] },
        { en: sourceText("en", "pairs.m_33622539bf24ac12"), tr: sourceText("tr", "pairs.m_33622539bf24ac12"), fields: [
                { key: 'sulphur_pct', en: sourceText("en", "pairs.m_8948f43c080234e9"), tr: sourceText("tr", "pairs.m_8948f43c080234e9"), type: 'Number', sourceRow: 2200 },
                { key: 'viscosity', en: sourceText("en", "pairs.m_2cb71b4da83d2cea"), tr: sourceText("tr", "pairs.m_2cb71b4da83d2cea"), type: 'Number', sourceRow: 2201 },
                { key: 'density', en: sourceText("en", "pairs.m_d4badadcaea4598d"), tr: sourceText("tr", "pairs.m_d4badadcaea4598d"), type: 'Number', sourceRow: 2202 },
                { key: 'sample_seal_no', en: sourceText("en", "pairs.m_5ab3fe1b1157aed3"), tr: sourceText("tr", "pairs.m_5ab3fe1b1157aed3"), type: 'Text', sourceRow: 2203 },
                { key: 'delivery_discrepancy', en: sourceText("en", "pairs.m_e6989e131290f612"), tr: sourceText("tr", "pairs.m_e6989e131290f612"), type: 'Code', sourceRow: 2204 },
                { key: 'compliance_state', en: sourceText("en", "pairs.m_6f4ce262b1d60012"), tr: sourceText("tr", "pairs.m_6f4ce262b1d60012"), type: 'Code', sourceRow: 2205, readOnly: true },
                { key: 'verification_state', en: sourceText("en", "pairs.m_5266e1cb93753f18"), tr: sourceText("tr", "pairs.m_5266e1cb93753f18"), type: 'Code', sourceRow: 2206, readOnly: true },
                { key: 'evidence_ref', en: sourceText("en", "pairs.m_e0942db861384fcf"), tr: sourceText("tr", "pairs.m_e0942db861384fcf"), type: 'Reference', sourceRow: 2207 },
            ] },
    ],
    Port: [
        { en: sourceText("en", "pairs.m_edd180539cb49a0f"), tr: sourceText("tr", "pairs.m_edd180539cb49a0f"), fields: [
                { key: 'voyage_id', en: sourceText("en", "pairs.m_dc8d57b5db5355d8"), tr: sourceText("tr", "pairs.m_dc8d57b5db5355d8"), type: 'Reference', sourceRow: 2208 },
                { key: 'port_call_id', en: sourceText("en", "pairs.m_030259b533e03439"), tr: sourceText("tr", "pairs.m_030259b533e03439"), type: 'Reference', sourceRow: 2209 },
                { key: 'port', en: sourceText("en", "pairs.m_cbad0b491f9631c7"), tr: sourceText("tr", "pairs.m_cbad0b491f9631c7"), type: 'Reference', sourceRow: 2210 },
                { key: 'terminal', en: sourceText("en", "pairs.m_120a9e3e7ad504a7"), tr: sourceText("tr", "pairs.m_120a9e3e7ad504a7"), type: 'Text', sourceRow: 2211 },
            ] },
        { en: sourceText("en", "pairs.m_93abef9b5e8b87fc"), tr: sourceText("tr", "pairs.m_93abef9b5e8b87fc"), fields: [
                { key: 'activity_type', en: sourceText("en", "pairs.m_0fb50c58dc1ee5a1"), tr: sourceText("tr", "pairs.m_0fb50c58dc1ee5a1"), type: 'Code', sourceRow: 2212 },
                { key: 'event_start', en: sourceText("en", "pairs.m_44966b609e54e58a"), tr: sourceText("tr", "pairs.m_44966b609e54e58a"), type: 'Instant', sourceRow: 2213 },
                { key: 'event_end', en: sourceText("en", "pairs.m_f9b1a4f18c89dcee"), tr: sourceText("tr", "pairs.m_f9b1a4f18c89dcee"), type: 'Instant', sourceRow: 2214 },
                { key: 'remarks', en: sourceText("en", "pairs.m_77ed3e0e1e5abc95"), tr: sourceText("tr", "pairs.m_77ed3e0e1e5abc95"), type: 'Narrative', sourceRow: 2215 },
            ] },
        { en: sourceText("en", "pairs.m_636490ba23547d3d"), tr: sourceText("tr", "pairs.m_636490ba23547d3d"), fields: [
                { key: 'cargo_operation', en: sourceText("en", "pairs.m_ae281cc6a175271d"), tr: sourceText("tr", "pairs.m_ae281cc6a175271d"), type: 'Text', sourceRow: 2216 },
                { key: 'cargo_quantity', en: sourceText("en", "pairs.m_6dd8ee12a45041a8"), tr: sourceText("tr", "pairs.m_6dd8ee12a45041a8"), type: 'Number', sourceRow: 2217 },
                { key: 'draught_fwd', en: sourceText("en", "pairs.m_13dcf110dc0456ad"), tr: sourceText("tr", "pairs.m_13dcf110dc0456ad"), type: 'Number', sourceRow: 2218 },
                { key: 'draught_aft', en: sourceText("en", "pairs.m_7f5a8108d969c7bd"), tr: sourceText("tr", "pairs.m_7f5a8108d969c7bd"), type: 'Number', sourceRow: 2219 },
            ] },
        { en: sourceText("en", "pairs.m_e1ce1ab34ae08711"), tr: sourceText("tr", "pairs.m_e1ce1ab34ae08711"), fields: [
                { key: 'tug_usage', en: sourceText("en", "pairs.m_2e40f06f1afa0929"), tr: sourceText("tr", "pairs.m_2e40f06f1afa0929"), type: 'Text', sourceRow: 2220 },
                { key: 'pilot_usage', en: sourceText("en", "pairs.m_18277af06708c6d2"), tr: sourceText("tr", "pairs.m_18277af06708c6d2"), type: 'Text', sourceRow: 2221 },
                { key: 'delay_reason', en: sourceText("en", "pairs.m_5db8c28ac135f2a0"), tr: sourceText("tr", "pairs.m_5db8c28ac135f2a0"), type: 'Narrative', sourceRow: 2222 },
                { key: 'letter_of_protest', en: sourceText("en", "pairs.m_5fece9e75fcdea26"), tr: sourceText("tr", "pairs.m_5fece9e75fcdea26"), type: 'Reference', sourceRow: 2223 },
                { key: 'sof_state', en: sourceText("en", "pairs.m_de20d489fe59aecd"), tr: sourceText("tr", "pairs.m_de20d489fe59aecd"), type: 'Code', sourceRow: 2224, readOnly: true },
                { key: 'evidence_ref', en: sourceText("en", "pairs.m_247b02384243ff9a"), tr: sourceText("tr", "pairs.m_247b02384243ff9a"), type: 'Reference', sourceRow: 2225 },
            ] },
    ],
};
export const shipOpsFields = (kind: ShipOpsKind) => shipOpsGroups[kind].flatMap(group => group.fields);
export const mutableShipOpsFields = (kind: ShipOpsKind) => shipOpsFields(kind).filter(field => !field.readOnly);
export const emptyShipOpsFields = (kind: ShipOpsKind): ShipOpsFields => Object.fromEntries(mutableShipOpsFields(kind).map(field => [field.key, { state: 'Missing', value: null }]));
export const shipOpsTitle = (kind: ShipOpsKind, language: AppLocale) => localizedLabel(({ Fuel: { en: sourceText("en", "pairs.m_4409cb4e233ec268"), tr: sourceText("tr", "pairs.m_4409cb4e233ec268") }, Port: { en: sourceText("en", "pairs.m_13875df296e09add"), tr: sourceText("tr", "pairs.m_13875df296e09add") } })[kind], language);
export const shipOpsScreen = (kind: ShipOpsKind) => kind === 'Fuel' ? 'fuel-bunker-log' : 'port-activity-sof';
export const shipOpsRequired: Record<ShipOpsKind, string[]> = { Fuel: ['record_date', 'vessel_id', 'record_type', 'fuel_type', 'compliance_state', 'verification_state'], Port: ['voyage_id', 'port_call_id', 'port', 'activity_type', 'event_start', 'sof_state'] };
export const shipOpsCodeOptions: Record<string, readonly string[]> = {
    record_type: ['Consumption', 'Delivery/BDN', 'Transfer', 'ROB', 'Tank Sounding'], fuel_type: ['VLSFO', 'HFO', 'MGO', 'LNG', 'Methanol', 'Other'], delivery_discrepancy: ['NONE', 'ATTENTION', 'DISPUTED'],
    compliance_state: ['READY', 'CONDITIONAL', 'REVIEW_REQUIRED', 'NOT_READY'], verification_state: ['PENDING', 'VERIFIED', 'REJECTED'], sof_state: ['DRAFT', 'REVIEWED', 'FINAL'],
    activity_type: ['Arrival', 'NOR', 'Pilot Onboard', 'Tug Made Fast', 'Berthing', 'Loading', 'Discharging', 'Shifting', 'Tug Let Go', 'Pilot Off', 'Departure', 'Other'],
};
