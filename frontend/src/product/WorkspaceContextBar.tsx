import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { ArrowLeftRight, Wifi, WifiOff } from 'lucide-react'
import { ApiError } from './api'
import type { Session } from './directory-contract'
import { loadWorkspaceContext } from './workspace-context-api'
import type { WorkspaceContext } from './workspace-context-api'
import './workspace-context-bar.css'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import { WorkspaceVesselSwitcher } from './WorkspaceVesselSwitcher'

type Props = { session: Session | null; vessel?: string | null; language: 'tr' | 'en'; onNavigate?: (href: string, committed?: () => void) => void }
export function WorkspaceContextBar(props: Props) {
  const identity = props.session
  if (!identity?.authenticated || !identity.canRead || !identity.tenantId || !identity.actorId) return null
  // Logout destroys the old cache, even if a later login uses the same IDs.
  return <ScopedWorkspaceContextBar key={`${identity.tenantId}:${identity.actorId}:${props.vessel ?? ''}`} {...props} />
}
function ScopedWorkspaceContextBar({ session, vessel = null, language, onNavigate }: Props) {
  const epoch = useWorkspaceRevalidation()
  const tenant = session?.tenantId, actor = session?.actorId
  const allowed = session?.authenticated === true && session.canRead === true && !!tenant && !!actor
  const scope = `${tenant}:${actor}:${vessel ?? ''}:${epoch}`
  const [attempt, retry] = useState(0)
  const [result, setResult] = useState<{ scope: string; owner: Session | null; data?: WorkspaceContext; error?: number } | null>(null)
  const [online, setOnline] = useState(navigator.onLine)
  const [chooser, setChooser] = useState<{ owner: Session; epoch: number } | null>(null)
  const en = language === 'en'
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update); window.addEventListener('offline', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update) }
  }, [])
  useEffect(() => {
    if (!allowed || !tenant || !actor) return
    if (!navigator.onLine || document.visibilityState === 'hidden') return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    loadWorkspaceContext(tenant, actor, vessel, c.signal).then(data => { if (active) setResult({ scope, owner: session, data }) })
      .catch(error => { if (active) setResult({ scope, owner: session, error: error instanceof ApiError ? error.status : 0 }) })
    return () => { active = false; clearTimeout(timer); c.abort() }
  }, [allowed, tenant, actor, scope, vessel, attempt, session])
  if (!allowed) return null
  const current = result?.scope === scope && result.owner === session ? result : null
  return <section className="workspace-global-context" aria-label={en ? 'Current workspace context' : 'Geçerli çalışma bağlamı'}>
    <span className="context-workspace">{current?.data && session?.tenantName || (en ? 'Current workspace' : 'Geçerli çalışma alanı')}</span>
    {!online ? <span role="status">{en ? 'Offline · reconnect to load context.' : 'Çevrimdışı · bağlamı yüklemek için yeniden bağlanın.'}</span> : document.visibilityState === 'hidden' ? <span role="status">{en ? 'Context paused while this window is hidden.' : 'Pencere gizliyken bağlam duraklatıldı.'}</span> : !current ? <span role="status">{en ? 'Loading context…' : 'Bağlam yükleniyor…'}</span> : !current.data ? <>
      <span role="alert">{current.error === 401 || current.error === 403 || current.error === 404 ? (en ? 'Context access unavailable.' : 'Bağlam erişimi kullanılamıyor.') : (en ? 'Context could not be loaded.' : 'Bağlam yüklenemedi.')}</span>
      <button type="button" disabled={!online} onClick={() => { setResult(null); retry(n => n + 1) }}>{en ? 'Retry context' : 'Bağlamı tekrar yükle'}</button>
    </> : <>
      <span>{current.data.vessel ? `${current.data.vessel.code} · ${current.data.vessel.name} · ${current.data.vessel.kind === 'Ship' ? (en ? 'Ship' : 'Gemi') : (en ? 'Tug' : 'Römorkör')}` : (en ? 'No vessel selected' : 'Araç seçilmedi')}</span>
      <span className="context-role">{en ? 'Role context' : 'Rol bağlamı'}: {current.data.roleContext.state === 'Configured'
        ? current.data.roleContext.roles.map(role => en ? role.en : role.tr).join(' · ')
        : current.data.roleContext.state === 'Missing' ? (en ? 'Not assigned' : 'Atanmamış') : (en ? 'Unavailable' : 'Kullanılamıyor')}</span>
    </>}
    {onNavigate && current?.data && online && session && <button type="button" className="context-switch-trigger" onClick={() => setChooser({ owner: session, epoch })}><ArrowLeftRight aria-hidden="true" />{en ? 'Change vessel context' : 'Araç bağlamını değiştir'}</button>}
    <span className="context-connection">{online ? <Wifi aria-hidden="true" /> : <WifiOff aria-hidden="true" />}{online ? (en ? 'Online' : 'Çevrimiçi') : (en ? 'Offline · context may be stale' : 'Çevrimdışı · bağlam eski olabilir')}</span>
    {chooser?.owner === session && chooser.epoch === epoch && session && onNavigate && createPortal(<WorkspaceVesselSwitcher session={session} vessel={vessel} language={language} onClose={() => setChooser(null)} onNavigate={onNavigate} />, document.querySelector('.product-shell') ?? document.body)}
  </section>
}
