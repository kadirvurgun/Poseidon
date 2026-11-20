import { localizedCopy, localizedText } from "../i18n/messages";
import { presentationLabel } from '../i18n/labels';
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { useEffect, useState } from 'react';
import { ApiError } from './api';
import { owningInputs, type CrewOwningInput } from './crew-api';
const compliance = localizedCopy('CrewCompliance_tsx_copy');
export function CrewOwningInputs({ vessel, assignment, language }: {
    vessel: string;
    assignment: string;
    language: AppLocale;
}) {
    const [inputs, setInputs] = useState<CrewOwningInput[] | null>(null), [failed, setFailed] = useState(false), [denied, setDenied] = useState(false), [generation, reload] = useState(0);
    const resetScope1 = [vessel, assignment, generation] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], assignment) || !Object.is(previousScope1[2], generation)) {
        setPreviousScope1(resetScope1);
        setInputs(null);
        setFailed(false);
        setDenied(false);
    }
    useEffect(() => {
        const c = new AbortController();
        owningInputs(vessel, assignment, c.signal).then(v => { if (!c.signal.aborted)
            setInputs(v); }).catch(e => {
            if (!c.signal.aborted) {
                setFailed(true);
                setDenied(e instanceof ApiError && [401, 403, 404].includes(e.status));
            }
        });
        return () => c.abort();
    }, [vessel, assignment, generation]);
    const label: Record<string, string> = {
        certificate_state: presentationLabel(language, 'crewCertificateInput'),
        contact_status: presentationLabel(language, 'crewContactInput'),
        competence_state: presentationLabel(language, 'crewCompetenceInput'),
        work_rest_state: presentationLabel(language, 'crewWorkRestInput'),
        eligibility_state: presentationLabel(language, 'crewEligibilityInput'),
        fitness_state: presentationLabel(language, 'crewFitnessInput'),
    };
    const states: Record<CrewOwningInput['state'], string> = {
        ELIGIBLE: compliance[language].state_ELIGIBLE,
        ATTENTION: compliance[language].state_ATTENTION,
        NOT_ELIGIBLE: compliance[language].state_NOT_ELIGIBLE,
        REVIEW_REQUIRED: compliance[language].state_REVIEW_REQUIRED,
        Withheld: presentationLabel(language, 'crewAccessWithheld'),
        Missing: presentationLabel(language, 'crewNoVisibleDeclaration'),
        DeclaredUnverified: presentationLabel(language, 'crewDeclaredUnverified'),
        Unavailable: presentationLabel(language, 'crewProtectedUnavailable'),
        EvaluationUnavailable: presentationLabel(language, 'crewEvaluationUnavailable'),
    };
    return <section aria-label={localizedText(language, "pairs.m_417e6f415e738092")}><h2>{localizedText(language, "pairs.m_417e6f415e738092")}</h2>
    <p>{localizedText(language, "pairs.m_b6a9d6adae7da178")}</p>
    {!inputs && !failed && <p role="status">{localizedText(language, "pairs.m_42b97f8d749a53c4")}</p>}
    {failed && <p role="alert">{denied ? (localizedText(language, "pairs.m_844f3ae7f83a4dd4")) : (localizedText(language, "pairs.m_2e55a0e830f5cc65"))}</p>}
    <Button type="button" variant="outline" onClick={() => reload(x => x + 1)}>{localizedText(language, "pairs.m_15e0a9cd9a01c66e")}</Button>
    {inputs && <dl>{inputs.map(v => <div key={v.field}><dt>{label[v.field]}</dt><dd>{states[v.state]}</dd></div>)}</dl>}
  </section>;
}
