// Real Chromium + built React, bridged to ASP.NET TestServer over stdio.
// HTTPS is virtual within Playwright routing: no TLS certificate, socket IdP or real identity.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || '/opt/codex/cua_node/lib/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const readline = require('node:readline');
let sequence = 0;
const pending = new Map();
readline.createInterface({input: process.stdin}).on('line', line => { const value = JSON.parse(line); pending.get(value.id)?.(value); pending.delete(value.id); });
const rpc = data => new Promise(resolve => { const id = ++sequence; pending.set(id, resolve); process.stdout.write(JSON.stringify({id, ...data}) + '\n'); });
(async () => {
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', args: ['--no-sandbox'] });
  const results = [];
  try {
    for (const [width,height] of [[390,844],[768,1024],[1440,900]]) for (const lang of ['tr', 'en']) {
      const context = await browser.newContext({ viewport: { width, height }, timezoneId: lang === 'tr' ? 'Pacific/Honolulu' : 'Pacific/Kiritimati' });
      const page = await context.newPage();
      page.setDefaultTimeout(15000);
      let writes = 0, authorize = 0, cancel = false;
      let loseEvidenceResponse=false, evidenceWrites=0, droppedEvidenceId=null;
      let loseCreateResponse = false, inventoryWrites = 0, holdLogout = false, releaseLogout;
      const uncertainKeys = [];
      page.on('dialog', dialog => cancel ? dialog.dismiss() : dialog.accept());
      const t = (tr, en) => lang === 'tr' ? tr : en;
      const button = name => page.getByRole('button', {name, exact: true});
      await context.route('**/*', async route => {
        const request = route.request(); const url = new URL(request.url());
        console.error("route", request.method(), url.origin + url.pathname);
        if (url.origin === 'https://issuer.invalid' && url.pathname === '/authorize') {
          authorize++;
          await rpc({op: 'authorize', nonce: url.searchParams.get('nonce'), challenge: url.searchParams.get('code_challenge')});
          const state = url.searchParams.get('state'); assert.match(state, /^[a-zA-Z0-9_-]+$/);
          await route.fulfill({contentType:'text/html', body:`<form method="post" action="https://localhost/bff/callback"><input name="code" value="isolated-code"><input name="state" value="${state}"></form><script>document.forms[0].submit()</script>`}); return;
        }
        assert.equal(url.origin, 'https://localhost');
        if (url.pathname.startsWith('/bff/')) {
          if(request.method()==='POST' && url.pathname==='/bff/logout' && holdLogout)await new Promise(resolve=>{releaseLogout=resolve;});
          if(request.method()==='POST' && url.pathname.startsWith('/bff/inventory/'))inventoryWrites++;
          if(request.method()==='POST' && url.pathname==='/bff/inventory/vessels' && (loseCreateResponse || uncertainKeys.length))uncertainKeys.push((await request.allHeaders())['idempotency-key']);
          if (request.method() === 'POST' && url.pathname.startsWith('/bff/api/')) writes++;
          if(request.method()==='POST' && url.pathname.startsWith('/bff/api/v1/evidence-intake'))evidenceWrites++;
          const response = await rpc({op:'request', url:request.url(), method:request.method(), headers:await request.allHeaders(), body:request.postData(), bodyBase64:request.postDataBuffer()?.toString('base64') ?? null});
          console.error("response",url.pathname,response.status);
          if(loseEvidenceResponse && request.method()==='POST' && /^\/bff\/api\/v1\/evidence-intake\/[^/]+\/content$/.test(url.pathname)){assert.equal(response.status,200);droppedEvidenceId=JSON.parse(Buffer.from(response.body,'base64').toString()).id;loseEvidenceResponse=false;await route.abort('connectionfailed');return;}
          if(loseCreateResponse && request.method()==='POST' && url.pathname==='/bff/inventory/vessels') {
            assert.equal(response.status,200);loseCreateResponse=false;
            await rpc({op:'write_permission',allowed:false});await route.abort('connectionfailed');return;
          }
          if (response.status >= 300 && response.status < 400 && response.headers.location) {
            // Playwright does not reroute a fulfilled redirect chain. Relay the handler's
            // exact Location as a new navigation, retaining its original Set-Cookie headers.
            // This tests handler + browser state, not native HTTP redirect/TLS transport.
            const target = new URL(response.headers.location, url).href;
            assert(['https://localhost', 'https://issuer.invalid'].includes(new URL(target).origin));
            delete response.headers.location; delete response.headers['content-length'];
            response.headers['content-type'] = 'text/html';
            await route.fulfill({status:200,headers:response.headers,body:'<script>location.replace('+JSON.stringify(target).replaceAll('<','\\u003c')+')</script>'}); return;
          }
          await route.fulfill({status:response.status, headers:response.headers, body:Buffer.from(response.body,'base64')}); return;
        }
        assert(!url.pathname.startsWith('/api/'), 'BFF UI must never call bearer API');
        const relative = url.pathname === '/' ? 'index.html' : url.pathname.slice(1);
        const target = path.resolve(process.env.POSEIDON_BFF_BROWSER_DIST, relative);
        assert(target.startsWith(path.resolve(process.env.POSEIDON_BFF_BROWSER_DIST) + path.sep));
        await route.fulfill({body: fs.readFileSync(target), contentType: relative.endsWith('.js') ? 'text/javascript' : relative.endsWith('.css') ? 'text/css' : 'text/html'});
      });
      await page.goto('https://localhost/?lang='+lang);
      await button(t('Giriş yap','Sign in')).waitFor(); assert.equal(authorize,0);
      await button(t('Giriş yap','Sign in')).click();
      await button(t('Yeni taslak','New draft')).waitFor(); assert.equal(authorize,1);
      assert.equal(await page.locator('#token').count(),0);
      const cookie = (await context.cookies()).find(c => c.name === '__Host-Poseidon.Session');
      assert(cookie && cookie.secure && cookie.httpOnly);
      assert(!(await page.evaluate(() => document.cookie)).includes('Poseidon.Session'));
      assert(!cookie.value.includes('callback-subject'));
      await button(t('Yeni taslak','New draft')).click(); await page.locator('#name').fill('Browser BFF '+lang);
      await button(t('Taslağı kaydet','Save draft')).click();
      await page.getByText(t('Taslak sunucuya kaydedildi.','Draft saved to the server.'),{exact:true}).waitFor();
      await button(t('Sertifika taslakları','Certificate drafts')).click();
      await button(t('Yeni taslak','New draft')).click();
      await page.locator('#vessel-search').fill('Browser BFF '+lang);
      await button(t('Gemileri ara','Search vessels')).click();
      await page.locator('#vessel_id option').filter({hasText:'Browser BFF '+lang}).first().waitFor({state:'attached'});
      await page.locator('#vessel_id').selectOption({label: await page.locator('#vessel_id option').filter({hasText:'Browser BFF '+lang}).last().textContent()});
      await page.locator('#type').fill('BFF isolated certificate');
      await button(t('Taslağı kaydet','Save draft')).click();
      await page.getByText(t('Taslak sunucuya kaydedildi.','Draft saved to the server.'),{exact:true}).waitFor();
      await page.locator('#type').fill('Unsaved retained '+lang);
      cancel = true; await button(t('Gemi taslakları','Vessel drafts')).click();
      assert.equal(await page.locator('#type').inputValue(),'Unsaved retained '+lang);
      await button(t('Çıkış','Sign out')).click(); assert.equal(await page.locator('#type').inputValue(),'Unsaved retained '+lang);
      await rpc({op:'expire'});
      const beforeExpiryWrites = writes;
      await button(t('Taslağı kaydet','Save draft')).click();
      await page.getByText(t('Oturum sona erdi.','Session ended.'),{exact:true}).waitFor();
      assert.equal(await page.locator('#type').inputValue(),'Unsaved retained '+lang);
      assert(await button(t('Aynı isteği tekrar dene','Retry same request')).isDisabled());
      await button(t('Yeniden giriş yap','Sign in again')).click(); assert.equal(authorize,1);
      cancel = false; await button(t('Yeniden giriş yap','Sign in again')).click();
      await button(t('Yeni taslak','New draft')).waitFor(); assert.equal(authorize,2);
      assert.equal(await page.locator('#type').count(),0);
      assert.equal(writes,beforeExpiryWrites+1); // one rejected request, no automatic retry
      // Full administrative journey against actual server/DB; no browser API mock.
      await button(t('İdari envanter','Administrative inventory')).click();
      await button(t('Yeni envanter taslağı','New inventory draft')).click();
      assert.equal(await page.locator('#inv-asset_mode').inputValue(),'');
      await button(t('Metadata kaydet','Save metadata')).click();
      await page.getByText(t('Sunucudaki güncel kayıt doğrulandı.','Current server record confirmed.'),{exact:true}).waitFor();
      assert(await button(t('Envantere al','Record in inventory')).isDisabled());
      const adminName='Admin synthetic '+width+' '+lang+' '+Date.now();
      await page.locator('#inv-name').fill(adminName);
      await page.locator('#inv-flag').fill('Declared TR');await page.locator('#inv-vessel_type').fill('Declared tug');
      await page.locator('#inv-asset_mode').selectOption('TUG');
      await page.locator('#inv-gt').fill('0');await page.locator('#inv-loa').fill('12.345');
      await page.locator('#inv-master_name').fill('Unverified synthetic contact');
      await page.locator('#inv-notes').fill('Synthetic private note');
      await button(t('Metadata kaydet','Save metadata')).click();
      await button(t('Envantere al','Record in inventory')).waitFor();
      await button(t('Envantere al','Record in inventory')).click();
      await button(t('Düzeltme için yeniden aç','Reopen for correction')).waitFor();
      const identity=(await page.locator('.inventory-id').textContent()).match(/ID: ([0-9a-f-]+)/)[1];
      assert.equal(await page.locator('#inv-imo_no').inputValue(),'');assert(await page.locator('#inv-asset_mode').isDisabled());
      await page.locator('#inv-name').fill(adminName+' corrected');await button(t('Metadata kaydet','Save metadata')).click();
      await page.getByText(t('Sunucudaki güncel kayıt doğrulandı.','Current server record confirmed.'),{exact:true}).waitFor();
      await page.locator('#reopen-reason').fill('Synthetic restricted reason');await button(t('Düzeltme için yeniden aç','Reopen for correction')).click();
      await button(t('Envantere al','Record in inventory')).waitFor();
      await page.waitForFunction(()=>!document.querySelector('#inv-asset_mode').matches(':disabled'));assert(!(await page.locator('#inv-asset_mode').isDisabled()));
      await page.locator('#inv-asset_mode').selectOption('SHIP');await button(t('Metadata kaydet','Save metadata')).click();
      await button(t('Envantere al','Record in inventory')).click();await button(t('Düzeltme için yeniden aç','Reopen for correction')).waitFor();
      assert((await page.locator('.inventory-id').textContent()).includes(identity));
      const historyBody=await page.evaluate(async id=>{const session=await (await fetch('/bff/session')).json();const stamp=`${session.subject}:${session.tenantId}:${session.companyId}`;return await (await fetch(`/bff/inventory/vessels/${id}/history`,{headers:{'X-Poseidon-Scope':stamp}})).text();},identity);
      for(const secret of ['Synthetic restricted reason','Synthetic private note','Unverified synthetic contact','callback-subject','before_data','after_data'])assert(!historyBody.includes(secret));
      assert(JSON.parse(historyBody).items.length===7);
      assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
      await page.locator('#inv-notes').fill('Retain dirty administrative edit');cancel=true;
      await button(t('Sertifika taslakları','Certificate drafts')).click();assert.equal(await page.locator('#inv-notes').inputValue(),'Retain dirty administrative edit');
      cancel=false;await button(t('Sertifika taslakları','Certificate drafts')).click();
      await button(t('Yeni taslak','New draft')).click();await page.locator('#vessel-search').fill(adminName);
      await button(t('Gemileri ara','Search vessels')).click();await page.locator(`#vessel_id option[value="${identity}"]`).waitFor({state:'attached'});
      await page.locator('#vessel_id').selectOption(identity);await page.locator('#type').fill('Recorded vessel synthetic certificate');
      await button(t('Taslağı kaydet','Save draft')).click();await page.getByText(t('Taslak sunucuya kaydedildi.','Draft saved to the server.'),{exact:true}).waitFor();
      // Certificate administrative tracking: frozen server day, differing browser timezones.
      await button(t('Belge kayıtları','Certificate records')).click();
      await button(t('Yeni belge taslağı','New certificate draft')).click();
      await page.locator('#cert-vessel-search').fill(adminName);
      await button(t('Gemileri ara','Search vessels')).click();
      await page.locator(`#cert-vessel_id option[value="${identity}"]`).waitFor({state:'attached'});await page.locator('#cert-vessel_id').selectOption(identity);
      await button(t('Belge metadatasını kaydet','Save certificate metadata')).click();
      await page.getByText(t('Sunucudaki güncel kayıt okundu.','Current server record loaded.'),{exact:true}).waitFor();
      assert(await button(t('İdari kayda al','Record administrative entry')).isDisabled());
      await page.locator('#cert-type').fill('Declared browser certificate');await page.locator('#cert-issuer').fill('Declared authority');
      await button(t('Belge metadatasını kaydet','Save certificate metadata')).click();
      await button(t('İdari kayda al','Record administrative entry')).click();
      await button(t('Düzeltme için yeniden aç','Reopen for correction')).waitFor();
      assert.equal(await page.locator('#cert-expiry').inputValue(),'');
      const certIdentity=(await page.locator('.certificate-register .inventory-id').textContent()).match(/ID: ([0-9a-f-]+)/)[1];
      await page.locator('#cert-expiry').fill('2026-11-26');await page.locator('#cert-issue').fill('2026-11-01');
      assert(await button(t('Belge metadatasını kaydet','Save certificate metadata')).isDisabled());
      await page.locator('#cert-reason').fill('PRIVATE browser correction');
      await button(t('Belge metadatasını kaydet','Save certificate metadata')).click();
      await page.getByText(t('Sunucudaki güncel kayıt okundu.','Current server record loaded.'),{exact:true}).waitFor();
      assert.equal(await page.locator('#cert-expiry').inputValue(),'2026-11-26');
      const tracked=await page.evaluate(async id=>{const s=await(await fetch('/bff/session')).json();const headers={'X-Poseidon-Scope':`${s.subject}:${s.tenantId}:${s.companyId}`};return await(await fetch('/bff/api/v1/certificate-register/'+id,{headers})).json();},certIdentity);
      assert.equal(tracked.dateTracking.asOfDate,'2026-10-27');assert.equal(tracked.dateTracking.bucket,'WITHIN_30_DAYS');assert.equal(tracked.dateTracking.daysToRecordedExpiry,30);assert.equal(tracked.dateTracking.issueDateInFuture,true);assert.equal(tracked.verificationAssessment,'NOT_PERFORMED');assert.equal(tracked.operationalAssessment,'NOT_PERFORMED');assert.equal(tracked.parentVessel.adminState,'RECORDED');
      await page.locator('#cert-reason').fill('PRIVATE browser reopen');await button(t('Düzeltme için yeniden aç','Reopen for correction')).click();
      await button(t('İdari kayda al','Record administrative entry')).click();await button(t('Düzeltme için yeniden aç','Reopen for correction')).waitFor();
      assert((await page.locator('.certificate-register .inventory-id').textContent()).includes(certIdentity));
      const certificateHistory=await page.evaluate(async id=>{const s=await(await fetch('/bff/session')).json();return await(await fetch('/bff/api/v1/certificate-register/'+id+'/history',{headers:{'X-Poseidon-Scope':`${s.subject}:${s.tenantId}:${s.companyId}`}})).text();},certIdentity);
      assert.equal(JSON.parse(certificateHistory).items.length,6);assert(!certificateHistory.includes('PRIVATE'));assert(!certificateHistory.includes('callback-subject'));
      // Generated benign bytes only; browser never receives stored content or storage keys.
      const syntheticPdf=Buffer.from('%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n');
      await page.locator('#evidence-file').setInputFiles({name:'synthetic-browser.pdf',mimeType:'application/pdf',buffer:syntheticPdf});
      loseEvidenceResponse=true;await button(t('Karantinaya gönder','Send to quarantine')).click();
      await button(t('Yalnız alım durumunu kontrol et','Check intake status only')).waitFor();await button(t('Yalnız alım durumunu kontrol et','Check intake status only')).click();
      await page.locator('.evidence-intake [role="status"]').filter({hasText:t('Karantinada saklandı','Stored in quarantine')}).waitFor();
      const intakeStatus=await page.locator('.evidence-intake [role="status"]').textContent();const intakeId=intakeStatus.match(/[0-9a-f]{8}-[0-9a-f-]{27,}/)[0];
      const intakeCheck=await page.evaluate(async id=>{const s=await(await fetch('/bff/session')).json();const headers={'X-Poseidon-Scope':`${s.subject}:${s.tenantId}:${s.companyId}`};const receipt=await(await fetch('/bff/api/v1/evidence-intake/'+id,{headers})).json();const download=await fetch('/bff/api/v1/evidence-intake/'+id+'/content',{headers});const history=await(await fetch('/bff/api/v1/evidence-intake/'+id+'/history',{headers})).text();const listing=await fetch('/bff/api/v1/evidence-intake?'+new URLSearchParams({targetType:receipt.targetType,targetId:receipt.targetId}),{headers});return {receipt,downloadStatus:download.status,history,listStatus:listing.status,list:await listing.json()};},intakeId);
      assert.equal(intakeCheck.listStatus,200);assert(intakeCheck.list.items.some(x=>x.id===intakeId));assert.equal(intakeCheck.receipt.state,'STORED_QUARANTINED');assert.equal(intakeCheck.receipt.scanState,'UNAVAILABLE');assert.equal(intakeCheck.receipt.canDownload,false);assert.equal(intakeCheck.receipt.verificationAssessment,'NOT_PERFORMED');assert(intakeCheck.downloadStatus>=400);assert(!intakeCheck.history.includes('synthetic-browser.pdf'));assert(!intakeCheck.history.includes('callback-subject'));
      await page.locator('.evidence-intake li').filter({hasText:intakeId}).getByRole('button',{name:t('Yeni dosyayla düzelt','Correct with a new file'),exact:true}).click();
      await page.locator('#evidence-reason').fill('PRIVATE synthetic correction reason');
      await page.locator('#evidence-file').setInputFiles({name:'synthetic-correction.pdf',mimeType:'application/pdf',buffer:Buffer.from(syntheticPdf.toString().replace('%PDF-1.4','%PDF-1.4\n% synthetic correction'))});
      await button(t('Karantinaya gönder','Send to quarantine')).click();
      await page.waitForFunction(old=>{const status=document.querySelector('.evidence-intake [role="status"]');return status&&!status.textContent.includes(old);},intakeId);
      await page.locator('.evidence-intake [role="status"]').filter({hasText:t('Karantinada saklandı','Stored in quarantine')}).waitFor();
      await page.locator('.evidence-intake li').filter({hasText:'synthetic-browser.pdf'}).getByRole('button',{name:t('Alım geçmişi','Intake history'),exact:true}).click();
      await page.getByText(t('Güvenli alım geçmişi','Safe intake history'),{exact:true}).waitFor();
      const corrected=await page.evaluate(async id=>{const session=await(await fetch('/bff/session')).json();const headers={'X-Poseidon-Scope':`${session.subject}:${session.tenantId}:${session.companyId}`};const old=await(await fetch('/bff/api/v1/evidence-intake/'+id,{headers})).json();const history=await(await fetch('/bff/api/v1/evidence-intake/'+id+'/history',{headers})).text();return {old,history};},intakeId);
      assert(corrected.old.supersededBy);assert.equal(corrected.old.sha256,intakeCheck.receipt.sha256);assert(!corrected.history.includes('PRIVATE'));assert.equal(JSON.parse(corrected.history).items.length,4);
      // Abandon only local retry state after a committed response is lost. Native selection resets.
      const sameFile={name:'synthetic-abandon.pdf',mimeType:'application/pdf',buffer:syntheticPdf};
      await page.locator('#evidence-file').setInputFiles(sameFile);loseEvidenceResponse=true;await button(t('Karantinaya gönder','Send to quarantine')).click();
      await button(t('Yerel alımı bırak','Abandon local intake')).waitFor();
      assert.equal(await page.locator('#evidence-file').evaluate(e=>e.files.length),1);
      await button(t('Yerel alımı bırak','Abandon local intake')).click();
      assert.equal(await page.locator('#evidence-file').inputValue(),'');assert.equal(await page.locator('#evidence-file').evaluate(e=>e.files.length),0);
      const beforeReselect=evidenceWrites;await page.locator('#evidence-file').setInputFiles(sameFile);assert(await button(t('Karantinaya gönder','Send to quarantine')).isEnabled());assert.equal(evidenceWrites,beforeReselect);
      const retained=await page.evaluate(async id=>{const s=await(await fetch('/bff/session')).json();return await(await fetch('/bff/api/v1/evidence-intake/'+id,{headers:{'X-Poseidon-Scope':`${s.subject}:${s.tenantId}:${s.companyId}`}})).json();},droppedEvidenceId);assert.equal(retained.state,'STORED_QUARANTINED');
      await page.locator('#evidence-file').setInputFiles([]); // No new send; fixture leaves the server object intact.
      assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
      await page.locator('#cert-notes').fill('Retain certificate edit');cancel=true;await button(t('İdari envanter','Administrative inventory')).click();assert.equal(await page.locator('#cert-notes').inputValue(),'Retain certificate edit');cancel=false;
      // Commit happened, but the browser loses the response; replay permission is then revoked.
      await button(t('İdari envanter','Administrative inventory')).click();
      await button(t('Yeni envanter taslağı','New inventory draft')).click();
      const uncertainName='Uncertain synthetic '+width+' '+lang+' '+Date.now();
      await page.locator('#inv-name').fill(uncertainName);await page.locator('#inv-flag').fill('TR');
      await page.locator('#inv-vessel_type').fill('Tug');await page.locator('#inv-asset_mode').selectOption('TUG');
      loseCreateResponse=true;await button(t('Metadata kaydet','Save metadata')).click();
      await button(t('Aynı işlemi tekrar dene','Retry same operation')).waitFor();
      await button(t('Aynı işlemi tekrar dene','Retry same operation')).click();await page.getByText('action_forbidden',{exact:true}).waitFor();
      assert(await button(t('Metadata kaydet','Save metadata')).isDisabled());
      await rpc({op:'write_permission',allowed:true});
      await button(t('Aynı işlemi tekrar dene','Retry same operation')).click();
      await page.getByText(t('Sunucudaki güncel kayıt doğrulandı.','Current server record confirmed.'),{exact:true}).waitFor();
      assert.equal(uncertainKeys.length,3);assert.equal(new Set(uncertainKeys).size,1);
      const persisted=await page.evaluate(async name=>{const session=await (await fetch('/bff/session')).json();const headers={'X-Poseidon-Scope':`${session.subject}:${session.tenantId}:${session.companyId}`};const list=await (await fetch('/bff/inventory/vessels?q='+encodeURIComponent(name),{headers})).json();const history=await (await fetch('/bff/inventory/vessels/'+list.items[0].id+'/history',{headers})).json();return {objects:list.items.length,events:history.items.length};},uncertainName);
      assert.deepEqual(persisted,{objects:1,events:1});
      // While confirmed logout is held before server processing, even forced form submit sends no write.
      holdLogout=true;const beforeLogoutWrites=inventoryWrites;
      await button(t('Çıkış','Sign out')).click();
      for(let tries=0;!releaseLogout && tries<100;tries++)await new Promise(resolve=>setTimeout(resolve,10));
      assert.equal(typeof releaseLogout,'function');assert(await button(t('Metadata kaydet','Save metadata')).isDisabled());assert(await button(t('Envantere al','Record in inventory')).isDisabled());
      await page.locator('#inv-name').evaluate(input=>input.closest('form').dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})));
      assert.equal(inventoryWrites,beforeLogoutWrites);releaseLogout();
      await button(t('Giriş yap','Sign in')).waitFor();

      await page.reload(); await button(t('Giriş yap','Sign in')).waitFor(); assert.equal(authorize,2);
      const storage = await page.evaluate(() => ({local:localStorage.length, session:sessionStorage.length, cookie:document.cookie}));
      assert.equal(storage.local,0); assert.equal(storage.session,0); assert(!storage.cookie.includes('Poseidon.Session'));
      results.push({lang, width, height, administrativeJourney:'PASS', certificateJourney:'PASS', abandonSameFileReselect:'PASS: native input reset; no automatic resend; committed object retained', quarantinedEvidence:'PASS: generated PDF, lost response recovery, supersede retains hash, content blocked, safe history', certificateDateContext:'2026-10-27 UTC; expiry+30; browser Hawaii/Kiritimati unchanged', uncertainCreateReplay:'PASS: one object and one event; original key across 403', deferredLogoutBarrier:'PASS: zero new inventory writes', administrativeEvents:7, recordedVesselCertificate:'PASS', status:'PASS', successfulDraftWrites:3, rejectedAfterExpiry:1, automaticRetries:0, logins:authorize, virtualHttps:true});
      await context.close();
    }
    if (process.env.POSEIDON_BFF_BROWSER_REPORT) fs.writeFileSync(process.env.POSEIDON_BFF_BROWSER_REPORT, JSON.stringify({chromium:browser.version(),results},null,2));
  } finally { await browser.close(); }
})().then(() => process.exit(0)).catch(error => { console.error(error); process.exit(1); });
