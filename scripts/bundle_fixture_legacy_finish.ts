// DEV-BUNDLE-FIXTURE-01. Real signed fixture draft/review, never operational approval.
import {randomUUID} from 'node:crypto'
import {spawnSync} from 'node:child_process'
import {readFileSync,realpathSync,writeFileSync} from 'node:fs'
import {resolve} from 'node:path'
import {strictContext} from '../tests/Poseidon.Browser.Tests/lab/strict-browser.ts'
import {login} from '../tests/Poseidon.Browser.Tests/lab/journeys.ts'
const privateRoot=realpathSync(process.env.POSEIDON_LAB_PRIVATE!)
if(privateRoot!==realpathSync(resolve(import.meta.dirname,'../.runtime/oidc-lab')))throw new Error('fixture_boundary')
const plan=JSON.parse(readFileSync(resolve(privateRoot,'source-bundle-plan.json'),'utf8'))
const observed=JSON.parse(readFileSync(resolve(privateRoot,'source-bundle-observed.json'),'utf8'))
if(observed.schemaVersion!=='poseidon.bundle-observed-sources.v1'||observed.runId!==plan.runId)throw new Error('fixture_scope_invalid')
const evidence={schemaVersion:'poseidon.bundle-legacy-preconditions.v1',status:'RUNNING',stage:'begin',checks:[] as {stage:string;status:number}[],accepted:false,canonicalSourceApproved:false,decisionsRecorded:0}
const lanes:Record<string,Record<string,string>>={}
const persist=(name:string,value:unknown)=>{const temp=resolve(privateRoot,'.'+randomUUID()+'.json');writeFileSync(temp,JSON.stringify(value),{flag:'wx',mode:0o600});const published=spawnSync(process.env.POSEIDON_LAB_PYTHON!,[resolve(import.meta.dirname,'bundle_private.py'),'publish-temp','--source',temp,'--target',resolve(privateRoot,name)],{stdio:'ignore',windowsHide:true,timeout:30000});if(published.status!==0)throw new Error('fixture_new_publication_failed')}
try{
  const reviewer=await strictContext({baseURL:'https://localhost:5443'})
  try{
    const page=await reviewer.context.newPage();page.setDefaultTimeout(15000);evidence.stage='reviewer_login';await login(page,plan.reviewerAccount,'/app/today')
    const response=await page.request.get('/api/v1/auth/session');if(response.status()!==200)throw new Error('fixture_session_failed')
    const user=await response.json();if(user.actorId!==plan.reviewerActorId||user.tenantId!==plan.tenantId||!user.authenticated)throw new Error('fixture_actor_mismatch')
    for(const lane of ['desktop','narrow']){
      const source=observed.lanes[lane].rule,root='/api/v1/regulatory-source-governance/'+source.id
      evidence.stage='read_actual_'+lane+'_source_authority'
      const current=await page.request.get(root);evidence.checks.push({stage:evidence.stage,status:current.status()});if(current.status()!==200)throw new Error('fixture_source_unavailable')
      const context=await current.json();if(context.items.length!==0)throw new Error('fixture_rule_reused')
      const draftAuthority=context.authorities.find((a:{action:string;authorityId:string})=>a.action==='draft')?.authorityId
      const reviewAuthority=context.authorities.find((a:{action:string;authorityId:string})=>a.action==='review')?.authorityId
      if(!draftAuthority||!reviewAuthority)throw new Error('fixture_exact_source_assignment_required')
      const content={sourceBytesBase64:Buffer.from('FICTIONAL '+plan.runId+' '+lane+' software source; no maritime authority').toString('base64'),publisher:'FICTIONAL software fixture publisher',edition:'1',ownerId:plan.reviewerActorId,ownerRole:plan.reviewerRoleId,validationReview:'FICTIONAL software source review only',inputs:[{field:'context.mode',basis:'Declared'}],control:null}
      evidence.stage='author_actual_'+lane+'_qualification_draft'
      const saved=await page.request.post(root+'/events',{headers:{'X-CSRF-TOKEN':user.csrfToken,'Idempotency-Key':randomUUID()},data:{expectedVersion:0,ruleRevision:source.revision,authorityId:draftAuthority,state:'DRAFT',content}})
      evidence.checks.push({stage:evidence.stage,status:saved.status()});if(saved.status()!==200)throw new Error('fixture_qualification_draft_failed')
      const draft=await saved.json();if(draft.state!=='DRAFT'||draft.blockers.length)throw new Error('fixture_incomplete_review_source')
      evidence.stage='request_actual_'+lane+'_qualification_review'
      const review=await page.request.post(root+'/events',{headers:{'X-CSRF-TOKEN':user.csrfToken,'Idempotency-Key':randomUUID()},data:{expectedVersion:draft.version,ruleRevision:source.revision,authorityId:reviewAuthority,state:'IN_REVIEW'}})
      evidence.checks.push({stage:evidence.stage,status:review.status()});if(review.status()!==200||(await review.json()).state!=='IN_REVIEW')throw new Error('fixture_review_failed')
    }
  }finally{await reviewer.close()}
  const writer=await strictContext({baseURL:'https://localhost:5443'})
  try{
    const page=await writer.context.newPage();page.setDefaultTimeout(15000);evidence.stage='writer_login';await login(page,'lab-manager','/app/today')
    const session=await page.request.get('/api/v1/auth/session');if(session.status()!==200)throw new Error('fixture_session_failed')
    const user=await session.json();if(user.actorId!==plan.writerActorId||user.tenantId!==plan.tenantId||!user.authenticated)throw new Error('fixture_actor_mismatch')
    const read=async(path:string,stage:string)=>{evidence.stage=stage;const r=await page.request.get(path);evidence.checks.push({stage,status:r.status()});if(r.status()!==200)throw new Error('fixture_read_failed');return r.json()}
    for(const lane of ['desktop','narrow']){
      const row=observed.lanes[lane],batch=plan.legacyBatches[lane]
      const legacy=await read('/api/v1/vessels/'+plan.vesselId+'/imports/'+batch+'/legacy-protection','actual_'+lane+'_legacy_storage')
      if(legacy.storageState!=='LegacyUnavailable'||legacy.interpretationState!=='Unavailable')throw new Error('fixture_legacy_state_required')
      const q=await read('/api/v1/regulatory-source-governance/'+row.rule.id,'actual_'+lane+'_independent_qualification_assignment')
      if(q.items[0].state!=='IN_REVIEW'||!q.actions.includes('qualify'))throw new Error('fixture_pending_source_required')
      const list=await read('/api/v1/evidence?vesselId='+plan.vesselId+'&offset=0','actual_'+lane+'_recording_evidence')
      const actual=list.items.find((e:{id:string;links:{id:string;kind:string}[]})=>e.id===row.evidence.id&&e.links.some(l=>l.id===row.control.id&&l.kind==='Control'))
      if(!actual||actual.verification!=='UNVERIFIED')throw new Error('fixture_unverified_link_required')
      const noeval=await read('/api/v1/controls/orphans?vesselId='+row.noEvaluationVesselId+'&offset=0','actual_'+lane+'_no_evaluation')
      if(noeval.available!==false)throw new Error('fixture_no_evaluation_required')
      lanes[lane]={POSEIDON_BROWSER_NO_EVALUATION_VESSEL:row.noEvaluationVesselId,POSEIDON_BROWSER_HUMAN_EVIDENCE:actual.id,POSEIDON_BROWSER_QUALIFICATION_RULE:row.rule.id,POSEIDON_BROWSER_LEGACY_CREW_BATCH:batch,POSEIDON_BROWSER_LEGACY_CREW_VESSEL:plan.vesselId}
    }
  }finally{await writer.close()}
  persist('source-bundle-lane-environment.json',{schemaVersion:'poseidon.bundle.signed-lane-fixtures.v1',runId:plan.runId,lanes})
  evidence.status='PRECONDITIONS_ONLY';evidence.stage='complete'
}catch{evidence.status='FAILED'}
finally{persist('source-bundle-legacy-preconditions.json',evidence)}
process.exitCode=evidence.status==='PRECONDITIONS_ONLY'?0:1
