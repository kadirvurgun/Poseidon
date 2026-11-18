import { describe,expect,it } from 'vitest'
import { downtimeLocationPath,readDowntimeLocation } from './downtime-location'
import { downtimeId,downtimeOriginId,downtimeVessel } from './downtime-test-fixtures'
describe('downtime canonical selectors — authored, not executed',()=>{
 it('roundtrips only exact metadata selectors for each source kind',()=>{for(const kind of ['Defect','WorkOrder','Engine'] as const){const route={vessel:downtimeVessel,id:null,offset:0,origin:{kind,id:downtimeOriginId,revision:2}};expect(readDowntimeLocation(downtimeLocationPath(route))).toEqual(route)}})
 it('rejects duplicated, partial, mixed saved/source and private URL data',()=>{for(const query of ['originKind=Defect&originId='+downtimeOriginId,'originKind=Noon&originId='+downtimeOriginId+'&originRevision=2','id='+downtimeId+'&originKind=Defect&originId='+downtimeOriginId+'&originRevision=2','id='+downtimeId+'&id='+downtimeId,'originKind=Defect&originId='+downtimeOriginId+'&originRevision=0','actorId='+downtimeId,'offset=01'])expect(readDowntimeLocation('/app/operational-downtime/'+downtimeVessel+'?'+query).invalid).toBe(true)})
})
