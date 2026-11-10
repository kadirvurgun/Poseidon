// DEV-BUNDLE-FIXTURE-01: private publish-once software-fixture intents/receipts.
// No authenticated response is synthesized, and unknown non-idempotent writes stop.
import {createHash, randomUUID} from 'node:crypto'
import {existsSync, lstatSync, readFileSync, realpathSync} from 'node:fs'
import {resolve, sep} from 'node:path'

export const canonical = (value: unknown): string => {
  if (Array.isArray(value)) return '['+value.map(canonical).join(',')+']'
  if (value !== null && typeof value === 'object') return '{'+Object.entries(value).sort(([a],[b])=>a.localeCompare(b)).map(([k,v])=>JSON.stringify(k)+':'+canonical(v)).join(',')+'}'
  return JSON.stringify(value)
}
export const digest = (value: unknown) => createHash('sha256').update(canonical(value)).digest('hex')
export const fixtureUuid = (attempt: string, key: string) => {
  const hex=createHash('sha256').update('poseidon.bundle.fixture-journal.v1:'+attempt+':'+key).digest('hex').slice(0,32)
  return hex.slice(0,8)+'-'+hex.slice(8,12)+'-'+hex.slice(12,16)+'-'+hex.slice(16,20)+'-'+hex.slice(20)
}
const uuid=(value:unknown):value is string=>typeof value==='string'&&/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value)&&value!=='00000000-0000-0000-0000-000000000000'
type Publish=(name:string,value:unknown)=>unknown
type Identity={actorId:string;tenantId:string}
type Intent={schemaVersion:string;attemptId:string;stage:string;identity:Identity;method:string;path:string;body:unknown;key:string;idempotent:boolean}
type Manifest={schemaVersion:string;attemptId:string;bindingSha256:string;createdAt:string}
export type WireResult={status:number;body:unknown}
export class FixtureJournal {
  readonly root:string
  readonly manifest:Manifest
  readonly publish:Publish
  constructor(root:string,binding:unknown,publish:Publish,explicitAttempt?:string) {
    this.root=realpathSync(root);this.publish=publish
    const file='source-bundle-provision-attempt.json'
    const prior=this.load(file)
    if(prior!==undefined) {
      const m=prior as Manifest
      if(m.schemaVersion!=='poseidon.bundle.fixture-attempt.v1'||!uuid(m.attemptId)||m.bindingSha256!==digest(binding)||!Number.isFinite(Date.parse(m.createdAt))||explicitAttempt!==undefined&&explicitAttempt!==m.attemptId)throw new Error('fixture_attempt_binding_changed')
      this.manifest=m
    } else {
      // A status-only legacy report has no receipts: never adopt its orphan drafts.
      if(this.load('source-bundle-preparation.json')!==undefined&&!uuid(explicitAttempt))throw new Error('fixture_legacy_attempt_requires_explicit_new_uuid')
      if(explicitAttempt!==undefined&&!uuid(explicitAttempt))throw new Error('fixture_attempt_uuid_invalid')
      this.manifest={schemaVersion:'poseidon.bundle.fixture-attempt.v1',attemptId:explicitAttempt??randomUUID(),bindingSha256:digest(binding),createdAt:new Date().toISOString()}
      publish(file,this.manifest)
    }
  }
  load(name:string):unknown {
    if(!/^[a-z][a-z0-9-]*\.json$/.test(name))throw new Error('fixture_journal_name_invalid')
    const file=resolve(this.root,name)
    if(!existsSync(file))return undefined
    if(lstatSync(file).isSymbolicLink()||!lstatSync(file).isFile()||lstatSync(file).size>2097152||!realpathSync(file).startsWith(this.root+sep))throw new Error('fixture_journal_boundary')
    return JSON.parse(readFileSync(file,'utf8'))
  }
  id(key:string){return fixtureUuid(this.manifest.attemptId,key)}
  async command(stage:string,identity:Identity,method:string,path:string,body:unknown,idempotent:boolean,
    send:(intent:Intent)=>Promise<WireResult>,verify:(body:unknown)=>Promise<void>):Promise<unknown> {
    if(!/^[a-z][a-z0-9_]*$/.test(stage)||!uuid(identity.actorId)||!uuid(identity.tenantId)||!path.startsWith('/api/v1/'))throw new Error('fixture_command_invalid')
    const prefix='source-bundle-command-'+stage.replaceAll('_','-')
    const intent:Intent={schemaVersion:'poseidon.bundle.fixture-intent.v1',attemptId:this.manifest.attemptId,stage,identity,method,path,body,key:this.id(stage),idempotent}
    const previous=this.load(prefix+'-intent.json')
    if(previous!==undefined&&canonical(previous)!==canonical(intent))throw new Error('fixture_intent_changed')
    if(previous===undefined)this.publish(prefix+'-intent.json',intent)
    const receipt=this.load(prefix+'-receipt.json') as {intentSha256:string;status:number;body:unknown}|undefined
    if(receipt!==undefined) {
      if(receipt.intentSha256!==digest(intent)||receipt.status!==200)throw new Error('fixture_receipt_invalid')
      await verify(receipt.body) // A receipt alone never establishes current source access.
      return receipt.body
    }
    const sent=prefix+'-sent.json'
    const marker=this.load(sent)
    if(marker!==undefined&&canonical(marker)!==canonical({intentSha256:digest(intent)}))throw new Error('fixture_sent_marker_invalid')
    if(marker!==undefined&&!idempotent)throw new Error('fixture_nonidempotent_outcome_unknown')
    if(marker===undefined)this.publish(sent,{intentSha256:digest(intent)})
    // Every retry uses this immutable original key and payload. Never retry with
    // a new identity/key or infer a response from a list or local declaration.
    const result=await send(intent)
    if(result.status!==200)throw new Error('fixture_http_failed')
    this.publish(prefix+'-receipt.json',{intentSha256:digest(intent),status:result.status,body:result.body})
    await verify(result.body)
    return result.body
  }
}

// Every reused receipt is checked against an actual current, authorized API read.
// The callbacks are the signed browser API context in the producer, never routing
// interception or a fabricated session. Unit tests use a synthetic transport only.
export async function verifyFixtureReceipt(path:string,receipt:unknown,body:unknown,vessel:string,read:(path:string)=>Promise<any>) {
  const row=receipt as any, request=body as any
  const match=(actual:any,expected:any,keys:string[])=>{
    if(!actual||keys.some(k=>actual[k]!==expected[k]))throw new Error('fixture_current_source_changed')
  }
  if(path==='/api/v1/vessels') {
    const current=await read(path+'/'+row.id)
    match(current,row,['id','version','companyId','code','name','kind']);match(current,request,['companyId','code','name','kind'])
  } else if(/^\/api\/v1\/(controls|evidence)\?/.test(path)) {
    const current=await read(path.split('?')[0]+'/'+row.id+'/versions?vesselId='+vessel+'&offset=0')
    match(current.items?.[0],row,['id','revision','version','hash','lifecycle','verification'])
  } else if(/^\/api\/v1\/(equipment-hierarchy|maintenance-plan)\//.test(path)) {
    const current=await read(path), actual=current.items?.find((r:any)=>r.id===row.id)
    match(actual,row,['id','revision','version'])
  } else if(/^\/api\/v1\/(noon-reports|engine-daily-logs)\?/.test(path)) {
    const current=await read(path.split('?')[0]+'/'+row.id+'?vesselId='+vessel)
    match(current,row,['id','vesselId','revision','version','state','hash'])
  } else if(/^\/api\/v1\/vessels\/[^/]+\/files(?:\/[^/]+\/bytes)?$/.test(path)) {
    const root=path.replace(/\/[^/]+\/bytes$/,'')
    const current=await read(root+'/'+row.id)
    match(current,row,['id','vesselId','name','length'])
    if(current.vesselId!==vessel||!['Pending','CleanTransport'].includes(current.state))throw new Error('fixture_file_source_changed')
    if(current.state==='CleanTransport'&&(current.checksum!==(request.checksum??row.checksum)||current.revision!==1))throw new Error('fixture_file_source_changed')
    if(path.endsWith('/bytes'))match(current,row,['state','checksum','revision','version'])
  } else if(/^\/api\/v1\/(source-drafts|requirement-drafts)$/.test(path)) {
    const current=await read(path+'/'+row.current.id)
    match(current,row,['version']);match(current.current,row.current,['id','revision','hash','sourceId','sourceRevision','lifecycle'])
  } else if(path==='/api/v1/rules') {
    const current=await read(path+'/'+row.current.id+'/versions?offset=0')
    match(current.items?.[0],row,['version']);match(current.items?.[0]?.current,row.current,['id','revision','hash','sourceId','sourceRevision','requirementId','requirementRevision','lifecycle'])
  } else throw new Error('fixture_receipt_reader_unavailable')
}
