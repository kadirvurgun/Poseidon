import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { Content } from './control-evidence-api';
import { complianceKeys, type OwningReferences } from './compliance-input-contract';
export function ComplianceInputEditor({ value, onChange, offline, language }: {
    value: Content;
    onChange: (v: Content) => void;
    offline: boolean;
    language: AppLocale;
}) {
    const t = (en: string, tr: string) => localizedLabel({ en: en, tr: tr }, language);
    const key = complianceKeys.find(k => value[k] != null);
    const choose = (k: string) => onChange(Object.assign({}, value, { tugCapability: null, operation: null, serviceAuthorization: null, cargoPlan: null, winchFitment: null, classPack: null, msmd: null, crewRoleAssessment: null }, (k === 'classPack' ? { classPack: { packReference: null, edition: null, surveyReference: null, iacsM79: null } } : k === 'msmd' ? { msmd: { documentReference: null, issuerReference: null, roles: null } } : k === 'crewRoleAssessment' ? { crewRoleAssessment: { assignmentId: null, assignmentRevision: null, assignmentVersion: null, positionId: null, positionVersion: null, from: null, to: null, criterion: null, recordReference: null, satisfied: null, credential: null } } : {})));
    const record = key ? value[key] : null;
    const labels: Record<string, [
        string,
        string
    ]> = { packReference: ['Class rule pack reference', 'Klas kural paketi referansı'], edition: ['Pack edition', 'Paket sürümü'], surveyReference: ['Survey record reference', 'Sörvey kayıt referansı'], iacsM79: ['M79 statement in source', 'Kaynakta M79 beyanı'], documentReference: ['MSMD document reference', 'MSMD belge referansı'], issuerReference: ['Issuer reference', 'Düzenleyen referansı'], assignmentId: ['Assignment reference', 'Atama referansı'], assignmentRevision: ['Assignment revision', 'Atama revizyonu'], assignmentVersion: ['Assignment version', 'Atama sürümü'], positionId: ['Position reference', 'Pozisyon referansı'], positionVersion: ['Position version', 'Pozisyon sürümü'], from: ['Assessment period start', 'Değerlendirme dönemi başlangıcı'], to: ['Assessment period end (exclusive)', 'Değerlendirme dönemi sonu (hariç)'], criterion: ['Source criterion code', 'Kaynak kriter kodu'], recordReference: ['Qualified assessment source reference', 'Nitelikli değerlendirme kaynak referansı'], satisfied: ['Source criterion outcome', 'Kaynak kriter sonucu'] };
    return <fieldset disabled={offline}><legend>{localizedText(language, "pairs.m_e02c89c92ec24358")}</legend><p>{localizedText(language, "pairs.m_d14ca1a8d51767b1")}</p>
    <label>{localizedText(language, "pairs.m_63dc6d3e0c177c8b")}<select value={key ?? ''} onChange={e => choose(e.target.value)}><option value="">{localizedText(language, "pairs.m_0edb8c6e3e366a7c")}</option><option value="classPack">{localizedText(language, "pairs.m_b255563d2a91d160")}</option><option value="msmd">{localizedText(language, "pairs.m_deb2cf9acd6915f0")}</option><option value="crewRoleAssessment">{localizedText(language, "pairs.m_9e21ed5b97d839d5")}</option></select></label>
    {key && record && Object.entries(record).filter(([field]) => field !== 'roles' && field !== 'credential').map(([field, v]) => <label key={field}>{t(...labels[field])}{['iacsM79', 'satisfied'].includes(field) ? <select value={v === null ? '' : String(v)} onChange={e => onChange({ ...value, [key]: { ...record, [field]: e.target.value === '' ? null : e.target.value === 'true' } } as Content)}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="true">{localizedText(language, "pairs.m_b0df697bd7b8694e")}</option><option value="false">{localizedText(language, "pairs.m_63083b11c5b758e1")}</option></select> : <input type={field === 'from' || field === 'to' ? 'date' : field.endsWith('Version') || field.endsWith('Revision') ? 'number' : 'text'} min={1} maxLength={2000} value={v === null ? '' : String(v)} onChange={e => onChange({ ...value, [key]: { ...record, [field]: !e.target.value ? null : field.endsWith('Version') || field.endsWith('Revision') ? Number(e.target.value) : e.target.value } } as Content)}/>}</label>)}
    {value.crewRoleAssessment && <><button type="button" onClick={() => onChange({ ...value, crewRoleAssessment: { ...value.crewRoleAssessment!, credential: value.crewRoleAssessment!.credential ? null : { id: '', revision: 0, version: 0 } } })}>{localizedText(language, "pairs.m_57561f93046a7677")}</button>
      {value.crewRoleAssessment.credential && (['id', 'revision', 'version'] as const).map(k => <label key={k}>{k}<input type={k === 'id' ? 'text' : 'number'} min={1} value={value.crewRoleAssessment!.credential![k]} onChange={e => onChange({ ...value, crewRoleAssessment: { ...value.crewRoleAssessment!, credential: { ...value.crewRoleAssessment!.credential!, [k]: k === 'id' ? e.target.value : Number(e.target.value) } } })}/></label>)}</>}
    {value.msmd && <><button type="button" onClick={() => onChange({ ...value, msmd: { ...value.msmd!, roles: [...(value.msmd!.roles ?? []), { positionId: '', count: 1, sourceLocator: '' }] } })}>{localizedText(language, "pairs.m_2b0ee31bd24ebadd")}</button>
      {(value.msmd.roles ?? []).map((r, i) => <div key={i}>{(['positionId', 'count', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input type={k === 'count' ? 'number' : 'text'} min={1} value={r[k]} onChange={e => onChange({ ...value, msmd: { ...value.msmd!, roles: value.msmd!.roles!.map((x, n) => n === i ? { ...x, [k]: k === 'count' ? Number(e.target.value) : e.target.value } : x) } })}/></label>)}<button type="button" onClick={() => onChange({ ...value, msmd: { ...value.msmd!, roles: value.msmd!.roles!.filter((_, n) => n !== i) } })}>{localizedText(language, "pairs.m_f6e7e1c66184a692")}</button></div>)}</>}
  </fieldset>;
}
export function ComplianceEvaluationSelector({ value, onChange, offline, language }: {
    value: OwningReferences | null;
    onChange: (v: OwningReferences | null) => void;
    offline: boolean;
    language: AppLocale;
}) {
    const update = (v: Partial<OwningReferences>) => onChange({ operationId: null, cargoPlanId: null, winchFitmentId: null, ...value, ...v });
    return <fieldset disabled={offline}><legend>{localizedText(language, "pairs.m_a1160f55ab8a5c71")}</legend><p>{localizedText(language, "pairs.m_4828caa5d42e78d5")}</p>
    {(['classPackId', 'msmdId', 'sourceEvidenceId'] as const).map((k, i) => <label key={k}>{[localizedText(language, "pairs.m_f8813d03b08a0846"), localizedText(language, "pairs.m_a9a23777c88bf89e"), localizedText(language, "pairs.m_b8fbfac99a3660f1")][i]}<input value={value?.[k] ?? ''} onChange={e => update({ [k]: e.target.value || null })}/></label>)}
    <label>{localizedText(language, "pairs.m_f332a474e7ac0756")}<textarea value={value?.roleAssessmentIds?.join('\n') ?? ''} onChange={e => update({ roleAssessmentIds: e.target.value ? e.target.value.split('\n') : null })}/></label>
    {(['evaluationId', 'ruleId', 'ruleRevision'] as const).map((k, i) => <label key={k}>{[localizedText(language, "pairs.m_396de12ea59201a1"), localizedText(language, "pairs.m_500132af606d7b96"), localizedText(language, "pairs.m_2be609e43c1f4841")][i]}<input value={value?.ism?.[k] ?? ''} type={k === 'ruleRevision' ? 'number' : 'text'} min={1} onChange={e => update({ ism: { evaluationId: '', ruleId: '', ruleRevision: 0, ...value?.ism, [k]: k === 'ruleRevision' ? Number(e.target.value) : e.target.value } })}/></label>)}
    <button type="button" onClick={() => update({ classPackId: null, msmdId: null, sourceEvidenceId: null, roleAssessmentIds: null, ism: null })}>{localizedText(language, "pairs.m_16b139f3de883303")}</button>
  </fieldset>;
}
