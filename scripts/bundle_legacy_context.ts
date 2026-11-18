// A previous fixture setup may have reached IN_REVIEW before a later read failed.
// Reuse only the exact observed synthetic history, never infer human qualification.
import {createHash} from 'node:crypto'
import {canonical} from './bundle_fixture_journal.ts'

type Expected={ruleId:string;ruleRevision:number;actorId:string;roleId:string;
  draftAuthority:string;reviewAuthority:string;content:Record<string,unknown>}
export function qualificationFixtureState(context:unknown,expected:Expected):'NEW'|'DRAFT'|'IN_REVIEW' {
  const value=context as {items?:unknown[];hasMore?:boolean}
  if(!value||!Array.isArray(value.items)||value.hasMore!==false||value.items.length>2)throw new Error('fixture_source_history_changed')
  if(value.items.length===0)return 'NEW'
  const raw=expected.content.sourceBytesBase64
  if(typeof raw!=='string')throw new Error('fixture_source_history_changed')
  const sourceHash=createHash('sha256').update(Buffer.from(raw,'base64')).digest('hex').toUpperCase()
  const rows=[...value.items].reverse() as Record<string,any>[]
  const nullable=['trigger','impactReview','supersedingRuleId','supersedingRuleRevision','tugCalculation','literalBindings','owningDocuments','classPack','eligibility','ismDependency']
  const expectedInputs=(expected.content.inputs as Record<string,unknown>[]).map(input=>({...input,maxAgeHours:null}))
  for(const [index,row]of rows.entries()){
    const content=row?.content
    if(row?.ruleId!==expected.ruleId||row.ruleRevision!==expected.ruleRevision||row.revision!==index+1||
      !Number.isSafeInteger(row.version)||row.version<1||row.version>4294967295||row.actorId!==expected.actorId||
      row.roleId!==expected.roleId||row.authorityId!==(index===0?expected.draftAuthority:expected.reviewAuthority)||
      row.state!==(index===0?'DRAFT':'IN_REVIEW')||row.sourceHash!==sourceHash||
      typeof row.hash!=='string'||!/^[A-F0-9]{64}$/.test(row.hash)||
      row.priorHash!==(index===0?null:rows[index-1].hash)||!Array.isArray(row.blockers)||row.blockers.length||
      row.policyVersion!=='recorded-qualification-v1'||!content||content.sourceBytesBase64!==null||
      ['publisher','edition','ownerId','ownerRole','validationReview','control'].some(k=>canonical(content[k])!==canonical(expected.content[k]))||
      canonical(content.inputs)!==canonical(expectedInputs)||nullable.some(k=>content[k]!==null&&content[k]!==undefined))
      throw new Error('fixture_source_history_changed')
  }
  return rows.length===1?'DRAFT':'IN_REVIEW'
}
