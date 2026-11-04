import { useEffect, useId, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { loadOnboarding, type OnboardingAssessment, type OnboardingField } from './compliance-api'
import './compliance-enterprise.css'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

export const onboardingSteps = [
  { en: 'Identity and classification', tr: 'Kimlik ve sınıflandırma', fields: ['imo', 'flag', 'shipType', 'classSociety', 'classNumber'] },
  { en: 'Technical particulars', tr: 'Teknik özellikler', fields: ['gt', 'nt', 'dwt', 'loa', 'breadth', 'draft', 'buildDate', 'mainPropulsionKw', 'contractForConstructionDate', 'constructionRecordReference'] },
  { en: 'Operation and source context', tr: 'Operasyon ve kaynak bağlamı', fields: ['tradingArea', 'cargoContext', 'declaration', 'sourceReference', 'sourceObservedAt'] },
  { en: 'Review declared inputs', tr: 'Beyan edilen girdileri incele', fields: [] },
] as const
export function OnboardingSteps({ step, language, disabled, onChange }: { step: number; language: 'tr' | 'en'; disabled: boolean; onChange: (step: number) => void }) {
  return <nav aria-label={language === 'en' ? 'Onboarding steps' : 'Gemi ekleme adımları'}><ol className="onboarding-steps">{onboardingSteps.map((item, index) => <li key={item.en}><Button variant={step === index ? 'secondary' : 'ghost'} disabled={disabled} aria-current={step === index ? 'step' : undefined} onClick={() => onChange(index)}><span aria-hidden="true">{index + 1}</span>{item[language]}</Button></li>)}</ol></nav>
}
const labels: Record<OnboardingField, [string, string]> = { 'vessel.code': ['Vessel code', 'Araç kodu'], 'vessel.name': ['Vessel name', 'Araç adı'], 'profile.imo': ['IMO', 'IMO'], 'profile.flag': ['Flag', 'Bayrak'], 'profile.classSociety': ['Class society', 'Klas kuruluşu'], 'profile.shipType': ['Ship type', 'Gemi tipi'], 'profile.gt': ['GT', 'GT'], 'profile.buildDate': ['Build date', 'İnşa tarihi'], 'profile.tradingArea': ['Trading area', 'Sefer bölgesi'], 'profile.cargoContext': ['Cargo context', 'Yük bağlamı'] }
export function OnboardingAssessmentView({ vessel, savedRevision, dirty, language }: { vessel: string; savedRevision: number | null; dirty: boolean; language: 'tr' | 'en' }) {
  const epoch = useWorkspaceRevalidation(), online = navigator.onLine, visible = document.visibilityState !== 'hidden'
  const en = language === 'en', description = useId(), [attempt, retry] = useState(0), [result, setResult] = useState<{ vessel: string; revision: number | null; epoch: number; data?: OnboardingAssessment; failed?: boolean } | null>(null)
  useEffect(() => {
    if (!online || !visible) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    loadOnboarding(vessel, c.signal).then(data => { if (active) setResult({ vessel, revision: savedRevision, epoch, data }) }).catch(() => { if (active) setResult({ vessel, revision: savedRevision, epoch, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [vessel, savedRevision, attempt, epoch, online, visible])
  const current = result?.vessel === vessel && result.revision === savedRevision && result.epoch === epoch ? result : null
  const data = current?.data
  const assessment = data && data.profileRevision === savedRevision ? data : null
  return <section className="onboarding-assessment" aria-label={en ? 'Saved onboarding assessment' : 'Kayıtlı gemi ekleme değerlendirmesi'}><h3>{en ? 'Saved onboarding assessment' : 'Kayıtlı gemi ekleme değerlendirmesi'}</h3>
    {dirty && <p role="status">{en ? 'Unsaved inputs are not included in this server assessment. Save the draft to assess the next exact revision.' : 'Kaydedilmemiş girdiler bu sunucu değerlendirmesine dahil değildir. Sonraki kesin sürümü değerlendirmek için taslağı kaydedin.'}</p>}
    {!online || !visible ? <p role="status">{en ? 'Current assessment unavailable while offline or hidden.' : 'Çevrimdışı veya gizliyken güncel değerlendirme kullanılamıyor.'}</p> : !current ? <p role="status">{en ? 'Loading saved assessment…' : 'Kayıtlı değerlendirme yükleniyor…'}</p> : !assessment ? <p role="alert">{current.failed ? (en ? 'Onboarding assessment unavailable.' : 'Gemi ekleme değerlendirmesi kullanılamıyor.') : (en ? 'The saved profile revision changed. Reload the profile before relying on this assessment.' : 'Kayıtlı profil sürümü değişti. Değerlendirmeye dayanmadan önce profili yeniden yükleyin.')}</p> : <>
      <Badge tone="warning">{assessment.state === 'REVIEW_REQUIRED' ? (en ? 'Review required' : 'İnceleme gerekli') : (en ? 'Declared complete · unverified' : 'Beyan tam · doğrulanmamış')}</Badge><p>{en ? 'Saved profile revision' : 'Kayıtlı profil sürümü'}: {assessment.profileRevision ?? (en ? 'No saved draft' : 'Kayıtlı taslak yok')}</p>
      <ul className="onboarding-checklist">{assessment.fields.map(field => <li key={field.key}><strong>{labels[field.key][en ? 0 : 1]}</strong><span>{field.complete ? (en ? 'Declared' : 'Beyan edilmiş') : field.state === 'Unknown' ? (en ? 'Unknown · review required' : 'Bilinmiyor · inceleme gerekli') : (en ? 'Incomplete · review required' : 'Eksik · inceleme gerekli')}</span></li>)}</ul>
      <p>{en ? 'Verification remains separate. Missing critical inputs block submission; complete declarations do not establish verified compliance.' : 'Doğrulama ayrıdır. Eksik kritik girdiler gönderimi engeller; tam beyanlar doğrulanmış uyum oluşturmaz.'}</p>
    </>}
    <EnterpriseActions aria-label={en ? 'Onboarding review actions' : 'Gemi ekleme inceleme işlemleri'}><Button variant="outline" disabled={!online || !visible} onClick={() => { setResult(null); retry(n => n + 1) }}>{en ? 'Refresh saved assessment' : 'Kayıtlı değerlendirmeyi yenile'}</Button><Button disabled aria-describedby={description}>{en ? 'Submit for review' : 'İncelemeye gönder'}</Button></EnterpriseActions><p id={description}>{en ? 'Authoritative submission is unavailable in this draft workflow.' : 'Bu taslak akışında yetkili gönderim kullanılamıyor.'}</p>
  </section>
}
