// DEV-102 / T-OPS2-006. Source declarations retain uncertainty and never settle hire claims.
export const downtimeGroups = [
 { en:'Interruption and cause',tr:'Kesinti ve neden',fields:[{key:'downtime_type',en:'Downtime type',tr:'Kesinti türü'},{key:'cause',en:'Declared cause',tr:'Beyan edilen neden'},{key:'operational_impact',en:'Declared operational impact',tr:'Beyan edilen operasyon etkisi'}]},
 { en:'Recorded time and declared duration',tr:'Kayıt zamanı ve beyan edilen süre',fields:[{key:'start_at',en:'Interruption start',tr:'Kesinti başlangıcı'},{key:'end_at',en:'Interruption end',tr:'Kesinti bitişi'},{key:'duration_minutes',en:'Declared duration (minutes)',tr:'Beyan edilen süre (dakika)'}]},
 { en:'Unresolved owning references',tr:'Çözümlenmemiş sahip referansları',fields:[{key:'voyage_id',en:'Voyage reference',tr:'Sefer referansı'},{key:'linked_defect',en:'Defect reference',tr:'Arıza referansı'},{key:'linked_work_order',en:'Work order reference',tr:'İş emri referansı'},{key:'linked_event',en:'Event reference',tr:'Olay referansı'},{key:'claim_evidence',en:'Claim evidence reference',tr:'Talep kanıt referansı'}]},
] as const
export type DowntimeFieldKey = typeof downtimeGroups[number]['fields'][number]['key']
export type DowntimeFact = { state:'Known'|'Missing'|'Unknown';value:string|null }
export type DowntimeFields = Record<DowntimeFieldKey,DowntimeFact>
export type DowntimeRecordFields = DowntimeFields & Record<'vessel_id'|'loss_of_hire_flag'|'closure_state',DowntimeFact>
export type DowntimeFieldDefinition = {key:DowntimeFieldKey|'vessel_id'|'loss_of_hire_flag'|'closure_state';type:'Text'|'Narrative'|'Instant'|'Number'|'Code'|'Reference';required:boolean;options:string[];readOnly:boolean;referenceUnavailable:boolean;sourceRow:number;unit:string|null;requiredWhen:'SOURCE_CONDITIONAL_UNRESOLVED'|null}
export const downtimeSourceVersion = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2226-2239:v46.3'
export const downtimeTypeOptions=['Technical Failure','Planned Maintenance','Weather','Port Delay','Crew','Regulatory','Commercial','Other']
export function downtimeFieldInfo():{key:DowntimeFieldKey;en:string;tr:string}[]{const result:{key:DowntimeFieldKey;en:string;tr:string}[]=[];for(const group of downtimeGroups)for(const field of group.fields)result.push(field);return result}
export function emptyDowntimeFields():DowntimeFields{return Object.fromEntries(downtimeFieldInfo().map(field=>[field.key,{state:'Missing',value:null}])) as DowntimeFields}
export const downtimeFactLabel=(state:DowntimeFact['state'],en:boolean)=>state==='Known'?(en?'Declared value':'Beyan edilen değer'):state==='Missing'?(en?'Missing':'Eksik'):(en?'Unknown':'Bilinmiyor')
const labels:Record<string,string>={'Technical Failure':'Teknik arıza','Planned Maintenance':'Planlı bakım','Weather':'Hava durumu','Port Delay':'Liman gecikmesi','Crew':'Mürettebat','Regulatory':'Mevzuat','Commercial':'Ticari','Other':'Diğer'}
export const downtimeCodeLabel=(value:string,en:boolean)=>en?value:labels[value]??value
export function downtimeSchema():DowntimeFieldDefinition[]{
 const f=(key:DowntimeFieldDefinition['key'],type:DowntimeFieldDefinition['type'],sourceRow:number,required=false,readOnly=false):DowntimeFieldDefinition=>({key,type,sourceRow,required,readOnly,options:key==='downtime_type'?[...downtimeTypeOptions]:key==='loss_of_hire_flag'?['YES','NO','REVIEW_REQUIRED']:key==='closure_state'?['OPEN','VERIFIED','CLOSED']:[],referenceUnavailable:type==='Reference'&&!readOnly,unit:key==='duration_minutes'?'min':null,requiredWhen:required?null:'SOURCE_CONDITIONAL_UNRESOLVED'})
 return [f('vessel_id','Reference',2226,true,true),f('voyage_id','Reference',2227),f('downtime_type','Code',2228,true),f('start_at','Instant',2229,true),f('end_at','Instant',2230),f('duration_minutes','Number',2231),f('cause','Narrative',2232,true),f('linked_defect','Reference',2233),f('linked_work_order','Reference',2234),f('linked_event','Reference',2235),f('operational_impact','Narrative',2236,true),f('loss_of_hire_flag','Code',2237,false,true),f('claim_evidence','Reference',2238),f('closure_state','Code',2239,true,true)]
}
export const downtimeMutableSchema=()=>downtimeSchema().filter(field=>!field.readOnly)
