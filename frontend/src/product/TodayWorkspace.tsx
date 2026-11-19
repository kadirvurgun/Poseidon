import { localizedCopy, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useRef, useState } from 'react';
import * as api from './api';
import * as service from './today-api';
import { loginHref } from './workspace-location';
import { dueReason, dueState } from './maintenance-copy';
import { todayTugCopy } from './today-tug-copy';
import { TodayTugExplanation } from './TodayTugExplanation';
import { Anchor, ArrowLeft, ArrowUpRight, CircleHelp, Info, RefreshCw, Ship, TriangleAlert } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { Card } from '../components/ui/card';
import { TodayOverview } from './TodayOverview';
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns';
const copy = localizedCopy("TodayWorkspace_tsx_copy");
type Load<T> = {
    state: 'loading';
} | {
    state: 'ok';
    data: T;
} | {
    state: 'error';
    status: number;
    contract?: boolean;
};
const status = (e: unknown) => e instanceof api.ApiError ? e.status : 0;
// A client decoder failure is a contract-validation failure, never an HTTP 502 response.
const failure = (e: unknown) => ({ state: 'error' as const, status: status(e), contract: e instanceof service.TodayContractError });
const focusError = (node: HTMLDivElement | null) => { node?.focus(); };
type Props = {
    id: string | null;
    language: AppLocale;
    offline: boolean;
    onNavigate: (href: string) => void;
};
// A changed vessel must never render or reuse the previous vessel's snapshot/disclosure.
export function TodayWorkspace(props: Props) {
    const id = props.id?.toLowerCase() ?? null;
    return <TodayContext key={id ?? 'select'} {...props} id={id}/>;
}
function TodayContext({ id, language, offline, onNavigate }: Props) {
    const t = { ...copy[language], ...todayTugCopy[language] };
    const [data, setData] = useState<Load<service.Today>>({ state: 'loading' });
    const [vessels, setVessels] = useState<Load<api.Page<api.Vessel>>>({ state: 'loading' });
    const [offset, setOffset] = useState(0);
    const [attempt, setAttempt] = useState(0);
    const [whyAttempt, setWhyAttempt] = useState(0);
    const [expanded, setExpanded] = useState<string | null>(null);
    const [cardOffset, setCardOffset] = useState(0);
    const [why, setWhy] = useState<Load<service.Why>>({ state: 'loading' });
    const disclosureButton = useRef<HTMLButtonElement>(null);
    const [previousOffline, setPreviousOffline] = useState(offline);
    // Invalidate the disclosure before rendering a changed connection state. Reconnecting
    // starts a new read; an offline snapshot is never promoted to a current observation.
    if (previousOffline !== offline) {
        setPreviousOffline(offline);
        setExpanded(null);
        setWhy({ state: 'loading' });
        if (!offline) {
            setData({ state: 'loading' });
            setVessels({ state: 'loading' });
        }
    }
    useEffect(() => {
        if (offline)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        const promise = id ? service.today(id, c.signal, cardOffset).then(data => { if (active)
            setData({ state: 'ok', data }); })
            : service.todayVessels(offset, c.signal).then(data => { if (active)
                setVessels({ state: 'ok', data }); });
        promise.catch(e => { if (active) {
            if (id)
                setData(failure(e));
            else
                setVessels(failure(e));
        } });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [id, attempt, offset, cardOffset, offline]);
    useEffect(() => {
        if (!id || !expanded || offline)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        service.why(id, c.signal, expanded).then(data => { if (active)
            setWhy({ state: 'ok', data }); }).catch(e => {
            if (!active)
                return;
            const code = status(e);
            setWhy(failure(e));
            // A new denial invalidates the whole previously authorized snapshot.
            if ([401, 403, 404].includes(code)) {
                setData({ state: 'error', status: code });
                setExpanded(null);
            }
        });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [id, expanded, whyAttempt, offline]);
    const refresh = () => { setCardOffset(0); setExpanded(null); setWhy({ state: 'loading' }); setData({ state: 'loading' }); setVessels({ state: 'loading' }); setAttempt(v => v + 1); };
    if (!id)
        return <section aria-label={t.select}><h2>{t.select}</h2>{vessels.state === 'loading' ? <p role="status">{offline ? t.offlineEmpty : t.loading}</p> : vessels.state === 'error' ? <TodayError code={vessels.status} contract={vessels.contract} retry={refresh} offline={offline} t={t}/> : <>{offline && <p role="status">{t.offline}</p>}{vessels.data.items.length === 0 && <p>{t.empty}</p>}
    <div className="enterprise-vessel-grid">{vessels.data.items.map(v => {
      const Icon = v.kind === 'Tug' ? Anchor : Ship;
      const identity = `${v.code} · ${v.name}`;
      return <Card className="enterprise-vessel-card" key={v.id} role="group" aria-label={identity}>
        <div className="enterprise-vessel-card-header">
          <span className="enterprise-vessel-icon"><Icon aria-hidden="true"/></span>
          <div className="enterprise-vessel-card-identity">
            <h3 dir="auto" title={v.code}>{v.code}</h3>
            <p dir="auto" title={v.name}>{v.name}</p>
          </div>
        </div>
        <Button className="enterprise-vessel-card-action" variant="outline" size="sm" aria-label={identity} onClick={() => onNavigate(`/app/today/${v.id}`)}>
          <span>{localizedText(language, "pairs.m_dc42cf81fa770206")}</span><ArrowUpRight aria-hidden="true"/>
        </Button>
      </Card>;
    })}</div>
    <EnterpriseActions align="between" className="enterprise-vessel-pagination">
      <Button variant="ghost" disabled={offline} onClick={refresh}><RefreshCw aria-hidden="true"/>{t.refresh}</Button>
      <EnterprisePagination label={t.select} previous={t.previous} next={t.next} previousDisabled={!offset || offline} nextDisabled={!vessels.data.hasMore || offline || offset >= 100000} onPrevious={() => { setVessels({ state: 'loading' }); setOffset(v => Math.max(0, v - 25)); }} onNext={() => { setVessels({ state: 'loading' }); setOffset(v => v + 25); }}/>
    </EnterpriseActions></>}</section>;
    return <section aria-label={localizedText(language, "pairs.m_14d5741b2e6e2585")} aria-busy={!offline && data.state === 'loading'} data-today-state={offline ? 'offline' : data.state}><EnterpriseActions align="start" className="today-card-toolbar" aria-label={localizedText(language, "pairs.m_76ee6d9d280d1972")}><Button variant="outline" disabled={offline || data.state === 'loading'} onClick={refresh}><RefreshCw aria-hidden="true"/>{t.refresh}</Button><Button variant="ghost" onClick={() => onNavigate('/app/today')}><ArrowLeft data-inline-navigation-icon aria-hidden="true"/>{t.switch}</Button></EnterpriseActions>
    {data.state === 'loading' ? <p role="status">{offline ? t.offlineEmpty : t.loading}</p> : data.state === 'error' ? <TodayError code={data.status} contract={data.contract} retry={refresh} offline={offline} t={t}/> : <>
      <h2>{t.context}: {data.data.vessel.kind === 'Tug' ? (localizedText(language, "pairs.m_306aaabcb4e04a9f")) : (localizedText(language, "pairs.m_bb1dbfbac5d5c650"))} · {data.data.vessel.code} · {data.data.vessel.name}</h2>
      <p role="status">{offline ? t.offline : t.stale}</p><p>{t.read}: <time dateTime={data.data.readAt}>{data.data.readAt}</time></p>
      <TodayViewStateNotice state={data.data.viewState} t={t}/>
      <TodayOverview data={data.data} language={language}/>
      <h3>{t.coverage}</h3><ul className="today-source-coverage">{data.data.coverage.map(c => <li key={c.source}>{t[c.source as keyof typeof t]}: {t[c.status as keyof typeof t]}</li>)}</ul>
      {data.data.cards.items.length === 0 && <p>{t.empty}</p>}
      {data.data.cards.items.map(card => {
                const open = expanded === card.key;
                const disclosureId = card.key === 'regulatory-draft' ? 'today-why' : `today-why-${card.key.replace(/[^a-z0-9-]/g, '-')}`;
                const PriorityIcon = card.priority === 'Attention' ? TriangleAlert : card.priority === 'Review' ? CircleHelp : Info;
                return <article key={card.key} className="product-detail today-feed-card" data-priority={card.priority} aria-label={card.source === 'TugJob' ? `${t.TugJob} ${card.key.slice('tug-job:'.length)}` : `${t[card.priority as keyof typeof t]} · ${t[card.source as keyof typeof t]}`}><div className="today-feed-card-header"><h3>{t[card.priority as keyof typeof t]} · {t[card.source as keyof typeof t]}</h3><Badge tone={card.priority === 'Attention' ? 'warning' : card.priority === 'Review' ? 'neutral' : 'info'}><PriorityIcon aria-hidden="true"/>{t[card.priority as keyof typeof t]}</Badge></div>
          {card.source === 'TugJob' && <p>{t.jobReference}: {card.key.slice('tug-job:'.length)}</p>}
          <p>{t[card.reason as keyof typeof t]}</p>
          <TodayCardSource source={card.sourceRef} language={language} t={t}/>
          <p>{card.owner ? `${t.ownerRole}: ${card.owner}` : t.owner}</p>
          <div className="today-feed-card-actions"><Button variant="outline" onClick={() => onNavigate(card.href)}>{t[card.nextAction as keyof typeof t]}<ArrowUpRight aria-hidden="true"/></Button>
          <Button variant="ghost" aria-expanded={open} aria-controls={disclosureId} disabled={offline} onClick={event => {
                        disclosureButton.current = event.currentTarget;
                        setWhy({ state: 'loading' });
                        setExpanded(open ? null : card.key);
                    }}>{open ? t.close : t.why}</Button></div>
          {open && !offline && <section id={disclosureId} aria-label={t.why} aria-live="polite">{why.state === 'loading' ? <p role="status">{t.loading}</p> : why.state === 'error' ? <TodayError code={why.status} contract={why.contract} retry={() => { disclosureButton.current?.focus(); setWhy({ state: 'loading' }); setWhyAttempt(v => v + 1); }} offline={offline} t={t}/> : <TodayExplanation why={why.data} language={language} t={t}/>}</section>}
        </article>;
            })}
      <EnterprisePagination label={localizedText(language, "pairs.m_790248ce9984ffa4")} previous={t.previousCards} next={t.nextCards} previousDisabled={offline || cardOffset === 0} nextDisabled={offline || !data.data.cards.hasMore || cardOffset >= 100000} onPrevious={() => { setExpanded(null); setWhy({ state: 'loading' }); setData({ state: 'loading' }); setCardOffset(v => Math.max(0, v - 25)); }} onNext={() => { setExpanded(null); setWhy({ state: 'loading' }); setData({ state: 'loading' }); setCardOffset(v => v + 25); }}/>
    </>}
  </section>;
}
function TodayError({ code, contract, retry, offline, t }: {
    code: number;
    contract?: boolean;
    retry: () => void;
    offline: boolean;
    t: Record<keyof typeof copy.en, string>;
}) {
    // Deterministic status → message mapping (61.03 §33). 0 = network/timeout abort. Only a client
    // decoder failure is a validation failure; an HTTP 502/503/504 response is "source unavailable".
    const message = contract ? t.contract : code === 401 ? t.login : code === 403 ? t.denied : code === 404 ? t.missing : code === 429 ? t.rateLimited : t.error;
    return <div role="alert" tabIndex={-1} ref={focusError} data-error-status={code} data-error-kind={contract ? 'contract' : code === 0 ? 'network' : 'http'}><p>{message}</p>{code === 401 ? <a href={loginHref()}>{t.login}</a> : <button disabled={offline} onClick={retry}>{t.retry}</button>}</div>;
}
// DEV-023: the composition state is shown as text + icon; only Ready omits the "not all clear" caveat
// and even Ready never states readiness or verification.
function TodayViewStateNotice({ state, t }: {
    state: service.TodayViewState;
    t: Record<keyof typeof copy.en, string>;
}) {
    const Icon = state === 'Ready' ? Info : TriangleAlert;
    const text = state === 'Ready' ? t.viewReady : state === 'Empty' ? t.viewEmpty : state === 'Partial' ? t.viewPartial : t.viewUnavailable;
    return <p className="today-view-state" data-view-state={state}><Icon aria-hidden="true"/>{text}</p>;
}
function TodayCardSource({ source, language, t }: {
    source: service.TodaySourceRef;
    language: AppLocale;
    t: Record<keyof typeof copy.en, string>;
}) {
    return <dl className="today-card-source" aria-label={localizedText(language, "pairs.m_10c331bd1a515ec2")}>
      <div><dt>{t.sourceOwner}</dt><dd>{t[source.owner]}</dd></div>
      <div><dt>{localizedText(language, "pairs.m_2e37798ea762793a")}</dt><dd>{t[source.kind as keyof typeof t]} · {source.id}</dd></div>
      <div><dt>{localizedText(language, "pairs.m_291f25245b3342d1")}</dt><dd>{source.revision ?? t.noRevision}</dd></div>
      <div><dt>{t.sourceSaved}</dt><dd>{source.savedAt ? <time dateTime={source.savedAt}>{source.savedAt}</time> : t.noRevision}</dd></div>
    </dl>;
}
function TodayExplanation({ why, language, t }: {
    why: service.Why;
    language: AppLocale;
    t: Record<keyof typeof copy.en, string>;
}) {
    return <><p>{t[why.reason as keyof typeof t]}</p><p>{t.rule}: {why.rule}</p>
    {why.source && <dl className="today-why-lineage" aria-label={localizedText(language, "pairs.m_b8ae2d08ef1646de")}><div><dt>{localizedText(language, "pairs.m_10c331bd1a515ec2")}</dt><dd>{t[why.source.kind as keyof typeof t]}</dd></div><div><dt>{localizedText(language, "pairs.m_2e37798ea762793a")}</dt><dd>{why.source.id}</dd></div><div><dt>{localizedText(language, "pairs.m_291f25245b3342d1")}</dt><dd>{why.source.revision ?? t.unknown}</dd></div></dl>}
    <p>{t.read}: <time dateTime={why.readAt}>{why.readAt}</time></p><p>{t.currentOwner}: {why.owner ?? t.unknown}</p>
    {why.inputs && <p>{t.counts}: {why.inputs.known} / {why.inputs.missing} / {why.inputs.unknown}</p>}
    {why.tug && <TodayTugExplanation detail={why.tug} language={language}/>}
    {why.maintenance && <><p>{dueState(why.maintenance.calculation.state, language === 'en')} · {dueReason(why.maintenance.calculation.reason, language === 'en')}</p>
      <p>{t.lineage}: {why.maintenance.planRevision} / {why.maintenance.assetRevision}</p><p>{t.currentAsset}: {why.maintenance.currentAssetRevision ?? t.unknown}</p>
      <p>{t.planReference}: {why.maintenance.planId}</p><p>{t.assetReference}: {why.maintenance.assetId}</p>
      <p>{t.algorithm}: {why.maintenance.calculation.algorithm}</p><p>{t.observationAssessment}: {why.maintenance.observationState === 'NotRequested' ? t.noObservation : dueReason(why.maintenance.observationState, language === 'en')}</p>
      <p>{t.boundary}: {why.maintenance.calculation.nextDueDate ?? t.unknown} / {why.maintenance.calculation.nextDueMeter ?? t.unknown}</p></>}
    <p>{t.provenance}: {why.provenance}</p><p>{t.saved}: {why.savedAt ?? t.unknown}</p><p>{t.observed}: {why.sourceObservedAt ?? t.unknown}</p><p>{t.freshness}</p>
    <p>{t.blockers}</p><ul>{why.blockers.map(b => <li key={b}>{t[b as keyof typeof t]}</li>)}</ul></>;
}
