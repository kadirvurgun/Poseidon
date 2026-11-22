import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { TugAuditRevision } from './TugAuditRevision';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterpriseFormGrid, EnterprisePagination, EnterpriseSectionHeader } from './EnterprisePatterns';
import { ExternalLink, Filter, RotateCcw, X } from 'lucide-react';
import './audit-enterprise.css';
import { useEffect, useState } from 'react';
import { ApiError } from './api';
import { auditDetail, auditPage, auditTypes, changes } from './audit-api';
import type { AuditPage, AuditRecord } from './audit-api';
type Load<T> = {
    state: 'loading';
} | {
    state: 'ok';
    data: T;
} | {
    state: 'error';
    status: number;
};
const reasons: Record<string, readonly [
    string,
    string
]> = {
    PREPARATION_COMMAND: [sourceText("tr", "pairs.m_e8382c108ebe19cf"), sourceText("en", "pairs.m_e8382c108ebe19cf")],
    FILE_TRANSPORT: [sourceText("tr", "pairs.m_3e6e66db2df245fd"), sourceText("en", "pairs.m_3e6e66db2df245fd")],
    OFFLINE_LIFECYCLE: [sourceText("tr", "pairs.m_09526a54f3e1d59a"), sourceText("en", "pairs.m_09526a54f3e1d59a")],
    REGULATORY_WORKFLOW: [sourceText("tr", "pairs.m_f17fadad45b6d9c3"), sourceText("en", "pairs.m_f17fadad45b6d9c3")],
    HUMAN_REVIEW_WORKFLOW: [sourceText("tr", "pairs.m_318ed74fe7335341"), sourceText("en", "pairs.m_318ed74fe7335341")],
    SOURCE_QUALIFICATION: [sourceText("tr", "pairs.m_69a95f5785d1fe05"), sourceText("en", "pairs.m_69a95f5785d1fe05")],
};
const actionKeys: Record<string, string> = { Added: "pairs.m_933d201dceb0376a", Modified: "pairs.m_1cb814c6f2835d9d", Deleted: "pairs.m_16978605d10ea04d" };
/** Server write-audit enum (Added/Modified/Deleted) shown with the same localized labels as the action filter. */
function auditActionText(action: string, language: AppLocale | boolean) {
    const key = actionKeys[action];
    return key ? localizedText(language, key) : action;
}
function reasonText(row: AuditRecord, en: boolean) {
    if (row.reasonStatus === 'SystemOperation')
        return localizedText(en, "pairs.m_b09e5aa499638cdd");
    if (row.reasonStatus === 'RecordedProtected')
        return localizedText(en, "pairs.m_4ad59cfbaa13bf64");
    if (row.reasonStatus === 'RecordedControlledCode') {
        const value = changes(row.changeSummary).find(x => x.field === 'TechnicalReasonCode')?.after;
        return value && reasons[value] ? localizedTuple(reasons[value], en, 1) : localizedText(en, "pairs.m_7c5301f9e1891eed");
    }
    return localizedText(en, "pairs.m_7c5301f9e1891eed");
}
export function AuditWorkspace({ language, selected, query = '', allowed, onNavigate }: {
    language: AppLocale;
    selected: string | null;
    query?: string;
    allowed: boolean;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    const [page, setPage] = useState<Load<AuditPage>>({ state: 'loading' });
    const [detail, setDetail] = useState<Load<AuditRecord>>({ state: 'loading' });
    const [attempt, setAttempt] = useState(0);
    useEffect(() => {
        if (!allowed)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        const load = async () => {
            setPage({ state: 'loading' });
            setDetail({ state: 'loading' });
            try {
                const data = await auditPage(query, c.signal);
                if (!active)
                    return;
                if (selected) {
                    const record = await auditDetail(selected, c.signal);
                    if (!active)
                        return;
                    setDetail({ state: 'ok', data: record });
                }
                setPage({ state: 'ok', data });
            }
            catch (error) {
                if (active) {
                    const status = error instanceof ApiError ? error.status : 0;
                    setPage({ state: 'error', status });
                    setDetail({ state: 'error', status });
                }
            }
        };
        void load();
        return () => { active = false; clearTimeout(timer); c.abort(); };
    }, [allowed, query, selected, attempt]);
    const href = (id: string | null, value = query) => `/app/audit${id ? `/${id}` : ''}${value ? `?${value}` : ''}`;
    if (!allowed)
        return <p role="alert">{localizedText(en, "pairs.m_15521da446dac71b")}</p>;
    return <section className="audit-workspace" aria-label={localizedText(en, "pairs.m_9f8662ba28b5b4fc")}>
    <p>{localizedText(en, "pairs.m_eee03f844cd0f235")}</p>
    <form key={query} className="audit-filters" onSubmit={e => {
            e.preventDefault();
            const data = new FormData(e.currentTarget);
            const next = new URLSearchParams();
            for (const key of ['vesselId', 'resourceId', 'entityType', 'action']) {
                const value = String(data.get(key) ?? '').trim();
                if (value)
                    next.set(key, value);
            }
            onNavigate(href(null, next.toString()));
        }}>
      <EnterpriseFormGrid><legend>{localizedText(en, "pairs.m_9f8662ba28b5b4fc")}</legend>
      <label>{localizedText(en, "pairs.m_c1ad7a5b833649d6")}<input name="vesselId" maxLength={36} defaultValue={new URLSearchParams(query).get('vesselId') ?? ''}/></label>
      <label>{localizedText(en, "pairs.m_8612eac9020efdab")}<input name="resourceId" maxLength={36} defaultValue={new URLSearchParams(query).get('resourceId') ?? ''}/></label>
      <label>{localizedText(en, "pairs.m_f0bff6f4be8b363a")}<select name="entityType" defaultValue={new URLSearchParams(query).get('entityType') ?? ''}><option value="">{localizedText(en, "pairs.m_75bae93ce2976538")}</option>{auditTypes.map(x => <option key={x}>{x}</option>)}</select></label>
      <label>{localizedText(en, "pairs.m_d3e6f26569c924d2")}<select name="action" defaultValue={new URLSearchParams(query).get('action') ?? ''}><option value="">{localizedText(en, "pairs.m_75bae93ce2976538")}</option><option value="Added">{localizedText(en, "pairs.m_933d201dceb0376a")}</option><option value="Modified">{localizedText(en, "pairs.m_1cb814c6f2835d9d")}</option><option value="Deleted">{localizedText(en, "pairs.m_16978605d10ea04d")}</option></select></label>
      </EnterpriseFormGrid>
      <EnterpriseActions aria-label={localizedText(en, "pairs.m_f093245ea4c21985")}><Button type="submit"><Filter aria-hidden="true"/>{localizedText(en, "pairs.m_f093245ea4c21985")}</Button><Button variant="outline" type="button" onClick={() => onNavigate('/app/audit')}><RotateCcw aria-hidden="true"/>{localizedText(en, "pairs.m_82b8bb60a3e95b62")}</Button></EnterpriseActions>
    </form>
    {page.state === 'loading' && <p role="status">{localizedText(en, "pairs.m_9e04b8b6eafff876")}</p>}
    {page.state === 'error' && <div role="alert"><p>{page.status === 403 ? (localizedText(en, "pairs.m_15521da446dac71b")) : page.status === 401 ? (localizedText(en, "pairs.m_a5811cfb5b422a85")) : page.status === 404 ? (localizedText(en, "pairs.m_a7941e4ded9276d0")) : (localizedText(en, "pairs.m_2d0cefd2d465cddd"))}</p><Button variant="outline" onClick={() => setAttempt(x => x + 1)}><RotateCcw aria-hidden="true"/>{localizedText(en, "pairs.m_044bf27da41fda96")}</Button></div>}
    {page.state === 'ok' && <><p>{localizedText(en, "pairs.m_f84203f3d0dbb0a4")}: {page.data.total}</p>
      {!page.data.items.length ? <p>{localizedText(en, "pairs.m_b6bca76490f43354")}</p> : <div className="product-table"><table><thead><tr><th scope="col">{localizedText(en, "pairs.m_c4caad84b0f6020d")}</th><th scope="col">{localizedText(en, "pairs.m_d0c23c0c7de86db7")}</th><th scope="col">{localizedText(en, "pairs.m_d3e6f26569c924d2")}</th></tr></thead><tbody>{page.data.items.map(row => <tr key={row.id}><td><time dateTime={row.occurredAt}>{row.occurredAt}</time></td><td><Button variant="link" className="audit-record-link" onClick={() => onNavigate(href(row.id))}>{row.entityType} · {row.resourceId ?? (localizedText(en, "pairs.m_28ccb09d3de4eb4e"))}</Button></td><td>{auditActionText(row.action, language)}</td></tr>)}</tbody></table></div>}
      <EnterprisePagination label={localizedText(en, "pairs.m_9f8662ba28b5b4fc")} previous={localizedText(en, "pairs.m_85b1371021e15e99")} next={localizedText(en, "pairs.m_93a493ffda9a1959")} previousDisabled={!new URLSearchParams(query).has('cursor')} nextDisabled={!page.data.nextCursor} onPrevious={() => { const q = new URLSearchParams(query); q.delete('cursor'); onNavigate(href(null, q.toString())); }} onNext={() => { const q = new URLSearchParams(query); q.set('cursor', page.data.nextCursor!); onNavigate(href(null, q.toString())); }}/></>}
    {selected && detail.state === 'ok' && page.state === 'ok' && <section className="product-detail" aria-label={localizedText(en, "pairs.m_2c31f8cf42dde138")}><EnterpriseSectionHeader title={localizedText(en, "pairs.m_2c31f8cf42dde138")} actions={<Button variant="outline" onClick={() => onNavigate(href(null))}><X aria-hidden="true"/>{localizedText(en, "pairs.m_99627faa6a096fa8")}</Button>}/><dl className="enterprise-record-summary">
      <div><dt>{localizedText(en, "pairs.m_ec4066bcd7e4ac55")}</dt><dd>{detail.data.actorReference ?? (localizedText(en, "pairs.m_cd6839b0e3657d73"))}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_40fe6e6c772556b6")}</dt><dd>{detail.data.roleContext ?? (localizedText(en, "pairs.m_4692577f9f59af00"))}</dd></div>
      <div className="audit-capability-summary"><dt>{localizedText(en, "pairs.m_27e1da592872c935")}</dt><dd>{detail.data.capabilityContext ?? (localizedText(en, "pairs.m_ffe74bc6484c16db"))}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_540d9fd15a75d9b6")}</dt><dd>{reasonText(detail.data, en)}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_07af3649fbc3d286")}</dt><dd>{detail.data.metadataStatus === 'Recorded' ? (localizedText(en, "pairs.m_79bfb63c6d5d8a16")) : (localizedText(en, "pairs.m_cc15d45c5d8bffe2"))}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_565c33d809d6fb66")}</dt><dd>{detail.data.beforeVersion ?? '—'} → {detail.data.afterVersion ?? (localizedText(en, "pairs.m_cd6839b0e3657d73"))}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_ee30ffc3e05c06ea")}</dt><dd>{detail.data.correlationId ?? '—'}</dd></div>
    </dl>{detail.data.changeSummary === null ? <p>{localizedText(en, "pairs.m_b9974a091846907a")}</p> : <ul>{changes(detail.data.changeSummary).map(c => <li key={c.field}>{c.field}: {c.evidence === 'Allowlisted' ? `${c.before ?? '—'} → ${c.after ?? '—'}` : c.evidence === 'RevisionReference' ? `${detail.data.beforeRevision ?? '—'} → ${detail.data.afterRevision ?? '—'}` : (localizedText(en, "pairs.m_33bdc1f5e4a999af"))}</li>)}</ul>}
      {detail.data.revisionHref && ['TugJob', 'TugJobRevision'].includes(detail.data.entityType) && <TugAuditRevision key={detail.data.id} auditId={detail.data.id} language={language}/>}
      {detail.data.revisionHref ? <EnterpriseActions align="start"><Button variant="outline" asChild><a href={detail.data.revisionHref}><ExternalLink aria-hidden="true"/>{localizedText(en, "pairs.m_1a815f2af28b7c6f")}</a></Button></EnterpriseActions> : <p>{localizedText(en, "pairs.m_a3908938204d6f10")}</p>}
    </section>}
  </section>;
}
