import { useId } from 'react'
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'
import { defectCodeLabel, defectFactLabel, defectGroups, type DefectFact, type DefectFieldDefinition, type DefectFields } from './defect-fields'
import './defect-workspace.css'
export function DefectFieldsForm({ fields, schema, language, disabled, errors = {}, onChange }: { fields: DefectFields; schema: DefectFieldDefinition[]; language: 'en' | 'tr'; disabled: boolean; errors?: Record<string, string[]>; onChange: (fields: DefectFields) => void }) {
  const id = useId(), en = language === 'en'
  return <div className="defect-fields"><p className="defect-hint">{en ? 'Unassessed declaration. Incomplete drafts may be saved; safety and operating authorization are separate.' : 'Değerlendirilmemiş beyan. Eksik taslak kaydedilebilir; emniyet ve işletme yetkisi ayrıdır.'}</p>{defectGroups.map(group => <EnterpriseFormGrid key={group.en} disabled={disabled}><legend>{en ? group.en : group.tr}</legend>{group.fields.map(info => {
    const field = schema.find(field => field.key === info.key), fact = fields[info.key], label = en ? info.en : info.tr, fieldId = id + '-' + info.key
    if (!field) return <p role="alert" key={info.key}>{en ? 'Source field contract unavailable: ' : 'Kaynak alan sözleşmesi kullanılamıyor: '}{label}</p>
    const invalid = !!(errors[info.key]?.length || errors['fields.' + info.key]?.length), update = (value: DefectFact) => { if (!disabled) onChange({ ...fields, [info.key]: value }) }
    const hint = field.type === 'Instant' ? (en ? 'Include date, seconds and Z or an explicit UTC offset. Missing or unknown expiry never means cleared.' : 'Tarih, saniye ve Z veya açık UTC farkı ekleyin. Eksik veya bilinmeyen süre kalkmış sayılmaz.') : info.key === 'lifecycle_stage' ? (en ? 'Declared phase only. Verify and Effectiveness do not establish verification, effectiveness or closure.' : 'Yalnız beyan edilen aşama. Doğrulama ve Etkinlik aşamaları doğrulama, etkinlik veya kapanış oluşturmaz.') : null
    const input = { id: fieldId, value: fact.value ?? '', disabled: disabled || fact.state !== 'Known', 'aria-invalid': invalid, 'aria-describedby': [hint ? fieldId + '-help' : '', invalid ? fieldId + '-error' : ''].filter(Boolean).join(' ') || undefined, onChange: (event: { target: { value: string } }) => update({ state: 'Known', value: event.target.value }) }
    return <div key={info.key} className={'defect-field' + (field.type === 'Narrative' ? ' defect-field-wide' : '')}>
      <EnterpriseField htmlFor={fieldId + '-state'} label={label + (en ? ' value state' : ' değer durumu')}><select id={fieldId + '-state'} value={fact.state} disabled={disabled} onChange={event => { const state = event.target.value; if (state === 'Missing' || state === 'Unknown') update({ state, value: null }); else if (state === 'Known') update({ state, value: fact.value ?? '' }) }}><option value="Missing">{defectFactLabel('Missing', en)}</option><option value="Unknown">{defectFactLabel('Unknown', en)}</option><option value="Known">{defectFactLabel('Known', en)}</option></select></EnterpriseField>
      <EnterpriseField htmlFor={fieldId} label={label}>{field.type === 'Narrative' ? <textarea {...input} maxLength={4000} rows={3} /> : field.type === 'Code' ? <select {...input}><option value="">{en ? 'Choose declared source value' : 'Beyan edilen kaynak değerini seç'}</option>{field.options.map(option => <option key={option} value={option}>{defectCodeLabel(option, en)}</option>)}</select> : <input {...input} type="text" maxLength={field.type === 'Instant' ? 40 : 256} placeholder={field.type === 'Instant' ? '2026-11-06T12:00:00Z' : undefined} />}
        {hint && <p id={fieldId + '-help'} className="defect-hint">{hint}</p>}
        {invalid && <p id={fieldId + '-error'} className="field-error">{en ? 'Check this declared value or choose Missing / Unknown.' : 'Beyan değerini kontrol edin veya Eksik / Bilinmiyor seçin.'}</p>}
      </EnterpriseField>
    </div>
  })}</EnterpriseFormGrid>)}</div>
}
