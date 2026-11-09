import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { ReactNode } from 'react';
import { CalendarClock, FileText, MapPin, Pencil, Ship, Weight } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import type { TugRequest } from './tug-api';
import { tugLabel } from './tug-labels';
import './tug-request-summary.css';
function Field({ label, children, multiline = false }: {
    label: string;
    children: ReactNode;
    multiline?: boolean;
}) {
    return <div><dt>{label}</dt><dd className={multiline ? 'tug-request-narrative' : undefined}>{children}</dd></div>;
}
export function TugRequestSummary({ record, language, editing, onEdit }: {
    record: TugRequest;
    language: AppLocale;
    editing: boolean;
    onEdit?: () => void;
}) {
    const en = language === 'en', i = record.inputs, label = (code: string) => tugLabel(code, language);
    const unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd");
    const value = (raw: string | number | null) => raw === null ? unknown : raw;
    const choice = (raw: string | null) => raw === null ? unknown : label(raw);
    const date = (raw: string | null) => raw && Number.isFinite(Date.parse(raw)) ? <time dateTime={raw}>{new Intl.DateTimeFormat(en ? 'en-GB' : 'tr-TR', { timeZone: 'UTC', year: 'numeric', month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(raw))}</time> : unknown;
    return <section className="tug-request-summary" aria-label={localizedText(en, "pairs.m_4edde3f86c681c86")}>
    <div className="tug-request-heading"><div><div><Badge>{localizedText(en, "pairs.m_6f7b0af9992cd6bf")} · r{record.revision}</Badge><Badge tone={['Critical', 'Emergency'].includes(i.priority ?? '') ? 'danger' : i.priority === 'High' ? 'warning' : 'neutral'}>{choice(i.priority)}</Badge></div><h2>{record.jobNumber}</h2><p>{choice(i.jobType)} · {label(record.provenance)}</p></div>
      {!editing && onEdit && <Button onClick={onEdit}><Pencil />{localizedText(en, "pairs.m_2a8233db25e97857")}</Button>}
    </div>
    <div className="tug-request-completeness"><FileText aria-hidden="true"/><div><strong>{localizedText(en, "pairs.m_ea523cc93b670c41")}</strong><p>{record.missing.length > 0
            ? `${localizedText(en, "pairs.m_d74de8c6e1157488")}: ${record.missing.map(label).join(', ')}`
            : (localizedText(en, "pairs.m_edcfa5dba8ac4340"))}</p></div></div>
    {!editing && <div className="tug-request-grid">
      <Card><CardHeader><CardTitle>{localizedText(en, "pairs.m_66fa63df38fb0834")}</CardTitle><Ship size={17} aria-hidden="true"/></CardHeader><CardContent><dl>
        <Field label={localizedText(en, "pairs.m_135f6f7818069194")}>{value(i.assistedVessel)}</Field><Field label={localizedText(en, "pairs.m_192d393830307582")}>{value(i.assistedVesselImo)}</Field>
        <Field label={localizedText(en, "pairs.m_aef3f92b82b79438")}>{value(i.customerRef)}</Field><Field label={localizedText(en, "pairs.m_b556ace00c560a04")}>{choice(i.requestSource)}</Field>
        <Field label={localizedText(en, "pairs.m_b78923f06c7bfa5d")}>{value(i.requestOwner)}</Field>
      </dl></CardContent></Card>
      <Card><CardHeader><CardTitle>{localizedText(en, "pairs.m_a970c2229bb625d9")}</CardTitle><CalendarClock size={17} aria-hidden="true"/></CardHeader><CardContent><dl>
        <Field label={localizedText(en, "pairs.m_edf9d6a77e66393c")}>{date(i.requestedStart)}</Field><Field label={localizedText(en, "pairs.m_5fcfa29b787b558b")}>{date(i.requestedEnd)}</Field>
      </dl><p className="tug-request-note">{localizedText(en, "pairs.m_5adadcd9d4d5402c")}</p></CardContent></Card>
      <Card><CardHeader><CardTitle>{localizedText(en, "pairs.m_ab5becfb4e67be62")}</CardTitle><MapPin size={17} aria-hidden="true"/></CardHeader><CardContent><dl>
        <Field label={localizedText(en, "pairs.m_4936290c1e33377c")}>{choice(i.jobType)}</Field><Field label={localizedText(en, "pairs.m_6c454ca04f132394")}>{value(i.operationArea)}</Field>
        <Field label={localizedText(en, "pairs.m_5ef40c52d6c3bc7a")}>{value(i.pickupPoint)}</Field><Field label={localizedText(en, "pairs.m_a5633b4fdbdc5ef0")}>{value(i.destinationPoint)}</Field>
      </dl></CardContent></Card>
      <Card><CardHeader><CardTitle>{localizedText(en, "pairs.m_dd85801ee27c6082")}</CardTitle><Weight size={17} aria-hidden="true"/></CardHeader><CardContent><dl>
        <Field label={localizedText(en, "pairs.m_5ad2709e01f66db5")}>{value(i.requiredBollardPull)}</Field><Field label={localizedText(en, "pairs.m_f65b450b39dd7b33")}>{value(i.requiredTugCount)}</Field>
        <Field multiline label={localizedText(en, "pairs.m_ace74de96d335c55")}>{value(i.specialRequirements)}</Field><Field multiline label={localizedText(en, "pairs.m_0fb9c90ba8619153")}>{value(i.notes)}</Field>
      </dl></CardContent></Card>
    </div>}
    <details className="tug-request-lineage"><summary>{localizedText(en, "pairs.m_34431f266f301e14")}</summary><dl>
      <Field label={localizedText(en, "pairs.m_6e5e129473b4ca08")}>{record.id}</Field><Field label={localizedText(en, "pairs.m_084ab5b9b7688319")}>{record.sourceVersion}</Field><Field label={localizedText(en, "pairs.m_eab2f86466058965")}>{date(record.savedAt)}</Field>
    </dl></details>
  </section>;
}
