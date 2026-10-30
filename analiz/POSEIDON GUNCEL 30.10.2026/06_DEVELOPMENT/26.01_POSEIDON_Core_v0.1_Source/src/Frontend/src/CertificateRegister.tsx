import {EvidenceIntake} from "./EvidenceIntake";
import {useEffect,useRef,useState} from "react";
import {Api,ApiError,newRequestKey,type CertificateRecord,type CertificateBrowseOptions,type DraftData,type InventoryHistoryItem} from "./api";
import {certificateFields,certificateActions,dateBucketLabels,validateCertificate} from "./certificateFields";
import {CertificateVesselPicker} from "./CertificateVesselPicker";
import {dateBuckets} from "./api";
import {Button} from "./components/ui/button";
type Props={api:Api;lang:"tr"|"en";expired:boolean;parentBusy?:boolean;onFailure:(e:unknown)=>void;onDirty:(dirty:boolean)=>void;onBusy:(busy:boolean)=>void;onUncertain?:(uncertain:boolean)=>void};
export function CertificateRegister({api,lang,expired,parentBusy=false,onFailure,onDirty,onBusy,onUncertain}:Props) {
 const t=(tr:string,en:string)=>lang==="tr"?tr:en;
 const [rows,setRows]=useState<CertificateRecord[]>([]),[next,setNext]=useState<string|null>(null),[filter,setFilter]=useState<CertificateBrowseOptions>({});
 const [query,setQuery]=useState<CertificateBrowseOptions>({});
 const [asOfDate,setAsOfDate]=useState<string|null>(null),[validationData,setValidationData]=useState<Record<string,string>|null>(null);
 const fieldErrors=validationData?validateCertificate(validationData,lang):{};
 const [record,setRecord]=useState<CertificateRecord>(),[editing,setEditing]=useState(false),[data,setData]=useState<Record<string,string>>({}),[baseline,setBaseline]=useState("{}");
 const [caps,setCaps]=useState({canWriteDraft:false,canManage:false}),[loading,setLoading]=useState(false),[ownBusy,setBusy]=useState(false),[error,setError]=useState<ApiError|null>(null),[notice,setNotice]=useState(false);
 const [history,setHistory]=useState<InventoryHistoryItem[]>([]),[historyNext,setHistoryNext]=useState<string|null>(null),[reason,setReason]=useState("");
 type Operation={action:"create"|"metadata"|"record"|"reopen";body:object;key:string;record?:CertificateRecord;confirmedId?:string;uncertain?:boolean};
 const [pending,setPending]=useState<Operation|null>(null);
 const seq=useRef(0),historySeq=useRef(0),abort=useRef<AbortController|null>(null),lock=useRef(false),alive=useRef(true);
 const [evidenceBusy,setEvidenceBusy]=useState(false),[evidenceDirty,setEvidenceDirty]=useState(false),[evidenceUncertain,setEvidenceUncertain]=useState(false);
 const busy=ownBusy||evidenceBusy;
 const dirty=evidenceDirty||JSON.stringify(data)!==baseline || reason.trim()!=="" || !!pending;
 useEffect(()=>{onDirty(dirty);},[dirty,onDirty]);
 useEffect(()=>{onBusy(busy);},[busy,onBusy]);
 useEffect(()=>{onUncertain?.(!!pending?.uncertain||evidenceUncertain);},[pending?.uncertain,evidenceUncertain,onUncertain]);
 useEffect(()=>()=>{alive.current=false;seq.current++;historySeq.current++;abort.current?.abort();onDirty(false);onBusy(false);onUncertain?.(false);},[onDirty,onBusy,onUncertain]);
 const failure=(e:unknown)=>{if(!alive.current)return;const fault=e instanceof ApiError?e:new ApiError(0,"UNCONFIRMED","Operation not confirmed.");setError(fault);onFailure(fault);};
 useEffect(()=>{alive.current=true;void api.certificateCapabilities().then(setCaps).catch(failure);},[api]);
 useEffect(()=>{if(expired){seq.current++;historySeq.current++;abort.current?.abort();setRows([]);setAsOfDate(null);setHistory([]);setNext(null);setHistoryNext(null);setPending(operation=>operation&&!operation.confirmedId?{...operation,uncertain:true}:operation);setNotice(false);}},[expired]);
 const canLeave=()=>!dirty||window.confirm((pending?.uncertain||evidenceUncertain)?t("İşlemin sonucu bilinmiyor; kayıt sunucuda oluşmuş olabilir. Ayrılırsanız aynı anahtarla tekrar deneme bilgisi kaybolur. Devam edilsin mi?","The outcome is unknown; the record may already exist on the server. Leaving discards the original retry key. Continue?"):t("Kaydedilmemiş değişiklikler bırakılacak. Devam edilsin mi?","Unsaved changes will be discarded. Continue?"));
 async function list(options:CertificateBrowseOptions=query) {
  if(expired)return;const n=++seq.current;abort.current?.abort();const control=new AbortController();abort.current=control;setRows([]);setNext(null);setLoading(true);setError(null);
  try {const page=await api.certificateList(options,control.signal);if(n===seq.current&&!control.signal.aborted){setRows(page.items);setNext(page.nextCursor);setAsOfDate(page.asOfDate);}}
  catch(e){if(n===seq.current&&!control.signal.aborted)failure(e);}finally{if(n===seq.current)setLoading(false);}
 }
 useEffect(()=>{if(!expired)void list();return()=>{seq.current++;abort.current?.abort();};},[api,query,expired]);
 function accept(item:CertificateRecord) {setRecord(item);const values=Object.fromEntries(Object.entries(item.data).map(([k,v])=>[k,String(v)]));setData(values);setBaseline(JSON.stringify(values));setEditing(true);setReason("");setPending(null);setValidationData(null);}
 async function historyPage(id:string,cursor?:string) {const n=++historySeq.current;try{const page=await api.certificateHistory(id,cursor);if(n===historySeq.current&&alive.current){setHistory(old=>cursor?[...old,...page.items]:page.items);setHistoryNext(page.nextCursor);}}catch(e){if(n===historySeq.current)failure(e);}}
 async function open(id:string) {
  if(busy||parentBusy||expired||!canLeave())return;const n=++seq.current;historySeq.current++;setHistory([]);setHistoryNext(null);setBusy(true);setError(null);setNotice(false);
  try {const item=await api.certificateGet(id);if(n!==seq.current)return;accept(item);await historyPage(id);}catch(e){if(n===seq.current)failure(e);}finally{setBusy(false);}
 }
 function create(){if(busy||parentBusy||expired||!canLeave())return;seq.current++;historySeq.current++;setEditing(true);setRecord(undefined);setData({});setBaseline("{}");setValidationData(null);setHistory([]);setHistoryNext(null);setReason("");setPending(null);setError(null);setNotice(false);}
 function back(){if(busy||parentBusy||expired||!canLeave())return;setEditing(false);setData({});setBaseline("{}");setReason("");setPending(null);historySeq.current++;setHistory([]);setNotice(false);setValidationData(null);void list();}
 function metadata():DraftData {
  return Object.fromEntries(Object.entries(data).filter(([,value])=>value.trim()!=="").map(([key,value])=>[key,value.trim()]));
 }
 async function write(action:Operation["action"],retry=false) {
  if(expired||parentBusy||evidenceBusy||lock.current)return;
  if(!retry && (action==='create'||action==='metadata')) {
   setValidationData(data);const invalid=validateCertificate(data,lang);
   if(Object.keys(invalid).length){document.getElementById('cert-'+Object.keys(invalid)[0])?.focus();return;}
   if(record?.adminState==='RECORDED'&&!reason.trim()){document.getElementById('cert-reason')?.focus();return;}
  }
  if(!retry&&action==='reopen'&&!reason.trim())return;
  let operation:Operation;
  try {operation=retry&&pending?pending:{action,body:action==="record"?{}:action==="reopen"?{reason:reason.trim()}:{data:metadata(),...(record?.adminState==='RECORDED'?{reason:reason.trim()}: {})},key:newRequestKey(),record};}catch(e){failure(e);return;}
  const n=++seq.current;let writeConfirmed=!!operation.confirmedId;
  lock.current=true;setBusy(true);setPending(operation);setError(null);setNotice(false);
  try {
   if(!operation.confirmedId) {
    const saved=await api.certificateWrite(operation.action,operation.body,operation.key,operation.record);writeConfirmed=true;
    if(n!==seq.current||!alive.current)return;
    operation={...operation,confirmedId:saved.id,uncertain:false};setPending(operation);
   }
   // Replay may return an older version; read current full metadata before further replacement.
   const latest=await api.certificateGet(operation.confirmedId!);if(n!==seq.current||!alive.current)return;accept(latest);setNotice(true);await historyPage(latest.id);
  }catch(e){if(n!==seq.current)return;failure(e);if(!writeConfirmed) {
    // A later rejection says nothing about an earlier request whose response was lost.
    if(operation.uncertain || !(e instanceof ApiError) || e.status===0 || e.status===401 || e.status>=500)setPending({...operation,uncertain:true});
    else setPending(null);
   }}
  finally{lock.current=false;setBusy(false);}
 }
 const label=(key:string)=>key==='adminState'?t('İdari durum','Administrative state'):key==='vessel_id'?t('Yetkili gemi','Authorized vessel'):certificateFields.find(f=>f.key===key)?.[lang]??t('Geçmiş metadata değişikliği','Historical metadata change');
 const state=(value:string)=>value==='RECORDED'?t('İdari kayda alındı','Administratively recorded'):t('Taslak','Draft');
 const writable=record?.adminState==='RECORDED'?caps.canManage:caps.canWriteDraft;
 const frozen=parentBusy||busy||expired;
 const tracking=(item:CertificateRecord)=><div className="certificate-date-context"><p>{dateBucketLabels[item.dateTracking.bucket][lang==='tr'?0:1]}</p><p>{t('Tarih bağlamı','Date context')}: <time>{item.dateTracking.asOfDate}</time> · UTC {t('takvim tarihi','calendar date')}</p>{item.dateTracking.daysToRecordedExpiry!==null&&<p>{t('Kayıtlı bitiş tarihine gün farkı','Days to recorded expiry')}: {item.dateTracking.daysToRecordedExpiry}</p>}{item.dateTracking.issueDateInFuture&&<p>{t('Kayıtlı düzenlenme tarihi gelecekte. Bu bir geçerlilik başlangıcı değerlendirmesi değildir.','Recorded issue date is in the future. This is not a valid-from assessment.')}</p>}</div>;
 return <section className="inventory card certificate-register">
  <h1>{t('İdari belge kayıtları ve tarih takibi','Administrative certificate records and date tracking')}</h1>
  <p role="note">{t('Tarih takibi yalnız girilen metadataya dayanır; özgünlük, hukuki geçerlilik ve operasyonel hazırlık değerlendirilmemiştir.','Date tracking from entered metadata only; authenticity, legal validity and operational readiness have not been assessed.')}</p>
  {error&&<div role="alert" className="error-panel"><div><strong>{t('İşlem doğrulanmadı','Operation not confirmed')}</strong><p>{error.status===412?t('Kayıt değişti. Yeniden yüklemeden üzerine yazılmaz.','The record changed. Reload before replacing metadata.'):error.status===403?t('Bu işlem için yetkiniz yok.','You do not have this action capability.'):error.status===401?t('Oturum sona erdi; otomatik tekrar gönderim yok.','Session ended; nothing is retried automatically.'):error.status===422?t('Alanları ve düzeltme gerekçesini kontrol edin. Tarih ve gemi değişikliği sınırları sunucuda denetlenir.','Check fields and the correction reason. Date and vessel-change constraints are enforced by the server.'):t('İşlem tamamlanmadı. Bağlantıyı, tarih filtresini ve güncel sürümü kontrol edin.','The operation was not completed. Check connection, date filters and current version.')}</p><code>{error.code}</code></div></div>}
  {notice&&<p role="status">{t('Sunucudaki güncel kayıt okundu.','Current server record loaded.')}</p>}
  {expired&&<p>{t('Oturum sona erdi; düzenlemeler donduruldu.','Session ended; edits are frozen.')}</p>}
  {!editing?<>
   <Button onClick={create} disabled={frozen||!caps.canWriteDraft}>{t('Yeni belge taslağı','New certificate draft')}</Button>
   <form className="inventory-filters" onSubmit={e=>{e.preventDefault();if(!frozen)setQuery({...filter});}}>
    <label>{t('Belge tipi / düzenleyen / numara ara','Search type / issuer / number')}<input maxLength={100} disabled={frozen} value={filter.q??''} onChange={e=>setFilter({...filter,q:e.target.value})}/></label>
    <label>{t('İdari durum filtresi','Administrative state filter')}<select value={filter.adminState??''} disabled={frozen} onChange={e=>setFilter({...filter,adminState:e.target.value})}><option value="">{t('Tümü','All')}</option><option value="DRAFT">{state('DRAFT')}</option><option value="RECORDED">{state('RECORDED')}</option></select></label>
    <label>{t('Kayıtlı bitiş tarihi filtresi','Recorded expiry date filter')}<select value={filter.dateBucket??''} disabled={frozen} onChange={e=>setFilter({...filter,dateBucket:e.target.value})}><option value="">{t('Tümü','All')}</option>{dateBuckets.map(bucket=><option key={bucket} value={bucket}>{dateBucketLabels[bucket][lang==='tr'?0:1]}</option>)}</select></label>
    <CertificateVesselPicker api={api} lang={lang} value={filter.vessel_id??''} disabled={frozen} all onChange={vessel_id=>setFilter({...filter,vessel_id})} onFailure={failure}/>
    <Button type="submit" disabled={frozen}>{t('Belge kayıtlarında ara','Search certificate records')}</Button><Button type="button" disabled={frozen} onClick={()=>{setFilter({});setQuery({});}}>{t('Filtreleri temizle','Clear filters')}</Button>
   </form>
   {asOfDate&&<p>{t('Bu sayfa serisinin tarih bağlamı','Date context for this page series')}: <time>{asOfDate}</time> · UTC. {t('Sonraki sayfada bu tarih korunur. Bugün için ilk sayfayı yenileyin; kayıtlar arada değişebilir.','Next pages retain this date. Refresh the first page for today; records may change meanwhile.')}</p>}
   {loading?<p role="status">{t('Yükleniyor…','Loading…')}</p>:<><p>{rows.length} {t('kayıt bu sayfada','records on this page')}</p>{rows.length===0&&<p>{t('Bu kapsam ve filtrelerde belge kaydı yok.','No certificate records in this scope and filter.')}</p>}<ul className="inventory-list">{rows.map(row=><li key={row.id}><div><strong>{row.data.type||t('Tipsiz taslak','Untyped draft')}</strong><p>{row.parentVessel.name||row.parentVessel.id} · {state(row.adminState)}</p><p>{dateBucketLabels[row.dateTracking.bucket][lang==='tr'?0:1]}</p></div><Button disabled={frozen} onClick={()=>void open(row.id)}>{t('Aç','Open')}</Button></li>)}</ul></>}
   <div className="page-actions"><Button disabled={frozen||loading} onClick={()=>void list()}>{t('Bugün için ilk sayfayı yenile','Refresh first page for today')}</Button>{next&&<Button disabled={frozen||loading} onClick={()=>void list({...query,cursor:next})}>{t('Sonraki sayfa','Next page')}</Button>}</div>
  </>:<>
   <div className="page-actions"><Button onClick={back} disabled={frozen}>{t('Belge listesine dön','Back to certificates')}</Button>{record&&<Button disabled={frozen} onClick={()=>void open(record.id)}>{t('Sunucudan yeniden yükle','Reload from server')}</Button>}</div>
   <h2>{record?state(record.adminState):t('Yeni belge taslağı','New certificate draft')}</h2>
   {record&&<><p className="certificate-id inventory-id">ID: {record.id} · {t('Sürüm','Version')}: {record.version}</p><p>{t('Eksik idari alanlar','Missing administrative fields')}: {record.completeness.missingRequiredFields.map(label).join(', ')||t('Yok','None')}</p>{record.completeness.invalidFields.length>0&&<p>{t('Geçersiz alanlar','Invalid fields')}: {record.completeness.invalidFields.map(label).join(', ')}</p>}
    <section><h3>{t('Güncel yetkili gemi projeksiyonu','Current authorized vessel projection')}</h3><p>{record.parentVessel.name||t('Adsız gemi','Unnamed vessel')} · {record.parentVessel.id}</p><p>{t('Geminin idari durumu','Vessel administrative state')}: {record.parentVessel.adminState==='RECORDED'?t('Envantere alındı','Recorded in inventory'):t('Taslak','Draft')}</p><p>{t('Beyan edilen IMO / bayrak','Declared IMO / flag')}: {record.parentVessel.imoNo??t('Girilmemiş','Not entered')} / {record.parentVessel.flag??t('Girilmemiş','Not entered')}</p></section>{tracking(record)}</>}
   <form onSubmit={e=>{e.preventDefault();void write(record?'metadata':'create');}}>
    <fieldset disabled={frozen||!!pending||!writable}><legend>{t('Beyan edilen belge metadatası','Declared certificate metadata')}</legend>
     {!record&&<CertificateVesselPicker api={api} lang={lang} value={data.vessel_id??''} error={fieldErrors.vessel_id} disabled={frozen||!!pending||!writable} onChange={vessel_id=>setData({...data,vessel_id})} onFailure={failure}/>}
     {fieldErrors.vessel_id&&<p id="cert-vessel_id-error" role="alert">{fieldErrors.vessel_id}</p>}
     <div className="inventory-fields">{certificateFields.map(f=><label key={f.key} htmlFor={'cert-'+f.key}>{f[lang]}{f.key==='notes'?<textarea id={'cert-'+f.key} maxLength={4000} value={data[f.key]??''} onChange={e=>setData({...data,[f.key]:e.target.value})}/>:<input id={'cert-'+f.key} type={f.type??'text'} maxLength={200} value={data[f.key]??''} aria-invalid={!!fieldErrors[f.key]} aria-describedby={fieldErrors[f.key]?'cert-'+f.key+'-error':undefined} onChange={e=>setData({...data,[f.key]:e.target.value})}/>} {fieldErrors[f.key]&&<span id={'cert-'+f.key+'-error'} role="alert">{fieldErrors[f.key]}</span>}</label>)}</div>
    </fieldset>
    {record?.adminState==='RECORDED'&&<><label htmlFor="cert-reason">{t('Düzeltme / yeniden açma gerekçesi (kısıtlı denetim kaydı)','Correction / reopen reason (restricted audit record)')}</label><textarea id="cert-reason" maxLength={500} value={reason} disabled={frozen||!!pending||!caps.canManage} onChange={e=>setReason(e.target.value)}/></>}
    <Button type="submit" disabled={frozen||!!pending||!writable||(record?.adminState==='RECORDED'&&!reason.trim())}>{t('Belge metadatasını kaydet','Save certificate metadata')}</Button>
   </form>
   {pending?.uncertain&&<p role="alert">{t('Sonuç bilinmiyor; önceki istek kaydedilmiş olabilir. Aynı anahtar korunur; yeni bir kayıt isteği başlatılmaz.','Outcome unknown; the earlier request may have committed. The original key is retained; no new create request is started.')}</p>}
   {pending?.confirmedId&&<p>{t('Yazma onaylandı; güncel kayıt okunmayı bekliyor.','Write confirmed; the current record still needs to be read.')}</p>}
   {pending&&<Button disabled={frozen} onClick={()=>void write(pending.action,true)}>{pending.confirmedId?t('Güncel kaydı yeniden oku','Read current record again'):t('Aynı işlemi tekrar dene','Retry same operation')}</Button>}
   {record&&<section><h3>{t('İdari kayıt işlemleri','Administrative commands')}</h3>{record.adminState==='DRAFT'?<Button disabled={frozen||dirty||!caps.canManage||record.completeness.missingRequiredFields.length>0||record.completeness.invalidFields.length>0} onClick={()=>void write('record')}>{t('İdari kayda al','Record administrative entry')}</Button>:<Button disabled={frozen||!!pending||!caps.canManage||!reason.trim()||JSON.stringify(data)!==baseline} onClick={()=>void write('reopen')}>{t('Düzeltme için yeniden aç','Reopen for correction')}</Button>}</section>}
   {record&&import.meta.env.VITE_ENABLE_EVIDENCE_INTAKE==="true"&&<EvidenceIntake key={record.id} api={api} targetType="Certificate" targetId={record.id} lang={lang} disabled={parentBusy||ownBusy||!!pending} expired={expired} onFailure={onFailure} onBusy={setEvidenceBusy} onDirty={setEvidenceDirty} onUncertain={setEvidenceUncertain}/>}
   {record&&<section><h3>{t('Değişiklik geçmişi','Change history')}</h3><ol>{history.map(event=><li key={event.eventId}><time>{new Date(event.occurredAt).toLocaleString(lang==='tr'?'tr-TR':'en-GB',{timeZone:'UTC'})} UTC</time> · {t('Sürüm','Version')} {event.version} · {(certificateActions[event.action]??certificateActions.HISTORICAL_METADATA_CHANGE)[lang==='tr'?0:1]} · {event.actorRelation==='SELF'?t('Siz','You'):t('Başka kullanıcı','Other user')}<p>{event.changedFields.map(label).join(', ')||t('Geçmiş metadata değişikliği','Historical metadata change')}</p></li>)}</ol>{historyNext&&<Button disabled={frozen} onClick={()=>void historyPage(record.id,historyNext)}>{t('Önceki değişiklikler','Earlier changes')}</Button>}</section>}
  </>}
  <section><h3>{t('Açık alan sınırları','Open domain boundaries')}</h3><p>{t('Doğrulama ve operasyon değerlendirmesi: bu uygulama diliminde yapılmadı. Kaynak metni veya tarih, doğrulanmış kanıt ya da yenileme değildir.','Verification and operational assessment: not performed by this application slice. Entered text or dates are not verified evidence or renewal.')}</p><p>{t('Yetkili tip/düzenleyen katalogları, kanıt dosyaları ve doğrulama yetkisi, sörvey pencereleri, resmî yenileme/iptal, PSC hazırlığı, bildirim ve çevrimdışı çalışma ayrı açık alanlardır.','Governed type/issuer catalogues, evidence files and verification authority, survey windows, formal renewal/revocation, PSC readiness, notifications and offline use remain separate open domains.')}</p></section>
 </section>;
}
