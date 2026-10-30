// Synthetic fixtures through an existing loopback Development API; no hosting/setup/grants.
const assert=require('node:assert/strict');
const base=new URL(process.env.POSEIDON_UAT_API||'http://127.0.0.1:5080');
assert(['localhost','127.0.0.1','[::1]'].includes(base.hostname)&&!base.username&&!base.password&&!base.search&&!base.hash);
(async()=>{
 const response=await fetch(new URL('/api/v1/dev/session',base),{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({fixture:'certificate-manager'})});assert.equal(response.status,200);
 const {accessToken,session}=await response.json();assert.equal(session.synthetic,true);assert.equal(session.subject,'synthetic-certificate-manager');
 const write=async(path,body,version)=>{const r=await fetch(new URL(path,base),{method:'POST',headers:{Authorization:'Bearer '+accessToken,'Content-Type':'application/json','Idempotency-Key':crypto.randomUUID(),...(version?{'If-Match':`"${version}"`}:{})},body:JSON.stringify(body)});assert.equal(r.status,200,'Synthetic write must be confirmed');return r.json();};
 const parent=await write('/api/v1/inventory/vessels',{data:{name:'SYNTHETIC Certificate UAT Tug',asset_mode:'TUG',flag:'Declared TR',vessel_type:'Declared tug'}});
 const root='/api/v1/certificate-register';const partial=await write(root,{data:{vessel_id:parent.id}});
 const asOf=partial.dateTracking.asOfDate;const day=(offset)=>{const date=new Date(asOf+'T00:00:00Z');date.setUTCDate(date.getUTCDate()+offset);return date.toISOString().slice(0,10);};
 const records=[{id:partial.id,adminState:partial.adminState,dateBucket:partial.dateTracking.bucket}];
 for(const [name,expiry]of [['No expiry',undefined],['Past date',day(-1)],['Today',day(0)],['30 days',day(30)],['31 days',day(31)],['91 days',day(91)]]){
  const draft=await write(root,{data:{vessel_id:parent.id,type:'SYNTHETIC '+name,issuer:'Declared synthetic issuer',...(expiry?{expiry}:{})}});
  const recorded=await write(root+'/'+draft.id+'/commands/record',{},draft.version);assert.equal(recorded.verificationAssessment,'NOT_PERFORMED');records.push({id:recorded.id,adminState:recorded.adminState,dateBucket:recorded.dateTracking.bucket});
 }
 console.log(JSON.stringify({synthetic:true,fixture:'certificate-manager',parentId:parent.id,parentAdminState:parent.adminState,asOfDate:asOf,timeBasis:'UTC_CALENDAR_DATE',records,note:'Declared synthetic data only. No private user access or user acceptance established. Re-running creates new records.'},null,2));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
