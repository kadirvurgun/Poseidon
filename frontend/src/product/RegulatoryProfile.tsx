import { localizedLabel } from "../i18n/messages";
import { localizedCopy, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useProfileRecovery } from './use-profile-recovery';
import { profileFields } from './profile-contract';
import type { ProfileContent } from './profile-contract';
import { RequiredDataField } from './RequiredDataField';
import { loginHref } from './workspace-location';
import { ConstructionRecordPicker } from './ConstructionRecordPicker';
import { ProfileSourceSummary } from './ProfileSourceSummary';
import { ProfileFieldProvenance } from './ProfileFieldProvenance';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns';
import { OnboardingSteps, onboardingSteps, OnboardingAssessmentView } from './VesselOnboarding';
const copy = localizedCopy("RegulatoryProfile_tsx_copy");
export type ProfileDiscardHandle = { context: string; discard: (canRelease: () => boolean) => Promise<boolean>; canLeave: () => boolean };
type Props = {
    id: string;
    language: AppLocale;
    csrf: string; // Retained caller contract; writes use fresh actual session CSRF.
    owner?: string | null;
    onRegisterDiscard?: (handle: ProfileDiscardHandle) => () => void;
    offline?: boolean;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
    journey?: 'profile' | 'onboarding';
    // DEV-020: opt-in field provenance/freshness panel bound to the saved profile revision.
    fieldProvenance?: boolean;
};
export function RegulatoryProfile(props: Props) {
    const { id, language, journey = 'profile', onRegisterDiscard } = props;
    const onboarding = journey === 'onboarding', [step, setStep] = useState(0);
    const recovery = useProfileRecovery(props);
    const { profile, content, history, historyOffset, historyFailure, failure, busy: saving, uncertain, dirty, notice, invalidateAccess } = recovery;
    const currentRecovery = useRef(recovery);
    useLayoutEffect(() => { currentRecovery.current = recovery; });
    const discardContext = `${id}/${journey}/${props.owner ?? ''}`;
    useEffect(() => onRegisterDiscard?.({ context: discardContext,
        discard: canRelease => currentRecovery.current.discard(canRelease), canLeave: () => currentRecovery.current.canLeave(),
    }), [discardContext, onRegisterDiscard]);
    const errors = failure?.errors ?? {};
    const t = copy[language];
    useEffect(() => {
        const prevent = (event: BeforeUnloadEvent) => { if (dirty || saving || uncertain) event.preventDefault(); };
        window.addEventListener('beforeunload', prevent);
        return () => window.removeEventListener('beforeunload', prevent);
    }, [dirty, saving, uncertain]);
    function change(key: keyof ProfileContent, value: ProfileContent[typeof key]) { recovery.edit(key, value); }
    function submit(event: FormEvent) {
        event.preventDefault();
        if (saving) return;
        if (recovery.attempt) recovery.retry();
        else recovery.capture();
    }
    const stepLocked = saving || uncertain || !!recovery.confirmed || !recovery.canShow;
    const status = failure?.status;
    const errorText = uncertain ? t.uncertain : failure?.code === 'version_conflict' ? t.conflict : status === 401 ? t.expired : status === 403 ? t.denied : status === 404 ? t.missing : t.failed;
    const provenance = (value: string) => ({ Synthetic: localizedText(language, "pairs.m_195b868f709e8de4"), Missing: localizedText(language, "pairs.m_a5bd35d90de7c160"), Unknown: t.unknown, DeclaredUnverified: localizedText(language, "pairs.m_e043dea57f9e1669") })[value] ?? t.unknown;
    const savedObservation = profile?.content.sourceObservedAt;
    const observedAt = savedObservation?.state === 'Known' && typeof savedObservation.value === 'string' ? savedObservation.value : null;
    const title = onboarding ? (localizedText(language, "pairs.m_372d69a18a20b71d")) : t.title;
    return <section className="regulatory-profile" aria-label={title}>
    <h2>{title}</h2><p className="profile-draft-note">{t.note}</p>
    {failure && <div role="alert"><p>{errorText}</p>{status === 401 && <a href={loginHref()}>{localizedText(language, "pairs.m_16505c0ee5e527ad")}</a>}
      {!profile && <button onClick={recovery.recheck}>{t.reloadRead}</button>}
      {failure.code === 'version_conflict' && !uncertain && <button disabled={!recovery.canDiscard} onClick={() => void recovery.discard()}>{t.reload}</button>}</div>}
    {!profile && !failure && !notice && <p role="status">{t.loading}</p>}
    {notice && <p role="status">{t.saved}</p>}
    {(recovery.attempt || recovery.confirmed || dirty) && <EnterpriseActions>
      <Button variant="outline" disabled={saving} onClick={recovery.recheck}>{localizedText(language, "pairs.m_542619365a11df9d")}</Button>
      {!recovery.attempt && <Button variant="outline" disabled={!recovery.canDiscard} onClick={() => void recovery.discard()}>{localizedText(language, "pairs.m_92ccf21431e2c4a0")}</Button>}
    </EnterpriseActions>}
    {profile && content && <>
      <p className="profile-context">{profile.tenantId} · <strong>{profile.vessel.code} · {profile.vessel.name}</strong> · {profile.vessel.kind === 'Ship' ? (localizedText(language, "pairs.m_bb1dbfbac5d5c650")) : (localizedText(language, "pairs.m_306aaabcb4e04a9f"))}</p>
      {!profile.revision && <p>{t.empty}</p>}
      <p>{t.provenance}: {provenance(profile.provenance)} · {t.revision}: {profile.revision} · {t.time}: {profile.savedAt ?? t.unknown}</p>
      <ProfileSourceSummary profile={profile} language={language} />
      {props.fieldProvenance && !onboarding && <ProfileFieldProvenance vessel={id} language={language} revision={profile.revision} dirty={dirty} onDenied={invalidateAccess}/>}
      <aside className="profile-source-context" aria-label={localizedText(language, "pairs.m_e263b98182c6b18a")}><strong>{t.observed}</strong><p>{observedAt !== null ? <time dateTime={observedAt}>{observedAt}</time> : savedObservation?.state === 'Missing' ? (localizedText(language, "pairs.m_a5bd35d90de7c160")) : t.unknown}</p><p>{localizedText(language, "pairs.m_bdf69e4bcf117eaa")}</p></aside>
      <p>{profileFields.filter(([key]) => content[key]?.state !== 'Known').length} {onboarding ? (localizedText(language, "pairs.m_344296b81dee2be9")) : t.count}</p>
      {!profile.vessel.canEdit && <p>{t.readonly}</p>}
      {onboarding && step === 0 && <p>{localizedText(language, "pairs.m_2557e1055098f1ce")}</p>}
      {onboarding && <OnboardingSteps step={step} language={language} disabled={stepLocked} onChange={setStep}/>}
      {onboarding && Object.keys(errors).length > 0 && <div role="alert"><p>{localizedText(language, "pairs.m_a775986543772964")}</p><ul>{onboardingSteps.slice(0, 3).map((item, index) => item.fields.some(field => errors[field]?.length) ? <li key={item.en}><Button variant="outline" disabled={stepLocked} onClick={() => setStep(index)}>{localizedText(language, "pairs.m_c58ded00c56a8b4b")}: {localizedLabel(item, language)}</Button></li> : null)}</ul></div>}
      <form noValidate aria-busy={saving} onSubmit={event => void submit(event)}>
        {(!onboarding || step === 1) && <ConstructionRecordPicker key={`${recovery.scope}/${uncertain}/${!!recovery.confirmed}`} load={recovery.loadFiles} vessel={id} language={language} value={content.constructionRecord} disabled={recovery.locked} onChange={value => change('constructionRecord', value)} onDenied={invalidateAccess}/>}
        {onboarding && <h3>{localizedLabel(onboardingSteps[step], language)}</h3>}
        <div className="profile-field-grid">{profileFields.filter(([key]) => !onboarding || onboardingSteps[step].fields.some(field => field === key)).map(([key, kind, tr, en]) => {
                const common = { label: localizedLabel({ en: en, tr: tr }, language), language, disabled: recovery.locked, error: !!errors[key]?.length };
                return kind === 'number' ? <RequiredDataField key={key} {...common} kind="number" value={content[key] ?? { state: 'Missing' }} onChange={value => change(key, value)}/> :
                    <RequiredDataField key={key} {...common} kind="text" value={content[key] ?? { state: 'Missing' }} onChange={value => change(key, value)}/>;
            })}</div>
        {onboarding && step === 3 && <><dl className="onboarding-review-values">{profileFields.map(([key, , tr, en]) => <div key={key}><dt>{localizedLabel({ en: en, tr: tr }, language)}</dt><dd>{content[key]?.state === 'Known' ? String(content[key]?.value ?? t.unknown) : content[key]?.state === 'Unknown' ? t.unknown : (localizedText(language, "pairs.m_a5bd35d90de7c160"))}</dd></div>)}</dl><OnboardingAssessmentView vessel={id} savedRevision={profile.revision > 0 ? profile.revision : null} dirty={dirty} language={language}/></>}
        {onboarding && <EnterpriseActions aria-label={localizedText(language, "pairs.m_69b974dc1090cab4")}><Button variant="outline" disabled={step === 0 || stepLocked} onClick={() => setStep(n => Math.max(0, n - 1))}>{localizedText(language, "pairs.m_11b5b0852be5c621")}</Button><Button variant="outline" disabled={step === 3 || stepLocked} onClick={() => setStep(n => Math.min(3, n + 1))}>{localizedText(language, "pairs.m_892b82e46cfa25df")}</Button></EnterpriseActions>}
        {profile.vessel.canEdit && !recovery.confirmed && <EnterpriseActions aria-label={localizedText(language, "pairs.m_7207e9da6d60a23c")}><Button type="submit" disabled={saving || (uncertain ? !recovery.canRetry : recovery.locked || failure?.code === 'version_conflict')}>{saving ? t.saving : uncertain ? t.retry : t.save}</Button></EnterpriseActions>}
      </form>
      <h3>{t.history}</h3>
      {historyFailure ? <div role="alert">{t.failed}<button onClick={recovery.historyRetry} disabled={stepLocked}>{t.reloadRead}</button></div> : !history ? <p role="status">{t.loading}</p> : <>
        {!history.items.length && <p>{t.noHistory}</p>}
        {history.items.map(row => <details key={row.revision}><summary>{t.revision} {row.revision} · {row.savedAt} · {provenance(row.provenance)}</summary>
          <dl>{profileFields.map(([key, , tr, en]) => <div key={key}><dt>{localizedLabel({ en: en, tr: tr }, language)}</dt><dd>{row.content[key]?.state === 'Known' ? String(row.content[key]?.value) : row.content[key]?.state === 'Unknown' ? t.unknown : (localizedText(language, "pairs.m_a5bd35d90de7c160"))}</dd></div>)}</dl></details>)}
        <EnterprisePagination label={localizedText(language, "pairs.m_8f426e60b0953c8c")} previous={t.previous} next={t.next} previousDisabled={historyOffset === 0} nextDisabled={!history.hasMore} onPrevious={() => recovery.historyPage(historyOffset - 25)} onNext={() => recovery.historyPage(historyOffset + 25)}/>
      </>}
    </>}
  </section>;
}
