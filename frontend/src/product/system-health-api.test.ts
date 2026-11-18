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
])('rejects wrong-scope, invented zero/healthy or malformed telemetry %#', value => {
  expect(() => decodeSystemHealth(value, healthVessel)).toThrow()
})
