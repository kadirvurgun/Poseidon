// Explicit synthetic Development fixture only. Generates benign PDF bytes; reads no user files.
// Requires already configured opt-in private Development root; never configures infrastructure.
const {randomUUID,createHash}=require('node:crypto');
(async()=>{
 const base=process.env.POSEIDON_API_URL||'http://127.0.0.1:5080';const origin=new URL(base);
 if(origin.protocol!=='http:'||!['127.0.0.1','localhost','[::1]'].includes(origin.hostname)||origin.username||origin.password)throw Error('Only an explicitly local synthetic Development API is supported.');
 let token;async function request(path,init={}){const r=await fetch(base+path,{...init,headers:{...(token?{Authorization:'Bearer '+token}:{}),...init.headers}});const value=await r.json();if(!r.ok)throw Error(`Unconfirmed HTTP ${r.status}: ${value.code||'unknown'}`);return value;}
 const session=await request('/api/v1/dev/session',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({fixture:'evidence-manager'})});token=session.accessToken;
 async function json(path,body,version){return request(path,{method:'POST',headers:{'Content-Type':'application/json','Idempotency-Key':randomUUID(),...(version?{'If-Match':'"'+version+'"'}:{})},body:JSON.stringify(body)});}
 const parent=await json('/api/v1/inventory/vessels',{data:{name:'Synthetic evidence UAT tug',asset_mode:'TUG'}});
 const bytes=Buffer.from('%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n');
 const metadata={targetType:'Vessel',targetId:parent.id,filename:'synthetic-uat.pdf',declaredMime:'application/pdf',expectedBytes:bytes.length,expectedSha256:createHash('sha256').update(bytes).digest('hex'),title:'Synthetic quarantine fixture',source:'Generated test bytes only'};
 const reserved=await json('/api/v1/evidence-intake',metadata);const stored=await request('/api/v1/evidence-intake/'+reserved.id+'/content',{method:'POST',headers:{'Content-Type':'application/pdf','Idempotency-Key':randomUUID(),'If-Match':'"'+reserved.version+'"'},body:bytes});
 if(stored.state!=='STORED_QUARANTINED'||stored.scanState!=='UNAVAILABLE'||stored.canDownload!==false)throw Error('Unexpected quarantine boundary');
 console.log(JSON.stringify({fixture:'generated synthetic PDF signature fixture; not externally validated document',vesselId:parent.id,intakeId:stored.id,state:stored.state,scanState:stored.scanState,sha256:stored.sha256,canDownload:stored.canDownload,userAccess:'NOT_ESTABLISHED',userAcceptance:'PENDING'},null,2));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
