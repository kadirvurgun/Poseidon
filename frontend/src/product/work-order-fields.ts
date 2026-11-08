// DEV-054 / T-S8-003. Source declarations only; execution literals never grant authority.
export const workOrderGroups = [
  { en: 'Work order and declared priority', tr: 'İş emri ve beyan edilen öncelik', fields: [
    { key: 'work_order_no', en: 'Work order number', tr: 'İş emri numarası' }, { key: 'description', en: 'Work description', tr: 'İş açıklaması' },
    { key: 'work_order_type', en: 'Work order type', tr: 'İş emri türü' }, { key: 'priority', en: 'Priority', tr: 'Öncelik' }, { key: 'criticality', en: 'Criticality', tr: 'Kritiklik' },
    { key: 'execution_stage', en: 'Declared execution phase', tr: 'Beyan edilen yürütme aşaması' },
  ] },
  { en: 'Equipment and responsibility', tr: 'Ekipman ve sorumluluk', fields: [
    { key: 'equipment_id', en: 'Equipment reference', tr: 'Ekipman referansı' }, { key: 'job_id', en: 'Job reference', tr: 'İş referansı' },
    { key: 'assigned_to', en: 'Assigned person (source conflict)', tr: 'Atanan kişi (kaynak çelişkisi)' }, { key: 'executed_by', en: 'Executed by reference', tr: 'Uygulayan referansı' },
  ] },
  { en: 'Declared dates', tr: 'Beyan edilen tarihler', fields: [
    { key: 'planned_start', en: 'Planned start', tr: 'Planlanan başlangıç' }, { key: 'due_date', en: 'Due date', tr: 'Son tarih' }, { key: 'planned_finish', en: 'Planned finish', tr: 'Planlanan bitiş' },
    { key: 'actual_start', en: 'Declared actual start', tr: 'Beyan edilen fiilî başlangıç' }, { key: 'actual_finish', en: 'Declared actual finish', tr: 'Beyan edilen fiilî bitiş' },
  ] },
  { en: 'Execution declarations', tr: 'Uygulama beyanları', fields: [
    { key: 'meter_before', en: 'Meter before (unit unresolved)', tr: 'Önceki sayaç (birim belirsiz)' }, { key: 'meter_after', en: 'Meter after (unit unresolved)', tr: 'Sonraki sayaç (birim belirsiz)' },
    { key: 'findings', en: 'Findings', tr: 'Bulgular' }, { key: 'work_done', en: 'Work done declaration', tr: 'Yapılan iş beyanı' }, { key: 'spares_used', en: 'Spares used declaration', tr: 'Kullanılan yedek beyanı' }, { key: 'measurements', en: 'Measurements declaration', tr: 'Ölçüm beyanı' },
  ] },
  { en: 'Evidence and review declarations', tr: 'Kanıt ve inceleme beyanları', fields: [
    { key: 'photo_evidence', en: 'Photo evidence reference', tr: 'Fotoğraf kanıt referansı' }, { key: 'document_evidence', en: 'Document evidence reference', tr: 'Belge kanıt referansı' },
    { key: 'verification_result', en: 'Verification result declaration', tr: 'Doğrulama sonucu beyanı' }, { key: 'verified_by', en: 'Verifier reference', tr: 'Doğrulayan referansı' },
  ] },
] as const
export type WorkOrderFieldKey = typeof workOrderGroups[number]['fields'][number]['key']
export type WorkOrderFact = { state: 'Known' | 'Missing' | 'Unknown'; value: string | null }
export type WorkOrderFields = Record<WorkOrderFieldKey, WorkOrderFact>
export type WorkOrderFieldDefinition = { key: WorkOrderFieldKey; type: 'Text' | 'Narrative' | 'Date' | 'Number' | 'Code' | 'Reference' | 'SourceConflict'; required: boolean; options: string[]; sourceLocator: string; requiredWhen: 'SOURCE_CONDITIONAL_UNRESOLVED' | null; knownUnavailable: boolean }
export const workOrderSourceVersion = '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:55;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:1426-1463:v46.3'
export function workOrderFieldInfo(): { key: WorkOrderFieldKey; en: string; tr: string }[] { const fields: { key: WorkOrderFieldKey; en: string; tr: string }[] = []; for (const group of workOrderGroups) for (const field of group.fields) fields.push(field); return fields }
export const emptyWorkOrderFields = (): WorkOrderFields => Object.fromEntries(workOrderFieldInfo().map(field => [field.key, { state: 'Missing', value: null }])) as WorkOrderFields
const codeEn: Record<string,string> = { RELEASED: 'Released phase declaration', IN_PROGRESS: 'In progress phase declaration', EVIDENCE_SUBMITTED: 'Evidence submitted phase declaration' }, codeTr: Record<string,string> = { RELEASED: 'Serbest bırakma aşaması beyanı', IN_PROGRESS: 'Devam eden aşama beyanı', EVIDENCE_SUBMITTED: 'Kanıt sunma aşaması beyanı' }
export const workOrderCodeLabel = (value: string, en: boolean) => (en ? codeEn : codeTr)[value] ?? value
export const workOrderFactLabel = (state: WorkOrderFact['state'], en: boolean) => state === 'Known' ? (en ? 'Declared value' : 'Beyan edilen değer') : state === 'Missing' ? (en ? 'Missing' : 'Eksik') : (en ? 'Unknown' : 'Bilinmiyor')

export function workOrderSchema(): WorkOrderFieldDefinition[] {
  const f = (key: WorkOrderFieldKey, type: WorkOrderFieldDefinition['type'], row: number, required = false, knownUnavailable = false): WorkOrderFieldDefinition => ({ key, type, required, options: [], sourceLocator: '14.02/sheet:Data Dictionary 2316/row:' + row, requiredWhen: required ? null : 'SOURCE_CONDITIONAL_UNRESOLVED', knownUnavailable })
  return [f('work_order_no','Text',1427,true), f('equipment_id','Reference',1428,true,true), f('job_id','Reference',1429,false,true), f('work_order_type','Code',1430,false,true), f('priority','Code',1431,false,true), f('criticality','Code',1432,false,true), f('planned_start','Date',1433), f('due_date','Date',1434), f('assigned_to','SourceConflict',1435,false,true), f('description','Narrative',1444), f('planned_finish','Date',1448), f('actual_start','Date',1449), f('actual_finish','Date',1450), f('executed_by','Reference',1452,false,true), f('meter_before','Number',1453), f('meter_after','Number',1454), f('findings','Narrative',1455), f('work_done','Narrative',1456), f('spares_used','Text',1457), f('measurements','Text',1458), f('photo_evidence','Reference',1459,false,true), f('document_evidence','Reference',1460,false,true), f('verification_result','Text',1461), f('verified_by','Reference',1462,false,true), { key:'execution_stage',type:'Code',required:false,options:['RELEASED','IN_PROGRESS','EVIDENCE_SUBMITTED'],sourceLocator:'18.01/sheet:Backlog/row:55',requiredWhen:null,knownUnavailable:false }]
}
