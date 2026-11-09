// DEV-BUNDLE-FIXTURE-01; actual isolated HTTPS + provider + audited API only.
// No copied sessions, fulfilled responses, verified evidence or production approvals.
import { createHash, randomUUID } from 'node:crypto'
import { readFileSync, realpathSync, renameSync, writeFileSync } from 'node:fs'
import { isAbsolute, relative, resolve, sep } from 'node:path'
import { strictContext } from '../tests/Poseidon.Browser.Tests/lab/strict-browser.ts'
import { login } from '../tests/Poseidon.Browser.Tests/lab/journeys.ts'

const privateRoot = realpathSync(process.env.POSEIDON_LAB_PRIVATE!)
const expectedRoot = realpathSync(resolve(import.meta.dirname, '../.runtime/oidc-lab'))
if (privateRoot !== expectedRoot) throw new Error('fixture_boundary')
const plan = JSON.parse(readFileSync(resolve(privateRoot, 'source-bundle-plan.json'), 'utf8'))
const config = JSON.parse(readFileSync(resolve(privateRoot, 'appsettings.json'), 'utf8'))
const uuid = (v: unknown): v is string => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(v) && v !== '00000000-0000-0000-0000-000000000000'
if (plan.schemaVersion !== 'poseidon.bundle-fixture-plan.v1' || !['runId','tenantId','vesselId','foreignTenantId','foreignVesselId','writerActorId','reviewerActorId','restrictedActorId','foreignActorId'].every(k => uuid(plan[k]))) throw new Error('fixture_plan_invalid')
const environment: Record<string,string> = {}
type FixturePin={id:string;revision:number;version:number}
type ObservedLane={noEvaluationVesselId:string;control:FixturePin;evidence:FixturePin;rule?:FixturePin&{sourceId:string;sourceRevision:number}}
const observed = {schemaVersion:'poseidon.bundle-observed-sources.v1',runId:plan.runId,lanes:{} as Record<string,ObservedLane>}
const evidence = { schemaVersion: 'poseidon.bundle-fixture-preparation.v1', status: 'RUNNING', stage: 'begin', httpChecks: [] as { stage: string; status: number }[], fixtureCount: 0, blockers: [] as string[], accepted: false }
const persist = (name: string, value: unknown) => {
  if (!/^[a-z][a-z0-9-]*\.json$/.test(name)) throw new Error('fixture_name_invalid')
  const file = resolve(privateRoot, name), child = relative(privateRoot, file)
  if (!child || isAbsolute(child) || child.startsWith('..' + sep)) throw new Error('fixture_path_invalid')
  const pending = resolve(privateRoot, '.' + randomUUID() + '.json')
  writeFileSync(pending, JSON.stringify(value), { flag: 'wx', mode: 0o600 }); renameSync(pending, file)
  return file
}
const fixture = (key: string, file: string, value: unknown) => { environment[key] = persist(file, value); evidence.fixtureCount++ }
const common = { runId: plan.runId, tenantId: plan.tenantId, vesselId: plan.vesselId, actorId: plan.writerActorId }
const owned = await strictContext({ baseURL: 'https://localhost:5443' })
try {
  const page = await owned.context.newPage(); page.setDefaultTimeout(15000)
  evidence.stage = 'writer_login'; await login(page, 'lab-manager', '/app/today')
  const read = async (path: string, stage: string) => {
    evidence.stage = stage; const r = await page.request.get(path); evidence.httpChecks.push({stage,status:r.status()})
    if (r.status() !== 200) throw new Error('fixture_http_failed')
    return await r.json()
  }
  const user = await read('/api/v1/auth/session', 'actual_writer_session')
  if (user.authenticated !== true || user.actorId !== plan.writerActorId || user.tenantId !== plan.tenantId || typeof user.csrfToken !== 'string' || user.csrfToken.length < 16) throw new Error('fixture_actual_actor_mismatch')
  const vessel = await read('/api/v1/vessels/' + plan.vesselId, 'actual_ship')
  if (vessel.id !== plan.vesselId || vessel.kind !== 'Ship') throw new Error('fixture_actual_ship_required')
  const post = async (path: string, body: unknown, stage: string) => {
    evidence.stage = stage
    const r = await page.request.post(path, {headers:{'X-CSRF-TOKEN':user.csrfToken,'Idempotency-Key':randomUUID()},data:body})
    evidence.httpChecks.push({stage,status:r.status()}); if (r.status() !== 200) throw new Error('fixture_http_failed')
    return await r.json()
  }
  for(const [route,key,query] of [['certificate-command-center','canWriteVessel','vesselId'],['renewal-workflow','canWrite','vesselId'],['operational-downtime','canWrite','vessel'],['defects','canWrite','vessel'],['work-orders','canWrite','vessel'],['fuel-bunker-logs','canWrite','vesselId'],['port-activity-sof','canWrite','vesselId']] as const) {
    const caps=await read('/api/v1/'+route+'/capabilities?'+query+'='+plan.vesselId,'actual_'+route+'_writer_capability')
    if(caps[key]!==true)throw new Error('fixture_writer_capability_required')
  }
  for(const lane of ['desktop','narrow']) {
    const extra=await post('/api/v1/vessels',{code:'SYNTHETIC-'+lane+'-'+plan.runId.slice(0,8),name:'FICTIONAL no-evaluation '+lane+' software fixture',kind:'Ship',companyId:vessel.companyId},'create_actual_'+lane+'_unevaluated_ship')
    const actual=await read('/api/v1/vessels/'+extra.id,'read_actual_'+lane+'_unevaluated_ship')
    const orphans=await read('/api/v1/controls/orphans?vesselId='+actual.id+'&offset=0','actual_'+lane+'_unevaluated_orphans')
    if(actual.kind!=='Ship'||orphans.available!==false||orphans.reason!=='AUTHORITATIVE_APPLICABILITY_UNAVAILABLE')throw new Error('fixture_unqualified_ship_required')
    const control=await post('/api/v1/controls?vesselId='+plan.vesselId,{expectedVersion:0,content:{title:'FICTIONAL '+lane+' human recording control '+plan.runId},requirements:[],links:[],file:null,reasonCode:'INITIAL_AUTHORING'},'create_actual_'+lane+'_recording_control')
    const evidence=await post('/api/v1/evidence?vesselId='+plan.vesselId,{expectedVersion:0,content:{title:'FICTIONAL '+lane+' human recording evidence '+plan.runId,provenance:'FICTIONAL incomplete software declaration; no operational evidence'},requirements:[],links:[{kind:'Control',id:control.id,revision:control.revision,expectedVersion:control.version}],file:null,reasonCode:'INITIAL_AUTHORING'},'create_actual_'+lane+'_recording_evidence')
    if(control.lifecycle!=='DRAFT'||evidence.verification!=='UNVERIFIED')throw new Error('fixture_unverified_draft_required')
    observed.lanes[lane]={noEvaluationVesselId:actual.id,control:{id:control.id,revision:control.revision,version:control.version},evidence:{id:evidence.id,revision:evidence.revision,version:evidence.version}}
  }
  const id = () => randomUUID()
  const assetId = id(), asset = await post('/api/v1/equipment-hierarchy/'+plan.vesselId, {id:assetId,baseVersion:null,parentId:null,code:'SYNTHETIC-FIXTURE',name:'FICTIONAL software fixture equipment',level:'Equipment',systemFamily:null,equipmentType:null,criticality:null,maker:null,model:null,serialNumber:null,location:null,commissionDate:null,responsibleRoleId:null,notes:'FICTIONAL unverified test declaration'}, 'create_actual_asset')
  const assetContext = await read('/api/v1/vessels/'+plan.vesselId+'/defect-asset-context/'+assetId+'?revision='+asset.revision,'actual_asset_context')
  if (assetContext.current !== true || assetContext.pin.id !== assetId || assetContext.pin.revision !== asset.revision || assetContext.qualification !== 'REVIEW_REQUIRED') throw new Error('fixture_asset_pin_invalid')
  const planId = id(), maintenance = await post('/api/v1/maintenance-plan/'+plan.vesselId,{id:planId,baseVersion:null,assetId,jobId:null,ownerRoleId:null,inputs:{triggerType:null,timeZone:'UTC',provenance:'FICTIONAL incomplete software fixture'},referenceAt:new Date().toISOString()},'create_actual_maintenance_plan')
  const planContext = await read('/api/v1/vessels/'+plan.vesselId+'/work-order-plan-origins/'+planId+'?revision='+maintenance.revision,'actual_plan_context')
  if (planContext.current !== true || planContext.pin.id !== planId || planContext.pin.revision !== maintenance.revision) throw new Error('fixture_plan_pin_invalid')
  let noon: unknown, engine: unknown
  for (const [kind,route] of [['Noon','noon-reports'],['Engine','engine-daily-logs']] as const) {
    const recordId=id(), row=await post('/api/v1/'+route+'?vesselId='+plan.vesselId,{id:recordId,expectedVersion:0,fields:{},reasonCode:'INITIAL_AUTHORING'},'create_actual_'+kind.toLowerCase())
    if (row.id !== recordId || row.revision !== 1 || row.state !== 'DRAFT') throw new Error('fixture_daily_pin_invalid')
    const origin=await read('/api/v1/vessels/'+plan.vesselId+'/ship-operation-origins/'+kind+'/'+recordId+'?revision='+row.revision,'actual_'+kind.toLowerCase()+'_context')
    if (origin.current !== true || origin.pin.id !== recordId || origin.pin.kind !== kind) throw new Error('fixture_origin_invalid')
    if(kind==='Noon')noon=origin.pin;else engine=origin.pin
  }
  const files: Record<string,{id:string;revision:number;checksum:string}>={}
  const bytes=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==','base64')
  const checksum=createHash('sha256').update(bytes).digest('hex').toUpperCase()
  for(const lane of ['desktop','narrow']) {
    const root='/api/v1/vessels/'+plan.vesselId+'/files'
    const staged=await post(root,{name:'synthetic-'+plan.runId+'-'+lane+'.png',mime:'image/png',length:bytes.length,checksum},'stage_actual_'+lane+'_file')
    evidence.stage='upload_actual_'+lane+'_file'
    const upload=await page.request.put(root+'/'+staged.id+'/bytes',{headers:{'X-CSRF-TOKEN':user.csrfToken,'Content-Type':'image/png'},data:bytes})
    evidence.httpChecks.push({stage:evidence.stage,status:upload.status()});if(upload.status()!==200)throw new Error('fixture_file_upload_failed')
    const file=await read(root+'/'+staged.id,'read_actual_'+lane+'_clean_file')
    if(file.state!=='CleanTransport'||file.checksum!==checksum||file.revision!==1||!uuid(file.id))throw new Error('fixture_clean_file_required')
    files[lane]={id:file.id,revision:file.revision,checksum:file.checksum}
  }
  fixture('POSEIDON_BROWSER_DAILY_FIXTURE','daily-fixture.json',{schemaVersion:'poseidon.daily.signed-fixture.v1',sourceVersion:plan.sourceVersion,runId:plan.runId,tenantId:plan.tenantId,vesselId:plan.vesselId,foreignTenantId:plan.foreignTenantId,foreignVesselId:plan.foreignVesselId,writerActorId:plan.writerActorId,reviewerActorId:plan.reviewerActorId,restrictedActorId:plan.restrictedActorId,foreignActorId:plan.foreignActorId,reviewerAccount:plan.reviewerAccount,foreignAccount:plan.foreignAccount})
  fixture('POSEIDON_BROWSER_CERTIFICATE_FIXTURE','certificate-fixture.json',{schemaVersion:'poseidon.certificate.signed-fixture.v1',...common,ownerRoleId:null,files})
  fixture('POSEIDON_BROWSER_SHIP_OPERATIONS_FIXTURE','ship-operations-fixture.json',{schemaVersion:'poseidon.ship-operations.signed-fixture.v1',...common,origin:noon})
  fixture('POSEIDON_BROWSER_DEFECT_FIXTURE','defect-fixture.json',{schemaVersion:'poseidon.defect.signed-fixture.v1',...common,assetId,assetRevision:asset.revision})
  fixture('POSEIDON_BROWSER_WORK_ORDER_FIXTURE','work-order-fixture.json',{schemaVersion:'poseidon.work-order.signed-fixture.v1',...common,planId,planRevision:maintenance.revision})
  fixture('POSEIDON_BROWSER_DOWNTIME_FIXTURE','downtime-fixture.json',{schemaVersion:'poseidon.downtime.signed-fixture.v1',...common,origin:engine})
  const extraction=config.CertificateExtraction
  if(extraction?.Enabled===true && typeof extraction.ModelVersion==='string' && typeof extraction.PromptVersion==='string' && Array.isArray(extraction.Scopes) && extraction.Scopes.some((s:{TenantId:string;ActorId:string;VesselId:string;Scope:string})=>s.TenantId===plan.tenantId&&s.ActorId===plan.writerActorId&&s.VesselId===plan.vesselId&&s.Scope==='Vessel')) {
    fixture('POSEIDON_BROWSER_CERTIFICATE_EXTRACTION_FIXTURE','certificate-extraction-fixture.json',{schemaVersion:'poseidon.certificate-extraction.signed-fixture.v1',...common,modelVersion:extraction.ModelVersion,promptVersion:extraction.PromptVersion,files})
  } else evidence.blockers.push('EXPLICIT_EXTRACTION_PROTOCOL_POLICY_REQUIRED')
  for(const [account,actor,tenant] of [[plan.reviewerAccount,plan.reviewerActorId,plan.tenantId],[plan.foreignAccount,plan.foreignActorId,plan.foreignTenantId],['lab-restricted',plan.restrictedActorId,plan.tenantId]]) {
    const other=await strictContext({baseURL:'https://localhost:5443'})
    try {
      const signed=await other.context.newPage();signed.setDefaultTimeout(15000);evidence.stage='actual_'+account+'_login';await login(signed,account,'/app/today')
      const response=await signed.request.get('/api/v1/auth/session');evidence.httpChecks.push({stage:evidence.stage,status:response.status()})
      if(response.status()!==200)throw new Error('fixture_session_failed');const actual=await response.json()
      if(actual.authenticated!==true||actual.actorId!==actor||actual.tenantId!==tenant)throw new Error('fixture_actual_actor_mismatch')
      if(account===plan.reviewerAccount) {
        const author=async(path:string,data:unknown,stage:string)=>{evidence.stage=stage;const r=await signed.request.post('/api/v1/'+path,{headers:{'X-CSRF-TOKEN':actual.csrfToken,'Idempotency-Key':randomUUID()},data});evidence.httpChecks.push({stage,status:r.status()});if(r.status()!==200)throw new Error('fixture_reviewer_authoring_failed');return r.json()}
        for(const lane of ['desktop','narrow']) {
          const known=(value:string)=>({state:'Known',value})
          const source=await author('source-drafts',{expectedVersion:0,content:{title:known('FICTIONAL '+lane+' software source '+plan.runId),authority:known('FICTIONAL publisher; no maritime authority'),locator:known('FICTIONAL/'+plan.runId+'/'+lane)}},'create_actual_'+lane+'_source')
          const requirement=await author('requirement-drafts',{expectedVersion:0,content:{instrument:known('FICTIONAL software requirement'),amendment:known('1')},sourceId:source.current.id,sourceRevision:source.current.revision},'create_actual_'+lane+'_requirement')
          const rule=await author('rules',{expectedVersion:0,contract:'09.03:309afa07b393fb96556a0ea4ec70098b1e2b8648ab6daec181f5a81ce9ac8825',rule:{ruleId:'SYNTHETIC-FOUNDATION-'+lane+'-'+plan.runId,version:1,source:{sourceId:source.current.id,locator:'FICTIONAL/'+plan.runId+'/'+lane,authorityLayer:'PRODUCT_CONTROL'},lifecycle:{state:'DRAFT',effectiveFrom:'2000-01-01'},scope:{},when:{field:'context.mode',operator:'EXISTS'},then:{result:'APPLICABLE'},unknownPolicy:'REVIEW_REQUIRED'},sourceId:source.current.id,sourceRevision:source.current.revision,requirementId:requirement.current.id,requirementRevision:requirement.current.revision,reasonCode:'INITIAL_AUTHORING'},'create_actual_'+lane+'_rule')
          observed.lanes[lane].rule={id:rule.current.id,revision:rule.current.revision,version:rule.version,sourceId:source.current.id,sourceRevision:source.current.revision}
        }
      }
      if(account===plan.foreignAccount){const ship=await signed.request.get('/api/v1/vessels/'+plan.foreignVesselId);if(ship.status()!==200||(await ship.json()).kind!=='Ship')throw new Error('fixture_foreign_ship_required')}
    } finally {await other.close()}
  }
  persist('source-bundle-observed.json',observed)
  persist('source-bundle-environment.json',environment)
  evidence.status=evidence.blockers.length?'BLOCKED':'PRECONDITIONS_ONLY';evidence.stage='complete'
} catch {evidence.status='FAILED'}
finally {await owned.close();persist('source-bundle-preparation.json',evidence)}
process.exitCode=evidence.status==='PRECONDITIONS_ONLY'?0:1
