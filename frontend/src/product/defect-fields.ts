import { localizedText, sourceText } from "../i18n/messages";
// DEV-055 / Backlog56. Supporting DD443–465 is not the complete owning
// asset-reliability schema and does not grant a safety decision workflow.
export const defectGroups = [
    { en: sourceText("en", "pairs.m_489dde9711d080b2"), tr: sourceText("tr", "pairs.m_489dde9711d080b2"), fields: [
            { key: 'defect_no', en: sourceText("en", "pairs.m_4efd84c874659c1a"), tr: sourceText("tr", "pairs.m_4efd84c874659c1a") },
            { key: 'record_datetime', en: sourceText("en", "pairs.m_2ed72dbf1f367b17"), tr: sourceText("tr", "pairs.m_2ed72dbf1f367b17") },
            { key: 'criticality', en: sourceText("en", "pairs.m_442bb3be7a6d4b52"), tr: sourceText("tr", "pairs.m_442bb3be7a6d4b52") },
            { key: 'lifecycle_stage', en: sourceText("en", "pairs.m_cbc2daebbf53f126"), tr: sourceText("tr", "pairs.m_cbc2daebbf53f126") },
            { key: 'failure_consequence', en: sourceText("en", "pairs.m_2c1e086b454d429b"), tr: sourceText("tr", "pairs.m_2c1e086b454d429b") },
        ] },
    { en: sourceText("en", "pairs.m_d73f03c3c6db6a98"), tr: sourceText("tr", "pairs.m_d73f03c3c6db6a98"), fields: [
            { key: 'safeguard', en: sourceText("en", "pairs.m_48bf72280842da30"), tr: sourceText("tr", "pairs.m_48bf72280842da30") },
            { key: 'restriction', en: sourceText("en", "pairs.m_e38c32b7651af4ed"), tr: sourceText("tr", "pairs.m_e38c32b7651af4ed") },
        ] },
    { en: sourceText("en", "pairs.m_4aa2ba134db76cc6"), tr: sourceText("tr", "pairs.m_4aa2ba134db76cc6"), fields: [
            { key: 'temporary_measure', en: sourceText("en", "pairs.m_b398ece81ee52515"), tr: sourceText("tr", "pairs.m_b398ece81ee52515") },
            { key: 'temporary_measure_expires_at', en: sourceText("en", "pairs.m_a4164e1e47d7e3ac"), tr: sourceText("tr", "pairs.m_a4164e1e47d7e3ac") },
        ] },
    { en: sourceText("en", "pairs.m_0a0d8ff4130f7e02"), tr: sourceText("tr", "pairs.m_0a0d8ff4130f7e02"), fields: [
            { key: 'action', en: sourceText("en", "pairs.m_3b3a767b1d368ff6"), tr: sourceText("tr", "pairs.m_3b3a767b1d368ff6") },
            { key: 'notes', en: sourceText("en", "pairs.m_70388f5ba2dbec5f"), tr: sourceText("tr", "pairs.m_70388f5ba2dbec5f") },
        ] },
] as const;
export type DefectFieldKey = typeof defectGroups[number]['fields'][number]['key'];
export type DefectFact = {
    state: 'Known' | 'Missing' | 'Unknown';
    value: string | null;
};
export type DefectFields = Record<DefectFieldKey, DefectFact>;
export type DefectFieldDefinition = {
    key: DefectFieldKey;
    type: 'Text' | 'Narrative' | 'Instant' | 'Code';
    required: false;
    options: string[];
    sourceLocator: string;
    requiredWhen: 'SOURCE_CONDITIONAL_UNRESOLVED' | null;
};
export const defectSourceVersion = '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:56;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:443-465:v46.3';
export const defectSchema = (): DefectFieldDefinition[] => {
    const dictionary = '14.02/sheet:Data Dictionary 2316/row:', backlog = '18.01/sheet:Backlog/row:56', conditional = 'SOURCE_CONDITIONAL_UNRESOLVED' as const;
    return [
        { key: 'defect_no', type: 'Text', required: false, options: [], sourceLocator: dictionary + '461', requiredWhen: conditional },
        { key: 'record_datetime', type: 'Instant', required: false, options: [], sourceLocator: dictionary + '447', requiredWhen: null },
        { key: 'criticality', type: 'Code', required: false, options: ['Critical', 'High', 'Medium', 'Low'], sourceLocator: dictionary + '457', requiredWhen: conditional },
        { key: 'failure_consequence', type: 'Narrative', required: false, options: [], sourceLocator: dictionary + '462', requiredWhen: conditional },
        { key: 'safeguard', type: 'Narrative', required: false, options: [], sourceLocator: dictionary + '463', requiredWhen: conditional },
        ...(['restriction', 'temporary_measure', 'action'] as const).map(key => ({ key, type: 'Narrative' as const, required: false as const, options: [], sourceLocator: backlog, requiredWhen: null })),
        { key: 'temporary_measure_expires_at', type: 'Instant', required: false, options: [], sourceLocator: backlog, requiredWhen: null },
        { key: 'lifecycle_stage', type: 'Code', required: false, options: ['Detect', 'Assess', 'Safeguard', 'Action', 'Verify', 'Effectiveness'], sourceLocator: backlog, requiredWhen: null },
        { key: 'notes', type: 'Narrative', required: false, options: [], sourceLocator: dictionary + '465', requiredWhen: null },
    ];
};
export function defectFieldInfo(): {
    key: DefectFieldKey;
    en: string;
    tr: string;
}[] {
    const fields: {
        key: DefectFieldKey;
        en: string;
        tr: string;
    }[] = [];
    for (const group of defectGroups)
        for (const field of group.fields)
            fields.push(field);
    return fields;
}
export const emptyDefectFields = (): DefectFields => Object.fromEntries(defectFieldInfo().map(field => [field.key, { state: 'Missing', value: null }])) as DefectFields;
const codeLabels: Record<string, string> = { Critical: 'Kritik', High: 'Yüksek', Medium: 'Orta', Low: 'Düşük', Detect: 'Tespit', Assess: 'Değerlendirme', Safeguard: 'Koruyucu tedbir', Action: 'İşlem', Verify: 'Doğrulama aşaması', Effectiveness: 'Etkinlik inceleme aşaması' };
export const defectCodeLabel = (value: string, en: boolean) => en ? value : codeLabels[value] ?? value;
export const defectFactLabel = (value: DefectFact['state'], en: boolean) => value === 'Known' ? (localizedText(en, "pairs.m_c2e9e0b3ab9f0e8c")) : value === 'Missing' ? (localizedText(en, "pairs.m_a5bd35d90de7c160")) : (localizedText(en, "pairs.m_a5a7675bd7000dbd"));
