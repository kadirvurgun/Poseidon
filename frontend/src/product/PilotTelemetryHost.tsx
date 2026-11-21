import { useEffect } from 'react'
import { request } from './api'
import { decodePilotCapabilities, pilotAreaForPath, pilotFlushNow, pilotRoleActive, pilotTelemetryOptedOut, startPilotTelemetry } from './pilot-telemetry'
import type { Session } from './directory-contract'

// S11 gate B1 (DEV-072): app-shell wiring of the privacy-safe pilot telemetry. Mounted once next to
// the routed workspace (main.tsx). It renders nothing and starts the queue only when the trusted
// server says this membership is enrolled AND retention is configured (capabilities.canContribute)
// and the user has not opted out in the pilot panel. Any failure leaves telemetry off silently;
// it never blocks, delays or changes a screen.
export function PilotTelemetryHost({ pathname = window.location.pathname }: { pathname?: string }) {
  useEffect(() => {
    if (!/^\/app(?:\/|$)/.test(pathname) || pilotTelemetryOptedOut()) return
    const c = new AbortController(), timer = setTimeout(() => c.abort(), 15000)
    let stop: (() => void) | null = null
    void (async () => {
      const session = await request<Session>('auth/session', c.signal)
      if (!session.authenticated || !session.canRead || !session.tenantId || !session.actorId || !session.csrfToken) return
      const caps = decodePilotCapabilities(await request<unknown>('pilot-metrics/capabilities', c.signal))
      if (c.signal.aborted || !caps.canContribute) return
      stop = startPilotTelemetry({ tenantId: session.tenantId.toLowerCase(), actorId: session.actorId.toLowerCase(), csrfToken: session.csrfToken })
      pilotRoleActive(pilotAreaForPath(pathname))
      pilotFlushNow()
    })().catch(() => undefined).finally(() => clearTimeout(timer))
    return () => { c.abort(); clearTimeout(timer); stop?.() }
  }, [pathname])
  return null
}
