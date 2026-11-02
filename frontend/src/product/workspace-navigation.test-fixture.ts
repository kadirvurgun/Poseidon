import type { Lenses } from './workspace-lens-api'
import type { WorkspaceContext } from './workspace-context-api'

// Synthetic server projections for existing mocked shell/form tests. New endpoint,
// wire-boundary and signed-host cases separately cover actual trusted provisioning.
export const navigationTestIdentity = { tenantId: '10000000-0000-0000-0000-000000000098', actorId: '10000000-0000-0000-0000-000000000099' }
export const navigationTestRole = '20000000-0000-0000-0000-000000000099'
export function navigationTestLenses(tenantId: string, actorId: string, vesselId: string | null, canSearch = false, companies = true): Lenses {
  return { tenantId, actorId, vesselId, mode: vesselId ? 'Ship' : 'Fleet', state: 'Configured', version: 'synthetic-menu-v1', canSearch,
    lenses: [{ reference: navigationTestRole, tr: 'KURGUSAL test menüsü', en: 'FICTIONAL test menu', items: [
      ...(companies ? [{ key: 'companies', href: '/app/companies', group: 'daily' as const }] : []),
      { key: 'today', href: '/app/today' + (vesselId ? '/' + vesselId : ''), group: 'daily' },
      { key: 'fleet', href: '/app/fleet', group: 'daily' },
    ] }] }
}
export async function navigationTestContext(tenantId: string, actorId: string, vessel: string | null): Promise<WorkspaceContext> {
  return { tenantId, actorId, roleContext: { state: 'Missing', catalogVersion: null, roles: [] },
    vessel: vessel ? { id: vessel, code: 'SYNTHETIC', name: 'Synthetic vessel', kind: 'Ship' } : null }
}
