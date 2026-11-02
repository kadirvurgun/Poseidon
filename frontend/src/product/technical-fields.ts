export const assetFields = [
  ['code', 'Equipment code', 'Ekipman kodu'], ['name', 'Equipment name', 'Ekipman adı'], ['systemFamily', 'System family', 'Sistem ailesi'],
  ['equipmentType', 'Type declaration', 'Tür beyanı'], ['criticality', 'Criticality declaration', 'Kritiklik beyanı'],
  ['maker', 'Maker', 'Üretici'], ['model', 'Model', 'Model'], ['serialNumber', 'Serial number', 'Seri numarası'],
  ['location', 'Location', 'Konum'], ['commissionDate', 'Commission date', 'Devreye alma tarihi'], ['notes', 'Notes', 'Notlar'],
] as const
export const planFields = [
  ['observationId', 'Normalized observation reference', 'Normalleştirilmiş gözlem referansı'],
  ['planType', 'Plan type declaration', 'Plan türü beyanı'], ['intervalValue', 'Interval value (exact decimal)', 'Aralık değeri (kesin ondalık)'],
  ['intervalUnit', 'Unit (day / month / year for calendar)', 'Birim (takvim için day / month / year)'],
  ['meterSource', 'Meter source / unit context', 'Sayaç kaynağı / birim bağlamı'], ['lastDoneDate', 'Declared last done date', 'Beyan edilen son bakım tarihi'],
  ['lastDoneMeter', 'Declared last done meter', 'Beyan edilen son bakım sayacı'], ['currentMeter', 'Observed meter', 'Gözlenen sayaç'],
  ['observedAt', 'Observation time (ISO with offset)', 'Gözlem zamanı (ISO, saat farkı ile)'], ['observationRevision', 'Observation version / reference', 'Gözlem sürümü / referansı'],
  ['timeZone', 'Time zone (IANA)', 'Saat dilimi (IANA)'], ['provenance', 'Input provenance', 'Girdi kaynağı'],
  ['warningLead', 'Warning lead declaration', 'Ön uyarı beyanı'], ['gracePeriod', 'Grace declaration', 'Tolerans beyanı'],
  ['postponementAllowed', 'Postponement policy reference / Unknown', 'Erteleme politika referansı / Unknown'],
  ['approvalRule', 'Approval rule reference', 'Onay kuralı referansı'], ['criticality', 'Criticality declaration', 'Kritiklik beyanı'],
  ['applicableRequirement', 'Applicable requirement reference', 'Uygulanabilir gereksinim referansı'],
] as const
