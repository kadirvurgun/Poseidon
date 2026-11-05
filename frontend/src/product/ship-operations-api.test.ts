import { describe, expect, it } from 'vitest'
import { decodeShipOpsCapabilities, decodeShipOpsOrigin, decodeShipOpsRecord, freezeShipOpsCommand, shipOpsFieldErrors, validShipOpsInstant } from './ship-operations-api'
import { emptyShipOpsFields } from './ship-operation-fields'
import { shipOpsActor, shipOpsCapsFixture, shipOpsRecordFixture, shipOpsRecordId, shipOpsTenant, shipOpsVessel } from './ship-operation-test-fixtures'
import { readShipOpsLocation, shipOpsLocationPath } from './ship-operation-location'
import { decodeLenses, lensHref } from './workspace-lens-api'
describe('T-OPS2-004/005 strict ordinary declaration contracts (source only)', () => {
  it('accepts incomplete drafts and freezes exact numeric text without readonly fields or source promotion', () => {
    const fields = emptyShipOpsFields('Fuel'); fields.quantity_received_mt = { state: 'Known', value: '9007199254740993.123456789' }; fields.rob_before_mt = { state: 'Known', value: '0' }
    const command = freezeShipOpsCommand('Fuel', shipOpsVessel, shipOpsRecordId, shipOpsTenant + '/' + shipOpsActor, fields, null, null)
    const body = JSON.parse(command.body)
    expect(body).toMatchObject({ expectedVersion: 0, reasonCode: 'INITIAL_AUTHORING', origin: null, fields: { quantity_received_mt: { state: 'Known', value: '9007199254740993.123456789' }, rob_before_mt: { state: 'Known', value: '0' }, rob_after_mt: { state: 'Missing', value: null } } })
    expect(Object.keys(body.fields)).toHaveLength(19)
    expect(body.fields.vessel_id).toBeUndefined(); expect(body.fields.compliance_state).toBeUndefined(); expect(body.fields.verification_state).toBeUndefined()
  })
  it('rejects manufactured READY, VERIFIED or FINAL while retaining genuine declared draft status', () => {
    const fuel = shipOpsRecordFixture(), port = shipOpsRecordFixture('Port')
    expect(decodeShipOpsRecord(fuel, shipOpsVessel, 'Fuel').state).toBe('DRAFT')
    expect(() => decodeShipOpsRecord({ ...fuel, fields: { ...fuel.fields, compliance_state: { state: 'Known', value: 'READY' } } }, shipOpsVessel, 'Fuel')).toThrow()
    expect(() => decodeShipOpsRecord({ ...fuel, fields: { ...fuel.fields, verification_state: { state: 'Known', value: 'VERIFIED' } } }, shipOpsVessel, 'Fuel')).toThrow()
    expect(() => decodeShipOpsRecord({ ...port, fields: { ...port.fields, sof_state: { state: 'Known', value: 'FINAL' } } }, shipOpsVessel, 'Port')).toThrow()
    expect(() => decodeShipOpsRecord({ ...fuel, currentRevision: false, canEdit: true }, shipOpsVessel, 'Fuel')).toThrow()
  })
  it('requires explicit second-precision offset instants and preserves seven fractional digits for event ordering', () => {
    for (const value of ['2026-11-05T12:00', '2026-11-05T12:00:00', '2026-11-05T12:00:00-00:00', '2026-02-30T12:00:00Z']) expect(validShipOpsInstant(value)).toBe(false)
    expect(validShipOpsInstant('2026-11-05T15:00:00.1234567+03:00')).toBe(true)
    const fields = emptyShipOpsFields('Port')
    fields.event_start = { state: 'Known', value: '2026-11-05T15:00:00.1234567+03:00' }; fields.event_end = { state: 'Known', value: '2026-11-05T12:00:00.1234566Z' }
    expect(shipOpsFieldErrors(fields, 'Port')).toHaveProperty('event_end')
    fields.event_end.value = '2026-11-05T12:00:00.1234567Z'
    expect(shipOpsFieldErrors(fields, 'Port')).not.toHaveProperty('event_end')
  })
  it('cannot invent catalog availability, file mappings, unit conventions or review capabilities', () => {
    const caps = shipOpsCapsFixture()
    expect(decodeShipOpsCapabilities(caps, shipOpsVessel, 'Fuel').canSubmit).toBe(false)
    for (const schema of [caps.schema.map(field => field.key === 'evidence_ref' ? { ...field, referenceUnavailable: false } : field), caps.schema.map(field => field.key === 'viscosity' ? { ...field, unit: 'cSt' } : field)]) expect(() => decodeShipOpsCapabilities({ ...caps, schema }, shipOpsVessel, 'Fuel')).toThrow()
    expect(() => decodeShipOpsCapabilities({ ...caps, canReview: true }, shipOpsVessel, 'Fuel')).toThrow()
    const fields = emptyShipOpsFields('Fuel'); fields.supplier = { state: 'Known', value: shipOpsRecordId }
    expect(() => freezeShipOpsCommand('Fuel', shipOpsVessel, shipOpsRecordId, shipOpsTenant + '/' + shipOpsActor, fields, null, null)).toThrow()
  })
  it('rejects foreign scope, changed source version and a Port claim linked to Engine origin', () => {
    expect(() => decodeShipOpsRecord(shipOpsRecordFixture(), shipOpsRecordId, 'Fuel')).toThrow()
    expect(() => decodeShipOpsRecord({ ...shipOpsRecordFixture(), sourceVersion: 'guessed' }, shipOpsVessel, 'Fuel')).toThrow()
    const origin = { kind: 'Engine', id: shipOpsRecordId, revision: 1, version: 3, hash: 'B'.repeat(64), sourceVersion: '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3' }
    expect(decodeShipOpsOrigin(origin, 'Fuel').kind).toBe('Engine')
    expect(() => decodeShipOpsOrigin(origin, 'Port')).toThrow()
  })
  it('keeps origin selectors nonprivate, exact-revision and distinct from saved record identifiers', () => {
    const base = '/app/fuel-bunker-log/' + shipOpsVessel, query = '?originKind=Noon&originId=' + shipOpsRecordId + '&originRevision=2'
    expect(shipOpsLocationPath(readShipOpsLocation(base + query))).toBe(base + query)
    for (const bad of [query + '&originRevision=3', query + '&id=' + shipOpsRecordId, '?originKind=Noon&originId=' + shipOpsRecordId, '?supplier=private-name', query.replace('=2', '=0')]) expect(readShipOpsLocation(base + bad).invalid).toBe(true)
    expect(readShipOpsLocation('/app/port-activity-sof/' + shipOpsVessel + query.replace('Noon', 'Engine')).invalid).toBe(true)
  })
  it('accepts actual scoped daily navigation and rejects an operational item projected into Tug mode', () => {
    const value = { tenantId: shipOpsTenant, actorId: shipOpsActor, vesselId: shipOpsVessel, mode: 'Ship', state: 'Configured', version: 'synthetic-source-v1', canSearch: false, lenses: [{ reference: shipOpsRecordId, tr: 'Kaynak rol', en: 'Source role', items: [{ key: 'fuel-bunker-log', href: lensHref('fuel-bunker-log', shipOpsVessel), group: 'daily' }] }] }
    expect(decodeLenses(value, shipOpsTenant, shipOpsActor, shipOpsVessel).lenses[0].items[0].group).toBe('daily')
    expect(() => decodeLenses({ ...value, mode: 'Tug' }, shipOpsTenant, shipOpsActor, shipOpsVessel)).toThrow()
  })
})
