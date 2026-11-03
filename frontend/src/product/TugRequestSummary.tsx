import type { ReactNode } from 'react'
import { CalendarClock, FileText, MapPin, Pencil, Ship, Weight } from 'lucide-react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card'
import type { TugRequest } from './tug-api'
import { tugLabel } from './tug-labels'
import './tug-request-summary.css'

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <div><dt>{label}</dt><dd>{children}</dd></div>
}

export function TugRequestSummary({ record, language, editing, onEdit }: { record: TugRequest; language: 'tr' | 'en'; editing: boolean; onEdit?: () => void }) {
  const en = language === 'en', i = record.inputs, label = (code: string) => tugLabel(code, language)
  const unknown = en ? 'Unknown' : 'Bilinmiyor'
  const value = (raw: string | number | null) => raw === null ? unknown : raw
  const choice = (raw: string | null) => raw === null ? unknown : label(raw)
  const date = (raw: string | null) => raw && Number.isFinite(Date.parse(raw)) ? <time dateTime={raw}>{new Intl.DateTimeFormat(en ? 'en-GB' : 'tr-TR', { timeZone: 'UTC', year: 'numeric', month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(raw))}</time> : unknown
  return <section className="tug-request-summary" aria-label={en ? 'Current request detail' : 'Güncel talep detayı'}>
    <div className="tug-request-heading"><div><div><Badge>{en ? 'Draft' : 'Taslak'} · r{record.revision}</Badge><Badge tone={['Critical', 'Emergency'].includes(i.priority ?? '') ? 'danger' : i.priority === 'High' ? 'warning' : 'neutral'}>{choice(i.priority)}</Badge></div><h2>{record.jobNumber}</h2><p>{choice(i.jobType)} · {label(record.provenance)}</p></div>
      {!editing && onEdit && <Button onClick={onEdit}><Pencil />{en ? 'Edit draft' : 'Taslağı düzenle'}</Button>}
    </div>
    <div className="tug-request-completeness"><FileText aria-hidden="true" /><div><strong>{en ? 'Draft completeness' : 'Taslak bütünlüğü'}</strong><p>{record.missing.length > 0
      ? `${en ? 'Missing required fields' : 'Eksik zorunlu alanlar'}: ${record.missing.map(label).join(', ')}`
      : (en ? 'No missing request fields reported. Planning and operational approval are evaluated separately.' : 'Talep alanlarında eksik bildirilmedi. Planlama ve operasyon onayı ayrıca değerlendirilir.')}</p></div></div>
    {!editing && <div className="tug-request-grid">
      <Card><CardHeader><CardTitle>{en ? 'Request' : 'Talep'}</CardTitle><Ship size={17} aria-hidden="true" /></CardHeader><CardContent><dl>
        <Field label={en ? 'Assisted vessel' : 'Hizmet verilecek gemi'}>{value(i.assistedVessel)}</Field><Field label={en ? 'Vessel IMO' : 'Gemi IMO'}>{value(i.assistedVesselImo)}</Field>
        <Field label={en ? 'Customer reference' : 'Müşteri referansı'}>{value(i.customerRef)}</Field><Field label={en ? 'Request source' : 'Talep kaynağı'}>{choice(i.requestSource)}</Field>
        <Field label={en ? 'Request owner reference' : 'Talep sorumlusu referansı'}>{value(i.requestOwner)}</Field>
      </dl></CardContent></Card>
      <Card><CardHeader><CardTitle>{en ? 'Requested window' : 'Talep aralığı'}</CardTitle><CalendarClock size={17} aria-hidden="true" /></CardHeader><CardContent><dl>
        <Field label={en ? 'Requested start · UTC' : 'Talep başlangıcı · UTC'}>{date(i.requestedStart)}</Field><Field label={en ? 'Requested end · UTC' : 'Talep bitişi · UTC'}>{date(i.requestedEnd)}</Field>
      </dl><p className="tug-request-note">{en ? 'Requested times remain separate from the draft plan and any approved operational window.' : 'Talep zamanları, taslak plandan ve onaylı operasyon aralığından ayrıdır.'}</p></CardContent></Card>
      <Card><CardHeader><CardTitle>{en ? 'Route and operation' : 'Rota ve operasyon'}</CardTitle><MapPin size={17} aria-hidden="true" /></CardHeader><CardContent><dl>
        <Field label={en ? 'Job type' : 'İş tipi'}>{choice(i.jobType)}</Field><Field label={en ? 'Operation area' : 'Operasyon alanı'}>{value(i.operationArea)}</Field>
        <Field label={en ? 'Pickup point' : 'Alış noktası'}>{value(i.pickupPoint)}</Field><Field label={en ? 'Destination point' : 'Varış noktası'}>{value(i.destinationPoint)}</Field>
      </dl></CardContent></Card>
      <Card><CardHeader><CardTitle>{en ? 'Planning demand' : 'Planlama talebi'}</CardTitle><Weight size={17} aria-hidden="true" /></CardHeader><CardContent><dl>
        <Field label={en ? 'Required BP · tonnes' : 'Gerekli BP · ton'}>{value(i.requiredBollardPull)}</Field><Field label={en ? 'Required tug count' : 'Gerekli römorkör sayısı'}>{value(i.requiredTugCount)}</Field>
        <Field label={en ? 'Special requirements' : 'Özel gereksinimler'}>{value(i.specialRequirements)}</Field><Field label={en ? 'Notes' : 'Notlar'}>{value(i.notes)}</Field>
      </dl></CardContent></Card>
    </div>}
    <details className="tug-request-lineage"><summary>{en ? 'Request revision and source context' : 'Talep sürümü ve kaynak bağlamı'}</summary><dl>
      <Field label={en ? 'Request reference' : 'Talep referansı'}>{record.id}</Field><Field label={en ? 'Source version' : 'Kaynak sürümü'}>{record.sourceVersion}</Field><Field label={en ? 'Saved at · UTC' : 'Kayıt zamanı · UTC'}>{date(record.savedAt)}</Field>
    </dl></details>
  </section>
}
