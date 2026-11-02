import { useEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import { filePage, type FileRecord } from './file-api'
import type { ProfileContent } from './profile-contract'

// Ordinary vessel files only. The server independently excludes person/document stores.
export function ConstructionRecordPicker({ vessel, language, value, disabled, onChange, onDenied }: {
  vessel: string; language: 'tr' | 'en'; value: ProfileContent['constructionRecord']; disabled: boolean;
  onChange: (v: ProfileContent['constructionRecord']) => void; onDenied: (error: ApiError) => unknown;
}) {
  const [rows, setRows] = useState<FileRecord[]>([]), [offset, setOffset] = useState(0), [more, setMore] = useState(false)
  const [loading, setLoading] = useState(true), [failure, setFailure] = useState(false), [attempt, setAttempt] = useState(0)
  const [offline, setOffline] = useState(!navigator.onLine)
  const selected = useRef(value); selected.current = value
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  useEffect(() => {
    const online = () => setOffline(false), offline = () => setOffline(true)
    window.addEventListener('online', online); window.addEventListener('offline', offline)
    return () => { window.removeEventListener('online', online); window.removeEventListener('offline', offline) }
  }, [])
  useEffect(() => {
    setRows([]); setMore(false); setFailure(false)
    if (offline) { setLoading(false); return }
    const controller = new AbortController(); let active = true
    const timer = setTimeout(() => controller.abort(), 10000); setLoading(true)
    filePage(vessel, offset, controller.signal).then(page => {
      if (active) { setRows(page.items.filter(f => f.state === 'CleanTransport')); setMore(page.hasMore); setFailure(!page.available) }
    }).catch(error => {
      if (active) {
        setFailure(true)
        if (error instanceof ApiError && (error.status === 401 || selected.current && [403, 404].includes(error.status))) onDenied(error)
      }
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false; controller.abort(); clearTimeout(timer) }
  }, [vessel, offset, attempt, offline, onDenied])
  return <fieldset disabled={disabled || loading || offline}>
    <legend>{t('Construction source record', 'İnşa kaynak kaydı')}</legend>
    <p>{t('A clean file is transport only. The exact date, record and profile require source-scoped human attestation.', 'Temiz dosya yalnız aktarım durumudur. Kesin tarih, kayıt ve profil kaynak kapsamında insan teyidi gerektirir.')}</p>
    {loading && <p role="status">{t('Loading vessel records…', 'Araç kayıtları yükleniyor…')}</p>}
    {offline && <p role="status">{t('Offline: record selection unavailable.', 'Çevrimdışı: kayıt seçimi kullanılamıyor.')}</p>}
    {failure && <p role="alert">{t('Record access unavailable. Reload the profile to retry.', 'Kayıtlara erişilemiyor. Tekrar denemek için profili yükleyin.')}</p>}
    {!loading && !failure && !offline && rows.length === 0 && <p>{t('No clean vessel records on this page.', 'Bu sayfada temiz araç kaydı yok.')}</p>}
    <label>{t('Class / build record file', 'Klas / inşa kayıt dosyası')}<select disabled={failure} value={value?.id ?? ''} onChange={e => {
      const file = rows.find(f => f.id === e.target.value)
      onChange(file?.revision && file.checksum ? { id: file.id, revision: file.revision, checksum: file.checksum } : null)
    }}><option value="">{t('Missing: incomplete draft', 'Eksik: tamamlanmamış taslak')}</option>
      {value && !rows.some(f => f.id === value.id) && <option value={value.id}>{value.id} / {value.revision}</option>}
      {rows.map(f => <option key={f.id} value={f.id}>{f.name} / {f.revision}</option>)}</select></label>
    <button type="button" disabled={failure || offset === 0} onClick={() => setOffset(n => n - 25)}>{t('Previous records', 'Önceki kayıtlar')}</button>
    <button type="button" disabled={failure || !more} onClick={() => setOffset(n => n + 25)}>{t('Next records', 'Sonraki kayıtlar')}</button>
    <button type="button" onClick={() => setAttempt(n => n + 1)}>{t('Refresh records', 'Kayıtları yenile')}</button>
  </fieldset>
}
