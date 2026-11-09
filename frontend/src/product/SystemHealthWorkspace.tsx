import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { Button } from '../components/ui/button';
import { Card } from '../components/ui/card';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterprisePageHeader, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import * as api from './api';
import { isGuid } from './identifiers';
import { loadSystemHealth, type SystemHealth } from './system-health-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { decodeTodayVessels } from './today-api';
import './offline-enterprise.css';
import './product.css';
function selection() {
    const query = new URLSearchParams(window.location.search), values = query.getAll('vessel');
    if ([...query.keys()].some(key => key !== 'vessel') || values.length > 1 || values.length === 1 && !isGuid(values[0]))
        return { invalid: true, vessel: '' };
    return { invalid: false, vessel: values[0]?.toLowerCase() ?? '' };
}
export function SystemHealthWorkspace() {
    const [language, setLanguage] = useAppLanguage(), en = language === 'en';
    const [route] = useState(selection), [vessel, setVessel] = useState(route.vessel), [offset, setOffset] = useState(0), [attempt, retry] = useState(0);
    const epoch = useWorkspaceRevalidation();
    const [identity, setIdentity] = useState<{
        epoch: number;
        attempt: number;
        user?: api.Session;
        error?: boolean;
    } | null>(null);
    const [page, setPage] = useState<{
        epoch: number;
        attempt: number;
        offset: number;
        rows?: api.Page<api.Vessel>;
        error?: boolean;
    } | null>(null);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden';
    const currentIdentity = identity?.epoch === epoch && identity.attempt === attempt ? identity : null;
    const user = currentIdentity?.user ?? null;
    const currentPage = page?.epoch === epoch && page.attempt === attempt && page.offset === offset ? page : null;
    useEffect(() => {
        if (route.invalid || !online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        api.session(c.signal).then(user => { if (active)
            setIdentity({ epoch, attempt, user }); }).catch(() => { if (active)
            setIdentity({ epoch, attempt, error: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [route.invalid, epoch, attempt, online, visible]);
    useEffect(() => {
        if (!user?.authenticated || !user.canRead || !online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        api.vessels(offset, '', c.signal).then(rows => decodeTodayVessels(rows, offset)).then(rows => { if (active)
            setPage({ epoch, attempt, offset, rows }); }).catch(() => { if (active)
            setPage({ epoch, attempt, offset, error: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [user, epoch, attempt, offset, online, visible]);
    const title = localizedText(en, "pairs.m_4e40e458d82d0a06");
    useEffect(() => { document.title = `POSEIDON · ${title}`; }, [title, language]);
    return <div className="product-shell enterprise-shell"><EnterpriseChrome session={user} vessel={vessel || null} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => { committed?.(); window.location.assign(href); }} title={title} mainId="health-main"/>
    <main id="health-main" tabIndex={-1}><EnterprisePageHeader title={title} eyebrow={localizedText(en, "pairs.m_65573df431fdbeba")} description={localizedText(en, "pairs.m_1870cb83b9ffd55f")} actions={<Button variant="outline" disabled={!online} onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_707ce97fea8c0150")}</Button>}/>
      {route.invalid ? <p role="alert">{localizedText(en, "pairs.m_1fc8daca0d6aa881")}</p> : !online ? <p role="status">{localizedText(en, "pairs.m_9c0377f404da833f")}</p> : !visible ? <p role="status">{localizedText(en, "pairs.m_7d13b28eba99609f")}</p> : !user ? <p role={currentIdentity?.error ? 'alert' : 'status'}>{currentIdentity?.error ? (localizedText(en, "pairs.m_d193a7c3b108e061")) : (localizedText(en, "pairs.m_fd79802c7b54938a"))}</p> : !user.authenticated || !user.canRead ? <p role="alert">{localizedText(en, "pairs.m_8af78b9d88834709")}</p> : <>
        <EnterpriseToolbar><label>{localizedText(en, "pairs.m_558f2674cb9f682e")}<select value={vessel} onChange={event => { const id = event.target.value; setVessel(id); history.replaceState(null, '', `/app/system-health${id ? '?vessel=' + id : ''}`); }}><option value="">{localizedText(en, "pairs.m_6bf1a4e450734ac1")}</option>{vessel && !currentPage?.rows?.items.some(row => row.id === vessel) && <option value={vessel}>{vessel}</option>}{currentPage?.rows?.items.map(row => <option key={row.id} value={row.id}>{row.code} · {row.name}</option>)}</select></label></EnterpriseToolbar>
        {currentPage?.error && <p role="alert">{localizedText(en, "pairs.m_e0b902838820e1d9")}</p>}
        {currentPage?.rows && <EnterprisePagination label={localizedText(en, "pairs.m_86c9cba3060ef9f4")} previous={localizedText(en, "pairs.m_3ebc6aea5e3f139e")} next={localizedText(en, "pairs.m_1062ab19d3959cdd")} previousDisabled={!offset} nextDisabled={!currentPage.rows.hasMore || offset >= 100000} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/>}
        {vessel ? <HealthSnapshot key={`${user.tenantId}:${user.actorId}:${vessel}:${epoch}:${attempt}`} vessel={vessel} language={language}/> : <p role="status">{localizedText(en, "pairs.m_a558c33c8fd047aa")}</p>}
      </>}
    </main></div>;
}
function HealthSnapshot({ vessel, language }: {
    vessel: string;
    language: AppLocale;
}) {
    const en = language === 'en', [result, setResult] = useState<{
        data?: SystemHealth;
        error?: number;
    } | null>(null);
    useEffect(() => {
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        loadSystemHealth(vessel, c.signal).then(data => { if (active)
            setResult({ data }); }).catch(error => { if (active)
            setResult({ error: error instanceof api.ApiError ? error.status : 0 }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [vessel]);
    if (!result)
        return <p role="status">{localizedText(en, "pairs.m_b0bf6990a15178cd")}</p>;
    if (!result.data)
        return <p role="alert">{[401, 403, 404].includes(result.error ?? 0) ? (localizedText(en, "pairs.m_e17715a04d098321")) : (localizedText(en, "pairs.m_4f4cbc436901d390"))}</p>;
    const data = result.data, unknown = localizedText(en, "pairs.m_49138f2831bf4262"), number = (value: number | null) => value === null ? unknown : new Intl.NumberFormat(language, { maximumFractionDigits: 3 }).format(value);
    return <section aria-label={localizedText(en, "pairs.m_7f4f2ed6d01ca685")}><p>{localizedText(en, "pairs.m_49b1431c59b5cdfd")}: <time dateTime={data.readAt}>{data.readAt}</time></p><div className="health-observation-grid">
    <Card><h2>{localizedText(en, "pairs.m_4e835cca4a574454")}</h2><p>{localizedText(en, "pairs.m_4b0406792f2ebb98")}</p><dl><dt>{localizedText(en, "pairs.m_6333a772dad696bb")}</dt><dd>{number(data.api.requests)}</dd><dt>{localizedText(en, "pairs.m_802b2eb1d78ce6c8")}</dt><dd>{number(data.api.errors)}</dd><dt>{localizedText(en, "pairs.m_b9bb7b900e01b2b5")}</dt><dd>{number(data.api.averageLatencyMs)}</dd></dl></Card>
    <Card><h2>{localizedText(en, "pairs.m_703e106740210efa")}</h2><p>{localizedText(en, "pairs.m_4b0406792f2ebb98")}</p><dl><dt>{localizedText(en, "pairs.m_ac7c66b5f6d69401")}</dt><dd>{number(data.audit.persisted)}</dd><dt>{localizedText(en, "pairs.m_6c48bc2696e63fab")}</dt><dd>{number(data.audit.unavailable)}</dd><dt>{localizedText(en, "pairs.m_b9bb7b900e01b2b5")}</dt><dd>{number(data.audit.averageLatencyMs)}</dd></dl></Card>
    <Card><h2>{localizedText(en, "pairs.m_d5b1251f1235a186")}</h2><p>{localizedText(en, "pairs.m_bf7595a5d9ae3c10")}</p><dl><dt>{localizedText(en, "pairs.m_e0a1492c7fc3092b")}</dt><dd>{number(data.sync.committed)}</dd><dt>{localizedText(en, "pairs.m_36764ce358d73457")}</dt><dd>{number(data.sync.unresolvedConflicts)}</dd><dt>{localizedText(en, "pairs.m_b7692b58ee122093")}</dt><dd>{unknown}</dd></dl></Card>
    <Card><h2>{localizedText(en, "pairs.m_82bf2623c669ec68")}</h2><p>{localizedText(en, "pairs.m_0a9a9fc0151ccf83")}</p><dl><dt>{localizedText(en, "pairs.m_74f30816b309c257")}</dt><dd>{data.files.adapter === 'Unavailable' ? unknown : data.files.adapter}</dd><dt>{localizedText(en, "pairs.m_60f28cada3892ea3")}</dt><dd>{data.files.state === 'ConfiguredUnprobed' ? (localizedText(en, "pairs.m_4fa0648c24a4530a")) : unknown}</dd><dt>{localizedText(en, "pairs.m_07d03a88278a463d")}</dt><dd>{data.files.scanState === 'ConfiguredUnprobed' ? (localizedText(en, "pairs.m_4fa0648c24a4530a")) : unknown}</dd></dl></Card>
    <Card><h2>{localizedText(en, "pairs.m_b80dfb7247613250")}</h2><p>{localizedText(en, "pairs.m_66a73f8e5625ca28")}</p><dl>{(['current', 'stale', 'future', 'unknown', 'unavailable'] as const).map((key, index) => <div key={key}><dt>{(en ? ['Current', 'Stale', 'Future dated', 'Unknown', 'Unavailable'] : ['Güncel', 'Eski', 'İleri tarihli', 'Bilinmiyor', 'Kullanılamıyor'])[index]}</dt><dd>{number(data.freshness[key])}</dd></div>)}</dl></Card>
  </div></section>;
}
