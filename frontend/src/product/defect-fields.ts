// DEV-055 / Backlog56. Supporting DD443–465 is not the complete owning
// asset-reliability schema and does not grant a safety decision workflow.
export const defectGroups = [
  { en: 'Declaration and failure context', tr: 'Beyan ve arıza bağlamı', fields: [
    { key: 'defect_no', en: 'Defect number', tr: 'Kusur numarası' },
    { key: 'record_datetime', en: 'Declared record time', tr: 'Beyan edilen kayıt zamanı' },
    { key: 'criticality', en: 'Declared criticality', tr: 'Beyan edilen kritiklik' },
    { key: 'lifecycle_stage', en: 'Declared workflow phase', tr: 'Beyan edilen iş akışı aşaması' },
    { key: 'failure_consequence', en: 'Failure consequence declaration', tr: 'Arıza sonucu beyanı' },
  ] },
  { en: 'Safeguards and declared restrictions', tr: 'Koruyucu tedbirler ve beyan edilen kısıtlamalar', fields: [
    { key: 'safeguard', en: 'Safeguard declaration', tr: 'Koruyucu tedbir beyanı' },
    { key: 'restriction', en: 'Restriction declaration', tr: 'Kısıtlama beyanı' },
  ] },
  { en: 'Temporary measure and expiry', tr: 'Geçici tedbir ve son süre', fields: [
    { key: 'temporary_measure', en: 'Temporary measure declaration', tr: 'Geçici tedbir beyanı' },
    { key: 'temporary_measure_expires_at', en: 'Declared temporary measure expiry', tr: 'Beyan edilen geçici tedbir son süresi' },
  ] },
  { en: 'Action and source notes', tr: 'İşlem ve kaynak notları', fields: [
    { key: 'action', en: 'Action declaration', tr: 'İşlem beyanı' },
    { key: 'notes', en: 'Declaration notes', tr: 'Beyan notları' },
  ] },
] as const
export type DefectFieldKey = typeof defectGroups[number]['fields'][number]['key']
export type DefectFact = { state: 'Known' | 'Missing' | 'Unknown'; value: string | null }
export type DefectFields = Record<DefectFieldKey, DefectFact>
export type DefectFieldDefinition = { key: DefectFieldKey; type: 'Text' | 'Narrative' | 'Instant' | 'Code'; required: false; options: string[]; sourceLocator: string; requiredWhen: 'SOURCE_CONDITIONAL_UNRESOLVED' | null }
export const defectSourceVersion = '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:56;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:443-465:v46.3'
export const defectSchema = (): DefectFieldDefinition[] => {
  const dictionary = '14.02/sheet:Data Dictionary 2316/row:', backlog = '18.01/sheet:Backlog/row:56', conditional = 'SOURCE_CONDITIONAL_UNRESOLVED' as const
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
  ]
}
export function defectFieldInfo(): { key: DefectFieldKey; en: string; tr: string }[] {
  const fields: { key: DefectFieldKey; en: string; tr: string }[] = []
  for (const group of defectGroups) for (const field of group.fields) fields.push(field)
  return fields
}
export const emptyDefectFields = (): DefectFields => Object.fromEntries(defectFieldInfo().map(field => [field.key, { state: 'Missing', value: null }])) as DefectFields
const codeLabels: Record<string, string> = { Critical: 'Kritik', High: 'Yüksek', Medium: 'Orta', Low: 'Düşük', Detect: 'Tespit', Assess: 'Değerlendirme', Safeguard: 'Koruyucu tedbir', Action: 'İşlem', Verify: 'Doğrulama aşaması', Effectiveness: 'Etkinlik inceleme aşaması' }
export const defectCodeLabel = (value: string, en: boolean) => en ? value : codeLabels[value] ?? value
export const defectFactLabel = (value: DefectFact['state'], en: boolean) => value === 'Known' ? (en ? 'Declared value' : 'Beyan edilen değer') : value === 'Missing' ? (en ? 'Missing' : 'Eksik') : (en ? 'Unknown' : 'Bilinmiyor')
