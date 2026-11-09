import { localizedLabel } from "../i18n/messages";
import { localizedTuple } from "../i18n/messages";
import { localizedText, sourceText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useId, useState } from 'react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseActions } from './EnterprisePatterns';
import { loadOnboarding, type OnboardingAssessment, type OnboardingField } from './compliance-api';
import './compliance-enterprise.css';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
export const onboardingSteps = [
    { en: sourceText("en", "pairs.m_522036cca84ed10a"), tr: sourceText("tr", "pairs.m_522036cca84ed10a"), fields: ['imo', 'flag', 'shipType', 'classSociety', 'classNumber'] },
    { en: sourceText("en", "pairs.m_10da6fcfbfbe46ac"), tr: sourceText("tr", "pairs.m_10da6fcfbfbe46ac"), fields: ['gt', 'nt', 'dwt', 'loa', 'breadth', 'draft', 'buildDate', 'mainPropulsionKw', 'contractForConstructionDate', 'constructionRecordReference'] },
    { en: sourceText("en", "pairs.m_49b48e712adc946d"), tr: sourceText("tr", "pairs.m_49b48e712adc946d"), fields: ['tradingArea', 'cargoContext', 'declaration', 'sourceReference', 'sourceObservedAt'] },
    { en: sourceText("en", "pairs.m_3e055bef01f6e2f1"), tr: sourceText("tr", "pairs.m_3e055bef01f6e2f1"), fields: [] },
] as const;
export function OnboardingSteps({ step, language, disabled, onChange }: {
    step: number;
    language: AppLocale;
    disabled: boolean;
    onChange: (step: number) => void;
}) {
    return <nav aria-label={localizedText(language, "pairs.m_8bc63909a885cf38")}><ol className="onboarding-steps">{onboardingSteps.map((item, index) => <li key={item.en}><Button variant={step === index ? 'secondary' : 'ghost'} disabled={disabled} aria-current={step === index ? 'step' : undefined} onClick={() => onChange(index)}><span aria-hidden="true">{index + 1}</span>{localizedLabel(item, language)}</Button></li>)}</ol></nav>;
}
const labels: Record<OnboardingField, [
    string,
    string
]> = { 'vessel.code': [sourceText("en", "pairs.m_f6b4560462562dd7"), sourceText("tr", "pairs.m_f6b4560462562dd7")], 'vessel.name': [sourceText("en", "pairs.m_bb336986086fcf27"), sourceText("tr", "pairs.m_bb336986086fcf27")], 'profile.imo': [sourceText("en", "pairs.m_76a9d7c6a28d152e"), sourceText("tr", "pairs.m_76a9d7c6a28d152e")], 'profile.flag': [sourceText("en", "pairs.m_89f14ddcc216bfff"), sourceText("tr", "pairs.m_89f14ddcc216bfff")], 'profile.classSociety': [sourceText("en", "pairs.m_d6f66d83374b622b"), sourceText("tr", "pairs.m_d6f66d83374b622b")], 'profile.shipType': [sourceText("en", "pairs.m_74d2ca24a5449a18"), sourceText("tr", "pairs.m_74d2ca24a5449a18")], 'profile.gt': [sourceText("en", "pairs.m_289093ee560f5345"), sourceText("tr", "pairs.m_289093ee560f5345")], 'profile.buildDate': [sourceText("en", "pairs.m_06ae6bdccff9bfb1"), sourceText("tr", "pairs.m_06ae6bdccff9bfb1")], 'profile.tradingArea': [sourceText("en", "pairs.m_fef35f1e36a8aabf"), sourceText("tr", "pairs.m_fef35f1e36a8aabf")], 'profile.cargoContext': [sourceText("en", "pairs.m_1c28884f28a26286"), sourceText("tr", "pairs.m_1c28884f28a26286")] };
export function OnboardingAssessmentView({ vessel, savedRevision, dirty, language }: {
    vessel: string;
    savedRevision: number | null;
    dirty: boolean;
    language: AppLocale;
}) {
    const epoch = useWorkspaceRevalidation(), online = navigator.onLine, visible = document.visibilityState !== 'hidden';
    const en = language === 'en', description = useId(), [attempt, retry] = useState(0), [result, setResult] = useState<{
        vessel: string;
        revision: number | null;
        epoch: number;
        data?: OnboardingAssessment;
        failed?: boolean;
    } | null>(null);
    useEffect(() => {
        if (!online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        loadOnboarding(vessel, c.signal).then(data => {
            if (active)
                setResult({ vessel, revision: savedRevision, epoch, data });
        }).catch(() => {
            if (active)
                setResult({ vessel, revision: savedRevision, epoch, failed: true });
        }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [vessel, savedRevision, attempt, epoch, online, visible]);
    const current = result?.vessel === vessel && result.revision === savedRevision && result.epoch === epoch ? result : null;
    const data = current?.data;
    const assessment = data && data.profileRevision === savedRevision ? data : null;
    return <section className="onboarding-assessment" aria-label={localizedText(en, "pairs.m_93c6c5afa656ae49")}><h3>{localizedText(en, "pairs.m_93c6c5afa656ae49")}</h3>
    {dirty && <p role="status">{localizedText(en, "pairs.m_207cc0dc35e7523c")}</p>}
    {!online || !visible ? <p role="status">{localizedText(en, "pairs.m_e3b8ae15aa817931")}</p> : !current ? <p role="status">{localizedText(en, "pairs.m_055bd06503b9ead6")}</p> : !assessment ? <p role="alert">{current.failed ? (localizedText(en, "pairs.m_4ab360a52a340ed0")) : (localizedText(en, "pairs.m_71db14476d79fa74"))}</p> : <>
      <Badge tone="warning">{assessment.state === 'REVIEW_REQUIRED' ? (localizedText(en, "pairs.m_6edb7d9a4aca3fd7")) : (localizedText(en, "pairs.m_934013d6713625c6"))}</Badge><p>{localizedText(en, "pairs.m_31bee807a4c68295")}: {assessment.profileRevision ?? (localizedText(en, "pairs.m_bb3af7dbded40f7a"))}</p>
      <ul className="onboarding-checklist">{assessment.fields.map(field => <li key={field.key}><strong>{localizedTuple(labels[field.key], en, 0)}</strong><span>{field.complete ? (localizedText(en, "pairs.m_ae469eb27e58edf5")) : field.state === 'Unknown' ? (localizedText(en, "pairs.m_0722fd29544cdfd0")) : (localizedText(en, "pairs.m_186616de0b3f99fe"))}</span></li>)}</ul>
      <p>{localizedText(en, "pairs.m_08fe312b232a2419")}</p>
    </>}
    <EnterpriseActions aria-label={localizedText(en, "pairs.m_95c76ae693e7bed5")}><Button variant="outline" disabled={!online || !visible} onClick={() => { setResult(null); retry(n => n + 1); }}>{localizedText(en, "pairs.m_874b14d877a60899")}</Button><Button disabled aria-describedby={description}>{localizedText(en, "pairs.m_50f8c965f68497f1")}</Button></EnterpriseActions><p id={description}>{localizedText(en, "pairs.m_158ad2e4b588cdac")}</p>
  </section>;
}
