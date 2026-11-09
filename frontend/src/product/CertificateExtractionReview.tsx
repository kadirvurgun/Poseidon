import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { EnterpriseActions } from './EnterprisePatterns';
import { ApiError, session as readSession, type Session } from './api';
import { isGuid } from './identifiers';
import { certificatesApi, type CertificateDto } from './certificates-api';
import { certificateExtractionApi, freezeExtraction, type CertificateExtraction, type ConfirmableField, type ExtractionAttempt, type ExtractionField } from './certificate-extraction-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import './certificate-extraction.css';
export type CertificateExtractionReviewProps = {
    vessel: string;
    certificateId: string;
    expectedVersion: number;
    enabled: boolean;
    language: AppLocale;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
    onApplied: (saved: CertificateDto) => void;
};
const boundary = (s: Session | undefined) => s?.authenticated && s.canRead && isGuid(s.actorId) && isGuid(s.tenantId) ? `${s.tenantId}/${s.actorId}` : null;
const labels: Record<ExtractionField, [
    string,
    string
]> = { certificate_type: [sourceText("en", "pairs.m_02bf68d3d0c738c1"), sourceText("tr", "pairs.m_02bf68d3d0c738c1")], certificate_no: [sourceText("en", "pairs.m_c7de1be23dbdbf03"), sourceText("tr", "pairs.m_c7de1be23dbdbf03")], issuer: [sourceText("en", "pairs.m_381062193b369f22"), sourceText("tr", "pairs.m_381062193b369f22")], issue_date: [sourceText("en", "pairs.m_439665a53c0e2c7a"), sourceText("tr", "pairs.m_439665a53c0e2c7a")], expiry_date: [sourceText("en", "pairs.m_23fad7efff1af296"), sourceText("tr", "pairs.m_23fad7efff1af296")], vessel_reference: [sourceText("en", "pairs.m_eb0b0ca4973c8270"), sourceText("tr", "pairs.m_eb0b0ca4973c8270")], person_reference: [sourceText("en", "pairs.m_7a17cdbe3cd0c57e"), sourceText("tr", "pairs.m_7a17cdbe3cd0c57e")] };
type Preview = {
    owner: string;
    resource: string;
    source: CertificateExtraction;
};
export function CertificateExtractionReview({ vessel, certificateId, expectedVersion, enabled, language, onDirty, onSaving, onApplied }: CertificateExtractionReviewProps) {
    const en = language === 'en', id = useId(), epoch = useWorkspaceRevalidation(), resource = `${vessel}/${certificateId}/${expectedVersion}`;
    const mayRead = enabled && navigator.onLine && document.visibilityState !== 'hidden';
    const [activation, setActivation] = useState({ resource, mayRead, generation: 0 });
    if (activation.resource !== resource || activation.mayRead !== mayRead)
        setActivation({ resource, mayRead, generation: activation.generation + 1 });
    const [authorizationAttempt, reauthorize] = useState(0), scope = `${resource}/${activation.generation}/${epoch}/${mayRead}/${authorizationAttempt}`;
    const [auth, setAuth] = useState<{
        scope: string;
        user?: Session;
    }>({ scope: '' }), [preview, setPreview] = useState<Preview | null>(null);
    const [values, setValues] = useState<Partial<Record<ConfirmableField, string>>>({}), [selected, setSelected] = useState<ConfirmableField[]>([]), [contextConfirmed, setContextConfirmed] = useState(false);
    const [pending, setPending] = useState<ExtractionAttempt | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [validation, setValidation] = useState(false);
    const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ scope: '', owner: null as string | null });
    const user = mayRead && auth.scope === scope ? auth.user : undefined, owner = boundary(user);
    const current = owner && preview?.owner === owner && preview.resource === resource ? preview.source : null;
    const retained = !!preview || !!pending, samePending = !!pending && pending.owner === owner && pending.vessel === vessel && pending.id === certificateId;
    useLayoutEffect(() => {
        committed.current = { scope, owner };
        if (!mayRead)
            writer.current?.abort();
    }, [scope, owner, mayRead]);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort(); }; }, []);
    useEffect(() => {
        if (!mayRead)
            return;
        const c = new AbortController();
        let active = true;
        async function load() {
            const user = await readSession(c.signal);
            if (!boundary(user)) {
                if (active)
                    setAuth({ scope });
                return;
            }
            // Session identity alone does not prove current source/file visibility.
            // Use the owning current read before restoring any retained private facts.
            const record = await certificatesApi.read(vessel, certificateId, c.signal);
            const after = await readSession(c.signal);
            if (active)
                setAuth(record.currentRevision && boundary(user) === boundary(after) ? { scope, user: after } : { scope });
        }
        load().catch(() => {
            if (active)
                setAuth({ scope });
        });
        return () => { active = false; c.abort(); };
    }, [scope, mayRead, vessel, certificateId]);
    useEffect(() => { onDirty(retained); }, [retained, onDirty]);
    useEffect(() => { onSaving(busy || !!pending); }, [busy, pending, onSaving]);
    const canExtract = !!owner && !!user && mayRead && !busy && !pending && !preview;
    async function extract() {
        if (!canExtract || running.current || !owner || !user)
            return;
        running.current = true;
        setBusy(true);
        setFailure(null);
        const c = new AbortController();
        writer.current = c;
        const original = { owner, scope }, timer = setTimeout(() => c.abort(), 95000);
        try {
            const fresh = await readSession(c.signal);
            if (boundary(fresh) !== original.owner)
                throw new ApiError(403, 'identity_boundary_changed');
            const source = await certificateExtractionApi.extract(vessel, certificateId, expectedVersion, fresh.csrfToken, c.signal);
            const after = await readSession(c.signal);
            if (!mounted.current || committed.current.scope !== original.scope || committed.current.owner !== original.owner || boundary(after) !== original.owner)
                return;
            setPreview({ owner: original.owner, resource, source });
            setSelected([]);
            setContextConfirmed(false);
            setValues(Object.fromEntries(source.candidates.filter(f => f.key !== 'person_reference' && f.key !== 'vessel_reference').map(f => [f.key, f.value ?? ''])));
        }
        catch (error) {
            if (mounted.current) {
                const problem = error instanceof ApiError ? error : new ApiError(0);
                setFailure(problem);
                if ([401, 403, 404].includes(problem.status))
                    setAuth({ scope });
            }
        }
        finally {
            clearTimeout(timer);
            writer.current = null;
            running.current = false;
            if (mounted.current)
                setBusy(false);
        }
    }
    async function confirm() {
        if (running.current || busy || !mayRead || !owner || !user || !current || pending && !samePending)
            return;
        let command: ExtractionAttempt;
        try {
            command = pending ?? freezeExtraction(vessel, certificateId, owner, current, selected.map(key => ({ key, value: values[key] ?? '', confirmed: true })), contextConfirmed);
        }
        catch {
            setValidation(true);
            return;
        }
        // Freeze the exact body/key BEFORE transport. An uncertain command cannot
        // be discarded, edited, replayed as another actor or automatically retried.
        setPending(command);
        setBusy(true);
        setFailure(null);
        setValidation(false);
        running.current = true;
        const c = new AbortController();
        writer.current = c;
        const originalScope = scope, timer = setTimeout(() => c.abort(), 20000);
        let sent = false, receiptReceived = false;
        try {
            const fresh = await readSession(c.signal);
            if (boundary(fresh) !== command.owner)
                throw new ApiError(403, 'identity_boundary_changed');
            sent = true;
            const saved = await certificateExtractionApi.confirm(command, fresh.csrfToken, c.signal);
            receiptReceived = true;
            const after = await readSession(c.signal);
            if (!mounted.current || committed.current.scope !== originalScope || committed.current.owner !== command.owner || boundary(after) !== command.owner) {
                if (mounted.current)
                    setFailure(new ApiError(0, 'identity_boundary_changed'));
                return;
            }
            setPending(null);
            setPreview(null);
            setValues({});
            setSelected([]);
            setContextConfirmed(false);
            onApplied(saved);
        }
        catch (error) {
            if (!mounted.current)
                return;
            const problem = error instanceof ApiError ? error : new ApiError(0);
            setFailure(problem);
            if ([401, 403, 404].includes(problem.status))
                setAuth({ scope });
            // A received owning receipt establishes a committed outcome even when
            // the following session fence fails. Preserve its original key/body,
            // just as for any previously unknown command. Only a first definitive
            // rejection before receiving a receipt permits preview discard/review.
            if (!pending && !receiptReceived && (!sent || problem.status > 0 && problem.status < 500))
                setPending(null);
        }
        finally {
            clearTimeout(timer);
            writer.current = null;
            running.current = false;
            if (mounted.current)
                setBusy(false);
        }
    }
    const show = mayRead && !!owner && (!retained || !!current) && (!pending || samePending);
    return <section className="certificate-extraction" aria-label={localizedText(en, "pairs.m_cfa97825239771a3")} aria-busy={busy}>
    <div className="enterprise-section-header"><div><h3>{localizedText(en, "pairs.m_55a4d63f3a4a6d6a")}</h3><p>{localizedText(en, "pairs.m_92f67b3ae863312d")}</p></div><Badge tone="info">{localizedText(en, "pairs.m_8701cef409d436a7")}</Badge></div>
    {!show ? <><p role="status">{localizedText(en, "pairs.m_986675e9e04b28b9")}</p><Button type="button" variant="outline" disabled={!mayRead || busy} onClick={() => reauthorize(n => n + 1)}>{localizedText(en, "pairs.m_d67c28865e23af5e")}</Button></> : <>
      {failure && <p role="alert">{pending ? (localizedText(en, "pairs.m_6c13494cf11f12dd")) : failure.status === 409 ? (localizedText(en, "pairs.m_b76a99ceff4977a4")) : (localizedText(en, "pairs.m_fa76ab31b0359504"))}</p>}
      {!current ? <Button variant="outline" disabled={!canExtract} onClick={() => void extract()}>{busy ? (localizedText(en, "pairs.m_798f28bf80ffc800")) : (localizedText(en, "pairs.m_17219e47c3b40ecc"))}</Button> : <>
        <p>{localizedText(en, "pairs.m_bd61fb9c05c6d646")}</p>
        <form noValidate onSubmit={event => { event.preventDefault(); void confirm(); }} aria-label={localizedText(en, "pairs.m_18aff4bea8f96512")}>
          <div className="certificate-extraction-facts">{current.candidates.map(field => {
                    const writable = field.key !== 'person_reference' && field.key !== 'vessel_reference', key = field.key as ConfirmableField, label = localizedTuple(labels[field.key], en, 0);
                    return <div className={`certificate-extraction-fact${field.lowConfidence ? ' certificate-extraction-review' : ''}`} key={field.key}>
              <div><strong>{label}</strong><span>{field.confidence === null ? (localizedText(en, "pairs.m_9a214d467aaf7049")) : `${Math.round(field.confidence * 100)}%`}{field.page !== null && ` · ${localizedText(en, "pairs.m_184525c5f4abe995")} ${field.page}`}</span><Badge tone="warning">{field.lowConfidence ? (localizedText(en, "pairs.m_eef646bf042a1867")) : (localizedText(en, "pairs.m_6edb7d9a4aca3fd7"))}</Badge></div>
              {writable ? <><label htmlFor={`${id}-${field.key}`}>{localizedText(en, "pairs.m_2ba01c331dadc017")} · {label}</label><input id={`${id}-${field.key}`} value={values[key] ?? ''} maxLength={512} disabled={busy || !!pending || !current.canConfirm} onChange={event => { setValues(previous => ({ ...previous, [key]: event.target.value })); setSelected(previous => previous.filter(f => f !== key)); }}/><label><input type="checkbox" checked={selected.includes(key)} disabled={busy || !!pending || !current.canConfirm} onChange={event => setSelected(previous => event.target.checked ? [...previous, key] : previous.filter(f => f !== key))}/>{localizedText(en, "pairs.m_a3ceb6f6f95bd980")} · {label}</label></> : <p>{field.value ?? (localizedText(en, "pairs.m_c7522b6efb2897ac"))} · {localizedText(en, "pairs.m_9eeed9a0aff40f8c")}</p>}
              <details><summary>{localizedText(en, "pairs.m_f0c42fff3b4f9e7e")}</summary><ul>{field.reasons.map(reason => <li key={reason}><code>{reason}</code></li>)}</ul></details>
            </div>;
                })}</div>
          <label><input type="checkbox" checked={contextConfirmed} disabled={busy || !!pending || !current.canConfirm} onChange={event => setContextConfirmed(event.target.checked)}/>{localizedText(en, "pairs.m_9d84a9a0d7b3c19b")}</label>
          {validation && <p role="alert">{localizedText(en, "pairs.m_cd4bff0e9163b914")}</p>}
          {!current.canConfirm && <p>{localizedText(en, "pairs.m_533caf37ad49ba61")}</p>}
          <EnterpriseActions><Button type="button" variant="outline" disabled={busy || !!pending} onClick={() => { setPreview(null); setValues({}); setSelected([]); setContextConfirmed(false); setFailure(null); setValidation(false); }}>{localizedText(en, "pairs.m_c29e33a3537102d5")}</Button><Button type="submit" disabled={busy || !current.canConfirm || !!pending && !samePending}>{pending ? (localizedText(en, "pairs.m_21646f70a1e8e004")) : (localizedText(en, "pairs.m_9f8203c2bde11b22"))}</Button></EnterpriseActions>
        </form>
        <details><summary>{localizedText(en, "pairs.m_3bc25afd3fc60eff")}</summary><dl><dt>{localizedText(en, "pairs.m_548b333bda9137be")}</dt><dd>{current.modelVersion} / {current.promptVersion}</dd><dt>{localizedText(en, "pairs.m_6979312f892b4c80")}</dt><dd>{current.policyVersion} / <time dateTime={current.expiresAt}>{current.expiresAt}</time></dd><dt>{localizedText(en, "pairs.m_7bb902c2524638fe")}</dt><dd><code>{current.sourceVersion}</code></dd></dl></details>
      </>}
    </>}
  </section>;
}
