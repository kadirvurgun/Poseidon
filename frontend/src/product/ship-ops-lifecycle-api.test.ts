import { describe, expect, it } from 'vitest'
import { decodeDowntimeLifecycle, decodeSofFinalization, freezeDowntimeLifecycle, freezeSofFinalize } from './ship-ops-lifecycle-api'
import { shipOpsActor, shipOpsRecordFixture, shipOpsRecordId, shipOpsTenant, shipOpsVessel } from './ship-operation-test-fixtures'
import { freezeShipOpsCommand } from './ship-operations-api'
import { mutableShipOpsFields } from './ship-operation-fields'
import { downtimeLifecycleFixture, sofFinalizationFixture } from './ship-ops-lifecycle-test-fixtures'

describe('T-OPS2-005 SOF finalization contract', () => {
  it('accepts an appointed finalize affordance and freezes acknowledgement of every unresolved reference', () => {
    const view = decodeSofFinalization(sofFinalizationFixture(), shipOpsVessel, shipOpsRecordId)
    const attempt = freezeSofFinalize(view, view.authorities[0].id, ['voyage_id', 'port', 'port_call_id'])
    expect(JSON.parse(attempt.body)).toEqual({ expectedVersion: 7, authorityId: view.authorities[0].id, reasonCode: 'HUMAN_SOF_FINALIZATION', acknowledgedUnresolved: ['port', 'port_call_id', 'voyage_id'] })
    expect(attempt.path).toContain('/finalize?vesselId=')
    expect(() => freezeSofFinalize(view, view.authorities[0].id, ['voyage_id'])).toThrow()
  })
  it('rejects a fabricated signature, a finalize affordance without appointment and a duty outside the source list', () => {
    const base = sofFinalizationFixture()
    expect(() => decodeSofFinalization({ ...base, authorities: [] }, shipOpsVessel, shipOpsRecordId)).toThrow()
    expect(() => decodeSofFinalization({ ...base, authorities: [{ ...base.authorities[0], duty: 'Administrator' }] }, shipOpsVessel, shipOpsRecordId)).toThrow()
    expect(() => decodeSofFinalization({ ...base, qualifications: ['FINAL_IS_INTERNAL_NOT_EXTERNAL_AGREEMENT'] }, shipOpsVessel, shipOpsRecordId)).toThrow()
    const event = { id: '30000000-0000-4000-8000-000000000001', fromRevision: 1, toRevision: 2, actorId: '30000000-0000-4000-8000-000000000002', roleId: null, duty: 'Master', reasonCode: 'HUMAN_SOF_FINALIZATION', acknowledgedUnresolved: [], signatureState: 'SIGNED', recordHash: 'A'.repeat(64), savedAt: '2026-11-19T10:00:00Z' }
    expect(() => decodeSofFinalization({ ...base, canFinalize: false, sofState: 'FINAL', finalRevision: 2, events: [event] }, shipOpsVessel, shipOpsRecordId)).toThrow()
    expect(decodeSofFinalization({ ...base, canFinalize: false, sofState: 'FINAL', finalRevision: 2, events: [{ ...event, signatureState: 'SOF_SIGNATURE_NOT_RECORDED' }] }, shipOpsVessel, shipOpsRecordId).events).toHaveLength(1)
  })
})

describe('T-OPS2-005 post-FINAL amendment', () => {
  it('sends an explicit amendment reason when editing a FINAL SOF revision', () => {
    const base = shipOpsRecordFixture('Port'), record: typeof base = { ...base, fields: { ...base.fields, sof_state: { state: 'Known', value: 'FINAL' } } }
    const fields = Object.fromEntries(mutableShipOpsFields('Port').map(f => [f.key, base.fields[f.key]]))
    expect(JSON.parse(freezeShipOpsCommand('Port', shipOpsVessel, record.id, shipOpsTenant + '/' + shipOpsActor, fields, record, null).body).reasonCode).toBe('POST_FINAL_AMENDMENT')
    expect(JSON.parse(freezeShipOpsCommand('Port', shipOpsVessel, record.id, shipOpsTenant + '/' + shipOpsActor, fields, shipOpsRecordFixture('Port'), null).body).reasonCode).toBe('AUTHORING_CORRECTION')
  })
})

describe('T-OPS2-006 downtime lifecycle contract', () => {
  it('requires an appointment for review actions and never offers verify or close to the recorder', () => {
    const view = decodeDowntimeLifecycle(downtimeLifecycleFixture(), shipOpsVessel, shipOpsRecordId)
    expect(JSON.parse(freezeDowntimeLifecycle(view, 'VERIFY', view.authorities[0].id, null).body)).toMatchObject({ action: 'VERIFY', reasonCode: 'HUMAN_DOWNTIME_LIFECYCLE', expectedVersion: 4 })
    expect(() => freezeDowntimeLifecycle(view, 'VERIFY', null, null)).toThrow()
    expect(() => freezeDowntimeLifecycle(view, 'CLOSE', view.authorities[0].id, null)).toThrow()
    expect(() => decodeDowntimeLifecycle({ ...downtimeLifecycleFixture(), actorIsRecorder: true }, shipOpsVessel, shipOpsRecordId)).toThrow()
    expect(() => decodeDowntimeLifecycle({ ...downtimeLifecycleFixture(), authorities: [] }, shipOpsVessel, shipOpsRecordId)).toThrow()
  })
  it('keeps Loss of Hire a candidate and requires the no-entitlement qualifier', () => {
    expect(() => decodeDowntimeLifecycle({ ...downtimeLifecycleFixture(), lossOfHireCandidate: 'YES' }, shipOpsVessel, shipOpsRecordId)).toThrow()
    expect(() => decodeDowntimeLifecycle({ ...downtimeLifecycleFixture(), qualifications: ['CLAIM_EVIDENCE_LINK_IS_NOT_VERIFICATION'] }, shipOpsVessel, shipOpsRecordId)).toThrow()
    expect(() => decodeDowntimeLifecycle({ ...downtimeLifecycleFixture(), allowedActions: ['APPROVE_CLAIM'] }, shipOpsVessel, shipOpsRecordId)).toThrow()
  })
})
