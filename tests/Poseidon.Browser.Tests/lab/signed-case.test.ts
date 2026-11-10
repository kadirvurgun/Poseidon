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
