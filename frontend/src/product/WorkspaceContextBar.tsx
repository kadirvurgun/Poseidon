import { useEffect, useState } from 'react'
import { ApiError } from './api'
import type { Session } from './directory-contract'
import { loadWorkspaceContext } from './workspace-context-api'
import type { WorkspaceContext } from './workspace-context-api'
import './workspace-context-bar.css'

type Props = { session: Session | null; vessel?: string | null; language: 'tr' | 'en' }
export function WorkspaceContextBar({ session, vessel = null, language }: Props) {
  const tenant = session?.tenantId, actor = session?.actorId
  const allowed = session?.authenticated === true && session.canRead === true && !!tenant && !!actor
  const scope = `${tenant}:${actor}:${vessel ?? ''}`
  const [attempt, retry] = useState(0)
  const [result, setResult] = useState<{ scope: string; data?: WorkspaceContext; error?: number } | null>(null)
  const [online, setOnline] = useState(navigator.onLine)
  const en = language === 'en'
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update); window.addEventListener('offline', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update) }
  }, [])
  useEffect(() => {
    if (!allowed || !tenant || !actor) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    loadWorkspaceContext(tenant, actor, vessel, c.signal).then(data => { if (active) setResult({ scope, data }) })
      .catch(error => { if (active) setResult({ scope, error: error instanceof ApiError ? error.status : 0 }) })
    return () => { active = false; clearTimeout(timer); c.abort() }
  }, [allowed, tenant, actor, scope, vessel, attempt])
  if (!allowed) return null
  const current = result?.scope === scope ? result : null
  return <section className="workspace-global-context" aria-label={en ? 'Current workspace context' : 'Geçerli çalışma bağlamı'}>
    <span className="context-workspace">{current?.data && session?.tenantName || (en ? 'Current workspace' : 'Geçerli çalışma alanı')}</span>
    {!current ? <span role="status">{en ? 'Loading context…' : 'Bağlam yükleniyor…'}</span> : !current.data ? <>
      <span role="status">{current.error === 401 || current.error === 403 || current.error === 404 ? (en ? 'Context access unavailable.' : 'Bağlam erişimi kullanılamıyor.') : (en ? 'Context could not be loaded.' : 'Bağlam yüklenemedi.')}</span>
      <button type="button" onClick={() => { setResult(null); retry(n => n + 1) }}>{en ? 'Retry context' : 'Bağlamı tekrar yükle'}</button>
    </> : <>
      <span>{current.data.vessel ? `${current.data.vessel.code} · ${current.data.vessel.name} · ${current.data.vessel.kind === 'Ship' ? (en ? 'Ship' : 'Gemi') : (en ? 'Tug' : 'Römorkör')}` : (en ? 'No vessel selected' : 'Araç seçilmedi')}</span>
      <span>{en ? 'Role context' : 'Rol bağlamı'}: {current.data.roleContext.state === 'Configured'
        ? current.data.roleContext.roles.map(role => en ? role.en : role.tr).join(' · ')
        : current.data.roleContext.state === 'Missing' ? (en ? 'Not assigned' : 'Atanmamış') : (en ? 'Unavailable' : 'Kullanılamıyor')}</span>
    </>}
    <span className="context-connection">{online ? (en ? 'Online' : 'Çevrimiçi') : (en ? 'Offline · context may be stale' : 'Çevrimdışı · bağlam eski olabilir')}</span>
  </section>
}
