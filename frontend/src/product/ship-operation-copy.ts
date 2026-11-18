const options: Record<string, string> = {
  Consumption: 'Tüketim', 'Delivery/BDN': 'İkmal / BDN', Transfer: 'Transfer', ROB: 'ROB', 'Tank Sounding': 'Tank ölçümü',
  VLSFO: 'VLSFO', HFO: 'HFO', MGO: 'MGO', LNG: 'LNG', Methanol: 'Metanol', Other: 'Diğer',
  NONE: 'Fark beyan edilmedi', ATTENTION: 'Dikkat gerektiren beyan', DISPUTED: 'İhtilaf beyanı',
  Arrival: 'Varış', NOR: 'NOR kaydı', 'Pilot Onboard': 'Kılavuzun gemiye çıkışı', 'Tug Made Fast': 'Römorkör bağlantısı',
  Berthing: 'Yanaşma', Loading: 'Yükleme', Discharging: 'Tahliye', Shifting: 'Yer değiştirme', 'Tug Let Go': 'Römorkör bağlantısının bırakılması', 'Pilot Off': 'Kılavuzun ayrılması', Departure: 'Ayrılış',
}
export const shipOpsOptionLabel = (value: string, en: boolean) => en ? value : options[value] ?? value
export const shipOpsFactLabel = (state: 'Known' | 'Missing' | 'Unknown', en: boolean) => state === 'Known' ? (en ? 'Declared value' : 'Beyan edilen değer') : state === 'Missing' ? (en ? 'Missing' : 'Eksik') : (en ? 'Unknown' : 'Bilinmiyor')
export function shipOpsBlockerLabel(code: string, en: boolean) {
  const labels: Record<string, [string, string]> = {
    REQUIRED_DATA_MISSING: ['Required source information is missing.', 'Gerekli kaynak bilgisi eksik.'],
    REQUIRED_DATA_UNKNOWN: ['Required source information remains unknown.', 'Gerekli kaynak bilgisi bilinmiyor.'],
    SOURCE_CONDITIONAL_UNRESOLVED: ['A source condition has no approved predicate.', 'Kaynak koşulunun kabul edilmiş bir kuralı yok.'],
    REFERENCE_UNAVAILABLE: ['An owning reference catalog is unavailable.', 'Sahip referans kataloğu kullanılamıyor.'],
    HUMAN_REVIEW_REQUIRED: ['Human review has not been established.', 'İnsan incelemesi kanıtlanmış değil.'],
    VERIFICATION_NOT_ESTABLISHED: ['Verification has not been established.', 'Doğrulama kanıtlanmış değil.'],
  }
  return labels[code]?.[en ? 0 : 1] ?? (en ? 'Source review remains required.' : 'Kaynak incelemesi gerekliliği sürüyor.')
}
