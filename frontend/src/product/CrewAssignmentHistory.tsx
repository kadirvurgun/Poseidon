import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { ApiError } from './api';
import { history, type CrewHistoryRow } from './crew-api';
export function CrewAssignmentHistory({ vessel, assignment, language }: {
    vessel: string;
    assignment: string;
    language: AppLocale;
}) {
    const en = language === 'en', [rows, setRows] = useState<CrewHistoryRow[] | null>(null), [error, setError] = useState(0), [generation, reload] = useState(0);
    const [offset, setOffset] = useState(0), [more, setMore] = useState(false);
    const resetScope1 = [vessel, assignment, generation, offset] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], assignment) || !Object.is(previousScope1[2], generation) || !Object.is(previousScope1[3], offset)) {
        setPreviousScope1(resetScope1);
        setRows(null);
        setError(0);
    }
    useEffect(() => {
        const c = new AbortController();
        history(vessel, assignment, c.signal, offset).then(v => { if (!c.signal.aborted) {
            setRows(v.items);
            setMore(v.hasMore);
        } })
            .catch(e => { if (!c.signal.aborted) {
            setRows(null);
            setError(e instanceof ApiError ? e.status : 503);
        } });
        return () => c.abort();
    }, [vessel, assignment, generation, offset]);
    return <section aria-label={localizedText(en, "pairs.m_500bf5aecb194f2f")}>
    <h2>{localizedText(en, "pairs.m_500bf5aecb194f2f")}</h2>
    <p>{localizedText(en, "pairs.m_1f5b61bbe470c744")}</p>
    {!rows && !error && <p role="status">{localizedText(en, "pairs.m_9e04b8b6eafff876")}</p>}
    {error > 0 && <p role="alert">{[401, 403, 404].includes(error) ? (localizedText(en, "pairs.m_896fd55df6a10428")) : (localizedText(en, "pairs.m_73fce7218f90e9fc"))}</p>}
    <button onClick={() => reload(x => x + 1)}>{localizedText(en, "pairs.m_d5bf490df907fe28")}</button>
    <button disabled={!rows || offset === 0} onClick={() => setOffset(x => Math.max(0, x - 25))}>{localizedText(en, "pairs.m_d085c88e5e284a56")}</button>
    <button disabled={!rows || !more} onClick={() => setOffset(x => x + 25)}>{localizedText(en, "pairs.m_4763364381a5af10")}</button>
    {rows && (rows.length === 0 ? <p>{localizedText(en, "pairs.m_c97cdf787e378a37")}</p> : <ol>{rows.map(r => <li key={r.revision}>
      {r.revision} · {r.status} · {r.from ?? (localizedText(en, "pairs.m_a5a7675bd7000dbd"))} — {r.to ?? (localizedText(en, "pairs.m_a5a7675bd7000dbd"))}
      <dl><dt>{localizedText(en, "pairs.m_d7ddba8b10c848cd")}</dt><dd>{r.savedAt}</dd><dt>{localizedText(en, "pairs.m_499af7a3eda46ca2")}</dt><dd>{r.assignmentPolicyVersion ?? (localizedText(en, "pairs.m_a7854587f4af1a4c"))}</dd>
        <dt>{localizedText(en, "pairs.m_9f8ec433e53371b4")}</dt><dd>{r.positionPolicyVersion ?? (localizedText(en, "pairs.m_a7854587f4af1a4c"))}</dd></dl>
    </li>)}</ol>)}
  </section>;
}
