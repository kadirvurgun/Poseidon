// Source-owned presentation metadata: 14.02 Data Dictionary2316 rows2186–2225.
// Source labels/options do not establish approved catalogs or legal transitions.
export const shipOpsSourceVersion = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2186-2225:v46.3'
export const shipOpsKinds = ['Fuel', 'Port'] as const
export type ShipOpsKind = typeof shipOpsKinds[number]
export type ShipOpsFact = { state: 'Known' | 'Missing' | 'Unknown'; value: string | null }
export type ShipOpsFields = Record<string, ShipOpsFact>
export type ShipOpsFieldType = 'Instant' | 'Reference' | 'Code' | 'Text' | 'Narrative' | 'Number'
export type ShipOpsFieldDefinition = { key: string; type: ShipOpsFieldType; required: boolean; options: string[]; readOnly: boolean; referenceUnavailable: boolean; sourceRow: number; unit: string | null; requiredWhen: string | null }
export type ShipOpsFieldInfo = { key: string; en: string; tr: string; type: ShipOpsFieldType; sourceRow: number; readOnly?: boolean }
export const shipOpsGroups: Record<ShipOpsKind, { en: string; tr: string; fields: ShipOpsFieldInfo[] }[]> = {
  Fuel: [
    { en: 'Record and delivery context', tr: 'Kayıt ve ikmal bağlamı', fields: [
      { key: 'record_date', en: 'Record time', tr: 'Kayıt zamanı', type: 'Instant', sourceRow: 2186 },
      { key: 'vessel_id', en: 'Vessel scope', tr: 'Araç kapsamı', type: 'Reference', sourceRow: 2187, readOnly: true },
      { key: 'voyage_id', en: 'Voyage reference', tr: 'Sefer referansı', type: 'Reference', sourceRow: 2188 },
      { key: 'record_type', en: 'Record type', tr: 'Kayıt türü', type: 'Code', sourceRow: 2189 },
      { key: 'fuel_type', en: 'Fuel type', tr: 'Yakıt türü', type: 'Code', sourceRow: 2190 },
      { key: 'supplier', en: 'Supplier reference', tr: 'Tedarikçi referansı', type: 'Reference', sourceRow: 2191 },
      { key: 'bdn_no', en: 'BDN number', tr: 'BDN numarası', type: 'Text', sourceRow: 2192 },
      { key: 'delivery_port', en: 'Delivery port reference', tr: 'İkmal limanı referansı', type: 'Reference', sourceRow: 2193 },
    ] },
    { en: 'Quantities and ROB declarations', tr: 'Miktar ve ROB beyanları', fields: [
      { key: 'quantity_received_mt', en: 'Quantity received', tr: 'Alınan miktar', type: 'Number', sourceRow: 2194 },
      { key: 'quantity_consumed_mt', en: 'Quantity consumed', tr: 'Tüketilen miktar', type: 'Number', sourceRow: 2195 },
      { key: 'rob_before_mt', en: 'ROB before', tr: 'Önceki ROB', type: 'Number', sourceRow: 2196 },
      { key: 'rob_after_mt', en: 'ROB after', tr: 'Sonraki ROB', type: 'Number', sourceRow: 2197 },
      { key: 'tank_id', en: 'Tank reference', tr: 'Tank referansı', type: 'Reference', sourceRow: 2198 },
      { key: 'tank_capacity', en: 'Tank capacity', tr: 'Tank kapasitesi', type: 'Number', sourceRow: 2199 },
    ] },
    { en: 'Fuel properties and sample record', tr: 'Yakıt özellikleri ve numune kaydı', fields: [
      { key: 'sulphur_pct', en: 'Declared sulphur', tr: 'Beyan edilen kükürt', type: 'Number', sourceRow: 2200 },
      { key: 'viscosity', en: 'Viscosity', tr: 'Viskozite', type: 'Number', sourceRow: 2201 },
      { key: 'density', en: 'Density', tr: 'Yoğunluk', type: 'Number', sourceRow: 2202 },
      { key: 'sample_seal_no', en: 'Sample / seal number', tr: 'Numune / mühür numarası', type: 'Text', sourceRow: 2203 },
      { key: 'delivery_discrepancy', en: 'Delivery discrepancy declaration', tr: 'İkmal farkı beyanı', type: 'Code', sourceRow: 2204 },
      { key: 'compliance_state', en: 'Compliance review state', tr: 'Uyum inceleme durumu', type: 'Code', sourceRow: 2205, readOnly: true },
      { key: 'verification_state', en: 'Verification review state', tr: 'Doğrulama inceleme durumu', type: 'Code', sourceRow: 2206, readOnly: true },
      { key: 'evidence_ref', en: 'BDN / evidence reference', tr: 'BDN / kanıt referansı', type: 'Reference', sourceRow: 2207 },
    ] },
  ],
  Port: [
    { en: 'Port call context', tr: 'Liman uğrağı bağlamı', fields: [
      { key: 'voyage_id', en: 'Voyage reference', tr: 'Sefer referansı', type: 'Reference', sourceRow: 2208 },
      { key: 'port_call_id', en: 'Port call reference', tr: 'Liman uğrağı referansı', type: 'Reference', sourceRow: 2209 },
      { key: 'port', en: 'Port reference', tr: 'Liman referansı', type: 'Reference', sourceRow: 2210 },
      { key: 'terminal', en: 'Terminal / location declaration', tr: 'Terminal / konum beyanı', type: 'Text', sourceRow: 2211 },
    ] },
    { en: 'Recorded activity and timing', tr: 'Kayıtlı faaliyet ve zaman', fields: [
      { key: 'activity_type', en: 'Activity type', tr: 'Faaliyet türü', type: 'Code', sourceRow: 2212 },
      { key: 'event_start', en: 'Event start', tr: 'Olay başlangıcı', type: 'Instant', sourceRow: 2213 },
      { key: 'event_end', en: 'Event end', tr: 'Olay bitişi', type: 'Instant', sourceRow: 2214 },
      { key: 'remarks', en: 'Activity remarks', tr: 'Faaliyet açıklamaları', type: 'Narrative', sourceRow: 2215 },
    ] },
    { en: 'Cargo and draught declarations', tr: 'Yük ve su çekimi beyanları', fields: [
      { key: 'cargo_operation', en: 'Cargo operation declaration', tr: 'Yük operasyonu beyanı', type: 'Text', sourceRow: 2216 },
      { key: 'cargo_quantity', en: 'Cargo quantity', tr: 'Yük miktarı', type: 'Number', sourceRow: 2217 },
      { key: 'draught_fwd', en: 'Draught forward', tr: 'Baş su çekimi', type: 'Number', sourceRow: 2218 },
      { key: 'draught_aft', en: 'Draught aft', tr: 'Kıç su çekimi', type: 'Number', sourceRow: 2219 },
    ] },
    { en: 'Support, exceptions and evidence', tr: 'Destek, istisnalar ve kanıt', fields: [
      { key: 'tug_usage', en: 'Recorded tug usage', tr: 'Kayıtlı römorkör kullanımı', type: 'Text', sourceRow: 2220 },
      { key: 'pilot_usage', en: 'Recorded pilot usage', tr: 'Kayıtlı kılavuz kullanımı', type: 'Text', sourceRow: 2221 },
      { key: 'delay_reason', en: 'Delay / exception reason', tr: 'Gecikme / istisna gerekçesi', type: 'Narrative', sourceRow: 2222 },
      { key: 'letter_of_protest', en: 'Letter of protest reference', tr: 'Protesto mektubu referansı', type: 'Reference', sourceRow: 2223 },
      { key: 'sof_state', en: 'SOF review state', tr: 'SOF inceleme durumu', type: 'Code', sourceRow: 2224, readOnly: true },
      { key: 'evidence_ref', en: 'Attachment / evidence reference', tr: 'Ek / kanıt referansı', type: 'Reference', sourceRow: 2225 },
    ] },
  ],
}
export const shipOpsFields = (kind: ShipOpsKind) => shipOpsGroups[kind].flatMap(group => group.fields)
export const mutableShipOpsFields = (kind: ShipOpsKind) => shipOpsFields(kind).filter(field => !field.readOnly)
export const emptyShipOpsFields = (kind: ShipOpsKind): ShipOpsFields => Object.fromEntries(mutableShipOpsFields(kind).map(field => [field.key, { state: 'Missing', value: null }]))
export const shipOpsTitle = (kind: ShipOpsKind, language: 'en' | 'tr') => ({ Fuel: { en: 'Fuel, bunker and ROB', tr: 'Yakıt, ikmal ve ROB' }, Port: { en: 'Port activity and SOF', tr: 'Liman faaliyeti ve SOF' } })[kind][language]
export const shipOpsScreen = (kind: ShipOpsKind) => kind === 'Fuel' ? 'fuel-bunker-log' : 'port-activity-sof'
