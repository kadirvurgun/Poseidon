import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useLayoutEffect, useEffect, useRef, useState } from 'react';
import { crewFileVersions, type CrewFileKind, type CrewFileManifest } from './crew-files-api';
import type { PersonFileOwner } from './file-api';
import { VesselFiles } from './VesselFiles';
import { CrewPhotoSelection } from './CrewPhotoSelection';
type Props = {
    vessel: string;
    assignment: string;
    person: string;
    language: AppLocale;
    csrf: string;
    onDirty: (value: boolean) => void;
};
// Parent mounts this private child for one current authenticated session/tenant/vessel/person only.
export function CrewPersonalFiles(props: Props) {
    const en = props.language === 'en';
    const scope = `${props.vessel}:${props.assignment}:${props.person}:${props.csrf}`;
    const [loaded, setLoaded] = useState<{
        scope: string;
        data: CrewFileManifest;
    } | null>(null);
    const [owner, setOwner] = useState<{
        scope: string;
        data: PersonFileOwner;
    } | null>(null);
    const [kind, setKind] = useState<CrewFileKind>('Photo');
    const [expiry, setExpiry] = useState('');
    const [error, setError] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [busy, setBusy] = useState(false);
    const [generation, reload] = useState(0);
    const [photoDirty, setPhotoDirty] = useState(false);
    const source = loaded?.scope === scope ? loaded.data : null;
    const selected = owner?.scope === scope ? owner.data : undefined;
    const changeDirty = (value: boolean) => { setDirty(value); props.onDirty(value || photoDirty); };
    const callbacks = useRef(props);
    useLayoutEffect(() => { callbacks.current = props; }, [props]);
    useEffect(() => () => callbacks.current.onDirty(false), []);
    const resetScope1 = [scope, generation, props.vessel, props.assignment, props.person] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], scope) || !Object.is(previousScope1[1], generation) || !Object.is(previousScope1[2], props.vessel) || !Object.is(previousScope1[3], props.assignment) || !Object.is(previousScope1[4], props.person)) {
        setPreviousScope1(resetScope1);
        setLoaded(null);
        setError(false);
    }
    useEffect(() => {
        const c = new AbortController();
        let active = true;
        crewFileVersions(props.vessel, props.assignment, props.person, c.signal).then(data => {
            if (active) {
                setLoaded({ scope, data });
                setKind(old => data.kinds.includes(old) ? old : data.kinds[0]);
            }
        }).catch(() => {
            if (active) {
                setLoaded(null);
                setOwner(null);
                setExpiry('');
                setDirty(false);
                callbacks.current.onDirty(false);
                setError(true);
            }
        });
        return () => { active = false; c.abort(); };
    }, [scope, generation, props.vessel, props.assignment, props.person]);
    const label = (value: CrewFileKind) => ({ Photo: localizedText(en, "pairs.m_d0c8ec50587fd4bd"), Passport: localizedText(en, "pairs.m_db2ea4b40a052d11"),
        SeamanBook: localizedText(en, "pairs.m_f3e6b930fb3e208c"), CertificateDocument: localizedText(en, "pairs.m_72a1820bda4a96f7") })[value];
    return <section aria-label={localizedText(en, "pairs.m_5dba1c00d950f6f6")}>
    <h2>{localizedText(en, "pairs.m_5dba1c00d950f6f6")}</h2>
    <p>{localizedText(en, "pairs.m_0a796092e1663f24")}</p>
    {error && <p role="alert">{localizedText(en, "pairs.m_a916644ed662af49")}</p>}
    <button disabled={dirty || busy || photoDirty} onClick={() => { setOwner(null); reload(n => n + 1); }}>{localizedText(en, "pairs.m_91288c402383fb98")}</button>
    {source && <>
      <p>{source.policyVersion} · {source.purpose}</p>
      {source.kinds.includes('Photo') && <CrewPhotoSelection key={scope} {...props} manifest={source} blocked={dirty || busy} onDirty={value => { setPhotoDirty(value); if (value)
                setOwner(null); props.onDirty(value || dirty); }} onReview={row => setOwner({ scope, data: { type: 'Person', assignment: props.assignment, kind: 'Photo', revision: row.revision, expiresOn: null } })} onDenied={() => { setLoaded(null); setOwner(null); setPhotoDirty(false); setDirty(false); props.onDirty(false); setError(true); }}/>}

      {source.canWrite && <form onSubmit={e => { e.preventDefault(); const revision = source.next[kind]; if (revision)
                setOwner({ scope, data: { type: 'Person', assignment: props.assignment, kind, revision, expiresOn: kind === 'Photo' ? null : expiry || null } }); }}>
        <fieldset disabled={dirty || busy || photoDirty}><label>{localizedText(en, "pairs.m_b6ff0a2c6aed236e")}<select value={kind} onChange={e => { setKind(e.target.value as CrewFileKind); setExpiry(''); setOwner(null); }}>{source.kinds.map(k => <option key={k} value={k}>{label(k)}</option>)}</select></label>
          {kind !== 'Photo' && <label>{localizedText(en, "pairs.m_97f8cf9035a2e34a")}<input type="date" value={expiry} onChange={e => { setExpiry(e.target.value); setOwner(null); }}/></label>}
          <button>{localizedText(en, "pairs.m_9894f05cb2fcf2ff")}</button></fieldset>
      </form>}
      <ol>{source.items.map(row => <li key={row.id}>
        <button disabled={dirty || busy || photoDirty} onClick={() => setOwner({ scope, data: { type: 'Person', assignment: props.assignment, kind: row.kind, revision: row.revision, expiresOn: row.expiresOn } })}>{label(row.kind)} · {row.revision}</button>
        {' · '}<span>{row.expiresOn ?? (localizedText(en, "pairs.m_e32290c3d5424d23"))}</span>{' · '}<span>{row.state === 'CleanTransport' ? (localizedText(en, "pairs.m_2b9182ba51fb844b")) : (localizedText(en, "pairs.m_2f78663c1bbaf3b3"))}</span>
      </li>)}</ol>
      {source.hasMore && <p role="status">{localizedText(en, "pairs.m_cecac3865bcd5ee4")}</p>}
      {selected && <VesselFiles key={`${scope}:${selected.kind}:${selected.revision}`} owner={selected} vessel={props.vessel} csrf={props.csrf} language={props.language} onDirty={changeDirty} onSaving={setBusy} onCommitted={() => reload(n => n + 1)}/>}
    </>}
  </section>;
}
