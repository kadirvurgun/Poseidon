import { SourceDraftLaunch } from './SourceDraftLaunch'
import { workOrderApi } from './work-order-api'
import type { Session } from './directory-contract'

export function WorkOrderPlanLaunch({ session, vessel, planId, revision, language, disabled, onNavigate }: {
  session: Session; vessel: string; planId: string; revision: number; language: 'en' | 'tr'; disabled: boolean; onNavigate: (href: string) => void
}) {
  const en = language === 'en'
  return <SourceDraftLaunch session={session} vessel={vessel} sourceId={planId} revision={revision}
    href={`/app/work-order-center/${vessel}?${new URLSearchParams({ planId, planRevision: String(revision) })}`} disabled={disabled} loadCapability={workOrderApi.capabilities} onNavigate={onNavigate}
    title={en ? 'Prepare a work order' : 'İş emri hazırla'} description={en ? 'Open a separate draft linked to this saved maintenance plan.' : 'Bu kayıtlı bakım planına bağlı ayrı bir taslak açın.'}
    action={en ? 'Open linked work order draft' : 'Bağlı iş emri taslağı aç'} />
}
