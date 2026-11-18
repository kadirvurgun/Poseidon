import { useCallback, useEffect, useId, useState } from 'react'
import type { FormEvent } from 'react'
import type { Locale } from '../i18n'
import { Button } from '../components/ui/button'
import { ApiError } from './api'
import { loadProfileSession } from './profile-api'
import { profileFields } from './profile-contract'
import { declareFieldSource, loadFieldHistory, loadProfileFields, sourceKinds } from './profile-field-api'
import type { DeclareFieldSource, FieldFreshness, FieldHistory, FieldSource, ProfileField, ProfileFields } from './profile-field-api'
import './profile-field-provenance.css'

// DEV-020 / T-S2-001. Saved field value versions, declared sources and server-computed
// freshness. Declared sources are never verification; FRESH is never shown as a green
// "ready" state and UNKNOWN always carries its reason. Only TR/EN copy is authored.
const copy = {
  en: {
    title: 'Field sources and freshness', loading: 'Loading field sources…', reload: 'Reload',
    owner: 'Owning legal entity', binding: 'Legacy company binding — not a registered owner or operator',
    policy: 'Freshness policy', unconfigured: 'No approved freshness policy is configured; no field can be assessed as fresh.',
    resolved: 'Approved freshness policy applied', policyUnavailable: 'Freshness policy is unavailable; freshness cannot be assessed.',
    summary: 'Profile freshness', counts: (f: number, s: number, u: number, n: number) => `${f} within policy age · ${s} stale · ${u} unknown · ${n} unavailable`,
    empty: 'No saved profile yet. Save the profile before declaring field sources.',
    dirty: 'Unsaved form edits are not shown here. Save the profile to declare sources for new values.',
    field: 'Field', value: 'Saved value', version: 'Value version', source: 'Declared source', freshness: 'Freshness', verification: 'Verification',
    notVerified: 'Not verified', noSource: 'No source declared', priorSource: 'Declared for an earlier value', revision: 'revision',
    declare: 'Declare source', cancel: 'Cancel', submit: 'Record declared source', saving: 'Recording…', retry: 'Retry the same declaration',
    kind: 'Source kind', reference: 'Source reference', observed: 'Observed at (with timezone)', state: 'Data state',
    saved: 'Declared source recorded. It is unverified.', history: 'History', hideHistory: 'Hide history', noHistory: 'No saved versions.',
    previous: 'Previous', next: 'Next', more: 'More declarations exist for this version.',
    errors: { 401: 'Your session has expired. Sign in again.', 403: 'You no longer have permission to view these field sources.', 404: 'This vessel is not available.',
      409: 'The field or its sources changed since you loaded them. Reload before declaring again.', 422: 'A source can only be declared for a recorded (known or unknown) value.',
      400: 'Check the source details.', other: 'Field sources could not be loaded or saved. Try again.', uncertain: 'The result is unknown. Retry sends the same declaration once more.' },
    declaredBy: 'declared by', recorded: 'recorded', observedLabel: 'observed', age: (s: number) => `age ${Math.floor(s / 86400)} d`,
    states: { FRESH: 'Within policy age (not verified)', STALE: 'Stale', UNKNOWN: 'Unknown', UNAVAILABLE: 'Unavailable' } as Record<string, string>,
    valueStates: { Missing: 'Missing', Unknown: 'Unknown', Known: 'Known' } as Record<string, string>,
    kinds: { DOCUMENT: 'Document', REGISTRY_RECORD: 'Registry record', CLASS_RECORD: 'Class record', OWNER_DECLARATION: 'Owner declaration',
      SURVEY_OR_MEASUREMENT: 'Survey or measurement', OTHER: 'Other', UNKNOWN: 'Source unknown' } as Record<string, string>,
    reasons: { VALUE_NOT_RECORDED: 'Value not recorded', VALUE_UNKNOWN: 'Value recorded as unknown', FIELD_VERSION_UNAVAILABLE: 'Value version unavailable',
      SOURCE_NOT_DECLARED: 'No source declared for this value', SOURCE_DECLARED_FOR_PRIOR_VALUE: 'Source was declared for an earlier value',
      SOURCE_UNKNOWN: 'Source is unknown', SOURCE_REFERENCE_NOT_RECORDED: 'Source reference not recorded', OBSERVED_AT_UNKNOWN: 'Observation time unknown',
      OBSERVED_AT_NOT_RECORDED: 'Observation time not recorded', OBSERVED_AT_AFTER_CHECK: 'Observation time is after the check time',
      FRESHNESS_POLICY_UNCONFIGURED: 'No approved freshness policy', FRESHNESS_POLICY_UNAVAILABLE: 'Freshness policy unavailable',
      WITHIN_POLICY_MAX_AGE: 'Within the approved maximum age', POLICY_MAX_AGE_EXCEEDED: 'Older than the approved maximum age',
      FIELDS_UNKNOWN: 'Some fields have unknown freshness', FIELDS_STALE: 'Some fields are stale', FIELDS_UNAVAILABLE: 'Some fields are unavailable',
      ALL_FIELDS_WITHIN_POLICY: 'All fields within policy age (not verified)', NO_PROFILE_FIELDS: 'No profile fields' } as Record<string, string>,
  },
  tr: {
    title: 'Alan kaynakları ve tazelik', loading: 'Alan kaynakları yükleniyor…', reload: 'Yeniden yükle',
    owner: 'Sahip tüzel kişi', binding: 'Eski şirket bağı — kayıtlı malik veya işletmeci değildir',
    policy: 'Tazelik politikası', unconfigured: 'Onaylı tazelik politikası yapılandırılmamış; hiçbir alan taze olarak değerlendirilemez.',
    resolved: 'Onaylı tazelik politikası uygulandı', policyUnavailable: 'Tazelik politikasına ulaşılamıyor; tazelik değerlendirilemez.',
    summary: 'Profil tazeliği', counts: (f: number, s: number, u: number, n: number) => `${f} politika süresi içinde · ${s} eski · ${u} bilinmiyor · ${n} erişilemiyor`,
    empty: 'Henüz kayıtlı profil yok. Alan kaynağı beyan etmeden önce profili kaydedin.',
    dirty: 'Kaydedilmemiş form değişiklikleri burada görünmez. Yeni değerlere kaynak beyan etmek için profili kaydedin.',
    field: 'Alan', value: 'Kayıtlı değer', version: 'Değer sürümü', source: 'Beyan edilen kaynak', freshness: 'Tazelik', verification: 'Doğrulama',
    notVerified: 'Doğrulanmadı', noSource: 'Kaynak beyan edilmedi', priorSource: 'Önceki değer için beyan edildi', revision: 'revizyon',
    declare: 'Kaynak beyan et', cancel: 'Vazgeç', submit: 'Beyan edilen kaynağı kaydet', saving: 'Kaydediliyor…', retry: 'Aynı beyanı yeniden dene',
    kind: 'Kaynak türü', reference: 'Kaynak referansı', observed: 'Gözlem zamanı (saat dilimi ile)', state: 'Bilgi durumu',
    saved: 'Beyan edilen kaynak kaydedildi. Doğrulanmamıştır.', history: 'Geçmiş', hideHistory: 'Geçmişi gizle', noHistory: 'Kayıtlı sürüm yok.',
    previous: 'Önceki', next: 'Sonraki', more: 'Bu sürüm için daha fazla beyan var.',
    errors: { 401: 'Oturumunuzun süresi doldu. Yeniden giriş yapın.', 403: 'Bu alan kaynaklarını görüntüleme yetkiniz artık yok.', 404: 'Bu gemiye erişilemiyor.',
      409: 'Alan veya kaynakları siz yükledikten sonra değişti. Yeniden beyan etmeden önce yeniden yükleyin.', 422: 'Kaynak yalnız kaydedilmiş (biliniyor veya bilinmiyor) bir değer için beyan edilebilir.',
      400: 'Kaynak bilgilerini kontrol edin.', other: 'Alan kaynakları yüklenemedi veya kaydedilemedi. Yeniden deneyin.', uncertain: 'Sonuç bilinmiyor. Yeniden deneme aynı beyanı bir kez daha gönderir.' },
    declaredBy: 'beyan eden', recorded: 'kayıt', observedLabel: 'gözlem', age: (s: number) => `yaş ${Math.floor(s / 86400)} g`,
    states: { FRESH: 'Politika süresi içinde (doğrulanmadı)', STALE: 'Eski', UNKNOWN: 'Bilinmiyor', UNAVAILABLE: 'Erişilemiyor' } as Record<string, string>,
    valueStates: { Missing: 'Eksik', Unknown: 'Bilinmiyor', Known: 'Biliniyor' } as Record<string, string>,
    kinds: { DOCUMENT: 'Belge', REGISTRY_RECORD: 'Sicil kaydı', CLASS_RECORD: 'Klas kaydı', OWNER_DECLARATION: 'Sahip beyanı',
      SURVEY_OR_MEASUREMENT: 'Sörvey veya ölçüm', OTHER: 'Diğer', UNKNOWN: 'Kaynak bilinmiyor' } as Record<string, string>,
    reasons: { VALUE_NOT_RECORDED: 'Değer kaydedilmedi', VALUE_UNKNOWN: 'Değer bilinmiyor olarak kaydedildi', FIELD_VERSION_UNAVAILABLE: 'Değer sürümü yok',
      SOURCE_NOT_DECLARED: 'Bu değer için kaynak beyan edilmedi', SOURCE_DECLARED_FOR_PRIOR_VALUE: 'Kaynak önceki bir değer için beyan edildi',
      SOURCE_UNKNOWN: 'Kaynak bilinmiyor', SOURCE_REFERENCE_NOT_RECORDED: 'Kaynak referansı kaydedilmedi', OBSERVED_AT_UNKNOWN: 'Gözlem zamanı bilinmiyor',
      OBSERVED_AT_NOT_RECORDED: 'Gözlem zamanı kaydedilmedi', OBSERVED_AT_AFTER_CHECK: 'Gözlem zamanı kontrol zamanından sonra',
      FRESHNESS_POLICY_UNCONFIGURED: 'Onaylı tazelik politikası yok', FRESHNESS_POLICY_UNAVAILABLE: 'Tazelik politikasına ulaşılamıyor',
      WITHIN_POLICY_MAX_AGE: 'Onaylı azami yaş içinde', POLICY_MAX_AGE_EXCEEDED: 'Onaylı azami yaştan eski',
      FIELDS_UNKNOWN: 'Bazı alanların tazeliği bilinmiyor', FIELDS_STALE: 'Bazı alanlar eski', FIELDS_UNAVAILABLE: 'Bazı alanlara erişilemiyor',
      ALL_FIELDS_WITHIN_POLICY: 'Tüm alanlar politika süresi içinde (doğrulanmadı)', NO_PROFILE_FIELDS: 'Profil alanı yok' } as Record<string, string>,
  },
}
type Copy = typeof copy.en
type Load = { state: 'loading' } | { state: 'ok'; data: ProfileFields } | { state: 'error'; status: number }
type Draft = { kind: string; refState: 'Missing' | 'Unknown' | 'Known'; ref: string; obsState: 'Missing' | 'Unknown' | 'Known'; obs: string }
type Pending = { path: string; key: string; command: DeclareFieldSource; uncertain: boolean }
type Props = { vessel: string; language: Locale; revision: number; dirty?: boolean; onDenied?: (error: ApiError) => void }

const label = (path: string, tr: boolean) => {
  const entry = profileFields.find(([key]) => key === path)
  return entry ? (tr ? entry[2] : entry[3]) : path
}
function FreshnessBadge({ value, t }: { value: FieldFreshness; t: Copy }) {
  return <span className="field-freshness" data-state={value.state}>
    <strong>{t.states[value.state] ?? t.states.UNKNOWN}</strong>
    <span>{t.reasons[value.reasonCode] ?? value.reasonCode}</span>
    {value.observedAt && <span>{t.observedLabel}: <time dateTime={value.observedAt}>{value.observedAt}</time></span>}
    {typeof value.ageSeconds === 'number' && <span>{t.age(value.ageSeconds)}</span>}
  </span>
}
function SourceText({ source, t }: { source: FieldSource; t: Copy }) {
  const ref = source.reference.state === 'Known' ? source.reference.value : t.valueStates[source.reference.state]
  const observed = source.observedAt.state === 'Known' ? source.observedAt.value : t.valueStates[source.observedAt.state]
  return <span className="field-source">
    <strong>{t.kinds[source.sourceKind] ?? source.sourceKind}</strong> · {ref}
    <span> · {t.observedLabel}: {observed} · {t.recorded}: <time dateTime={source.recordedAt}>{source.recordedAt}</time> · {t.declaredBy}: {source.declaredBy}</span>
  </span>
}

export function ProfileFieldProvenance({ vessel, language, revision, dirty = false, onDenied }: Props) {
  const tr = language === 'tr', t = tr ? copy.tr : copy.en
  const [open, setOpen] = useState<string | null>(null)
  const [draft, setDraft] = useState<Draft>({ kind: 'DOCUMENT', refState: 'Known', ref: '', obsState: 'Known', obs: '' })
  const [pending, setPending] = useState<Pending | null>(null)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<number | null>(null)
  const [notice, setNotice] = useState(false)
  const [history, setHistory] = useState<{ path: string; offset: number; data: FieldHistory | null; status?: number } | null>(null)
  const formId = useId()

  const [nonce, setNonce] = useState(0)
  const loadKey = `${vessel}/${revision}/${nonce}`
  const [result, setResult] = useState<{ key: string; load: Load } | null>(null)
  // A load for an older vessel/revision/reload key is never shown as current.
  const load: Load = result?.key === loadKey ? result.load : { state: 'loading' }
  const reload = useCallback(() => setNonce(n => n + 1), [])
  useEffect(() => {
    const abort = new AbortController()
    loadProfileFields(vessel, abort.signal).then(data => { if (!abort.signal.aborted) setResult({ key: loadKey, load: { state: 'ok', data } }) }, (error: unknown) => {
      if (abort.signal.aborted) return
      const status = error instanceof ApiError ? error.status : 0
      // Withdrawn access removes previously shown source details.
      setResult({ key: loadKey, load: { state: 'error', status } }); setHistory(null); setOpen(null)
      if (status === 403 && error instanceof ApiError) onDenied?.(error)
    })
    return () => abort.abort()
  }, [loadKey, vessel, onDenied])

  const openHistory = useCallback((path: string, offset: number) => {
    setHistory({ path, offset, data: null })
    const abort = new AbortController()
    loadFieldHistory(vessel, path, offset, abort.signal).then(data => setHistory(h => h?.path === path ? { path, offset, data } : h),
      (error: unknown) => setHistory(h => h?.path === path ? { path, offset, data: null, status: error instanceof ApiError ? error.status : 0 } : h))
  }, [vessel])

  function start(field: ProfileField) {
    setOpen(field.fieldPath); setFailure(null); setNotice(false); setPending(null)
    setDraft({ kind: 'DOCUMENT', refState: 'Known', ref: '', obsState: 'Known', obs: '' })
  }
  async function submit(event: FormEvent, field: ProfileField) {
    event.preventDefault()
    if (busy) return
    // An unresolved attempt keeps its original key and body until the outcome is known.
    const attempt: Pending = pending?.path === field.fieldPath && pending.uncertain ? pending : {
      path: field.fieldPath, key: crypto.randomUUID(), uncertain: false, command: {
        fieldPath: field.fieldPath, expectedValueRevision: field.valueRevision ?? 0, expectedSequence: field.latestSequence, sourceKind: draft.kind,
        reference: draft.refState === 'Known' ? { state: 'Known', value: draft.ref } : { state: draft.refState, value: null },
        observedAt: draft.obsState === 'Known' ? { state: 'Known', value: draft.obs.trim() } : { state: draft.obsState, value: null },
      } }
    setPending(attempt); setBusy(true); setFailure(null)
    try {
      const abort = new AbortController()
      const session = await loadProfileSession(abort.signal)
      await declareFieldSource(vessel, attempt.command, session.csrf, attempt.key, abort.signal)
      setPending(null); setOpen(null); setNotice(true); reload()
    } catch (error) {
      const status = error instanceof ApiError ? error.status : 0
      const unknownOutcome = status === 0 || status >= 500
      setPending(unknownOutcome ? { ...attempt, uncertain: true } : null)
      setFailure(unknownOutcome ? -1 : status)
      if (status === 403 && error instanceof ApiError) onDenied?.(error)
    } finally { setBusy(false) }
  }
  const errorText = (status: number) => status === -1 ? t.errors.uncertain : (t.errors as Record<number | string, string>)[status] ?? t.errors.other

  return <section className="profile-field-provenance" aria-label={t.title} aria-busy={load.state === 'loading'}>
    <h3>{t.title}</h3>
    {load.state === 'loading' && <p role="status">{t.loading}</p>}
    {load.state === 'error' && <div role="alert" className="field-provenance-error"><p>{errorText(load.status)}</p>
      {load.status !== 403 && load.status !== 401 && <Button variant="outline" onClick={reload}>{t.reload}</Button>}</div>}
    {load.state === 'ok' && (() => {
      const data = load.data
      return <>
        <dl className="field-provenance-meta">
          <div><dt>{t.owner}</dt><dd>{data.owner.legalEntityId}<span className="field-provenance-note">{t.binding}</span></dd></div>
          <div><dt>{t.policy}</dt><dd>{data.policy.status === 'RESOLVED' ? t.resolved : data.policy.status === 'UNAVAILABLE' ? t.policyUnavailable : t.unconfigured}</dd></div>
          <div><dt>{t.summary}</dt><dd><span className="field-freshness" data-state={data.summary.state}><strong>{t.states[data.summary.state]}</strong>
            <span>{t.reasons[data.summary.reasonCode] ?? data.summary.reasonCode}</span></span>
            <span className="field-provenance-note">{t.counts(data.summary.fresh, data.summary.stale, data.summary.unknown, data.summary.unavailable)}</span></dd></div>
        </dl>
        {notice && <p role="status">{t.saved}</p>}
        {dirty && <p className="field-provenance-note">{t.dirty}</p>}
        {data.profileRevision === 0 ? <p>{t.empty}</p> : <div className="field-provenance-table" role="region" aria-label={`${t.source} · ${t.freshness}`} tabIndex={0}><table>
          <thead><tr><th scope="col">{t.field}</th><th scope="col">{t.value}</th><th scope="col">{t.version}</th><th scope="col">{t.source}</th>
            <th scope="col">{t.freshness}</th><th scope="col">{t.verification}</th><th scope="col"><span className="sr-only">{t.declare}</span></th></tr></thead>
          <tbody>{data.fields.map(field => {
            const name = label(field.fieldPath, tr)
            return [<tr key={field.fieldPath}>
              <th scope="row">{name}</th>
              <td>{field.value.state === 'Known' ? <>{field.value.value}{field.unit ? <span className="field-provenance-note"> {field.unit}</span> : null}</> : t.valueStates[field.value.state]}</td>
              <td>{field.valueRevision != null ? `${t.revision} ${field.valueRevision}` : '—'}</td>
              <td>{field.source ? <SourceText source={field.source} t={t}/> : field.priorValueSource ? <span>{t.priorSource}</span> : <span>{t.noSource}</span>}</td>
              <td><FreshnessBadge value={field.freshness} t={t}/></td>
              <td>{t.notVerified}</td>
              <td className="field-provenance-actions">
                <Button variant="outline" size="sm" aria-expanded={history?.path === field.fieldPath} onClick={() => history?.path === field.fieldPath ? setHistory(null) : openHistory(field.fieldPath, 0)}>
                  {history?.path === field.fieldPath ? t.hideHistory : t.history}<span className="sr-only">: {name}</span></Button>
                {data.canDeclare && field.value.state !== 'Missing' && field.valueRevision != null &&
                  <Button variant="outline" size="sm" disabled={busy} onClick={() => start(field)}>{t.declare}<span className="sr-only">: {name}</span></Button>}
              </td>
            </tr>,
            open === field.fieldPath && <tr key={field.fieldPath + '-form'}><td colSpan={7}>
              <form className="field-source-form" aria-label={`${t.declare}: ${name}`} noValidate onSubmit={event => void submit(event, field)}>
                <label htmlFor={`${formId}-kind`}>{t.kind}</label>
                <select id={`${formId}-kind`} value={draft.kind} disabled={busy || !!pending?.uncertain} onChange={e => setDraft(d => ({ ...d, kind: e.target.value }))}>
                  {sourceKinds.map(kind => <option key={kind} value={kind}>{t.kinds[kind]}</option>)}</select>
                <label htmlFor={`${formId}-ref`}>{t.reference}</label>
                <div className="field-source-pair">
                  <select aria-label={`${t.reference} — ${t.state}`} value={draft.refState} disabled={busy || !!pending?.uncertain} onChange={e => setDraft(d => ({ ...d, refState: e.target.value as Draft['refState'] }))}>
                    <option value="Known">{t.valueStates.Known}</option><option value="Unknown">{t.valueStates.Unknown}</option><option value="Missing">{t.valueStates.Missing}</option></select>
                  <input id={`${formId}-ref`} maxLength={500} value={draft.ref} disabled={busy || draft.refState !== 'Known' || !!pending?.uncertain} onChange={e => setDraft(d => ({ ...d, ref: e.target.value }))}/>
                </div>
                <label htmlFor={`${formId}-obs`}>{t.observed}</label>
                <div className="field-source-pair">
                  <select aria-label={`${t.observed} — ${t.state}`} value={draft.obsState} disabled={busy || !!pending?.uncertain} onChange={e => setDraft(d => ({ ...d, obsState: e.target.value as Draft['obsState'] }))}>
                    <option value="Known">{t.valueStates.Known}</option><option value="Unknown">{t.valueStates.Unknown}</option><option value="Missing">{t.valueStates.Missing}</option></select>
                  <input id={`${formId}-obs`} placeholder="2026-01-01T00:00:00Z" value={draft.obs} disabled={busy || draft.obsState !== 'Known' || !!pending?.uncertain}
                    aria-invalid={failure === 400} onChange={e => setDraft(d => ({ ...d, obs: e.target.value }))}/>
                </div>
                {failure !== null && <div role="alert"><p>{errorText(failure)}</p>{failure === 409 && <Button variant="outline" onClick={() => { setOpen(null); reload() }}>{t.reload}</Button>}</div>}
                <div className="field-source-buttons">
                  <Button type="submit" disabled={busy || failure === 409}>{busy ? t.saving : pending?.uncertain ? t.retry : t.submit}</Button>
                  {!pending?.uncertain && <Button variant="outline" type="button" disabled={busy} onClick={() => { setOpen(null); setFailure(null) }}>{t.cancel}</Button>}
                </div>
              </form></td></tr>,
            history?.path === field.fieldPath && <tr key={field.fieldPath + '-history'}><td colSpan={7}>
              {history.status !== undefined ? <p role="alert">{errorText(history.status)}</p> : !history.data ? <p role="status">{t.loading}</p> :
                history.data.items.length === 0 ? <p>{t.noHistory}</p> : <><ol className="field-history">{history.data.items.map(item => <li key={item.valueRevision}>
                  <strong>{t.revision} {item.valueRevision}</strong> · <time dateTime={item.recordedAt}>{item.recordedAt}</time> · {item.value.state === 'Known' ? item.value.value : t.valueStates[item.value.state]}
                  {item.sources.length > 0 && <ul>{item.sources.map(source => <li key={source.sequence}><SourceText source={source} t={t}/></li>)}</ul>}
                  {item.moreSources && <p className="field-provenance-note">{t.more}</p>}
                </li>)}</ol>
                <div className="field-source-buttons">
                  <Button variant="outline" size="sm" disabled={history.offset === 0} onClick={() => openHistory(field.fieldPath, history.offset - 25)}>{t.previous}</Button>
                  <Button variant="outline" size="sm" disabled={!history.data.hasMore} onClick={() => openHistory(field.fieldPath, history.offset + 25)}>{t.next}</Button>
                </div></>}
            </td></tr>]
          })}</tbody></table></div>}
      </>
    })()}
  </section>
}
