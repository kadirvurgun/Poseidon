import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid, EnterprisePagination } from './EnterprisePatterns';
import { ApiError, session, type Session } from './api';
import { isGuid } from './identifiers';
import { certificatesApi, type CertificateDto } from './certificates-api';
import { certificateAssertionApi, freezeCertificateAssertion, type CertificateAssertionAttempt } from './certificate-assertion-api';
import type { CertificateAssertionCapabilities, CertificateAssertionPage, CertificateAssertionState, CertificateVerification } from './certificate-assertion-contract';
import { CertificateAssertionSummary } from './CertificateAssertionSummary';
import { assertionStateLabel } from './certificate-copy';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
type Props = {
    vessel: string;
    record: CertificateDto;
    enabled: boolean;
    language: AppLocale;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
    onRecorded: (value: CertificateVerification) => void;
};
type Draft = {
    owner: string;
    version: number;
    revision: number;
    authorityId: string;
    state: CertificateAssertionState | '';
    rationale: string;
};
const identity = (user: Session | undefined) => user?.authenticated && user.canRead && isGuid(user.actorId) && isGuid(user.tenantId) ? user.tenantId + '/' + user.actorId : null;
export function CertificateAssertionPanel({ vessel, record, enabled, language, onDirty, onSaving, onRecorded }: Props) {
    const en = language === 'en', id = useId(), epoch = useWorkspaceRevalidation(), resource = vessel + '/' + record.id + '/' + record.version;
    const available = enabled && navigator.onLine && document.visibilityState !== 'hidden';
    const [activation, setActivation] = useState({ resource, available, generation: 0 });
    if (activation.resource !== resource || activation.available !== available)
        setActivation({ resource, available, generation: activation.generation + 1 });
    const [offset, setOffset] = useState(0), [attempt, retry] = useState(0), scope = resource + '/' + activation.generation + '/' + epoch + '/' + attempt + '/' + offset + '/' + available;
    const [source, setSource] = useState<{
        scope: string;
        user?: Session;
        record?: CertificateDto;
        caps?: CertificateAssertionCapabilities;
        page?: CertificateAssertionPage;
        failed?: boolean;
    } | null>(null);
    const [draft, setDraft] = useState<Draft | null>(null), [pending, setPending] = useState<CertificateAssertionAttempt | null>(null), [busy, setBusy] = useState(false), [validation, setValidation] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [saved, setSaved] = useState<{
        owner: string;
        value: CertificateVerification;
    } | null>(null);
    const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ owner: null as string | null, scope: '' });
    const current = available && source?.scope === scope ? source : null, owner = identity(current?.user), caps = current?.caps;
    const sameOwner = !draft || draft.owner === owner, samePending = !pending || pending.owner === owner;
    const samePin = !!current?.record && current.record.version === record.version && current.record.revision === record.revision && (!draft || draft.version === record.version && draft.revision === record.revision);
    const retained = !!draft || !!pending;
    const allowed = caps?.authorities.find(authority => authority.id === draft?.authorityId);
    const mayRecord = !!owner && !!caps?.canRecord && sameOwner && samePin && samePending && !busy && !pending && failure?.status !== 409;
    useLayoutEffect(() => { committed.current = { owner, scope }; if (!available)
        writer.current?.abort(); }, [owner, scope, available]);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort(); }; }, []);
    useEffect(() => { onDirty(retained); }, [retained, onDirty]);
    useEffect(() => { onSaving(busy || !!pending); }, [busy, pending, onSaving]);
    useEffect(() => {
        if (!available)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        async function load() {
            const user = await session(c.signal), originalOwner = identity(user);
            if (!originalOwner)
                throw new ApiError(403);
            const actual = await certificatesApi.read(vessel, record.id, c.signal);
            const [caps, page] = await Promise.all([certificateAssertionApi.capabilities(vessel, record.id, c.signal), certificateAssertionApi.history(vessel, record.id, offset, c.signal)]);
            if (identity(await session(c.signal)) !== originalOwner || actual.scope !== 'Vessel' || caps.certificateRevision !== actual.revision || caps.certificateVersion !== actual.version)
                throw new ApiError(403, 'identity_changed');
            if (active)
                setSource({ scope, user, record: actual, caps, page });
        }
        load().catch(() => { if (active)
            setSource({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; clearTimeout(timer); c.abort(); };
    }, [available, scope, vessel, record.id, offset]);
    const edit = (patch: Partial<Draft>) => {
        if (!mayRecord || !owner)
            return;
        setDraft(previous => ({ owner, version: record.version, revision: record.revision, authorityId: '', state: '', rationale: '', ...previous, ...patch }));
        onDirty(true);
        setValidation(false);
        setSaved(null);
    };
    async function save() {
        if (!current?.user || !owner || !caps || !sameOwner || !samePending || running.current || busy || !available)
            return;
        let command: CertificateAssertionAttempt;
        try {
            if (pending)
                command = pending;
            else {
                if (!draft || !draft.state || !mayRecord)
                    throw new ApiError(400);
                command = freezeCertificateAssertion(record, caps, owner, draft.authorityId, draft.state, draft.rationale);
            }
        }
        catch {
            setValidation(true);
            return;
        }
        const wasUnknown = !!pending, originalScope = scope;
        setPending(command);
        setBusy(true);
        onDirty(true);
        onSaving(true);
        setValidation(false);
        setFailure(null);
        running.current = true;
        const c = new AbortController();
        writer.current = c;
        const timer = setTimeout(() => c.abort(), 20000);
        let sent = false, receiptReceived = false;
        try {
            const fresh = await session(c.signal);
            if (identity(fresh) !== command.owner)
                throw new ApiError(403, 'identity_changed');
            const actual = await certificatesApi.read(vessel, record.id, c.signal), authority = await certificateAssertionApi.capabilities(vessel, record.id, c.signal);
            if (identity(await session(c.signal)) !== command.owner || !authority.canRecord || !authority.authorities.some(option => option.id === command.authorityId && option.roleId === command.roleId && option.states.includes(command.state)))
                throw new ApiError(403, 'authority_unavailable');
            // An old unknown command may recover its immutable receipt after a newer
            // revision exists. Do not replace its body/base with the current version.
            if (!wasUnknown && (actual.version !== command.certificateVersion || actual.revision !== command.certificateRevision || authority.certificateVersion !== command.certificateVersion || authority.certificateRevision !== command.certificateRevision))
                throw new ApiError(409, 'source_revision_changed');
            sent = true;
            const receipt = await certificateAssertionApi.record(command, fresh.csrfToken, c.signal);
            receiptReceived = true;
            const after = await session(c.signal);
            if (!mounted.current || committed.current.scope !== originalScope || committed.current.owner !== command.owner || identity(after) !== command.owner) {
                if (mounted.current)
                    setFailure(new ApiError(0, 'outcome_unknown'));
                return;
            }
            setSaved({ owner: command.owner, value: receipt });
            setPending(null);
            setDraft(null);
            onRecorded(receipt);
            retry(n => n + 1);
        }
        catch (error) {
            if (!mounted.current)
                return;
            const problem = error instanceof ApiError ? error : new ApiError(0);
            setFailure(problem);
            if (!wasUnknown && (!sent || !receiptReceived && problem.status > 0 && problem.status < 500))
                setPending(null);
            if ([401, 403, 404].includes(problem.status))
                setSource({ scope, failed: true });
        }
        finally {
            clearTimeout(timer);
            writer.current = null;
            running.current = false;
            if (mounted.current)
                setBusy(false);
        }
    }
    const pendingAuthority = !pending || !!caps?.canRecord && caps.authorities.some(authority => authority.id === pending.authorityId && authority.roleId === pending.roleId);
    const show = available && !!owner && sameOwner && samePending && pendingAuthority;
    return <section className="certificate-assertion-panel certificate-metadata-form" aria-label={localizedText(en, "pairs.m_15449d2dacb6f878")} aria-busy={busy}>
    <h2>{localizedText(en, "pairs.m_a1c1074f4efc25a4")}</h2>
    <p>{localizedText(en, "pairs.m_3de689da598185e7")}</p>
    {current?.failed && <p role="alert">{localizedText(en, "pairs.m_159cd18edf09f095")}</p>}
    {!show ? <><p role="status">{localizedText(en, "pairs.m_54b3dd15d3148d94")}</p><Button variant="outline" disabled={!available || busy} onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_42245bd48b823cfa")}</Button></> : <>
      {saved && saved.owner === owner && <CertificateAssertionSummary value={saved.value} language={language}/>}
      {failure && <p role="alert">{pending ? (localizedText(en, "pairs.m_27ea1b9e92a5e72f")) : failure.status === 409 ? (localizedText(en, "pairs.m_dae99283735e5d14")) : (localizedText(en, "pairs.m_af09a82569c58fda"))}</p>}
      {!samePin && <p role="alert">{localizedText(en, "pairs.m_00c8d2fbf478b69e")}</p>}
      {(failure?.status === 409 || !samePin) && <Button variant="outline" disabled={busy || !!pending} onClick={() => { setFailure(null); retry(n => n + 1); }}>{localizedText(en, "pairs.m_e7646bd46fc2d833")}</Button>}
      {!caps?.canRecord && <p>{localizedText(en, "pairs.m_6115778ce02c6bc3")}</p>}
      <form noValidate aria-label={localizedText(en, "pairs.m_59fc932362759b0e")} onSubmit={event => { event.preventDefault(); void save(); }}>
        <EnterpriseFormGrid disabled={!mayRecord}><legend>{localizedText(en, "pairs.m_edf01e94aec371f2")}</legend>
          <EnterpriseField htmlFor={id + '-authority'} label={localizedText(en, "pairs.m_46ef5b85b0d74792")}><select id={id + '-authority'} aria-required="true" aria-invalid={validation && !draft?.authorityId} aria-describedby={validation ? id + '-errors' : undefined} value={draft?.authorityId ?? ''} onChange={event => edit({ authorityId: event.target.value, state: '' })}><option value="">{localizedText(en, "pairs.m_5e5183b20ee82038")}</option>{caps?.authorities.map((authority, index) => <option key={authority.id} value={authority.id}>{localizedText(en, "pairs.m_b5cdede00947cbdf")} {index + 1} · {authority.roleId.slice(-8)}</option>)}</select></EnterpriseField>
          <EnterpriseField htmlFor={id + '-state'} label={localizedText(en, "pairs.m_beb5d55fe5db19b4")}><select id={id + '-state'} aria-required="true" aria-invalid={validation && !draft?.state} aria-describedby={validation ? id + '-errors' : undefined} value={draft?.state ?? ''} onChange={event => { const state = allowed?.states.find(state => state === event.target.value); edit({ state: state ?? '' }); }}><option value="">{localizedText(en, "pairs.m_d584684d80840c3b")}</option>{allowed?.states.map(state => <option key={state} value={state}>{assertionStateLabel(state, en)}</option>)}</select></EnterpriseField>
          <EnterpriseField htmlFor={id + '-reason'} label={localizedText(en, "pairs.m_bd6d806b3b63db83")}><textarea id={id + '-reason'} value={draft?.rationale ?? ''} maxLength={2000} rows={4} aria-required="true" aria-invalid={validation} aria-describedby={validation ? id + '-errors' : undefined} onChange={event => edit({ rationale: event.target.value })}/></EnterpriseField>
        </EnterpriseFormGrid>
        {validation && <p role="alert" id={id + '-errors'}>{localizedText(en, "pairs.m_daf61f1ae2a7c169")}</p>}
        <EnterpriseActions aria-label={localizedText(en, "pairs.m_1dd4b59b38469926")}><Button variant="outline" disabled={busy || !!pending || !draft} onClick={() => { setDraft(null); setValidation(false); setFailure(null); onDirty(false); retry(n => n + 1); }}>{localizedText(en, "pairs.m_8f20e790467db10e")}</Button><Button type="submit" disabled={busy || !owner || !caps?.canRecord || !samePending || !pending && !mayRecord}>{pending ? (localizedText(en, "pairs.m_ad397a1907a280a4")) : (localizedText(en, "pairs.m_effd4eb0076d537f"))}</Button></EnterpriseActions>
      </form>
      <section aria-label={localizedText(en, "pairs.m_d04de9c6f5577ad8")}><h3>{localizedText(en, "pairs.m_59f537bf32230298")}</h3>{!current?.page?.items.length && <p>{localizedText(en, "pairs.m_7246c9826bbe30d9")}</p>}{current?.page?.items.map(value => <details key={value.id}><summary>{localizedText(en, "pairs.m_535a68fb1925e166")} {value.revision} · {value.savedAt}</summary><CertificateAssertionSummary value={value} language={language}/></details>)}<EnterprisePagination label={localizedText(en, "pairs.m_bc8a7f634882d839")} previous={localizedText(en, "pairs.m_f084ff31f80ea85b")} next={localizedText(en, "pairs.m_40f5165d595b3230")} previousDisabled={!offset || retained || busy} nextDisabled={!current?.page?.hasMore || retained || busy} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/></section>
    </>}
  </section>;
}
