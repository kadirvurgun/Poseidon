import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { CompliancePolicies } from './compliance-input-contract';
export function CompliancePolicyEditor({ value, onChange, language }: {
    value: CompliancePolicies;
    onChange: (v: CompliancePolicies) => void;
    language: AppLocale;
}) {
    return <fieldset><legend>{localizedText(language, "pairs.m_fe5a97d9b8b6eac0")}</legend>
    <p>{localizedText(language, "pairs.m_da956ac89a353be3")}</p>
    <button type="button" onClick={() => onChange({ classPack: value.classPack ? null : { packReference: null, edition: null, sourceLocator: null } })}>{localizedText(language, "pairs.m_374ee89ebe455b30")}</button>
    {value.classPack && (['packReference', 'edition', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input value={value.classPack?.[k] ?? ''} onChange={e => onChange({ classPack: { ...value.classPack!, [k]: e.target.value || null } })}/></label>)}
    <button type="button" onClick={() => onChange({ ismDependency: value.ismDependency ? null : { ruleId: null, ruleRevision: null, sourceLocator: null } })}>{localizedText(language, "pairs.m_058ce0bf82fea0e2")}</button>
    {value.ismDependency && (['ruleId', 'ruleRevision', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input type={k === 'ruleRevision' ? 'number' : 'text'} min={1} value={value.ismDependency?.[k] ?? ''} onChange={e => onChange({ ismDependency: { ...value.ismDependency!, [k]: !e.target.value ? null : k === 'ruleRevision' ? Number(e.target.value) : e.target.value } })}/></label>)}
    <button type="button" onClick={() => onChange({ eligibility: value.eligibility ? null : { positionId: null, sourceLocator: null, criteria: [] } })}>{localizedText(language, "pairs.m_554de91b1b649ca5")}</button>
    {value.eligibility && <>{(['positionId', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input value={value.eligibility?.[k] ?? ''} onChange={e => onChange({ eligibility: { ...value.eligibility!, [k]: e.target.value || null } })}/></label>)}
      {(value.eligibility.criteria ?? []).map((r, i) => <div key={i}><label>{localizedText(language, "pairs.m_20a8a69e9d5b2b73")}<select value={r.requireCredential === null ? '' : String(r.requireCredential)} onChange={e => onChange({ eligibility: { ...value.eligibility!, criteria: value.eligibility!.criteria!.map((x, n) => n === i ? { ...x, requireCredential: e.target.value === '' ? null : e.target.value === 'true' } : x) } })}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="true">{localizedText(language, "pairs.m_b0df697bd7b8694e")}</option><option value="false">{localizedText(language, "pairs.m_63083b11c5b758e1")}</option></select></label>{(['code', 'ruleId', 'ruleRevision', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input value={r[k] ?? ''} type={k === 'ruleRevision' ? 'number' : 'text'} min={1} onChange={e => onChange({ eligibility: { ...value.eligibility!, criteria: value.eligibility!.criteria!.map((x, n) => n === i ? { ...x, [k]: !e.target.value ? null : k === 'ruleRevision' ? Number(e.target.value) : e.target.value } : x) } })}/></label>)}<button type="button" onClick={() => onChange({ eligibility: { ...value.eligibility!, criteria: value.eligibility!.criteria!.filter((_, n) => n !== i) } })}>{localizedText(language, "pairs.m_3d084b44788badca")}</button></div>)}
      <button type="button" onClick={() => onChange({ eligibility: { ...value.eligibility!, criteria: [...(value.eligibility!.criteria ?? []), { code: null, ruleId: null, ruleRevision: null, sourceLocator: null, requireCredential: null }] } })}>{localizedText(language, "pairs.m_94c4d8339b16ba4a")}</button></>}
  </fieldset>;
}
