import { useEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import { readCatalog, saveCatalog, type Catalog, type CatalogCommand } from './crew-catalog-api'

// Mounted with the actual session/context key by the owning workspace. No persistence of proposal text.
export function CrewCatalog({ vessel, language, csrf, onDirty, onChanged }: { vessel: string; language: 'tr' | 'en'; csrf: string; onDirty: (dirty: boolean) => void; onChanged: () => void }) {
  const en = language === 'en', [data, setData] = useState<Catalog | null>(null), [error, setError] = useState(0)
  const [generation, reload] = useState(0), [reason, setReason] = useState(''), [proposal, setProposal] = useState('')
  const [state, setState] = useState<'idle' | 'saving' | 'uncertain' | 'conflict' | 'invalid'>('idle')
  const frozen = useRef<{ key: string; command: CatalogCommand } | null>(null), pending = useRef(false), live = useRef(true)
  const controller = useRef<AbortController | null>(null)
  useEffect(() => { live.current = true; return () => { live.current = false; controller.current?.abort() } }, [])
  useEffect(() => { onDirty(reason.length > 0 || proposal.length > 0 || state === 'uncertain' || state === 'saving'); return () => onDirty(false) }, [reason, proposal, state, onDirty])
  useEffect(() => {
    const c = new AbortController(); setData(null); setError(0)
    readCatalog(vessel, c.signal).then(v => { if (!c.signal.aborted) setData(v) }).catch(e => { if (!c.signal.aborted) setError(e instanceof ApiError ? e.status : 503) })
    return () => c.abort()
  }, [vessel, generation])
  async function save(action: Catalog['actions'][number]) {
    if (pending.current || !data || !navigator.onLine) return
    if (!frozen.current) {
      if (!reason.trim()) { setState('invalid'); return }
      const state = { draft: 'Draft', review: 'Reviewed', adopt: 'Adopted', withdraw: 'Withdrawn' }[action.action] as CatalogCommand['state']
      let content
      try { content = action.action === 'draft' ? JSON.parse(proposal) : undefined } catch { setState('invalid'); return }
      frozen.current = { key: crypto.randomUUID(), command: { expectedVersion: data.version, authorityId: action.authorityId, state, reason, ...(content ? { content } : {}) } }
    }
    pending.current = true; setState('saving'); const c = new AbortController(); controller.current = c
    try {
      await saveCatalog(vessel, frozen.current.command, frozen.current.key, csrf, c.signal)
      if (!live.current || c.signal.aborted) return
      frozen.current = null; setReason(''); setProposal(''); setState('idle'); reload(v => v + 1); onChanged()
    } catch (e) {
      if (!live.current || c.signal.aborted) return
      if (e instanceof ApiError && [400, 403, 404, 409, 401].includes(e.status)) { frozen.current = null; setState(e.status === 409 ? 'conflict' : 'invalid'); setError(e.status); if ([401, 403, 404].includes(e.status)) { setData(null); setReason(''); setProposal('') } }
      else setState('uncertain')
    } finally { pending.current = false }
  }
  return <section aria-label={en ? 'Position catalog governance' : 'Pozisyon kataloğu yönetimi'}><h2>{en ? 'Position catalog governance' : 'Pozisyon kataloğu yönetimi'}</h2>
    <p>{en ? 'Catalog declarations do not establish qualification, fitness or a safety decision. Independent appointed human authority is required.' : 'Katalog beyanları yetkinlik, sağlık uygunluğu veya emniyet kararı değildir. Bağımsız atanmış insan yetkisi gerekir.'}</p>
    {!data && !error && <p role="status">{en ? 'Loading catalog…' : 'Katalog yükleniyor…'}</p>}
    {error > 0 && <p role="alert">{[401, 403, 404].includes(error) ? (en ? 'Catalog access denied.' : 'Katalog erişimi reddedildi.') : (en ? 'Catalog unavailable or changed.' : 'Katalog kullanılamıyor veya değişti.')}</p>}
    {data && <><p>{en ? 'Active revision' : 'Etkin sürüm'}: {data.activeRevision ?? (en ? 'No durable adoption' : 'Kalıcı kabul yok')}</p>
      <ol>{data.items.map(e => <li key={e.revision}>{e.revision} · {e.state} · {e.policyVersion} · {e.savedAt}<p>{e.reason}</p></li>)}</ol>
      {data.actions.length > 0 && <><fieldset disabled={state === 'saving' || state === 'uncertain' || state === 'conflict'}>
        <label>{en ? 'Reason' : 'Gerekçe'}<textarea maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label>
        {data.actions.some(a => a.action === 'draft') && <label>{en ? 'Source-defined catalog proposal (JSON)' : 'Kaynak tanımlı katalog önerisi (JSON)'}<textarea value={proposal} onChange={e => setProposal(e.target.value)} /></label>}
      </fieldset>{data.actions.map(a => <button key={a.action} disabled={state === 'saving' || state === 'conflict' || !navigator.onLine || state === 'uncertain' && frozen.current?.command.state !== ({ draft: 'Draft', review: 'Reviewed', adopt: 'Adopted', withdraw: 'Withdrawn' }[a.action])} onClick={() => void save(a)}>{state === 'uncertain' ? (en ? 'Retry exact command' : 'Aynı komutu tekrar dene') : ({ draft: en ? 'Propose revision' : 'Sürüm öner', review: en ? 'Record review' : 'İncelemeyi kaydet', adopt: en ? 'Adopt catalog' : 'Kataloğu kabul et', withdraw: en ? 'Withdraw catalog' : 'Kataloğu geri çek' }[a.action])}</button>)}</>}
    </>}
    {state === 'uncertain' && <p role="alert">{en ? 'Result uncertain. Retry the same command before editing.' : 'Sonuç belirsiz. Düzenlemeden önce aynı komutu tekrar deneyin.'}</p>}
    {state === 'invalid' && <p role="alert">{en ? 'Check the source proposal, reason and current authority.' : 'Kaynak önerisini, gerekçeyi ve güncel yetkiyi kontrol edin.'}</p>}
    <button disabled={state === 'saving' || state === 'uncertain'} onClick={() => { frozen.current = null; setReason(''); setProposal(''); setState('idle'); reload(v => v + 1) }}>{en ? 'Discard and reload catalog' : 'Vazgeç ve kataloğu yenile'}</button>
  </section>
}
