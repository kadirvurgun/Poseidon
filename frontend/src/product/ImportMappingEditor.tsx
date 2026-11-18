import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import './import-mapping-editor.css';
// The owning workspace adapts generated API DTOs to this presentation model.
// Descriptors originate from the authorized server, never from uploaded headers.
export type ImportMappingField = Readonly<{
    key: string;
    label: string;
    required: boolean;
    writable: boolean;
}>;
export type ImportMappingBinding = Readonly<{
    targetField: string;
    sourceColumn: number;
}>;
export type ImportMappingTemplate = Readonly<{
    version: string;
    fingerprint: string;
    fields: readonly ImportMappingField[];
    allowSourceColumnReuse: boolean;
}>;
type Props = {
    language: AppLocale;
    currentContextKey: string;
    loadedContextKey: string;
    state: 'loading' | 'ready' | 'denied' | 'unavailable';
    template: ImportMappingTemplate | null;
    headers: readonly string[];
    bindings: readonly ImportMappingBinding[];
    busy?: boolean;
    failure?: 'draft' | 'preview' | null;
    onChange: (bindings: readonly ImportMappingBinding[]) => void;
    onSaveDraft: () => void;
    onPreview: () => void;
    onRetry?: () => void;
};
// A controlled editor: it neither caches private mappings nor commits domain rows.
// Context keys must cover actor/session, tenant, vessel, upload and schema revision.
export function ImportMappingEditor(props: Props) {
    const en = props.language === 'en';
    if (props.state === 'loading')
        return <p role="status">{localizedText(en, "pairs.m_9cd251ec2526e8b6")}</p>;
    if (props.state === 'denied')
        return <p role="alert">{localizedText(en, "pairs.m_50dc62bed2d57e74")}</p>;
    if (props.state !== 'ready')
        return <div role="alert">
    <p>{localizedText(en, "pairs.m_0ad46399348d2ad7")}</p>
    {props.onRetry && <button type="button" onClick={props.onRetry}>{localizedText(en, "pairs.m_044bf27da41fda96")}</button>}
  </div>;
    if (!props.currentContextKey || props.loadedContextKey !== props.currentContextKey)
        return <p role="status">
    {localizedText(en, "pairs.m_5973c890bee9cf6f")}
  </p>;
    const template = props.template;
    if (!template || !template.version || !template.fingerprint || !Array.isArray(template.fields) ||
        template.fields.length === 0 || template.fields.some(field => !field || !field.key ||
        !field.label || /\p{Cc}/u.test(field.key) || field.required && !field.writable) ||
        new Set(template.fields.map(field => field.key)).size !== template.fields.length)
        return <p role="alert">
    {localizedText(en, "pairs.m_5129055bd6f4012a")}
  </p>;
    return <MappingFields key={props.currentContextKey} {...props} template={template}/>;
}
function MappingFields({ language, template, headers, bindings, busy, failure, onChange, onPreview, onSaveDraft }: Props & {
    template: ImportMappingTemplate;
}) {
    const prefix = useId(), en = language === 'en';
    const writable = new Set(template.fields.filter(field => field.writable).map(field => field.key));
    const mappedSources = new Map<number, number>();
    for (const binding of bindings)
        if (binding && Number.isInteger(binding.sourceColumn)) {
            mappedSources.set(binding.sourceColumn, (mappedSources.get(binding.sourceColumn) ?? 0) + 1);
        }
    const unknownMapping = bindings.some(binding => !binding || !writable.has(binding.targetField));
    const problems = template.fields.map(field => {
        const choices = bindings.filter(binding => binding?.targetField === field.key);
        if (choices.length > 1)
            return localizedText(en, "pairs.m_493c348574e894f8");
        if (choices.length === 0)
            return field.required ? (localizedText(en, "pairs.m_d0c7887dbe32d183")) : null;
        const column = choices[0].sourceColumn;
        if (!field.writable || !Number.isInteger(column) || column < 0 || column >= headers.length)
            return localizedText(en, "pairs.m_ece1bd0c6161e5f4");
        if (!template.allowSourceColumnReuse && (mappedSources.get(column) ?? 0) > 1)
            return localizedText(en, "pairs.m_1d87e3a6509a26c4");
        return null;
    });
    const invalid = unknownMapping || problems.some(Boolean);
    function change(targetField: string, value: string) {
        if (busy)
            return;
        const rest = bindings.filter(binding => binding?.targetField !== targetField);
        onChange(value === '' ? rest : [...rest, { targetField, sourceColumn: Number(value) }]);
    }
    return <section className="import-mapping" aria-label={localizedText(en, "pairs.m_3ca5cad594f63994")} aria-busy={busy || undefined}>
    <h3>{localizedText(en, "pairs.m_6f56a6971b8a89c4")}</h3>
    <p>{localizedText(en, "pairs.m_f4ab4b3c859b1b5d")}: <bdi>{template.version}</bdi></p>
    <p id={`${prefix}-help`}>{localizedText(en, "pairs.m_c1ca2ab27356bb16")}</p>
    {headers.length === 0 && <p role="status">{localizedText(en, "pairs.m_7111c3365b681548")}</p>}
    {unknownMapping && <p role="alert">{localizedText(en, "pairs.m_4ce6afc82e449ab3")}</p>}
    <fieldset disabled={busy} aria-describedby={`${prefix}-help`}>
      <legend>{localizedText(en, "pairs.m_f952422662e36fb2")}</legend>
      {template.fields.map((field, index) => {
            const id = `${prefix}-field-${index}`, issueId = `${id}-issue`;
            const choices = bindings.filter(binding => binding?.targetField === field.key);
            const value = choices.length === 1 && Number.isInteger(choices[0].sourceColumn) &&
                choices[0].sourceColumn >= 0 && choices[0].sourceColumn < headers.length ? String(choices[0].sourceColumn) : '';
            return <div className="import-mapping__field" key={field.key}>
          {field.writable ? <>
            <label htmlFor={id}>{field.label}{field.required ? (localizedText(en, "pairs.m_bc50313e087919bc")) : ''}</label>
            <select id={id} value={value} onChange={event => change(field.key, event.target.value)} aria-required={field.required || undefined} aria-invalid={problems[index] ? true : undefined} aria-describedby={problems[index] ? issueId : undefined}>
              <option value="">{localizedText(en, "pairs.m_692d0d1785a284dc")}</option>
              {headers.map((header, column) => <option key={column} value={column}>
                {column + 1} — {header.trim() || (localizedText(en, "pairs.m_d9fbf804b869032a"))}
              </option>)}
            </select>
          </> : <p>{field.label} — {localizedText(en, "pairs.m_1a21e752656253fe")}</p>}
          {problems[index] && <p className="import-mapping__issue" id={issueId}>{problems[index]}</p>}
        </div>;
        })}
    </fieldset>
    {failure && <p role="alert">{failure === 'draft'
                ? (localizedText(en, "pairs.m_639725270de5cc9b"))
                : (localizedText(en, "pairs.m_b368effa6005050d"))}</p>}
    <div className="import-mapping__actions">
      <button type="button" disabled={busy} onClick={onSaveDraft}>{localizedText(en, "pairs.m_fb77eabc157b31ea")}</button>
      <button type="button" disabled={busy || invalid || headers.length === 0} onClick={onPreview}>{localizedText(en, "pairs.m_b2aaf5d89c81a490")}</button>
    </div>
  </section>;
}
