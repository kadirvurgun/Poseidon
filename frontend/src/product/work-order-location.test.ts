import { describe,expect,it } from 'vitest'
import { readWorkOrderLocation,workOrderLocationPath } from './work-order-location'
import { workOrderVessel,workOrderId,workOrderPlanPinFixture } from './work-order-test-fixtures'
describe('T-S8-003 owning work order route — authored, not executed',()=>{
 it('round-trips only exact resource identity or explicit plan origin selectors',()=>{const pin=workOrderPlanPinFixture(),route={vessel:workOrderVessel,id:null,offset:0,plan:{id:pin.id,revision:pin.revision}};expect(readWorkOrderLocation(workOrderLocationPath(route))).toEqual(route);expect(readWorkOrderLocation('/app/work-order-center')).toEqual({vessel:null,id:null,offset:0})})
 it('rejects duplicate selectors, private values, mixed origins and record context',()=>{const root='/app/work-order-center/'+workOrderVessel;for(const query of ['?id='+workOrderId+'&id='+workOrderId,'?planId='+workOrderId,'?planId='+workOrderId+'&planRevision=0','?id='+workOrderId+'&planId='+workOrderId+'&planRevision=1','?fields=PRIVATE','?offset=100001'])expect(readWorkOrderLocation(root+query).invalid).toBe(true)})
})
