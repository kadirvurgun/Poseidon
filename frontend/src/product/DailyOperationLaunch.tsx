import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { EnterpriseActions } from './EnterprisePatterns';
import type { DailyCapabilities, DailyRecord } from './daily-operations-api';
// Launch metadata contains only an existing resource/revision. Private values,
// session identifiers and command keys never enter the destination URL.
export function DailyOperationLaunch({ record, destinations, disabled, language, onNavigate }: {
    record: DailyRecord;
    destinations: DailyCapabilities['destinations'];
    disabled: boolean;
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    const routes = destinations.filter(item => item.key === 'fuel-bunker-log' && (record.kind === 'Noon' || record.kind === 'Engine') || item.key === 'port-activity-sof' && record.kind === 'Noon' || item.key === 'operational-downtime' && record.kind === 'Engine');
    if (!routes.length)
        return null;
    return <section aria-label={localizedText(en, "pairs.m_30c1b54476113265")}><h3>{localizedText(en, "pairs.m_30c1b54476113265")}</h3>
    <p>{localizedText(en, "pairs.m_8a9dc26eadc4c908")}</p>
    {disabled && <p>{localizedText(en, "pairs.m_6b77a4d1296b4295")}</p>}
    <EnterpriseActions align="start">{routes.map(item => <Button key={item.key} variant="outline" disabled={disabled} onClick={() => onNavigate(`${item.href}?${new URLSearchParams({ originKind: record.kind, originId: record.id, originRevision: String(record.revision) })}`)}>{item.key === 'fuel-bunker-log' ? (localizedText(en, "pairs.m_d5294b3ceb5f547c")) : item.key === 'operational-downtime' ? (localizedText(en, "pairs.m_9877942c87d8ab41")) : (localizedText(en, "pairs.m_b158f129fdd8a989"))}</Button>)}</EnterpriseActions>
  </section>;
}
