import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns';
import { loadPassport, type CompliancePassport as Passport } from './compliance-api';
import { ApplicabilityReasonView, CapturedEvaluationInputs } from './ApplicabilityReasonView';
import type { Row } from './control-evidence-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { ComplianceBlockers } from './ComplianceBlockers';
import { PassportCertificates } from './PassportCertificates';
import './compliance-enterprise.css';
export function CompliancePassport({ vessel, language, onNavigate }: {
    vessel: string;
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [offset, setOffset] = useState(0), [attempt, retry] = useState(0);
    const [result, setResult] = useState<{
        scope: string;
        data?: Passport;
        failed?: boolean;
    } | null>(null), scope = `${vessel}/${offset}/${attempt}/${epoch}`;
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden';
    useEffect(() => {
        if (!online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        loadPassport(vessel, offset, c.signal).then(data => { if (active)
            setResult({ scope, data }); }).catch(() => { if (active)
            setResult({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [vessel, offset, scope, online, visible]);
    const current = result?.scope === scope ? result : null, data = current?.data;
    const unknown = localizedText(en, "pairs.m_49138f2831bf4262");
    return <section className="compliance-passport" aria-label={localizedText(en, "pairs.m_79a9c76a3213e157")}><h2>{localizedText(en, "pairs.m_79a9c76a3213e157")}</h2><EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_f02cc865638753ae")}><Button variant="outline" disabled={!online} onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_0803e16c8c0d813d")}</Button><Button variant="outline" onClick={() => onNavigate(`/app/fleet/${vessel}/onboarding`)}>{localizedText(en, "pairs.m_b6ffba5bcebc2a9f")}</Button></EnterpriseActions>
    {!online ? <p role="status">{localizedText(en, "pairs.m_ea3589ffe2f11c34")}</p> : !visible ? <p role="status">{localizedText(en, "pairs.m_5f323e3ae06075eb")}</p> : !current ? <p role="status">{localizedText(en, "pairs.m_6bc1dfc828a8fd7f")}</p> : !data ? <p role="alert">{localizedText(en, "pairs.m_312fa1b15637da70")}</p> : <>
      <Badge tone="warning">{localizedText(en, "pairs.m_d3bed0e564e449bd")}</Badge><p>{localizedText(en, "pairs.m_49b1431c59b5cdfd")}: <time dateTime={data.readAt}>{data.readAt}</time></p>
      <PassportCertificates page={data.certificates} language={language} onNavigate={onNavigate}/>
      <div className="passport-coverage"><section><h3>{localizedText(en, "pairs.m_fe46dd552ddbaaa3")}</h3><p>{data.plan.coverage === 'CURRENT_QUALIFIED_CATALOG_ONLY' ? (localizedText(en, "pairs.m_69895a73282032ed")) : unknown}</p><p>{localizedText(en, "pairs.m_0cff059beade516b")}: {data.plan.uncontrolledRequirementCount ?? unknown}</p><p>{localizedText(en, "pairs.m_f3e861d0bcd77840")}: {unknown}</p></section></div>
      <section id="applicability" aria-label={localizedText(en, "pairs.m_379fda2bd6f2f20d")}><h3>{localizedText(en, "pairs.m_379fda2bd6f2f20d")}</h3><Badge tone="neutral">{data.applicability.reviewerState === 'QUALIFIED_CURRENT' ? (localizedText(en, "pairs.m_30a6416adb3609c6")) : (localizedText(en, "pairs.m_6edb7d9a4aca3fd7"))}</Badge>{!data.applicability.current && <p>{localizedText(en, "pairs.m_f7a91f916e0b3cbc")}</p>}<ApplicabilityReasonView results={data.applicability.results} language={language}/><CapturedEvaluationInputs inputs={data.applicability.inputs} inputHash={data.applicability.inputHash} ruleSetHash={data.applicability.ruleSetHash} language={language}/><Button disabled>{localizedText(en, "pairs.m_8c5ee87a1337c9c4")}</Button><p>{localizedText(en, "pairs.m_2e0042cfff674097")}</p></section>
      <section aria-label={localizedText(en, "pairs.m_9b2680e1e3b5248f")}><h3>{localizedText(en, "pairs.m_9b2680e1e3b5248f")}</h3>{!data.plan.requirements.length && <p>{localizedText(en, "pairs.m_55810d769762a451")}</p>}{data.plan.requirements.map(row => <details key={`${row.ruleId}/${row.ruleRevision}`}><summary>{row.requirementId ?? unknown} / {row.requirementRevision ?? unknown} · {row.state === 'LINKED' ? (localizedText(en, "pairs.m_35a232fe1d4a5a27")) : (localizedText(en, "pairs.m_6edb7d9a4aca3fd7"))}</summary><p>{localizedText(en, "pairs.m_8ab4bae5786f60ad")}: {row.ruleId} / {row.ruleRevision}</p><p>{localizedText(en, "pairs.m_b4f1fa82036ef943")}: {row.sourceId ?? unknown} / {row.sourceRevision ?? unknown}</p><p>{localizedText(en, "pairs.m_e7d43233f52b27e1")}: {row.controlIds.length ? row.controlIds.join(' · ') : unknown}</p><ComplianceBlockers codes={row.blockers} language={language}/>{row.requirementId && <Button variant="outline" onClick={() => onNavigate(`/app/requirements/${row.requirementId}`)}>{localizedText(en, "pairs.m_f7344077d7340f74")}</Button>}</details>)}</section>
      <PassportRecords rows={data.plan.controls} kind="Control" language={language}/><PassportRecords rows={data.plan.evidence} kind="Evidence" language={language}/>
      <section aria-label={localizedText(en, "pairs.m_7da40843c0bfe9e2")}><h3>{localizedText(en, "pairs.m_7da40843c0bfe9e2")}</h3><ComplianceBlockers codes={data.blockers} language={language}/></section>
      <EnterprisePagination label={localizedText(en, "pairs.m_3e8371cdb2595664")} previous={localizedText(en, "pairs.m_8c654d061fabe655")} next={localizedText(en, "pairs.m_0cd16a138ba06554")} previousDisabled={!offset} nextDisabled={!data.plan.hasMore && !data.certificates.hasMore || offset >= 100000} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/>
    </>}
  </section>;
}
function PassportRecords({ rows, kind, language }: {
    rows: Row[];
    kind: 'Control' | 'Evidence';
    language: AppLocale;
}) {
    const en = language === 'en', title = kind === 'Control' ? (localizedText(en, "pairs.m_7165c849558459ed")) : (localizedText(en, "pairs.m_865ab8a1282f01ac")), unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd");
    return <section aria-label={title}><h3>{title}</h3>{!rows.length && <p>{localizedText(en, "pairs.m_e2e5c078918eeb87")}</p>}{rows.map(row => <details key={`${row.id}/${row.revision}`}><summary>{row.content.title ?? row.id} · {row.revision} · {row.timeState}</summary><dl className="onboarding-review-values"><div><dt>{localizedText(en, "pairs.m_e43b1696cdc0ac59")}</dt><dd>{row.completion} / {row.verification} / {row.effectiveness}</dd></div><div><dt>{localizedText(en, "pairs.m_a3ac51a77290cf8f")}</dt><dd>{row.content.ownerRole ?? unknown} / {row.content.frequency ?? unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_36ab6d4e77a88c01")}</dt><dd>{row.content.evidencePolicy ?? unknown} / {row.content.verificationPolicy ?? unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_dcd168206de47c70")}</dt><dd>{row.content.observedAt ?? unknown} / {row.content.freshUntil ?? unknown}</dd></div></dl><p>{localizedText(en, "pairs.m_cb56cddb4733ce23")}</p><p>{localizedText(en, "pairs.m_12f0d7b8397e3b83")}: <code>{row.hash}</code></p><ComplianceBlockers codes={row.blockers} language={language}/></details>)}</section>;
}
