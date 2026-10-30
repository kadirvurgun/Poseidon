// Chromium HTTPS routing -> actual loopback Kestrel HTTP. No TLS/real Codespaces claim.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'/opt/codex/cua_node/lib/node_modules/playwright');
const origin=process.env.UAT_ORIGIN||'https://localhost';
const transportHost=process.env.UAT_TRANSPORT_HOST||'localhost';
const http=require('node:http'),assert=require('node:assert/strict'),fs=require('node:fs');
function send(request){return new Promise(async(resolve,reject)=>{
 const url=new URL(request.url());assert.equal(url.origin,origin);
 const headers=await request.allHeaders();headers.host=transportHost;delete headers['content-length'];
 if(process.env.UAT_REWRITE_ORIGIN==='true' && headers.origin===origin)headers.origin='https://localhost:8080';
 const req=http.request({host:'127.0.0.1',port:8080,path:url.pathname+url.search,method:request.method(),headers},res=>{const chunks=[];res.on('data',x=>chunks.push(x));res.on('end',()=>resolve({status:res.statusCode,headers:Object.fromEntries(Object.entries(res.headers).filter(([k])=>!['transfer-encoding','connection','content-length'].includes(k)).map(([k,v])=>[k,Array.isArray(v)?v.join('\n'):String(v)])),body:Buffer.concat(chunks)}));});req.on('error',reject);req.end(request.postDataBuffer());
});}
(async()=>{const browser=await chromium.launch({executablePath:'/usr/bin/chromium',args:['--no-sandbox']});const results=[];
try{for(const [width,height] of [[390,844],[768,1024],[1440,900]])for(const lang of ['tr','en']){
 const context=await browser.newContext({viewport:{width,height}});let lose=false,keys=[],hold=false,release;let writes=0,legacyFault=0,legacyKeys=[];
 await context.route(origin+'/**',async route=>{const request=route.request();const url=new URL(request.url());if(url.pathname==='/bff/logout'&&hold)await new Promise(r=>release=r);if(request.method()==='POST'&&url.pathname.startsWith('/bff/inventory'))writes++;
 if(request.method()==='POST'&&url.pathname==='/bff/inventory/vessels')keys.push((await request.allHeaders())['idempotency-key']);
 if(request.method()==='POST'&&url.pathname==='/bff/api/v1/vessels'&&legacyFault){legacyKeys.push((await request.allHeaders())['idempotency-key']);if(legacyFault===2){legacyFault=3;await route.fulfill({status:422,contentType:'application/json',body:JSON.stringify({code:'invalid_imo_checksum',message:'Synthetic rollout rejection',errors:{imo_no:['Synthetic rollout rejection']}})});return;}if(legacyFault===1){const committed=await send(request);assert.equal(committed.status,200);legacyFault=2;await route.abort();return;}legacyFault=0;}
 const result=await send(request);if(result.status>=300&&result.status<400&&result.headers.location){const target=new URL(result.headers.location,url);assert.equal(target.origin,origin);delete result.headers.location;result.headers['content-type']='text/html';await route.fulfill({status:200,headers:result.headers,body:'<script>location.replace('+JSON.stringify(target.href).replaceAll('<','\\u003c')+')</script>'});return;}if(lose&&request.method()==='POST'&&url.pathname==='/bff/inventory/vessels'){lose=false;await route.abort();}else await route.fulfill(result);});
 const page=await context.newPage();let cancel=false;page.on('dialog',d=>cancel?d.dismiss():d.accept());const t=(tr,en)=>lang==='tr'?tr:en;const button=name=>page.getByRole('button',{name,exact:true});
 await page.goto(origin);if(lang==='en')await button('Switch to English').click();await button(t('Giriş yap','Sign in')).click();await button(t('Yeni taslak','New draft')).waitFor();
 const cookie=(await context.cookies()).find(c=>c.name==='__Host-Poseidon.Session');assert(cookie&&cookie.secure&&cookie.httpOnly);assert(!(await page.evaluate(()=>document.cookie)).includes('Poseidon.Session'));
 const checks=await page.evaluate(async()=>({missingCsrf:(await fetch('/bff/logout',{method:'POST'})).status,bearer:(await fetch('/api/v1/session')).status,evidence:(await fetch('/bff/api/v1/evidence-intake')).status}));assert.equal(checks.missingCsrf,400);assert.equal(checks.bearer,401);assert(checks.evidence>=400);
 let legacyPosts=0;page.on('request',r=>{if(r.method()==='POST'&&new URL(r.url()).pathname.startsWith('/bff/api/v1/vessels'))legacyPosts++;});
 await button(t('Yeni taslak','New draft')).click();
 await page.locator('#notes').fill('Unsaved checksum test');
 await page.locator('#imo_no').fill('1234568');
 await button(t('Taslağı kaydet','Save draft')).click();
 await page.getByText(t('IMO kontrol basamağı uyuşmuyor. Kaynağından kontrol edin veya isteğe bağlı alanı boş bırakın.','IMO check digit does not match. Check the source or leave this optional field blank.'),{exact:true}).waitFor();
 assert.equal(legacyPosts,0);assert.equal(await page.locator('#notes').inputValue(),'Unsaved checksum test');
 await page.locator('#imo_no').fill('');
 const legacyName=`Legacy uncertain ${width} ${lang} ${Date.now()}`;await page.locator('#name').fill(legacyName);
 legacyFault=1;await button(t('Taslağı kaydet','Save draft')).click();await button(t('Aynı isteği tekrar dene','Retry same request')).waitFor();
 await button(t('Aynı isteği tekrar dene','Retry same request')).click();await page.getByText('invalid_imo_checksum · HTTP 422',{exact:true}).waitFor();
 assert(await page.locator('#imo_no').isDisabled());assert.equal(await page.locator('#notes').inputValue(),'Unsaved checksum test');
 cancel=true;await button(t('Listeye dön','Back to list')).click();cancel=false;assert.equal(await page.locator('#name').inputValue(),legacyName);assert.equal(legacyKeys.length,2);
 await button(t('Aynı isteği tekrar dene','Retry same request')).click();await page.getByText(t('Taslak sunucuya kaydedildi.','Draft saved to the server.'),{exact:true}).waitFor();assert.equal(legacyKeys.length,3);assert.equal(new Set(legacyKeys).size,1);
 const matches=await page.evaluate(async name=>{const session=await(await fetch('/bff/session')).json();const response=await fetch('/bff/api/v1/vessels?'+new URLSearchParams({q:name}),{headers:{'X-Poseidon-Scope':session.subject+':'+session.tenantId+':'+session.companyId}});const body=await response.json();return body.items.filter(x=>x.data.name===name).length;},legacyName);assert.equal(matches,1);
 await button(t('İdari envanter','Administrative inventory')).click();await button(t('Yeni envanter taslağı','New inventory draft')).click();const name=`UAT ${width} ${lang} ${Date.now()}`;await page.locator('#inv-name').fill(name);
 lose=true;await button(t('Metadata kaydet','Save metadata')).click();await page.getByRole('alert').first().waitFor();
 await button(t('Aynı işlemi tekrar dene','Retry same operation')).click();await page.getByText(t('Sunucudaki güncel kayıt doğrulandı.','Current server record confirmed.'),{exact:true}).waitFor();assert.equal(keys.length,2);assert.equal(keys[0],keys[1]);
 await page.locator('#inv-flag').fill('Declared TR');await page.locator('#inv-vessel_type').fill('Tug');await page.locator('#inv-asset_mode').selectOption('TUG');await button(t('Metadata kaydet','Save metadata')).click();await button(t('Envantere al','Record in inventory')).click();await button(t('Düzeltme için yeniden aç','Reopen for correction')).waitFor();
 const bypass=await page.evaluate(async name=>{
  const session=await(await fetch('/bff/session')).json();
  const headers={'X-Poseidon-Scope':session.subject+':'+session.tenantId+':'+session.companyId};
  const list=await(await fetch('/bff/inventory/vessels?'+new URLSearchParams({q:name}),{headers})).json();
  const item=list.items.find(x=>x.data.name===name);if(!item)throw new Error('Synthetic fixture missing');
  const csrf=await(await fetch('/bff/antiforgery')).json();
  const response=await fetch('/bff/inventory/vessels/'+item.id+'/metadata',{method:'POST',headers:{...headers,'Content-Type':'application/json','X-CSRF-TOKEN':csrf.requestToken,'X-Poseidon-Uat-Origin':location.origin,'If-Match':JSON.stringify(String(item.version)),'Idempotency-Key':crypto.randomUUID()},body:JSON.stringify({data:{...item.data,imo_no:'1234568'}})});
  const after=await(await fetch('/bff/inventory/vessels/'+item.id,{headers})).json();
  return {status:response.status,unchanged:after.version===item.version,imoAbsent:!Object.hasOwn(after.data,'imo_no')};
 },name);assert.equal(bypass.status,422);assert(bypass.unchanged&&bypass.imoAbsent);
 await page.locator('#inv-name').fill('Unsaved');cancel=true;await button(t('Belge kayıtları','Certificate records')).click();assert.equal(await page.locator('#inv-name').inputValue(),'Unsaved');cancel=false;
 const before=writes;hold=true;await button(t('Çıkış','Sign out')).click();await page.waitForTimeout(100);assert(await button(t('Metadata kaydet','Save metadata')).isDisabled());assert.equal(writes,before);hold=false;release();await button(t('Giriş yap','Sign in')).waitFor();
 assert.equal((await context.cookies()).filter(c=>c.name==='__Host-Poseidon.Session').length,0);await button(t('Giriş yap','Sign in')).click();await button(t('Yeni taslak','New draft')).waitFor();assert.equal(await page.locator('#inv-name').count(),0);
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await button(t('Çıkış','Sign out')).click();await button(t('Giriş yap','Sign in')).waitFor();
 results.push({width,height,lang,status:'PASS',cookie:'Secure HttpOnly',lostResponse:'same-key replay',legacyLostResponseThen422:'SAME_KEY_THREE_ATTEMPTS_ONE_PERSISTED_RECORD_CANCEL_RETAINS_EDITS',logoutBarrier:'PASS',dirtyCancel:'PASS',imoChecksum:'LOCAL_REJECT_ZERO_POSTS_NOTES_RETAINED_AND_BFF_BYPASS_422_UNCHANGED_VERSION',transport:'EMULATED_HTTPS_ACTUAL_LOOPBACK_KESTREL',origin,transportHost,originRewrite:process.env.UAT_REWRITE_ORIGIN==='true'?'https://localhost:8080':'NONE'});await context.close();
}fs.writeFileSync(process.env.UAT_REPORT||'/tmp/poseidon-uat-browser.json',JSON.stringify({results},null,2));console.log(JSON.stringify(results));}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1});
