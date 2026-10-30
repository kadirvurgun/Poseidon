import {imoMessage} from "./imo";
import {EvidenceIntake} from "./EvidenceIntake";
import {useEffect,useRef,useState} from "react";
import {Api,ApiError,newRequestKey,type InventoryVessel,type InventoryBrowseOptions,type InventoryData,type InventoryHistoryItem} from "./api";
import {inventoryFields,inventoryActions} from "./inventoryFields";
import {Button} from "./components/ui/button";
type Props={api:Api;lang:"tr"|"en";expired:boolean;parentBusy?:boolean;onFailure:(e:unknown)=>void;onDirty:(dirty:boolean)=>void;onBusy:(busy:boolean)=>void;onUncertain?:(uncertain:boolean)=>void};
export function Inventory({api,lang,expired,parentBusy=false,onFailure,onDirty,onBusy,onUncertain}:Props) {
 const t=(tr:string,en:string)=>lang==="tr"?tr:en;
 const [rows,setRows]=useState<InventoryVessel[]>([]),[next,setNext]=useState<string|null>(null),[filter,setFilter]=useState<InventoryBrowseOptions>({});
 const [query,setQuery]=useState<InventoryBrowseOptions>({});
 const [record,setRecord]=useState<InventoryVessel>(),[editing,setEditing]=useState(false),[data,setData]=useState<Record<string,string>>({}),[baseline,setBaseline]=useState("{}");
 const [caps,setCaps]=useState({canWriteDraft:false,canManage:false}),[loading,setLoading]=useState(false),[ownBusy,setBusy]=useState(false),[error,setError]=useState<ApiError|null>(null),[notice,setNotice]=useState(false);
 const [history,setHistory]=useState<InventoryHistoryItem[]>([]),[historyNext,setHistoryNext]=useState<string|null>(null),[reason,setReason]=useState("");
 type Operation={action:"create"|"metadata"|"record"|"reopen";body:object;key:string;record?:InventoryVessel;confirmedId?:string;uncertain?:boolean};
 const [pending,setPending]=useState<Operation|null>(null);
 const [showImoError,setShowImoError]=useState(false);
 const imoError=imoMessage(data.imo_no,lang);
 const seq=useRef(0),historySeq=useRef(0),abort=useRef<AbortController|null>(null),lock=useRef(false),alive=useRef(true);
 const [evidenceBusy,setEvidenceBusy]=useState(false),[evidenceDirty,setEvidenceDirty]=useState(false),[evidenceUncertain,setEvidenceUncertain]=useState(false);
 const busy=ownBusy||evidenceBusy;
 const dirty=evidenceDirty||JSON.stringify(data)!==baseline || reason.trim()!=="" || !!pending;
 useEffect(()=>{onDirty(dirty);},[dirty,onDirty]);
 useEffect(()=>{onBusy(busy);},[busy,onBusy]);
 useEffect(()=>{onUncertain?.(!!pending?.uncertain||evidenceUncertain);},[pending?.uncertain,evidenceUncertain,onUncertain]);
 useEffect(()=>()=>{alive.current=false;seq.current++;historySeq.current++;abort.current?.abort();onDirty(false);onBusy(false);onUncertain?.(false);},[onDirty,onBusy,onUncertain]);
 const failure=(e:unknown)=>{if(!alive.current)return;const fault=e instanceof ApiError?e:new ApiError(0,"UNCONFIRMED","Operation not confirmed.");setError(fault);onFailure(fault);};
 useEffect(()=>{alive.current=true;void api.inventoryCapabilities().then(setCaps).catch(failure);},[api]);
 useEffect(()=>{if(expired){seq.current++;historySeq.current++;abort.current?.abort();setRows([]);setHistory([]);setNext(null);setHistoryNext(null);setPending(operation=>operation&&!operation.confirmedId?{...operation,uncertain:true}:operation);setNotice(false);}},[expired]);
 const canLeave=()=>!dirty||window.confirm((pending?.uncertain||evidenceUncertain)?t("İşlemin sonucu bilinmiyor; kayıt sunucuda oluşmuş olabilir. Ayrılırsanız aynı anahtarla tekrar deneme bilgisi kaybolur. Devam edilsin mi?","The outcome is unknown; the record may already exist on the server. Leaving discards the original retry key. Continue?"):t("Kaydedilmemiş değişiklikler bırakılacak. Devam edilsin mi?","Unsaved changes will be discarded. Continue?"));
 async function list(options:InventoryBrowseOptions=query) {
  if(expired)return;const n=++seq.current;abort.current?.abort();const control=new AbortController();abort.current=control;setRows([]);setNext(null);setLoading(true);setError(null);
  try {const page=await api.inventoryList(options,control.signal);if(n===seq.current&&!control.signal.aborted){setRows(page.items);setNext(page.nextCursor);}}
  catch(e){if(n===seq.current&&!control.signal.aborted)failure(e);}finally{if(n===seq.current)setLoading(false);}
 }
 useEffect(()=>{if(!expired)void list();return()=>{seq.current++;abort.current?.abort();};},[api,query,expired]);
 function accept(item:InventoryVessel) {setShowImoError(item.completeness.invalidFields.includes("imo_no"));setRecord(item);const values=Object.fromEntries(Object.entries(item.data).map(([k,v])=>[k,String(v)]));setData(values);setBaseline(JSON.stringify(values));setEditing(true);setReason("");setPending(null);}
 async function historyPage(id:string,cursor?:string) {const n=++historySeq.current;try{const page=await api.inventoryHistory(id,cursor);if(n===historySeq.current&&alive.current){setHistory(old=>cursor?[...old,...page.items]:page.items);setHistoryNext(page.nextCursor);}}catch(e){if(n===historySeq.current)failure(e);}}
 async function open(id:string) {
  if(busy||parentBusy||expired||!canLeave())return;const n=++seq.current;historySeq.current++;setHistory([]);setHistoryNext(null);setBusy(true);setError(null);setNotice(false);
  try {const item=await api.inventoryGet(id);if(n!==seq.current)return;accept(item);await historyPage(id);}catch(e){if(n===seq.current)failure(e);}finally{setBusy(false);}
 }
 function create(){if(busy||parentBusy||expired||!canLeave())return;seq.current++;historySeq.current++;setEditing(true);setRecord(undefined);setShowImoError(false);setData({});setBaseline("{}");setHistory([]);setHistoryNext(null);setReason("");setPending(null);setError(null);setNotice(false);}
 function back(){if(busy||parentBusy||expired||!canLeave())return;setEditing(false);setData({});setBaseline("{}");setReason("");setPending(null);historySeq.current++;setHistory([]);setNotice(false);void list();}
 function metadata():InventoryData {
  setShowImoError(true);
  if(imoError)throw new ApiError(422,"invalid_imo",imoError);
  const result:InventoryData={};
  for(const field of inventoryFields){const value=data[field.key]?.trim();if(!value)continue;
   if(field.type==="number"){
    // Decimal(12,3): reject precision before JavaScript conversion can round it.
    if(!/^\d{1,9}(?:\.\d{1,3})?$/.test(value))throw new ApiError(422,"numeric_precision",t("Sayı: en fazla 9 tam ve 3 ondalık basamak; negatif olamaz.","Number: at most 9 integer and 3 decimal digits; nonnegative."));
    result[field.key]=Number(value);
   }else result[field.key]=value;
  }return result;
 }
 async function write(action:Operation["action"],retry=false) {
  if(expired||parentBusy||evidenceBusy||lock.current)return;
  let operation:Operation;
  try {operation=retry&&pending?pending:{action,body:action==="record"?{}:action==="reopen"?{reason:reason.trim()}:{data:metadata()},key:newRequestKey(),record};}catch(e){failure(e);return;}
  const n=++seq.current;let writeConfirmed=!!operation.confirmedId;
  lock.current=true;setBusy(true);setPending(operation);setError(null);setNotice(false);
  try {
   if(!operation.confirmedId) {
    const saved=await api.inventoryWrite(operation.action,operation.body,operation.key,operation.record);writeConfirmed=true;
    if(n!==seq.current||!alive.current)return;
    operation={...operation,confirmedId:saved.id,uncertain:false};setPending(operation);
   }
   // Replay may return an older version; read current full metadata before further replacement.
   const latest=await api.inventoryGet(operation.confirmedId!);if(n!==seq.current||!alive.current)return;accept(latest);setNotice(true);await historyPage(latest.id);
  }catch(e){if(n!==seq.current)return;failure(e);if(!writeConfirmed) {
    // A later rejection says nothing about an earlier request whose response was lost.
    if(operation.uncertain || !(e instanceof ApiError) || e.status===0 || e.status===401 || e.status>=500)setPending({...operation,uncertain:true});
    else setPending(null);
   }}
  finally{lock.current=false;setBusy(false);}
 }
 const label=(key:string)=>key==="adminState"?t("İdari durum","Administrative state"):key==="company_id"?t("Yetkili şirket","Authorized company"):inventoryFields.find(f=>f.key===key)?.[lang]??t("Geçmiş metadata değişikliği","Historical metadata change");
 const state=(value:string)=>value==="RECORDED"?t("Envantere alındı","Recorded in inventory"):t("Taslak","Draft");
 const writable=record?.adminState==="RECORDED"?caps.canManage:caps.canWriteDraft;
 return <section className="inventory card">
  <h1>{t("İdari gemi / römorkör envanteri","Administrative vessel / tug inventory")}</h1>
  <p role="note">{t("Yalnız idari envanter kaydıdır; operasyonel veya mevzuat onayı değildir.","Administrative inventory record only; no operational or regulatory approval.")}</p>
  {error&&<div role="alert" className="error-panel"><div><strong>{t("İşlem doğrulanmadı","Operation not confirmed")}</strong><p>{error.status===412?t("Kayıt değişti. Yeniden yüklemeden üzerine yazılmaz.","The record changed. Reload before replacing metadata."):error.status===403?t("Bu işlem için yetkiniz yok.","You do not have this action capability."):error.status===401?t("Oturum sona erdi; otomatik tekrar gönderim yok.","Session ended; nothing is retried automatically."):t("Kaydetme / geçiş tamamlanmadı. Alanları, bağlantıyı ve güncel sürümü kontrol edin.","Save / transition was not confirmed. Check fields, connection and current version.")}</p><code>{error.code}</code>{error.code==="numeric_precision"&&<p>{error.message}</p>}</div></div>}
  {notice&&<p role="status">{t("Sunucudaki güncel kayıt doğrulandı.","Current server record confirmed.")}</p>}
  {expired&&<p>{t("Oturum sona erdi; düzenlemeler donduruldu.","Session ended; edits are frozen.")}</p>}
  {!editing?<>
   <Button onClick={create} disabled={parentBusy||busy||expired||!caps.canWriteDraft}>{t("Yeni envanter taslağı","New inventory draft")}</Button>
   <form className="inventory-filters" onSubmit={e=>{e.preventDefault();setQuery({...filter});}}>
    {([['q','Ad / IMO / resmî numara ara','Search name / IMO / official number'],['flag','Bayrak filtresi','Flag filter'],['vessel_type','Alt tip filtresi','Subtype filter']] as const).map(([key,tr,en])=><label key={key}>{t(tr,en)}<input maxLength={key==='q'?100:200} value={filter[key]??""} onChange={e=>setFilter({...filter,[key]:e.target.value})}/></label>)}
    <label>{t("İdari durum filtresi","Administrative state filter")}<select value={filter.adminState??""} onChange={e=>setFilter({...filter,adminState:e.target.value})}><option value="">{t("Tümü","All")}</option><option value="DRAFT">{state("DRAFT")}</option><option value="RECORDED">{state("RECORDED")}</option></select></label>
    <label>{t("Varlık türü filtresi","Asset mode filter")}<select value={filter.asset_mode??""} onChange={e=>setFilter({...filter,asset_mode:e.target.value})}><option value="">{t("Tümü","All")}</option><option value="SHIP">{t("Gemi","Ship")}</option><option value="TUG">{t("Römorkör","Tug")}</option></select></label>
    <Button type="submit" disabled={parentBusy||busy||expired}>{t("Envanterde ara","Search inventory")}</Button><Button type="button" disabled={parentBusy||expired||busy} onClick={()=>{setFilter({});setQuery({});}}>{t("Filtreleri temizle","Clear filters")}</Button>
   </form>
   {loading?<p role="status">{t("Yükleniyor…","Loading…")}</p>:<><p>{rows.length} {t("kayıt bu sayfada","records on this page")}</p><ul className="inventory-list">{rows.map(row=><li key={row.id}><span>{row.data.name||t("Adsız taslak","Unnamed draft")} · {state(row.adminState)}</span><Button disabled={parentBusy||busy||expired} onClick={()=>void open(row.id)}>{t("Aç","Open")}</Button></li>)}</ul></>}
   <div className="page-actions"><Button disabled={parentBusy||busy||expired||loading} onClick={()=>void list()}>{t("İlk sayfa / yenile","First page / refresh")}</Button>{next&&<Button disabled={parentBusy||busy||expired||loading} onClick={()=>void list({...query,cursor:next})}>{t("Sonraki sayfa","Next page")}</Button>}</div>
  </>:<>
   <div className="page-actions"><Button onClick={back} disabled={parentBusy||busy||expired}>{t("Envanter listesine dön","Back to inventory")}</Button>{record&&<Button disabled={parentBusy||busy||expired} onClick={()=>void open(record.id)}>{t("Sunucudan yeniden yükle","Reload from server")}</Button>}</div>
   <h2>{record?state(record.adminState):t("Yeni taslak","New draft")}</h2>
   {record&&<><p className="inventory-id">ID: {record.id} · {t("Sürüm","Version")}: {record.version}</p><p>{t("Oluşturulma / son değişiklik","Created / last modified")}: {[record.createdAt,record.modifiedAt].map(v=>v?new Date(v).toLocaleString(lang==='tr'?'tr-TR':'en-GB'):t("Bilinmiyor","Unknown")).join(' / ')}</p><p>{t("Eksik idari alanlar","Missing administrative fields")}: {record.completeness.missingRequiredFields.map(label).join(', ')||t("Yok","None")}</p></>}
   <p>{t("Geçersiz idari alanlar","Invalid administrative fields")}: {record?.completeness.invalidFields.map(label).join(', ')||t("Yok","None")}</p>
   <form onSubmit={e=>{e.preventDefault();void write(record?'metadata':'create');}}>
    <fieldset disabled={parentBusy||busy||expired||!!pending||!writable}><legend>{t("Beyan edilen metadata","Declared metadata")}</legend><div className="inventory-fields">
     {inventoryFields.map(f=><label key={f.key} htmlFor={'inv-'+f.key}>{f[lang]}{f.key==='asset_mode'?<select id={'inv-'+f.key} value={data[f.key]??""} disabled={record?.adminState==='RECORDED'} onChange={e=>setData({...data,[f.key]:e.target.value})}><option value="">{t("Seçilmedi","Not selected")}</option><option value="SHIP">{t("Gemi","Ship")}</option><option value="TUG">{t("Römorkör","Tug")}</option></select>:f.key==='notes'?<textarea id={'inv-'+f.key} maxLength={4000} value={data[f.key]??""} onChange={e=>setData({...data,[f.key]:e.target.value})}/>:<input id={'inv-'+f.key} type={f.type==='number'?'text':f.type??'text'} aria-label={f.key==='imo_no'?f[lang]:undefined} aria-invalid={f.key==='imo_no'&&showImoError&&!!imoError||undefined} aria-describedby={f.key==='imo_no'&&showImoError&&imoError?'inv-imo-error':undefined} inputMode={f.type==='number'?'decimal':undefined} maxLength={200} value={data[f.key]??""} onChange={e=>setData({...data,[f.key]:e.target.value})}/>}{f.key==='imo_no'&&showImoError&&imoError&&<span id="inv-imo-error" role="alert">{imoError}</span>}</label>)}
    </div></fieldset>
    <Button type="submit" disabled={parentBusy||busy||expired||!!pending||!writable}>{t("Metadata kaydet","Save metadata")}</Button>
   </form>
   {pending?.uncertain&&<p role="alert">{t("Sonuç bilinmiyor; önceki istek kaydedilmiş olabilir. Aynı anahtar korunur; yeni bir kayıt isteği başlatılmaz.","Outcome unknown; the earlier request may have committed. The original key is retained; no new create request is started.")}</p>}
  {pending?.confirmedId&&<p>{t("Yazma onaylandı; güncel kayıt okunmayı bekliyor.","Write confirmed; the current record still needs to be read.")}</p>}
  {pending&&<Button disabled={parentBusy||busy||expired} onClick={()=>void write(pending.action,true)}>{pending.confirmedId?t("Güncel kaydı yeniden oku","Read current record again"):t("Aynı işlemi tekrar dene","Retry same operation")}</Button>}
   {record&&<section><h3>{t("İdari kayıt işlemleri","Administrative commands")}</h3>{record.adminState==='DRAFT'?<Button disabled={parentBusy||busy||expired||dirty||!caps.canManage||record.completeness.missingRequiredFields.length>0||record.completeness.invalidFields.length>0} onClick={()=>void write('record')}>{t("Envantere al","Record in inventory")}</Button>:<><label htmlFor="reopen-reason">{t("Yeniden açma gerekçesi (kısıtlı denetim kaydı)","Reopen reason (restricted audit record)")}</label><textarea id="reopen-reason" maxLength={500} value={reason} disabled={parentBusy||!caps.canManage||busy||expired||!!pending} onChange={e=>setReason(e.target.value)}/><Button disabled={parentBusy||busy||expired||!!pending||!caps.canManage||!reason.trim()||JSON.stringify(data)!==baseline} onClick={()=>void write('reopen')}>{t("Düzeltme için yeniden aç","Reopen for correction")}</Button></>}</section>}
   <section><h3>{t("Ayrı alanların sınırı","Separate domain boundaries")}</h3><ul><li>{t("Sorumlu rol: atanmadı; iletişim adları yetki vermez.","Responsible role: not assigned; contact names grant no authority.")}</li><li>{t("Gereksinim bağlantısı: değerlendirilmedi.","Requirement link: not evaluated.")}</li><li>{t("Kanıt doğrulaması: uygulanmadı; referans yalnız metindir.","Evidence verification: not implemented; reference is text only.")}</li><li>{t("Doğrulayan: atanmadı.","Verifier: not assigned.")}</li><li>{t("Personel uygunluğu: değerlendirilmedi.","Crew eligibility: not evaluated.")}</li><li>{t("Operasyonel hazırlık: değerlendirilmedi.","Operational readiness: not evaluated.")}</li></ul></section>
   {record&&import.meta.env.VITE_ENABLE_EVIDENCE_INTAKE==="true"&&<EvidenceIntake key={record.id} api={api} targetType="Vessel" targetId={record.id} lang={lang} disabled={parentBusy||ownBusy||!!pending} expired={expired} onFailure={onFailure} onBusy={setEvidenceBusy} onDirty={setEvidenceDirty} onUncertain={setEvidenceUncertain}/>}
   {record&&<section><h3>{t("Değişiklik geçmişi","Change history")}</h3><ol>{history.map(event=><li key={event.eventId}><time>{new Date(event.occurredAt).toLocaleString(lang==='tr'?'tr-TR':'en-GB')}</time> · {t("Sürüm","Version")} {event.version} · {(inventoryActions[event.action]??inventoryActions.HISTORICAL_METADATA_CHANGE)[lang==='tr'?0:1]} · {event.actorRelation==='SELF'?t("Siz","You"):t("Başka kullanıcı","Other user")}<p>{event.changedFields.map(label).join(', ')||t("Geçmiş metadata değişikliği","Historical metadata change")}</p></li>)}</ol>{historyNext&&<Button disabled={parentBusy||busy||expired} onClick={()=>void historyPage(record.id,historyNext)}>{t("Önceki değişiklikler","Earlier changes")}</Button>}</section>}
  </>}
 </section>;
}
