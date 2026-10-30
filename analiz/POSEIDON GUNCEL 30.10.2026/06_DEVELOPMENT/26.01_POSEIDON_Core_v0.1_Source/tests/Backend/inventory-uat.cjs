// Explicit synthetic UAT seeding through an already running Development API.
// No host setup, account creation, migration, credentials or network configuration.
// Run: node tests/Backend/inventory-uat.cjs
const assert = require('node:assert/strict');
const base = new URL(process.env.POSEIDON_UAT_API || 'http://127.0.0.1:5080');
assert(['127.0.0.1','localhost','[::1]'].includes(base.hostname), 'Only loopback Development API is supported');
assert(!base.username && !base.password && !base.search && !base.hash);
(async()=>{
 const login=await fetch(new URL('/api/v1/dev/session',base),{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({fixture:'inventory-manager'})});
 assert.equal(login.status,200,'Requires existing explicitly enabled Development synthetic fixtures');
 const {accessToken,session}=await login.json();assert.equal(session.synthetic,true);assert.equal(session.subject,'synthetic-inventory-manager');
 const write=async(path,body,version)=>{
  const response=await fetch(new URL('/api/v1/inventory/vessels'+path,base),{method:'POST',headers:{Authorization:'Bearer '+accessToken,'Content-Type':'application/json','Idempotency-Key':crypto.randomUUID(),...(version?{'If-Match':`"${version}"`}:{})},body:JSON.stringify(body)});
  assert.equal(response.status,200,'Synthetic fixture save must be confirmed');return response.json();
 };
 const incomplete=await write('',{data:{notes:'SYNTHETIC UAT: incomplete; no invented mode, flag or IMO.'}});
 const tug=await write('',{data:{name:'SYNTHETIC UAT Tug',flag:'Declared TR',vessel_type:'Declared harbour tug',asset_mode:'TUG',official_no:'SYNTHETIC-001',port_registry:'Synthetic port',call_sign:'Synthetic call',mmsi:'Declared reference',gt:0,nt:10.123,dwt:100,loa:25,breadth:8,draft:3,build_date:'2030-01-01',class_society:'Declared synthetic class',class_no:'Synthetic class ref',doc_company:'Declared synthetic operator',company_imo:'1234567',master_name:'Synthetic contact only',dpa_name:'Synthetic DPA contact only',evidence_ref:'Synthetic source text; no attachment',notes:'Synthetic UAT only; no operational approval.'}});
 const recorded=await write(`/${tug.id}/commands/record`,{},tug.version);
 assert.equal(recorded.id,tug.id);assert.equal(recorded.adminState,'RECORDED');assert.equal(recorded.data.imo_no,undefined);
 console.log(JSON.stringify({synthetic:true,fixture:'inventory-manager',created:[{id:incomplete.id,adminState:incomplete.adminState},{id:recorded.id,adminState:recorded.adminState}],note:'Re-running creates new synthetic records. No real user acceptance or private URL is established.'},null,2));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
