import { localizedCopy } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { dailyApi, type NoonSummaryPage } from './daily-operations-api';
import type { ShipOpsKind, ShipOpsFields } from './ship-operation-fields';
import { shipOperationsLedgerApi, ticksToMinutes, type FuelLedger, type LedgerReviewState, type PortTimeline } from './ship-operations-ledger-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
const copy = localizedCopy('ShipOperationsLedgerPanel_tsx_copy');
type Loaded = { scope: string; fuel?: FuelLedger; port?: PortTimeline; noon?: NoonSummaryPage | null; failed?: boolean };
const tone = (state: LedgerReviewState) => state === 'REVIEW_REQUIRED' ? 'warning' as const : 'neutral' as const;
const known = (fields: ShipOpsFields, key: string) => fields[key]?.state === 'Known' ? fields[key].value! : null;
// DEV-100 ROB continuity / DEV-101 SOF timeline. Read-only review aid: never
// edits a record, never shows a green "ready" state, never fabricates a signature.
export function ShipOperationsLedgerPanel({ kind, vessel, language, onOpen }: { kind: ShipOpsKind; vessel: string; language: AppLocale; onOpen: (id: string) => void }) {
    const t = copy[language], epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [source, setSource] = useState<Loaded | null>(null);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = `${kind}/${vessel}/${epoch}/${attempt}`;
    const current = online && visible && source?.scope === scope ? source : null;
    useEffect(() => {
        if (!online || !visible) return;
        const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000);
        (async () => {
            if (kind === 'Fuel') {
                // Reviewed Noon context is optional: a missing Noon right must not hide the ledger.
                const [fuel, noon] = await Promise.all([shipOperationsLedgerApi.fuel(vessel, c.signal), dailyApi.noonSummary(vessel, 0, c.signal).catch(() => null)]);
                if (active) setSource({ scope, fuel, noon });
            } else {
                const port = await shipOperationsLedgerApi.port(vessel, c.signal);
                if (active) setSource({ scope, port });
            }
        })().catch(() => { if (active) setSource({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [kind, vessel, scope, online, visible]);
    const state = (value: LedgerReviewState) => <Badge tone={tone(value)}>{t.states[value]}</Badge>;
    const flags = (values: string[]) => values.length > 0 && <ul className="ship-operations-ledger-flags" aria-label={t.flags}>{values.map(flag => <li key={flag}><code>{flag}</code></li>)}</ul>;
    const body = !online || !visible ? <p role="status">{t.offline}</p>
        : !current ? <p role="status">{t.loading}</p>
            : current.failed ? <div role="alert"><p>{t.unavailable}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{t.retry}</Button></div>
                : <>
                    {(current.fuel ?? current.port)!.truncated && <p role="note">{t.truncated}</p>}
                    {(current.fuel ?? current.port)!.unplaced.length > 0 && <p role="note">{t.unplaced.replace('{{count}}', String((current.fuel ?? current.port)!.unplaced.length))}</p>}
                    {current.fuel && <p role="note">{t.tankBasis}</p>}
                    {current.fuel && (current.fuel.entries.length === 0 ? <p>{t.empty}</p> : <ol className="ship-operations-ledger">{current.fuel.entries.map(({ record, assessment }) => <li key={record.id}>
                        <p>{state(assessment.reviewState)} <time dateTime={known(record.fields, 'record_date') ?? undefined}>{known(record.fields, 'record_date')}</time> · {known(record.fields, 'fuel_type')} · {known(record.fields, 'record_type') ?? t.notDeclared}</p>
                        <dl className="ship-operations-summary-grid">
                            <div><dt>{t.balance}</dt><dd>{state(assessment.balance.state)} {assessment.balance.differenceMt !== null && <>{t.difference}: {assessment.balance.differenceMt} mt</>}{assessment.balance.reason && <> <code>{assessment.balance.reason}</code></>}</dd></div>
                            <div><dt>{t.continuity}</dt><dd>{state(assessment.continuity.state)} {assessment.continuity.differenceMt !== null && <>{t.difference}: {assessment.continuity.differenceMt} mt</>}{assessment.continuity.reason && <> <code>{assessment.continuity.reason}</code></>}</dd></div>
                        </dl>{assessment.basis.includes('HISTORY_TRUNCATED') && <p role="note">{t.historyBoundary}</p>}{flags(assessment.flags)}<Button variant="outline" onClick={() => onOpen(record.id)}>{t.open}</Button></li>)}</ol>)}
                    {current.port && (current.port.entries.length === 0 ? <p>{t.empty}</p> : <ol className="ship-operations-ledger">{current.port.entries.map(({ record, assessment }) => <li key={record.id}>
                        <p>{state(assessment.reviewState)} {known(record.fields, 'activity_type') ?? t.notDeclared} · <time dateTime={known(record.fields, 'event_start') ?? undefined}>{known(record.fields, 'event_start')}</time>{known(record.fields, 'event_end') && <> – <time dateTime={known(record.fields, 'event_end')!}>{known(record.fields, 'event_end')}</time></>}</p>
                        <dl className="ship-operations-summary-grid"><div><dt>{t.duration}</dt><dd>{assessment.durationState === 'KNOWN' ? t.minutes.replace('{{value}}', ticksToMinutes(assessment.durationTicks!)) : t.durationUnknown}</dd></div>
                            {known(record.fields, 'remarks') && <div><dt>{t.remarks}</dt><dd>{known(record.fields, 'remarks')}</dd></div>}
                            {known(record.fields, 'delay_reason') && <div><dt>{t.delay}</dt><dd>{known(record.fields, 'delay_reason')}</dd></div>}</dl>
                        {flags(assessment.flags)}<Button variant="outline" onClick={() => onOpen(record.id)}>{t.open}</Button></li>)}</ol>)}
                    {current.port && <p role="note">{t.signature}</p>}
                    {kind === 'Fuel' && <section aria-label={t.noonTitle}><h3>{t.noonTitle}</h3><p className="ship-operations-hint">{t.noonQualifier}</p>
                        {current.noon === null ? <p>{t.noonUnavailable}</p> : !current.noon?.items.some(item => item.state === 'MASTER_REVIEW_RECORDED') ? <p>{t.noonNone}</p>
                            : <ul>{current.noon.items.filter(item => item.state === 'MASTER_REVIEW_RECORDED').slice(0, 5).map(item => <li key={item.recordId}><Badge tone="neutral">{t.noonReviewed}</Badge> {item.fields.filter(field => ['report_date', 'fuel_summary', 'rob_summary'].includes(field.key)).map(field => field.value.state === 'Known' ? field.value.value : t.notDeclared).join(' · ')}</li>)}</ul>}
                    </section>}
                </>;
    return <section className="ship-operations-ledger-panel" aria-label={kind === 'Fuel' ? t.fuelTitle : t.portTitle}><h2>{kind === 'Fuel' ? t.fuelTitle : t.portTitle}</h2><p className="ship-operations-hint">{kind === 'Fuel' ? t.fuelIntro : t.portIntro}</p>{body}</section>;
}
