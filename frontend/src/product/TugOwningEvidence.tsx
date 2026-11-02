import type { PlanningEvidence } from './tug-planning-api'
import { TugCrewCoverage } from './TugCrewCoverage'

export function TugOwningEvidence({ value, language }: { value: PlanningEvidence | null; language: 'tr' | 'en' }) {
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  const state = (s: string) => ({ CurrentPlanningCapacity: t('Current planning capacity', 'Güncel planlama kapasitesi'), Blocked: t('Blocked', 'Engelli'), ReviewRequired: t('Review required', 'İnceleme gerekli'), Unverified: t('Unverified', 'Doğrulanmamış'), ChangedInput: t('Input changed', 'Girdi değişti'), Expired: t('Expired', 'Süresi dolmuş'), SourceReviewRequired: t('Source review required', 'Kaynak incelemesi gerekli'), UnsupportedContext: t('Formula does not cover this job', 'Formül bu işi kapsamıyor'), Missing: t('Missing', 'Eksik'), Unknown: t('Unknown', 'Bilinmiyor'), Unavailable: t('Unavailable under current access', 'Güncel erişimde kullanılamıyor') }[s] ?? t('Review required', 'İnceleme gerekli'))
  return <section aria-label={t('Owning capacity and readiness evidence', 'Kapasite ve hazırlığın sahip kanıtları')}>
    <h4>{t('Owning capacity and readiness evidence', 'Kapasite ve hazırlığın sahip kanıtları')}</h4>
    <p>{t('Planning calculation and current recorded blockers. Master safety authority remains separate.', 'Planlama hesabı ve güncel kayıtlı engeller. Kaptanın emniyet yetkisi ayrıdır.')}</p>
    {!value && <p role="status">{t('Current evidence unavailable; refresh when connected.', 'Güncel kanıt kullanılamıyor; bağlantı kurulduğunda yenileyin.')}</p>}
    {value?.crew && <TugCrewCoverage items={value.crew} language={language} />}
    <ul>{value?.ownedBp?.map(b => <li key={b.tugId}>{b.tugId} · {state(b.state)} · {b.tonnes ?? '—'} t
      <p>{t('Why / next action', 'Neden / sonraki adım')}: {b.why} · {t('Evidence revision', 'Kanıt sürümü')}: {b.evidenceRevision ?? '—'} · {t('Condition revision', 'Kondisyon sürümü')}: {b.conditionRevision ?? '—'}</p>
      <p>{t('Qualified source revision', 'Nitelendirilmiş kaynak sürümü')}: {b.qualificationRevision ?? '—'} · {t('Test time', 'Test zamanı')}: {b.observedAt ?? '—'} · {t('Valid until', 'Geçerlilik sonu')}: {b.validUntil ?? '—'}</p>
      <a href="/app/evidence-graph">{t('Review owning evidence and independent verification', 'Sahip kanıtı ve bağımsız doğrulamayı incele')}</a>
    </li>)}</ul>
    <ul>{value?.readinessFacts?.map((f, i) => <li key={`${f.tugId}/${f.source}/${f.id}/${i}`}>{f.tugId} · {f.state === 'Current' ? t('Current fact; overall readiness separate', 'Güncel bulgu; genel hazırlık ayrı') : state(f.state)} · {f.source} · r{f.revision ?? '—'} · {f.why} · {f.observedAt ?? '—'} · {f.expiresAt ?? '—'}</li>)}</ul>
    <ul aria-label={t('Recorded operational conflicts', 'Kayıtlı operasyonel çakışmalar')}>{value?.operationalConflicts?.map(c => <li key={`${c.tugId}/${c.kind}/${c.resourceId}`}>{c.tugId} · {t('Human conflict review required', 'İnsan çakışma incelemesi gerekli')} · {c.kind} · r{c.revision} · {c.why} · {c.provenance} · {c.observedAt ?? '—'}</li>)}</ul>
  </section>
}
