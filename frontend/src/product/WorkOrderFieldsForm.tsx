import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns';
import { workOrderCodeLabel, workOrderFactLabel, workOrderGroups, type WorkOrderFact, type WorkOrderFieldDefinition, type WorkOrderFields } from './work-order-fields';
import './work-order-workspace.css';
export function WorkOrderFieldsForm({ fields, schema, language, disabled, errors = {}, onChange }: {
    fields: WorkOrderFields;
    schema: WorkOrderFieldDefinition[];
    language: AppLocale;
    disabled: boolean;
    errors?: Record<string, string[]>;
    onChange: (fields: WorkOrderFields) => void;
}) {
    const id = useId(), en = language === 'en';
    return <div className="work-order-fields"><p className="work-order-hint">{localizedText(en, "pairs.m_b9d4cf378d86a52e")}</p>{workOrderGroups.map(group => <EnterpriseFormGrid key={group.en} disabled={disabled}><legend>{localizedLabel(group, en)}</legend>{group.fields.map(info => {
                const field = schema.find(field => field.key === info.key), fact = fields[info.key], label = localizedLabel(info, en), fieldId = id + '-' + info.key;
                if (!field)
                    return <p role="alert" key={info.key}>{localizedText(en, "pairs.m_31bd810f3ae41f5a")}{label}</p>;
                const invalid = !!(errors[info.key]?.length || errors['fields.' + info.key]?.length), update = (value: WorkOrderFact) => { if (!disabled)
                    onChange({ ...fields, [info.key]: value }); };
                const hint = field.knownUnavailable ? (field.type === 'SourceConflict' ? (localizedText(en, "pairs.m_ace004c01a7af351")) : (localizedText(en, "pairs.m_2c043f0afee2d159"))) : field.type === 'Date' ? (localizedText(en, "pairs.m_553453ef86b7939f")) : field.type === 'Number' ? (localizedText(en, "pairs.m_b9bbaa308c990dbb")) : info.key === 'execution_stage' || info.key === 'verification_result' ? (localizedText(en, "pairs.m_8ceb5f768c332c06")) : field.required || field.requiredWhen ? (localizedText(en, "pairs.m_7efb039c0539952f")) : null;
                const input = { id: fieldId, value: fact.value ?? '', disabled: disabled || fact.state !== 'Known' || field.knownUnavailable, 'aria-invalid': invalid, 'aria-describedby': [hint ? fieldId + '-help' : '', invalid ? fieldId + '-error' : ''].filter(Boolean).join(' ') || undefined, onChange: (event: {
                        target: {
                            value: string;
                        };
                    }) => update({ state: 'Known', value: event.target.value }) };
                return <div key={info.key} className={'work-order-field' + (field.type === 'Narrative' ? ' work-order-field-wide' : '')}>
      <EnterpriseField htmlFor={fieldId + '-state'} label={label + (localizedText(en, "pairs.m_08c5c606755993dd"))}><select id={fieldId + '-state'} value={fact.state} disabled={disabled} onChange={event => { const state = event.target.value; if (state === 'Missing' || state === 'Unknown')
                    update({ state, value: null });
                else if (state === 'Known' && !field.knownUnavailable)
                    update({ state, value: fact.value ?? '' }); }}><option value="Missing">{workOrderFactLabel('Missing', en)}</option><option value="Unknown">{workOrderFactLabel('Unknown', en)}</option><option value="Known" disabled={field.knownUnavailable}>{workOrderFactLabel('Known', en)}</option></select></EnterpriseField>
      <EnterpriseField htmlFor={fieldId} label={label}>{field.type === 'Narrative' ? <textarea {...input} maxLength={4000} rows={3}/> : field.type === 'Code' ? <select {...input}><option value="">{localizedText(en, "pairs.m_5937f0fbabdf68cc")}</option>{field.options.map(option => <option key={option} value={option}>{workOrderCodeLabel(option, en)}</option>)}</select> : <input {...input} type={field.type === 'Date' ? 'date' : 'text'} inputMode={field.type === 'Number' ? 'decimal' : undefined} maxLength={field.type === 'Number' ? 48 : 256}/>}
        {hint && <p id={fieldId + '-help'} className="work-order-hint">{hint}</p>}{invalid && <p id={fieldId + '-error'} className="field-error">{localizedText(en, "pairs.m_a6e667fd999725c3")}</p>}
      </EnterpriseField>
    </div>;
            })}</EnterpriseFormGrid>)}</div>;
}
