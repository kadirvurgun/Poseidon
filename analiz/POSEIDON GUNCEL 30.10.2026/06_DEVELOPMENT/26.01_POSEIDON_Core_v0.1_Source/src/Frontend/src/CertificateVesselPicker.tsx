import {useEffect,useRef,useState} from 'react';
import {Api,type InventoryVessel} from './api';
import {Button} from './components/ui/button';
type Props={api:Api;lang:'tr'|'en';value:string;disabled:boolean;all?:boolean;error?:string;onChange:(value:string)=>void;onFailure:(e:unknown)=>void};
export function CertificateVesselPicker({api,lang,value,disabled,all=false,error,onChange,onFailure}:Props){
 const t=(tr:string,en:string)=>lang==='tr'?tr:en;
 const [rows,setRows]=useState<InventoryVessel[]>([]),[search,setSearch]=useState(''),[applied,setApplied]=useState(''),[cursor,setCursor]=useState<string|null>(null),[loading,setLoading]=useState(false);
 const sequence=useRef(0),abort=useRef<AbortController|null>(null);
 async function load(append=false){if(disabled)return;const n=++sequence.current;abort.current?.abort();const control=new AbortController();abort.current=control;const q=append?applied:search.trim();if(!append){setRows([]);setCursor(null);}setLoading(true);
  try{const page=await api.inventoryList({q,cursor:append?cursor??undefined:undefined},control.signal);if(n!==sequence.current||control.signal.aborted)return;setRows(old=>append?[...old,...page.items.filter(v=>!old.some(p=>p.id===v.id))]:page.items);setApplied(q);setCursor(page.nextCursor);}
  catch(e){if(n===sequence.current&&!control.signal.aborted){setRows([]);setCursor(null);onFailure(e);}}finally{if(n===sequence.current)setLoading(false);}
 }
 useEffect(()=>{void load();return()=>{sequence.current++;abort.current?.abort();};},[api]);
 useEffect(()=>{if(disabled){sequence.current++;abort.current?.abort();setRows([]);setCursor(null);setLoading(false);}},[disabled]);
 return <div className="certificate-vessel-picker">
  <label htmlFor="cert-vessel-search">{t('Seçilecek gemiyi ara','Search vessels for selection')}</label><input id="cert-vessel-search" maxLength={100} value={search} disabled={disabled} onChange={e=>setSearch(e.target.value)}/>
  <Button type="button" disabled={disabled||loading} onClick={()=>void load()}>{t('Gemileri ara','Search vessels')}</Button>
  <label htmlFor="cert-vessel_id">{all?t('Gemi filtresi','Vessel filter'):t('Yetkili gemi','Authorized vessel')}</label>
  <select id="cert-vessel_id" value={value} disabled={disabled} aria-required={!all} aria-invalid={!!error} aria-describedby={error?"cert-vessel_id-error":undefined} onChange={e=>onChange(e.target.value)}>
   <option value="">{all?t('Tüm yetkili gemiler','All authorized vessels'):t('Gemi seçin — varsayılan yok','Choose a vessel — no default')}</option>
   {value&&!rows.some(v=>v.id===value)&&<option value={value}>{t('Seçilen gemi','Selected vessel')} · {value}</option>}
   {rows.map(v=><option key={v.id} value={v.id}>{String(v.data.name||t('Adsız taslak','Unnamed draft'))} · {v.adminState==='RECORDED'?t('Envantere alındı','Recorded in inventory'):t('Taslak','Draft')} · {v.id}</option>)}
  </select>
  {loading&&<p role="status">{t('Gemiler yükleniyor…','Loading vessels…')}</p>}{cursor&&<Button type="button" disabled={disabled||loading} onClick={()=>void load(true)}>{t('Daha fazla gemi','More vessels')}</Button>}
 </div>;
}
