import { localizedLabel, sourceText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId, useState } from 'react';
import { ChevronDown, type LucideIcon } from 'lucide-react';
import { Button } from '../components/ui/button';
export type SidebarDestination = {
    key: string;
    href: string;
    label: string;
    icon: LucideIcon;
};
const sections = [
    { key: 'overview', tr: sourceText("tr", "pairs.m_5f50c36af429c54d"), en: sourceText("en", "pairs.m_5f50c36af429c54d"), keys: ['today', 'fleet'] },
    { key: 'operations', tr: sourceText("tr", "pairs.m_d045227afb1d4aaa"), en: sourceText("en", "pairs.m_d045227afb1d4aaa"), keys: ['noon-report', 'deck-daily-log', 'engine-daily-log', 'fuel-bunker-log', 'port-activity-sof', 'operational-downtime'] },
    { key: 'technical', tr: sourceText("tr", "pairs.m_470cefc094dedfe3"), en: sourceText("en", "pairs.m_470cefc094dedfe3"), keys: ['equipment', 'maintenance', 'defects', 'work-order-center'] },
    { key: 'crew', tr: sourceText("tr", "pairs.m_a6f7eeadcb7b0ca5"), en: sourceText("en", "pairs.m_a6f7eeadcb7b0ca5"), keys: ['crew'] },
    { key: 'tug', tr: sourceText("tr", "pairs.m_8d10fb1a220cd756"), en: sourceText("en", "pairs.m_8d10fb1a220cd756"), keys: ['tug-command', 'tug-job-board', 'tug-job-intake'] },
    { key: 'assurance', tr: sourceText("tr", "pairs.m_dda1fc9a101a6728"), en: sourceText("en", "pairs.m_dda1fc9a101a6728"), keys: ['certificate-command-center', 'renewal-workflow', 'sources', 'requirements', 'audit', 'import'] },
    { key: 'workspace', tr: sourceText("tr", "pairs.m_c3e5a55de61effb3"), en: sourceText("en", "pairs.m_c3e5a55de61effb3"), keys: ['companies', 'system-health', 'notification-escalation'] },
];
// Presentation only. Items come from the existing current server/lens
// intersection; opening a category creates neither a link nor a permission.
export function EnterpriseSidebarNav({ items, currentKey, language, onNavigate }: {
    items: SidebarDestination[];
    currentKey?: string;
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const id = useId(), en = language === 'en';
    const groups = sections.map(section => ({ ...section, items: items.filter(item => section.keys.includes(item.key)) })).filter(group => group.items.length);
    const activeGroup = groups.find(group => group.items.some(item => item.key === currentKey))?.key ?? groups[0]?.key;
    const [selection, setSelection] = useState<{
        route?: string;
        group: string | null;
    } | null>(null);
    const expanded = selection && selection.route === currentKey && (selection.group === null || groups.some(group => group.key === selection.group)) ? selection.group : activeGroup;
    return <div className="enterprise-sidebar-nav">{groups.map(group => {
            const open = expanded === group.key, current = group.items.some(item => item.key === currentKey), Icon = group.items[0].icon;
            const contentId = `${id}-${group.key}`, label = localizedLabel(group, en);
            return <section className="enterprise-nav-group" key={group.key}>
      <Button variant="ghost" className="enterprise-nav-group-trigger" title={label} aria-expanded={open} aria-controls={contentId} data-current={current || undefined} onClick={() => setSelection({ route: currentKey, group: open ? null : group.key })}>
        <Icon aria-hidden="true"/><span>{label}</span><ChevronDown aria-hidden="true" className="enterprise-nav-chevron"/>
      </Button>
      <div id={contentId} className="enterprise-nav-group-content" hidden={!open}>
        {group.items.map(item => {
                    const ItemIcon = item.icon;
                    return <Button key={item.key} variant="ghost" className="enterprise-nav-item" title={item.label} data-navigation-key={item.key} aria-current={item.key === currentKey ? 'page' : undefined} onClick={() => onNavigate(item.href)}>
          <ItemIcon aria-hidden="true"/><span>{item.label}</span>{item.key === currentKey && <span className="enterprise-nav-indicator" aria-hidden="true"/>}
        </Button>;
                })}
      </div>
    </section>;
        })}</div>;
}
