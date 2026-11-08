import { useId, useState } from 'react'
import { ChevronDown, type LucideIcon } from 'lucide-react'
import { Button } from '../components/ui/button'

export type SidebarDestination = { key: string; href: string; label: string; icon: LucideIcon }
const sections = [
  { key: 'overview', tr: 'Genel bakış', en: 'Overview', keys: ['today', 'fleet'] },
  { key: 'operations', tr: 'Gemi operasyonları', en: 'Ship operations', keys: ['noon-report', 'deck-daily-log', 'engine-daily-log', 'fuel-bunker-log', 'port-activity-sof'] },
  { key: 'technical', tr: 'Teknik ve bakım', en: 'Technical and maintenance', keys: ['equipment', 'maintenance', 'defects', 'work-order-center'] },
  { key: 'crew', tr: 'Mürettebat', en: 'Crew management', keys: ['crew'] },
  { key: 'tug', tr: 'Römorkör operasyonları', en: 'Tug operations', keys: ['tug-command', 'tug-job-board', 'tug-job-intake'] },
  { key: 'assurance', tr: 'Uyum ve güvence', en: 'Compliance and assurance', keys: ['certificate-command-center', 'renewal-workflow', 'sources', 'requirements', 'audit', 'import'] },
  { key: 'workspace', tr: 'Çalışma alanı', en: 'Workspace', keys: ['companies', 'system-health', 'notification-escalation'] },
]

// Presentation only. Items come from the existing current server/lens
// intersection; opening a category creates neither a link nor a permission.
export function EnterpriseSidebarNav({ items, currentKey, language, onNavigate }: {
  items: SidebarDestination[]; currentKey?: string; language: 'tr' | 'en'; onNavigate: (href: string) => void
}) {
  const id = useId(), en = language === 'en'
  const groups = sections.map(section => ({ ...section, items: items.filter(item => section.keys.includes(item.key)) })).filter(group => group.items.length)
  const activeGroup = groups.find(group => group.items.some(item => item.key === currentKey))?.key ?? groups[0]?.key
  const [selection, setSelection] = useState<{ route?: string; group: string | null } | null>(null)
  const expanded = selection && selection.route === currentKey && (selection.group === null || groups.some(group => group.key === selection.group)) ? selection.group : activeGroup
  return <div className="enterprise-sidebar-nav">{groups.map(group => {
    const open = expanded === group.key, current = group.items.some(item => item.key === currentKey), Icon = group.items[0].icon
    const contentId = `${id}-${group.key}`, label = en ? group.en : group.tr
    return <section className="enterprise-nav-group" key={group.key}>
      <Button variant="ghost" className="enterprise-nav-group-trigger" title={label} aria-expanded={open} aria-controls={contentId} data-current={current || undefined}
        onClick={() => setSelection({ route: currentKey, group: open ? null : group.key })}>
        <Icon aria-hidden="true" /><span>{label}</span><ChevronDown aria-hidden="true" className="enterprise-nav-chevron" />
      </Button>
      <div id={contentId} className="enterprise-nav-group-content" hidden={!open}>
        {group.items.map(item => { const ItemIcon = item.icon; return <Button key={item.key} variant="ghost" className="enterprise-nav-item" title={item.label}
          data-navigation-key={item.key} aria-current={item.key === currentKey ? 'page' : undefined} onClick={() => onNavigate(item.href)}>
          <ItemIcon aria-hidden="true" /><span>{item.label}</span>{item.key === currentKey && <span className="enterprise-nav-indicator" aria-hidden="true" />}
        </Button> })}
      </div>
    </section>
  })}</div>
}
