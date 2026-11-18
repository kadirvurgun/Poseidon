import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns';
import { shipOpsGroups, type ShipOpsFact, type ShipOpsFieldDefinition, type ShipOpsFields, type ShipOpsKind } from './ship-operation-fields';
import { shipOpsFactLabel, shipOpsOptionLabel } from './ship-operation-copy';
import './ship-operations.css';
export function ShipOperationsFields({ kind, fields, schema, language, disabled, errors = {}, onChange }: {
    kind: ShipOpsKind;
    fields: ShipOpsFields;
    schema: ShipOpsFieldDefinition[];
    language: AppLocale;
    disabled: boolean;
    errors?: Record<string, string[]>;
    onChange: (fields: ShipOpsFields) => void;
}) {
    const id = useId(), en = language === 'en';
    return <div className="ship-operations-fields">{shipOpsGroups[kind].map(group => <EnterpriseFormGrid key={group.en} disabled={disabled}>
    <legend>{localizedLabel(group, en)}</legend>
    {group.fields.filter(info => !info.readOnly).map(info => {
                const definition = schema.find(field => field.key === info.key), fieldId = id + '-' + info.key, fact = fields[info.key] ?? { state: 'Missing', value: null };
                if (!definition || definition.readOnly || definition.type !== info.type)
                    return <p key={info.key} role="alert">{localizedText(en, "pairs.m_b7340907a3a5b978")}{localizedLabel(info, en)}</p>;
                const label = (localizedLabel(info, en)) + (definition.unit ? ' (' + definition.unit + ')' : ''), error = !!(errors[info.key]?.length || errors['fields.' + info.key]?.length);
                const blocked = disabled || definition.referenceUnavailable || fact.state !== 'Known';
                const update = (value: ShipOpsFact) => { if (!disabled)
                    onChange({ ...fields, [info.key]: value }); };
                const attributes = { id: fieldId, disabled: blocked, value: fact.value ?? '', 'aria-invalid': error, 'aria-describedby': fieldId + '-help' + (error ? ' ' + fieldId + '-error' : ''), onChange: (event: {
                        target: {
                            value: string;
                        };
                    }) => update({ state: 'Known', value: event.target.value }) };
                return <div key={info.key} className={'ship-operations-field' + (definition.type === 'Narrative' ? ' ship-operations-field-wide' : '')}>
        <div className="ship-operations-field-heading"><span>{label}</span><small>{definition.required ? (localizedText(en, "pairs.m_f8c48b8849896e81")) : (localizedText(en, "pairs.m_9a8f0c898d0722cd"))}</small></div>
        <EnterpriseField htmlFor={fieldId + '-state'} label={label + (localizedText(en, "pairs.m_08c5c606755993dd"))}>
          <select id={fieldId + '-state'} disabled={disabled} value={fact.state} onChange={event => {
                        const state = event.target.value;
                        if (state === 'Missing' || state === 'Unknown')
                            update({ state, value: null });
                        else if (state === 'Known' && !definition.referenceUnavailable)
                            update({ state: 'Known', value: fact.value ?? '' });
                    }}><option value="Missing">{shipOpsFactLabel('Missing', en)}</option><option value="Unknown">{shipOpsFactLabel('Unknown', en)}</option><option value="Known" disabled={definition.referenceUnavailable}>{shipOpsFactLabel('Known', en)}</option></select>
        </EnterpriseField>
        <EnterpriseField htmlFor={fieldId} label={label}>
          {definition.type === 'Narrative' ? <textarea {...attributes} maxLength={4000} rows={4}/> : definition.type === 'Code' ? <select {...attributes}><option value="">{localizedText(en, "pairs.m_91490d057aef40c8")}</option>{definition.options.map(option => <option key={option} value={option}>{shipOpsOptionLabel(option, en)}</option>)}</select> : <input {...attributes} type="text" inputMode={definition.type === 'Number' ? 'decimal' : undefined} maxLength={definition.type === 'Number' ? 48 : 512} placeholder={definition.type === 'Instant' ? '2026-11-05T12:00:00Z' : undefined}/>}
          <p className="ship-operations-hint" id={fieldId + '-help'}>{definition.referenceUnavailable ? (localizedText(en, "pairs.m_f0024540c51704ad")) : definition.type === 'Number' ? (localizedText(en, "pairs.m_582b0aa6b5c6eb08")) : definition.type === 'Instant' ? (localizedText(en, "pairs.m_2350ca1f32600313")) : (localizedText(en, "pairs.m_11e0b8c8f943cee0"))}</p>
          {error && <p className="field-error" id={fieldId + '-error'}>{localizedText(en, "pairs.m_af9d7f28266f0409")}</p>}
        </EnterpriseField>
      </div>;
            })}
  </EnterpriseFormGrid>)}</div>;
}
