import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Anchor, ArrowUpRight, Search, Ship, WifiOff, X } from 'lucide-react'
import { Button } from '../components/ui/button'
import * as api from './api'
import type { Session } from './directory-contract'
import { isGuid } from './identifiers'
import { loadWorkspaceContext } from './workspace-context-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import { decodeTodayVessels } from './today-api'
import './workspace-vessel-switcher.css'

type Props = { session: Session; vessel: string | null; language: 'tr' | 'en'; onClose: () => void;
  onNavigate: (href: string, committed?: () => void) => void }

// A context switch carries no form/entity ID from the old vessel into the new one.
export function vesselContextHref(path: string, vessel: string, kind: 'Ship' | 'Tug') {
  if (!isGuid(vessel)) throw new api.ApiError(400, 'invalid_context')
  const id = vessel.toLowerCase()
  if (path === '/app/system-health') return `/app/system-health?vessel=${id}`
  if (path === '/app/notification-escalation') return `/app/notification-escalation?vessel=${id}`
  if (/^\/app\/today(?:\/|$)/.test(path)) return `/app/today/${id}`
  const family = /^\/app\/(work-order-center|defects|crew-roster|equipment-hierarchy|maintenance-plan|excel-migration)(?:\/|$)/.exec(path)
  if (family) return `/app/${family[1]}/${id}`
  const tug = /^\/app\/(tug-command|tug-job-board|tug-job-intake)(?:\/|$)/.exec(path)
  if (tug && kind === 'Tug') return `/app/${tug[1]}?vessel=${id}`
  return `/app/fleet/${id}`
}

export function WorkspaceVesselSwitcher({ session, vessel, language, onClose, onNavigate }: Props) {
  const en = language === 'en'; const epoch = useWorkspaceRevalidation()
  const dialog = useRef<HTMLDialogElement>(null)
  const [query, setQuery] = useState(''); const [search, setSearch] = useState('')
  const [offset, setOffset] = useState(0); const [attempt, retry] = useState(0)
  const scope = `${epoch}:${offset}:${attempt}:${search}`
  const [result, setResult] = useState<{ owner: Session; epoch: number; offset: number; search: string; attempt: number; data?: api.Page<api.Vessel>; failed?: boolean } | null>(null)
  const [selection, setSelection] = useState<{ owner: Session; scope: string; id: string; state: 'checking' | 'denied' } | null>(null)
  const check = useRef<AbortController | null>(null)
  const alive = useRef(true)
  const online = navigator.onLine; const visible = document.visibilityState !== 'hidden'
  const current = result?.owner === session && result.epoch === epoch && result.offset === offset && result.search === search && result.attempt === attempt ? result : null
  const choosing = selection?.owner === session && selection.scope === scope ? selection : null
  const activeScope = useRef<{ session: Session; scope: string } | null>(null)
  useLayoutEffect(() => {
    activeScope.current = { session, scope }
    return () => { activeScope.current = null; check.current?.abort() }
  }, [session, scope])
  useEffect(() => {
    alive.current = true
    const previous = document.activeElement as HTMLElement | null
    const node = dialog.current; node?.showModal()
    return () => { alive.current = false; node?.close(); check.current?.abort(); if (previous?.isConnected) previous.focus() }
  }, [])
  useEffect(() => {
    check.current?.abort()
    if (!online || !visible) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => { c.abort(); if (active) setResult({ owner: session, epoch, offset, search, attempt, failed: true }) }, 10000)
    api.vessels(offset, '', c.signal, search).then(data => decodeTodayVessels(data, offset)).then(data => {
      if (active && !c.signal.aborted) setResult({ owner: session, epoch, offset, search, attempt, data })
    }).catch(() => { if (active) setResult({ owner: session, epoch, offset, search, attempt, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; clearTimeout(timer); c.abort(); check.current?.abort() }
  }, [session, epoch, offset, search, attempt, online, visible])
  async function choose(id: string) {
    if (!online || !visible || choosing?.state === 'checking' || !current?.data?.items.some(row => row.id === id) || !session.tenantId || !session.actorId) return
    check.current?.abort(); const c = new AbortController(); check.current = c
    const stillCurrent = () => alive.current && activeScope.current?.session === session && activeScope.current.scope === scope
    const timer = setTimeout(() => { c.abort(); if (stillCurrent()) setSelection({ owner: session, scope, id, state: 'denied' }) }, 10000)
    setSelection({ owner: session, scope, id, state: 'checking' })
    try {
      // A list result is not a switch grant. Recheck the target against the server
      // before handing it to the workspace's existing unsaved-change guard.
      const target = await loadWorkspaceContext(session.tenantId, session.actorId, id, c.signal)
      if (c.signal.aborted || !stillCurrent() || !navigator.onLine || document.visibilityState === 'hidden') return
      if (!target.vessel) throw new api.ApiError(502, 'invalid_context')
      setSelection(null)
      onNavigate(vesselContextHref(window.location.pathname, target.vessel.id, target.vessel.kind), onClose)
    } catch {
      if (stillCurrent()) setSelection({ owner: session, scope, id, state: 'denied' })
    } finally { clearTimeout(timer) }
  }
  const title = en ? 'Change vessel context' : 'Araç bağlamını değiştir'
  return <dialog ref={dialog} className="workspace-vessel-switcher" aria-labelledby="vessel-switch-title" onCancel={event => { event.preventDefault(); onClose() }}>
    <div className="vessel-switch-heading"><div><span className="vessel-switch-eyebrow">{en ? 'WORKSPACE CONTEXT' : 'ÇALIŞMA BAĞLAMI'}</span><h2 id="vessel-switch-title">{title}</h2></div><Button variant="ghost" size="icon" aria-label={en ? 'Close vessel chooser' : 'Araç seçiciyi kapat'} onClick={onClose}><X aria-hidden="true" /></Button></div>
    <p id="vessel-switch-help">{en ? 'Choose a vessel available in your current workspace. Unsaved changes are checked before you leave.' : 'Geçerli çalışma alanınızda erişebildiğiniz aracı seçin. Ayrılmadan önce kaydedilmemiş değişiklikler kontrol edilir.'}</p>
    <form aria-label={en ? 'Find vessel' : 'Araç bul'} onSubmit={event => { event.preventDefault(); if (online && visible) { setSearch(query.trim()); setOffset(0); retry(n => n + 1) } }}>
      <label htmlFor="vessel-switch-query">{en ? 'Vessel name or code' : 'Araç adı veya kodu'}</label>
      <div className="vessel-switch-query"><input autoFocus id="vessel-switch-query" type="search" maxLength={100} value={query} aria-describedby="vessel-switch-help" onChange={event => setQuery(event.target.value)} /><Button type="submit" variant="outline" disabled={!online || !visible}><Search aria-hidden="true" />{en ? 'Search vessels' : 'Araç ara'}</Button></div>
    </form>
    {!online ? <p role="status" className="vessel-switch-state"><WifiOff aria-hidden="true" />{en ? 'Offline. Reconnect to choose a vessel.' : 'Çevrimdışı. Araç seçmek için yeniden bağlanın.'}</p> : !visible ? <p role="status">{en ? 'Return to this window to reload current vessel access.' : 'Güncel araç erişimini yüklemek için bu pencereye dönün.'}</p> : current?.failed ? <div role="alert"><p>{en ? 'Vessels could not be loaded. Current context is unchanged.' : 'Araçlar yüklenemedi. Geçerli bağlam değişmedi.'}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{en ? 'Retry vessels' : 'Araçları tekrar yükle'}</Button></div> : !current?.data ? <p role="status">{en ? 'Loading vessels…' : 'Araçlar yükleniyor…'}</p> : <>
      {choosing?.state === 'denied' && <p role="alert">{en ? 'Vessel access could not be confirmed. Choose another vessel or retry.' : 'Araç erişimi doğrulanamadı. Başka araç seçin veya tekrar deneyin.'}</p>}
      {choosing?.state === 'checking' && <p role="status">{en ? 'Checking vessel access…' : 'Araç erişimi kontrol ediliyor…'}</p>}
      {!current.data.items.length && <p role="status">{en ? 'No vessels match this search in your scope.' : 'Yetki alanınızda bu aramaya uygun araç yok.'}</p>}
      <ul className="vessel-switch-results">{current.data.items.map(row => { const Icon = row.kind === 'Tug' ? Anchor : Ship; const selected = row.id.toLowerCase() === vessel?.toLowerCase(); return <li key={row.id}><button type="button" disabled={choosing?.state === 'checking' || selected} onClick={() => void choose(row.id)} aria-label={`${row.code} · ${row.name} · ${row.kind === 'Tug' ? (en ? 'Tug' : 'Römorkör') : (en ? 'Ship' : 'Gemi')}`}><Icon aria-hidden="true" /><span><strong>{row.code} · {row.name}</strong><small>{row.kind === 'Tug' ? (en ? 'Tug' : 'Römorkör') : (en ? 'Ship' : 'Gemi')}{selected && ` · ${en ? 'Current context' : 'Geçerli bağlam'}`}</small></span><ArrowUpRight aria-hidden="true" /></button></li> })}</ul>
      <div className="vessel-switch-pagination"><Button variant="outline" disabled={!offset || choosing?.state === 'checking'} onClick={() => setOffset(n => Math.max(0, n - 25))}>{en ? 'Previous vessels' : 'Önceki araçlar'}</Button><Button variant="outline" disabled={!current.data.hasMore || offset >= 100000 || choosing?.state === 'checking'} onClick={() => setOffset(n => n + 25)}>{en ? 'Next vessels' : 'Sonraki araçlar'}</Button></div>
    </>}
  </dialog>
}
