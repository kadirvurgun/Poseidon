import { useEffect, useState } from 'react'
import { ApiError, request } from './api'
import type { Session } from './directory-contract'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

export type EnterpriseDestination = { key: string; href: string }
const destinations: Record<string, string> = { 'operational-downtime': '/app/operational-downtime', 'risk-assessment': '/app/risk-assessment', 'pms-command-center': '/app/pms-command-center', 'role-uat': '/app/role-uat', 'pilot-gate': '/app/pilot-gate', 'pilot-program': '/app/pilot-program', 'user-admin': '/app/user-admin', 'departure-signoff': '/app/departure-signoff', 'regulatory-review': '/app/regulatory-review', 'service-engineer-report': '/app/service-engineer-report', 'voice-event-draft': '/app/voice-event-draft', 'document-compare': '/app/document-compare', 'risk-template-library': '/app/risk-template-library', 'critical-spares': '/app/critical-spares', 'high-risk-work': '/app/high-risk-work', 'safety-events': '/app/safety-events', 'data-trust-center': '/app/data-trust-center', 'industry-assurance': '/app/industry-assurance', 'towage-plan': '/app/towage-plan', 'towage-risk-briefing': '/app/towage-risk-briefing', 'towage-execution': '/app/towage-execution', 'tug-daily-log': '/app/tug-daily-log', 'tug-operation-history': '/app/tug-operation-history', 'tug-operations-offline': '/app/tug-operations-offline', 'tug-demo-journey': '/app/tug-demo-journey', 'drydock-planning': '/app/drydock-planning', 'work-order-center': '/app/work-order-center', defects: '/app/defects', 'fuel-bunker-log': '/app/fuel-bunker-log', 'port-activity-sof': '/app/port-activity-sof', 'certificate-command-center': '/app/certificate-command-center', 'renewal-workflow': '/app/renewal-workflow', 'pre-survey-checklist': '/app/pre-survey-checklist', 'survey-planner': '/app/survey-planner', 'noon-report': '/app/noon-report', 'deck-daily-log': '/app/deck-daily-log', 'engine-daily-log': '/app/engine-daily-log', today: '/app/today', fleet: '/app/fleet', companies: '/app/companies',
  sources: '/app/sources', requirements: '/app/requirements', audit: '/app/audit', 'audit-ready': '/app/audit-ready',
  'tug-command': '/app/tug-command', 'tug-job-board': '/app/tug-job-board', 'tug-job-intake': '/app/tug-job-intake', 'tug-assignment': '/app/tug-assignment', 'tug-readiness': '/app/tug-readiness', 'tug-equipment-readiness': '/app/tug-equipment-readiness', 'tug-crew-readiness': '/app/tug-crew-readiness', 'system-health': '/app/system-health', 'notification-escalation': '/app/notification-escalation', 'offline-conflict-center': '/app/offline-conflict-center' }
export function decodeEnterpriseNavigation(value: unknown): { destinations: EnterpriseDestination[]; canSearch: boolean } {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new ApiError(502, 'invalid_navigation')
  const v = value as Record<string, unknown>
  // DEV-022: closed shape. An extra member (for example a hidden count) is a contract breach, not ignored.
  if (Object.keys(v).length !== 3 || !Array.isArray(v.destinations) || v.destinations.length > Object.keys(destinations).length || typeof v.canSearch !== 'boolean' || typeof v.canWriteRegulatory !== 'boolean') throw new ApiError(502, 'invalid_navigation')
  const result: EnterpriseDestination[] = []
  for (const item of v.destinations) {
    if (!item || typeof item !== 'object' || Array.isArray(item) || Object.keys(item).length !== 2 || typeof item.key !== 'string' ||
      !Object.hasOwn(destinations, item.key) || item.href !== destinations[item.key] || result.some(d => d.key === item.key)) throw new ApiError(502, 'invalid_navigation')
    result.push({ key: item.key, href: item.href })
  }
  return { destinations: result, canSearch: v.canSearch }
}

export function useEnterpriseNavigation(session: Session | null) {
  const epoch = useWorkspaceRevalidation()
  const [attempt, retry] = useState(0)
  const [state, setState] = useState<{ owner: Session; epoch: number; attempt: number; destinations: EnterpriseDestination[]; canSearch: boolean; failed?: boolean } | null>(null)
  useEffect(() => {
    if (!session?.authenticated || !session.canRead) return
    const controller = new AbortController(); let active = true
    const timer = setTimeout(() => controller.abort(), 10000)
    request<unknown>('workspace', controller.signal).then(decodeEnterpriseNavigation).then(value => {
      if (active) setState({ owner: session, epoch, attempt, ...value })
    }).catch(() => { if (active) setState({ owner: session, epoch, attempt, destinations: [], canSearch: false, failed: true }) })
    return () => { active = false; controller.abort(); clearTimeout(timer) }
  }, [session, epoch, attempt])
  const current = state?.owner === session && state.epoch === epoch && state.attempt === attempt ? state : null
  return { destinations: current?.destinations ?? [], canSearch: current?.canSearch ?? false,
    searchGrant: current?.canSearch && !current.failed ? current : null,
    epoch, loading: !!session?.authenticated && session.canRead && !current, failed: current?.failed ?? false, retry: () => retry(n => n + 1) }
}

/** Every navigation destination key the client contract accepts (USER-GUIDE-01 help coverage guard). */
export const enterpriseDestinationKeys: readonly string[] = Object.freeze(Object.keys(destinations))
