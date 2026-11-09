import { sourceText, localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useRef, useState } from 'react';
import { ApiError, request } from './api';
type Revision = {
    jobId: string;
    revision: number;
    jobNumber: string;
    sourceVersion: string;
    savedAt: string;
    provenance: string;
    requestedStart: string | null;
    requestedEnd: string | null;
    requiredBollardPull: string | null;
    requiredTugCount: number | null;
    jobType: string | null;
    priority: string | null;
    ownerRoleReference: string | null;
};
export function TugAuditRevision({ auditId, language }: {
    auditId: string;
    language: AppLocale;
}) {
    const [value, setValue] = useState<Revision | null>(null), [state, setState] = useState<'idle' | 'loading' | 'error'>('idle');
    const current = useRef<AbortController | null>(null), en = language === 'en';
    const resetScope1 = [auditId] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], auditId)) {
        setPreviousScope1(resetScope1);
        setValue(null);
        setState('idle');
    }
    useEffect(() => { return () => current.current?.abort(); }, [auditId]);
    const load = async () => {
        current.current?.abort();
        const controller = new AbortController();
        current.current = controller;
        setValue(null);
        setState('loading');
        try {
            const response = await request<{
                kind: string;
                tug: Revision | null;
            }>(`audit/${encodeURIComponent(auditId)}/revision`, controller.signal);
            if (response?.kind !== 'TugJob' || !response.tug || !Number.isSafeInteger(response.tug.revision) || response.tug.revision <= 0 || typeof response.tug.jobNumber !== 'string')
                throw new ApiError(502);
            if (!controller.signal.aborted) {
                setValue(response.tug);
                setState('idle');
            }
        }
        catch {
            if (!controller.signal.aborted)
                setState('error');
        }
    };
    return <section aria-label={localizedText(en, "pairs.m_4b0b708e1ef5eba3")}><button disabled={state === 'loading'} onClick={() => void load()}>{localizedText(en, "pairs.m_f41e0a8eae5f3517")}</button>
    {state === 'loading' && <p role="status">{localizedText(en, "pairs.m_b10b29c03e3b9b79")}</p>}
    {state === 'error' && <p role="alert">{localizedText(en, "pairs.m_fb71268990607932")}</p>}
    {value && <><p>{localizedText(en, "pairs.m_c51eaa100cd391ec")}</p><dl>
      {([['jobNumber', sourceText("en", "pairs.m_ce38f1273ed8b4cd"), sourceText("tr", "pairs.m_ce38f1273ed8b4cd")], ['revision', sourceText("en", "pairs.m_9063738166a73fc0"), sourceText("tr", "pairs.m_9063738166a73fc0")], ['savedAt', sourceText("en", "pairs.m_99bf579926929ccb"), sourceText("tr", "pairs.m_99bf579926929ccb")],
                ['sourceVersion', sourceText("en", "pairs.m_084ab5b9b7688319"), sourceText("tr", "pairs.m_084ab5b9b7688319")], ['requestedStart', sourceText("en", "pairs.m_ec4a6eea652abc05"), sourceText("tr", "pairs.m_ec4a6eea652abc05")], ['requestedEnd', sourceText("en", "pairs.m_fac253f0ccf1a9cf"), sourceText("tr", "pairs.m_fac253f0ccf1a9cf")],
                ['requiredBollardPull', sourceText("en", "pairs.m_89c45d84da6e344e"), sourceText("tr", "pairs.m_89c45d84da6e344e")], ['requiredTugCount', sourceText("en", "pairs.m_f65b450b39dd7b33"), sourceText("tr", "pairs.m_f65b450b39dd7b33")], ['jobType', sourceText("en", "pairs.m_4936290c1e33377c"), sourceText("tr", "pairs.m_4936290c1e33377c")],
                ['priority', sourceText("en", "pairs.m_9077f204711545d1"), sourceText("tr", "pairs.m_9077f204711545d1")], ['ownerRoleReference', sourceText("en", "pairs.m_aad8c1474d4ec372"), sourceText("tr", "pairs.m_aad8c1474d4ec372")]] as const).map(([key, english, turkish]) => <div key={key}><dt>{localizedLabel({ en: english, tr: turkish }, en)}</dt><dd>{value[key] ?? (localizedText(en, "pairs.m_a5bd35d90de7c160"))}</dd></div>)}
    </dl></>}
  </section>;
}
