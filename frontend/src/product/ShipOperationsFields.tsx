import { useId } from 'react'
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'
import { shipOpsGroups, type ShipOpsFact, type ShipOpsFieldDefinition, type ShipOpsFields, type ShipOpsKind } from './ship-operation-fields'
import { shipOpsFactLabel, shipOpsOptionLabel } from './ship-operation-copy'
import './ship-operations.css'

export function ShipOperationsFields({ kind, fields, schema, language, disabled, errors = {}, onChange }: { kind: ShipOpsKind; fields: ShipOpsFields; schema: ShipOpsFieldDefinition[]; language: 'en' | 'tr'; disabled: boolean; errors?: Record<string, string[]>; onChange: (fields: ShipOpsFields) => void }) {
  const id = useId(), en = language === 'en'
  return <div className="ship-operations-fields">{shipOpsGroups[kind].map(group => <EnterpriseFormGrid key={group.en} disabled={disabled}>
    <legend>{en ? group.en : group.tr}</legend>
    {group.fields.filter(info => !info.readOnly).map(info => {
      const definition = schema.find(field => field.key === info.key), fieldId = id + '-' + info.key, fact = fields[info.key] ?? { state: 'Missing', value: null }
      if (!definition || definition.readOnly || definition.type !== info.type) return <p key={info.key} role="alert">{en ? 'Field contract unavailable: ' : 'Alan sözleşmesi kullanılamıyor: '}{en ? info.en : info.tr}</p>
      const label = (en ? info.en : info.tr) + (definition.unit ? ' (' + definition.unit + ')' : ''), error = !!(errors[info.key]?.length || errors['fields.' + info.key]?.length)
      const blocked = disabled || definition.referenceUnavailable || fact.state !== 'Known'
      const update = (value: ShipOpsFact) => { if (!disabled) onChange({ ...fields, [info.key]: value }) }
      const attributes = { id: fieldId, disabled: blocked, value: fact.value ?? '', 'aria-invalid': error, 'aria-describedby': fieldId + '-help' + (error ? ' ' + fieldId + '-error' : ''), onChange: (event: { target: { value: string } }) => update({ state: 'Known', value: event.target.value }) }
      return <div key={info.key} className={'ship-operations-field' + (definition.type === 'Narrative' ? ' ship-operations-field-wide' : '')}>
        <div className="ship-operations-field-heading"><span>{label}</span><small>{definition.required ? (en ? 'Source-required' : 'Kaynakta gerekli') : (en ? 'Source condition unresolved' : 'Kaynak koşulu çözümlenmemiş')}</small></div>
        <EnterpriseField htmlFor={fieldId + '-state'} label={label + (en ? ' value state' : ' değer durumu')}>
          <select id={fieldId + '-state'} disabled={disabled} value={fact.state} onChange={event => {
            const state = event.target.value
            if (state === 'Missing' || state === 'Unknown') update({ state, value: null })
            else if (state === 'Known' && !definition.referenceUnavailable) update({ state: 'Known', value: fact.value ?? '' })
          }}><option value="Missing">{shipOpsFactLabel('Missing', en)}</option><option value="Unknown">{shipOpsFactLabel('Unknown', en)}</option><option value="Known" disabled={definition.referenceUnavailable}>{shipOpsFactLabel('Known', en)}</option></select>
        </EnterpriseField>
        <EnterpriseField htmlFor={fieldId} label={label}>
          {definition.type === 'Narrative' ? <textarea {...attributes} maxLength={4000} rows={4} /> : definition.type === 'Code' ? <select {...attributes}><option value="">{en ? 'Choose declared source option' : 'Beyan edilen kaynak seçeneğini seç'}</option>{definition.options.map(option => <option key={option} value={option}>{shipOpsOptionLabel(option, en)}</option>)}</select> : <input {...attributes} type="text" inputMode={definition.type === 'Number' ? 'decimal' : undefined} maxLength={definition.type === 'Number' ? 48 : 512} placeholder={definition.type === 'Instant' ? '2026-11-05T12:00:00Z' : undefined} />}
          <p className="ship-operations-hint" id={fieldId + '-help'}>{definition.referenceUnavailable ? (en ? 'The owning catalog or evidence mapping is unavailable. Keep Missing or Unknown; free text cannot establish a reference.' : 'Sahip katalog veya kanıt eşlemesi kullanılamıyor. Eksik ya da Bilinmiyor bırakın; serbest metin referans oluşturamaz.') : definition.type === 'Number' ? (en ? "Exact declared text; use '.' for decimals. Missing and Unknown are separate from zero." : "Kesin beyan metni; ondalık için '.' kullanın. Eksik ve Bilinmiyor sıfırdan ayrıdır.") : definition.type === 'Instant' ? (en ? 'Include date, seconds and Z or an explicit UTC offset. A timezone-less local time is not accepted.' : 'Tarih, saniye ve Z veya açık UTC farkı ekleyin. Saat dilimsiz yerel saat kabul edilmez.') : (en ? 'A declared value remains unverified; incomplete drafts may be saved.' : 'Beyan değeri doğrulanmış sayılmaz; eksik taslak kaydedilebilir.')}</p>
          {error && <p className="field-error" id={fieldId + '-error'}>{en ? 'Check this source value or choose Missing / Unknown.' : 'Kaynak değerini kontrol edin veya Eksik / Bilinmiyor seçin.'}</p>}
        </EnterpriseField>
      </div>
    })}
  </EnterpriseFormGrid>)}</div>
}
