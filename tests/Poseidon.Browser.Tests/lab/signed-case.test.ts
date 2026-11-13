import {test} from 'node:test'
import assert from 'node:assert/strict'
import {runSignedCase, signedDestination} from './signed-case.ts'

test('each independent journey authenticates its own account instead of reusing the first ticket',async()=>{
  const calls:string[]=[], phases:string[]=[], page={identity:'new-context'}
  const authenticate=async(_page:typeof page,account:string,destination:string)=>{assert.equal(_page,page);calls.push('login:'+account+destination)}
  for(const restricted of [false,false,true])await runSignedCase(page,{restricted,run:async()=>{calls.push('journey')}},authenticate,phase=>phases.push(phase))
  assert.deepEqual(calls,['login:lab-manager/app/today','journey','login:lab-manager/app/today','journey','login:lab-restricted/app/today','journey'])
  assert.deepEqual(phases,['LOGIN','JOURNEY','LOGIN','JOURNEY','LOGIN','JOURNEY'])
})

test('only an explicit crew boundary entry selects its fresh genuine provider account',async()=>{
  const calls:string[]=[],phases:string[]=[],page={identity:'same-owned-page'}
  await runSignedCase(page,{restricted:false,account:'lab-crew-boundary',run:async value=>{
    assert.equal(value,page);calls.push('journey')
  }},async(value,account,destination)=>{
    assert.equal(value,page);calls.push('login:'+account+destination)
  },phase=>phases.push(phase))
  assert.deepEqual(calls,['login:lab-crew-boundary/app/today','journey'])
  assert.deepEqual(phases,['LOGIN','JOURNEY'])
})

test('unknown inherited or restricted account overrides fail before login phase or callback',async()=>{
  const invalid:unknown[]=[undefined,null,'','lab-manager','lab-reviewer','lab-restricted','unknown',true,1,[],{}]
  const entries:unknown[]=[...invalid.map(account=>({restricted:false,account})),
    {restricted:true,account:'lab-crew-boundary'},{restricted:'false',account:'lab-crew-boundary'},
    Object.assign(Object.create({account:'lab-crew-boundary'}),{restricted:false})]
  for(const value of entries){
    let logins=0,journeys=0;const phases:string[]=[]
    const entry=Object.assign(value as object,{run:async()=>{journeys++}}) as {
      restricted:boolean;account?:'lab-crew-boundary';run:()=>Promise<void>
    }
    await assert.rejects(runSignedCase({},entry,async()=>{logins++},phase=>phases.push(phase)),/invalid_signed_account/)
    assert.equal(logins,0);assert.equal(journeys,0);assert.deepEqual(phases,[])
  }
})

test('crew boundary authentication and journey failures are never retried or changed to manager',async()=>{
  for(const failure of ['login','journey']){
    const accounts:string[]=[];let journeys=0
    await assert.rejects(runSignedCase({}, {restricted:false,account:'lab-crew-boundary',run:async()=>{
      journeys++;if(failure==='journey')throw new Error('synthetic journey failure')
    }},async(_page,account)=>{
      accounts.push(account);if(failure==='login')throw new Error('synthetic login failure')
    }))
    assert.deepEqual(accounts,['lab-crew-boundary']);assert.equal(journeys,failure==='login'?0:1)
  }
})
test('failed authentication cannot execute a journey or fall back to a previously signed context',async()=>{
  let journeys=0,logins=0
  await assert.rejects(runSignedCase({}, {restricted:false,run:async()=>{journeys++}},async()=>{logins++;throw new Error('synthetic login failure')}))
  assert.equal(journeys,0);assert.equal(logins,1)
})
test('failed journey is not retried or reclassified as an authentication refresh',async()=>{
  let journeys=0,logins=0;const phases:string[]=[]
  await assert.rejects(runSignedCase({}, {restricted:false,run:async()=>{journeys++;throw new Error('synthetic case failure')}},async()=>{logins++},p=>phases.push(p)))
  assert.equal(journeys,1);assert.equal(logins,1);assert.deepEqual(phases,['LOGIN','JOURNEY'])
})
test('deep local destinations preserve exact record and query context',()=>{
  for(const path of ['/app','/app/today','/app/noon-report/30000000-0000-0000-0000-000000000001?id=70000000-0000-0000-0000-000000000001','/app/fleet/ship/profile'])assert.equal(signedDestination(path),path)
})
test('external encoded traversing or malformed destinations fail before any browser action',()=>{
  for(const path of ['https://example.invalid/app','//example.invalid/app','/application','/app/../other','/app/%2f%2fevil','/app/evil\\path','/app/evil\npath','/app/'+ 'x'.repeat(512)])assert.throws(()=>signedDestination(path),/invalid_signed_destination/)
})
