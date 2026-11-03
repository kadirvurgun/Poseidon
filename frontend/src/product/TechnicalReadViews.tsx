import type { ReactNode } from 'react'
import { Box, CalendarClock, CircleHelp, ClipboardList, Layers3, ListTree, Settings2, TriangleAlert, Wrench } from 'lucide-react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card'
import { ScopeMetric } from './DirectoryOverview'
import { dueReason, dueState } from './maintenance-copy'
import { assetFields, planFields } from './technical-fields'
import type { Asset, Plan, Plans, References, Snapshot } from './technical-api'
import './technical-enterprise.css'

function Field({ label, children }: { label: string; children: ReactNode }) { return <div><dt>{label}</dt><dd>{children}</dd></div> }
const provenance = (code: string, en: boolean) => code === 'SyntheticUnverified' ? (en ? 'Synthetic, unverified' : 'Sentetik, doğrulanmamış')
  : code === 'UserDeclaredUnverified' ? (en ? 'User declaration, unverified' : 'Kullanıcı beyanı, doğrulanmamış') : code

export function TechnicalOverview({ assets, plans, en }: { assets: Snapshot; plans: Plans | null; en: boolean }) {
  const returned = en ? 'Returned authorized records' : 'Dönen yetkili kayıtlar'
  return <section className="enterprise-metrics technical-overview" aria-label={en ? 'Current technical scope overview' : 'Güncel teknik kapsam özeti'}>
    {plans ? <>
      <ScopeMetric title={en ? 'Maintenance plans' : 'Bakım planları'} value={plans.items.length} note={returned} icon={ClipboardList} />
      <ScopeMetric title={en ? 'Due plans' : 'Zamanı gelen planlar'} value={plans.items.filter(plan => plan.due.state === 'Due').length} note={en ? 'Current server calculation' : 'Güncel sunucu hesabı'} icon={TriangleAlert} />
      <ScopeMetric title={en ? 'Input review' : 'Girdi incelemesi'} value={plans.items.filter(plan => !['Due', 'NotDue'].includes(plan.due.state)).length} note={en ? 'Unresolved calculation inputs' : 'Çözümlenmemiş hesap girdileri'} icon={CircleHelp} />
      <ScopeMetric title={en ? 'Not due' : 'Zamanı gelmeyen planlar'} value={plans.items.filter(plan => plan.due.state === 'NotDue').length} note={en ? 'Operational fitness is separate' : 'Operasyonel uygunluk ayrıdır'} icon={CalendarClock} />
    </> : <>
      <ScopeMetric title={en ? 'Hierarchy records' : 'Hiyerarşi kayıtları'} value={assets.items.length} note={returned} icon={ListTree} />
      <ScopeMetric title={en ? 'Systems' : 'Sistemler'} value={assets.items.filter(asset => asset.level === 'System').length} note={returned} icon={Layers3} />
      <ScopeMetric title={en ? 'Equipment records' : 'Ekipman kayıtları'} value={assets.items.filter(asset => asset.level === 'Equipment').length} note={returned} icon={Settings2} />
      <ScopeMetric title={en ? 'Components' : 'Bileşenler'} value={assets.items.filter(asset => asset.level === 'Component').length} note={returned} icon={Box} />
    </>}
  </section>
}

export function TechnicalPlanList({ plans, assets, references, filter, selectedId, en, onSelect }: {
  plans: Plan[]; assets: Asset[]; references: References | null; filter: string; selectedId: string | null; en: boolean; onSelect: (id: string) => void
}) {
  const locale = en ? 'en' : 'tr', query = filter.toLocaleLowerCase(locale)
  const rows = plans.map(plan => ({ plan, asset: assets.find(asset => asset.id === plan.assetId), job: references?.jobs.find(job => job.id === plan.jobId) }))
    .filter(({ plan, asset, job }) => `${job?.title ?? ''} ${job?.code ?? ''} ${asset?.name ?? ''} ${asset?.code ?? ''} ${plan.id}`.toLocaleLowerCase(locale).includes(query))
  return <div className="technical-plan-list">
    <p className="technical-record-count">{rows.length} / {plans.length} {en ? 'returned plans' : 'dönen plan'}</p>
    {rows.length === 0 && <p className="technical-list-empty">{plans.length === 0 ? (en ? 'No plans.' : 'Plan bulunamadı.') : (en ? 'No matching plans.' : 'Eşleşen plan bulunamadı.')}</p>}
    <ul>{rows.map(({ plan, asset, job }) => <li key={plan.id}><Button variant="ghost" className="technical-plan-item" aria-current={selectedId === plan.id ? 'true' : undefined} aria-label={`${job?.title ?? (en ? 'Incomplete plan' : 'Eksik plan')} · ${dueState(plan.due.state, en)}`} onClick={() => onSelect(plan.id)}>
      <span className="technical-plan-item-title"><Wrench aria-hidden="true" /><strong>{job?.title ?? (en ? 'Incomplete plan' : 'Eksik plan')}</strong><Badge tone={plan.due.state === 'Due' ? 'danger' : plan.due.state === 'NotDue' ? 'neutral' : 'warning'}>{dueState(plan.due.state, en)}</Badge></span>
      <span>{asset?.name || asset?.code || (en ? 'Equipment reference unavailable' : 'Ekipman referansı kullanılamıyor')}{job?.code ? ` · ${job.code}` : ''}</span>
      <span>{en ? 'Next due date / meter' : 'Sonraki bakım tarihi / sayacı'}: {plan.due.nextDueDate ?? '—'} / {plan.due.nextDueMeter ?? '—'} · r{plan.revision}</span>
    </Button></li>)}</ul>
  </div>
}

export function TechnicalEmptyDetail({ plan, en }: { plan: boolean; en: boolean }) {
  return <div className="technical-empty-detail"><ListTree aria-hidden="true" /><h2>{plan ? (en ? 'Select a maintenance plan' : 'Bakım planı seçin') : (en ? 'Select equipment' : 'Ekipman seçin')}</h2><p>{en ? 'Choose a record to inspect its current inputs, source context and revision history.' : 'Güncel girdileri, kaynak bağlamını ve revizyon geçmişini incelemek için bir kayıt seçin.'}</p></div>
}

export function EquipmentSummary({ asset, en }: { asset: Asset; en: boolean }) {
  const unknown = en ? 'Unknown' : 'Bilinmiyor'
  const groups = [{ title: en ? 'Identity and classification' : 'Kimlik ve sınıflandırma', keys: ['code', 'name', 'systemFamily', 'equipmentType', 'criticality'] },
    { title: en ? 'Manufacturer and location' : 'Üretici ve konum', keys: ['maker', 'model', 'serialNumber', 'location', 'commissionDate'] }]
  return <div className="technical-read-detail" aria-label={en ? 'Equipment detail' : 'Ekipman detayı'}>
    <div className="technical-detail-heading"><div><Badge>{asset.level === 'System' ? (en ? 'System' : 'Sistem') : asset.level === 'Component' ? (en ? 'Component' : 'Bileşen') : (en ? 'Equipment' : 'Ekipman')}</Badge><h2>{asset.name || asset.code || unknown}</h2><p>{asset.code ?? unknown} · {provenance(asset.provenance, en)}</p></div><Badge>r{asset.revision}</Badge></div>
    <div className="technical-detail-cards">{groups.map(group => <Card key={group.title}><CardHeader><CardTitle>{group.title}</CardTitle></CardHeader><CardContent><dl>{assetFields.filter(([key]) => group.keys.includes(key)).map(([key, english, turkish]) => <Field key={key} label={en ? english : turkish}>{asset[key] ?? unknown}</Field>)}</dl></CardContent></Card>)}</div>
    <Card className="technical-notes"><CardHeader><CardTitle>{en ? 'Responsibility and notes' : 'Sorumluluk ve notlar'}</CardTitle></CardHeader><CardContent><dl><Field label={en ? 'Responsible role reference' : 'Sorumlu rol referansı'}>{asset.responsibleRoleId ?? unknown}</Field><Field label={en ? 'Notes' : 'Notlar'}>{asset.notes ?? unknown}</Field></dl></CardContent></Card>
    <details className="technical-lineage"><summary>{en ? 'Hierarchy and revision context' : 'Hiyerarşi ve revizyon bağlamı'}</summary><dl><Field label={en ? 'Equipment reference' : 'Ekipman referansı'}>{asset.id}</Field><Field label={en ? 'Parent reference' : 'Üst kayıt referansı'}>{asset.parentId ?? (en ? 'Root record' : 'Kök kayıt')}</Field><Field label={en ? 'Revision' : 'Revizyon'}>{asset.revision}</Field></dl></details>
  </div>
}

export function MaintenancePlanSummary({ plan, asset, references, en }: { plan: Plan; asset: Asset | undefined; references: References | null; en: boolean }) {
  const unknown = en ? 'Unknown' : 'Bilinmiyor', job = references?.jobs.find(job => job.id === plan.jobId)
  return <div className="technical-read-detail" aria-label={en ? 'Maintenance plan detail' : 'Bakım planı detayı'}>
    <div className="technical-detail-heading"><div><Badge>{en ? 'Draft plan' : 'Taslak plan'} · r{plan.revision}</Badge><h2>{job?.title ?? (en ? 'Incomplete plan' : 'Eksik plan')}</h2><p>{asset?.name || asset?.code || unknown}</p></div><Badge tone={plan.due.state === 'Due' ? 'danger' : plan.due.state === 'NotDue' ? 'neutral' : 'warning'}>{dueState(plan.due.state, en)}</Badge></div>
    <div className="technical-calculation"><CalendarClock aria-hidden="true" /><div><strong>{en ? 'Current calculation' : 'Güncel hesap'}</strong><p>{dueReason(plan.due.reason, en)}</p><p>{en ? 'Current planning calculation. Historical results remain in revision history; this does not verify completed maintenance or operational fitness.' : 'Güncel planlama hesabı. Geçmiş sonuçlar revizyon geçmişinde korunur; bu hesap tamamlanmış bakımı veya operasyonel uygunluğu doğrulamaz.'}</p></div></div>
    <div className="technical-detail-cards"><Card><CardHeader><CardTitle>{en ? 'Schedule and observation' : 'Takvim ve gözlem'}</CardTitle></CardHeader><CardContent><dl>
      <Field label={en ? 'Current calculation reference clock' : 'Güncel hesap referans zamanı'}>{plan.referenceAt}</Field><Field label={en ? 'Time zone' : 'Saat dilimi'}>{plan.inputs.timeZone ?? unknown}</Field>
      <Field label={en ? 'Next due date / meter' : 'Sonraki bakım tarihi / sayacı'}>{plan.due.nextDueDate ?? unknown} / {plan.due.nextDueMeter ?? unknown}</Field>
      <Field label={en ? 'Source / observation version' : 'Kaynak / gözlem sürümü'}>{plan.inputs.provenance ?? unknown} / {plan.inputs.observationRevision ?? unknown}</Field><Field label={en ? 'Observation time' : 'Gözlem zamanı'}>{plan.inputs.observedAt ?? unknown}</Field>
    </dl>{plan.observationState && plan.observationState !== 'NotRequested' && <p className="technical-note">{en ? 'Current observation assessment' : 'Güncel gözlem değerlendirmesi'}: {dueReason(plan.observationState, en)} · {plan.observationAssessedAt}</p>}</CardContent></Card>
    <Card><CardHeader><CardTitle>{en ? 'Owning records' : 'Sahip kayıtlar'}</CardTitle></CardHeader><CardContent><dl>
      <Field label={en ? 'Maintenance job' : 'Bakım işi'}>{job?.title ?? unknown}</Field><Field label={en ? 'Owner role reference' : 'Sorumlu rol referansı'}>{plan.ownerRoleId ?? unknown}</Field><Field label={en ? 'Recorded equipment revision' : 'Kayıtlı ekipman revizyonu'}>{plan.assetRevision}</Field><Field label={en ? 'Input provenance' : 'Girdi kaynağı'}>{provenance(plan.provenance, en)}</Field>
    </dl></CardContent></Card></div>
    <details className="technical-lineage"><summary>{en ? 'All recorded plan inputs' : 'Kayıtlı plan girdilerinin tümü'}</summary><dl>{planFields.map(([key, english, turkish]) => <Field key={key} label={en ? english : turkish}>{plan.inputs[key] ?? unknown}</Field>)}</dl></details>
    <p className="technical-note">{en ? 'Postponement requires a resolved authority, risk, evidence and maximum-extension policy. No postponement is activated here.' : 'Erteleme; yetki, risk, kanıt ve azami uzatma politikasının çözümlenmesini gerektirir. Burada erteleme etkinleştirilmez.'}</p>
  </div>
}
