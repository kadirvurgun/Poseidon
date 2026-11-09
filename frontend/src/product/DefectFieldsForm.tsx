import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns';
import { defectCodeLabel, defectFactLabel, defectGroups, type DefectFact, type DefectFieldDefinition, type DefectFields } from './defect-fields';
import './defect-workspace.css';
export function DefectFieldsForm({ fields, schema, language, disabled, errors = {}, onChange }: {
    fields: DefectFields;
    schema: DefectFieldDefinition[];
    language: AppLocale;
    disabled: boolean;
    errors?: Record<string, string[]>;
    onChange: (fields: DefectFields) => void;
}) {
    const id = useId(), en = language === 'en';
    return <div className="defect-fields"><p className="defect-hint">{localizedText(en, "pairs.m_5778670da810c34b")}</p>{defectGroups.map(group => <EnterpriseFormGrid key={group.en} disabled={disabled}><legend>{localizedLabel(group, en)}</legend>{group.fields.map(info => {
                const field = schema.find(field => field.key === info.key), fact = fields[info.key], label = localizedLabel(info, en), fieldId = id + '-' + info.key;
                if (!field)
                    return <p role="alert" key={info.key}>{localizedText(en, "pairs.m_31bd810f3ae41f5a")}{label}</p>;
                const invalid = !!(errors[info.key]?.length || errors['fields.' + info.key]?.length), update = (value: DefectFact) => { if (!disabled)
                    onChange({ ...fields, [info.key]: value }); };
                const hint = field.type === 'Instant' ? (localizedText(en, "pairs.m_98306c26efd23750")) : info.key === 'lifecycle_stage' ? (localizedText(en, "pairs.m_b1865e282245fbce")) : null;
                const input = { id: fieldId, value: fact.value ?? '', disabled: disabled || fact.state !== 'Known', 'aria-invalid': invalid, 'aria-describedby': [hint ? fieldId + '-help' : '', invalid ? fieldId + '-error' : ''].filter(Boolean).join(' ') || undefined, onChange: (event: {
                        target: {
                            value: string;
                        };
                    }) => update({ state: 'Known', value: event.target.value }) };
                return <div key={info.key} className={'defect-field' + (field.type === 'Narrative' ? ' defect-field-wide' : '')}>
      <EnterpriseField htmlFor={fieldId + '-state'} label={label + (localizedText(en, "pairs.m_08c5c606755993dd"))}><select id={fieldId + '-state'} value={fact.state} disabled={disabled} onChange={event => { const state = event.target.value; if (state === 'Missing' || state === 'Unknown')
                    update({ state, value: null });
                else if (state === 'Known')
                    update({ state, value: fact.value ?? '' }); }}><option value="Missing">{defectFactLabel('Missing', en)}</option><option value="Unknown">{defectFactLabel('Unknown', en)}</option><option value="Known">{defectFactLabel('Known', en)}</option></select></EnterpriseField>
      <EnterpriseField htmlFor={fieldId} label={label}>{field.type === 'Narrative' ? <textarea {...input} maxLength={4000} rows={3}/> : field.type === 'Code' ? <select {...input}><option value="">{localizedText(en, "pairs.m_5937f0fbabdf68cc")}</option>{field.options.map(option => <option key={option} value={option}>{defectCodeLabel(option, en)}</option>)}</select> : <input {...input} type="text" maxLength={field.type === 'Instant' ? 40 : 256} placeholder={field.type === 'Instant' ? '2026-11-06T12:00:00Z' : undefined}/>}
        {hint && <p id={fieldId + '-help'} className="defect-hint">{hint}</p>}
        {invalid && <p id={fieldId + '-error'} className="field-error">{localizedText(en, "pairs.m_11d432b5561ce971")}</p>}
      </EnterpriseField>
    </div>;
            })}</EnterpriseFormGrid>)}</div>;
}
