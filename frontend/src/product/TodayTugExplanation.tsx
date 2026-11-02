import type { TodayTugEvidence } from './today-api'
import { dueReason } from './maintenance-copy'
import { TugCrewCoverage } from './TugCrewCoverage'

const copy = {
  en: { job: 'Job reference', lineage: 'Current request / plan / planned request revision', missing: 'No draft plan is stored.',
    window: 'Declared plan start / end', unknown: 'Unknown', assignments: 'Currently visible assigned tug references',
    empty: 'No tugs assigned in this draft.', readiness: 'Current planning assessment', capacity: 'Qualified planning capacity (tonnes)',
    conflict: 'Declared conflict assessment', facts: 'Current owning source facts', revision: 'Revision', observation: 'Observed at',
    qualification: 'Source-specific qualification or coverage is required. Open the job to review its owning inputs.',
    Current: 'Current source fact; operational approval remains separate', Unknown: 'Unknown', Unavailable: 'Unavailable',
    Blocked: 'Blocked by current inputs', ReviewRequired: 'Review required', HumanReviewRequired: 'Authorized human review required',
    BoundaryReviewRequired: 'Operational boundary review required', Crew: 'Crew coverage', CrewAssignment: 'Crew assignment',
    CrewCertificate: 'Crew certificate declaration', Equipment: 'Equipment condition', EquipmentPms: 'Equipment and maintenance access',
    MaintenancePlan: 'Maintenance planning', OperationContext: 'Job operation context', BollardPullCertificate: 'Bollard pull certificate',
    CapabilityEquipmentCondition: 'Capability equipment condition' },
  tr: { job: 'İş referansı', lineage: 'Güncel talep / plan / planlanan talep revizyonu', missing: 'Kaydedilmiş taslak plan yok.',
    window: 'Beyan edilen plan başlangıcı / bitişi', unknown: 'Bilinmiyor', assignments: 'Güncel görünür atanmış römorkör referansları',
    empty: 'Bu taslakta atanmış römorkör yok.', readiness: 'Güncel planlama değerlendirmesi', capacity: 'Nitelendirilmiş planlama kapasitesi (ton)',
    conflict: 'Beyan edilen çakışma değerlendirmesi', facts: 'Güncel kaynak bulguları', revision: 'Revizyon', observation: 'Gözlem zamanı',
    qualification: 'Kaynağa özel nitelendirme veya kapsam gerekiyor. Kaynak girdilerini incelemek için işi açın.',
    Current: 'Güncel kaynak bulgusu; operasyon onayı ayrıca gerekir', Unknown: 'Bilinmiyor', Unavailable: 'Kullanılamıyor',
    Blocked: 'Güncel girdiler engelliyor', ReviewRequired: 'İnceleme gerekli', HumanReviewRequired: 'Yetkili insan incelemesi gerekli',
    BoundaryReviewRequired: 'Operasyonel sınır incelemesi gerekli', Crew: 'Mürettebat kapsamı', CrewAssignment: 'Mürettebat ataması',
    CrewCertificate: 'Mürettebat sertifika beyanı', Equipment: 'Ekipman durumu', EquipmentPms: 'Ekipman ve bakım erişimi',
    MaintenancePlan: 'Bakım planlaması', OperationContext: 'İş operasyon bağlamı', BollardPullCertificate: 'Çekme kuvveti sertifikası',
    CapabilityEquipmentCondition: 'Kapasite ekipman durumu' }
}

export function TodayTugExplanation({ detail, language }: { detail: TodayTugEvidence; language: 'tr' | 'en' }) {
  const t = copy[language]
  const label = (key: string) => t[key as keyof typeof t] ?? t.unknown
  return <>
    <p>{t.job}: {detail.jobId}</p>
    <p>{t.lineage}: {detail.requestRevision} / {detail.planRevision ?? t.unknown} / {detail.plannedRequestRevision ?? t.unknown}</p>
    {detail.planRevision === null ? <p>{t.missing}</p> : <>
      <p>{t.window}: {detail.plannedStart ?? t.unknown} / {detail.plannedEnd ?? t.unknown}</p>
      <p>{t.assignments}</p>{detail.assignedTugs.length === 0 ? <p>{t.empty}</p> : <ul>{detail.assignedTugs.map(id => <li key={id}>{id}</li>)}</ul>}
      {detail.evidence && <>
        {detail.evidence.crew && <TugCrewCoverage items={detail.evidence.crew} language={language} />}
        <p>{t.readiness}: {label(detail.evidence.readiness)}</p>
        <p>{t.capacity}: {detail.evidence.availableBollardPull ?? t.unknown}</p>
        <p>{t.conflict}: {label(detail.evidence.conflictState)}</p>
        <details><summary>{t.facts}</summary><ul>{detail.evidence.readinessFacts?.map((fact, index) =>
          <li key={`${fact.tugId}:${fact.source}:${fact.id ?? 'missing'}:${index}`}>
            <p>{label(fact.source)} · {label(fact.state)}</p>
            <p>{fact.tugId}{fact.id && ` / ${fact.id}`} · {t.revision}: {fact.revision ?? t.unknown}</p>
            <p>{t.observation}: {fact.observedAt ?? t.unknown}</p>
            <p>{fact.source === 'MaintenancePlan' ? dueReason(fact.why, language === 'en') : t.qualification}</p>
          </li>)}</ul></details>
      </>}
    </>}
  </>
}
