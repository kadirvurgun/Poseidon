import { createHash } from 'node:crypto'
import { readFileSync,realpathSync,statSync } from 'node:fs'
import { isAbsolute,relative,sep } from 'node:path'
import { expect,type Page } from '@playwright/test'
import { downtimeSourceVersion } from '../../../frontend/src/product/downtime-fields.ts'
import { defectSourceVersion } from '../../../frontend/src/product/defect-fields.ts'
import { workOrderSourceVersion } from '../../../frontend/src/product/work-order-fields.ts'
export type DowntimeSignedFixture={schemaVersion:'poseidon.downtime.signed-fixture.v1';runId:string;tenantId:string;vesselId:string;actorId:string;origin:{kind:'Defect'|'WorkOrder'|'Engine';id:string;revision:number;version:number;hash:string;sourceVersion:string}}
const object=(v:unknown):v is Record<string,unknown>=>!!v&&typeof v==='object'&&!Array.isArray(v)
const exact=(v:unknown,keys:string[]):v is Record<string,unknown>=>object(v)&&Object.keys(v).length===keys.length&&keys.every(key=>Object.hasOwn(v,key))
const guid=(v:unknown):v is string=>typeof v==='string'&&/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(v)&&v!=='00000000-0000-0000-0000-000000000000'
export function requireDowntimeFixture():DowntimeSignedFixture{
 const name=process.env.POSEIDON_BROWSER_DOWNTIME_FIXTURE,privateRoot=process.env.POSEIDON_LAB_PRIVATE
 if(!name||!privateRoot)throw new Error('downtime_signed_fixture_required')
 const root=realpathSync(privateRoot),path=realpathSync(name),child=relative(root,path)
 if(!child||isAbsolute(child)||child==='..'||child.startsWith('..'+sep)||!statSync(path).isFile()||statSync(path).size>8192)throw new Error('downtime_fixture_boundary')
 let value:unknown;try{value=JSON.parse(readFileSync(path,'utf8'))}catch{throw new Error('downtime_fixture_invalid')}
 if(!exact(value,['schemaVersion','runId','tenantId','vesselId','actorId','origin'])||value.schemaVersion!=='poseidon.downtime.signed-fixture.v1'||!['runId','tenantId','vesselId','actorId'].every(key=>guid(value[key]))||!exact(value.origin,['kind','id','revision','version','hash','sourceVersion']))throw new Error('downtime_fixture_invalid')
 const origin=value.origin
 if(!['Defect','WorkOrder','Engine'].includes(String(origin.kind))||!guid(origin.id)||typeof origin.revision!=='number'||!Number.isSafeInteger(origin.revision)||origin.revision<1||typeof origin.version!=='number'||!Number.isSafeInteger(origin.version)||origin.version<1||origin.version>4294967295||typeof origin.hash!=='string'||!/^[a-fA-F0-9]{64}$/.test(origin.hash)||origin.sourceVersion!==(origin.kind==='Defect'?defectSourceVersion:origin.kind==='WorkOrder'?workOrderSourceVersion:'14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3'))throw new Error('downtime_fixture_invalid')
 return value as DowntimeSignedFixture
}
export function downtimeSignedId(fixture:DowntimeSignedFixture,page:Page,purpose:string){const view=page.viewportSize();if(!view||view.width<320)throw new Error('downtime_fixture_lane_required');const h=createHash('sha256').update(fixture.schemaVersion+':'+fixture.runId+':'+(view.width<=420?'narrow':'desktop')+':'+purpose).digest('hex');return h.slice(0,8)+'-'+h.slice(8,12)+'-'+h.slice(12,16)+'-'+h.slice(16,20)+'-'+h.slice(20,32)}
export async function requireDowntimeContext(page:Page,fixture:DowntimeSignedFixture){
 const session=await page.request.get('/api/v1/auth/session');expect(session.status()).toBe(200);const user=await session.json();expect(user.authenticated===true&&user.canRead===true&&user.actorId===fixture.actorId&&user.tenantId===fixture.tenantId).toBe(true);expect(typeof user.csrfToken==='string'&&user.csrfToken.length>16).toBe(true)
 const vessel=await page.request.get('/api/v1/vessels/'+fixture.vesselId);expect(vessel.status()).toBe(200);expect((await vessel.json()).kind).toBe('Ship')
 const root='/api/v1/operational-downtime',capsResult=await page.request.get(root+'/capabilities?vessel='+fixture.vesselId);expect(capsResult.status()).toBe(200);const caps=await capsResult.json();expect(caps.vesselId===fixture.vesselId&&caps.canRead===true&&caps.canWrite===true&&caps.canVerify===false&&caps.canClose===false&&caps.canFinalize===false&&caps.sourceVersion===downtimeSourceVersion).toBe(true);expect(caps.offline).toEqual({supported:false,state:'UNAVAILABLE'});expect(caps.schema).toHaveLength(14);expect(caps.originKinds).toContain(fixture.origin.kind)
 const mutable=caps.schema.filter((field:{readOnly:boolean})=>!field.readOnly);expect(mutable).toHaveLength(11);const fields:Record<string,{state:'Known'|'Missing'|'Unknown';value:string|null}>=Object.fromEntries(mutable.map((field:{key:string})=>[field.key,{state:'Missing',value:null}]))
 const origin=await page.request.get('/api/v1/vessels/'+fixture.vesselId+'/downtime-origins/'+fixture.origin.kind+'/'+fixture.origin.id+'?revision='+fixture.origin.revision);expect(origin.status()).toBe(200);const context=await origin.json();expect(context.pin).toEqual(fixture.origin);expect(context.current===true&&context.qualification==='REVIEW_REQUIRED').toBe(true)
 return {root,user:user as {csrfToken:string},fields,sourceVersion:downtimeSourceVersion}
}
