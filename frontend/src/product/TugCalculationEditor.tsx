import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { blankCalculation, type CalculationPolicy } from './tug-capability-contract';
export function TugCalculationEditor({ value, onChange, language }: {
    value: CalculationPolicy | null;
    onChange: (v: CalculationPolicy | null) => void;
    language: AppLocale;
}) {
    const patch = (p: Partial<CalculationPolicy>) => onChange({ ...(value ?? blankCalculation()), ...p });
    return <fieldset><legend>{localizedText(language, "pairs.m_e87d20206610498f")}</legend>
    <label>{localizedText(language, "pairs.m_07c59b58ccd70809")}<select value={value ? 'tug' : ''} onChange={e => onChange(e.target.value ? blankCalculation() : null)}><option value="">{localizedText(language, "pairs.m_42667eee3f85f42c")}</option><option value="tug">{localizedText(language, "pairs.m_3ab8931c37ea1407")}</option></select></label>
    {value && <>
      <p>{localizedText(language, "pairs.m_2f5644bcc33e74a7")}</p>
      <label>{localizedText(language, "pairs.m_c3a68f7f06c0640b")}<select value={value.formula ?? ''} onChange={e => patch({ formula: e.target.value as CalculationPolicy['formula'] || null })}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="MeasuredTimesVerifiedCondition">{localizedText(language, "pairs.m_8aea525336a21449")}</option></select></label>
      <label>{localizedText(language, "pairs.m_53b42a6a66d1ae4b")}<select value={value.aggregation ?? ''} onChange={e => patch({ aggregation: e.target.value as CalculationPolicy['aggregation'] || null })}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="SumForPlanning">{localizedText(language, "pairs.m_c2efe40c14b2e764")}</option></select></label>
      <label>{localizedText(language, "pairs.m_e61d107bcac7c7ed")}<select value={value.rounding ?? ''} onChange={e => patch({ rounding: e.target.value as CalculationPolicy['rounding'] || null })}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="Floor">{localizedText(language, "pairs.m_72124e543ac60554")}</option></select></label>
      {(['sourceLocator', 'maxLossPercent'] as const).map((key, i) => <label key={key}>{[localizedText(language, "pairs.m_a995d42ca27deb9a"), localizedText(language, "pairs.m_21249e9ca17d6ce1")][i]}<input value={value[key] ?? ''} onChange={e => patch({ [key]: e.target.value || null })}/></label>)}
      {(['scale', 'maxTestAgeDays'] as const).map((key, i) => <label key={key}>{[localizedText(language, "pairs.m_f83d7b6a1d1dea3d"), localizedText(language, "pairs.m_366251a94a7dee3c")][i]}<input type="number" value={value[key] ?? ''} onChange={e => patch({ [key]: e.target.value === '' ? null : Number(e.target.value) })}/></label>)}
      {(['jobTypes', 'areas'] as const).map((key, i) => <label key={key}>{[localizedText(language, "pairs.m_4308c017e7c4759f"), localizedText(language, "pairs.m_ea68e7e1177a4599")][i]}<input value={value[key]?.join(';') ?? ''} onChange={e => patch({ [key]: e.target.value ? e.target.value.split(';') : null })}/></label>)}
    </>}
  </fieldset>;
}
