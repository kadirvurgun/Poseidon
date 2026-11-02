import { useEffect, useState } from 'react'
import { ApiError } from './api'
import { history, type CrewHistoryRow } from './crew-api'

export function CrewAssignmentHistory({ vessel, assignment, language }: { vessel: string; assignment: string; language: 'tr' | 'en' }) {
  const en = language === 'en', [rows, setRows] = useState<CrewHistoryRow[] | null>(null), [error, setError] = useState(0), [generation, reload] = useState(0)
  const [offset, setOffset] = useState(0), [more, setMore] = useState(false)
  useEffect(() => {
    const c = new AbortController(); setRows(null); setError(0)
    history(vessel, assignment, c.signal, offset).then(v => { if (!c.signal.aborted) { setRows(v.items); setMore(v.hasMore) } })
      .catch(e => { if (!c.signal.aborted) { setRows(null); setError(e instanceof ApiError ? e.status : 503) } })
    return () => c.abort()
  }, [vessel, assignment, generation, offset])
  return <section aria-label={en ? 'Assignment history' : 'Atama geçmişi'}>
    <h2>{en ? 'Assignment history' : 'Atama geçmişi'}</h2>
    <p>{en ? 'Unverified declarations and original policy versions. Historical identity and watch text are withheld.' : 'Doğrulanmamış beyanlar ve özgün politika sürümleri. Geçmiş kimlik ve vardiya metni gizlidir.'}</p>
    {!rows && !error && <p role="status">{en ? 'Loading history…' : 'Geçmiş yükleniyor…'}</p>}
    {error > 0 && <p role="alert">{[401, 403, 404].includes(error) ? (en ? 'Assignment history access denied.' : 'Atama geçmişi erişimi reddedildi.') : (en ? 'Assignment history unavailable.' : 'Atama geçmişi kullanılamıyor.')}</p>}
    <button onClick={() => reload(x => x + 1)}>{en ? 'Reload assignment history' : 'Atama geçmişini yenile'}</button>
    <button disabled={!rows || offset === 0} onClick={() => setOffset(x => Math.max(0, x - 25))}>{en ? 'Previous assignment history' : 'Önceki atama geçmişi'}</button>
    <button disabled={!rows || !more} onClick={() => setOffset(x => x + 25)}>{en ? 'Next assignment history' : 'Sonraki atama geçmişi'}</button>
    {rows && (rows.length === 0 ? <p>{en ? 'No assignment revisions.' : 'Atama sürümü yok.'}</p> : <ol>{rows.map(r => <li key={r.revision}>
      {r.revision} · {r.status} · {r.from ?? (en ? 'Unknown' : 'Bilinmiyor')} — {r.to ?? (en ? 'Unknown' : 'Bilinmiyor')}
      <dl><dt>{en ? 'Recorded at' : 'Kayıt zamanı'}</dt><dd>{r.savedAt}</dd><dt>{en ? 'Declaration policy' : 'Beyan politikası'}</dt><dd>{r.assignmentPolicyVersion ?? (en ? 'Historical policy unknown' : 'Geçmiş politika bilinmiyor')}</dd>
        <dt>{en ? 'Position catalog' : 'Pozisyon kataloğu'}</dt><dd>{r.positionPolicyVersion ?? (en ? 'Historical policy unknown' : 'Geçmiş politika bilinmiyor')}</dd></dl>
    </li>)}</ol>)}
  </section>
}
