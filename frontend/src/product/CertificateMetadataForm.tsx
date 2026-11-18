import { localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import type { ReactNode } from 'react';
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns';
import { certificateMetadataLabels, declarationLabels, type CertificateDeclaration, type CertificateMetadata } from './certificate-metadata';
import './certificates-enterprise.css';
export function CertificateMetadataForm({ value, language, disabled, scope = 'Vessel', errors = {}, documentPicker, onChange }: {
    value: CertificateMetadata;
    language: AppLocale;
    disabled: boolean;
    scope?: 'Vessel' | 'Crew';
    errors?: Record<string, string[]>;
    documentPicker?: ReactNode;
    onChange: (value: CertificateMetadata) => void;
}) {
    const id = useId(), en = language === 'en';
    const text = (field: keyof CertificateDeclaration) => {
        const label = localizedTuple(declarationLabels[field], en, 0), key = `declaration.${field}`, error = !!(errors[key]?.length || errors[field]?.length), attributes = { id: `${id}-${field}`, disabled, value: value.declaration[field] ?? '', 'aria-invalid': error, 'aria-describedby': error ? `${id}-${field}-error` : undefined, onChange: (event: {
                target: {
                    value: string;
                };
            }) => onChange({ ...value, declaration: { ...value.declaration, [field]: event.target.value || null } }) };
        return <EnterpriseField key={field} htmlFor={attributes.id} label={label}>{field === 'limitations' ? <textarea {...attributes} maxLength={4096} rows={3}/> : <input {...attributes} type={field === 'issueDate' || field === 'expiryDate' ? 'date' : 'text'} maxLength={4096}/>}{error && <p id={`${id}-${field}-error`} className="field-error">{localizedText(en, "pairs.m_46ee64e256587edc")}</p>}</EnterpriseField>;
    };
    const metadata = (field: keyof typeof certificateMetadataLabels) => {
        const label = localizedTuple(certificateMetadataLabels[field], en, 0), numeric = field === 'renewalLeadDaysNumeric' || field === 'issuingCountryNumeric', error = !!errors[field]?.length, attributes = { id: `${id}-${field}`, disabled, value: value[field] ?? '', 'aria-invalid': error, 'aria-describedby': error ? `${id}-${field}-error` : undefined, onChange: (event: {
                target: {
                    value: string;
                };
            }) => onChange({ ...value, [field]: event.target.value || null }) };
        return <EnterpriseField key={field} htmlFor={attributes.id} label={label}>{field === 'notes' ? <textarea {...attributes} maxLength={4096} rows={3}/> : <input {...attributes} type={field === 'endorsementExpiry' ? 'date' : 'text'} inputMode={numeric ? 'decimal' : undefined} maxLength={numeric ? 1024 : 4096}/>}{numeric && <p className="certificate-field-hint">{localizedText(en, "pairs.m_36f01e37029ddb8b")}</p>}{error && <p id={`${id}-${field}-error`} className="field-error">{localizedText(en, "pairs.m_46ee64e256587edc")}</p>}</EnterpriseField>;
    };
    return <div className="certificate-metadata-form"><EnterpriseFormGrid disabled={disabled}><legend>{localizedText(en, "pairs.m_fe2bd06e2567f248")}</legend>{(['certificateTypeReference', 'certificateNumber', 'issuer', 'issueDate', 'expiryDate', 'limitations'] as const).map(text)}<p className="certificate-field-hint">{localizedText(en, "pairs.m_9ed9a77cfee744d3")}</p></EnterpriseFormGrid><EnterpriseFormGrid disabled={disabled}><legend>{localizedText(en, "pairs.m_aeb22a0e4e9c686c")}</legend>{(['sourceSystem', 'renewalLeadDaysNumeric', 'notes'] as const).map(metadata)}{documentPicker}</EnterpriseFormGrid>{scope === 'Crew' && <details><summary>{localizedText(en, "pairs.m_4c82eb4788a1226d")}</summary><EnterpriseFormGrid disabled={disabled}>{(['issuingCountryNumeric', 'endorsementNumber', 'endorsementExpiry', 'stcwReference', 'tankerRelevance', 'vesselTypeScope'] as const).map(metadata)}<EnterpriseField htmlFor={`${id}-endorsement`} label={localizedText(en, "pairs.m_57c0351bef24d7d0")}><select id={`${id}-endorsement`} disabled={disabled} value={value.endorsementRequired === null ? '' : String(value.endorsementRequired)} onChange={event => onChange({ ...value, endorsementRequired: event.target.value === '' ? null : event.target.value === 'true' })}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option><option value="true">{localizedText(en, "pairs.m_13ec430971cbc4d6")}</option><option value="false">{localizedText(en, "pairs.m_2cd3fe348e592de7")}</option></select></EnterpriseField></EnterpriseFormGrid></details>}</div>;
}
