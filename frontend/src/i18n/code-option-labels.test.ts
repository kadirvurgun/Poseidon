import { describe, expect, it } from 'vitest'
import { changeAppLanguage } from './index'
import { codeOptionLabel } from './labels'
import { defectCodeLabel, defectSchema } from '../product/defect-fields'
import { downtimeCodeLabel, downtimeTypeOptions } from '../product/downtime-fields'
import { shipOpsOptionLabel } from '../product/ship-operation-copy'
import { workOrderCodeLabel, workOrderSchema } from '../product/work-order-fields'

describe('canonical operational option presentation; execution deferred', () => {
  it('changes all four owning option dictionaries without translating their command values or units', async () => {
    const defect = structuredClone(defectSchema())
    const workOrder = structuredClone(workOrderSchema())
    const downtime = [...downtimeTypeOptions]
    await changeAppLanguage('it', false)
    expect(defectCodeLabel('Verify', false)).toBe('Fase di verifica')
    expect(downtimeCodeLabel('Planned Maintenance', false)).toBe('Manutenzione pianificata')
    expect(shipOpsOptionLabel('Pilot Onboard', false)).toBe('Pilota a bordo')
    expect(workOrderCodeLabel('RELEASED', false)).toBe('Dichiarazione della fase di rilascio')
    for (const code of ['ROB', 'VLSFO', 'HFO', 'MGO', 'LNG']) expect(shipOpsOptionLabel(code, false)).toBe(code)
    expect(defectSchema()).toEqual(defect)
    expect(workOrderSchema()).toEqual(workOrder)
    expect(downtimeTypeOptions).toEqual(downtime)
    expect(workOrderSchema().find(field => field.key === 'execution_stage')?.options).toEqual(['RELEASED', 'IN_PROGRESS', 'EVIDENCE_SUBMITTED'])
  })

  it.each(['UNRESOLVED_SOURCE_CODE', 'Unknown', '__proto__', 'constructor', 'toString'])(
    'retains an unknown code as source data instead of resolving inherited members: %s', value => {
      expect(codeOptionLabel(value, 'ar', { Verify: 'verifyPhase' })).toBe(value)
      expect(defectCodeLabel(value, false)).toBe(value)
      expect(downtimeCodeLabel(value, false)).toBe(value)
      expect(shipOpsOptionLabel(value, false)).toBe(value)
      expect(workOrderCodeLabel(value, false)).toBe(value)
    },
  )

  it('preserves the established English and Turkish wording and declaration boundary', () => {
    expect(defectCodeLabel('Verify', true)).toBe('Verify')
    expect(defectCodeLabel('Verify', false)).toBe('Doğrulama aşaması')
    expect(workOrderCodeLabel('RELEASED', true)).toBe('Released phase declaration')
    expect(workOrderCodeLabel('RELEASED', false)).toBe('Serbest bırakma aşaması beyanı')
    expect(shipOpsOptionLabel('NONE', false)).toBe('Fark beyan edilmedi')
  })
})
