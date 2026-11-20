import { useEffect, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { ApiError, session, vessels as listVessels, type Session, type Vessel } from './api';
import { isGuid } from './identifiers';
import { assuranceApi, signalKinds, type TrustItem, type TrustPage } from './assurance-api';
import './tug.css';

// S10 WP-A DEV-065 / T-S10-002 (route data-trust-center). Shows missing / stale / conflicting / unverified / orphan
// signals with source, provenance and freshness. Presentation only: there is no green or "ready" state on this screen;
// the best state is "no material signal", and any material signal holds readiness at review required.
const copy = localizedCopy('DataTrust_tsx_copy');
type Copy = (typeof copy)[AppLocale];
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;

// /app/data-trust-center?vessel=<id>[&offset=<n>]
export function dataTrustRoute(href: string) {
  const url = new URL(href, 'http://local'), p = url.searchParams, value = (k: string) => p.get(k) ?? '';
  const offset = value('offset') === '' ? 0 : Number(value('offset'));
  const invalid = !/^\/app\/data-trust-center\/?$/.test(url.pathname) || [...p.keys()].some(k => !['vessel', 'offset'].includes(k) || p.getAll(k).length !== 1) ||
    value('vessel') !== '' && !isGuid(value('vessel')) || !Number.isSafeInteger(offset) || offset < 0 || offset > 100000 || value('offset') !== '' && !/^\d+$/.test(value('offset'));
  return { vessel: value('vessel'), offset: invalid ? 0 : offset, invalid };
}
const routeHref = (vessel: string, offset = 0) => `/app/data-trust-center?vessel=${vessel}${offset ? `&offset=${offset}` : ''}`;

export function DataTrustCenterWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => dataTrustRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [vesselOptions, setVesselOptions] = useState<Vessel[]>([]);
  const [page, setPage] = useState<TrustPage | null>(null), [loading, setLoading] = useState(true), [error, setError] = useState(0);
  const [offline, setOffline] = useState(!navigator.onLine), [reload, setReload] = useState(0), [loadedAt, setLoadedAt] = useState(() => Date.now()), [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => setRoute(dataTrustRoute(window.location.href));
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    const timer = setInterval(() => setNow(Date.now()), 60000);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); clearInterval(timer); };
  }, []);
  useEffect(() => { document.title = `POSEIDON · ${t.title}`; }, [t.title]);
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve(); if (c.signal.aborted) return;
      setLoading(true); setError(0);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      setUser(await session(c.signal));
      if (!route.vessel) { setVesselOptions((await listVessels(0, '', c.signal)).items); setPage(null); return; }
      setPage(await assuranceApi.trust(route.vessel, route.offset, c.signal)); setLoadedAt(Date.now());
    })().then(() => { if (!c.signal.aborted) setLoading(false); }).catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e.status : 500); setLoading(false); });
    return () => c.abort();
  }, [route, reload]);
  const go = (vessel: string, offset = 0) => { history.pushState(null, '', routeHref(vessel, offset)); setRoute(dataTrustRoute(window.location.href)); };
  const stale = now - loadedAt > 15 * 60000;
  return <div className="product-shell enterprise-shell tug-workspace data-trust">
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={h => window.location.assign(h)} title={t.title} mainId="data-trust-main"/>
    <main id="data-trust-main" tabIndex={-1} aria-busy={loading} data-testid="data-trust">
      <div className="enterprise-page-heading"><div><h1>{t.title}</h1></div></div>
      <p className="tug-planning-boundary">{t.boundary}</p>
      {offline && <p role="alert" data-state="offline">{t.offline}</p>}
      {stale && !loading && page && <p role="status" data-state="stale">{t.stale} <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.reload}</Button></p>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[401, 403].includes(error) ? t.denied : error === 404 ? t.notFound : error === 400 ? t.invalidRoute : t.unavailable}</p>
        <Button type="button" variant="outline" disabled={offline} onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>}
      {!loading && !error && !route.vessel && <section aria-label={t.chooseVessel}><h2>{t.chooseVessel}</h2>
        {vesselOptions.length === 0 ? <p role="status" data-state="empty">{t.noVessels}</p> :
          <ul>{vesselOptions.map(v => <li key={v.id}><a href={routeHref(v.id)} onClick={e => { e.preventDefault(); go(v.id); }}>{v.code} · {v.name}</a></li>)}</ul>}</section>}
      {!loading && !error && page && <TrustView page={page} t={t} onPage={offset => go(route.vessel, offset)}/>}
    </main></div>;
}

function Signals({ item, t }: { item: TrustItem; t: Copy }) {
  if (item.signals.length === 0) return <Badge tone="neutral">{t.noSignal}</Badge>;
  return <ul className="data-trust-signals">{item.signals.map(s => <li key={s.kind + s.reason}><Badge tone="warning" data-kind={s.kind}>{label(t, 'kind_' + s.kind)}</Badge> <code>{s.reason}</code></li>)}</ul>;
}

function Rows({ items, t }: { items: TrustItem[]; t: Copy }) {
  return <table><thead><tr><th scope="col">{t.subject}</th><th scope="col">{t.provenance}</th><th scope="col">{t.freshness}</th><th scope="col">{t.verification}</th>
    <th scope="col">{t.signals}</th></tr></thead>
    <tbody>{items.map(i => <tr key={i.subjectKind + (i.subjectId ?? i.fieldPath)} data-disposition={i.disposition}>
      <td>{label(t, 'subject_' + i.subjectKind)} · {i.title ?? (i.fieldPath ? <code>{i.fieldPath}</code> : <bdi dir="ltr">{i.subjectId}</bdi>)} · r{i.revision}</td>
      <td>{i.provenance ?? t.notRecorded}{i.sourceKind && <> · <code>{i.sourceKind}</code></>}</td>
      <td><Badge tone={i.freshness === 'CURRENT' ? 'neutral' : 'warning'}>{label(t, 'fresh_' + i.freshness)}</Badge>
        {i.observedAt ? <> <time dateTime={i.observedAt}>{i.observedAt}</time></> : <> {t.notRecorded}</>}</td>
      <td><Badge tone={i.verification === 'VERIFIED' ? 'neutral' : 'warning'}>{label(t, 'ver_' + i.verification)}</Badge></td>
      <td><Signals item={i} t={t}/></td></tr>)}</tbody></table>;
}

export function TrustView({ page, t, onPage }: { page: TrustPage; t: Copy; onPage: (offset: number) => void }) {
  const review = page.disposition === 'REVIEW_REQUIRED';
  return <>
    <section aria-labelledby="data-trust-summary" data-disposition={page.disposition}><h2 id="data-trust-summary">{t.summary}</h2>
      <p><Badge tone={review ? 'warning' : 'neutral'}>{label(t, 'disposition_' + page.disposition)}</Badge> {review ? t.readinessFloor : t.noReadinessEffect}</p>
      {page.state === 'UNAVAILABLE' && <p role="alert">{t.unavailable}</p>}
      <ul aria-label={t.counts}>{signalKinds.map(k => <li key={k}>{label(t, 'kind_' + k)}: <strong>{page.counts[k.toLowerCase() as keyof typeof page.counts]}</strong></li>)}</ul>
      {page.withheld > 0 && <p role="note">{t.withheld.replace('{{count}}', String(page.withheld))}</p>}
      {page.truncated && <p role="note">{t.truncated}</p>}
    </section>
    <section aria-labelledby="data-trust-subjects"><h2 id="data-trust-subjects">{t.subjects}</h2>
      {page.items.length === 0 ? <p role="status" data-state="empty">{t.empty}</p> : <Rows items={page.items} t={t}/>}
      <p>{page.offset > 0 && <Button type="button" variant="outline" onClick={() => onPage(Math.max(0, page.offset - page.limit))}>{t.previous}</Button>}
        {page.hasMore && <Button type="button" variant="outline" onClick={() => onPage(page.offset + page.limit)}>{t.next}</Button>}</p>
    </section>
    <section aria-labelledby="data-trust-profile"><h2 id="data-trust-profile">{t.profileFields}</h2>
      {page.profileFields.length === 0 ? <p role="status" data-state="empty">{t.empty}</p> : <Rows items={page.profileFields} t={t}/>}
    </section>
  </>;
}
