import{test}from'node:test'
import assert from'node:assert/strict'
import{createHash}from'node:crypto'
import{qualificationFixtureState}from'./bundle_legacy_context.ts'
const content={sourceBytesBase64:Buffer.from('FICTIONAL source; no maritime authority').toString('base64'),publisher:'FICTIONAL',edition:'1',ownerId:'actor',ownerRole:'role',validationReview:'software-only',inputs:[{field:'context.mode',basis:'Declared'}],control:null}
const expected={ruleId:'rule',ruleRevision:1,actorId:'actor',roleId:'role',draftAuthority:'draft-grant',reviewAuthority:'review-grant',content}
const row=(revision:number)=>({ruleId:'rule',ruleRevision:1,revision,version:7,actorId:'actor',roleId:'role',authorityId:revision===1?'draft-grant':'review-grant',state:revision===1?'DRAFT':'IN_REVIEW',sourceHash:createHash('sha256').update(Buffer.from(content.sourceBytesBase64,'base64')).digest('hex').toUpperCase(),hash:revision===1?'A'.repeat(64):'B'.repeat(64),priorHash:revision===1?null:'A'.repeat(64),policyVersion:'recorded-qualification-v1',blockers:[],content:{...content,sourceBytesBase64:null,inputs:[{field:'context.mode',basis:'Declared',maxAgeHours:null}]}})
test('exact existing draft or pending review is observed without manufacturing a qualification',()=>{
 assert.equal(qualificationFixtureState({items:[],hasMore:false},expected),'NEW')
 assert.equal(qualificationFixtureState({items:[row(1)],hasMore:false},expected),'DRAFT')
 assert.equal(qualificationFixtureState({items:[row(2),row(1)],hasMore:false},expected),'IN_REVIEW')
})
test('different actor/source/content, advanced lifecycle and broken lineage cannot be adopted',()=>{
 for(const mutation of[(r:any)=>r.actorId='other',(r:any)=>r.sourceHash='C'.repeat(64),(r:any)=>r.state='IN_FORCE',(r:any)=>r.content.edition='2',(r:any)=>r.priorHash='C'.repeat(64),(r:any)=>r.content.inputs[0].basis='Verified',(r:any)=>r.content.control={requireFile:false}]){
  const current=row(2);mutation(current)
  assert.throws(()=>qualificationFixtureState({items:[current,row(1)],hasMore:false},expected),/source_history_changed/)
 }
})
test('hidden or extra historical events and unresolved blockers fail closed',()=>{
 assert.throws(()=>qualificationFixtureState({items:[row(1)],hasMore:true},expected),/source_history_changed/)
 assert.throws(()=>qualificationFixtureState({items:[row(2),row(1),row(1)],hasMore:false},expected),/source_history_changed/)
 const blocked=row(1);blocked.blockers.push('SourceUnqualified' as never)
 assert.throws(()=>qualificationFixtureState({items:[blocked],hasMore:false},expected),/source_history_changed/)
})
