import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { PlanningEvidence } from './tug-planning-api';
import { TugCrewCoverage } from './TugCrewCoverage';
export function TugOwningEvidence({ value, language }: {
    value: PlanningEvidence | null;
    language: AppLocale;
}) {
    const state = (s: string) => ({ CurrentPlanningCapacity: localizedText(language, "pairs.m_1fd8aefc57a4c334"), Blocked: localizedText(language, "pairs.m_a3c928906659adcf"), ReviewRequired: localizedText(language, "pairs.m_6edb7d9a4aca3fd7"), Unverified: localizedText(language, "pairs.m_b980704b1fa89cd9"), ChangedInput: localizedText(language, "pairs.m_3a996a44383b83fd"), Expired: localizedText(language, "pairs.m_c308d69700588faa"), SourceReviewRequired: localizedText(language, "pairs.m_300952988ccc08fd"), UnsupportedContext: localizedText(language, "pairs.m_f677b1f4452aa20f"), Missing: localizedText(language, "pairs.m_a5bd35d90de7c160"), Unknown: localizedText(language, "pairs.m_a5a7675bd7000dbd"), Unavailable: localizedText(language, "pairs.m_ed3d73c9d7e984d7") }[s] ?? localizedText(language, "pairs.m_6edb7d9a4aca3fd7"));
    return <section aria-label={localizedText(language, "pairs.m_db0a99664b91c94a")}>
    <h4>{localizedText(language, "pairs.m_db0a99664b91c94a")}</h4>
    <p>{localizedText(language, "pairs.m_38df4f28283f892f")}</p>
    {!value && <p role="status">{localizedText(language, "pairs.m_591a5f0c68122c58")}</p>}
    {value?.crew && <TugCrewCoverage items={value.crew} language={language}/>}
    <ul>{value?.ownedBp?.map(b => <li key={b.tugId}>{b.tugId} · {state(b.state)} · {b.tonnes ?? '—'} t
      <p>{localizedText(language, "pairs.m_423d1061b74e5d2b")}: {b.why} · {localizedText(language, "pairs.m_4ca25dd50654ea33")}: {b.evidenceRevision ?? '—'} · {localizedText(language, "pairs.m_0c55e4db9712910b")}: {b.conditionRevision ?? '—'}</p>
      <p>{localizedText(language, "pairs.m_52b3bd6be5980b92")}: {b.qualificationRevision ?? '—'} · {localizedText(language, "pairs.m_f11e257c8459d2a3")}: {b.observedAt ?? '—'} · {localizedText(language, "pairs.m_3d36fc578d03f5ee")}: {b.validUntil ?? '—'}</p>
      <a href="/app/evidence-graph">{localizedText(language, "pairs.m_029b894a3be6054a")}</a>
    </li>)}</ul>
    <ul>{value?.readinessFacts?.map((f, i) => <li key={`${f.tugId}/${f.source}/${f.id}/${i}`}>{f.tugId} · {f.state === 'Current' ? localizedText(language, "pairs.m_a11ae0ec6293628d") : state(f.state)} · {f.source} · r{f.revision ?? '—'} · {f.why} · {f.observedAt ?? '—'} · {f.expiresAt ?? '—'}</li>)}</ul>
    <ul aria-label={localizedText(language, "pairs.m_393d0b1d13189bf4")}>{value?.operationalConflicts?.map(c => <li key={`${c.tugId}/${c.kind}/${c.resourceId}`}>{c.tugId} · {localizedText(language, "pairs.m_ba1120f4fa277acf")} · {c.kind} · r{c.revision} · {c.why} · {c.provenance} · {c.observedAt ?? '—'}</li>)}</ul>
  </section>;
}
