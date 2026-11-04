import type { SystemHealth } from './system-health-api'

export const healthVessel = '10000000-0000-4000-8000-000000000001'
export const unobservedHealth = (): SystemHealth => ({ vesselId: healthVessel, readAt: '2026-11-04T12:00:00Z',
  api: { state: 'Unavailable', scope: 'TenantProcessSinceStart', requests: null, errors: null, averageLatencyMs: null },
  audit: { state: 'Unavailable', scope: 'TenantProcessSinceStart', persisted: null, unavailable: null, averageLatencyMs: null },
  sync: { state: 'Observed', scope: 'SelectedVesselServerOutcomes', committed: 0, unresolvedConflicts: 0, localBacklog: null },
  files: { state: 'ConfiguredUnprobed', adapter: 'LocalDevelopment', scanState: 'Unavailable' },
  freshness: { state: 'Unavailable', scope: 'TenantProcessSinceStart', current: null, stale: null, future: null, unknown: null, unavailable: null } })
