import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { RegulatoryTraceLineage } from './RegulatoryTraceLineage';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import './regulatory-enterprise.css';
import { useState } from 'react';
import * as service from './human-review-api';
import { useHumanReviewRecovery } from './use-human-review-recovery';
import { timestamp } from './date-validation';
type Props = {
    anchor: service.Anchor;
    owner?: string | null;
    language: AppLocale;
    csrf: string;
    offline: boolean;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
};
export function HumanDecisionTrace({ anchor, owner, language, offline, onDirty, onSaving }: Props) {
    const recovery = useHumanReviewRecovery({ anchor, owner, offline, onDirty, onSaving });
    const { data, error, offset, history, busy, conflict, attempt, locked, dirty, confirmed } = recovery;
    const [asOf, setAsOf] = useState('');
    const [rationale, setRationale] = useState(''), [authority, setAuthority] = useState(''), [selected, setSelected] = useState<service.Review | null>(null);
    const [decision, setDecision] = useState(false), [recordingMode, setRecordingMode] = useState(false);
    const [humanDecision, setHumanDecision] = useState(''), [decisionContext, setDecisionContext] = useState(''), [triggerSource, setTriggerSource] = useState('');
    const grant = data?.authorities.find(g => g.id === authority);
    const capability = recordingMode ? 'human.decide' : decision ? 'human.review.submit' : selected ? (selected.kind === 'Decision' ? 'human.decide' : 'human.verify') : 'human.review.submit';
    const eligible = data?.authorities.filter(g => g.capability === capability && (!selected || g.operationId === selected.operationId)) ?? [];
    const edit = (action: () => void, grant?: service.Authority) => { if (recovery.edit(grant)) action(); };
    function clear() { setRationale(''); setHumanDecision(''); setDecisionContext(''); setTriggerSource(''); setRecordingMode(false); setSelected(null); setDecision(false); setAuthority(''); }
    function choose(row: service.Review | null, requestDecision = false, record = false) {
        if (dirty || !recovery.edit()) return;
        setSelected(row); setDecision(requestDecision); setRecordingMode(record); setAuthority('');
    }
    function command(state: string, reasonCode: string) {
        if (locked || !grant || !eligible.some(g => g.id === grant.id) || !rationale.trim() || rationale !== rationale.trim() || rationale.length > 2000) return;
        if (recordingMode && (!grant.recording || grant.recording.requiredGate !== null || [humanDecision, decisionContext, triggerSource].some(v => !v.trim() || v !== v.trim() || v.length > 2000))) return;
        const kind = decision || selected?.kind === 'Decision' ? 'Decision' : 'Verification';
        recovery.capture({ kind, id: decision ? null : selected?.id ?? null, body: JSON.stringify({
            expectedVersion: decision ? 0 : selected?.version ?? 0, authorityId: grant.id, operationId: grant.operationId,
            evidenceId: anchor.evidenceId, evidenceRevision: anchor.evidenceRevision, evidenceVersion: data!.evidenceVersion,
            controlId: anchor.controlId, controlRevision: anchor.controlRevision, controlVersion: data!.controlVersion,
            evaluationId: selected?.evaluationId ?? data?.evaluationId ?? null, verificationId: decision ? selected?.id : selected?.verificationId ?? null,
            verificationRevision: decision ? selected?.revision : selected?.verificationRevision ?? null, state, reasonCode, rationale,
            expectedProfileRevision: recordingMode ? data!.profileRevision : null,
            recording: recordingMode ? { decisionType: grant.recording!.decisionType, subjectType: grant.recording!.subjectType, subjectId: grant.recording!.subjectId, humanDecision, decisionContext, triggerSource } : null,
        }) }, grant, decision || !selected ? 1 : selected.revision + 1);
    }
    const label: Record<string, string> = {
        RECORDED: localizedText(language, "pairs.m_fab76565505d109e"),
        PENDING: localizedText(language, "pairs.m_49021895686e9792"), VERIFIED: localizedText(language, "pairs.m_a22bd5d3bcf5d52b"), REJECTED: localizedText(language, "pairs.m_116c426da11868d8"), EXPIRED: localizedText(language, "pairs.m_4ddfd606ba8bd458"), SUPERSEDED: localizedText(language, "pairs.m_61ecb1880ed2d354"),
        EXPIRED_REVIEW_REQUIRED: localizedText(language, "pairs.m_cb97148516a60e86"), SUPERSEDED_REVIEW_REQUIRED: localizedText(language, "pairs.m_366b764bdd69031c"),
    };
    const why: Record<string, string> = {
        CONTROL_COMPLETION_PENDING: localizedText(language, "pairs.m_9d833848ec8948f0"), VERIFICATION_PENDING_OR_STALE: localizedText(language, "pairs.m_8c38949546647970"), DECISION_RECORDING_PENDING: localizedText(language, "pairs.m_c49757edf12fd8dc"),
        SOURCE_NOT_CURRENT: localizedText(language, "pairs.m_fa84401e6235bbce"), PROFILE_CHANGED: localizedText(language, "pairs.m_aa38074473861e19"), QUALIFICATION_CHANGED: localizedText(language, "pairs.m_88ecda05f25e2dd0"), INPUT_STALE: localizedText(language, "pairs.m_fd01398417d78802"),
        QUALIFICATION_LINEAGE_UNAVAILABLE: localizedText(language, "pairs.m_98320145c1f37033"), VESSEL_CHANGED: localizedText(language, "pairs.m_b58422146ae14c5d"), RULE_CHANGED: localizedText(language, "pairs.m_3008b7329db904f7"),
        SOURCE_REVIEW_REQUIRED: localizedText(language, "pairs.m_300952988ccc08fd"), ATTESTATION_EXPIRED: localizedText(language, "pairs.m_893ba4e397c9e5db"), RECORDED_QUALIFICATION: localizedText(language, "pairs.m_147a7e846748a650"),
        SOURCE_ADMISSION_UNAVAILABLE: localizedText(language, "pairs.m_b996567eb442a3fc"), AUTHORITATIVE_EVALUATION_UNAVAILABLE: localizedText(language, "pairs.m_ddccdcb938cf8df1"),
        EXTERNAL_SIGNATURE_UNAVAILABLE: localizedText(language, "pairs.m_bcd16dd56a2c980d"), REQUIREMENT_LINK_MISSING: localizedText(language, "pairs.m_8412783a43220351"),
        REVISION_UNAVAILABLE: localizedText(language, "pairs.m_b8bfe139a4bb5f9d"), COMPLETION_AND_EFFECTIVENESS_UNKNOWN: localizedText(language, "pairs.m_e37e5180cb924e22"), EXISTENCE_IS_NOT_VERIFICATION: localizedText(language, "pairs.m_7b0253f1911e1605"), HISTORICAL_INPUTS_ONLY: localizedText(language, "pairs.m_9df2b8494293a26b"),
    };
    const segmentKind: Record<string, string> = { Source: localizedText(language, "pairs.m_10c331bd1a515ec2"), Requirement: localizedText(language, "pairs.m_b78129607daa611b"), Rule: localizedText(language, "pairs.m_cc646e6a24f49b58"), Applicability: localizedText(language, "pairs.m_91396bb51fea9845"), Control: localizedText(language, "pairs.m_5b5e6cb1ed3d01a8"), Evidence: localizedText(language, "pairs.m_da91a98dc9a604e4"), Evaluation: localizedText(language, "pairs.m_2211837d8e727dc1") };
    const segmentState: Record<string, string> = {
        DRAFT: localizedText(language, "pairs.m_6f7b0af9992cd6bf"), ADOPTED: localizedText(language, "pairs.m_273d3f4a33b570b2"), AWAITING: localizedText(language, "pairs.m_02efaef48e3dbd7e"),
        FUTURE_EFFECTIVE: localizedText(language, "pairs.m_3c0a0a26b887c8ad"), IN_FORCE: localizedText(language, "pairs.m_a4f326ab525f2a2f"), SUPERSEDED: localizedText(language, "pairs.m_61ecb1880ed2d354"),
        SUBMITTED: localizedText(language, "pairs.m_035292beeddab984"), COMPLETED: localizedText(language, "pairs.m_dd91c35846c28c7a"),
        UNKNOWN: localizedText(language, "pairs.m_a5a7675bd7000dbd"), REVIEW_REQUIRED: localizedText(language, "pairs.m_6edb7d9a4aca3fd7"), RETAINED: localizedText(language, "pairs.m_5364a48b3fad7ed3")
    };
    return <section className="human-review regulatory-workspace" aria-label={localizedText(language, "pairs.m_da9a1c5cb435aac2")}>
    <h3>{localizedText(language, "pairs.m_da9a1c5cb435aac2")}</h3>
    <p>{localizedText(language, "pairs.m_265f8c46e1aaeed6")}</p>
    {offline && <p role="status">{localizedText(language, "pairs.m_3530ac370ce33448")}</p>}
    {(error !== null || conflict) && <p role="alert">{error === 403 ? localizedText(language, "pairs.m_d53aca8daf96ef99") : error === 404 ? localizedText(language, "pairs.m_90ce5807f288abce") : conflict ? localizedText(language, "pairs.m_ec7585f3e5ceb065") : localizedText(language, "pairs.m_6ed3cc5d1dafcbac")}</p>}
    {!data && !offline && error === null && <p role="status">{localizedText(language, "pairs.m_fbcd4bf6b590b4a8")}</p>}
    <p role="status">{recovery.notice ? localizedText(language, "pairs.m_79054af08f971230") : ""}</p>
    {attempt?.unknown && <p role="status">{localizedText(language, "pairs.m_62930366e47b200e")}</p>}
    <EnterpriseToolbar><label>{localizedText(language, "pairs.m_bca6226d33126d14")}<input value={asOf} disabled={dirty || busy || !!confirmed} onChange={e => setAsOf(e.target.value)}/></label>
    <Button variant="outline" disabled={offline || dirty || busy || !!confirmed || !!asOf && !timestamp(asOf)} onClick={() => { recovery.setHistory(asOf); clear(); }}>{localizedText(language, "pairs.m_24b4976a4e29b3a0")}</Button></EnterpriseToolbar>
    {data && <>
      <p>{localizedText(language, "pairs.m_2cdad0c15f6f5209")}: {data.asOf} · {data.disposition} · {data.historical ? localizedText(language, "pairs.m_13837b5b7ac6b357") : localizedText(language, "pairs.m_58332eba7763554a")}</p>
      <p>{localizedText(language, "pairs.m_606813080f3d6489")}: {data.evidenceContext.provenance ?? localizedText(language, "pairs.m_a5a7675bd7000dbd")}</p>
      <p>{localizedText(language, "pairs.m_916dfcdf92f4f185")}: {data.evidenceContext.observedAt ?? localizedText(language, "pairs.m_a5a7675bd7000dbd")} / {data.evidenceContext.validUntil ?? localizedText(language, "pairs.m_a5a7675bd7000dbd")} / {data.evidenceContext.freshUntil ?? localizedText(language, "pairs.m_a5a7675bd7000dbd")}</p>
      <ul>{data.blockers.map(b => <li key={b}>{why[b]}</li>)}</ul>
      <RegulatoryTraceLineage segments={data.segments} language={language} kinds={segmentKind} states={segmentState} reasons={why}/>
      {data.items.length === 0 && <p>{localizedText(language, "pairs.m_5fc2802781d200e0")}</p>}
      {data.items.map(row => <article key={`${row.id}/${row.revision}`}><h4>{row.kind === 'Verification' ? localizedText(language, "pairs.m_97b1dbbc4d46611d") : localizedText(language, "pairs.m_6063b3b138f44054")} · {row.revision} · {label[row.state]}</h4>
        <p>{label[row.currentState]} · {row.savedAt}</p><p>{localizedText(language, "pairs.m_69826b5b2d7d15db")}: {row.actorId} / {row.roleId} / {row.authorityId} / {row.operationId}</p>
        <p>{row.rationale}</p><p>{localizedText(language, "pairs.m_c0167b245fd21c88")}: <code>{row.authorityHash}</code></p><code>{row.contextHash}</code><p>{localizedText(language, "pairs.m_3d36fc578d03f5ee")}: {row.validUntil ?? localizedText(language, "pairs.m_a5a7675bd7000dbd")}</p>
        {row.recording && <><p>{row.recording.content.humanDecision}</p><p>{row.recording.content.decisionContext} · {row.recording.content.triggerSource}</p><p>{row.recording.content.decisionType} · {row.recording.content.subjectType}/{row.recording.content.subjectId}</p><p>{localizedText(language, "pairs.m_624bd887180720a0")}</p></>}
        <EnterpriseActions align="start" aria-label={localizedText(language, "pairs.m_dc8380e9ea3f439e")}><Button variant="outline" asChild><a href={`/api/v1/${service.tracePath(anchor, 0, row.savedAt)}`} target="_blank" rel="noreferrer">{localizedText(language, "pairs.m_5e81b015a40b08b2")}</a></Button>
        <Button variant="outline" disabled={locked || dirty || row.state === 'RECORDED'} onClick={() => choose(row)}>{localizedText(language, "pairs.m_128381380bb9f057")}</Button>
        {row.kind === 'Verification' && <Button variant="outline" disabled={locked || dirty} onClick={() => choose(row, true)}>{localizedText(language, "pairs.m_c9a7810a7a3ede35")}</Button>}
        {row.kind === 'Verification' && <Button variant="outline" disabled={locked || dirty} onClick={() => choose(row, true, true)}>{localizedText(language, "pairs.m_543ca985b5af9c69")}</Button>}
        {row.kind === 'Decision' && row.state === 'PENDING' && <Button variant="outline" disabled={locked || dirty} onClick={() => choose(row, false, true)}>{localizedText(language, "pairs.m_543ca985b5af9c69")}</Button>}</EnterpriseActions>
      </article>)}
      {data.auditItems && <section aria-label={localizedText(language, "pairs.m_76dcfd615021b35d")}><h4>{localizedText(language, "pairs.m_76dcfd615021b35d")}</h4>{data.auditItems.map(e => <article key={`${e.id}/${e.revision}`}><p>{e.kind} · {e.state} · {e.currentState} · {e.savedAt} · {e.validUntil ?? '—'}</p><p>{e.id} / {e.revision} · {e.contextHash} · {e.authorityHash}</p></article>)}<Button disabled={dirty || busy || !data.auditHasMore} onClick={() => recovery.setOffset(offset + 25)}>{localizedText(language, "pairs.m_7deacf622d320b82")}</Button></section>}
      <EnterprisePagination label={localizedText(language, "pairs.m_4843022a3c413180")} previous={localizedText(language, "pairs.m_49b8865f52ef22ad")} next={localizedText(language, "pairs.m_d26023eac9179680")} previousDisabled={dirty || busy || offset === 0} nextDisabled={dirty || busy || !data.hasMore} onPrevious={() => recovery.setOffset(offset - 25)} onNext={() => recovery.setOffset(offset + 25)}/>
      {!history && <fieldset className="regulatory-review-command" disabled={locked || !!attempt}><legend>{localizedText(language, "pairs.m_1e7c4a57705609c7")}</legend>
        <Button variant="outline" disabled={dirty} onClick={() => choose(null)}>{localizedText(language, "pairs.m_0e211a8500728242")}</Button>
        <label>{localizedText(language, "pairs.m_5596762e073edde5")}<select value={authority} onChange={e => edit(() => setAuthority(e.target.value), eligible.find(grant => grant.id === e.target.value))}><option value="">{localizedText(language, "pairs.m_dd15208b3124fddb")}</option>{eligible.map(g => <option key={g.id} value={g.id}>{g.roleId} / {g.operationId} / {g.capability}{g.recording ? ` / ${g.recording.decisionType} / ${g.recording.subjectType}` : ''}</option>)}</select></label>
        {eligible.length === 0 && <p>{localizedText(language, "pairs.m_041dee8666c873d5")}</p>}
        <label>{localizedText(language, "pairs.m_590d7ac05bb45a0d")}<textarea maxLength={2000} value={rationale} onChange={e => edit(() => setRationale(e.target.value))}/></label>
        {recordingMode ? <>
          <p>{grant?.recording ? `${grant.recording.decisionType} / ${grant.recording.subjectType} / ${grant.recording.subjectId}` : localizedText(language, "pairs.m_b95e9b76de104295")}</p>
          {grant?.recording?.requiredGate && <p role="alert">{localizedText(language, "pairs.m_610104b0f6f25659")}: {grant.recording.requiredGate} · {grant.recording.gateSourceLocator}</p>}
          <label>{localizedText(language, "pairs.m_86d30e4e42c59c70")}<textarea maxLength={2000} value={humanDecision} onChange={e => edit(() => setHumanDecision(e.target.value))}/></label>
          <label>{localizedText(language, "pairs.m_1b666081a652cee7")}<textarea maxLength={2000} value={decisionContext} onChange={e => edit(() => setDecisionContext(e.target.value))}/></label>
          <label>{localizedText(language, "pairs.m_1e0c8d69d1880e2d")}<input maxLength={2000} value={triggerSource} onChange={e => edit(() => setTriggerSource(e.target.value))}/></label>
          <p>{localizedText(language, "pairs.m_64996018d6e55394")}</p>
          <EnterpriseActions aria-label={localizedText(language, "pairs.m_bea9b1888352e6f8")}><Button disabled={!grant?.recording || grant.recording.requiredGate !== null || !rationale.trim() || !humanDecision.trim() || !decisionContext.trim() || !triggerSource.trim()} onClick={() => void command('RECORDED', 'HUMAN_DECIDED')}>{localizedText(language, "pairs.m_75209c48b66c7117")}</Button></EnterpriseActions>
        </> : <EnterpriseActions aria-label={localizedText(language, "pairs.m_1c0ca65b618ce6a8")}>{(!selected || decision) ? <Button disabled={!grant || !rationale.trim()} onClick={() => void command('PENDING', 'REQUEST_REVIEW')}>{localizedText(language, "pairs.m_dea785542f056c41")}</Button> : <>
          {selected.kind === 'Verification' && <><Button disabled={!grant || !rationale.trim()} onClick={() => void command('VERIFIED', 'HUMAN_VERIFIED')}>{localizedText(language, "pairs.m_160f83c7b93e2bbf")}</Button><Button variant="outline" disabled={!grant || !rationale.trim()} onClick={() => void command('EXPIRED', 'VALIDITY_ELAPSED')}>{localizedText(language, "pairs.m_4a2057e620df9a4b")}</Button><Button variant="outline" disabled={!grant || !rationale.trim()} onClick={() => void command('SUPERSEDED', 'INPUT_SUPERSEDED')}>{localizedText(language, "pairs.m_77267d6950c12cd5")}</Button></>}
          <Button variant="destructive" disabled={!grant || !rationale.trim()} onClick={() => void command('REJECTED', 'HUMAN_REJECTED')}>{localizedText(language, "pairs.m_6d9cc1f0b64c0136")}</Button>
        </>}</EnterpriseActions>}
      </fieldset>}
    </>}
    <EnterpriseActions align="start" aria-label={localizedText(language, "pairs.m_7fb7bfcf00f28921")}>
      {attempt && <Button disabled={!recovery.canRetry} onClick={recovery.retry}>{localizedText(language, "pairs.m_3176d9a6fa209af6")}</Button>}
      <Button variant="outline" disabled={busy || offline} onClick={recovery.recheck}>{localizedText(language, 'pairs.m_542619365a11df9d')}</Button>
      {(dirty || confirmed) && recovery.canDiscard && <Button variant="outline" disabled={busy || !recovery.available} onClick={async () => { if (await recovery.discard()) clear(); }}>{localizedText(language, "pairs.m_d3fd36629a8388d7")}</Button>}
    </EnterpriseActions>
  </section>;
}
