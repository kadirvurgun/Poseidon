import { presentationLabel } from "../i18n/labels";
import type { PresentationLabel } from "../i18n/labels";
import { localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import { dailyGroups, type DailyField, type DailyFields, type DailyKind, type DailyValue } from './daily-report-fields';
import type { DailyReferenceOption } from './daily-operations-api';
import './daily-operations.css';
// The server schema controls unresolved references. This component never creates
// an approved lookup, review status or implicit Known default.
export function DailyReportFields({ kind, fields, language, disabled, unavailableKeys, requiredKeys = [], errors, units, maxLengths, narrativeKeys, referenceOptions, onChange }: {
    kind: DailyKind;
    fields: DailyFields;
    language: AppLocale;
    disabled: boolean;
    unavailableKeys: readonly string[];
    requiredKeys?: readonly string[];
    errors: Record<string, string[]>;
    units?: Record<string, string | null>;
    maxLengths?: Record<string, number>;
    narrativeKeys?: readonly string[];
    referenceOptions?: Readonly<Record<string, readonly DailyReferenceOption[]>>;
    onChange: (key: string, value: DailyValue) => void;
}) {
    return <>{dailyGroups[kind].map(group => <fieldset className="daily-field-group" key={group.en}><legend>{localizedLabel(group, language)}</legend>{group.fields.map(field => <DailyFieldInput key={field.key} field={field} value={fields[field.key] ?? { state: 'Missing', value: null }} language={language} disabled={disabled} unavailable={unavailableKeys.includes(field.key)} required={requiredKeys.includes(field.key)} error={!!errors[field.key]?.length} unit={units?.[field.key]} maximum={maxLengths?.[field.key] ?? 512} multiline={narrativeKeys ? narrativeKeys.includes(field.key) : field.kind === 'narrative'} references={field.kind === 'reference' ? referenceOptions?.[field.key] : undefined} onChange={value => onChange(field.key, value)}/>)}</fieldset>)}</>;
}
function DailyFieldInput({ field, value, language, disabled, unavailable, required, error, unit, maximum, multiline, references, onChange }: {
    field: DailyField;
    value: DailyValue;
    language: AppLocale;
    disabled: boolean;
    unavailable: boolean;
    required: boolean;
    error: boolean;
    unit?: string | null;
    maximum: number;
    multiline: boolean;
    references?: readonly DailyReferenceOption[];
    onChange: (value: DailyValue) => void;
}) {
    const id = useId(), en = language === 'en', label = localizedLabel(field, language), description = [error ? `${id}-error` : '', unavailable ? `${id}-source` : '', field.kind === 'number' ? `${id}-format` : ''].filter(Boolean).join(' ') || undefined;
    const common = { id, disabled: disabled || unavailable || value.state !== 'Known', 'aria-invalid': error, 'aria-required': required, 'aria-describedby': description, value: value.value ?? '', onChange: (e: {
            target: {
                value: string;
            };
        }) => onChange({ state: 'Known', value: e.target.value }) };
    return <div className={`daily-field${field.kind === 'narrative' ? ' daily-field-wide' : ''}`}><label htmlFor={id}>{label}</label><select aria-label={`${label} — ${localizedText(en, "pairs.m_f3724d2d3be86863")}`} aria-invalid={error} aria-describedby={description} disabled={disabled} value={value.state} onChange={e => onChange({ state: e.target.value as DailyValue['state'], value: e.target.value === 'Known' ? '' : null })}><option value="Missing">{localizedText(en, "pairs.m_a5bd35d90de7c160")}</option><option value="Unknown">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option><option value="Known" disabled={unavailable}>{localizedText(en, "pairs.m_23f42eba6cf47e2c")}</option></select>{field.kind === 'reference' && references && !unavailable ? <select {...common}><option value="">{localizedText(en, "pairs.m_b99f73d1ac07c92d")}</option>{value.state === 'Known' && value.value && !references.some(option => option.token === value.value) && <option value={value.value}>{value.value}</option>}{references.map(option => <option key={option.token} value={option.token}>{option.label}</option>)}</select> : field.kind === 'enum' ? <select {...common}><option value="">{localizedText(en, "pairs.m_b99f73d1ac07c92d")}</option>{field.options?.map(option => <option key={option} value={option}>{dailyOptionLabel(option, language)}</option>)}</select> : multiline ? <textarea {...common} maxLength={maximum} rows={3}/> : <input {...common} type="text" inputMode={field.kind === 'number' ? 'decimal' : undefined} maxLength={maximum} placeholder={field.key === 'report_date' ? 'YYYY-MM-DD' : undefined}/>}{field.kind === 'number' && <p id={`${id}-format`}>{localizedText(en, "pairs.m_b3253648ba5c53c4")}: {unit ?? (localizedText(en, "pairs.m_f24aaafc4169cb27"))}</p>}{required && <p>{localizedText(en, "pairs.m_f4f4047a691aa12c")}</p>}{field.kind === 'reference' && references && !unavailable && <p>{references.length ? localizedText(en, "pairs.m_5e0d7a1c3b9f2e41") : localizedText(en, "pairs.m_8c2f6b0e4a7d1395")}</p>}{unavailable && <p id={`${id}-source`}>{localizedText(en, "pairs.m_4dc5ff97252037b5")}</p>}{error && <p className="field-error" id={`${id}-error`}>{localizedText(en, "pairs.m_e6254b8d2b5aa432")}</p>}</div>;
}
const dailyOptionLabels: Readonly<Record<string, PresentationLabel>> = {
  'At Sea': 'atSea', Anchored: 'anchored', Berthing: 'berthing', Berthed: 'berthed', Loading: 'loading',
  Discharging: 'discharging', Drifting: 'drifting', Other: 'other', 'Daily Summary': 'dailySummary',
  OK: 'declaredOk', REVIEW_REQUIRED: 'reviewRequired', ATTENTION: 'attention', RUNNING: 'running',
  STANDBY: 'standby', STOPPED: 'stopped', RESTRICTED: 'restricted',
};
function dailyOptionLabel(option: string, language: AppLocale) {
  const key = Object.hasOwn(dailyOptionLabels, option) ? dailyOptionLabels[option] : undefined;
  return key ? presentationLabel(language, key) : option;
}
