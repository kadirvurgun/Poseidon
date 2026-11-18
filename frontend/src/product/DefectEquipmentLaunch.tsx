import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { SourceDraftLaunch } from './SourceDraftLaunch';
import { defectApi } from './defect-api';
import type { Session } from './directory-contract';
// A saved equipment pointer is launch context, never an inferred criticality,
// copied engineering assessment or an authorization to return to service.
export function DefectEquipmentLaunch({ session, vessel, assetId, revision, language, disabled, onNavigate }: {
    session: Session;
    vessel: string;
    assetId: string;
    revision: number;
    language: AppLocale;
    disabled: boolean;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    return <SourceDraftLaunch session={session} vessel={vessel} sourceId={assetId} revision={revision} href={`/app/defects/${vessel}?${new URLSearchParams({ assetId, assetRevision: String(revision) })}`} disabled={disabled} loadCapability={defectApi.capabilities} onNavigate={onNavigate} title={localizedText(en, "pairs.m_0f1d3f5ec2abfb44")} description={localizedText(en, "pairs.m_8ac05b9fa2c5c23d")} action={localizedText(en, "pairs.m_cfc23f6b43e15201")}/>;
}
