import { useEffect, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import type { Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { ApiError } from './api';
import { assuranceApi, decodePack, sendCommand, type Pack, type PackList } from './assurance-api';

// S10 WP-A DEV-064 / T-S10-001: audit-ready pack. A pack is a system compilation, not an official record: the label is
// always shown. The server re-authorizes every item on each read; excluded items are only counted. Superseded evidence
// revisions are shown with their own badge, never as current; a hash mismatch or unreadable source is a warning.
const copy = localizedCopy('AuditPack_tsx_copy');
type Copy = (typeof copy)[AppLocale];
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;

export function AuditPackPanel({ vessel, csrf, online, language }: { vessel: string; csrf: string | null; online: boolean; language: AppLocale }) {
  const t = copy[language];
  const [list, setList] = useState<PackList | null>(null), [pack, setPack] = useState<Pack | null>(null);
  const [error, setError] = useState(0), [busy, setBusy] = useState(false), [reload, setReload] = useState(0), [failure, setFailure] = useState<string | null>(null);
  useEffect(() => {
    if (!online) return;
    const c = new AbortController(); let active = true;
    assuranceApi.packs(vessel, c.signal).then(v => { if (active) { setList(v); setError(0); } })
      .catch(e => { if (active) setError(e instanceof ApiError ? e.status : 500); });
    return () => { active = false; c.abort(); };
  }, [vessel, online, reload]);
  const open = async (id: string) => {
    setFailure(null);
    try { setPack(await assuranceApi.pack(vessel, id, new AbortController().signal)); }
    catch (e) { setFailure(e instanceof ApiError ? e.code ?? String(e.status) : 'network'); }
  };
  const compile = async () => {
    if (!csrf) return;
    setBusy(true); setFailure(null);
    try { const saved = decodePack(await sendCommand(assuranceApi.compile(vessel, list?.items[0]?.id ?? null), csrf, new AbortController().signal), vessel); setPack(saved); setReload(n => n + 1); }
    catch (e) { setFailure(e instanceof ApiError ? e.code ?? String(e.status) : 'network'); }
    finally { setBusy(false); }
  };
  return <section aria-labelledby="audit-pack-title" data-testid="audit-pack">
    <h2 id="audit-pack-title">{t.title}</h2>
    <p role="note" data-label="SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD"><Badge tone="warning">{t.label}</Badge></p>
    {!online && <p role="status" data-state="offline">{t.offline}</p>}
    {error > 0 && <p role="alert">{error === 403 ? t.denied : t.loadError}</p>}
    {failure && <p role="alert" data-code={failure}>{failure === 'pack_scope_empty' ? t.scopeEmpty : t.failed} <code>{failure}</code></p>}
    {list && <>
      {list.canCompile && <Button type="button" disabled={busy || !online || !csrf} onClick={() => void compile()}>{busy ? t.compiling : t.compile}</Button>}
      {list.items.length === 0 ? <p role="status" data-state="empty">{t.empty}</p> :
        <ul aria-label={t.packs}>{list.items.map(p => <li key={p.id}><Button type="button" variant={pack?.id === p.id ? 'default' : 'outline'} onClick={() => void open(p.id)}>
          {t.asOf} <time dateTime={p.asOf}>{p.asOf}</time> · {p.itemCount} {t.items}{p.createdByMe ? ` · ${t.mine}` : ''}</Button></li>)}</ul>}
      {list.hasMore && <p role="note">{t.more}</p>}
    </>}
    {pack && <PackView pack={pack} vessel={vessel} t={t}/>}
  </section>;
}

function PackView({ pack, vessel, t }: { pack: Pack; vessel: string; t: Copy }) {
  return <article aria-label={t.pack} data-testid="audit-pack-view">
    <dl>
      <div><dt>{t.asOf}</dt><dd><time dateTime={pack.asOf}>{pack.asOf}</time></dd></div>
      <div><dt>{t.manifestHash}</dt><dd><code>{pack.manifestHash}</code></dd></div>
      <div><dt>{t.items}</dt><dd>{pack.included} / {pack.itemCount}</dd></div>
    </dl>
    {pack.excluded > 0 && <p role="note" data-state="excluded">{t.excluded.replace('{{count}}', String(pack.excluded))}</p>}
    {pack.unavailableChains > 0 && <p role="note">{t.unavailableChains.replace('{{count}}', String(pack.unavailableChains))}</p>}
    {pack.truncated && <p role="alert">{t.truncated}</p>}
    <p><a href={assuranceApi.exportHref(vessel, pack.id)} download>{t.export}</a></p>
    <table><thead><tr><th scope="col">{t.kind}</th><th scope="col">{t.resource}</th><th scope="col">{t.state}</th><th scope="col">{t.integrity}</th><th scope="col">{t.links}</th></tr></thead>
      <tbody>{pack.items.map(i => <tr key={i.ordinal} data-state={i.state} data-integrity={i.integrity}>
        <td>{label(t, 'kind_' + i.kind)}</td>
        <td><bdi dir="ltr">{i.resourceId}</bdi> · r{i.revision}{i.savedAt && <> · <time dateTime={i.savedAt}>{i.savedAt}</time></>}</td>
        <td><Badge tone={i.state === 'Current' || i.state === 'Recorded' ? 'neutral' : 'warning'}>{label(t, 'state_' + i.state)}</Badge></td>
        <td><Badge tone={i.integrity === 'Match' ? 'neutral' : 'warning'}>{label(t, 'integrity_' + i.integrity)}</Badge></td>
        <td>{i.fileHref && <a href={i.fileHref}>{t.file}</a>} {i.historyHref && <a href={i.historyHref.replace('/api/v1/audit?', '/app/audit?')}>{t.history}</a>}</td>
      </tr>)}</tbody></table>
  </article>;
}
