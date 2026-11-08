import { SourceDraftLaunch } from './SourceDraftLaunch'
import { downtimeApi } from './downtime-api'
import type { Session } from './directory-contract'

type SourceKind = 'Defect' | 'WorkOrder'
const grants = Object.fromEntries((['Defect', 'WorkOrder'] as const).map(kind => [kind, async (vessel: string, signal: AbortSignal) => {
  const caps = await downtimeApi.capabilities(vessel, signal)
  return { canWrite: caps.canWrite && caps.originKinds.includes(kind) }
}])) as Record<SourceKind, (vessel: string, signal: AbortSignal) => Promise<{ canWrite: boolean }>>

// Source read and target write are separate grants. Only an existing saved
// revision pointer is transferred; cause, time, claim and closure are not copied.
export function DowntimeSourceLaunch({ session, vessel, kind, sourceId, revision, language, disabled, onNavigate }: {
  session: Session; vessel: string; kind: SourceKind; sourceId: string; revision: number;
  language: 'en' | 'tr'; disabled: boolean; onNavigate: (href: string) => void
}) {
  const en = language === 'en'
  return <SourceDraftLaunch session={session} vessel={vessel} sourceId={sourceId} revision={revision}
    href={`/app/operational-downtime/${vessel}?${new URLSearchParams({ originKind: kind, originId: sourceId, originRevision: String(revision) })}`}
    disabled={disabled} loadCapability={grants[kind]} onNavigate={onNavigate}
    title={en ? 'Prepare an operational downtime record' : 'Operasyonel duruş kaydı hazırla'}
    description={en ? 'Open a separate draft linked to this saved declaration.' : 'Bu kayıtlı beyana bağlı ayrı bir taslak açın.'}
    action={en ? 'Open linked downtime draft' : 'Bağlı duruş taslağı aç'} />
}
