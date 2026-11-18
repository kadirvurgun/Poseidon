import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns';
import { downtimeCodeLabel, downtimeFactLabel, downtimeGroups, type DowntimeFact, type DowntimeFieldDefinition, type DowntimeFields } from './downtime-fields';
import './downtime-workspace.css';
export function DowntimeFieldsForm({ fields, schema, language, disabled, errors = {}, onChange }: {
    fields: DowntimeFields;
    schema: DowntimeFieldDefinition[];
    language: AppLocale;
    disabled: boolean;
    errors?: Record<string, string[]>;
    onChange: (fields: DowntimeFields) => void;
}) {
    const id = useId(), en = language === 'en';
    return <div className="downtime-fields"><p className="downtime-hint">{localizedText(en, "pairs.m_06259a1559d7bbff")}</p>{downtimeGroups.map(group => <EnterpriseFormGrid key={group.en} disabled={disabled}><legend>{localizedLabel(group, en)}</legend>{group.fields.map(info => {
                const field = schema.find(field => field.key === info.key), fact = fields[info.key], label = localizedLabel(info, en), fieldId = id + '-' + info.key;
                if (!field)
                    return <p role="alert" key={info.key}>{localizedText(en, "pairs.m_31bd810f3ae41f5a")}{label}</p>;
                const invalid = !!(errors[info.key]?.length || errors['fields.' + info.key]?.length), update = (value: DowntimeFact) => { if (!disabled)
                    onChange({ ...fields, [info.key]: value }); };
                const hint = field.type === 'Instant' ? (localizedText(en, "pairs.m_05c5d3dbc20e638c")) : field.type === 'Number' ? (localizedText(en, "pairs.m_27947dcf8e1c7ebb")) : null;
                const input = { id: fieldId, value: fact.value ?? '', disabled: disabled || fact.state !== 'Known' || field.referenceUnavailable, 'aria-invalid': invalid, 'aria-describedby': [hint ? fieldId + '-help' : '', invalid ? fieldId + '-error' : ''].filter(Boolean).join(' ') || undefined, onChange: (event: {
                        target: {
                            value: string;
                        };
                    }) => update({ state: 'Known', value: event.target.value }) };
                return <div key={info.key} className={'downtime-field' + (field.type === 'Narrative' ? ' downtime-field-wide' : '')}>
      <EnterpriseField htmlFor={fieldId + '-state'} label={label + (localizedText(en, "pairs.m_08c5c606755993dd"))}><select id={fieldId + '-state'} value={fact.state} disabled={disabled} onChange={event => { const state = event.target.value; if (state === 'Missing' || state === 'Unknown')
                    update({ state, value: null });
                else if (state === 'Known' && !field.referenceUnavailable)
                    update({ state, value: fact.value ?? '' }); }}><option value="Missing">{downtimeFactLabel('Missing', en)}</option><option value="Unknown">{downtimeFactLabel('Unknown', en)}</option><option value="Known" disabled={field.referenceUnavailable}>{downtimeFactLabel('Known', en)}</option></select></EnterpriseField>
      <EnterpriseField htmlFor={fieldId} label={label}>{field.type === 'Narrative' ? <textarea {...input} maxLength={4000} rows={3}/> : field.type === 'Code' ? <select {...input}><option value="">{localizedText(en, "pairs.m_5937f0fbabdf68cc")}</option>{field.options.map(option => <option key={option} value={option}>{downtimeCodeLabel(option, en)}</option>)}</select> : <input {...input} type="text" inputMode={field.type === 'Number' ? 'decimal' : undefined} maxLength={field.type === 'Number' ? 48 : 256}/>}
        {hint && <p id={fieldId + '-help'} className="downtime-hint">{hint}</p>}{invalid && <p id={fieldId + '-error'} className="field-error">{localizedText(en, "pairs.m_a6e667fd999725c3")}</p>}
      </EnterpriseField>
    </div>;
            })}</EnterpriseFormGrid>)}</div>;
}
