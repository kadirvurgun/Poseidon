import { useEffect, useRef, useState } from 'react'
import { ApiError, request } from './api'
type Revision = { jobId: string; revision: number; jobNumber: string; sourceVersion: string; savedAt: string; provenance: string;
  requestedStart: string | null; requestedEnd: string | null; requiredBollardPull: string | null; requiredTugCount: number | null; jobType: string | null; priority: string | null; ownerRoleReference: string | null }
export function TugAuditRevision({ auditId, language }: { auditId: string; language: 'tr' | 'en' }) {
  const [value, setValue] = useState<Revision | null>(null), [state, setState] = useState<'idle' | 'loading' | 'error'>('idle')
  const current = useRef<AbortController | null>(null), en = language === 'en'
  useEffect(() => { setValue(null); setState('idle'); return () => current.current?.abort() }, [auditId])
  const load = async () => {
    current.current?.abort(); const controller = new AbortController(); current.current = controller; setValue(null); setState('loading')
    try {
      const response = await request<{ kind: string; tug: Revision | null }>(`audit/${encodeURIComponent(auditId)}/revision`, controller.signal)
      if (response?.kind !== 'TugJob' || !response.tug || !Number.isSafeInteger(response.tug.revision) || response.tug.revision <= 0 || typeof response.tug.jobNumber !== 'string') throw new ApiError(502)
      if (!controller.signal.aborted) { setValue(response.tug); setState('idle') }
    } catch { if (!controller.signal.aborted) setState('error') }
  }
  return <section aria-label={en ? 'Exact tug revision' : 'Tam römorkör revizyonu'}><button disabled={state === 'loading'} onClick={() => void load()}>{en ? 'Review exact immutable tug revision' : 'Değişmez römorkör revizyonunu incele'}</button>
    {state === 'loading' && <p role="status">{en ? 'Loading revision…' : 'Revizyon yükleniyor…'}</p>}
    {state === 'error' && <p role="alert">{en ? 'Authorized historical revision unavailable.' : 'Yetkili tarihsel revizyon kullanılamıyor.'}</p>}
    {value && <><p>{en ? 'Historic unverified request facts. Current planning, assignment and readiness require separate evidence.' : 'Tarihsel doğrulanmamış talep olguları. Güncel planlama, atama ve hazırlık ayrı kanıt gerektirir.'}</p><dl>
      {([['jobNumber', 'Job number', 'İş numarası'], ['revision', 'Revision', 'Revizyon'], ['savedAt', 'Saved at', 'Kayıt zamanı'],
        ['sourceVersion', 'Source version', 'Kaynak sürümü'], ['requestedStart', 'Requested start', 'Talep başlangıcı'], ['requestedEnd', 'Requested end', 'Talep bitişi'],
        ['requiredBollardPull', 'Required BP', 'Gerekli BP'], ['requiredTugCount', 'Required tug count', 'Gerekli römorkör sayısı'], ['jobType', 'Job type', 'İş tipi'],
        ['priority', 'Priority', 'Öncelik'], ['ownerRoleReference', 'Owner role reference', 'Sorumlu rol referansı']] as const).map(([key, english, turkish]) => <div key={key}><dt>{en ? english : turkish}</dt><dd>{value[key] ?? (en ? 'Missing' : 'Eksik')}</dd></div>)}
    </dl></>}
  </section>
}
