import { SourceDraftLaunch } from './SourceDraftLaunch'
import { defectApi } from './defect-api'
import type { Session } from './directory-contract'

// A saved equipment pointer is launch context, never an inferred criticality,
// copied engineering assessment or an authorization to return to service.
export function DefectEquipmentLaunch({ session, vessel, assetId, revision, language, disabled, onNavigate }: {
  session: Session; vessel: string; assetId: string; revision: number; language: 'en' | 'tr';
  disabled: boolean; onNavigate: (href: string) => void
}) {
  const en = language === 'en'
  return <SourceDraftLaunch session={session} vessel={vessel} sourceId={assetId} revision={revision}
    href={`/app/defects/${vessel}?${new URLSearchParams({ assetId, assetRevision: String(revision) })}`} disabled={disabled} loadCapability={defectApi.capabilities} onNavigate={onNavigate}
    title={en ? 'Report a defect' : 'Arıza kaydı aç'} description={en ? 'Start a separate declaration linked to this saved equipment revision.' : 'Bu kayıtlı ekipman sürümüne bağlı ayrı bir beyan açın.'}
    action={en ? 'Open linked defect draft' : 'Bağlı arıza taslağı aç'} />
}
