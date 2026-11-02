import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import type { Session } from './directory-contract'
import { lensLabels, loadLenses, type Lenses } from './workspace-lens-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import { WorkspaceSearch } from './WorkspaceSearch'
import './workspace-role-navigation.css'

type Props = { session: Session | null; vessel?: string | null; language: 'tr' | 'en'; onNavigate: (href: string, committed?: () => void) => void; onSearchOpen?: () => void }
export function WorkspaceRoleNavigation(props: Props) {
  const s = props.session
  return s?.authenticated && s.canRead && s.tenantId && s.actorId
    ? <RoleNavigation key={`${s.tenantId}:${s.actorId}:${props.vessel ?? ''}`} {...props} /> : null
}
function RoleNavigation({ session, vessel = null, language, onNavigate, onSearchOpen }: Props) {
  const epoch = useWorkspaceRevalidation()
  const [attempt, retry] = useState(0)
  const [result, setResult] = useState<{ owner: Session | null; epoch: number; data: Lenses } | null>(null)
  const [error, setError] = useState(false)
  const [selection, select] = useState<{ owner: Session | null; reference: string } | null>(null)
  const [search, showSearch] = useState<Session | null>(null)
  const en = language === 'en'
  const data = result?.owner === session && result.epoch === epoch ? result.data : null
  const chosen = selection && selection.owner === session ? data?.lenses.find(x => x.reference === selection.reference) : null
  const resetScope1 = [session, vessel, epoch, attempt] as const
  const [previousScope1, setPreviousScope1] = useState(resetScope1)
  // Withdraw stale state in the same render that changes the request scope.
  if (!Object.is(previousScope1[0], session) || !Object.is(previousScope1[1], vessel) || !Object.is(previousScope1[2], epoch) || !Object.is(previousScope1[3], attempt)) {
    setPreviousScope1(resetScope1)
    setResult(null); setError(false)
  }
  useEffect(() => {
    const c = new AbortController(); let active = true

    const timer = setTimeout(() => c.abort(), 10000)
    if (navigator.onLine && document.visibilityState !== 'hidden') {
      loadLenses(session!.tenantId!, session!.actorId!, vessel, c.signal)
        .then(value => { if (active) {
          setResult({ owner: session, epoch, data: value })
          select(old => old && old.owner === session && !value.lenses.some(l => l.reference === old.reference) ? null : old)
          if (!value.canSearch) showSearch(null)
        } })
        .catch(() => { if (active) setError(true) })
    }
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [session, vessel, epoch, attempt])
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if (data?.canSearch && (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); onSearchOpen?.(); showSearch(session) }
    }
    window.addEventListener('keydown', key); return () => window.removeEventListener('keydown', key)
  }, [data, onSearchOpen, session])
  return <section className="workspace-role-navigation" aria-label={en ? 'Role navigation' : 'Rol menüsü'}>
    <p>{en ? 'Menu lens changes presentation only.' : 'Menü merceği yalnızca görünümü değiştirir.'}</p>
    {!data ? <><span role="status">{error ? (en ? 'Navigation unavailable.' : 'Menü kullanılamıyor.') : (en ? 'Navigation needs current online context.' : 'Menü için güncel çevrimiçi bağlam gerekli.')}</span><button type="button" onClick={() => { setResult(null); retry(x => x + 1) }}>{en ? 'Reload menu' : 'Menüyü yeniden yükle'}</button></>
      : <>
        <span>{data.mode === 'Fleet' ? (en ? 'Fleet context' : 'Filo bağlamı') : data.mode === 'Ship' ? (en ? 'Ship' : 'Gemi') : (en ? 'Tug' : 'Römorkör')}</span>
        {data.state === 'Configured' && data.lenses.length > 0 ? <>
          <label>{en ? 'Menu lens' : 'Menü merceği'} <select value={chosen?.reference ?? ''} onChange={e => select({ owner: session, reference: e.target.value })}>
            <option value="">{en ? 'Choose your menu lens' : 'Menü merceğini seçin'}</option>
            {data.lenses.map(l => <option key={l.reference} value={l.reference}>{en ? l.en : l.tr}</option>)}
          </select></label>
          {chosen && <nav aria-label={en ? 'Daily work menu' : 'Günlük iş menüsü'}>{(['daily', 'review'] as const).map(group => {
            const items = chosen.items.filter(i => i.group === group)
            return items.length > 0 && <div key={group}><strong>{group === 'daily' ? (en ? 'Daily work' : 'Günlük iş') : (en ? 'Review' : 'İnceleme')}</strong>{items.map(i => <button key={i.key} type="button" aria-current={window.location.pathname + window.location.search === i.href ? 'page' : undefined} onClick={() => onNavigate(i.href)}>{lensLabels[i.key][en ? 1 : 0]}</button>)}</div>
          })}{chosen.items.length === 0 && <p role="status">{en ? 'No available work in this lens and context.' : 'Bu mercek ve bağlamda kullanılabilir iş yok.'}</p>}</nav>}
        </> : <span role="status">{data.state === 'Missing' ? (en ? 'No assigned role context.' : 'Atanmış rol bağlamı yok.') : data.state === 'Unavailable' ? (en ? 'Role menu configuration unavailable.' : 'Rol menüsü yapılandırması kullanılamıyor.') : (en ? 'No role lens for this mode.' : 'Bu mod için rol merceği yok.')}</span>}
        <button type="button" onClick={() => onNavigate(vessel ? `/app/fleet/${vessel}` : '/app/fleet')}>{en ? 'Vessel context' : 'Araç bağlamı'}</button>
        {data.canSearch && <button type="button" onClick={() => { onSearchOpen?.(); showSearch(session) }}>{en ? 'Search workspace' : 'Çalışma alanında ara'}</button>}
      </>}
    {search && search === session && createPortal(<WorkspaceSearch language={language} onClose={() => showSearch(null)} onNavigate={href => onNavigate(href, () => showSearch(null))} />, document.querySelector('.product-shell') ?? document.body)}
  </section>
}
