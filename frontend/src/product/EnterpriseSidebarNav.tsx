import { localizedLabel, localizedText, sourceText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId, useRef, useState } from 'react';
import { ChevronDown, Search, X, type LucideIcon } from 'lucide-react';
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
    { key: 'tug', tr: sourceText("tr", "pairs.m_8d10fb1a220cd756"), en: sourceText("en", "pairs.m_8d10fb1a220cd756"), keys: ['tug-command', 'tug-job-board', 'tug-job-intake', 'tug-assignment', 'tug-readiness', 'tug-equipment-readiness', 'tug-crew-readiness'] },
    { key: 'assurance', tr: sourceText("tr", "pairs.m_dda1fc9a101a6728"), en: sourceText("en", "pairs.m_dda1fc9a101a6728"), keys: ['risk-assessment', 'risk-template-library', 'certificate-command-center', 'renewal-workflow', 'pre-survey-checklist', 'survey-planner', 'sources', 'requirements', 'audit', 'audit-ready', 'import'] },
    { key: 'workspace', tr: sourceText("tr", "pairs.m_c3e5a55de61effb3"), en: sourceText("en", "pairs.m_c3e5a55de61effb3"), keys: ['companies', 'system-health', 'notification-escalation', 'offline-conflict-center'] },
];
// DEV-022 / 65.03 paragraph 51: the menu filter only ever sees the authorized items the
// server already projected; it never requests, counts or hints at hidden entries.
// Locale-aware folding (65.03 paragraph 226): Turkish I/İ/ı/i and diacritics match
// for display search only; keys and routes are never case-folded.
export function foldMenuText(value: string, language: AppLocale) {
    let lowered: string;
    try { lowered = value.toLocaleLowerCase(language); }
    catch { lowered = value.toLowerCase(); }
    return lowered.normalize('NFD').replace(/\p{M}/gu, '').replace(/ı/g, 'i').trim();
}
// Presentation only. Items come from the existing current server/lens
// intersection; opening a category creates neither a link nor a permission.
export function EnterpriseSidebarNav({ items, currentKey, language, onNavigate }: {
    items: SidebarDestination[];
    currentKey?: string;
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const id = useId(), en = language === 'en';
    const [filter, setFilter] = useState('');
    const results = useRef<HTMLDivElement>(null);
    const input = useRef<HTMLInputElement>(null);
    const needle = foldMenuText(filter, language);
    const visible = needle ? items.filter(item => foldMenuText(item.label, language).includes(needle)) : items;
    // A heading is shown only when it has a visible authorized child (54.02 effectiveMenu).
    const groups = sections.map(section => ({ ...section, items: visible.filter(item => section.keys.includes(item.key)) })).filter(group => group.items.length);
    const activeGroup = groups.find(group => group.items.some(item => item.key === currentKey))?.key ?? groups[0]?.key;
    const [selection, setSelection] = useState<{
        route?: string;
        group: string | null;
    } | null>(null);
    const expanded = selection && selection.route === currentKey && (selection.group === null || groups.some(group => group.key === selection.group)) ? selection.group : activeGroup;
    const clear = () => { setFilter(''); input.current?.focus(); };
    return <div className="enterprise-sidebar-nav">
    {items.length > 0 && <div className="enterprise-nav-filter" role="search">
      <label htmlFor={`${id}-filter`}>{localizedText(language, "pairs.m_7c2c3c0f372d028d")}</label>
      <div className="enterprise-nav-filter-field">
        <Search aria-hidden="true"/>
        <input ref={input} id={`${id}-filter`} type="search" autoComplete="off" spellCheck={false} maxLength={60} value={filter} aria-describedby={`${id}-filter-hint`}
          aria-controls={`${id}-results`} onChange={event => setFilter(event.target.value)} onKeyDown={event => {
                if (event.key === 'Escape' && filter) {
                    // Clear first; a second Escape still closes the drawer.
                    event.preventDefault();
                    event.stopPropagation();
                    setFilter('');
                }
                else if (event.key === 'ArrowDown' && needle) {
                    const first = results.current?.querySelector<HTMLButtonElement>('.enterprise-nav-item');
                    if (first) { event.preventDefault(); first.focus(); }
                }
            }}/>
        {filter && <Button type="button" variant="ghost" size="icon-sm" aria-label={localizedText(language, "pairs.m_5af49f5d0c78416e")} onClick={clear}><X aria-hidden="true"/></Button>}
      </div>
      <p id={`${id}-filter-hint`} className="enterprise-nav-filter-hint">{localizedText(language, "pairs.m_4895cdb4b14ff26c")}</p>
    </div>}
    <div id={`${id}-results`} ref={results} className="enterprise-sidebar-nav-results" onKeyDown={event => {
            // Arrow keys move between visible results; ArrowUp on the first returns to the filter.
            // Tab order is unchanged (every item stays a regular tab stop).
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
            const target = event.target as HTMLElement;
            if (!target.classList.contains('enterprise-nav-item')) return;
            const list = [...(results.current?.querySelectorAll<HTMLButtonElement>('.enterprise-nav-item') ?? [])].filter(item => !item.closest('[hidden]'));
            const index = list.indexOf(target as HTMLButtonElement);
            if (index < 0) return;
            const next = event.key === 'ArrowDown' ? list[index + 1] : index === 0 ? input.current : list[index - 1];
            if (next) { event.preventDefault(); next.focus(); }
        }}>
    {needle && groups.length === 0 && <p className="enterprise-nav-loading" role="status">{localizedText(language, "pairs.m_9e601b252c4fb4b3")}</p>}
    {groups.map(group => {
            // While filtering every matching group is open so each match is reachable by keyboard.
            const open = needle ? true : expanded === group.key, current = group.items.some(item => item.key === currentKey), Icon = group.items[0].icon;
            const contentId = `${id}-${group.key}`, label = localizedLabel(group, en);
            return <section className="enterprise-nav-group" key={group.key}>
      <Button variant="ghost" className="enterprise-nav-group-trigger" title={label} aria-expanded={open} aria-controls={contentId} data-current={current || undefined} disabled={!!needle} onClick={() => setSelection({ route: currentKey, group: open ? null : group.key })}>
        <Icon aria-hidden="true"/><span>{label}</span><ChevronDown aria-hidden="true" className="enterprise-nav-chevron"/>
      </Button>
      <div id={contentId} className="enterprise-nav-group-content" hidden={!open}>
        {group.items.map(item => {
                    const ItemIcon = item.icon;
                    return <Button key={item.key} variant="ghost" className="enterprise-nav-item" title={item.label} data-navigation-key={item.key} aria-current={item.key === currentKey ? 'page' : undefined} onClick={() => { setFilter(''); onNavigate(item.href); }}>
          <ItemIcon aria-hidden="true"/><span>{item.label}</span>{item.key === currentKey && <span className="enterprise-nav-indicator" aria-hidden="true"/>}
        </Button>;
                })}
      </div>
    </section>;
        })}
    </div>
  </div>;
}
