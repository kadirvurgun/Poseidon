import { expect, it } from 'vitest'
import { decodeSystemHealth } from './system-health-api'
import { healthVessel, unobservedHealth } from './system-health.test-fixture'

it('DEV-037 retains unavailable telemetry as null and configuration as unprobed', () => {
  expect(decodeSystemHealth(unobservedHealth(), healthVessel)).toEqual(unobservedHealth())
})
it.each([
  { ...unobservedHealth(), vesselId: '10000000-0000-4000-8000-000000000002' },
  { ...unobservedHealth(), api: { ...unobservedHealth().api, scope: 'ProcessSinceStart' } },
  { ...unobservedHealth(), api: { ...unobservedHealth().api, requests: 0 } },
  { ...unobservedHealth(), sync: { ...unobservedHealth().sync, localBacklog: 0 } },
  { ...unobservedHealth(), sync: { ...unobservedHealth().sync, unresolvedConflicts: -1 } },
  { ...unobservedHealth(), files: { ...unobservedHealth().files, state: 'Healthy' } },
  { ...unobservedHealth(), files: { ...unobservedHealth().files, state: ['ConfiguredUnprobed'] } },
  { ...unobservedHealth(), api: { state: 'Observed', scope: 'TenantProcessSinceStart', requests: 1, errors: 0, averageLatencyMs: Infinity } },
  { ...unobservedHealth(), sync: { ...unobservedHealth().sync, committed: 3 } },
  { ...unobservedHealth(), sync: { ...unobservedHealth().sync, lastCommittedAt: '2026-11-04T11:59:00Z' } },
  { ...unobservedHealth(), service: { state: 'Unavailable', startedAt: '2026-11-04T11:00:00Z', uptimeSeconds: null } },
  { ...unobservedHealth(), service: { state: 'Healthy', startedAt: null, uptimeSeconds: null } },
  { ...unobservedHealth(), notifications: { ...unobservedHealth().notifications, dispatch: 'Healthy' } },
  { ...unobservedHealth(), notifications: { ...unobservedHealth().notifications, overdueEscalations: 1 } },
  { ...unobservedHealth(), notifications: { ...unobservedHealth().notifications, scope: 'Global' } },
  { ...unobservedHealth(), notifications: { ...unobservedHealth().notifications, delivered: 1 } },
  Object.fromEntries(Object.entries(unobservedHealth()).filter(([key]) => key !== 'notifications')),
])('rejects wrong-scope, invented zero/healthy or malformed telemetry %#', value => {
  expect(() => decodeSystemHealth(value, healthVessel)).toThrow()
})
it('DEV-037 accepts real last-commit and notification job observations', () => {
  const value = { ...unobservedHealth(), sync: { ...unobservedHealth().sync, committed: 2, lastCommittedAt: '2026-11-04T11:58:00Z' },
    notifications: { state: 'Observed', scope: 'SelectedVesselDurable', openUnacknowledged: 3, overdueEscalations: 1, dispatch: 'LeaseExpired', lastCompletedAt: '2026-11-04T11:00:00Z', nextRunAt: '2026-11-04T11:00:30Z' } }
  expect(decodeSystemHealth(value, healthVessel)).toEqual(value)
})
