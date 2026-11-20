import { useEffect, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { ApiError, session, type Session } from './api';
import { isGuid } from './identifiers';
import { highRiskTypes, towageApi, type HighRiskPage, type HighRiskRow } from './towage-api';
import './tug.css';

const copy = localizedCopy('HighRiskWork_tsx_copy');
type Copy = (typeof copy)[AppLocale];
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;

export function highRiskRoute(href: string) {
  const url = new URL(href, 'http://local'), p = url.searchParams;
  const value = (k: string) => p.get(k) ?? '';
  const offset = Number(value('offset') || '0');
  const invalid = !/^\/app\/high-risk-work\/?$/.test(url.pathname) || [...p.keys()].some(k => !['companyId', 'vesselId', 'workType', 'offset'].includes(k) || p.getAll(k).length !== 1) ||
    ['companyId', 'vesselId'].some(k => value(k) !== '' && !isGuid(value(k))) || value('workType') !== '' && !(highRiskTypes as readonly string[]).includes(value('workType')) ||
    !Number.isSafeInteger(offset) || offset < 0;
  return { companyId: value('companyId'), vesselId: value('vesselId'), workType: value('workType'), offset: invalid ? 0 : offset, invalid };
}
type Route = ReturnType<typeof highRiskRoute>;
const href = (r: Omit<Route, 'invalid'>) => {
  const q = new URLSearchParams();
  if (r.companyId) q.set('companyId', r.companyId); if (r.vesselId) q.set('vesselId', r.vesselId); if (r.workType) q.set('workType', r.workType);
  if (r.offset > 0) q.set('offset', String(r.offset));
  const s = q.toString(); return `/app/high-risk-work${s ? `?${s}` : ''}`;
};

// UNKNOWN / REVIEW_REQUIRED are grey-amber with their reason; nothing here is shown as a green "go".
function StateCell({ state, t }: { state: string; t: Copy }) {
  const tone = ['BLOCKED', 'FAILED', 'NOT_READY'].includes(state) ? 'danger' : ['READY', 'HEALTHY'].includes(state) ? 'info' : ['UNKNOWN'].includes(state) ? 'neutral' : 'warning';
  return <Badge tone={tone} data-state={state}>{label(t, 'st_' + state)}</Badge>;
}

export function HighRiskWorkWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => highRiskRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [page, setPage] = useState<HighRiskPage | null>(null), [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [offline, setOffline] = useState(!navigator.onLine), [open, setOpen] = useState<string>('');
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => setRoute(highRiskRoute(window.location.href));
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); };
  }, []);
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve();
      if (c.signal.aborted) return;
      setLoading(true); setError(0);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      setUser(await session(c.signal));
      setPage(await towageApi.highRisk({ companyId: route.companyId || undefined, vesselId: route.vesselId || undefined, workType: route.workType || undefined, offset: route.offset }, c.signal));
    })().then(() => { if (!c.signal.aborted) setLoading(false); }).catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e.status : 500); setLoading(false); });
    return () => c.abort();
  }, [route, reload]);
  const go = (next: Partial<Omit<Route, 'invalid'>>) => {
    const target = href({ ...route, offset: 0, ...next }); history.pushState(null, '', target); setRoute(highRiskRoute(window.location.href)); setOpen('');
  };
  const vessels = page?.vessels.filter(v => !route.companyId || v.companyId === route.companyId) ?? [];
  return <div className="product-shell enterprise-shell tug-workspace">
    <EnterpriseChrome session={user} vessel={route.vesselId || null} language={lang} onLanguageChange={language} onNavigate={h => window.location.assign(h)} title={t.title} mainId="high-risk-main"/>
    <main id="high-risk-main" tabIndex={-1} aria-busy={loading}>
      <div className="enterprise-page-heading"><div><h1>{t.title}</h1></div></div>
      <p className="tug-planning-boundary">{t.boundary}</p>
      {offline && <p role="alert">{t.offline} <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.reload}</Button></p>}
      <form aria-label={t.title} className="high-risk-filters" onSubmit={e => e.preventDefault()}>
        <label htmlFor="hrw-fleet">{t.fleet}</label><select id="hrw-fleet" value={route.companyId} disabled={loading} onChange={e => go({ companyId: e.target.value, vesselId: '' })}>
          <option value="">{t.all}</option>{page?.fleets.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select>
        <label htmlFor="hrw-vessel">{t.vessel}</label><select id="hrw-vessel" value={route.vesselId} disabled={loading} onChange={e => go({ vesselId: e.target.value })}>
          <option value="">{t.all}</option>{vessels.map(v => <option key={v.id} value={v.id}>{v.code} · {v.name}</option>)}</select>
        <label htmlFor="hrw-type">{t.workType}</label><select id="hrw-type" value={route.workType} disabled={loading} onChange={e => go({ workType: e.target.value })}>
          <option value="">{t.all}</option>{highRiskTypes.map(x => <option key={x} value={x}>{label(t, 'type_' + x)}</option>)}</select>
      </form>
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[401, 403].includes(error) ? t.denied : t.unavailable}</p><Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>}
      {!loading && !error && page && <>
        <p><small>{t.evaluatedAt}: <time dateTime={page.evaluatedAt}>{page.evaluatedAt}</time> · {t.coverage}: {page.coverage.map(c => <code key={c}>{c} </code>)}</small></p>
        {page.items.length === 0 ? <p role="status">{t.empty}</p> :
          <table className="high-risk-table"><thead><tr>{[t.colWork, t.colVessel, t.colType, t.colStart, t.colRisk, t.colPermit, t.colBarrier, t.colCrew, t.colEquipment, t.colNext, t.colOwner, t.why]
            .map(h => <th key={h} scope="col">{h}</th>)}</tr></thead>
            <tbody>{page.items.map(r => <Row key={`${r.workKind}:${r.workId}`} row={r} t={t} open={open === r.workId} toggle={() => setOpen(open === r.workId ? '' : r.workId)}/>)}</tbody></table>}
        <nav aria-label={t.title}>
          <Button type="button" variant="outline" disabled={route.offset === 0} onClick={() => go({ offset: Math.max(0, route.offset - page.limit) })}>{t.previous}</Button>
          <Button type="button" variant="outline" disabled={!page.hasMore} onClick={() => go({ offset: route.offset + page.limit })}>{t.next}</Button>
        </nav></>}
    </main></div>;
}

function Row({ row, t, open, toggle }: { row: HighRiskRow; t: Copy; open: boolean; toggle: () => void }) {
  return <>
    <tr data-critical={row.criticalBlockers > 0} data-work-kind={row.workKind}>
      <td><a href={row.link}>{row.workRef}</a>{row.criticalBlockers > 0 && <> <Badge tone="danger">{t.critical}: {row.criticalBlockers}</Badge></>}
        {row.reviewItems > 0 && <> <Badge tone="warning">{t.review}: {row.reviewItems}</Badge></>}</td>
      <td>{row.vesselCode}</td><td>{label(t, 'type_' + row.workType)}</td>
      <td>{row.plannedStart ? <time dateTime={row.plannedStart}>{row.plannedStart}</time> : t.notPlanned}</td>
      <td><StateCell state={row.riskState} t={t}/></td><td><StateCell state={row.permitState} t={t}/></td><td><StateCell state={row.criticalBarrierState} t={t}/></td>
      <td><StateCell state={row.crewReadiness} t={t}/></td><td><StateCell state={row.equipmentReadiness} t={t}/></td>
      <td data-next={row.nextAction.kind}>{label(t, 'next_' + row.nextAction.kind)}{row.nextAction.reason && <> <code>{row.nextAction.reason}</code></>}</td>
      <td>{row.ownerDue.ownerRole ? label(t, 'owner_' + row.ownerDue.ownerRole) : '—'} · {row.ownerDue.dueAt ? <time dateTime={row.ownerDue.dueAt}>{row.ownerDue.dueAt}</time> : t.dueUnresolved}</td>
      <td><Button type="button" variant="outline" aria-expanded={open} onClick={toggle}>{open ? t.hideWhy : t.why}</Button></td>
    </tr>
    {open && <tr><td colSpan={12}><table aria-label={`${t.why} ${row.workRef}`}><thead><tr><th scope="col">{t.dimension}</th><th scope="col">{t.stateLabel}</th><th scope="col">{t.reasonLabel}</th></tr></thead>
      <tbody>{row.why.map((d, i) => <tr key={`${d.dimension}-${d.reason}-${i}`}><td>{label(t, 'dim_' + d.dimension)}</td><td><StateCell state={d.state} t={t}/></td><td><code>{d.reason}</code></td></tr>)}</tbody></table>
      <p><a href={row.link}>{t.open}</a></p></td></tr>}
  </>;
}
