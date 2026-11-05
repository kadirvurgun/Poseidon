import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'
import { tugLabel } from './tug-labels'
import type { TugContext, TugInputs } from './tug-api'

type Props = { inputs: TugInputs; context: TugContext; language: 'tr' | 'en'; disabled: boolean; errors: Record<string, string[]>; onChange: (key: keyof TugInputs, value: string | number | null) => void }
const labels = {
  customerRef: ['Müşteri referansı', 'Customer reference'], assistedVessel: ['Hizmet verilecek gemi', 'Assisted vessel'], assistedVesselImo: ['Gemi IMO', 'Assisted vessel IMO'], operationArea: ['Operasyon alanı', 'Operation area'], pickupPoint: ['Alış noktası', 'Pickup point'], destinationPoint: ['Varış noktası', 'Destination point'], specialRequirements: ['Özel gereksinimler', 'Special requirements'], notes: ['Notlar', 'Notes'],
  requestSource: ['Talep kaynağı', 'Request source'], jobType: ['İş tipi', 'Job type'], priority: ['Öncelik', 'Priority'], requestOwner: ['Talep sorumlusu', 'Request owner'], requestedStart: ['Talep başlangıcı (UTC)', 'Requested start (UTC)'], requestedEnd: ['Talep bitişi (UTC, koşullu)', 'Requested end (UTC, conditional)'], requiredBollardPull: ['Gerekli BP (ton, kesin ondalık)', 'Required BP (tonnes, exact decimal)'], requiredTugCount: ['Gerekli römorkör sayısı', 'Required tug count'],
} satisfies Record<keyof TugInputs, [string, string]>
const wireKey = (key: string) => key.replace(/[A-Z]/g, x => `_${x.toLowerCase()}`)

export function TugRequestFields({ inputs, context, language, disabled, errors, onChange }: Props) {
  const en = language === 'en'
  const field = (key: keyof TugInputs, options?: string[]) => {
    const id = `tug-request-${key}`, error = errors[key] ?? errors[wireKey(key)] ?? errors[`inputs.${key}`]
    const describedBy = error?.length ? `${id}-error` : undefined
    const common = { id, 'aria-invalid': error?.length ? true as const : undefined, 'aria-describedby': describedBy }
    return <EnterpriseField key={key} htmlFor={id} label={labels[key][en ? 1 : 0]} className={['notes', 'specialRequirements'].includes(key) ? 'tug-field-wide' : undefined}>
      {options ? <select {...common} value={inputs[key] ?? ''} onChange={event => onChange(key, event.target.value || null)}><option value="">{en ? 'Unknown' : 'Bilinmiyor'}</option>{options.map(value => <option key={value} value={value}>{tugLabel(value, language)}</option>)}</select>
        : ['notes', 'specialRequirements'].includes(key) ? <textarea {...common} rows={3} maxLength={2000} value={String(inputs[key] ?? '')} onChange={event => onChange(key, event.target.value || null)} />
        : key === 'requestedStart' || key === 'requestedEnd' ? <input {...common} type="datetime-local" step="1" value={inputs[key]?.replace(/Z$/, '').slice(0, 19) ?? ''} onChange={event => onChange(key, event.target.value ? `${event.target.value.length === 16 ? event.target.value + ':00' : event.target.value}Z` : null)} />
        : key === 'requiredTugCount' ? <input {...common} type="number" min="1" max="1000" step="1" value={inputs[key] ?? ''} onChange={event => onChange(key, event.target.value ? Number(event.target.value) : null)} />
        : <input {...common} inputMode={key === 'requiredBollardPull' ? 'decimal' : undefined} maxLength={key === 'requiredBollardPull' ? 11 : 200} value={String(inputs[key] ?? '')} onChange={event => onChange(key, event.target.value || null)} />}
      {error?.length ? <p id={`${id}-error`} role="alert">{en ? 'Check this value.' : 'Bu değeri kontrol edin.'}</p> : null}
    </EnterpriseField>
  }
  return <>
    <p className="tug-form-guidance">{en ? 'Incomplete drafts can be saved. Required operational data and authorized review must be resolved before submission.' : 'Eksik taslaklar kaydedilebilir. Gönderim öncesinde zorunlu operasyon verileri ve yetkili inceleme tamamlanmalıdır.'}</p>
    <EnterpriseFormGrid disabled={disabled}><legend>{en ? 'Request and assisted vessel' : 'Talep ve hizmet verilecek gemi'}</legend>{field('requestSource', context.requestSources)}{field('customerRef')}{field('assistedVessel')}{field('assistedVesselImo')}{field('jobType', context.jobTypes)}{field('priority', context.priorities)}{field('requestOwner', context.owners)}</EnterpriseFormGrid>
    <EnterpriseFormGrid disabled={disabled}><legend>{en ? 'Requested time and location' : 'Talep edilen zaman ve konum'}</legend>{field('requestedStart')}{field('requestedEnd')}{field('operationArea')}{field('pickupPoint')}{field('destinationPoint')}</EnterpriseFormGrid>
    <EnterpriseFormGrid disabled={disabled}><legend>{en ? 'Resource needs and notes' : 'Kaynak ihtiyacı ve notlar'}</legend>{field('requiredBollardPull')}{field('requiredTugCount')}{field('specialRequirements')}{field('notes')}</EnterpriseFormGrid>
  </>
}
