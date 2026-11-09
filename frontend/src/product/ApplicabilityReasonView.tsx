import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { ReactNode } from 'react';
import { Badge } from '../components/ui/badge';
import type { EvaluationResult, Reason } from './applicability-api';
function reasonTree(reason: Reason, en: boolean): ReactNode {
    return <li key={reason.path}><strong>{reason.field ?? reason.operator ?? reason.path}</strong> · {reason.truth === 0 ? (localizedText(en, "pairs.m_a5a7675bd7000dbd")) : reason.truth === 1 ? (localizedText(en, "pairs.m_f2b1c53b82a3e806")) : (localizedText(en, "pairs.m_129a7406352aa95e"))} · {reason.reason}{reason.reviewRequired && <Badge tone="warning">{localizedText(en, "pairs.m_6edb7d9a4aca3fd7")}</Badge>}{reason.children.length > 0 && <ul>{reason.children.map(child => reasonTree(child, en))}</ul>}</li>;
}
export function ApplicabilityReasonView({ results, language }: {
    results: EvaluationResult[];
    language: AppLocale;
}) {
    const en = language === 'en', unknown = localizedText(en, "pairs.m_49138f2831bf4262");
    return <div className="applicability-reason-list">{results.map(row => <details key={`${row.ruleId}/${row.revision}`}><summary>{localizedText(en, "pairs.m_cc646e6a24f49b58")} {row.ruleId} / {row.revision} · {row.prediction.result}</summary><dl><div><dt>{localizedText(en, "pairs.m_f359418e02fd1952")}</dt><dd>{row.requirementId ?? unknown} / {row.requirementRevision ?? unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_b4f1fa82036ef943")}</dt><dd>{row.sourceId ?? unknown} / {row.sourceRevision ?? unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_d07a98f15fab786c")}</dt><dd>{row.prediction.lifecycle}</dd></div><div><dt>{localizedText(en, "pairs.m_0c7a1a0f59ea9eba")}</dt><dd>{row.prediction.currentResult}</dd></div></dl><p>{row.prediction.authoritative ? (localizedText(en, "pairs.m_6eef465495000c01")) : (localizedText(en, "pairs.m_c9a031a45f1fb289"))}</p><ul aria-label={localizedText(en, "pairs.m_e621bd7b50b95509")}>{reasonTree(row.prediction.reason, en)}</ul><ul>{row.prediction.blockers.map((code, index) => <li key={`${code}/${index}`}>{code}</li>)}</ul><ul>{row.prediction.predictedActivations.map(a => <li key={`${a.type}:${a.id}`}>{a.type}: {a.id} · {localizedText(en, "pairs.m_ce4d519d3aed942d")}</li>)}</ul><p>{localizedText(en, "pairs.m_016511e890a7e5f7")}: <code>{row.hash}</code></p></details>)}</div>;
}
// Inputs are already admitted by the strict owning snapshot decoder. Keep this
// presentation read-only; showing a captured fact does not re-qualify it.
export function CapturedEvaluationInputs({ inputs, inputHash, ruleSetHash, language }: {
    inputs: Record<string, unknown> | null;
    inputHash: string | null;
    ruleSetHash: string | null;
    language: AppLocale;
}) {
    const en = language === 'en', unavailable = localizedText(en, "pairs.m_49138f2831bf4262");
    const facts = inputs?.facts;
    const groups: {
        title: string;
        facts: Record<string, unknown>;
    }[] = [];
    if (facts && typeof facts === 'object' && !Array.isArray(facts))
        groups.push({ title: localizedText(en, "pairs.m_68ac94b4cb06cd3b"), facts: facts as Record<string, unknown> });
    const qualifications = inputs?.qualifications;
    if (Array.isArray(qualifications))
        for (const qualification of qualifications) {
            if (qualification && typeof qualification === 'object' && !Array.isArray(qualification)) {
                const q = qualification as Record<string, unknown>;
                if (q.facts && typeof q.facts === 'object' && !Array.isArray(q.facts))
                    groups.push({ title: `${localizedText(en, "pairs.m_c56b04f75006df7a")}: ${String(q.ruleId)} / ${String(q.revision)}`, facts: q.facts as Record<string, unknown> });
            }
        }
    return <section aria-label={localizedText(en, "pairs.m_daf010658701387c")}><h4>{localizedText(en, "pairs.m_daf010658701387c")}</h4><p>{localizedText(en, "pairs.m_21a06d6ac4219d70")}</p><dl className="onboarding-review-values"><div><dt>{localizedText(en, "pairs.m_9f52c45921eae919")}</dt><dd><code>{inputHash ?? unavailable}</code></dd></div><div><dt>{localizedText(en, "pairs.m_fe0d0b84603a3029")}</dt><dd><code>{ruleSetHash ?? unavailable}</code></dd></div><div><dt>{localizedText(en, "pairs.m_45e809424a7c815e")}</dt><dd>{typeof inputs?.asOf === 'string' ? inputs.asOf : unavailable}</dd></div></dl>{!groups.length && <p>{unavailable}</p>}{groups.map(group => <details key={group.title}><summary>{group.title}</summary><dl className="onboarding-review-values">{Object.entries(group.facts).map(([key, fact]) => {
                if (!fact || typeof fact !== 'object' || Array.isArray(fact))
                    return null;
                const value = fact as Record<string, unknown>;
                return <div key={key}><dt>{key}</dt><dd>{String(value.knowledge)} · {value.value === null ? unavailable : String(value.value)}<p>{localizedText(en, "pairs.m_dcd168206de47c70")}: {typeof value.observedAt === 'string' ? value.observedAt : unavailable} / {typeof value.freshUntil === 'string' ? value.freshUntil : unavailable}</p><p>{localizedText(en, "pairs.m_40f8162560856d8a")}: {String(value.provenance)} / {String(value.revision)}</p></dd></div>;
            })}</dl></details>)}</section>;
}
