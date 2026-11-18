import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { SourceDraftLaunch } from './SourceDraftLaunch';
import { workOrderApi } from './work-order-api';
import type { Session } from './directory-contract';
export function WorkOrderPlanLaunch({ session, vessel, planId, revision, language, disabled, onNavigate }: {
    session: Session;
    vessel: string;
    planId: string;
    revision: number;
    language: AppLocale;
    disabled: boolean;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    return <SourceDraftLaunch session={session} vessel={vessel} sourceId={planId} revision={revision} href={`/app/work-order-center/${vessel}?${new URLSearchParams({ planId, planRevision: String(revision) })}`} disabled={disabled} loadCapability={workOrderApi.capabilities} onNavigate={onNavigate} title={localizedText(en, "pairs.m_4e8c3a55c3f2730a")} description={localizedText(en, "pairs.m_761af9b4784a9679")} action={localizedText(en, "pairs.m_0ecc2a399cb4d49d")}/>;
}
