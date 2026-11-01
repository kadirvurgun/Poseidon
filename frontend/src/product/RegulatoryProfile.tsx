import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError } from './api'
import * as api from './profile-api'
import { profileFields } from './profile-contract'
import type { Profile, ProfileCommand, ProfileContent, ProfileHistory } from './profile-contract'
import { RequiredDataField } from './RequiredDataField'
import { loginHref } from './workspace-location'

const copy = {
  en: { title: 'Regulatory profile draft', note: 'Incomplete drafts can be saved. Declarations and reference values are unverified. This draft does not authorize submission or operations.', loading: 'Loading profile…', empty: 'No saved profile draft yet.', save: 'Save draft', saving: 'Saving…', saved: 'Draft saved.', retry: 'Retry same command', reload: 'Reload saved draft', conflict: 'The saved profile changed. Your draft is retained. Reload explicitly to discard it.', uncertain: 'The save outcome is unknown. Fields are frozen; retry the same command before leaving.', failed: 'Unable to load or save the profile.', denied: 'You do not have permission for this profile.', expired: 'Sign in to continue.', missing: 'Profile not found in your vessel scope.', history: 'Version history', noHistory: 'No saved versions.', observed: 'Source observation time', time: 'Saved at', provenance: 'Source status', revision: 'Version', unknown: 'Unknown', previous: 'Previous', next: 'Next', view: 'View saved values', count: 'fields have missing or unknown data. Mandatory submission policy is unresolved; draft only.', readonly: 'Read-only profile', reloadRead: 'Retry loading' },
  tr: { title: 'Mevzuat profili taslağı', note: 'Eksik taslaklar kaydedilebilir. Beyanlar ve referans değerleri doğrulanmamıştır. Bu taslak onaya gönderme veya operasyon yetkisi vermez.', loading: 'Profil yükleniyor…', empty: 'Henüz kaydedilmiş profil taslağı yok.', save: 'Taslağı kaydet', saving: 'Kaydediliyor…', saved: 'Taslak kaydedildi.', retry: 'Aynı komutu tekrar dene', reload: 'Kayıtlı taslağı yeniden yükle', conflict: 'Kayıtlı profil değişti. Taslağınız korundu. Silmek için yeniden yüklemeyi seçin.', uncertain: 'Kayıt sonucu bilinmiyor. Alanlar kilitli; ayrılmadan önce aynı komutu tekrar deneyin.', failed: 'Profil yüklenemedi veya kaydedilemedi.', denied: 'Bu profil için yetkiniz yok.', expired: 'Devam etmek için oturum açın.', missing: 'Yetkili tekne kapsamınızda profil bulunamadı.', history: 'Sürüm geçmişi', noHistory: 'Kayıtlı sürüm yok.', observed: 'Kaynak gözlem zamanı', time: 'Kayıt zamanı', provenance: 'Kaynak durumu', revision: 'Sürüm', unknown: 'Bilinmiyor', previous: 'Önceki', next: 'Sonraki', view: 'Kayıtlı değerleri görüntüle', count: 'alanda eksik veya bilinmeyen bilgi var. Zorunlu gönderim politikası açık; yalnız taslak.', readonly: 'Salt okunur profil', reloadRead: 'Yüklemeyi tekrar dene' },
}
type Props = { id: string; language: 'tr' | 'en'; csrf: string; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void }
export function RegulatoryProfile({ id, language, csrf, onDirty, onSaving }: Props) {
  const [profile, setProfile] = useState<Profile | null>(null)
  const [content, setContent] = useState<ProfileContent | null>(null)
  const [history, setHistory] = useState<ProfileHistory | null>(null)
  const [historyAttempt, setHistoryAttempt] = useState(0)
  const [historyOffset, setHistoryOffset] = useState(0)
  const [historyFailure, setHistoryFailure] = useState(false)
  const [failure, setFailure] = useState<ApiError | null>(null)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [attempt, setAttempt] = useState(0)
  const [saving, setSaving] = useState(false)
  const [uncertain, setUncertain] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [notice, setNotice] = useState(false)
  const busy = useRef(false)
  const frozen = useRef<{ key: string; body: ProfileCommand } | null>(null)
  const abort = useRef<AbortController | null>(null)
  const t = copy[language]
  useEffect(() => {
    const controller = new AbortController(); let active = true
    const timeout = setTimeout(() => controller.abort(), 10000)
    api.loadProfile(id, controller.signal).then(value => { if (active) { setProfile(value); setContent(value.content) } })
      .catch(error => { if (active) setFailure(error instanceof ApiError ? error : new ApiError(0)) })
    return () => { active = false; clearTimeout(timeout); controller.abort(); abort.current?.abort() }
  }, [id, attempt])
  useEffect(() => {
    const controller = new AbortController(); let active = true
    const timeout = setTimeout(() => controller.abort(), 10000)
    api.loadProfileHistory(id, historyOffset, controller.signal).then(value => { if (active) { setHistory(value); setHistoryFailure(false) } })
      .catch(() => { if (active) setHistoryFailure(true) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [id, historyOffset, attempt, historyAttempt])
  useEffect(() => {
    const prevent = (event: BeforeUnloadEvent) => { if (dirty || busy.current || uncertain) event.preventDefault() }
    window.addEventListener('beforeunload', prevent)
    return () => window.removeEventListener('beforeunload', prevent)
  }, [dirty, uncertain])
  function change(key: keyof ProfileContent, value: ProfileContent[typeof key]) {
    if (saving || uncertain) return
    setContent(current => ({ ...current, [key]: value })); setDirty(true); onDirty(true); setNotice(false)
    setErrors(current => ({ ...current, [key]: [] }))
    // A stale base stays blocked until explicit reload, even if local fields change.
    if (failure?.code !== 'version_conflict') setFailure(null)
    frozen.current = null
  }
  function reload() {
    if (busy.current || uncertain) return
    setProfile(null); setContent(null); setFailure(null); setErrors({}); setDirty(false); onDirty(false)
    setNotice(false); setHistory(null); setHistoryOffset(0); frozen.current = null; setAttempt(v => v + 1)
  }
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (busy.current || !profile || !content || !profile.vessel.canEdit || failure?.code === 'version_conflict') return
    if (!frozen.current) frozen.current = { key: crypto.randomUUID(), body: { expectedVersion: profile.version, content: structuredClone(content) } }
    const command = frozen.current
    busy.current = true; setSaving(true); onSaving(true); onDirty(true); setDirty(true); setNotice(false)
    const controller = new AbortController(); abort.current = controller
    const timeout = setTimeout(() => controller.abort(), 15000)
    let navigationFrozen = false
    try {
      const result = await api.saveProfile(id, command.body, csrf, command.key, controller.signal)
      setProfile(result); setContent(result.content); setFailure(null); setErrors({}); setUncertain(false)
      setDirty(false); onDirty(false); frozen.current = null; setNotice(true)
      setHistoryOffset(0); setHistoryAttempt(v => v + 1)
    } catch (error) {
      const value = error instanceof ApiError ? error : new ApiError(0)
      setFailure(value); setErrors(value.errors)
      const unknown = value.status === 0 || value.status >= 500
      setUncertain(unknown); navigationFrozen = unknown
      if (!unknown) frozen.current = null
    } finally { clearTimeout(timeout); busy.current = false; setSaving(false); onSaving(navigationFrozen) }
  }
  const status = failure?.status
  const errorText = failure?.code === 'version_conflict' ? t.conflict : uncertain ? t.uncertain : status === 401 ? t.expired : status === 403 ? t.denied : status === 404 ? t.missing : t.failed
  const provenance = (value: string) => ({ Synthetic: language === 'tr' ? 'Sentetik' : 'Synthetic', Missing: language === 'tr' ? 'Eksik' : 'Missing', Unknown: t.unknown, DeclaredUnverified: language === 'tr' ? 'Beyan — doğrulanmamış' : 'Declared — unverified' })[value] ?? t.unknown
  return <section className="regulatory-profile" aria-label={t.title}>
    <h2>{t.title}</h2><p className="profile-draft-note">{t.note}</p>
    {failure && <div role="alert"><p>{errorText}</p>{status === 401 && <a href={loginHref()}>{language === 'tr' ? 'Oturum aç' : 'Sign in'}</a>}
      {!profile && <button onClick={reload}>{t.reloadRead}</button>}
      {failure.code === 'version_conflict' && <button onClick={reload}>{t.reload}</button>}</div>}
    {!profile && !failure && <p role="status">{t.loading}</p>}
    {notice && <p role="status">{t.saved}</p>}
    {profile && content && <>
      <p className="profile-context">{profile.tenantId} · <strong>{profile.vessel.code} · {profile.vessel.name}</strong> · {profile.vessel.kind === 'Ship' ? (language === 'tr' ? 'Gemi' : 'Ship') : (language === 'tr' ? 'Römorkör' : 'Tug')}</p>
      {!profile.revision && <p>{t.empty}</p>}
      <p>{t.provenance}: {provenance(profile.provenance)} · {t.revision}: {profile.revision} · {t.time}: {profile.savedAt ?? t.unknown}</p>
      <p>{profileFields.filter(([key]) => content[key]?.state !== 'Known').length} {t.count}</p>
      {!profile.vessel.canEdit && <p>{t.readonly}</p>}
      <form noValidate aria-busy={saving} onSubmit={event => void submit(event)}>
        <div className="profile-field-grid">{profileFields.map(([key, kind, tr, en]) => {
          const common = { label: language === 'tr' ? tr : en, language, disabled: !profile.vessel.canEdit || saving || uncertain, error: !!errors[key]?.length }
          return kind === 'number' ? <RequiredDataField key={key} {...common} kind="number" value={content[key] ?? { state: 'Missing' }} onChange={value => change(key, value)} /> :
            <RequiredDataField key={key} {...common} kind="text" value={content[key] ?? { state: 'Missing' }} onChange={value => change(key, value)} />
        })}</div>
        {profile.vessel.canEdit && <div className="form-actions"><button type="submit" className="primary-button" disabled={saving || failure?.code === 'version_conflict'}>{saving ? t.saving : uncertain ? t.retry : t.save}</button></div>}
      </form>
      <h3>{t.history}</h3>
      {historyFailure ? <div role="alert">{t.failed}<button onClick={() => setHistoryAttempt(v => v + 1)} disabled={saving || uncertain}>{t.reloadRead}</button></div> : !history ? <p role="status">{t.loading}</p> : <>
        {!history.items.length && <p>{t.noHistory}</p>}
        {history.items.map(row => <details key={row.revision}><summary>{t.revision} {row.revision} · {row.savedAt} · {provenance(row.provenance)}</summary>
          <dl>{profileFields.map(([key, , tr, en]) => <div key={key}><dt>{language === 'tr' ? tr : en}</dt><dd>{row.content[key]?.state === 'Known' ? String(row.content[key]?.value) : row.content[key]?.state === 'Unknown' ? t.unknown : (language === 'tr' ? 'Eksik' : 'Missing')}</dd></div>)}</dl></details>)}
        <div className="form-actions"><button disabled={historyOffset === 0} onClick={() => { setHistory(null); setHistoryOffset(v => v - 25) }}>{t.previous}</button><button disabled={!history.hasMore} onClick={() => { setHistory(null); setHistoryOffset(v => v + 25) }}>{t.next}</button></div>
      </>}
    </>}
  </section>
}
