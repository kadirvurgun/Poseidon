import type { DefectTemporalState } from './defect-api'
export const defectTemporalLabel = (state: DefectTemporalState, en: boolean) => ({
  MEASURE_MISSING: ['Temporary measure information missing', 'Geçici tedbir bilgisi eksik'], MEASURE_UNKNOWN: ['Temporary measure remains unknown', 'Geçici tedbir bilinmiyor'],
  DEADLINE_MISSING: ['Declared measure has no expiry information', 'Beyan edilen tedbirin son süre bilgisi yok'], DEADLINE_UNKNOWN: ['Declared expiry remains unknown', 'Beyan edilen son süre bilinmiyor'],
  EXPIRED_DECLARED: ['Declared expiry reached', 'Beyan edilen son süreye ulaşıldı'], NOT_DUE_DECLARED: ['Declared expiry not yet reached', 'Beyan edilen son süreye henüz ulaşılmadı'],
})[state][en ? 0 : 1]
export function defectBlockerLabel(code: string, en: boolean) {
  const labels: Record<string, [string, string]> = { UNVERIFIED_DECLARATION: ['Declaration remains unverified.', 'Beyan doğrulanmış değil.'], CRITICALITY_NOT_ASSESSED: ['Criticality has not been assessed.', 'Kritiklik değerlendirilmiş değil.'], SAFEGUARD_NOT_VERIFIED: ['Safeguard has not been verified.', 'Koruyucu tedbir doğrulanmış değil.'], RESTRICTION_NOT_AUTHORIZED: ['Restriction is a declaration, not authorization.', 'Kısıtlama bir beyandır, yetkilendirme değildir.'], CLOSURE_POLICY_UNAVAILABLE: ['Controlled closure is unavailable.', 'Kontrollü kapanış kullanılamıyor.'], EFFECTIVENESS_NOT_ASSESSED: ['Effectiveness has not been assessed.', 'Etkinlik değerlendirilmiş değil.'] }
  return labels[code]?.[en ? 0 : 1] ?? (en ? 'Source review remains required.' : 'Kaynak incelemesi gerekliliği sürüyor.')
}
