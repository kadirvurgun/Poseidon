// Original-source metadata for synthetic transport/component fixtures only.
// Not a configured catalog, approved entity, Human sign-off or live authority.
import { emptyShipOpsFields, shipOpsSourceVersion, shipOpsFields, type ShipOpsFieldDefinition, type ShipOpsKind } from './ship-operation-fields'
import type { ShipOpsCapabilities, ShipOpsRecord } from './ship-operations-api'
const options: Record<string, string[]> = {
  record_type: ['Consumption', 'Delivery/BDN', 'Transfer', 'ROB', 'Tank Sounding'], fuel_type: ['VLSFO', 'HFO', 'MGO', 'LNG', 'Methanol', 'Other'], delivery_discrepancy: ['NONE', 'ATTENTION', 'DISPUTED'],
  compliance_state: ['READY', 'CONDITIONAL', 'REVIEW_REQUIRED', 'NOT_READY'], verification_state: ['PENDING', 'VERIFIED', 'REJECTED'], sof_state: ['DRAFT', 'REVIEWED', 'FINAL'],
  activity_type: ['Arrival', 'NOR', 'Pilot Onboard', 'Tug Made Fast', 'Berthing', 'Loading', 'Discharging', 'Shifting', 'Tug Let Go', 'Pilot Off', 'Departure', 'Other'],
}
export function shipOpsSchemaFixture(kind: ShipOpsKind): ShipOpsFieldDefinition[] {
  const required = kind === 'Fuel' ? ['record_date', 'vessel_id', 'record_type', 'fuel_type', 'compliance_state', 'verification_state'] : ['voyage_id', 'port_call_id', 'port', 'activity_type', 'event_start', 'sof_state']
  return shipOpsFields(kind).map(field => ({ key: field.key, type: field.type, required: required.includes(field.key), options: options[field.key] ?? [], readOnly: !!field.readOnly, referenceUnavailable: field.type === 'Reference' && field.key !== 'vessel_id', sourceRow: field.sourceRow, unit: field.key.endsWith('_mt') ? 'mt' : field.key === 'sulphur_pct' ? '%' : null, requiredWhen: required.includes(field.key) ? null : 'SOURCE_CONDITIONAL_UNRESOLVED' }))
}

export const shipOpsVessel = '20000000-0000-4000-8000-000000000001'
export const shipOpsRecordId = '20000000-0000-4000-8000-000000000002'
export const shipOpsActor = '20000000-0000-4000-8000-000000000003'
export const shipOpsTenant = '20000000-0000-4000-8000-000000000004'
export function shipOpsRecordFixture(kind: ShipOpsKind = 'Fuel'): ShipOpsRecord {
  const fields = emptyShipOpsFields(kind)
  if (kind === 'Fuel') { fields.vessel_id = { state: 'Known', value: shipOpsVessel }; fields.compliance_state = { state: 'Known', value: 'REVIEW_REQUIRED' }; fields.verification_state = { state: 'Known', value: 'PENDING' } }
  else fields.sof_state = { state: 'Known', value: 'DRAFT' }
  return { id: shipOpsRecordId, vesselId: shipOpsVessel, kind, revision: 1, version: 31, fields, state: 'DRAFT', provenance: 'SyntheticUnverified', savedAt: '2026-11-05T12:00:00Z', hash: 'A'.repeat(64), sourceVersion: shipOpsSourceVersion, blockers: ['UNVERIFIED_DECLARATION', 'SOURCE_POLICY_REVIEW_REQUIRED'], currentRevision: true, canEdit: true, origin: null }
}
export const shipOpsCapsFixture = (kind: ShipOpsKind = 'Fuel'): ShipOpsCapabilities => ({ vesselId: shipOpsVessel, kind, canRead: true, canWrite: true, canReview: false, canSubmit: false, schema: shipOpsSchemaFixture(kind), sourceVersion: shipOpsSourceVersion, offline: { supported: false, state: 'UNAVAILABLE', operation: 'ShipOpsDraft' } })

export const shipOpsOriginPinFixture = () => ({ kind: 'Noon' as const, id: '20000000-0000-4000-8000-000000000005', revision: 2, version: 61, hash: 'B'.repeat(64), sourceVersion: '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3' })
export const shipOpsOriginFixture = () => ({ vesselId: shipOpsVessel, pin: shipOpsOriginPinFixture(), current: true, state: 'DRAFT' as const, qualification: 'REVIEW_REQUIRED' as const, provenance: 'SyntheticUnverified' as const, savedAt: '2026-11-05T11:30:00Z', fields: [
  { key: 'report_date', type: 'Date' as const, sourceRow: 2128, unit: null, value: { state: 'Known' as const, value: '2026-11-05' } },
  { key: 'report_time_utc', type: 'Text' as const, sourceRow: 2129, unit: null, value: { state: 'Known' as const, value: '11:00' } },
  { key: 'activity', type: 'Code' as const, sourceRow: 2134, unit: null, value: { state: 'Missing' as const, value: null } },
  { key: 'fuel_summary', type: 'Text' as const, sourceRow: 2144, unit: null, value: { state: 'Known' as const, value: 'Unqualified summary 99 units' } },
  { key: 'rob_summary', type: 'Text' as const, sourceRow: 2145, unit: null, value: { state: 'Unknown' as const, value: null } },
], href: '/app/noon-report/' + shipOpsVessel + '?id=' + shipOpsOriginPinFixture().id })
