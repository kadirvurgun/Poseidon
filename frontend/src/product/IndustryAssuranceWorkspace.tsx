import { useEffect, useId, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { ApiError, session, vessels as listVessels, type Session, type Vessel } from './api';
import { isGuid } from './identifiers';
import type { Command } from './tug-readiness-api';
import {
  assuranceApi, base64, decodeInspection, readQueue, sendCommand, writeQueue, type AssurancePage, type Binding, type Import, type Inspection,
} from './assurance-api';
import './tug.css';

// S10 WP-A DEV-113 / T-ASS2-001 (route industry-assurance). SIRE 2.0 intake on a SYNTHETIC column schema (the licensed
// OCIMF format is not present). Import is staged with a diff; a different human decides every changed item (no blind
// overwrite). Findings stay OPEN / UNVERIFIED here: neither the import nor an action link closes or verifies them.
// Field photo captures are queued on the device when offline and replayed with the same Idempotency-Key.
const copy = localizedCopy('IndustryAssurance_tsx_copy');
type Copy = (typeof copy)[AppLocale];
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;
type Failure = { status: number; code: string; missing: string[] };

// /app/industry-assurance?vessel=<id>[&inspection=<id>]
export function assuranceRoute(href: string) {
  const url = new URL(href, 'http://local'), p = url.searchParams, value = (k: string) => p.get(k) ?? '';
  const invalid = !/^\/app\/industry-assurance\/?$/.test(url.pathname) || [...p.keys()].some(k => !['vessel', 'inspection'].includes(k) || p.getAll(k).length !== 1) ||
    ['vessel', 'inspection'].some(k => value(k) !== '' && !isGuid(value(k))) || value('inspection') !== '' && value('vessel') === '';
  return { vessel: value('vessel'), inspection: value('inspection'), invalid };
}
const routeHref = (vessel: string, inspection?: string) => `/app/industry-assurance?vessel=${vessel}${inspection ? `&inspection=${inspection}` : ''}`;

// "header=field" per line; parsing is presentation only, the server validates the mapping.
export function parseBindings(text: string): Binding[] {
  return text.split(/\r?\n/).map(l => l.trim()).filter(Boolean).map(l => { const i = l.lastIndexOf('='); return { header: l.slice(0, i).trim(), field: l.slice(i + 1).trim() }; });
}

export function IndustryAssuranceWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => assuranceRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [vesselOptions, setVesselOptions] = useState<Vessel[]>([]);
  const [page, setPage] = useState<AssurancePage | null>(null), [inspection, setInspection] = useState<Inspection | null>(null);
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [offline, setOffline] = useState(!navigator.onLine), [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false), [failure, setFailure] = useState<Failure | null>(null), [pending, setPending] = useState<Command | null>(null);
  // Offline capture queue (device storage); re-read whenever the vessel or the stored queue changes.
  const [stored, setStored] = useState<{ vessel: string; items: Command[] } | null>(null);
  const queue = stored?.vessel === route.vessel ? stored.items : route.vessel ? readQueue(route.vessel) : [];
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => setRoute(assuranceRoute(window.location.href));
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); };
  }, []);
  useEffect(() => { document.title = `POSEIDON · ${t.title}`; }, [t.title]);
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve(); if (c.signal.aborted) return;
      setLoading(true); setError(0);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      setUser(await session(c.signal));
      if (!route.vessel) { setVesselOptions((await listVessels(0, '', c.signal)).items); setPage(null); setInspection(null); return; }
      setPage(await assuranceApi.page(route.vessel, c.signal));
      setInspection(route.inspection ? await assuranceApi.inspection(route.vessel, route.inspection, c.signal) : null);
    })().then(() => { if (!c.signal.aborted) setLoading(false); }).catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e.status : 500); setLoading(false); });
    return () => c.abort();
  }, [route, reload]);
  const go = (vessel: string, id?: string) => { history.pushState(null, '', routeHref(vessel, id)); setRoute(assuranceRoute(window.location.href)); setFailure(null); };

  // One command; a definite server answer releases it, a network failure keeps it for an exact retry.
  const run = async (command: Command, after?: (value: unknown) => void) => {
    if (!user?.csrfToken || !route.vessel) return;
    setBusy(true); setFailure(null); setPending(command);
    try {
      const value = await sendCommand(command, user.csrfToken, new AbortController().signal);
      setPending(null);
      if (after) after(value);
      else { const saved = decodeInspection(value, route.vessel); setInspection(saved); if (route.inspection !== saved.id) go(route.vessel, saved.id); }
      setReload(n => n + (after ? 1 : 0));
    } catch (e) {
      if (e instanceof ApiError) { setPending(null); setFailure({ status: e.status, code: e.code ?? 'request_failed', missing: (e as ApiError & { missing?: string[] }).missing ?? [] }); }
      else setFailure({ status: 0, code: 'network', missing: [] });
    } finally { setBusy(false); }
  };
  const enqueue = (command: Command) => {
    const next = [...queue, command];
    if (writeQueue(route.vessel, next)) setStored({ vessel: route.vessel, items: next }); else setFailure({ status: 0, code: 'offline_queue_unavailable', missing: [] });
  };
  const flush = async () => {
    if (!user?.csrfToken) return;
    let remaining = [...queue];
    for (const command of queue) {
      try { await sendCommand(command, user.csrfToken, new AbortController().signal); remaining = remaining.filter(c => c.key !== command.key); }
      catch (e) {
        // A definite refusal is surfaced and dropped from the queue; a network failure keeps the exact command.
        if (e instanceof ApiError) { remaining = remaining.filter(c => c.key !== command.key); setFailure({ status: e.status, code: e.code ?? 'request_failed', missing: [] }); }
        else break;
      }
    }
    writeQueue(route.vessel, remaining); setStored({ vessel: route.vessel, items: remaining }); setReload(n => n + 1);
  };

  return <div className="product-shell enterprise-shell tug-workspace industry-assurance">
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={h => window.location.assign(h)} title={t.title} mainId="assurance-main"/>
    <main id="assurance-main" tabIndex={-1} aria-busy={loading || busy} data-testid="industry-assurance">
      <div className="enterprise-page-heading"><div><h1>{t.title}</h1></div></div>
      <p className="tug-planning-boundary">{t.boundary}</p>
      {offline && <p role="alert" data-state="offline">{t.offline}</p>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[401, 403].includes(error) ? t.denied : error === 404 ? t.notFound : error === 400 ? t.invalidRoute : t.unavailable}</p>
        <Button type="button" variant="outline" disabled={offline} onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>}
      {failure && <div role="alert" data-code={failure.code}>
        <p>{failure.code === 'not_applicable_vessel_type' ? t.notApplicableHelp : failure.status === 409 ? t.conflict : [401, 403].includes(failure.status) ? t.forbidden
          : failure.status === 422 ? t.blocked : failure.status === 0 ? t.uncertain : t.failed} <code>{failure.code}</code></p>
        {failure.missing.length > 0 && <ul aria-label={t.blocked}>{failure.missing.map(m => <li key={m}><code>{m}</code></li>)}</ul>}
        {failure.status === 409 && <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.reload}</Button>}
        {failure.status === 0 && pending && <Button type="button" variant="outline" disabled={busy || offline} onClick={() => void run(pending)}>{t.retryExact}</Button>}
      </div>}
      {queue.length > 0 && <p role="status" data-state="queued">{t.queued.replace('{{count}}', String(queue.length))}
        {' '}<Button type="button" variant="outline" disabled={offline || busy} onClick={() => void flush()}>{t.sendQueued}</Button></p>}
      {!loading && !error && !route.vessel && <section aria-label={t.chooseVessel}><h2>{t.chooseVessel}</h2>
        {vesselOptions.length === 0 ? <p role="status" data-state="empty">{t.noVessels}</p> :
          <ul>{vesselOptions.map(v => <li key={v.id}><a href={routeHref(v.id)} onClick={e => { e.preventDefault(); go(v.id); }}>{v.code} · {v.name}</a></li>)}</ul>}</section>}
      {!loading && !error && page && <Overview page={page} t={t} busy={busy} offline={offline} onOpen={id => go(route.vessel, id)} run={run}/>}
      {!loading && !error && page && inspection && <InspectionView inspection={inspection} page={page} t={t} busy={busy} offline={offline} vessel={route.vessel} run={run} enqueue={enqueue}/>}
    </main></div>;
}

type Run = (command: Command, after?: (value: unknown) => void) => Promise<void>;

function Overview({ page, t, busy, offline, onOpen, run }: { page: AssurancePage; t: Copy; busy: boolean; offline: boolean; onOpen: (id: string) => void; run: Run }) {
  const can = page.capabilities, ids = { key: useId(), bindings: useId(), mapping: useId(), ref: useId(), date: useId(), file: useId() };
  const [key, setKey] = useState('sire2-synthetic'), [bindings, setBindings] = useState(''), [mapping, setMapping] = useState(page.mappings[0]?.id ?? '');
  const [reference, setReference] = useState(''), [date, setDate] = useState(''), [file, setFile] = useState<File | null>(null);
  const stage = async () => {
    if (!file || !mapping) return;
    const workbook = base64(await file.arrayBuffer());
    await run(assuranceApi.stage(page.vesselId, { inspectionId: crypto.randomUUID(), mappingId: mapping, referenceNo: reference || null, inspectionDate: date || null, workbook }));
  };
  return <>
    <section aria-labelledby="assurance-applicability" data-applicability={page.applicability}><h2 id="assurance-applicability">{t.applicability}</h2>
      <p><Badge tone={page.applicability === 'APPLICABLE' ? 'neutral' : 'warning'}>{label(t, 'app_' + page.applicability)}</Badge>{' '}
        {page.applicability === 'NOT_APPLICABLE' ? t.notApplicableHelp : page.applicability === 'REVIEW_REQUIRED' ? t.reviewRequiredHelp : null}</p>
      <p role="note">{t.syntheticSchema}</p>
    </section>
    <section aria-labelledby="assurance-inspections"><h2 id="assurance-inspections">{t.inspections}</h2>
      {page.inspections.length === 0 ? <p role="status" data-state="empty">{t.noInspections}</p> :
        <ul>{page.inspections.map(i => <li key={i.id}><Button type="button" variant="outline" onClick={() => onOpen(i.id)}>
          {i.referenceNo} · <time dateTime={i.inspectionDate}>{i.inspectionDate}</time> · {i.items} {t.items} · {i.openFindings} {t.openFindings}
          {i.pendingImport && <> · <Badge tone="warning">{t.pendingImport}</Badge></>}</Button></li>)}</ul>}
    </section>
    <section aria-labelledby="assurance-mappings"><h2 id="assurance-mappings">{t.mappings}</h2>
      {page.mappings.length === 0 ? <p role="status" data-state="empty">{t.noMappings}</p> :
        <ul>{page.mappings.map(m => <li key={m.id}><code>{m.key}</code> v{m.version} · {m.bindings.map(b => `${b.header}=${b.field}`).join(', ')}</li>)}</ul>}
      {can.canWriteMapping && <form onSubmit={e => { e.preventDefault(); void run(assuranceApi.mapping(key, parseBindings(bindings)), () => undefined); }}>
        <label htmlFor={ids.key}>{t.mappingKey}</label><input id={ids.key} value={key} onChange={e => setKey(e.target.value)}/>
        <label htmlFor={ids.bindings}>{t.mappingBindings}</label>
        <textarea id={ids.bindings} rows={4} value={bindings} placeholder={can.fields.map(f => `<header>=${f}`).join('\n')} onChange={e => setBindings(e.target.value)}/>
        <Button type="submit" disabled={busy || offline}>{t.saveMapping}</Button></form>}
    </section>
    {can.canImport && page.applicability !== 'NOT_APPLICABLE' && <section aria-labelledby="assurance-import"><h2 id="assurance-import">{t.newImport}</h2>
      <form onSubmit={e => { e.preventDefault(); void stage(); }}>
        <label htmlFor={ids.mapping}>{t.mapping}</label>
        <select id={ids.mapping} value={mapping} onChange={e => setMapping(e.target.value)}><option value="">{t.notSelected}</option>
          {page.mappings.map(m => <option key={m.id} value={m.id}>{m.key} v{m.version}</option>)}</select>
        <label htmlFor={ids.ref}>{t.reference}</label><input id={ids.ref} value={reference} maxLength={64} onChange={e => setReference(e.target.value)}/>
        <label htmlFor={ids.date}>{t.date}</label><input id={ids.date} type="date" value={date} onChange={e => setDate(e.target.value)}/>
        <label htmlFor={ids.file}>{t.workbook}</label>
        <input id={ids.file} type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={e => setFile(e.target.files?.[0] ?? null)}/>
        <Button type="submit" disabled={busy || offline || !file || !mapping}>{t.stage}</Button></form>
    </section>}
  </>;
}

function ImportReview({ imp, vessel, canAccept, t, busy, offline, run }: { imp: Import; vessel: string; canAccept: boolean; t: Copy; busy: boolean; offline: boolean; run: Run }) {
  const [accepted, setAccepted] = useState<Record<string, boolean | undefined>>({}), [reason, setReason] = useState(''), reasonId = useId();
  const changed = imp.diff.filter(d => d.change === 'CHANGED');
  const complete = changed.every(d => accepted[d.questionRef] !== undefined);
  const decide = (decision: 'ACCEPT' | 'REJECT') => run(assuranceApi.decide(vessel, imp, decision,
    decision === 'ACCEPT' ? changed.filter(d => accepted[d.questionRef]).map(d => d.questionRef) : [],
    decision === 'ACCEPT' ? changed.filter(d => accepted[d.questionRef] === false).map(d => d.questionRef) : [], reason));
  return <article aria-label={t.diff} data-state={imp.state}>
    <h3>{t.diff} · <Badge tone={imp.state === 'PENDING_REVIEW' ? 'warning' : 'neutral'}>{label(t, 'import_' + imp.state)}</Badge></h3>
    <p>{label(t, 'change_NEW')}: {imp.new} · {label(t, 'change_CHANGED')}: {imp.changed} · {label(t, 'change_UNCHANGED')}: {imp.unchanged} · {label(t, 'change_ABSENT')}: {imp.absent}</p>
    <table><thead><tr><th scope="col">{t.question}</th><th scope="col">{t.change}</th><th scope="col">{t.fields}</th><th scope="col">{t.decision}</th></tr></thead>
      <tbody>{imp.diff.map(d => <tr key={d.questionRef} data-change={d.change}><td><code>{d.questionRef}</code></td>
        <td><Badge tone={d.change === 'CHANGED' ? 'warning' : 'neutral'}>{label(t, 'change_' + d.change)}</Badge></td><td>{d.fields.join(', ')}</td>
        <td>{d.change === 'CHANGED' && imp.state === 'PENDING_REVIEW' && canAccept && !imp.importedByMe ? <span>
          <label><input type="radio" name={`d-${d.questionRef}`} checked={accepted[d.questionRef] === true} onChange={() => setAccepted(a => ({ ...a, [d.questionRef]: true }))}/>{t.acceptChange}</label>
          <label><input type="radio" name={`d-${d.questionRef}`} checked={accepted[d.questionRef] === false} onChange={() => setAccepted(a => ({ ...a, [d.questionRef]: false }))}/>{t.rejectChange}</label>
        </span> : d.change === 'ABSENT' ? t.retained : null}</td></tr>)}</tbody></table>
    {imp.state === 'PENDING_REVIEW' && (imp.importedByMe ? <p role="note">{t.decisionSoD}</p> : canAccept && <form onSubmit={e => e.preventDefault()}>
      <label htmlFor={reasonId}>{t.reason}</label><textarea id={reasonId} rows={2} value={reason} onChange={e => setReason(e.target.value)}/>
      <Button type="button" disabled={busy || offline || !complete || reason.trim().length < 10} onClick={() => void decide('ACCEPT')}>{t.accept}</Button>
      <Button type="button" variant="outline" disabled={busy || offline || reason.trim().length < 10} onClick={() => void decide('REJECT')}>{t.reject}</Button></form>)}
  </article>;
}

function InspectionView({ inspection, page, t, busy, offline, vessel, run, enqueue }: { inspection: Inspection; page: AssurancePage; t: Copy; busy: boolean; offline: boolean
  vessel: string; run: Run; enqueue: (c: Command) => void }) {
  const can = page.capabilities;
  const ids = { q: useId(), file: useId(), rev: useId(), sum: useId(), media: useId(), action: useId() };
  const [question, setQuestion] = useState(inspection.items[0]?.questionRef ?? ''), [fileId, setFileId] = useState(''), [fileRev, setFileRev] = useState('1');
  const [checksum, setChecksum] = useState(''), [media, setMedia] = useState('PHOTO'), [actions, setActions] = useState<Record<string, string>>({});
  const capture = () => {
    const command = assuranceApi.evidence(vessel, inspection.id, { questionRef: question, file: { id: fileId, revision: Number(fileRev), checksum }, mediaKind: media,
      capturedAt: new Date().toISOString(), origin: offline ? 'OFFLINE' : 'ONLINE' });
    if (offline) enqueue(command); else void run(command);
  };
  return <section aria-labelledby="assurance-inspection" data-testid="assurance-inspection">
    <h2 id="assurance-inspection">{inspection.referenceNo} · <time dateTime={inspection.inspectionDate}>{inspection.inspectionDate}</time></h2>
    <p><Badge tone="warning">{label(t, 'app_' + inspection.applicability)}</Badge> · {t.provenance}: <code>{inspection.provenance}</code> · r{inspection.revision}</p>
    <p role="note">{inspection.notice}</p>
    {inspection.imports.map(i => <ImportReview key={i.id} imp={i} vessel={vessel} canAccept={can.canAccept} t={t} busy={busy} offline={offline} run={run}/>)}
    <h3>{t.items}</h3>
    {inspection.items.length === 0 ? <p role="status" data-state="empty">{t.noItems}</p> :
      <table><thead><tr><th scope="col">{t.question}</th><th scope="col">{t.observation}</th><th scope="col">{t.evidence}</th></tr></thead>
        <tbody>{inspection.items.map(i => <tr key={i.questionRef}><td><code>{i.questionRef}</code> r{i.revision}</td>
          <td><Badge tone={i.content.observationType === 'NEGATIVE' || i.content.observationType === 'UNKNOWN' ? 'warning' : 'neutral'}>{label(t, 'obs_' + i.content.observationType)}</Badge>
            {i.content.observationCategory && <> <code>{i.content.observationCategory}</code></>} {i.content.observationText}</td><td>{i.evidence}</td></tr>)}</tbody></table>}
    <h3>{t.findings}</h3>
    {inspection.findings.length === 0 ? <p role="status" data-state="empty">{t.noFindings}</p> :
      <ul>{inspection.findings.map(f => <li key={f.id} data-state={f.state}>
        <code>{f.questionRef}</code> · <Badge tone="warning">{label(t, 'finding_' + f.state)}</Badge> <Badge tone="warning">{t.unverified}</Badge>
        {!f.observationPresent && <> <Badge tone="warning">{t.observationRemoved}</Badge></>} · {label(t, 'link_' + f.actionLinkState)}
        {f.actionId && <> <bdi dir="ltr">{f.actionId}</bdi></>}
        {f.actionLinkState === 'UNLINKED' && can.canLinkAction && <span>
          <label htmlFor={`${ids.action}-${f.id}`}>{t.actionId}</label>
          <input id={`${ids.action}-${f.id}`} value={actions[f.id] ?? ''} onChange={e => setActions(a => ({ ...a, [f.id]: e.target.value.trim() }))}/>
          <Button type="button" variant="outline" disabled={busy || offline || !isGuid(actions[f.id] ?? '')} onClick={() => void run(assuranceApi.link(vessel, f, actions[f.id]!))}>{t.linkAction}</Button>
        </span>}</li>)}</ul>}
    <h3>{t.evidence}</h3>
    {inspection.evidence.length === 0 ? <p role="status" data-state="empty">{t.noEvidence}</p> :
      <ul>{inspection.evidence.map(e => <li key={e.id}><code>{e.questionRef}</code> · {label(t, 'media_' + e.mediaKind)} · {label(t, 'origin_' + e.origin)} ·
        <time dateTime={e.capturedAt}> {e.capturedAt}</time> · <Badge tone="warning">{t.unverified}</Badge> <a href={e.fileHref}>{t.file}</a></li>)}</ul>}
    {can.canCaptureEvidence && inspection.items.length > 0 && <form aria-label={t.captureEvidence} onSubmit={e => { e.preventDefault(); capture(); }}>
      <label htmlFor={ids.q}>{t.question}</label><select id={ids.q} value={question} onChange={e => setQuestion(e.target.value)}>
        {inspection.items.map(i => <option key={i.questionRef} value={i.questionRef}>{i.questionRef}</option>)}</select>
      <label htmlFor={ids.media}>{t.media}</label><select id={ids.media} value={media} onChange={e => setMedia(e.target.value)}>
        {can.mediaKinds.map(m => <option key={m} value={m} disabled={!can.availableMediaKinds.includes(m)}>{label(t, 'media_' + m)}</option>)}</select>
      {!can.availableMediaKinds.includes('VIDEO') && <p role="note">{t.videoUnavailable}</p>}
      <label htmlFor={ids.file}>{t.fileId}</label><input id={ids.file} value={fileId} onChange={e => setFileId(e.target.value.trim())}/>
      <label htmlFor={ids.rev}>{t.fileRevision}</label><input id={ids.rev} inputMode="numeric" value={fileRev} onChange={e => setFileRev(e.target.value)}/>
      <label htmlFor={ids.sum}>{t.checksum}</label><input id={ids.sum} value={checksum} onChange={e => setChecksum(e.target.value.trim())}/>
      <Button type="submit" disabled={busy || !isGuid(fileId) || !/^[A-Fa-f0-9]{64}$/.test(checksum) || !/^[1-9]\d*$/.test(fileRev)}>{offline ? t.queueOffline : t.captureEvidence}</Button>
    </form>}
  </section>;
}
