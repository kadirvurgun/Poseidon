import { useLayoutEffect, useEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import { crewPhoto, saveCrewPhoto, type CrewPhoto, type PhotoAttempt } from './crew-photo-api'
import type { CrewFileManifest, CrewFileVersion } from './crew-files-api'

type Props = { vessel: string; assignment: string; person: string; csrf: string; language: 'tr' | 'en'; manifest: CrewFileManifest;
  blocked: boolean; onDirty: (value: boolean) => void; onReview: (row: CrewFileVersion) => void; onDenied: () => void }
export function CrewPhotoSelection(p: Props) {
  const en = p.language === 'en'; const scope = `${p.vessel}:${p.assignment}:${p.person}:${p.csrf}`
  const [loaded, setLoaded] = useState<{ scope: string; data: CrewPhoto } | null>(null)
  const [attempt, setAttempt] = useState<{ scope: string; data: PhotoAttempt } | null>(null)
  const [failure, setFailure] = useState<'load' | 'uncertain' | 'conflict' | null>(null)
  const [busy, setBusy] = useState(false); const [generation, reload] = useState(0)
  const controller = useRef<AbortController | null>(null); const callbacks = useRef(p); useLayoutEffect(() => { callbacks.current = p }, [p])
  const data = loaded?.scope === scope ? loaded.data : null
  const pending = attempt?.scope === scope ? attempt.data : null
  useEffect(() => {
    const c = new AbortController(); controller.current = c; let active = true
    crewPhoto(p.vessel, p.assignment, p.person, c.signal).then(data => { if (active) { setLoaded({ scope, data }); setFailure(null) } }).catch(e => {
      if (!active) return
      setLoaded(null); setFailure('load')
      if (e instanceof ApiError && [401, 403, 404].includes(e.status)) callbacks.current.onDenied()
    })
    return () => { active = false; c.abort(); callbacks.current.onDirty(false) }
  }, [scope, generation, p.vessel, p.assignment, p.person])
  async function save(fileId: string | null) {
    if (!data || busy || p.blocked) return
    const operation = pending ?? { key: crypto.randomUUID(), command: { baseVersion: data.version, fileId, policyVersion: data.policyVersion, purpose: data.purpose } }
    setAttempt({ scope, data: operation }); p.onDirty(true); setBusy(true)
    const c = controller.current!
    try {
      await saveCrewPhoto(p.vessel, p.assignment, p.person, operation, p.csrf, c.signal)
      if (c.signal.aborted) return
      setAttempt(null); p.onDirty(false); setLoaded(null); reload(n => n + 1)
    } catch (e) {
      if (c.signal.aborted) return
      if (e instanceof ApiError && [400, 401, 403, 404].includes(e.status)) {
        setLoaded(null); setAttempt(null); p.onDirty(false); p.onDenied()
      } else setFailure(e instanceof ApiError && e.status === 409 ? 'conflict' : 'uncertain')
    } finally { if (!c.signal.aborted) setBusy(false) }
  }
  const photos = p.manifest.items.filter(f => f.kind === 'Photo' && f.state === 'CleanTransport')
  return <section aria-label={en ? 'Selected profile photo' : 'Seçili profil fotoğrafı'}>
    <h3>{en ? 'Selected profile photo' : 'Seçili profil fotoğrafı'}</h3>
    <p>{en ? 'Selection is a declaration and does not verify identity.' : 'Seçim bir beyandır; kimlik doğrulaması sağlamaz.'}</p>
    {failure && <p role="alert">{failure === 'conflict' ? (en ? 'Profile changed. Review current data before a new selection.' : 'Profil değişti. Yeni seçimden önce güncel veriyi inceleyin.') : failure === 'uncertain' ? (en ? 'Result uncertain. Retry the same selection.' : 'Sonuç belirsiz. Aynı seçimi yeniden deneyin.') : (en ? 'Photo selection unavailable.' : 'Fotoğraf seçimi kullanılamıyor.')}</p>}
    {data && <>
      <p role="status">{data.state === 'Missing' ? (en ? 'No photo selected; unknown.' : 'Fotoğraf seçilmedi; bilinmiyor.') : data.state === 'Unavailable' ? (en ? 'Saved photo is currently unavailable.' : 'Kayıtlı fotoğraf şu anda kullanılamıyor.') : (en ? 'Selected, unverified.' : 'Seçili, doğrulanmamış.')}</p>
      {data.selected && <button disabled={p.blocked || busy || !!pending} onClick={() => p.onReview(data.selected!)}>{en ? 'Review selected photo' : 'Seçili fotoğrafı incele'} · {data.selected.revision}</button>}
      {data.canWrite && <fieldset disabled={p.blocked || busy || !!pending}>
        {photos.map(f => <button key={f.id} onClick={() => void save(f.id)}>{en ? 'Select photo version' : 'Fotoğraf sürümünü seç'} · {f.revision}</button>)}
        <button disabled={data.state === 'Missing'} onClick={() => void save(null)}>{en ? 'Clear photo selection' : 'Fotoğraf seçimini kaldır'}</button>
      </fieldset>}
    </>}
    {pending && failure === 'uncertain' && <button disabled={busy || p.blocked} onClick={() => void save(pending.command.fileId)}>{en ? 'Retry same photo selection' : 'Aynı fotoğraf seçimini yeniden dene'}</button>}
    {pending && failure === 'conflict' && <button disabled={busy} onClick={() => { setAttempt(null); setLoaded(null); p.onDirty(false); reload(n => n + 1) }}>{en ? 'Discard conflicted selection and reload' : 'Çatışan seçimi bırak ve yeniden yükle'}</button>}
    {!pending && <button disabled={busy || p.blocked} onClick={() => { setLoaded(null); reload(n => n + 1) }}>{en ? 'Refresh photo selection' : 'Fotoğraf seçimini yenile'}</button>}
  </section>
}
