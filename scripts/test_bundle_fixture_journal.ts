// Synthetic transport unit checks only. No host/provider/browser or credentials.
import {test} from 'node:test'
import assert from 'node:assert/strict'
import {mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync} from 'node:fs'
import {tmpdir} from 'node:os'
import {join} from 'node:path'
import {FixtureJournal, verifyFixtureReceipt} from './bundle_fixture_journal.ts'

const actor={actorId:'11111111-1111-1111-1111-111111111111',tenantId:'22222222-2222-2222-2222-222222222222'}
const attempt='33333333-3333-3333-3333-333333333333'
const binding={runId:'synthetic-run',sourceVersion:'synthetic-source',...actor}
const setup=()=>{
  const root=mkdtempSync(join(tmpdir(),'poseidon-fixture-journal-test-'))
  const publish=(name:string,value:unknown)=>writeFileSync(join(root,name),JSON.stringify(value),{flag:'wx',mode:0o600,flush:true})
  const open=(scope:unknown=binding,id?:string)=>new FixtureJournal(root,scope,publish,id)
  return {root,publish,open,close:()=>rmSync(root,{recursive:true,force:true})}
}
const command=(journal:FixtureJournal,send:any,verify:any,options:any={})=>journal.command('synthetic_create',options.identity??actor,'POST','/api/v1/synthetic',{value:options.value??'original'},options.idempotent??true,send,verify)

test('restart after successful receipt performs current read and sends no duplicate mutation',async()=>{
  const s=setup();try{
    let mutations=0,reads=0
    const send=async()=>{mutations++;return {status:200,body:{synthetic:true,id:'original'}}}
    const verify=async(body:any)=>{reads++;assert.equal(body.id,'original')}
    await command(s.open(),send,verify)
    const retained=Object.fromEntries(readdirSync(s.root).map(file=>[file,readFileSync(join(s.root,file),'utf8')]))
    await command(s.open(),send,verify)
    assert.equal(mutations,1);assert.equal(reads,2)
    assert.deepEqual(Object.fromEntries(readdirSync(s.root).map(file=>[file,readFileSync(join(s.root,file),'utf8')])),retained)
  }finally{s.close()}
})

test('transport failure after send retries exact original idempotency key and body',async()=>{
  const s=setup();try{
    const attempts:any[]=[]
    await assert.rejects(command(s.open(),async(intent:any)=>{attempts.push(intent);throw new Error('synthetic lost response')},async()=>{}),/lost response/)
    await command(s.open(),async(intent:any)=>{attempts.push(intent);return {status:200,body:{synthetic:true}}},async()=>{})
    assert.deepEqual(attempts[1],attempts[0]);assert.equal(attempts.length,2)
  }finally{s.close()}
})

test('crash before receipt publication recovers only through same idempotent API command',async()=>{
  const s=setup();try{
    let calls=0,first:any
    const failing=new FixtureJournal(s.root,binding,(name,value)=>{if(name.endsWith('-receipt.json'))throw new Error('synthetic disk failure');s.publish(name,value)})
    await assert.rejects(command(failing,async intent=>{calls++;first=intent;return {status:200,body:{synthetic:true}}},async()=>{}),/disk failure/)
    await command(s.open(),async intent=>{calls++;assert.deepEqual(intent,first);return {status:200,body:{synthetic:true}}},async()=>{})
    assert.equal(calls,2)
  }finally{s.close()}
})

test('unknown non-idempotent send outcome fails closed across restart',async()=>{
  const s=setup();try{
    let sends=0
    const send=async()=>{sends++;throw new Error('synthetic lost upload response')}
    await assert.rejects(command(s.open(),send,async()=>{},{idempotent:false}),/lost upload/)
    await assert.rejects(command(s.open(),send,async()=>{},{idempotent:false}),/nonidempotent_outcome_unknown/)
    assert.equal(sends,1)
  }finally{s.close()}
})

test('changed actor tenant plan source or payload never reuses intent',async()=>{
  const s=setup();try{
    await command(s.open(),async()=>({status:200,body:{synthetic:true}}),async()=>{})
    for(const changed of [{...binding,sourceVersion:'changed'},{...binding,tenantId:attempt}])assert.throws(()=>s.open(changed),/binding_changed/)
    const never=async()=>{throw new Error('unexpected send')}
    await assert.rejects(command(s.open(),never,async()=>{},{value:'changed'}),/intent_changed/)
    await assert.rejects(command(s.open(),never,async()=>{},{identity:{...actor,actorId:attempt}}),/intent_changed/)
    await assert.rejects(command(s.open(),never,async()=>{},{identity:{...actor,tenantId:attempt}}),/intent_changed/)
  }finally{s.close()}
})

test('recorded receipt remains preserved when current read denies or record changed',async()=>{
  const s=setup();try{
    let calls=0
    const send=async()=>{calls++;return {status:200,body:{synthetic:true}}}
    await command(s.open(),send,async()=>{})
    const receipt=readdirSync(s.root).find(f=>f.endsWith('-receipt.json'))!,original=readFileSync(join(s.root,receipt),'utf8')
    await assert.rejects(command(s.open(),send,async()=>{throw new Error('synthetic current access denied')}),/access denied/)
    assert.equal(calls,1);assert.equal(readFileSync(join(s.root,receipt),'utf8'),original)
  }finally{s.close()}
})

test('legacy status-only failure requires explicit new owned attempt and keeps old evidence',()=>{
  const s=setup();try{
    s.publish('source-bundle-preparation.json',{status:'FAILED',stage:'synthetic_after_mutations'})
    assert.throws(()=>s.open(),/requires_explicit_new_uuid/)
    const journal=s.open(binding,attempt)
    assert.equal(journal.manifest.attemptId,attempt)
    assert.equal(JSON.parse(readFileSync(join(s.root,'source-bundle-preparation.json'),'utf8')).status,'FAILED')
    assert.equal(journal.id('asset'),s.open().id('asset'));assert.notEqual(journal.id('asset'),journal.id('plan'))
    assert.throws(()=>s.open(binding,actor.actorId),/binding_changed/)
  }finally{s.close()}
})

test('failed API response has no fabricated success receipt and restarts retain intent',async()=>{
  const s=setup();try{
    let original:any
    await assert.rejects(command(s.open(),async intent=>{original=intent;return {status:503,body:null}},async()=>{}),/http_failed/)
    assert.equal(readdirSync(s.root).filter(f=>f.endsWith('-receipt.json')).length,0)
    await command(s.open(),async intent=>{assert.deepEqual(intent,original);return {status:200,body:{synthetic:true}}},async()=>{})
  }finally{s.close()}
})

test('current record verification detects changed immutable references for each API family',async()=>{
  const vessel='44444444-4444-4444-4444-444444444444'
  const row={id:'synthetic',vesselId:vessel,revision:1,version:7,hash:'original',state:'DRAFT',lifecycle:'DRAFT',verification:'UNVERIFIED'}
  const cases:[string,unknown,unknown][]=[
    ['/api/v1/controls?vesselId='+vessel,row,{items:[{...row,revision:2}]}],
    ['/api/v1/evidence?vesselId='+vessel,row,{items:[{...row,hash:'changed'}]}],
    ['/api/v1/equipment-hierarchy/'+vessel,row,{items:[{...row,version:8}]}],
    ['/api/v1/maintenance-plan/'+vessel,row,{items:[{...row,revision:2}]}],
    ['/api/v1/noon-reports?vesselId='+vessel,row,{...row,hash:'changed'}],
    ['/api/v1/source-drafts',{version:7,current:row},{version:8,current:row}],
    ['/api/v1/requirement-drafts',{version:7,current:row},{version:7,current:{...row,sourceRevision:2}}],
    ['/api/v1/rules',{version:7,current:row},{items:[{version:7,current:{...row,requirementRevision:2}}]}],
  ]
  for(const [path,receipt,current]of cases)await assert.rejects(verifyFixtureReceipt(path,receipt,{},vessel,async()=>current),/current_source_changed/)
})

test('clean file transport remains separate from evidence verification and quarantine stops reuse',async()=>{
  const vessel='44444444-4444-4444-4444-444444444444',path='/api/v1/vessels/'+vessel+'/files'
  const pending={id:'synthetic',vesselId:vessel,name:'synthetic.png',length:12,state:'Pending',checksum:null,revision:null,version:1}
  await verifyFixtureReceipt(path,pending,{checksum:'HASH'},vessel,async()=>({...pending,state:'CleanTransport',checksum:'HASH',revision:1,version:2}))
  await assert.rejects(verifyFixtureReceipt(path,pending,{checksum:'HASH'},vessel,async()=>({...pending,state:'Receiving'})),/file_source_changed/)
  await assert.rejects(verifyFixtureReceipt(path,pending,{checksum:'HASH'},vessel,async()=>({...pending,state:'CleanTransport',checksum:'OTHER',revision:1})),/file_source_changed/)
})
