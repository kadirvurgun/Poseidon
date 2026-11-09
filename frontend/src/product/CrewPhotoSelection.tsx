import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useLayoutEffect, useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { crewPhoto, saveCrewPhoto, type CrewPhoto, type PhotoAttempt } from './crew-photo-api';
import type { CrewFileManifest, CrewFileVersion } from './crew-files-api';
type Props = {
    vessel: string;
    assignment: string;
    person: string;
    csrf: string;
    language: AppLocale;
    manifest: CrewFileManifest;
    blocked: boolean;
    onDirty: (value: boolean) => void;
    onReview: (row: CrewFileVersion) => void;
    onDenied: () => void;
};
export function CrewPhotoSelection(p: Props) {
    const en = p.language === 'en';
    const scope = `${p.vessel}:${p.assignment}:${p.person}:${p.csrf}`;
    const [loaded, setLoaded] = useState<{
        scope: string;
        data: CrewPhoto;
    } | null>(null);
    const [attempt, setAttempt] = useState<{
        scope: string;
        data: PhotoAttempt;
    } | null>(null);
    const [failure, setFailure] = useState<'load' | 'uncertain' | 'conflict' | null>(null);
    const [busy, setBusy] = useState(false);
    const [generation, reload] = useState(0);
    const controller = useRef<AbortController | null>(null);
    const callbacks = useRef(p);
    useLayoutEffect(() => { callbacks.current = p; }, [p]);
    const data = loaded?.scope === scope ? loaded.data : null;
    const pending = attempt?.scope === scope ? attempt.data : null;
    useEffect(() => {
        const c = new AbortController();
        controller.current = c;
        let active = true;
        crewPhoto(p.vessel, p.assignment, p.person, c.signal).then(data => { if (active) {
            setLoaded({ scope, data });
            setFailure(null);
        } }).catch(e => {
            if (!active)
                return;
            setLoaded(null);
            setFailure('load');
            if (e instanceof ApiError && [401, 403, 404].includes(e.status))
                callbacks.current.onDenied();
        });
        return () => { active = false; c.abort(); callbacks.current.onDirty(false); };
    }, [scope, generation, p.vessel, p.assignment, p.person]);
    async function save(fileId: string | null) {
        if (!data || busy || p.blocked)
            return;
        const operation = pending ?? { key: crypto.randomUUID(), command: { baseVersion: data.version, fileId, policyVersion: data.policyVersion, purpose: data.purpose } };
        setAttempt({ scope, data: operation });
        p.onDirty(true);
        setBusy(true);
        const c = controller.current!;
        try {
            await saveCrewPhoto(p.vessel, p.assignment, p.person, operation, p.csrf, c.signal);
            if (c.signal.aborted)
                return;
            setAttempt(null);
            p.onDirty(false);
            setLoaded(null);
            reload(n => n + 1);
        }
        catch (e) {
            if (c.signal.aborted)
                return;
            if (e instanceof ApiError && [400, 401, 403, 404].includes(e.status)) {
                setLoaded(null);
                setAttempt(null);
                p.onDirty(false);
                p.onDenied();
            }
            else
                setFailure(e instanceof ApiError && e.status === 409 ? 'conflict' : 'uncertain');
        }
        finally {
            if (!c.signal.aborted)
                setBusy(false);
        }
    }
    const photos = p.manifest.items.filter(f => f.kind === 'Photo' && f.state === 'CleanTransport');
    return <section aria-label={localizedText(en, "pairs.m_2bb88ba81c62d315")}>
    <h3>{localizedText(en, "pairs.m_2bb88ba81c62d315")}</h3>
    <p>{localizedText(en, "pairs.m_49e90f89059e18c8")}</p>
    {failure && <p role="alert">{failure === 'conflict' ? (localizedText(en, "pairs.m_83dd7702b0c17d98")) : failure === 'uncertain' ? (localizedText(en, "pairs.m_7a21bf257fbb693c")) : (localizedText(en, "pairs.m_e1c3c055a219ea16"))}</p>}
    {data && <>
      <p role="status">{data.state === 'Missing' ? (localizedText(en, "pairs.m_d74fa93ebd19db8e")) : data.state === 'Unavailable' ? (localizedText(en, "pairs.m_fd19c91bddc4f101")) : (localizedText(en, "pairs.m_17e655e10c39cfde"))}</p>
      {data.selected && <button disabled={p.blocked || busy || !!pending} onClick={() => p.onReview(data.selected!)}>{localizedText(en, "pairs.m_eaeaed7fc71b43eb")} · {data.selected.revision}</button>}
      {data.canWrite && <fieldset disabled={p.blocked || busy || !!pending}>
        {photos.map(f => <button key={f.id} onClick={() => void save(f.id)}>{localizedText(en, "pairs.m_0ae4e50fd1dd33bc")} · {f.revision}</button>)}
        <button disabled={data.state === 'Missing'} onClick={() => void save(null)}>{localizedText(en, "pairs.m_ef93cb4099f21b8a")}</button>
      </fieldset>}
    </>}
    {pending && failure === 'uncertain' && <button disabled={busy || p.blocked} onClick={() => void save(pending.command.fileId)}>{localizedText(en, "pairs.m_813591c7f2cefe9c")}</button>}
    {pending && failure === 'conflict' && <button disabled={busy} onClick={() => { setAttempt(null); setLoaded(null); p.onDirty(false); reload(n => n + 1); }}>{localizedText(en, "pairs.m_7c2b2786881c1841")}</button>}
    {!pending && <button disabled={busy || p.blocked} onClick={() => { setLoaded(null); reload(n => n + 1); }}>{localizedText(en, "pairs.m_4c4d078fc3169a69")}</button>}
  </section>;
}
