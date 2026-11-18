import { describe, expect, it } from 'vitest'
import { decodeWorkOrderPlanContext, decodeWorkOrderCapabilities, decodeWorkOrderRecord, freezeWorkOrderCommand, validWorkOrderPlanPin, workOrderInputErrors } from './work-order-api'
import { emptyWorkOrderFields } from './work-order-fields'
import { workOrderActor, workOrderPlanFixture, workOrderPlanPinFixture, workOrderCapsFixture, workOrderId, workOrderRecordFixture, workOrderTenant, workOrderVessel } from './work-order-test-fixtures'
describe('T-S8-003 owning work order contracts — authored, not executed', () => {
  it('captures incomplete missing and unknown facts without invented references or authority', () => {
    const fields = emptyWorkOrderFields(); fields.actual_finish = { state:'Unknown',value:null }
    const attempt = freezeWorkOrderCommand(workOrderVessel,workOrderId,workOrderTenant+'/'+workOrderActor,fields,null,null), body = JSON.parse(attempt.body)
    expect(body.fields.equipment_id).toEqual({state:'Missing',value:null});expect(body.fields.actual_finish).toEqual({state:'Unknown',value:null});expect(Object.keys(body.fields)).toHaveLength(25);expect(Object.keys(body).sort()).toEqual(['expectedVersion','fields','id','plan','sourceVersion'])
  })
  it('keeps release and evidence-submitted phase literals unassessed and rejects approved states', () => {
    const row = workOrderRecordFixture(); row.fields.execution_stage={state:'Known',value:'RELEASED'};row.fields.verification_result={state:'Known',value:'Verified claim; unassessed'}
    expect(decodeWorkOrderRecord(row,workOrderVessel).state).toBe('DRAFT');expect(decodeWorkOrderRecord(row,workOrderVessel).qualification).toBe('REVIEW_REQUIRED')
    for(const state of ['RELEASED','Ready','Verified','Closed']) expect(()=>decodeWorkOrderRecord({...row,state},workOrderVessel)).toThrow()
    for(const flag of ['canRelease','canSubmitEvidence','canVerify','canClose']) expect(()=>decodeWorkOrderCapabilities({...workOrderCapsFixture(),[flag]:true},workOrderVessel)).toThrow()
  })
  it('rejects known unresolved person/catalog/evidence references instead of accepting arbitrary IDs', () => {
    for(const key of ['equipment_id','assigned_to','executed_by','photo_evidence','document_evidence','verified_by','priority'] as const) {const fields=emptyWorkOrderFields();fields[key]={state:'Known',value:workOrderActor};expect(workOrderInputErrors(fields)[key]).toBeDefined()}
    expect(()=>decodeWorkOrderCapabilities({...workOrderCapsFixture(),schema:workOrderCapsFixture().schema.map(field=>field.key==='priority'?{...field,knownUnavailable:false,options:['High']}:field)},workOrderVessel)).toThrow()
  })
  it('preserves date-only boundaries and decimal lexical values without meter arithmetic', () => {
    const fields=emptyWorkOrderFields();fields.planned_start={state:'Known',value:'2028-02-29'};fields.planned_finish={state:'Known',value:'2028-03-01'};fields.meter_before={state:'Known',value:'00012.000'};fields.meter_after={state:'Unknown',value:null};expect(workOrderInputErrors(fields)).toEqual({})
    fields.planned_finish={state:'Known',value:'2028-02-28'};expect(workOrderInputErrors(fields).planned_finish).toBeDefined();fields.planned_finish={state:'Known',value:'2027-02-29'};expect(workOrderInputErrors(fields).planned_finish).toBeDefined();fields.planned_finish={state:'Known',value:'2028-03-01T00:00:00Z'};expect(workOrderInputErrors(fields).planned_finish).toBeDefined()
  })
  it('pins exact current owning maintenance sources and rejects private expansion or guessed versions', () => {
    const pin=workOrderPlanPinFixture(),context=workOrderPlanFixture();expect(decodeWorkOrderPlanContext(context,workOrderVessel,pin.id,pin.revision,pin).pin).toEqual(pin)
    expect(()=>decodeWorkOrderPlanContext({...context,current:false},workOrderVessel,pin.id)).toThrow();expect(()=>decodeWorkOrderPlanContext(context,workOrderVessel,pin.id,pin.revision,{...pin,version:pin.version+1})).toThrow();expect(()=>decodeWorkOrderPlanContext({...context,asset:{...context.asset,notes:'private'}},workOrderVessel,pin.id)).toThrow();expect(validWorkOrderPlanPin({...pin,sourceVersion:'guessed'})).toBe(false)
  })
})
