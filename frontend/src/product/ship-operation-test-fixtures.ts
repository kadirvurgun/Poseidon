// Original-source metadata for synthetic transport/component fixtures only.
// Not a configured catalog, approved entity, Human sign-off or live authority.
import { shipOpsFields, type ShipOpsFieldDefinition, type ShipOpsKind } from './ship-operation-fields'
const options: Record<string, string[]> = {
  record_type: ['Consumption', 'Delivery/BDN', 'Transfer', 'ROB', 'Tank Sounding'], fuel_type: ['VLSFO', 'HFO', 'MGO', 'LNG', 'Methanol', 'Other'], delivery_discrepancy: ['NONE', 'ATTENTION', 'DISPUTED'],
  compliance_state: ['READY', 'CONDITIONAL', 'REVIEW_REQUIRED', 'NOT_READY'], verification_state: ['PENDING', 'VERIFIED', 'REJECTED'], sof_state: ['DRAFT', 'REVIEWED', 'FINAL'],
  activity_type: ['Arrival', 'NOR', 'Pilot Onboard', 'Tug Made Fast', 'Berthing', 'Loading', 'Discharging', 'Shifting', 'Tug Let Go', 'Pilot Off', 'Departure', 'Other'],
}
export function shipOpsSchemaFixture(kind: ShipOpsKind): ShipOpsFieldDefinition[] {
  const required = kind === 'Fuel' ? ['record_date', 'vessel_id', 'record_type', 'fuel_type', 'compliance_state', 'verification_state'] : ['voyage_id', 'port_call_id', 'port', 'activity_type', 'event_start', 'sof_state']
  return shipOpsFields(kind).map(field => ({ key: field.key, type: field.type, required: required.includes(field.key), options: options[field.key] ?? [], readOnly: !!field.readOnly, referenceUnavailable: field.type === 'Reference' && field.key !== 'vessel_id', sourceRow: field.sourceRow, unit: field.key.endsWith('_mt') ? 'mt' : field.key === 'sulphur_pct' ? '%' : null, requiredWhen: required.includes(field.key) ? null : 'SOURCE_CONDITIONAL_UNRESOLVED' }))
}
