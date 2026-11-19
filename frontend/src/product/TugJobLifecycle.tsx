import { useEffect, useRef, useState } from 'react';
import { Send, XCircle } from 'lucide-react';
import { localizedCopy } from '../i18n/messages';
import { type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseActions } from './EnterprisePatterns';
import { ApiError, session } from './api';
import { TugLifecycleError, tugApi, type TugJobState, type TugLifecycleAttempt, type TugRequest } from './tug-api';
import { tugLabel } from './tug-labels';

const copy = localizedCopy('TugJobLifecycle_tsx_copy');
type Copy = (typeof copy)[AppLocale];
export function tugJobStateLabel(state: TugJobState, language: AppLocale) { return copy[language][state]; }
// Board/next-action codes introduced by DEV-081/082; all other codes keep the existing Tug labels.
export function tugLifecycleCodeLabel(code: string, language: AppLocale) {
    const t: Copy = copy[language];
    return code === 'SubmitRequest' ? t.SubmitRequest : code === 'NoFurtherActionCancelled' ? t.NoFurtherActionCancelled : tugLabel(code, language);
}
export function TugJobStateBadge({ state, language }: { state: TugJobState; language: AppLocale }) {
    return <Badge tone={state === 'Submitted' ? 'info' : state === 'Cancelled' ? 'warning' : 'neutral'} data-job-state={state}>{tugJobStateLabel(state, language)}</Badge>;
}

type Props = {
    record: TugRequest;
    vessel: string;
    canWrite: boolean;
    offline: boolean;
    language: AppLocale;
    /** `${tenantId}/${actorId}` of the editor that loaded this record. */
    owner: string | null;
    onChanged: (record: TugRequest) => void;
    onReload: () => void;
    onPending?: (pending: boolean) => void;
};

// DEV-081 / T-TUG-002 submit/cancel (60.02 §5). The exact command and key are retained after an
// uncertain outcome; a refused submission shows the server's missing keys and changes nothing.
export function TugJobLifecycle({ record, vessel, canWrite, offline, language, owner, onChanged, onReload, onPending }: Props) {
    const t = copy[language];
    const [attempt, setAttempt] = useState<TugLifecycleAttempt | null>(null), [busy, setBusy] = useState(false);
    const [notice, setNotice] = useState<'' | 'submitted' | 'cancelled' | 'uncertain' | 'conflict' | 'denied' | 'incomplete' | 'failed'>('');
    const [missing, setMissing] = useState<string[]>([]), [confirming, setConfirming] = useState(false), [reason, setReason] = useState('');
    const lock = useRef(false), mounted = useRef(true), writer = useRef<AbortController | null>(null);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort(); }; }, []);
    useEffect(() => { onPending?.(!!attempt || busy); }, [attempt, busy, onPending]);
    useEffect(() => () => onPending?.(false), [onPending]);
    const run = async (next: TugLifecycleAttempt) => {
        if (lock.current || !owner || offline || !navigator.onLine) return;
        lock.current = true; setBusy(true); setAttempt(next); setNotice(''); setMissing([]);
        const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000); writer.current = c;
        let sent = false;
        try {
            const s = await session(c.signal);
            if (!s.authenticated || !s.canRead || `${s.tenantId}/${s.actorId}` !== owner) throw new ApiError(403, 'identity_boundary_changed');
            const context = await tugApi.context(vessel, c.signal, s.tenantId ?? undefined);
            if (!context.canWrite) throw new ApiError(403, 'write_scope_changed');
            sent = true;
            const result = await tugApi.lifecycle(vessel, next, s.csrfToken, c.signal);
            if (!mounted.current) return;
            setAttempt(null); setConfirming(false); setReason('');
            setNotice(next.operation === 'submit' ? 'submitted' : 'cancelled');
            onChanged(result);
        } catch (e) {
            if (!mounted.current) return;
            const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status);
            if (!sent || definite) setAttempt(null);
            if (sent && !definite) setNotice('uncertain');
            else if (e instanceof TugLifecycleError && e.status === 422) { setMissing(e.missing); setNotice('incomplete'); }
            else if (e instanceof ApiError && e.status === 409) setNotice('conflict');
            else if (e instanceof ApiError && [401, 403, 404].includes(e.status)) setNotice('denied');
            else setNotice('failed');
        } finally {
            clearTimeout(timeout); if (writer.current === c) writer.current = null;
            lock.current = false; if (mounted.current) setBusy(false);
        }
    };
    const command = (operation: 'submit' | 'cancel'): TugLifecycleAttempt => ({ key: crypto.randomUUID(), operation, jobId: record.id,
        body: operation === 'submit' ? { expectedVersion: record.version } : { expectedVersion: record.version, reason: reason.trim() } });
    const draft = record.state === 'Draft', active = record.state !== 'Cancelled';
    const disabled = busy || offline || !owner;
    const message = notice === 'submitted' ? t.submitted : notice === 'cancelled' ? t.cancelled : notice === 'uncertain' ? t.uncertain : notice === 'conflict' ? t.conflict
        : notice === 'denied' ? t.denied : notice === 'incomplete' ? t.incomplete : notice === 'failed' ? t.failed : '';
    return <section className="tug-job-lifecycle" aria-label={t.lifecycle} aria-busy={busy}>
      <h2>{t.lifecycle}</h2>
      <p>{t.state}: <TugJobStateBadge state={record.state} language={language}/></p>
      {!draft && <p>{t.readOnly}</p>}
      {draft && record.missing.length > 0 && <div role="status"><p>{t.incomplete}</p><ul aria-label={t.missing}>{record.missing.map(key => <li key={key}>{tugLabel(key, language)}</li>)}</ul></div>}
      {message && <div role={notice === 'submitted' || notice === 'cancelled' ? 'status' : 'alert'}><p>{message}</p>
        {notice === 'incomplete' && missing.length > 0 && <ul aria-label={t.missing}>{missing.map(key => <li key={key}>{tugLabel(key, language)}</li>)}</ul>}
        {notice === 'conflict' && <Button type="button" variant="outline" onClick={onReload}>{t.reload}</Button>}</div>}
      {offline && canWrite && active && <p role="status">{t.offline}</p>}
      {canWrite && active && <>
        {draft && <p>{t.submitHint}</p>}
        {attempt && notice === 'uncertain' ? <EnterpriseActions layout="form" aria-label={t.lifecycle}>
            <Button type="button" disabled={disabled} onClick={() => void run(attempt)}>{t.retry}</Button>
          </EnterpriseActions>
          : confirming ? <form className="tug-job-cancel" onSubmit={event => { event.preventDefault(); if (reason.trim()) void run(command('cancel')); }}>
              <label htmlFor={`tug-cancel-reason-${record.id}`}>{t.cancelReason}</label>
              <textarea id={`tug-cancel-reason-${record.id}`} required maxLength={500} value={reason} disabled={busy} aria-describedby={`tug-cancel-hint-${record.id}`} onChange={event => setReason(event.target.value)}/>
              <p id={`tug-cancel-hint-${record.id}`}>{t.cancelHint}</p>
              <EnterpriseActions layout="form" aria-label={t.cancel}>
                <Button type="button" variant="outline" disabled={busy} onClick={() => { setConfirming(false); setReason(''); }}>{t.keep}</Button>
                <Button type="submit" variant="destructive" disabled={disabled || !reason.trim()}><XCircle aria-hidden="true"/>{t.cancelConfirm}</Button>
              </EnterpriseActions>
            </form>
          : <EnterpriseActions layout="form" aria-label={t.lifecycle}>
              <Button type="button" variant="outline" disabled={disabled} onClick={() => setConfirming(true)}><XCircle aria-hidden="true"/>{t.cancel}</Button>
              {draft && <Button type="button" disabled={disabled} onClick={() => void run(command('submit'))}><Send aria-hidden="true"/>{busy ? t.submitting : t.submit}</Button>}
            </EnterpriseActions>}
      </>}
    </section>;
}
