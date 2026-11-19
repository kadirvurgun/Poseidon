import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import './regulatory-enterprise.css';
import { ComplianceInputEditor } from './ComplianceInputEditor';
import * as qualification from './qualification-api';
import { TugCapabilityEditor } from './TugCapabilityEditor';
import { OperationEquipmentEditor } from './OperationEquipmentEditor';
import { useCallback, useEffect, useRef, useState } from 'react';
import * as service from './control-evidence-api';
import { useControlEvidenceRecovery } from './use-control-evidence-recovery';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import * as directory from './api';
import * as registers from './register-api';
import * as rules from './applicability-api';
import { filePage } from './file-api';
import type { FileRecord } from './file-api';
import { HumanDecisionTrace } from './HumanDecisionTrace';
import { VesselFiles } from './VesselFiles';
import { DraftConfirmation } from './DraftConfirmation';
import { ControlLifecycle, FINDINGS, ORPHAN_REASONS } from './ControlLifecycle';
import { timestamp } from './date-validation';
type Props = {
    owner?: string | null;
    language: AppLocale;
    csrf: string;
    offline: boolean;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
};
export function ControlEvidence(props: Props) {
    const language = props.language;
    const vesselEpoch = useWorkspaceRevalidation(), vesselScope = `${props.owner ?? ''}/${props.offline}/${vesselEpoch}`;
    const [vesselProof, setVesselProof] = useState<{ scope: string; owner: string } | null>(null), [vesselOwner, setVesselOwner] = useState<string | null>(null);
    const [vessels, setVessels] = useState<directory.Vessel[]>([]), [vesselOffset, setVesselOffset] = useState(0), [more, setMore] = useState(false);
    const [vessel, setVessel] = useState(''), [kind, setKind] = useState<service.Kind>('Control'), [dirty, setDirty] = useState(false), [saving, setSaving] = useState(false);
    const [pending, setPending] = useState<{
        vessel: string;
        kind: service.Kind;
    } | null>(null), [error, setError] = useState(false);
    useEffect(() => {
        if (props.offline)
            return;
        const c = new AbortController();
        let active = true;
        async function load() {
            const before = await service.loadSession(c.signal);
            if (props.owner !== undefined && props.owner !== before.owner) throw new directory.ApiError(403);
            const page = await directory.vessels(vesselOffset, '', c.signal);
            const after = await service.loadSession(c.signal);
            if (before.owner !== after.owner) throw new directory.ApiError(403);
            return { page, owner: after.owner };
        }
        load().then(({ page: p, owner }) => {
            if (active) {
                setVessels(p.items);
                setMore(p.hasMore);
                setError(false);
                setVesselProof({ scope: vesselScope, owner });
            }
        }).catch(() => {
            if (active) {
                setError(true);
                setVesselProof(null);
            }
        });
        return () => { active = false; c.abort(); };
    }, [vesselOffset, props.offline, props.owner, vesselScope]);
    const visibleVessels = vesselProof?.scope === vesselScope && !props.offline ? vessels : [];
    const change = (v: string, k: service.Kind) => {
        if (saving) return;
        if (dirty)
            setPending({ vessel: v, kind: k });
        else {
            setVessel(v);
            setVesselOwner(vesselProof?.owner ?? null);
            setKind(k);
        }
    };
    return <section className="control-evidence regulatory-workspace" aria-label={localizedText(language, "pairs.m_edd35c8e32c4c9c9")}>
    <h2>{localizedText(language, "pairs.m_edd35c8e32c4c9c9")}</h2>
    <p>{localizedText(language, "pairs.m_90295344c76121dc")}</p>
    {error && <p role="alert">{localizedText(language, "pairs.m_cee03bd7f5893ac6")}</p>}
    <EnterpriseToolbar><label>{localizedText(language, "pairs.m_ba91f7fe5286366c")}<select value={vessel} disabled={saving || vesselProof?.scope !== vesselScope} onChange={e => change(e.target.value, kind)}><option value="">{localizedText(language, "pairs.m_6e26333eb2a6b2c3")}</option>{vessel && vesselProof?.scope === vesselScope && vesselOwner === vesselProof.owner && !visibleVessels.some(v => v.id === vessel) && <option value={vessel}>{vessel}</option>}{visibleVessels.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
    <label>{localizedText(language, "pairs.m_b9f13e3611f63d16")}<select disabled={saving} value={kind} onChange={e => change(vessel, e.target.value as service.Kind)}><option value="Control">{localizedText(language, "pairs.m_72c87c6d289cd36f")}</option><option value="Evidence">{localizedText(language, "pairs.m_29983c02e9b2276d")}</option></select></label></EnterpriseToolbar>
    <EnterprisePagination label={localizedText(language, "pairs.m_ed930ce14a5fd685")} previous={localizedText(language, "pairs.m_3ebc6aea5e3f139e")} next={localizedText(language, "pairs.m_e86c29e387920d03")} previousDisabled={saving || vesselOffset === 0} nextDisabled={saving || !more} onPrevious={() => setVesselOffset(v => v - 25)} onNext={() => setVesselOffset(v => v + 25)}/>
    {pending && <DraftConfirmation language={props.language} onStay={() => setPending(null)} onDiscard={() => { if (saving) return; setVessel(pending.vessel); setVesselOwner(vesselProof?.owner ?? null); setKind(pending.kind); setPending(null); setDirty(false); props.onDirty(false); }}/>}
    {vessel && <Editor key={`${vessel}/${kind}`} {...props} vessel={vessel} kind={kind} isTug={visibleVessels.find(v => v.id === vessel)?.kind === 'Tug'} onDirty={v => { setDirty(v); props.onDirty(v); }} onSaving={v => { setSaving(v); props.onSaving(v); }}/>}
  </section>;
}
function Editor({ vessel, kind, language, csrf, offline, onDirty, onSaving, isTug, owner }: Props & {
    vessel: string;
    kind: service.Kind;
    isTug: boolean;
}) {
    const [offset, setOffset] = useState(0), [reload, setReload] = useState(0);
    const [selected, setSelected] = useState<service.Row | null>(null), [content, setContent] = useState(service.blank), [dirty, setDirty] = useState(false);
    const [requirements, setRequirements] = useState<service.RequirementPin[]>([]), [links, setLinks] = useState<service.GraphPin[]>([]), [file, setFile] = useState<service.FilePin | null>(null);
    const [owners, setOwners] = useState<{
        actorId: string;
        roleId: string;
    }[]>([]), [reqs, setReqs] = useState<registers.RegisterPage<registers.Draft> | null>(null), [reqOffset, setReqOffset] = useState(0);
    const [controls, setControls] = useState<service.Page | null>(null), [controlOffset, setControlOffset] = useState(0), [ruleRows, setRuleRows] = useState<rules.Page<rules.RuleDraft> | null>(null), [ruleOffset, setRuleOffset] = useState(0);
    const [files, setFiles] = useState<FileRecord[]>([]), [fileOffset, setFileOffset] = useState(0), [fileMore, setFileMore] = useState(false);
    const [history, setHistory] = useState<service.Page | null>(null), [historyOffset, setHistoryOffset] = useState(0), [asOf, setAsOf] = useState(''), [historyAsOf, setHistoryAsOf] = useState('');
    const [orphan, setOrphan] = useState<Awaited<ReturnType<typeof service.orphans>> | null>(null), [orphanOffset, setOrphanOffset] = useState(0);
    const [notice, setNotice] = useState('');
    const [reviewDrafts, setReviewDrafts] = useState<Record<string, boolean>>({}), [reviewSaves, setReviewSaves] = useState<Record<string, boolean>>({});
    const reviewDirty = Object.values(reviewDrafts).some(Boolean), reviewBusy = Object.values(reviewSaves).some(Boolean);
    const liveReviews = useRef<{ drafts: Record<string, boolean>; saves: Record<string, boolean> }>({ drafts: {}, saves: {} });
    const ackEpoch = useRef(0), ackRead = useRef<AbortController | null>(null), mounted = useRef(false);
    const humanBlocked = useCallback(() => Object.values(liveReviews.current.drafts).some(Boolean) || Object.values(liveReviews.current.saves).some(Boolean), []);
    function reportReview(key: string, value: boolean, saving: boolean) {
        const values = saving ? liveReviews.current.saves : liveReviews.current.drafts;
        if (values[key] === value) return;
        values[key] = value; ackEpoch.current++; ackRead.current?.abort();
        if (saving) setReviewSaves({ ...values }); else setReviewDrafts({ ...values });
    }
    const abortAcknowledgment = useCallback(() => { ackEpoch.current++; ackRead.current?.abort(); }, []);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; abortAcknowledgment(); }; }, [abortAcknowledgment]);
    const hydrate = useCallback((row: service.Row | null) => {
        setSelected(row); setContent(row?.content ?? service.blank()); setFile(row?.file ?? null);
        setRequirements(row?.requirements ?? []); setLinks(row?.links ?? []); setDirty(false);
    }, []);
    const recovery = useControlEvidenceRecovery({ vessel, kind, owner, id: selected?.id ?? null, offset, offline, blocked: humanBlocked, onSaved: hydrate });
    const { rows, busy, error, setError, conflict, attempt: frozen, read } = recovery;
    const [uploadDirty, setUploadDirty] = useState(false), [uploadBusy, setUploadBusy] = useState(false), [discard, setDiscard] = useState(false);
    const [source, setSource] = useState<registers.RegisterPage<registers.Revision> | null>(null), [pickerError, setPickerError] = useState(false);
    const [sourceId, setSourceId] = useState(''), [sourceOffset, setSourceOffset] = useState(0);
    const resourcesVisible = !offline && !!rows;
    const [previousResourcesVisible, setPreviousResourcesVisible] = useState(resourcesVisible);
    // Withdraw read projections in the render that loses the owning proof.
    // The local draft and immutable command remain owned by recovery.
    if (previousResourcesVisible !== resourcesVisible) {
        setPreviousResourcesVisible(resourcesVisible);
        if (!resourcesVisible) {
            setHistory(null); setSource(null); setOwners([]); setReqs(null); setControls(null);
            setRuleRows(null); setFiles([]); setFileMore(false); setOrphan(null);
        }
    }
    useEffect(() => { onDirty(dirty || recovery.dirty || uploadDirty || reviewDirty); }, [dirty, recovery.dirty, uploadDirty, reviewDirty, onDirty]);
    useEffect(() => { onSaving(recovery.barrier || uploadBusy || reviewBusy); }, [recovery.barrier, uploadBusy, reviewBusy, onSaving]);
    useEffect(() => {
        if (offline || !rows) return;
        let active = true;
        const c = new AbortController();
        read(signal => Promise.all([service.owners(vessel, signal), registers.listDrafts('requirements', '', reqOffset, signal), service.page(vessel, 'Control', controlOffset, signal), rules.rules(ruleOffset, signal), filePage(vessel, fileOffset, signal), service.orphans(vessel, orphanOffset, signal)]), c.signal).then(([o, r, controls, ruleRows, files, orphan]) => {
            if (!active)
                return;
            setOwners(o);
            setReqs(r);
            setControls(controls);
            setRuleRows(ruleRows);
            setFiles(files.items);
            setFileMore(files.hasMore);
            setOrphan(orphan);
            setPickerError(false);
        }).catch(() => {
            if (active) {
                setPickerError(true);
                setOwners([]);
                setReqs(null);
                setControls(null);
                setRuleRows(null);
                setFiles([]);
                setOrphan(null);
            }
        });
        return () => { active = false; c.abort(); };
    }, [vessel, rows, reqOffset, controlOffset, ruleOffset, fileOffset, orphanOffset, reload, offline, read]);
    const resetScope1 = [vessel, kind, selected, historyOffset, historyAsOf, offline, rows] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], kind) || !Object.is(previousScope1[2], selected) || !Object.is(previousScope1[3], historyOffset) || !Object.is(previousScope1[4], historyAsOf) || !Object.is(previousScope1[5], offline) || !Object.is(previousScope1[6], rows)) {
        setPreviousScope1(resetScope1);
        setHistory(null);
    }
    useEffect(() => {
        if (!selected || offline || !rows)
            return;
        let active = true;
        const c = new AbortController();
        read(signal => service.page(vessel, kind, historyOffset, signal, selected.id, historyAsOf || undefined), c.signal).then(p => {
            if (active)
                setHistory(p);
        }).catch(e => {
            if (active)
                setError(e instanceof directory.ApiError ? e.status : 0);
        });
        return () => { active = false; c.abort(); };
    }, [vessel, kind, selected, historyOffset, historyAsOf, offline, rows, read, setError]);
    const resetScope2 = [sourceId, sourceOffset, offline, rows] as const;
    const [previousScope2, setPreviousScope2] = useState(resetScope2);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope2[0], sourceId) || !Object.is(previousScope2[1], sourceOffset) || !Object.is(previousScope2[2], offline) || !Object.is(previousScope2[3], rows)) {
        setPreviousScope2(resetScope2);
        setSource(null);
    }
    useEffect(() => {
        if (!sourceId || offline || !rows)
            return;
        const c = new AbortController();
        let active = true;
        read(signal => registers.historyDraft('sources', sourceId, sourceOffset, signal), c.signal).then(p => {
            if (active)
                setSource(p);
        }).catch(() => {
            if (active)
                setPickerError(true);
        });
        return () => { active = false; c.abort(); };
    }, [sourceId, sourceOffset, offline, rows, read]);
    const locked = recovery.locked || uploadBusy || reviewDirty || reviewBusy;
    const mutate = (key: keyof service.Content, value: string | boolean | null) => { if (!recovery.edit()) return; setContent(c => ({ ...c, [key]: value })); setDirty(true); };
    function choose(row: service.Row | null) {
        if (!recovery.canShow || dirty || recovery.dirty || frozen || recovery.confirmed || uploadDirty || busy || uploadBusy || reviewDirty || reviewBusy)
            return;
        setSelected(row);
        setContent(row?.content ?? service.blank());
        setFile(row?.file ?? null);
        setRequirements(row?.requirements ?? []);
        setLinks(row?.links ?? []);
        setHistoryOffset(0);
        setSource(null);
        setSourceId('');
    }
    function save(action?: 'submit' | 'complete') {
        if (offline || busy || !recovery.canShow || !rows?.canWrite || humanBlocked())
            return;
        if (frozen) { recovery.retry(); return; }
        if (conflict || recovery.confirmed) return;
        if (!service.validContent(content)) {
            setError(400);
            return;
        }
        const command: service.Command = { expectedVersion: selected?.version ?? 0, content, requirements, links, file, reasonCode: action === 'submit' ? 'SUBMIT' : action === 'complete' ? 'COMPLETE' : selected ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING' };
        recovery.capture(command, selected, action);
    }
    const pages = (value: number, hasMore: boolean, update: (v: number) => void, label: string) => <EnterprisePagination label={label} previous={`${localizedText(language, "pairs.m_2f7d24c724255a10")} · ${label}`} next={`${localizedText(language, "pairs.m_e6f6e57e02dd12ba")} · ${label}`} previousDisabled={value === 0 || busy || recovery.dirty || !!recovery.confirmed || reviewDirty || reviewBusy} nextDisabled={!hasMore || busy || recovery.dirty || !!recovery.confirmed || reviewDirty || reviewBusy} onPrevious={() => update(value - 25)} onNext={() => update(value + 25)}/>;
    const blockerLabels: Record<string, string> = {
        SOURCE_REVIEW_REQUIRED: localizedText(language, "pairs.m_300952988ccc08fd"), QUALIFIED_CONTROL_POLICY_MISMATCH: localizedText(language, "pairs.m_62996b0644f1a887"), QUALIFIED_FRESHNESS_REQUIRED: localizedText(language, "pairs.m_f788e5af6a56115a"), SOURCE_ACKNOWLEDGMENT_REQUIRED: localizedText(language, "pairs.m_e69b9a0f3568eddd"), AUTHORITATIVE_EVALUATION_UNAVAILABLE: localizedText(language, "pairs.m_c0fa91fad5695c6a"), APPLICABILITY_REVIEW_REQUIRED: localizedText(language, "pairs.m_b79e557d29fe5098"), PROFILE_CHANGED: localizedText(language, "pairs.m_aa38074473861e19"), SOURCE_NOT_CURRENT: localizedText(language, "pairs.m_76b76a0b5fb00bb9"),
        TITLE_MISSING: localizedText(language, "pairs.m_bcfcbd55af4802ac"), OWNER_MISSING: localizedText(language, "pairs.m_21d6a0a393e8035d"), REQUIREMENT_LINK_MISSING: localizedText(language, "pairs.m_8412783a43220351"), APPLICABILITY_LINK_MISSING: localizedText(language, "pairs.m_a31491dc8589c9a7"),
        FREQUENCY_UNSPECIFIED: localizedText(language, "pairs.m_07eebc866ef7459e"), EVIDENCE_POLICY_MISSING: localizedText(language, "pairs.m_f647cf60a60dbc29"), VERIFICATION_POLICY_MISSING: localizedText(language, "pairs.m_c10298dbbcaae53b"), CRITICALITY_UNKNOWN: localizedText(language, "pairs.m_653425e4a46105fa"),
        CLEAN_FILE_MISSING: localizedText(language, "pairs.m_8b89e97b0b26d155"), PROVENANCE_MISSING: localizedText(language, "pairs.m_6f38603a2f5e488e"), OBSERVATION_TIME_MISSING: localizedText(language, "pairs.m_8e12e1984611c84c"), SOURCE_ADMISSION_UNAVAILABLE: localizedText(language, "pairs.m_b996567eb442a3fc"), VERIFICATION_NOT_PERFORMED: localizedText(language, "pairs.m_f0053856cea8d2e4"),
    };
    const blockerLabel = (code: string) => blockerLabels[code] ?? code;
    const timeLabel = (state: service.Row['timeState']) => ({ UNKNOWN: localizedText(language, "pairs.m_659bf073a81a53fb"), SUPERSEDED: localizedText(language, "pairs.m_5f273650148d18d2"), DECLARED_EXPIRED: localizedText(language, "pairs.m_49c1d92f2ba3c44a"), DECLARED_STALE: localizedText(language, "pairs.m_008377b0056af9bf"), DECLARED_NOT_YET_VALID: localizedText(language, "pairs.m_628d87f32567175f") })[state];
    return <div className="regulatory-editor">
    {offline && <p role="status">{localizedText(language, "pairs.m_5602ad4a9ac376da")}</p>}
    {(error !== null || conflict) && <p role="alert">{error === 409 || conflict ? localizedText(language, "pairs.m_1eca35ee027d5420") : [401, 403, 404].includes(error ?? -1) ? localizedText(language, "pairs.m_61c59566341f7466") : localizedText(language, "pairs.m_25d71925eacbba19")}</p>}
    {recovery.notice && <p role="status">{localizedText(language, recovery.confirmed?.row.lifecycle === 'DRAFT' ? 'pairs.m_f8d39f40bcb914e3' : 'pairs.m_79054af08f971230')}</p>}
    {recovery.canShow && notice && <p role="status">{notice}</p>}
    {frozen?.unknown && <p role="status">{localizedText(language, 'pairs.m_62930366e47b200e')}</p>}
    <EnterpriseActions>
      <Button variant="outline" disabled={busy || !recovery.available} onClick={recovery.recheck}>{localizedText(language, 'pairs.m_542619365a11df9d')}</Button>
      {frozen && !recovery.canShow && <Button disabled onClick={recovery.retry}>{localizedText(language, 'pairs.m_eb449a06e44678ab')}</Button>}
      {!frozen && recovery.canDiscard && <Button variant="outline" disabled={busy || uploadBusy || reviewDirty || reviewBusy} onClick={() => setDiscard(true)}>{localizedText(language, 'pairs.m_92ccf21431e2c4a0')}</Button>}
    </EnterpriseActions>
    {!rows && error === null && <p role="status">{localizedText(language, "pairs.m_06330283a810bdf9")}</p>}
    {!recovery.canShow && !frozen && <Button disabled>{localizedText(language, 'pairs.m_bd59c5fb1d6040b0')}</Button>}
    {recovery.canShow && <>
    {rows && <><p>{rows.canWrite ? localizedText(language, "pairs.m_508cd3efef2ef29d") : localizedText(language, "pairs.m_4ac4ea608f5308ee")}</p>{rows.items.length === 0 && <p>{localizedText(language, "pairs.m_9d2d926f131471ce")}</p>}<ul className="regulatory-register-list">{rows.items.map(r => <li key={r.id}><Button variant="outline" disabled={dirty || recovery.dirty || !!frozen || !!recovery.confirmed || uploadDirty || busy || reviewDirty || reviewBusy} onClick={() => choose(r)}>{r.content.title ?? r.id} · {r.revision}</Button> · {timeLabel(r.timeState)}</li>)}</ul>{pages(offset, rows.hasMore, setOffset, localizedText(language, "pairs.m_8b6433d45192da49"))}</>}
    <Button variant="outline" disabled={dirty || recovery.dirty || !!frozen || !!recovery.confirmed || uploadDirty || busy || reviewDirty || reviewBusy} onClick={() => choose(null)}>{localizedText(language, "pairs.m_ced646267c9def5d")}</Button>
    {kind === 'Evidence' && selected && <Button disabled={locked || dirty || recovery.dirty || uploadDirty} onClick={() => { if (!recovery.edit()) return; const old = selected; setSelected(null); setContent(service.blank()); setFile(null); setRequirements(old.requirements); setLinks([{ kind: 'Evidence', id: old.id, revision: old.revision, expectedVersion: old.version }]); setDirty(true); }}>{localizedText(language, "pairs.m_adf4f3cf079a1f1f")}</Button>}
    <form onChangeCapture={e => { if (!recovery.edit()) { e.preventDefault(); e.stopPropagation(); } }} onSubmit={e => { e.preventDefault(); save(); }}>
      <fieldset disabled={locked}><legend>{localizedText(language, "pairs.m_dfc8779d69512348")}</legend>
        {kind === 'Evidence' && <OperationEquipmentEditor vessel={vessel} language={language} offline={offline} isTug={isTug} value={content} onChange={v => { if (!recovery.edit()) return; setContent(v); setDirty(true); }}/>}
        {kind === 'Evidence' && <ComplianceInputEditor value={content} onChange={v => { if (!recovery.edit()) return; setContent(v); setDirty(true); }} offline={offline} language={language}/>}
        {kind === 'Evidence' && isTug && ![content.operation, content.serviceAuthorization, content.cargoPlan, content.winchFitment, content.classPack, content.msmd, content.crewRoleAssessment].some(Boolean) && <TugCapabilityEditor vessel={vessel} language={language} offline={offline} value={content.tugCapability ?? null} onChange={v => { if (!recovery.edit()) return; setContent(c => ({ ...c, tugCapability: v })); setDirty(true); }}/>}
        <label>{localizedText(language, "pairs.m_fa3e5e76e1977a4f")}<input value={content.title ?? ''} maxLength={2000} onChange={e => mutate('title', e.target.value || null)}/></label>
        <label>{localizedText(language, "pairs.m_f7636bbe8e724781")}<select value={content.ownerId ? `${content.ownerId}/${content.ownerRole}` : ''} onChange={e => { const [ownerId, ownerRole] = e.target.value.split('/'); setContent(c => ({ ...c, ownerId: ownerId || null, ownerRole: ownerRole || null })); setDirty(true); }}><option value="">{localizedText(language, "pairs.m_1015861ec36c5319")}</option>{content.ownerId && !owners.some(o => o.actorId === content.ownerId && o.roleId === content.ownerRole) && <option value={`${content.ownerId}/${content.ownerRole}`}>{localizedText(language, "pairs.m_b2e80edce385616b")}</option>}{owners.map(o => <option key={`${o.actorId}/${o.roleId}`} value={`${o.actorId}/${o.roleId}`}>{o.actorId} / {o.roleId}</option>)}</select></label>
        {kind === 'Control' ? <>
          <label>{localizedText(language, "pairs.m_53cb407a681f5bfd")}<select value={content.critical === null ? 'UNKNOWN' : String(content.critical)} onChange={e => mutate('critical', e.target.value === 'UNKNOWN' ? null : e.target.value === 'true')}><option value="UNKNOWN">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="true">{localizedText(language, "pairs.m_fa2da1ed99324868")}</option><option value="false">{localizedText(language, "pairs.m_d5c20ff32cde82cd")}</option></select></label>
          {(['frequency', 'evidencePolicy', 'verificationPolicy'] as const).map((k, i) => <label key={k}>{[localizedText(language, "pairs.m_35a98794e3befad9"), localizedText(language, "pairs.m_92081bca14a04ba0"), localizedText(language, "pairs.m_f893326fcc0ab605")][i]}<textarea value={content[k] ?? ''} maxLength={2000} onChange={e => mutate(k, e.target.value || null)}/></label>)}
        </> : <>
          <label>{localizedText(language, "pairs.m_e35398e4dc62018e")}<textarea value={content.provenance ?? ''} maxLength={2000} onChange={e => mutate('provenance', e.target.value || null)}/></label>
          {(['observedAt', 'validFrom', 'validUntil', 'freshUntil'] as const).map((k, i) => <label key={k}>{[localizedText(language, "pairs.m_b4d0d58c6b3fe527"), localizedText(language, "pairs.m_abe2ef89f658c7fb"), localizedText(language, "pairs.m_858a3c3d226c2697"), localizedText(language, "pairs.m_5e5aaa75ba7e588f")][i]}<input value={content[k] ?? ''} onChange={e => mutate(k, e.target.value || null)} placeholder="2026-11-02T09:00:00+03:00"/></label>)}
          <label>{localizedText(language, "pairs.m_29f278388477bf93")}<select disabled={!!selected?.file} value={file?.id ?? ''} onChange={e => { const f = files.find(f => f.id === e.target.value); setFile(f && f.revision && f.checksum ? { id: f.id, revision: f.revision, checksum: f.checksum } : null); setDirty(true); }}><option value="">{localizedText(language, "pairs.m_ecce18bf33ba73e3")}</option>{file && !files.some(f => f.id === file.id) && <option value={file.id}>{file.id} / {file.revision}</option>}{files.filter(f => f.state === 'CleanTransport').map(f => <option key={f.id} value={f.id}>{f.name} / {f.revision}</option>)}</select></label>
          {pages(fileOffset, fileMore, setFileOffset, localizedText(language, "pairs.m_8974ae7d0fd22752"))}
          <p>{localizedText(language, "pairs.m_bcf5dc4fb9e85353")}</p>
          <fieldset><legend>{localizedText(language, "pairs.m_3b4ec07c7acffc52")}</legend>{controls?.items.map(r => <label key={r.id}><input type="checkbox" checked={links.some(p => p.kind === 'Control' && p.id === r.id && p.revision === r.revision)} onChange={e => { setLinks(p => e.target.checked ? [...p, { kind: 'Control', id: r.id, revision: r.revision, expectedVersion: r.version }] : p.filter(p => !(p.kind === 'Control' && p.id === r.id && p.revision === r.revision))); setDirty(true); }}/>{r.content.title ?? r.id} / {r.revision}</label>)}{controls && pages(controlOffset, controls.hasMore, setControlOffset, localizedText(language, "pairs.m_ac367a141c427a88"))}</fieldset>
        </>}
        <fieldset><legend>{localizedText(language, "pairs.m_e38b66a2508d3fce")}</legend>{reqs?.items.map(d => {
            const r = d.current;
            if (!r.sourceId || !r.sourceRevision)
                return null;
            return <label key={r.id}><input type="checkbox" checked={requirements.some(p => p.requirementId === r.id && p.requirementRevision === r.revision)} onChange={e => { setRequirements(p => e.target.checked ? [...p, { requirementId: r.id!, requirementRevision: r.revision, sourceId: r.sourceId!, sourceRevision: r.sourceRevision!, ruleId: null, ruleRevision: null }] : p.filter(p => !(p.requirementId === r.id && p.requirementRevision === r.revision))); setDirty(true); }}/>{r.content.title.value ?? r.id} / {r.revision}</label>;
        })}{reqs && pages(reqOffset, reqs.hasMore, setReqOffset, localizedText(language, "pairs.m_91e31cf6763d0383"))}</fieldset>
        {requirements.map(p => <label key={`${p.requirementId}/${p.requirementRevision}`}>{localizedText(language, "pairs.m_486a20b3768b42a9")} · {p.requirementId}<select value={p.ruleId ? `${p.ruleId}/${p.ruleRevision}` : ''} onChange={e => { const r = ruleRows?.items.find(r => `${r.current.id}/${r.current.revision}` === e.target.value); setRequirements(ps => ps.map(x => x === p ? { ...x, ruleId: r?.current.id ?? null, ruleRevision: r?.current.revision ?? null } : x)); setDirty(true); }}><option value="">{localizedText(language, "pairs.m_ec181a110af37afd")}</option>{p.ruleId && !ruleRows?.items.some(r => r.current.id === p.ruleId && r.current.revision === p.ruleRevision) && <option value={`${p.ruleId}/${p.ruleRevision}`}>{p.ruleId}/{p.ruleRevision}</option>}{ruleRows?.items.filter(r => r.current.requirementId === p.requirementId && r.current.requirementRevision === p.requirementRevision && r.current.sourceId === p.sourceId && r.current.sourceRevision === p.sourceRevision).map(r => <option key={r.current.id} value={`${r.current.id}/${r.current.revision}`}>{r.current.id} / {r.current.revision}</option>)}</select></label>)}
        {ruleRows && pages(ruleOffset, ruleRows.hasMore, setRuleOffset, localizedText(language, "pairs.m_1846e4e75d747882"))}
      </fieldset>
      {pickerError && <p role="alert">{localizedText(language, "pairs.m_f80c9ed0adef324c")}</p>}
      <EnterpriseActions aria-label={localizedText(language, "pairs.m_b6af93a4bfb1b689")}>
       <Button type="submit" disabled={offline || busy || reviewDirty || reviewBusy || !rows?.canWrite || (!frozen && (conflict || !!recovery.confirmed || [401, 403, 404].includes(error ?? -1)))}>{frozen ? localizedText(language, "pairs.m_eb449a06e44678ab") : localizedText(language, "pairs.m_bd59c5fb1d6040b0")}</Button>
      <Button variant="outline" type="button" disabled={locked || offline || !selected} onClick={() => void save('submit')}>{localizedText(language, "pairs.m_8a068b1629e78502")}</Button>
      <Button variant="outline" type="button" disabled={locked || offline || selected?.lifecycle !== 'SUBMITTED' || dirty} onClick={() => void save('complete')}>{localizedText(language, "pairs.m_cf1b3085d3f59f3b")}</Button>
      {kind === 'Evidence' && requirements.filter(p => p.ruleId !== null).map(p => <Button key={p.ruleId} type="button" disabled={locked || offline} onClick={() => {
                if (humanBlocked()) return;
                ackRead.current?.abort();
                const c = new AbortController();
                ackRead.current = c;
                const generation = ++ackEpoch.current;
                const current = () => mounted.current && !c.signal.aborted && ackRead.current === c && ackEpoch.current === generation && !humanBlocked();
                recovery.acknowledge(signal => {
                    const abort = () => c.abort();
                    if (signal.aborted) c.abort(); else signal.addEventListener('abort', abort, { once: true });
                    return qualification.load(p.ruleId!, vessel, c.signal).finally(() => signal.removeEventListener('abort', abort));
                }, q => {
                    if (!current()) return;
                    const row = q.items[0];
                    if (row?.state === 'IN_FORCE' && row.sourceHash) {
                        setContent(previous => ({ ...previous, acknowledgments: [...(previous.acknowledgments ?? []).filter(a => a.ruleId !== p.ruleId), { ruleId: p.ruleId!, qualificationHash: row.hash, sourceHash: row.sourceHash }] }));
                        setDirty(true);
                        setNotice(`${localizedText(language, "pairs.m_c67d837f4a5b856e")}: ${row.sourceHash}`);
                    }
                    else
                        setError(409);
                }, current).finally(() => { if (ackRead.current === c) ackRead.current = null; });
            }}>{localizedText(language, "pairs.m_e771121463b43c62")} · {p.ruleId}</Button>)}
      </EnterpriseActions>
    </form>
    <ul aria-label={localizedText(language, "pairs.m_91c721ee7d562f1b")}>{requirements.map(p => <li key={`${p.requirementId}/${p.requirementRevision}`}>{p.requirementId}/{p.requirementRevision} → {p.sourceId}/{p.sourceRevision}<Button disabled={offline} onClick={() => { setSourceId(p.sourceId); setSourceOffset(0); }}>{localizedText(language, "pairs.m_8f506dbe88c3ebd3")}</Button><Button disabled={locked} onClick={() => { if (!recovery.edit()) return; setRequirements(ps => ps.filter(x => x !== p)); setDirty(true); }}>{localizedText(language, "pairs.m_96775b6b3f3e1c01")}</Button></li>)}{links.map(p => <li key={`${p.kind}/${p.id}/${p.revision}`}>{p.kind} {p.id}/{p.revision}<Button disabled={locked} onClick={() => { if (!recovery.edit()) return; setLinks(ps => ps.filter(x => x !== p)); setDirty(true); }}>{localizedText(language, "pairs.m_96775b6b3f3e1c01")}</Button></li>)}</ul>
    {source && <><ul aria-label={localizedText(language, "pairs.m_90a42aea41718d5b")}>{source.items.map(r => <li key={r.revision}>{r.id}/{r.revision} · {r.content.title.value} · {r.content.locator.value} · {r.hash} · {r.lifecycle}</li>)}</ul>{pages(sourceOffset, source.hasMore, setSourceOffset, localizedText(language, "pairs.m_d64eac926438b43c"))}</>}
    {selected && <section aria-label={localizedText(language, "pairs.m_d21b2e2163abcf4b")}><h3>{localizedText(language, "pairs.m_d21b2e2163abcf4b")}</h3><label>{localizedText(language, "pairs.m_285a2e9b5a7e00f7")}<input value={asOf} onChange={e => setAsOf(e.target.value)}/></label><Button disabled={offline || !!asOf && !timestamp(asOf)} onClick={() => { setHistoryAsOf(asOf); setHistoryOffset(0); }}>{localizedText(language, "pairs.m_abe27a0e178299f8")}</Button>{history?.items.map(r => <article key={r.revision}><h4>{r.revision} · {r.content.title}</h4><p>{r.savedAt} · {timeLabel(r.timeState)} · {r.hash}</p><p>{localizedText(language, "pairs.m_eb51fdc14888edff")}</p><p>{r.file ? `${r.file.id}/${r.file.revision} · ${r.file.checksum}` : localizedText(language, "pairs.m_f63c1fd92ab739ba")}</p><ul>{r.requirements.map(p => <li key={`${p.requirementId}/${p.requirementRevision}`}>{p.requirementId}/{p.requirementRevision} → {p.sourceId}/{p.sourceRevision} · {p.ruleId ?? '—'}/{p.ruleRevision ?? '—'}</li>)}{r.links.map(p => <li key={`${p.kind}/${p.id}/${p.revision}`}>{p.kind} {p.id}/{p.revision}</li>)}</ul><p>{localizedText(language, "pairs.m_ef764cb14360bfc4")}</p><details><summary>{localizedText(language, "pairs.m_976f026914e9fe02")}</summary><ul>{r.blockers.map(b => <li key={b}>{blockerLabel(b)}</li>)}</ul></details></article>)}{history && pages(historyOffset, history.hasMore, setHistoryOffset, localizedText(language, "pairs.m_bb5ad118ccbb2107"))}</section>}
    {selected && kind === 'Control' && recovery.canShow && <ControlLifecycle key={selected.id} vessel={vessel} control={selected} language={language} csrf={csrf} offline={offline} locked={locked || dirty || busy || !!frozen} timeLabel={timeLabel} onChanged={() => setReload(x => x + 1)}/>}
    {orphan && <p role="status">{orphan.available ? localizedText(language, "pairs.m_23e7a96b7b285e82") : localizedText(language, "pairs.m_bd6299ce40586f12")} · {orphan.reason} · {orphan.uncontrolledObligations.map(o => `${o.requirementId}/${o.requirementRevision}`).join(' · ')}</p>}
    </>}
    {discard && <DraftConfirmation language={language} onStay={() => setDiscard(false)} onDiscard={async () => {
      const result = await recovery.discard();
      if (!result.released) return;
      if (result.row || !selected) hydrate(result.row);
      else { setContent(service.blank()); setFile(null); setRequirements([]); setLinks([]); setDirty(false); }
      setDiscard(false); setNotice(''); setSource(null); setSourceId(''); setHistoryOffset(0); setReload(x => x + 1);
    }}/>}
    {selected?.kind === 'Evidence' && !dirty && !frozen && (!uploadDirty || reviewDirty || reviewBusy) && selected.links.filter(p => p.kind === 'Control').map(p => <div key={`${selected.id}/${selected.revision}/${p.id}/${p.revision}`} inert={busy}><HumanDecisionTrace owner={owner} anchor={{ vesselId: vessel, evidenceId: selected.id, evidenceRevision: selected.revision, controlId: p.id, controlRevision: p.revision }} language={language} csrf={csrf} offline={offline} onDirty={v => reportReview(`${p.id}/${p.revision}`, v, false)} onSaving={v => reportReview(`${p.id}/${p.revision}`, v, true)}/></div>)}
    {recovery.canShow && <section aria-label={localizedText(language, "pairs.m_21b567ca86adf1ba")}><h3>{localizedText(language, "pairs.m_21b567ca86adf1ba")}</h3><p>{localizedText(language, "pairs.m_7471b126af2cfc68")}</p>{orphan ? <><ul>{orphan.draftCriticalOrphans.map(id => <li key={id}>{id}</li>)}</ul><h4>{localizedText(language, FINDINGS.heading)}</h4>{orphan.criticalOrphans === null ? <p>{localizedText(language, FINDINGS.unavailable)}</p> : <ul aria-label={localizedText(language, FINDINGS.heading)}>{orphan.criticalOrphans.map(o => <li key={o.id}>{o.id} · r{o.revision} · {o.reasons.map(r => localizedText(language, ORPHAN_REASONS[r])).join(", ")}</li>)}</ul>}{pages(orphanOffset, orphan.hasMore, setOrphanOffset, localizedText(language, "pairs.m_bd73803992337989"))}</> : <p>{localizedText(language, "pairs.m_0880bff615d8df3e")}</p>}</section>}
    {kind === 'Evidence' && <div inert={reviewDirty || reviewBusy || !!frozen || busy || !recovery.canShow}><VesselFiles offline={offline} vessel={vessel} language={language} csrf={csrf} onDirty={setUploadDirty} onSaving={setUploadBusy} onCommitted={() => { setReload(x => x + 1); recovery.recheck(); }}/></div>}
  </div>;
}
