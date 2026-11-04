import { useEffect, useState } from 'react'
import { ApiError, request } from './api'
import type { Session } from './directory-contract'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'

export type EnterpriseDestination = { key: string; href: string }
const destinations: Record<string, string> = { today: '/app/today', fleet: '/app/fleet', companies: '/app/companies',
  sources: '/app/sources', requirements: '/app/requirements', audit: '/app/audit',
  'tug-command': '/app/tug-command', 'tug-job-board': '/app/tug-job-board', 'tug-job-intake': '/app/tug-job-intake', 'system-health': '/app/system-health' }
export function decodeEnterpriseNavigation(value: unknown): { destinations: EnterpriseDestination[]; canSearch: boolean } {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new ApiError(502, 'invalid_navigation')
  const v = value as Record<string, unknown>
  if (!Array.isArray(v.destinations) || v.destinations.length > Object.keys(destinations).length || typeof v.canSearch !== 'boolean' || typeof v.canWriteRegulatory !== 'boolean') throw new ApiError(502, 'invalid_navigation')
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
