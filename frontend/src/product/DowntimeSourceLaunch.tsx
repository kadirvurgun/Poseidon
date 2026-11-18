import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { SourceDraftLaunch } from './SourceDraftLaunch';
import { downtimeApi } from './downtime-api';
import type { Session } from './directory-contract';
type SourceKind = 'Defect' | 'WorkOrder';
const grants = Object.fromEntries((['Defect', 'WorkOrder'] as const).map(kind => [kind, async (vessel: string, signal: AbortSignal) => {
        const caps = await downtimeApi.capabilities(vessel, signal);
        return { canWrite: caps.canWrite && caps.originKinds.includes(kind) };
    }])) as Record<SourceKind, (vessel: string, signal: AbortSignal) => Promise<{
    canWrite: boolean;
}>>;
// Source read and target write are separate grants. Only an existing saved
// revision pointer is transferred; cause, time, claim and closure are not copied.
export function DowntimeSourceLaunch({ session, vessel, kind, sourceId, revision, language, disabled, onNavigate }: {
    session: Session;
    vessel: string;
    kind: SourceKind;
    sourceId: string;
    revision: number;
    language: AppLocale;
    disabled: boolean;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    return <SourceDraftLaunch session={session} vessel={vessel} sourceId={sourceId} revision={revision} href={`/app/operational-downtime/${vessel}?${new URLSearchParams({ originKind: kind, originId: sourceId, originRevision: String(revision) })}`} disabled={disabled} loadCapability={grants[kind]} onNavigate={onNavigate} title={localizedText(en, "pairs.m_1bf0e0879cc4716f")} description={localizedText(en, "pairs.m_d85ce4e74530389d")} action={localizedText(en, "pairs.m_9c5f445ea1abb1c8")}/>;
}
