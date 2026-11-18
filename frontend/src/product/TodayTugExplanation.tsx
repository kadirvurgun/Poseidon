import { localizedCopy } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { TodayTugEvidence } from './today-api';
import { dueReason } from './maintenance-copy';
import { TugCrewCoverage } from './TugCrewCoverage';
const copy = localizedCopy("TodayTugExplanation_tsx_copy");
export function TodayTugExplanation({ detail, language }: {
    detail: TodayTugEvidence;
    language: AppLocale;
}) {
    const t = copy[language];
    const label = (key: string) => t[key as keyof typeof t] ?? t.unknown;
    return <>
    <p>{t.job}: {detail.jobId}</p>
    <p>{t.lineage}: {detail.requestRevision} / {detail.planRevision ?? t.unknown} / {detail.plannedRequestRevision ?? t.unknown}</p>
    {detail.planRevision === null ? <p>{t.missing}</p> : <>
      <p>{t.window}: {detail.plannedStart ?? t.unknown} / {detail.plannedEnd ?? t.unknown}</p>
      <p>{t.assignments}</p>{detail.assignedTugs.length === 0 ? <p>{t.empty}</p> : <ul>{detail.assignedTugs.map(id => <li key={id}>{id}</li>)}</ul>}
      {detail.evidence && <>
        {detail.evidence.crew && <TugCrewCoverage items={detail.evidence.crew} language={language}/>}
        <p>{t.readiness}: {label(detail.evidence.readiness)}</p>
        <p>{t.capacity}: {detail.evidence.availableBollardPull ?? t.unknown}</p>
        <p>{t.conflict}: {label(detail.evidence.conflictState)}</p>
        <details><summary>{t.facts}</summary><ul>{detail.evidence.readinessFacts?.map((fact, index) => <li key={`${fact.tugId}:${fact.source}:${fact.id ?? 'missing'}:${index}`}>
            <p>{label(fact.source)} · {label(fact.state)}</p>
            <p>{fact.tugId}{fact.id && ` / ${fact.id}`} · {t.revision}: {fact.revision ?? t.unknown}</p>
            <p>{t.observation}: {fact.observedAt ?? t.unknown}</p>
            <p>{fact.source === 'MaintenancePlan' ? dueReason(fact.why, language === 'en') : t.qualification}</p>
          </li>)}</ul></details>
      </>}
    </>}
  </>;
}
