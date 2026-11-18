import { expect, it } from 'vitest'
import { defectSourceVersion, downtimeSourceVersion, workOrderSourceVersion } from '../contracts/operational-source-pins'
import { decodeDefectCapabilities, freezeDefectCommand } from './defect-api'
import { decodeDowntimeCapabilities, freezeDowntimeCommand } from './downtime-api'
import { decodeWorkOrderCapabilities, freezeWorkOrderCommand } from './work-order-api'
import { emptyDefectFields } from './defect-fields'
import { emptyDowntimeFields } from './downtime-fields'
import { emptyWorkOrderFields } from './work-order-fields'
import { defectActor, defectCapsFixture, defectId, defectTenant, defectVessel } from './defect-test-fixtures'
import { downtimeActor, downtimeCapsFixture, downtimeId, downtimeTenant, downtimeVessel } from './downtime-test-fixtures'
import { workOrderActor, workOrderCapsFixture, workOrderId, workOrderTenant, workOrderVessel } from './work-order-test-fixtures'

// GEN-S01: exercise the actual UI capability decoder and captured wire body.
// No duplicate source-pin literals or test-only replacement contracts.
const contracts = [
  {
    name: 'Defect', pin: defectSourceVersion,
    capability: (pin: string) => decodeDefectCapabilities({ ...defectCapsFixture(), sourceVersion: pin }, defectVessel),
    body: () => freezeDefectCommand(defectVessel, defectId, `${defectTenant}/${defectActor}`, emptyDefectFields(), null, null).body,
  },
  {
    name: 'Downtime', pin: downtimeSourceVersion,
    capability: (pin: string) => decodeDowntimeCapabilities({ ...downtimeCapsFixture(), sourceVersion: pin }, downtimeVessel),
    body: () => freezeDowntimeCommand(downtimeVessel, downtimeId, `${downtimeTenant}/${downtimeActor}`, emptyDowntimeFields(), null, null).body,
  },
  {
    name: 'Work order', pin: workOrderSourceVersion,
    capability: (pin: string) => decodeWorkOrderCapabilities({ ...workOrderCapsFixture(), sourceVersion: pin }, workOrderVessel),
    body: () => freezeWorkOrderCommand(workOrderVessel, workOrderId, `${workOrderTenant}/${workOrderActor}`, emptyWorkOrderFields(), null, null).body,
  },
] as const

it.each(contracts)('$name UI accepts and emits the shared canonical pin and rejects a changed capability pin', contract => {
  expect(contract.capability(contract.pin).sourceVersion).toBe(contract.pin)
  expect(JSON.parse(contract.body()).sourceVersion).toBe(contract.pin)
  expect(() => contract.capability(contract.pin + ':changed')).toThrow()
})
