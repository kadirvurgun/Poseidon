import { useEffect, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { isGuid } from './identifiers'
import type { Session } from './directory-contract'

// Shared launch pattern: the target capability is current and owner-bound.
// Only saved resource pointers cross the URL, never field values or grants.
export function SourceDraftLaunch({ session, vessel, sourceId, revision, href, title, description, action, disabled, loadCapability, onNavigate }: {
  session: Session; vessel: string; sourceId: string; revision: number; href: string; title: string; description: string; action: string;
  disabled: boolean; loadCapability: (vessel: string, signal: AbortSignal) => Promise<{ canWrite: boolean }>; onNavigate: (href: string) => void
}) {
  const scope = `${vessel}:${sourceId}:${revision}:${href}`
  const [grant, setGrant] = useState<{ owner: Session; scope: string; canWrite: boolean } | null>(null)
  useEffect(() => {
    if (!session.authenticated || !session.canRead) return
    const controller = new AbortController(); let active = true
    const timer = setTimeout(() => controller.abort(), 10000)
    loadCapability(vessel, controller.signal).then(caps => {
      if (active && !controller.signal.aborted) setGrant({ owner: session, scope, canWrite: caps.canWrite })
    }).catch(() => { if (active) setGrant({ owner: session, scope, canWrite: false }) })
    return () => { active = false; controller.abort(); clearTimeout(timer) }
  }, [session, vessel, scope, loadCapability])
  if (!isGuid(vessel) || !isGuid(sourceId) || !Number.isSafeInteger(revision) || revision < 1 || grant?.owner !== session || grant.scope !== scope || !grant.canWrite) return null
  return <section aria-label={title}><h3>{title}</h3><p>{description}</p><EnterpriseActions align="start">
    <Button variant="outline" disabled={disabled} onClick={() => { if (!disabled) onNavigate(href) }}>{action}</Button>
  </EnterpriseActions></section>
}
