import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Anchor, ArrowLeft, ArrowUpRight, Building2, ChevronRight, ClipboardList, FileCheck2, FolderCheck, Gauge, LayoutDashboard, Menu, Search, ShieldCheck, Ship, Users, Wrench, Upload, Waves, X } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { Button } from '../components/ui/button'
import type { Session } from './directory-contract'
import { WorkspaceContextBar } from './WorkspaceContextBar'
import { WorkspaceRoleNavigation } from './WorkspaceRoleNavigation'
import type { RoleLensProjection } from './WorkspaceRoleNavigation'
import { WorkspaceSearch } from './WorkspaceSearch'
import { useEnterpriseNavigation } from './enterprise-navigation'
import { enterpriseFocusTargets } from './enterprise-focus'
import './enterprise.css'

const labels: Record<string, { tr: string; en: string; icon: LucideIcon; group: string }> = {
  today: { tr: 'Bugün', en: 'Today', icon: LayoutDashboard, group: 'operations' },
  fleet: { tr: 'Filo', en: 'Fleet', icon: Ship, group: 'operations' },
  'tug-command': { tr: 'Römorkör merkezi', en: 'Tug command', icon: Anchor, group: 'operations' },
  'tug-job-board': { tr: 'İş panosu', en: 'Job board', icon: ClipboardList, group: 'operations' },
  'tug-job-intake': { tr: 'İş talebi', en: 'Job intake', icon: FileCheck2, group: 'operations' },
  sources: { tr: 'Kaynaklar', en: 'Sources', icon: FolderCheck, group: 'assurance' },
  requirements: { tr: 'Gereksinimler', en: 'Requirements', icon: ShieldCheck, group: 'assurance' },
  audit: { tr: 'İşlem geçmişi', en: 'Action history', icon: Gauge, group: 'assurance' },
  companies: { tr: 'Şirketler', en: 'Companies', icon: Building2, group: 'workspace' },
  crew: { tr: 'Mürettebat', en: 'Crew', icon: Users, group: 'operations' },
  equipment: { tr: 'Ekipman', en: 'Equipment', icon: Wrench, group: 'operations' },
  maintenance: { tr: 'Bakım planları', en: 'Maintenance plans', icon: ClipboardList, group: 'operations' },
  import: { tr: 'Kontrollü içe aktarma', en: 'Controlled import', icon: Upload, group: 'assurance' }
}
type Props = { session: Session | null; vessel?: string | null; language: 'tr' | 'en'; onLanguageChange: (value: 'tr' | 'en') => void;
  onNavigate: (href: string, committed?: () => void) => void; title: string; mainId: string; drawer?: boolean; onDrawerChange?: (open: boolean) => void; parentManagesFocus?: boolean }

export function EnterpriseChrome({ session, vessel = null, language, onLanguageChange, onNavigate, title, mainId,
  drawer: controlledDrawer, onDrawerChange, parentManagesFocus = false }: Props) {
  const en = language === 'en'; const navigation = useEnterpriseNavigation(session)
  const [localDrawer, setLocalDrawer] = useState(false)
  const [searchOwner, setSearchOwner] = useState<{ owner: Session } | null>(null)
  const [roleLens, setRoleLens] = useState<RoleLensProjection | null>(null)
  const projectLens = useCallback((value: RoleLensProjection | null) => setRoleLens(value), [])
  const currentLens = roleLens?.owner === session && roleLens.vessel === vessel && roleLens.epoch === navigation.epoch ? roleLens : null
  const menuItems = currentLens ? currentLens.items : navigation.destinations
  const menuButton = useRef<HTMLButtonElement>(null)
  const drawer = controlledDrawer ?? localDrawer
  const setDrawer = (open: boolean) => { setLocalDrawer(open); onDrawerChange?.(open) }
  const searchOpen = searchOwner !== null && searchOwner.owner === session && !!session?.authenticated && session.canRead
  const move = (href: string, committed?: () => void) => { setDrawer(false); onNavigate(href, () => { setSearchOwner(null); committed?.() }) }
  useEffect(() => {
    if (!drawer || parentManagesFocus) return
    const trigger = menuButton.current
    const nav = document.getElementById('workspace-navigation'); const main = document.getElementById(mainId)
    if (main) main.inert = true
    const targets = () => enterpriseFocusTargets(nav)
    targets()[0]?.focus()
    const key = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.preventDefault(); setLocalDrawer(false); onDrawerChange?.(false) }
      if (event.key === 'Tab') {
        const items = targets(); const first = items[0]; const last = items.at(-1)
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
      }
    }
    document.addEventListener('keydown', key)
    return () => { if (main) main.inert = false; document.removeEventListener('keydown', key); trigger?.focus() }
  }, [drawer, mainId, onDrawerChange, parentManagesFocus])
  useEffect(() => {
    const key = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k' && navigation.canSearch && session) {
        event.preventDefault(); setLocalDrawer(false); onDrawerChange?.(false); setSearchOwner({ owner: session })
      }
    }
    window.addEventListener('keydown', key); return () => window.removeEventListener('keydown', key)
  }, [navigation.canSearch, navigation.epoch, session, onDrawerChange])
  const path = window.location.pathname
  const currentKey = path === '/app' || path === '/app/' ? (session?.canReadCompanies ? 'companies' : 'fleet') : path.startsWith('/app/tug-job-detail') ? 'tug-job-board' : menuItems
    .find(item => { const target = item.href.split('?')[0]; return path === target || path.startsWith(target + '/') })?.key
  return <>
    <a className="skip-link" href={`#${mainId}`}>{en ? 'Skip to main content' : 'Ana içeriğe geç'}</a>
    <header className="product-header enterprise-topbar" inert={drawer}>
      <Button ref={menuButton} variant="ghost" size="icon" className="workspace-menu" aria-expanded={drawer} aria-controls="workspace-navigation" aria-label={en ? 'Menu' : 'Menü'} onClick={() => setDrawer(!drawer)}><Menu /></Button>
      <div className="enterprise-breadcrumb">{vessel && <Button variant="ghost" size="icon-sm" asChild><a href={`/app/fleet/${vessel}`} aria-label={en ? 'Back to vessel' : 'Araca dön'} onClick={event => { event.preventDefault(); move(`/app/fleet/${vessel}`) }}><ArrowLeft aria-hidden="true" /></a></Button>}<span>POSEIDON</span><ChevronRight aria-hidden="true" /><strong>{title}</strong></div>
      <div className="enterprise-topbar-actions">
        {navigation.canSearch && <Button variant="outline" className="enterprise-search-trigger" aria-label={en ? 'Search workspace' : 'Çalışma alanında ara'} onClick={() => { if (session) setSearchOwner({ owner: session }) }}><Search /><span>{en ? 'Search workspace' : 'Çalışma alanında ara'}</span><kbd>⌘ / Ctrl K</kbd></Button>}
        <div className="languages" role="group" aria-label={en ? 'Language' : 'Dil'}><button aria-pressed={!en} onClick={() => onLanguageChange('tr')}>TR</button><button aria-pressed={en} onClick={() => onLanguageChange('en')}>EN</button></div>
      </div>
      <WorkspaceContextBar session={session} vessel={vessel} language={language} onNavigate={move} />
    </header>
    {drawer && <button type="button" className="enterprise-nav-overlay" aria-label={en ? 'Close menu' : 'Menüyü kapat'} onClick={() => setDrawer(false)} />}
    <nav id="workspace-navigation" className={`product-nav enterprise-sidebar ${drawer ? 'is-open' : ''}`} aria-label="POSEIDON" role={drawer ? 'dialog' : undefined} aria-modal={drawer ? true : undefined}>
      <div className="enterprise-brand-row"><a className="enterprise-brand" href="/app" aria-label="POSEIDON" onClick={event => { event.preventDefault(); move('/app') }}><span className="enterprise-brand-mark"><Waves aria-hidden="true" /></span><span>POSEIDON<small>MARITIME OPERATIONS</small></span></a>{drawer && <Button variant="ghost" size="icon" aria-label={en ? 'Close' : 'Kapat'} onClick={() => setDrawer(false)}><X /></Button>}</div>
      <div className="enterprise-nav-scroll">
        <details className="enterprise-role-settings" open><summary>{en ? 'Work view' : 'Çalışma görünümü'}</summary><WorkspaceRoleNavigation session={session} vessel={vessel} language={language} onNavigate={move} embedded primary scopeEpoch={navigation.epoch} onProjection={projectLens} /></details>
        {(['operations', 'assurance', 'workspace'] as const).map(group => {
          const items = menuItems.filter(item => labels[item.key]?.group === group)
          return items.length > 0 && <div className="enterprise-nav-group" key={group}><p>{group === 'operations' ? (en ? 'OPERATIONS' : 'OPERASYON') : group === 'assurance' ? (en ? 'ASSURANCE' : 'GÜVENCE') : (en ? 'WORKSPACE' : 'ÇALIŞMA ALANI')}</p>
            {items.map(item => { const label = labels[item.key]; const Icon = label.icon
              return <Button variant="ghost" className="enterprise-nav-item" key={item.key} aria-current={item.key === currentKey ? 'page' : undefined} onClick={() => move(item.href)}><Icon aria-hidden="true" /><span>{en ? label.en : label.tr}</span>{item.key === currentKey && <span className="enterprise-nav-indicator" />}</Button>
            })}</div>
        })}
        {navigation.loading && <div className="enterprise-nav-loading" role="status">{en ? 'Loading navigation…' : 'Menü yükleniyor…'}</div>}
        {navigation.failed && <div className="enterprise-nav-loading" role="status"><p>{en ? 'Navigation unavailable' : 'Menü kullanılamıyor'}</p><Button variant="ghost" onClick={navigation.retry}>{en ? 'Retry navigation' : 'Menüyü tekrar yükle'}</Button></div>}
        {currentLens && menuItems.length === 0 && <p className="enterprise-nav-loading" role="status">{en ? 'No available work in this lens and context.' : 'Bu mercek ve bağlamda kullanılabilir iş yok.'}</p>}
      </div>
      <div className="enterprise-sidebar-footer"><span className="enterprise-workspace-avatar">{session?.tenantName?.slice(0, 2).toLocaleUpperCase(language) ?? 'PO'}</span><div><strong>{session?.tenantName ?? (en ? 'Maritime workspace' : 'Denizcilik çalışma alanı')}</strong><span>{session?.authenticated ? (en ? 'Authorized workspace' : 'Yetkili çalışma alanı') : (en ? 'Secure workspace sign-in' : 'Güvenli çalışma alanı girişi')}</span></div><ArrowUpRight aria-hidden="true" /></div>
    </nav>
    {searchOpen && createPortal(<WorkspaceSearch language={language} authorization={navigation.loading ? 'pending' : navigation.failed ? 'unavailable' : navigation.canSearch ? 'allowed' : 'denied'} grant={navigation.searchGrant} onRetryAuthorization={navigation.retry} onClose={() => setSearchOwner(null)} onNavigate={href => move(href, () => setSearchOwner(null))} />, document.querySelector('.product-shell') ?? document.body)}
  </>
}
