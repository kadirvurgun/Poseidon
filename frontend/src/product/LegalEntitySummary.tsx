import { useEffect, useState } from 'react';
import { Button } from '../components/ui/button';
import type { components } from '../generated/api';
import * as api from './api';
import { editorCopy } from './editor-copy';
import type { Language } from './editor-copy';

// PLAN-1511-5201: read-only view of the 52.01 LegalEntity that extends this company record.
// Unknown values are shown as unknown and a Draft is never presented as ready or active.
type LegalEntity = components['schemas']['HrLegalEntityDto'];
type State = { kind: 'idle' } | { kind: 'loading' } | { kind: 'unavailable' } | { kind: 'failed' } | { kind: 'ready'; entity: LegalEntity };

function decode(value: unknown): LegalEntity {
    const v = value as Partial<LegalEntity> | null;
    if (!v || typeof v.id !== 'string' || typeof v.status !== 'string' || !['Draft', 'Active', 'Inactive'].includes(v.status) ||
        !Array.isArray(v.missingForActivation) || (v.legalName != null && typeof v.legalName !== 'string'))
        throw new api.ApiError(502, 'invalid_contract');
    return v as LegalEntity;
}

export function LegalEntitySummary({ companyId, language }: { companyId: string; language: Language }) {
    const [state, setState] = useState<State>({ kind: 'idle' });
    const [requested, setRequested] = useState(0);
    const t = editorCopy[language];
    // Loaded only on explicit request: opening the company editor makes no HR read.
    useEffect(() => {
        if (requested === 0)
            return;
        const controller = new AbortController();
        let active = true;
        api.request<unknown>(`hr/legal-entities/${encodeURIComponent(companyId)}`, controller.signal)
            .then(value => { if (active) setState({ kind: 'ready', entity: decode(value) }); })
            .catch(error => { if (active) setState({ kind: error instanceof api.ApiError && (error.status === 403 || error.status === 404) ? 'unavailable' : 'failed' }); });
        return () => { active = false; controller.abort(); };
    }, [companyId, requested]);
    const status = (value: string) => value === 'Active' ? t.leActive : value === 'Inactive' ? t.leInactive : t.leDraft;
    const known = (value: string | null | undefined) => value ?? t.leUnknown;
    return <section className="legal-entity-summary" aria-labelledby="legal-entity-heading" data-testid="legal-entity-summary">
      <h3 id="legal-entity-heading">{t.leHeading}</h3>
      {(state.kind === 'idle' || state.kind === 'failed') && <Button variant="outline" onClick={() => { setState({ kind: 'loading' }); setRequested(v => v + 1); }}>{t.leShow}</Button>}
      {state.kind === 'loading' && <p role="status">{t.leLoading}</p>}
      {state.kind === 'unavailable' && <p role="status">{t.leUnavailable}</p>}
      {state.kind === 'failed' && <p role="alert">{t.leFailed}</p>}
      {state.kind === 'ready' && <>
        <dl>
          <dt>{t.leStatus}</dt><dd data-status={state.entity.status}>{status(state.entity.status)}</dd>
          <dt>{t.leLegalName}</dt><dd>{known(state.entity.legalName)}</dd>
          <dt>{t.leCountry}</dt><dd>{known(state.entity.countryCode)}</dd>
          <dt>{t.leTimeZone}</dt><dd>{known(state.entity.hrTimeZone)}</dd>
          {state.entity.missingForActivation.length > 0 && <><dt>{t.leMissing}</dt><dd>{state.entity.missingForActivation.length}</dd></>}
        </dl>
        <p>{t.leNote}</p>
      </>}
    </section>;
}
