import { localizedCopy } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useEffect, useRef, useState } from 'react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterprisePageHeader } from './EnterprisePatterns';
import { ApiError, session as readSession } from './api';
import type { Session } from './directory-contract';
import { positions as crewPositions } from './crew-api';
import { crewComplianceApi as api, credentialTone, eligibilityTone, type CommandOutcome, type EligibilityResult } from './crew-compliance-api';
import './crew-enterprise.css';

const copy = localizedCopy('CrewCompliance_tsx_copy');
type Copy = (typeof copy)['en'];
export type CrewComplianceView = 'certificates' | 'matrix' | 'safe-manning';
const today = () => new Date().toISOString().slice(0, 10);
const label = (t: Copy, prefix: string, code: string) => (t as Record<string, string>)[prefix + code] ?? code;

// One frozen attempt per human act (exact key and body); an unknown outcome keeps it so a retry replays once.
function useCommand(onDone: () => void) {
    const [busy, setBusy] = useState(false), [failure, setFailure] = useState<string | null>(null), [done, setDone] = useState(false);
    const frozen = useRef<{ key: string; body: string } | null>(null);
    async function run<T>(body: T, call: (body: T, csrf: string, key: string, signal: AbortSignal) => Promise<CommandOutcome>) {
        if (busy) return false;
        const serialized = JSON.stringify(body);
        if (frozen.current?.body !== serialized) frozen.current = { key: crypto.randomUUID(), body: serialized };
        setBusy(true); setFailure(null); setDone(false);
        const c = new AbortController(), timer = setTimeout(() => c.abort(), 20000);
        try {
            const user = await readSession(c.signal);
            await call(body, user.csrfToken, frozen.current.key, c.signal);
            frozen.current = null; setDone(true); onDone(); return true;
        } catch (error) {
            const problem = error instanceof ApiError ? error : new ApiError(0);
            const unknown = problem.status === 0 || problem.status >= 500;
            if (!unknown) frozen.current = null;
            setFailure(unknown ? 'outcome_unknown' : problem.code ?? String(problem.status)); return false;
        } finally { clearTimeout(timer); setBusy(false); }
    }
    return { busy, failure, done, run };
}
function CommandStatus({ t, command }: { t: Copy; command: ReturnType<typeof useCommand> }) {
    return <>{command.done && <p role="status">{t.done}</p>}{command.failure && <p role="alert">{command.failure === 'outcome_unknown' ? t.unknownOutcome
        : command.failure === 'version_conflict' || command.failure === 'evaluation_stale' ? t.conflict : t.failed.replace('{{code}}', command.failure)}</p>}</>;
}
function useLoad<T>(load: (signal: AbortSignal) => Promise<T>, scope: string) {
    const [state, setState] = useState<{ scope: string; value?: T; status?: number } | null>(null), [attempt, retry] = useState(0);
    const key = `${scope}/${attempt}`, online = navigator.onLine;
    useEffect(() => {
        if (!online) return;
        const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 15000);
        load(c.signal).then(value => { if (active) setState({ scope: key, value }); },
            e => { if (active) setState({ scope: key, status: e instanceof ApiError ? e.status : 503 }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [key, online]);
    return { current: online && state?.scope === key ? state : null, online, reload: () => retry(n => n + 1) };
}
function Loading<T>({ t, view, children }: { t: Copy; view: ReturnType<typeof useLoad<T>>; children: (value: T) => React.ReactNode }) {
    if (!view.online) return <p role="status">{t.offline}</p>;
    if (!view.current) return <p role="status">{t.loading}</p>;
    if (view.current.status || view.current.value === undefined) return <div role="alert"><p>{view.current.status === 403 || view.current.status === 404 ? t.denied : t.unavailable}</p>
        <Button variant="outline" onClick={view.reload}>{t.retry}</Button></div>;
    return <>{children(view.current.value)}</>;
}
function Reasons({ t, result }: { t: Copy; result: Pick<EligibilityResult, 'reasons'> }) {
    if (result.reasons.length === 0) return null;
    return <ul className="crew-compliance-reasons" aria-label={t.colReasons}>{result.reasons.map((r, i) =>
        <li key={i}><Badge tone={eligibilityTone(r.severity)}>{label(t, 'state_', r.severity)}</Badge> <code>{r.code}</code> <span>{label(t, 'dim_', r.dimension)}</span></li>)}</ul>;
}

// ---- DEV-048 crew certificates register + verification assertion ----
function Verification({ t, vessel, assignment, id, onChanged }: { t: Copy; vessel: string; assignment: string; id: string; onChanged: () => void }) {
    const view = useLoad(signal => api.verification(vessel, assignment, id, signal), `${vessel}/${assignment}/${id}`);
    const command = useCommand(() => { view.reload(); onChanged(); });
    const [authority, setAuthority] = useState(''), [state, setState] = useState(''), [rationale, setRationale] = useState('');
    return <Loading t={t} view={view}>{v => <div className="crew-compliance-detail">
        <p>{t.assertionNote}</p>
        {v.blockers.length > 0 && <ul aria-label={t.blockers}>{v.blockers.map(b => <li key={b}><code>{b}</code></li>)}</ul>}
        {v.history.length > 0 && <table><caption>{t.history}</caption><tbody>{v.history.map(a => <tr key={a.id}><td>#{a.sequence}</td><td>{a.state}</td><td>{a.duty}</td>
            <td>{a.savedAt.slice(0, 10)}</td><td>{a.current ? t.current : t.notCurrent}</td></tr>)}</tbody></table>}
        {v.canRecord ? <form onSubmit={e => { e.preventDefault(); void command.run({ credentialRevision: v.credentialRevision, expectedSequence: v.sequence, authorityId: authority, state, rationale },
            (body, csrf, key, signal) => api.recordAssertion(vessel, assignment, id, body, csrf, key, signal)); }}>
            <label>{t.authority}<select required value={authority} onChange={e => setAuthority(e.target.value)}><option value="">—</option>{v.authorities.map(a => <option key={a.id} value={a.id}>{a.duty}</option>)}</select></label>
            <label>{t.assertionState}<select required value={state} onChange={e => setState(e.target.value)}><option value="">—</option>{v.states.map(s => <option key={s}>{s}</option>)}</select></label>
            <label>{t.rationale}<textarea required maxLength={2000} value={rationale} onChange={e => setRationale(e.target.value)}/></label>
            <Button type="submit" disabled={command.busy || !authority || !state || !rationale.trim()}>{t.verify}</Button>
        </form> : <p role="status">{t.noAuthority}</p>}
        <CommandStatus t={t} command={command}/>
    </div>}</Loading>;
}
function Certificates({ t, vessel }: { t: Copy; vessel: string }) {
    const view = useLoad(signal => api.register(vessel, signal), vessel);
    const [open, setOpen] = useState<string | null>(null);
    return <Loading t={t} view={view}>{v => v.items.length === 0 ? <p>{t.empty}</p> : <>
        {v.truncated && <p role="status">{t.truncated}</p>}
        <table className="crew-compliance-table"><thead><tr><th>{t.colType}</th><th>{t.colExpiry}</th><th>{t.colEndorsement}</th><th>{t.colStatus}</th><th/></tr></thead>
        <tbody>{v.items.map(r => <tr key={r.credentialId}><td>{r.certificateTypeReference ?? t.unknown}</td><td>{r.expiryDate ?? t.unknown}</td>
            <td>{r.endorsementRequired === false ? t.notRequired : r.endorsementExpiry ?? t.unknown}</td>
            <td><Badge tone={credentialTone(r.status)}>{label(t, 'cert_', r.status)}</Badge></td>
            <td><Button variant="outline" aria-expanded={open === r.credentialId} onClick={() => setOpen(o => o === r.credentialId ? null : r.credentialId)}>{t.verify}</Button></td></tr>)}</tbody></table>
        {open && (() => { const row = v.items.find(r => r.credentialId === open)!; return <Verification key={open} t={t} vessel={vessel} assignment={row.assignmentId} id={open} onChanged={view.reload}/>; })()}
    </>}</Loading>;
}

// ---- DEV-049 training-position / competence matrix ----
const requirementTypes = ['CERTIFICATE', 'ENDORSEMENT', 'MEDICAL', 'TRAINING', 'COMPETENCE', 'FAMILIARIZATION'];
function Matrix({ t, vessel }: { t: Copy; vessel: string }) {
    const view = useLoad(signal => api.matrix(vessel, signal), vessel);
    const positions = useLoad(signal => crewPositions(vessel, signal), `positions/${vessel}`);
    const options = positions.current?.value ?? [];
    const [draft, setDraft] = useState<{ id: string; expectedVersion: number | null; positionId: string; requirementType: string; reference: string; mandatory: boolean;
        validityMonths: string; vesselTypeScope: string; cargoScope: string; status: string } | null>(null);
    const command = useCommand(() => { view.reload(); setDraft(null); });
    const save = () => draft && void command.run({ ...draft, validityMonths: draft.validityMonths ? Number(draft.validityMonths) : null, vesselTypeScope: draft.vesselTypeScope || null,
        cargoScope: draft.cargoScope || null, competenceLinkId: null, familiarizationLinkId: null }, (body, csrf, key, signal) => api.saveRequirement(vessel, body, csrf, key, signal));
    return <Loading t={t} view={view}>{v => <>
        <p>{t.trainingNote}</p>
        {v.items.length === 0 ? <p>{t.empty}</p> : <table className="crew-compliance-table"><thead><tr><th>{t.colPosition}</th><th>{t.colRequirement}</th><th>{t.reference}</th>
            <th>{t.colMandatory}</th><th>{t.colValidity}</th><th>{t.colScope}</th><th>{t.status}</th><th/></tr></thead>
        <tbody>{v.items.map(r => <tr key={r.id}><td>{r.positionCode ?? t.unknown}</td><td>{label(t, 'type_', r.requirementType)}</td><td>{r.reference}</td>
            <td>{r.mandatory ? t.yes : t.no}</td><td>{r.validityMonths ?? '—'}</td><td>{[r.vesselTypeScope, r.cargoScope].filter(Boolean).join(' / ') || '—'}</td><td>{r.status}</td>
            <td>{v.canWrite && <Button variant="outline" disabled={!!draft} onClick={() => setDraft({ id: r.id, expectedVersion: Number(r.version), positionId: r.positionId, requirementType: r.requirementType,
                reference: r.reference, mandatory: r.mandatory, validityMonths: r.validityMonths?.toString() ?? '', vesselTypeScope: r.vesselTypeScope ?? '', cargoScope: r.cargoScope ?? '', status: r.status })}>{t.edit}</Button>}</td></tr>)}</tbody></table>}
        {v.canWrite && !draft && options.length > 0 && <Button onClick={() => setDraft({ id: crypto.randomUUID(), expectedVersion: null, positionId: options[0].id, requirementType: 'CERTIFICATE',
            reference: '', mandatory: true, validityMonths: '', vesselTypeScope: '', cargoScope: '', status: 'ACTIVE' })}>{t.add}</Button>}
        {draft && <form onSubmit={e => { e.preventDefault(); save(); }}>
            <label>{t.colPosition}<select value={draft.positionId} disabled={draft.expectedVersion !== null} onChange={e => setDraft({ ...draft, positionId: e.target.value })}>
                {[...new Map([...options.map(p => [p.id, p.code] as const), ...v.items.map(r => [r.positionId, r.positionCode ?? r.positionId] as const)]).entries()].map(([id, code]) => <option key={id} value={id}>{code}</option>)}</select></label>
            <label>{t.colRequirement}<select value={draft.requirementType} disabled={draft.expectedVersion !== null} onChange={e => setDraft({ ...draft, requirementType: e.target.value })}>
                {requirementTypes.map(x => <option key={x} value={x}>{label(t, 'type_', x)}</option>)}</select></label>
            <label>{t.reference}<input required maxLength={200} value={draft.reference} onChange={e => setDraft({ ...draft, reference: e.target.value })}/></label>
            <label><input type="checkbox" checked={draft.mandatory} onChange={e => setDraft({ ...draft, mandatory: e.target.checked })}/>{t.colMandatory}</label>
            <label>{t.colValidity}<input type="number" min={1} max={240} value={draft.validityMonths} onChange={e => setDraft({ ...draft, validityMonths: e.target.value })}/></label>
            <label>{t.vesselScope}<input maxLength={100} value={draft.vesselTypeScope} onChange={e => setDraft({ ...draft, vesselTypeScope: e.target.value })}/></label>
            <label>{t.cargoScope}<input maxLength={100} value={draft.cargoScope} onChange={e => setDraft({ ...draft, cargoScope: e.target.value })}/></label>
            <label>{t.status}<select value={draft.status} onChange={e => setDraft({ ...draft, status: e.target.value })}><option>ACTIVE</option><option>RETIRED</option></select></label>
            <Button variant="outline" type="button" onClick={() => setDraft(null)}>{t.cancel}</Button><Button type="submit" disabled={command.busy || !draft.reference.trim()}>{t.save}</Button>
        </form>}
        <CommandStatus t={t} command={command}/>
    </>}</Loading>;
}

// ---- DEV-050 advisory Safe Manning evaluation + human decision ----
type DispensationDraft = { evidence: string; post: string; validFrom: string; validTo: string; below: boolean; forceMajeure: boolean };
const emptyDispensation = (w: { from: string; to: string }): DispensationDraft => ({ evidence: '', post: '', validFrom: w.from, validTo: w.to, below: false, forceMajeure: false });
// The server re-checks every field; the form only carries the human-supplied record and link.
function toDispensation(assignmentId: string, positionId: string, d: DispensationDraft) {
    const [evidenceKind = '', evidenceId = '', revision = '0'] = d.evidence.split('|');
    return { evidenceKind, evidenceId, evidenceRevision: Number(revision), assignmentId, positionId, post: d.post, validFrom: d.validFrom, validTo: d.validTo,
        holdsCertificateForPostImmediatelyBelow: d.below, forceMajeureDeclared: d.forceMajeure };
}
function SafeManning({ t, vessel }: { t: Copy; vessel: string }) {
    const [from, setFrom] = useState(today()), [to, setTo] = useState(today()), [applied, setApplied] = useState({ from: today(), to: today() });
    const view = useLoad(signal => api.evaluate(vessel, applied.from, applied.to, signal), `${vessel}/${applied.from}/${applied.to}`);
    const command = useCommand(() => view.reload());
    const [authority, setAuthority] = useState(''), [decision, setDecision] = useState(''), [rationale, setRationale] = useState('');
    // SOLAS V/14, A.1047(27), STCW Art. VIII: per-reason acknowledgements and Administration dispensations.
    const [acks, setAcks] = useState<Record<string, string>>({});
    const [dispensations, setDispensations] = useState<Record<string, DispensationDraft>>({});
    return <>
        <form className="crew-compliance-window" onSubmit={e => { e.preventDefault(); setApplied({ from, to }); }}>
            <label>{t.from}<input type="date" required value={from} onChange={e => setFrom(e.target.value)}/></label>
            <label>{t.to}<input type="date" required value={to} min={from} onChange={e => setTo(e.target.value)}/></label>
            <Button type="submit" variant="outline">{t.evaluate}</Button>
        </form>
        <p role="note">{t.decisionNote}</p>
        <Loading t={t} view={view}>{v => <>
            <p><Badge tone={eligibilityTone(v.evaluation.state)}>{label(t, 'state_', v.evaluation.state)}</Badge> {t.msmd}: <code>{v.evaluation.msmdState}</code></p>
            <Reasons t={t} result={v.evaluation}/>
            {v.evaluation.roles.length > 0 && <table className="crew-compliance-table"><caption>{t.roles}</caption><thead><tr><th>{t.colPosition}</th><th>{t.required}</th><th>{t.qualified}</th><th>{t.unknownCount}</th><th>{t.status}</th></tr></thead>
                <tbody>{v.evaluation.roles.map(r => <tr key={r.positionId}><td><code>{r.positionId.slice(0, 8)}</code></td><td>{r.required}</td><td>{r.qualified}</td><td>{r.unknown}</td><td>{r.state}</td></tr>)}</tbody></table>}
            {v.evaluation.members.length === 0 ? <p>{t.empty}</p> : <table className="crew-compliance-table"><caption>{t.members}</caption><thead><tr><th>{t.colAssignment}</th><th>{t.status}</th><th>{t.colReasons}</th></tr></thead>
                <tbody>{v.evaluation.members.map(m => <tr key={m.assignmentId}><td><a href={`/app/crew-roster/${vessel}/crew-profile/${m.assignmentId}`}>{m.assignmentId.slice(0, 8)}</a></td>
                    <td><Badge tone={eligibilityTone(m.state)}>{label(t, 'state_', m.state)}</Badge></td><td>{m.reasons.map(r => r.code).join(', ')}</td></tr>)}</tbody></table>}
            {v.latestDecision && <p role="status">{t.latestDecision}: {label(t, 'dec_', v.latestDecision.decision)} · {v.latestDecision.duty} · {v.latestDecision.savedAt.slice(0, 10)} · {label(t, 'state_', v.latestDecision.evaluationState)}</p>}
            {v.canDecide ? <form onSubmit={e => { e.preventDefault(); const accepting = decision === 'MANNING_ACCEPTED' && v.evaluation.state !== 'ELIGIBLE' && v.evaluation.state !== 'ATTENTION';
                void command.run({ from: applied.from, to: applied.to, evaluationHash: v.evaluation.hash, expectedSequence: v.sequence, authorityId: authority, decision, rationale,
                    acknowledgements: accepting ? (v.reviewReasonCodes ?? []).map(code => ({ reasonCode: code, statement: acks[code] ?? '' })) : null,
                    dispensations: accepting && v.evaluation.state === 'NOT_ELIGIBLE' ? (v.dispensationMembers ?? []).map(m => toDispensation(m.assignmentId, m.positionId ?? '', dispensations[m.assignmentId] ?? emptyDispensation(applied))) : null },
                    (body, csrf, key, signal) => api.recordDecision(vessel, body, csrf, key, signal)); }}>
                <h3>{t.decision}</h3>
                <ul className="crew-compliance-rules" aria-label={t.rulesTitle}><li>{t.ruleMsmd}</li><li>{t.ruleDispensation}</li><li>{t.ruleCommand}</li></ul>
                <label>{t.authority}<select required value={authority} onChange={e => setAuthority(e.target.value)}><option value="">—</option>{v.authorities.map(a => <option key={a.id} value={a.id}>{a.duty}</option>)}</select></label>
                <label>{t.decision}<select required value={decision} onChange={e => setDecision(e.target.value)}><option value="">—</option>{v.decisions.map(d => <option key={d} value={d}>{label(t, 'dec_', d)}</option>)}</select></label>
                <label>{t.rationale}<textarea required maxLength={2000} value={rationale} onChange={e => setRationale(e.target.value)}/></label>
                {decision === 'MANNING_ACCEPTED' && v.evaluation.state !== 'ELIGIBLE' && v.evaluation.state !== 'ATTENTION' && (v.reviewReasonCodes ?? []).length > 0 &&
                    <fieldset><legend>{t.acknowledgeTitle}</legend><p>{t.acknowledgeNote}</p>{(v.reviewReasonCodes ?? []).map(code => <label key={code}><code>{code}</code>
                        <textarea required maxLength={500} aria-label={`${t.acknowledgeTitle}: ${code}`} value={acks[code] ?? ''} onChange={e => setAcks({ ...acks, [code]: e.target.value })}/></label>)}</fieldset>}
                {decision === 'MANNING_ACCEPTED' && v.evaluation.state === 'NOT_ELIGIBLE' && <fieldset><legend>{t.dispensationTitle}</legend><p>{t.dispensationNote}</p>
                    {(v.evidenceOptions ?? []).length === 0 && <p role="alert">{t.noDispensationEvidence}</p>}
                    {(v.dispensationMembers ?? []).map(m => { const d = dispensations[m.assignmentId] ?? emptyDispensation(applied); const set = (patch: Partial<DispensationDraft>) => setDispensations({ ...dispensations, [m.assignmentId]: { ...d, ...patch } });
                        return <div key={m.assignmentId} className="crew-compliance-dispensation"><p><code>{m.assignmentId.slice(0, 8)}</code></p>
                            <label>{t.dispensationEvidence}<select required value={d.evidence} onChange={e => set({ evidence: e.target.value })}><option value="">—</option>
                                {(v.evidenceOptions ?? []).map(o => <option key={`${o.kind}|${o.id}|${o.revision}`} value={`${o.kind}|${o.id}|${o.revision}`}>{o.label}</option>)}</select></label>
                            <label>{t.post}<select required value={d.post} onChange={e => set({ post: e.target.value })}><option value="">—</option>{(v.posts ?? []).map(x => <option key={x} value={x}>{label(t, 'post_', x)}</option>)}</select></label>
                            <label>{t.from}<input type="date" required value={d.validFrom} onChange={e => set({ validFrom: e.target.value })}/></label>
                            <label>{t.to}<input type="date" required value={d.validTo} onChange={e => set({ validTo: e.target.value })}/></label>
                            <label><input type="checkbox" checked={d.below} onChange={e => set({ below: e.target.checked })}/>{t.holdsBelow}</label>
                            <label><input type="checkbox" checked={d.forceMajeure} onChange={e => set({ forceMajeure: e.target.checked })}/>{t.forceMajeure}</label></div>; })}</fieldset>}
                <Button type="submit" disabled={command.busy || !authority || !decision || !rationale.trim()}>{t.decision}</Button>
            </form> : <p role="status">{t.noAuthority}</p>}
            <CommandStatus t={t} command={command}/>
        </>}</Loading>
    </>;
}

// ---- DEV-049 per-assignment competence gaps (crew profile panel) ----
export function CrewCompetencePanel({ vessel, assignment, language }: { vessel: string; assignment: string; language: AppLocale }) {
    const t = copy[language];
    const view = useLoad(signal => api.competence(vessel, assignment, today(), today(), signal), `${vessel}/${assignment}`);
    const command = useCommand(() => view.reload());
    const [record, setRecord] = useState<{ requirementId: string; kind: string; completedOn: string; validUntil: string } | null>(null);
    const [signing, setSigning] = useState<{ id: string; revision: number; sequence: number; authorityId: string; outcome: string; rationale: string } | null>(null);
    return <section className="crew-compliance-panel" aria-label={t.competenceTitle}><h2>{t.competenceTitle}</h2><p>{t.trainingNote}</p>
        <Loading t={t} view={view}>{v => <>
            <p><Badge tone={eligibilityTone(v.evaluation.state)}>{label(t, 'state_', v.evaluation.state)}</Badge></p>
            <Reasons t={t} result={v.evaluation}/>
            {v.evaluation.gaps.length > 0 && <table className="crew-compliance-table"><caption>{t.gaps}</caption><tbody>{v.evaluation.gaps.map(g => <tr key={g.requirementId}>
                <td>{label(t, 'type_', g.type)}</td><td>{g.mandatory ? t.yes : t.no}</td><td>{g.state}</td><td><code>{g.reason}</code></td>
                <td>{v.canWrite && !['CERTIFICATE', 'ENDORSEMENT', 'MEDICAL'].includes(g.type) && <Button variant="outline" disabled={!!record} onClick={() =>
                    setRecord({ requirementId: g.requirementId, kind: g.type === 'TRAINING' ? 'TRAINING_COMPLETION' : g.type === 'FAMILIARIZATION' ? 'FAMILIARIZATION' : 'COMPETENCE_ASSESSMENT', completedOn: '', validUntil: '' })}>{t.recordAdd}</Button>}</td></tr>)}</tbody></table>}
            {record && <form onSubmit={e => { e.preventDefault(); void command.run({ id: crypto.randomUUID(), expectedVersion: null, requirementId: record.requirementId, kind: record.kind,
                completedOn: record.completedOn || null, validUntil: record.validUntil || null, reference: null }, (body, csrf, key, signal) => api.saveRecord(vessel, assignment, body, csrf, key, signal)).then(ok => { if (ok) setRecord(null); }); }}>
                <p>{label(t, 'kind_', record.kind)}</p>
                <label>{t.completedOn}<input type="date" value={record.completedOn} max={today()} onChange={e => setRecord({ ...record, completedOn: e.target.value })}/></label>
                <label>{t.validUntil}<input type="date" value={record.validUntil} onChange={e => setRecord({ ...record, validUntil: e.target.value })}/></label>
                <Button variant="outline" type="button" onClick={() => setRecord(null)}>{t.cancel}</Button><Button type="submit" disabled={command.busy}>{t.save}</Button></form>}
            {v.records.length > 0 && <table className="crew-compliance-table"><caption>{t.records}</caption><tbody>{v.records.map(r => <tr key={r.id}>
                <td>{label(t, 'kind_', r.kind)}</td><td>{r.completedOn ?? t.unknown}</td><td>{r.validUntil ?? '—'}</td>
                <td>{r.signOff ? `${r.signOff.outcome} · ${r.signOff.duty}${r.signOff.current ? '' : ' · ' + t.notCurrent}` : t.notSignedOff}</td>
                <td>{v.canSignOff && !r.actorIsRecorder && <Button variant="outline" disabled={!!signing} onClick={() => setSigning({ id: r.id, revision: Number(r.revision), sequence: Number(r.signOff?.sequence ?? 0),
                    authorityId: v.authorities[0]?.id ?? '', outcome: '', rationale: '' })}>{t.signOff}</Button>}</td></tr>)}</tbody></table>}
            {signing && <form onSubmit={e => { e.preventDefault(); void command.run({ recordRevision: signing.revision, expectedSequence: signing.sequence, authorityId: signing.authorityId,
                outcome: signing.outcome, rationale: signing.rationale }, (body, csrf, key, signal) => api.signOff(vessel, assignment, signing.id, body, csrf, key, signal)).then(ok => { if (ok) setSigning(null); }); }}>
                <label>{t.authority}<select required value={signing.authorityId} onChange={e => setSigning({ ...signing, authorityId: e.target.value })}>{v.authorities.map(a => <option key={a.id} value={a.id}>{a.duty}</option>)}</select></label>
                <label>{t.outcome}<select required value={signing.outcome} onChange={e => setSigning({ ...signing, outcome: e.target.value })}><option value="">—</option><option>CONFIRMED</option><option>NOT_CONFIRMED</option></select></label>
                <label>{t.rationale}<textarea required maxLength={2000} value={signing.rationale} onChange={e => setSigning({ ...signing, rationale: e.target.value })}/></label>
                <Button variant="outline" type="button" onClick={() => setSigning(null)}>{t.cancel}</Button><Button type="submit" disabled={command.busy || !signing.outcome || !signing.rationale.trim()}>{t.signOff}</Button></form>}
            <CommandStatus t={t} command={command}/>
        </>}</Loading>
    </section>;
}

export function CrewComplianceWorkspace({ view, vessel }: { view: CrewComplianceView; vessel: string }) {
    const [language, setLanguage] = useAppLanguage();
    const t = copy[language];
    const [user, setUser] = useState<Session | null>(null);
    useEffect(() => { const c = new AbortController(); readSession(c.signal).then(setUser, () => setUser(null)); return () => c.abort(); }, []);
    const title = view === 'certificates' ? t.titleCertificates : view === 'matrix' ? t.titleMatrix : t.titleManning;
    const intro = view === 'certificates' ? t.introCertificates : view === 'matrix' ? t.introMatrix : t.introManning;
    useEffect(() => { document.title = `POSEIDON · ${title}`; }, [title]);
    return <div className="product-shell enterprise-shell crew-shell">
        <EnterpriseChrome session={user} vessel={vessel} language={language} onLanguageChange={setLanguage} onNavigate={href => window.location.assign(href)} title={title} mainId="crew-compliance-main"/>
        <main id="crew-compliance-main" tabIndex={-1}>
            <nav className="crew-enterprise-breadcrumbs" aria-label={t.breadcrumbs}><a href={`/app/crew-roster/${vessel}`}>{t.roster}</a></nav>
            <EnterprisePageHeader title={title} description={<>{intro}<span className="enterprise-heading-badge"><Badge tone="warning">{t.advisory}</Badge></span></>}/>
            {view === 'certificates' && <Certificates t={t} vessel={vessel}/>}
            {view === 'matrix' && <Matrix t={t} vessel={vessel}/>}
            {view === 'safe-manning' && <SafeManning t={t} vessel={vessel}/>}
        </main>
    </div>;
}
