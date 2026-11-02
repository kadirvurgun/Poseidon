import { useId, useState } from 'react'
import './offline-conflict-review.css'

export type OfflineConflictValue = Readonly<{ kind: 'unknown' | 'missing' | 'withheld' }> |
  Readonly<{ kind: 'known'; text: string }>
export type OfflineConflictChoice = 'keepServer' | 'useCaptured'
export type OfflineConflictField = Readonly<{
  key: string
  label: string
  captured: OfflineConflictValue
  server: OfflineConflictValue
  choices: readonly OfflineConflictChoice[]
}>
export type OfflineConflict = Readonly<{
  id: string
  revision: string
  contextKey: string
  fields: readonly OfflineConflictField[]
}>
// Send choices for the exact server conflict revision, not copied client values,
// an actor identity, a safety signatory or a claimed current aggregate version.
export type OfflineConflictResolution = Readonly<{
  conflictId: string
  expectedRevision: string
  reason: string
  decisions: readonly Readonly<{ field: string; choice: OfflineConflictChoice }>[]
}>
type Props = {
  language: 'tr' | 'en'
  contextKey: string
  state: 'loading' | 'ready' | 'denied' | 'unavailable' | 'stale'
  conflict: OfflineConflict | null
  canResolve: boolean
  online: boolean
  operation: 'idle' | 'submitting' | 'outcomeUnknown' | 'failed'
  reasonLimit: number
  onResolve: (resolution: OfflineConflictResolution) => void
  onCheckOutcome: () => void
  onRetry?: () => void
}

function validValue(value: OfflineConflictValue) {
  return value && (['missing', 'unknown', 'withheld'].includes(value.kind) || value.kind === 'known' && typeof value.text === 'string')
}

export function OfflineConflictReview(props: Props) {
  const en = props.language === 'en', conflict = props.conflict
  if (props.state === 'loading') return <p role="status">{en ? 'Loading synchronization conflict…' : 'Eşitleme çakışması yükleniyor…'}</p>
  if (props.state === 'denied') return <p role="alert">{en ? 'Conflict access is not permitted.' : 'Çakışma kaydına erişim yetkiniz yok.'}</p>
  if (props.state === 'stale' || !props.contextKey || conflict && conflict.contextKey !== props.contextKey) return <p role="status">
    {en ? 'Reload the conflict for the current session and selection.' : 'Geçerli oturum ve seçim için çakışmayı yeniden yükleyin.'}
  </p>
  if (props.state !== 'ready' || !conflict || !conflict.id || !conflict.revision || !Array.isArray(conflict.fields) ||
    conflict.fields.length === 0 || new Set(conflict.fields.map(field => field?.key)).size !== conflict.fields.length ||
    conflict.fields.some(field => !field || !field.key || !field.label || !validValue(field.captured) || !validValue(field.server) ||
      !Array.isArray(field.choices) || field.choices.some((choice: unknown) => !['keepServer', 'useCaptured'].includes(String(choice))))) return <div role="alert">
    <p>{en ? 'Conflict details could not be loaded.' : 'Çakışma ayrıntıları yüklenemedi.'}</p>
    {props.onRetry && <button type="button" onClick={props.onRetry}>{en ? 'Retry' : 'Tekrar dene'}</button>}
  </div>
  return <Review key={`${props.contextKey}/${conflict.id}/${conflict.revision}`} {...props} conflict={conflict} />
}

function Review({ language, conflict, canResolve, online, operation, reasonLimit, onResolve, onCheckOutcome }: Props & { conflict: OfflineConflict }) {
  const en = language === 'en', prefix = useId()
  const [reason, setReason] = useState('')
  const [choices, setChoices] = useState<Readonly<Record<string, OfflineConflictChoice>>>({})
  const configuredLimit = Number.isSafeInteger(reasonLimit) && reasonLimit > 0 && reasonLimit <= 2_147_483_647
  const locked = !canResolve || !online || !configuredLimit || operation === 'submitting' || operation === 'outcomeUnknown'
  const available = (field: OfflineConflictField, choice: OfflineConflictChoice) =>
    field.captured.kind !== 'withheld' && field.server.kind !== 'withheld' && field.choices.includes(choice)
  const selected = (field: OfflineConflictField) => Object.hasOwn(choices, field.key) && available(field, choices[field.key])
  const complete = conflict.fields.every(selected) && reason.trim().length > 0 && reason.length <= reasonLimit
  const value = (item: OfflineConflictValue): string => {
    switch (item.kind) {
      case 'unknown': return en ? 'Unknown' : 'Bilinmiyor'
      case 'missing': return en ? 'Not provided' : 'Bildirilmedi'
      case 'withheld': return en ? 'Withheld' : 'Gösterilmiyor'
      case 'known': return item.text === '' ? (en ? 'Empty text' : 'Boş metin') : item.text
    }
  }
  const choiceLabel = (choice: OfflineConflictChoice) => choice === 'keepServer'
    ? (en ? 'Keep server value' : 'Sunucu değerini koru') : (en ? 'Use captured value' : 'Yakalanan değeri kullan')
  function submit() {
    if (locked || !complete) return
    onResolve({ conflictId: conflict.id, expectedRevision: conflict.revision, reason,
      decisions: conflict.fields.map(field => ({ field: field.key, choice: choices[field.key] })) })
  }
  return <section className="offline-conflict" aria-label={en ? 'Synchronization conflict review' : 'Eşitleme çakışması incelemesi'} aria-busy={operation === 'submitting' || undefined}>
    <h3>{en ? 'Resolve synchronization conflict' : 'Eşitleme çakışmasını çöz'}</h3>
    <p>{en ? 'Choose each value explicitly and provide a reason. Submission rechecks current access and record versions; it does not grant operational approval.'
      : 'Her değeri açıkça seçin ve gerekçeyi belirtin. Gönderim güncel yetkiyi ve kayıt sürümlerini yeniden kontrol eder; operasyonel onay vermez.'}</p>
    {!canResolve && <p role="status">{en ? 'You can view this conflict but cannot resolve it.' : 'Bu çakışmayı görüntüleyebilirsiniz; çözüm yetkiniz yok.'}</p>}
    {!online && <p role="status">{en ? 'Connect and authenticate before submitting a resolution.' : 'Çözümü göndermeden önce bağlanın ve oturumunuzu doğrulayın.'}</p>}
    {!configuredLimit && <p role="alert">{en ? 'Resolution policy is unavailable.' : 'Çözüm politikası kullanılamıyor.'}</p>}
    {operation === 'outcomeUnknown' && <div role="alert">
      <p>{en ? 'The submission outcome is unknown. Check the server record before submitting again.' : 'Gönderimin sonucu bilinmiyor. Yeniden göndermeden önce sunucu kaydını sorgulayın.'}</p>
      <button type="button" disabled={!online} onClick={onCheckOutcome}>{en ? 'Check submission outcome' : 'Gönderim sonucunu sorgula'}</button>
    </div>}
    {operation === 'failed' && <p role="alert">{en ? 'The resolution was not accepted. Your choices are retained; review the current conflict.' : 'Çözüm kabul edilmedi. Seçimleriniz korunuyor; geçerli çakışmayı inceleyin.'}</p>}
    <div className="offline-conflict__fields">
      {conflict.fields.map((field, index) => <fieldset key={field.key} disabled={locked}>
        <legend>{field.label}</legend>
        <dl>
          <div><dt>{en ? 'Captured' : 'Yakalanan'}</dt><dd><bdi>{value(field.captured)}</bdi></dd></div>
          <div><dt>{en ? 'Server' : 'Sunucu'}</dt><dd><bdi>{value(field.server)}</bdi></dd></div>
          <div><dt>{en ? 'Resolved' : 'Çözümlenen'}</dt><dd><bdi>{selected(field)
            ? value(choices[field.key] === 'keepServer' ? field.server : field.captured)
            : (en ? 'No choice made' : 'Seçim yapılmadı')}</bdi></dd></div>
        </dl>
        {(['keepServer', 'useCaptured'] as const).map(choice => <label key={choice} className="offline-conflict__choice">
          <input type="radio" name={`${prefix}-field-${index}`} value={choice} disabled={!available(field, choice)}
            checked={Object.hasOwn(choices, field.key) && choices[field.key] === choice}
            onChange={() => setChoices(prior => ({ ...prior, [field.key]: choice }))} />
          {choiceLabel(choice)}
        </label>)}
        {field.choices.length === 0 || field.captured.kind === 'withheld' || field.server.kind === 'withheld'
          ? <p>{en ? 'Resolution is unavailable under the current field policy.' : 'Geçerli alan politikasıyla çözüm yapılamıyor.'}</p> : null}
      </fieldset>)}
    </div>
    <label htmlFor={`${prefix}-reason`}>{en ? 'Resolution reason (required)' : 'Çözüm gerekçesi (zorunlu)'}</label>
    <textarea id={`${prefix}-reason`} value={reason} disabled={locked} aria-required="true"
      maxLength={configuredLimit ? reasonLimit : undefined} onChange={event => setReason(event.target.value)} />
    <button type="button" disabled={locked || !complete} onClick={submit}>{en ? 'Submit resolution' : 'Çözümü gönder'}</button>
  </section>
}
