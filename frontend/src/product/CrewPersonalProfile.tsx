import { useEffect, useRef, useState } from 'react'
import { ApiError, request } from './api'

// Authored consumer contract; generated parity is deferred with the larger gate.
const fields = [
  ['nationality', 'Nationality', 'Uyruk'], ['dateOfBirth', 'Date of birth', 'Doğum tarihi'],
  ['passportNo', 'Passport number', 'Pasaport numarası'], ['passportExpiry', 'Passport expiry', 'Pasaport bitiş tarihi'],
  ['seamanBookNo', 'Seaman book number', 'Gemi adamı cüzdan numarası'], ['seamanBookExpiry', 'Seaman book expiry', 'Cüzdan bitiş tarihi'],
  ['companyStartDate', 'Company start date', 'Şirkete giriş tarihi'], ['seaServiceMonths', 'Sea service months', 'Deniz hizmeti (ay)'],
  ['tankerExperienceMonths', 'Tanker experience months', 'Tanker deneyimi (ay)'], ['vesselTypeExperience', 'Vessel type experience', 'Gemi tipi deneyimi'],
  ['companyExperienceMonths', 'Company experience months', 'Şirket deneyimi (ay)'], ['emergencyContact', 'Emergency contact', 'Acil durum iletişimi'],
  ['preferredLanguage', 'Preferred language', 'Tercih edilen dil'], ['contactPhone', 'Contact phone', 'İletişim telefonu'], ['email', 'Email', 'E-posta'],
] as const
export type PersonalData = Partial<Record<typeof fields[number][0], string | null>>
export type PersonalProfile = { version: number | null; revision: number; policyVersion: string; purpose: string;
  data: PersonalData; readable: number[]; writable: number[]; storageState: string; options: { field: number; values: string[] }[] }
type Attempt = { key: string; body: { baseVersion: number | null; policyVersion: string; purpose: string; fields: number[]; data: PersonalData } }
export function CrewPersonalProfile({ vessel, assignment, csrf, language, onDirty }: {
  vessel: string; assignment: string; csrf: string; language: 'tr' | 'en'; onDirty: (dirty: boolean) => void
}) {
  const tr = language === 'tr'
  const [profile, setProfile] = useState<PersonalProfile | null>(null)
  const [changes, setChanges] = useState<PersonalData>({})
  const [state, setState] = useState<'loading' | 'ready' | 'denied' | 'unavailable' | 'saving' | 'uncertain' | 'conflict'>('loading')
  const [generation, setGeneration] = useState(0)
  const attempt = useRef<Attempt | null>(null)
  const inFlight = useRef(false)
  const active = useRef<AbortController | null>(null)
  const path = `vessels/${encodeURIComponent(vessel)}/crew/${encodeURIComponent(assignment)}/personal-profile`
  useEffect(() => {
    const controller = new AbortController(); active.current = controller
    setProfile(null); setChanges({}); attempt.current = null; setState('loading'); onDirty(false)
    request<PersonalProfile>(`${path}?purpose=crew-profile`, controller.signal).then(value => {
      if (!value || !value.data || !Array.isArray(value.readable) || !Array.isArray(value.writable) || !Array.isArray(value.options) ||
        [...value.readable, ...value.writable].some(f => !Number.isInteger(f) || f < 0 || f >= fields.length) || value.writable.some(f => !value.readable.includes(f))) throw new ApiError(502)
      if (!controller.signal.aborted) { setProfile(value); setState('ready') }
    }).catch(error => { if (!controller.signal.aborted) setState(error instanceof ApiError && [401, 403, 404].includes(error.status) ? 'denied' : 'unavailable') })
    return () => { controller.abort(); active.current?.abort() }
  }, [path, csrf, generation, onDirty])
  const save = async () => {
    if (!profile || inFlight.current) return
    if (!attempt.current) {
      const changedFields = fields.flatMap(([key], i) => Object.hasOwn(changes, key) ? [i] : [])
      if (!changedFields.length) return
      attempt.current = { key: crypto.randomUUID(), body: { baseVersion: profile.version, policyVersion: profile.policyVersion,
        purpose: profile.purpose, fields: changedFields, data: { ...changes } } }
    }
    const frozen = attempt.current
    const controller = new AbortController(); active.current = controller; inFlight.current = true; setState('saving')
    const timer = setTimeout(() => controller.abort(), 15000)
    try {
      await request(path, controller.signal, { method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': frozen.key }, body: JSON.stringify(frozen.body) })
      setChanges({}); attempt.current = null; onDirty(false); setGeneration(v => v + 1)
    } catch (error) {
      if (error instanceof ApiError && [401, 403, 404].includes(error.status)) { setProfile(null); setChanges({}); attempt.current = null; onDirty(false); setState('denied') }
      else setState(error instanceof ApiError && error.status === 409 ? 'conflict' : 'uncertain')
    } finally { clearTimeout(timer); inFlight.current = false }
  }
  if (state === 'loading') return <p role="status">{tr ? 'Kişisel profil yükleniyor…' : 'Loading personal profile…'}</p>
  if (!profile) return <p role="status">{state === 'denied' ? (tr ? 'Kişisel alan erişimi kapalı.' : 'Personal field access withheld.') : (tr ? 'Korumalı profil servisi kullanılamıyor.' : 'Protected profile service unavailable.')}</p>
  const locked = ['saving', 'uncertain', 'conflict'].includes(state)
  return <section aria-label={tr ? 'Korumalı kişisel profil' : 'Protected personal profile'}><h2>{tr ? 'Kişisel profil · doğrulanmamış beyan' : 'Personal profile · unverified declaration'}</h2>
    <form onSubmit={event => { event.preventDefault(); void save() }}><fieldset disabled={locked}>
      {fields.map(([key, en, turkish], i) => profile.readable.includes(i) && <label key={key}>{tr ? turkish : en}
        {profile.options.find(option => option.field === i) ? <select disabled={!profile.writable.includes(i)} value={changes[key] === undefined ? profile.data[key] ?? '' : changes[key] ?? ''}
          onChange={event => { setChanges(old => ({ ...old, [key]: event.target.value || null })); onDirty(true) }}><option value="">{tr ? 'Bilinmiyor' : 'Unknown'}</option>
          {profile.options.find(option => option.field === i)!.values.map(value => <option key={value}>{value}</option>)}</select> :
          <input type={['dateOfBirth', 'passportExpiry', 'seamanBookExpiry', 'companyStartDate'].includes(key) ? 'date' : 'text'} autoComplete="off" maxLength={4096}
            readOnly={!profile.writable.includes(i)} value={changes[key] === undefined ? profile.data[key] ?? '' : changes[key] ?? ''}
            onChange={event => { setChanges(old => ({ ...old, [key]: event.target.value || null })); onDirty(true) }} />}
      </label>)}
    </fieldset>
    {state === 'uncertain' && <p role="alert">{tr ? 'Sonuç belirsiz. Aynı denemeyi tekrar sorgulayın.' : 'Outcome uncertain. Retry the same attempt.'}</p>}
    {state === 'conflict' && <p role="alert">{tr ? 'Sürüm çakışması. Girdiyi inceleyip güncel profili yükleyin.' : 'Version conflict. Review the input and reload the current profile.'}</p>}
    <button disabled={state === 'saving' || state === 'conflict' || !profile.writable.length || !Object.keys(changes).length} type="submit">{state === 'uncertain' ? (tr ? 'Aynı denemeyi tekrarla' : 'Retry same attempt') : (tr ? 'Beyanı kaydet' : 'Save declaration')}</button>
    <button disabled={state === 'saving' || state === 'uncertain'} type="button" onClick={() => { onDirty(false); setGeneration(v => v + 1) }}>{tr ? 'Vazgeç ve yeniden yükle' : 'Discard and reload'}</button>
    </form></section>
}
