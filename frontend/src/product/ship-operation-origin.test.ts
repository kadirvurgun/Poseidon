import { describe, expect, it } from 'vitest'
import { decodeShipOpsOriginContext } from './ship-operation-origin'
import { freezeShipOpsCommand } from './ship-operations-api'
import { emptyShipOpsFields } from './ship-operation-fields'
import { shipOpsActor, shipOpsOriginFixture, shipOpsOriginPinFixture, shipOpsRecordId, shipOpsTenant, shipOpsVessel } from './ship-operation-test-fixtures'
describe('T-OPS2-004/005 exact daily origin context — source only', () => {
  it('binds all pin members and rejects extra source fields or private actor payloads', () => {
    const context = shipOpsOriginFixture(), pin = shipOpsOriginPinFixture(), selector = { kind: pin.kind, id: pin.id, revision: pin.revision }
    expect(decodeShipOpsOriginContext(context, shipOpsVessel, 'Fuel', selector, pin).pin).toEqual(pin)
    expect(() => decodeShipOpsOriginContext({ ...context, pin: { ...pin, hash: 'C'.repeat(64) } }, shipOpsVessel, 'Fuel', selector, pin)).toThrow()
    expect(() => decodeShipOpsOriginContext({ ...context, actorId: shipOpsActor }, shipOpsVessel, 'Fuel', selector, pin)).toThrow()
    expect(() => decodeShipOpsOriginContext({ ...context, fields: [...context.fields, { key: 'officer_id', type: 'Reference', value: { state: 'Known', value: shipOpsActor }, sourceRow: 0, unit: null }] }, shipOpsVessel, 'Fuel', selector, pin)).toThrow()
    expect(() => decodeShipOpsOriginContext(context, shipOpsVessel, 'Fuel', { ...selector, revision: pin.revision + 1 })).toThrow()
  })
  it('retains authorized historical context without promoting it to a current or approved source', () => {
    const pin = shipOpsOriginPinFixture()
    const value = decodeShipOpsOriginContext({ ...shipOpsOriginFixture(), current: false }, shipOpsVessel, 'Port', { kind: pin.kind, id: pin.id, revision: pin.revision }, pin)
    expect(value.current).toBe(false); expect(value.qualification).toBe('REVIEW_REQUIRED'); expect(value.state).toBe('DRAFT')
  })
  it('writes only an exact origin pin and independent mutable facts, never inferred summary quantities', () => {
    const pin = shipOpsOriginPinFixture(), fields = emptyShipOpsFields('Fuel')
    const attempt = freezeShipOpsCommand('Fuel', shipOpsVessel, shipOpsRecordId, shipOpsTenant + '/' + shipOpsActor, fields, null, pin)
    expect(JSON.parse(attempt.body)).toMatchObject({ origin: pin, fields: { quantity_received_mt: { state: 'Missing', value: null }, quantity_consumed_mt: { state: 'Missing', value: null }, rob_after_mt: { state: 'Missing', value: null } } })
    expect(attempt.body).not.toContain('Unqualified summary')
  })
})
