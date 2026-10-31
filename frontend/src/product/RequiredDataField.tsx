import { useId } from 'react'
import type { TextValue, NumberValue } from './profile-contract'

type BooleanValue = { state?: string; value?: boolean | null }
type Props = { label: string; language: 'tr' | 'en'; error?: boolean; disabled?: boolean } & (
  { kind: 'text'; value: TextValue; onChange: (value: TextValue) => void } |
  { kind: 'number'; value: NumberValue; onChange: (value: NumberValue) => void } |
  { kind: 'boolean'; value: BooleanValue; onChange: (value: BooleanValue) => void })

export function RequiredDataField(props: Props) {
  const id = useId()
  const tr = props.language === 'tr'
  const state = props.value.state ?? 'Missing'
  const empty = (next: string) => {
    if (props.kind === 'text') props.onChange({ state: next, value: next === 'Known' ? '' : null })
    else if (props.kind === 'number') props.onChange({ state: next, value: null })
    else props.onChange({ state: next, value: null })
  }
  return <div className="required-data-field">
    <label htmlFor={id}>{props.label}</label>
    <div className="required-data-controls">
      <select id={`${id}-state`} aria-label={`${props.label} — ${tr ? 'Bilgi durumu' : 'Data state'}`} disabled={props.disabled}
        value={state} onChange={e => empty(e.target.value)}>
        <option value="Missing">{tr ? 'Eksik' : 'Missing'}</option><option value="Unknown">{tr ? 'Bilinmiyor' : 'Unknown'}</option><option value="Known">{tr ? 'Biliniyor' : 'Known'}</option>
      </select>
      {props.kind === 'boolean' ? <select id={id} value={props.value.value == null ? '' : String(props.value.value)} disabled={props.disabled || state !== 'Known'}
        aria-invalid={props.error} aria-describedby={props.error ? `${id}-error` : undefined}
        onChange={e => props.onChange({ state: 'Known', value: e.target.value === '' ? null : e.target.value === 'true' })}>
        <option value="">{tr ? 'Seçiniz' : 'Choose'}</option><option value="true">{tr ? 'Evet' : 'Yes'}</option><option value="false">{tr ? 'Hayır' : 'No'}</option>
      </select> : <input id={id} type={props.kind === 'number' ? 'number' : 'text'} step={props.kind === 'number' ? 'any' : undefined}
        maxLength={props.kind === 'text' ? 500 : undefined} value={props.value.value ?? ''} disabled={props.disabled || state !== 'Known'}
        aria-invalid={props.error} aria-describedby={props.error ? `${id}-error` : undefined}
        onChange={e => {
          if (props.kind === 'text') props.onChange({ state: 'Known', value: e.target.value })
          else props.onChange({ state: 'Known', value: e.target.value === '' ? null : Number(e.target.value) })
        }} />}
    </div>
    {props.error && <p id={`${id}-error`} className="field-error">{tr ? 'Değeri ve bilgi durumunu kontrol edin.' : 'Check the value and data state.'}</p>}
  </div>
}
