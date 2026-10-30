// APP-FOUNDATION-01: public technical heartbeat; no operational data or identity.
import type { components } from './generated/api'
export type SystemStatus = components['schemas']['SystemStatus']

export async function getSystemStatus(signal: AbortSignal): Promise<SystemStatus> {
  const response = await fetch('/api/v1/system/status', {
    signal,
    cache: 'no-store',
    headers: { Accept: 'application/json' },
  })
  if (!response.ok) throw new Error('System status request failed')
  const payload: unknown = await response.json()
  if (typeof payload !== 'object' || payload === null || !('service' in payload) ||
      !('status' in payload) || payload.service !== 'poseidon' || payload.status !== 'available') {
    throw new Error('Unexpected system status contract')
  }
  return { service: 'poseidon', status: 'available' }
}
