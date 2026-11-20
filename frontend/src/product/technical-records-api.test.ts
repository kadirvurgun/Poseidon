import { describe, expect, it } from 'vitest'
import { ApiError } from './api'
import { decodeCommandCenter, decodeLifecycle, decodeServiceReport, freezeTechnicalCommand } from './technical-records-api'
import { centerFixture, lifecycleFixture, serviceFixture, techDefect, techVessel } from './technical-records-test-fixtures'

describe('DEV-055/DEV-056/DEV-103 technical record wire contract', () => {
  it('accepts an open lifecycle and rejects a closure without its human record', () => {
    expect(decodeLifecycle(lifecycleFixture(), techVessel, techDefect).stage).toBe('ASSESS')
    expect(() => decodeLifecycle(lifecycleFixture({ stage: 'CLOSED' }), techVessel, techDefect)).toThrow(ApiError)
    expect(() => decodeLifecycle(lifecycleFixture({ stage: 'CLOSED', closedBy: '00000000-0000-4000-8000-0000000055f9' }), techVessel, techDefect)).toThrow(ApiError)
    expect(() => decodeLifecycle(lifecycleFixture({ stage: 'FIXED' }), techVessel, techDefect)).toThrow(ApiError)
    expect(() => decodeLifecycle(lifecycleFixture(), '00000000-0000-4000-8000-000000000999', techDefect)).toThrow(ApiError)
  })
  it('never accepts verified evidence, a phantom verification or editable submitted reports', () => {
    expect(decodeServiceReport(serviceFixture(), techVessel).state).toBe('SUBMITTED')
    expect(() => decodeServiceReport(serviceFixture({ verificationState: 'VERIFIED' }), techVessel)).toThrow(ApiError)
    expect(() => decodeServiceReport(serviceFixture({ state: 'VERIFIED', verificationState: 'VERIFIED', verifiedRevision: null, canVerify: false }), techVessel)).toThrow(ApiError)
    expect(() => decodeServiceReport(serviceFixture({ evidence: [{ id: 'x', revision: 1, currency: 'CURRENT', state: 'VERIFIED' }] }), techVessel)).toThrow(ApiError)
    expect(() => decodeServiceReport(serviceFixture({ canEdit: true }), techVessel)).toThrow(ApiError)
  })
  it('rejects a command center that turns an unknown source into zero or a clean summary', () => {
    expect(decodeCommandCenter(centerFixture(), techVessel).readinessState).toBe('BLOCKED')
    const zero = centerFixture(); (zero.sections as Record<string, unknown>[])[2] = { ...(zero.sections as Record<string, unknown>[])[2], count: 0 }
    expect(() => decodeCommandCenter(zero, techVessel)).toThrow(ApiError)
    expect(() => decodeCommandCenter(centerFixture({ readinessState: 'NO_OPEN_EXCEPTIONS' }), techVessel)).toThrow(ApiError)
    const quietWithItems = centerFixture(); (quietWithItems.sections as Record<string, unknown>[])[1] = { ...(quietWithItems.sections as Record<string, unknown>[])[1], state: 'QUIET' }
    expect(() => decodeCommandCenter(quietWithItems, techVessel)).toThrow(ApiError)
  })
  it('freezes commands with one key and refuses foreign or extended paths', () => {
    const attempt = freezeTechnicalCommand('POST', `defects/${techDefect}/lifecycle/close?vesselId=${techVessel}`, { a: 1 })
    expect(attempt.key).toMatch(/^[0-9a-f-]{36}$/); expect(attempt.body).toBe('{"a":1}')
    expect(freezeTechnicalCommand('PUT', `service-reports/${techDefect}?vesselId=${techVessel}`, {}).method).toBe('PUT')
    expect(() => freezeTechnicalCommand('POST', '../auth/session', {})).toThrow(ApiError)
    expect(() => freezeTechnicalCommand('POST', `service-reports?vesselId=${techVessel}&tenantId=x`, {})).toThrow(ApiError)
  })
})
