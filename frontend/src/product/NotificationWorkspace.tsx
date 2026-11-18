import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useCallback, useEffect, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import * as api from './api';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import { DraftConfirmation } from './DraftConfirmation';
import { isGuid } from './identifiers';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import * as notifications from './notification-inbox-api';
import './offline-enterprise.css';
import './product.css';
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes';
export function NotificationWorkspace() {
    const [language, setLanguage] = useAppLanguage(), en = language === 'en', epoch = useWorkspaceRevalidation();
    const [selected, setSelected] = useState(() => {
        const query = new URLSearchParams(location.search), ids = query.getAll('vessel');
        return ids.length > 1 || [...query.keys()].some(k => k !== 'vessel') || ids.length === 1 && !isGuid(ids[0]) ? null : ids[0] ?? '';
    });
    const [identity, setIdentity] = useState<{
        epoch: number;
        user?: api.Session;
        failed?: boolean;
    } | null>(null);
    const [retainedUser, setRetainedUser] = useState<api.Session | null>(null);
    const [vessels, setVessels] = useState<{
        epoch: number;
        page?: api.Page<api.Vessel>;
        failed?: boolean;
    } | null>(null);
    const dirty = useRef(false);
    const setDirty = useCallback((value: boolean) => { dirty.current = value; }, []);
    const [saving, setSaving] = useState(false), [pending, setPending] = useState<(() => void) | null>(null);
    useUnsavedChangesSource(() => unsavedState(dirty.current, saving));
    const invalid = selected === null;
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', lastUser = retainedUser;
    const currentUser = identity?.epoch === epoch ? identity.user : null;
    const authorized = selected !== null && online && visible && !!currentUser?.authenticated && currentUser.canRead && isGuid(currentUser.actorId) && isGuid(currentUser.tenantId) && currentUser.actorId === lastUser?.actorId && currentUser.tenantId === lastUser?.tenantId;
    const guard = (action: () => void) => { if (saving)
        return; if (dirty.current)
        setPending(() => action);
    else
        action(); };
    useEffect(() => {
        const warn = (event: BeforeUnloadEvent) => { if (dirty.current || saving)
            event.preventDefault(); };
        window.addEventListener('beforeunload', warn);
        return () => window.removeEventListener('beforeunload', warn);
    }, [saving]);
    useEffect(() => {
        if (invalid || !online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        api.session(c.signal).then(async (user) => {
            if (!active)
                return;
            setIdentity({ epoch, user });
            if (user.authenticated && user.canRead) {
                setRetainedUser(user);
                try {
                    const page = await api.vessels(0, '', c.signal);
                    if (active)
                        setVessels({ epoch, page });
                }
                catch {
                    if (active)
                        setVessels({ epoch, failed: true });
                }
            }
        }).catch(() => { if (active)
            setIdentity({ epoch, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; clearTimeout(timer); c.abort(); };
    }, [epoch, online, visible, invalid]);
    const title = localizedText(en, "pairs.m_11fcfe7e5ea66ca5");
    useEffect(() => { document.title = `POSEIDON · ${title}`; }, [title, language]);
    return <div className="product-shell enterprise-shell"><EnterpriseChrome session={authorized ? lastUser : null} vessel={selected || null} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => guard(() => { committed?.(); location.assign(href); })} title={title} mainId="notification-main"/>
    <main id="notification-main" tabIndex={-1}><EnterprisePageHeader title={title} description={localizedText(en, "pairs.m_954bcedce6ba204d")}/>
      {selected === null ? <p role="alert">{localizedText(en, "pairs.m_c29db29d947dd102")}</p> : <>
        <EnterpriseToolbar><label>{localizedText(en, "pairs.m_96eabee93e35b626")}<select value={selected} disabled={saving || !authorized} onChange={event => { const value = event.target.value; guard(() => { setDirty(false); setSelected(value); history.replaceState(null, '', `/app/notification-escalation${value ? '?vessel=' + value : ''}`); }); }}><option value="">{localizedText(en, "pairs.m_6bf1a4e450734ac1")}</option>{selected && !vessels?.page?.items.some(v => v.id === selected) && <option value={selected}>{selected}</option>}{vessels?.epoch === epoch && vessels.page?.items.filter(v => isGuid(v.id)).map(v => <option key={v.id} value={v.id}>{v.code} · {v.name}</option>)}</select></label></EnterpriseToolbar>
        {vessels?.epoch === epoch && vessels.failed && <p role="alert">{localizedText(en, "pairs.m_cee03bd7f5893ac6")}</p>}
        {!online && <p role="status">{localizedText(en, "pairs.m_033449c0a4d373e3")}</p>}
        {identity?.epoch === epoch && identity.failed && <p role="alert">{localizedText(en, "pairs.m_91c20124a7cba29e")}</p>}
        {lastUser?.authenticated && lastUser.canRead && selected ? <NotificationInbox key={`${lastUser.tenantId}:${lastUser.actorId}:${selected}`} vessel={selected} session={lastUser} authorized={authorized} epoch={epoch} language={language} onDirty={setDirty} onSaving={setSaving} onNavigate={href => guard(() => location.assign(href))}/> : <p role="status">{localizedText(en, "pairs.m_8d136d6f07e152ed")}</p>}
      </>}
      {pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setDirty(false); setPending(null); action(); }}/>}
    </main></div>;
}
export function NotificationInbox({ vessel, session, authorized, epoch, language, onDirty, onSaving, onNavigate }: {
    vessel: string;
    session: api.Session;
    authorized: boolean;
    epoch: number;
    language: AppLocale;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en', [offset, setOffset] = useState(0), [reload, setReload] = useState(0), [change, setChange] = useState<boolean | null>(null);
    const [result, setResult] = useState<{
        scope: string;
        inbox?: notifications.Inbox;
        preference?: notifications.NoticePreference;
        error?: number;
    } | null>(null);
    const [attempt, setAttempt] = useState<notifications.NoticeCommand | null>(null), [busy, setBusy] = useState(false), [commandError, setCommandError] = useState<number | null>(null);
    const scope = `${epoch}:${offset}:${reload}`, current = result?.scope === scope ? result : null;
    const commandController = useRef<AbortController | null>(null), alive = useRef(true), busyRef = useRef(false);
    useEffect(() => { alive.current = true; return () => { alive.current = false; commandController.current?.abort(); onDirty(false); onSaving(false); }; }, [onDirty, onSaving]);
    useEffect(() => { onDirty(change !== null || !!attempt); onSaving(busy || !!attempt); }, [change, attempt, busy, onDirty, onSaving]);
    useEffect(() => { return () => commandController.current?.abort(); }, [authorized, epoch]);
    useEffect(() => {
        if (!authorized)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        notifications.loadInbox(vessel, offset, c.signal).then(async (inbox) => {
            const preference = inbox.canWritePreference ? await notifications.loadPreference(vessel, c.signal) : undefined;
            if (active)
                setResult({ scope, inbox, preference });
        }).catch(error => { if (active)
            setResult({ scope, error: error instanceof api.ApiError ? error.status : 0 }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [authorized, vessel, offset, reload, epoch, scope]);
    async function send(command: notifications.NoticeCommand) {
        if (!authorized || busyRef.current || !session.csrfToken || !current?.inbox || (command.kind === 'preference' ? !current.inbox.canWritePreference : !current.inbox.canAcknowledge))
            return;
        busyRef.current = true;
        setAttempt(command);
        setBusy(true);
        setCommandError(null);
        const c = new AbortController();
        commandController.current = c;
        const timer = setTimeout(() => c.abort(), 15000);
        try {
            await notifications.sendNoticeCommand(vessel, command, session.csrfToken, c.signal);
            if (alive.current) {
                setAttempt(null);
                setChange(null);
                setReload(n => n + 1);
            }
        }
        catch (error) {
            if (alive.current) {
                const code = error instanceof api.ApiError ? error.status : 0;
                setCommandError(code);
                if ([400, 401, 403, 404, 409].includes(code))
                    setAttempt(null);
                if ([401, 403, 404].includes(code))
                    setResult({ scope, error: code });
            }
        }
        finally {
            clearTimeout(timer);
            busyRef.current = false;
            if (alive.current)
                setBusy(false);
        }
    }
    const locked = !authorized || busy || !!attempt || commandError === 409 || !session.csrfToken;
    const inbox = current?.inbox, preference = current?.preference;
    const severity = { Info: localizedText(en, "pairs.m_1497414cb5959109"), Warning: localizedText(en, "pairs.m_e2d1559eef7ddc20"), Critical: localizedText(en, "pairs.m_3d7537ab29315925") };
    const state = { Open: localizedText(en, "pairs.m_3efd64823614f01e"), Acknowledged: localizedText(en, "pairs.m_a3b9cb4288889c3b"), Escalated: localizedText(en, "pairs.m_f70310439afd4b6e"), SourceChanged: localizedText(en, "pairs.m_f363ebff78cea9e7"), SourceUnavailable: localizedText(en, "pairs.m_d5c931dd594ef91d") };
    if (!authorized)
        return <p role="status">{localizedText(en, "pairs.m_8cbc6494f5a30e95")}</p>;
    return <section aria-label={localizedText(en, "pairs.m_0507fdf952f3ad0b")}>
    {attempt && !busy && <p role="status">{localizedText(en, "pairs.m_a0c5afbf151a6900")}</p>}
    {commandError !== null && <p role="alert">{commandError === 409 ? (localizedText(en, "pairs.m_3774c4baa7604fb5")) : attempt ? (localizedText(en, "pairs.m_8e04231a9489fea2")) : (localizedText(en, "pairs.m_f3914a58822e9271"))}</p>}
    <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_75bfea2052ec1c4a")}><Button variant="outline" disabled={busy} onClick={() => { setCommandError(null); setReload(n => n + 1); }}>{localizedText(en, "pairs.m_d2a88d5061272927")}</Button>{attempt && <Button disabled={busy || (attempt.kind === 'preference' ? !current?.inbox?.canWritePreference : !current?.inbox?.canAcknowledge)} onClick={() => void send(attempt)}>{localizedText(en, "pairs.m_2571ef545daf1799")}</Button>}</EnterpriseActions>
    {!current ? <p role="status">{localizedText(en, "pairs.m_fb172e5c5b4e8988")}</p> : current.error !== undefined ? <p role="alert">{localizedText(en, "pairs.m_2fd2a1f745b26e66")}</p> : inbox && <>
      <p role="status">{inbox.policyState === 'Unavailable' ? (localizedText(en, "pairs.m_48822cdb104871ee")) : (localizedText(en, "pairs.m_adb97419abaedda3"))}</p>
      <p>{localizedText(en, "pairs.m_b55a33dfaabb7c5c")}</p>
      {inbox.canWritePreference && preference && <div className="notification-preference"><label><input type="checkbox" checked={change ?? preference.mutedAdvisory} disabled={locked} onChange={event => setChange(event.target.checked)}/>{localizedText(en, "pairs.m_253b72c9ca68f37a")}</label><EnterpriseActions aria-label={localizedText(en, "pairs.m_d37818d311530a3b")}><Button variant="outline" disabled={locked || change === null} onClick={() => setChange(null)}>{localizedText(en, "pairs.m_2f7be743664c7d31")}</Button><Button disabled={locked || change === null} onClick={() => void send({ kind: 'preference', id: null, key: crypto.randomUUID(), body: JSON.stringify({ expectedVersion: preference.version, mutedAdvisory: change }) })}>{localizedText(en, "pairs.m_d46d7dd8c23b921c")}</Button></EnterpriseActions></div>}
      {!inbox.items.length && <p>{localizedText(en, "pairs.m_819dbcf4e4a13f34")}</p>}
      <ul className="notification-list">{inbox.items.map(row => <li key={row.id}><header><h2>{row.state === 'SourceUnavailable' ? (localizedText(en, "pairs.m_2d8ddbb7f5ac9582")) : row.state === 'SourceChanged' ? (localizedText(en, "pairs.m_3aa219f276007785")) : row.sourceState === 'Due' ? (localizedText(en, "pairs.m_0d1771e46060227a")) : (localizedText(en, "pairs.m_73270dda09c4b3d1"))}</h2><Badge tone={row.severity === 'Critical' ? 'danger' : row.severity === 'Warning' ? 'warning' : 'info'}>{severity[row.severity]}</Badge><Badge tone={row.state === 'SourceUnavailable' ? 'warning' : undefined}>{state[row.state]}</Badge></header><details><summary>{localizedText(en, "pairs.m_c9e2758d37f18098")}</summary><p><code>{row.ruleKey}</code> · {row.policyVersion}</p></details><dl><div><dt>{localizedText(en, "pairs.m_b4f1fa82036ef943")}</dt><dd>{row.sourceId} / {row.sourceRevision}</dd></div><div><dt>{localizedText(en, "pairs.m_0320b67d378f439d")}</dt><dd>{row.dueDate ?? (localizedText(en, "pairs.m_a5a7675bd7000dbd"))}</dd></div><div><dt>{localizedText(en, "pairs.m_715ff08122f31c54")}</dt><dd><time dateTime={row.createdAt}>{row.createdAt}</time></dd></div></dl>{row.state === 'SourceUnavailable' && <p role="status">{localizedText(en, "pairs.m_a7e3db96511147bb")}</p>}{row.mandatory && <p>{localizedText(en, "pairs.m_3c434b6124a9f388")}</p>}<EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_7057c8120005393a")}><Button variant="outline" asChild><a href={`/app/maintenance-plan/${vessel}?id=${row.sourceId}`} onClick={event => { event.preventDefault(); onNavigate(`/app/maintenance-plan/${vessel}?id=${row.sourceId}`); }}>{localizedText(en, "pairs.m_acae4f614bfa637f")}</a></Button>{inbox.canAcknowledge && <Button disabled={locked || change !== null || !['Open', 'Escalated'].includes(row.state)} onClick={() => void send({ kind: 'acknowledge', id: row.id, key: crypto.randomUUID(), body: JSON.stringify({ expectedVersion: row.version }) })}>{localizedText(en, "pairs.m_db571ded695c47a7")}</Button>}</EnterpriseActions></li>)}</ul>
      <EnterprisePagination label={localizedText(en, "pairs.m_bdf4af093f5b863b")} previous={localizedText(en, "pairs.m_fbe33d41fe33d8be")} next={localizedText(en, "pairs.m_fcdb36e24e04d473")} previousDisabled={locked || change !== null || !offset} nextDisabled={locked || change !== null || !inbox.hasMore || offset >= 100000} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/>
    </>}
  </section>;
}
