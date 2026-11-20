import { describe, expect, it } from 'vitest'
import { decodeExecution, decodeExecutionView, freezeEvidence, freezeTransition, positiveQuantity, validEvidence } from './work-order-execution-api'
import { decodeRequisition, decodeSpare, freezeDecision, freezeProposal } from './critical-spares-api'
import { decodeWorkOrderCapture, decodeWorkOrderOutcome, projectWorkOrderQueue, type WorkOrderOfflineContext } from './work-order-offline'
import { executionFixture, executionVessel, executionViewFixture, executionWorkOrder, requisitionFixture, spareFixture } from './work-order-execution-test-fixtures'

describe('DEV-054 execution contract', () => {
  it('accepts a human-recorded execution and rejects manufactured verification or unknown states', () => {
    expect(decodeExecution(executionFixture(), executionVessel, executionWorkOrder).state).toBe('IN_PROGRESS')
    expect(() => decodeExecution({ ...executionFixture(), state: 'APPROVED' }, executionVessel, executionWorkOrder)).toThrow()
    // HUMAN_VERIFIED is impossible before VERIFIED/CLOSED; VERIFIED without a verification time is a breach.
    expect(() => decodeExecution({ ...executionFixture(), gate: { state: 'HUMAN_VERIFIED', required: ['READING'], present: ['READING'], missing: [] } }, executionVessel, executionWorkOrder)).toThrow()
    expect(() => decodeExecution(executionFixture({ state: 'VERIFIED' }), executionVessel, executionWorkOrder)).toThrow()
    // An unconfigured policy is REVIEW_REQUIRED and can never list requirements.
    expect(() => decodeExecution({ ...executionFixture(), requiredEvidence: null, gate: { state: 'REVIEW_REQUIRED', required: ['READING'], present: [], missing: [] } }, executionVessel, executionWorkOrder)).toThrow()
    expect(() => decodeExecution({ ...executionFixture(), extra: true }, executionVessel, executionWorkOrder)).toThrow()
    expect(() => decodeExecution(executionFixture(), '99999999-9999-4999-8999-999999999999', executionWorkOrder)).toThrow()
    expect(() => decodeExecutionView({ ...executionViewFixture(), capabilities: { ...executionViewFixture().capabilities, offline: { supported: true, state: 'UNAVAILABLE_POLICY' } } }, executionVessel, executionWorkOrder)).toThrow()
  })
  it('freezes exact transition and evidence bodies bound to the current version and evidence hash', () => {
    const view = executionViewFixture()
    const submit = JSON.parse(freezeTransition(executionVessel, view, { action: 'SUBMIT' }).body)
    expect(submit).toEqual({ workOrderId: executionWorkOrder, action: 'SUBMIT', expectedVersion: 12, expectedRevision: 2, workOrderRevision: null, workOrderVersion: null,
      requiredEvidence: null, expectedEvidenceHash: 'A'.repeat(64), reasonCode: null })
    const release = JSON.parse(freezeTransition(executionVessel, executionViewFixture(null), { action: 'RELEASE', requiredEvidence: null }).body)
    expect(release).toMatchObject({ action: 'RELEASE', expectedVersion: 0, workOrderRevision: 1, workOrderVersion: 5, requiredEvidence: null })
    expect(() => freezeTransition(executionVessel, view, { action: 'RETURN' })).toThrow()
    expect(() => freezeTransition(executionVessel, view, { action: 'REQUIREMENTS', requiredEvidence: [] })).toThrow()
    expect(() => freezeTransition(executionVessel, executionViewFixture(executionFixture({ currentRevision: false })), { action: 'SUBMIT' })).toThrow()
    const evidence = { readings: [{ code: 'TEMP', value: '61.5', unit: 'degC', takenAt: null, note: null }], spares: [], files: [], workDone: 'FICTIONAL', findings: null }
    expect(JSON.parse(freezeEvidence(executionVessel, executionFixture(), evidence).body)).toEqual({ workOrderId: executionWorkOrder, expectedVersion: 12, expectedRevision: 2, evidence })
    expect(() => freezeEvidence(executionVessel, executionFixture({ state: 'RELEASED' }), evidence)).toThrow()
    expect(validEvidence({ ...evidence, readings: [{ ...evidence.readings[0], value: '61,5' }] })).toBe(false)
    expect(validEvidence({ ...evidence, spares: [{ spareId: null, spareRevision: null, quantity: '1', description: null }] })).toBe(false)
    expect(positiveQuantity('0')).toBe(false); expect(positiveQuantity('01')).toBe(false); expect(positiveQuantity('2.50')).toBe(false); expect(positiveQuantity('2.5')).toBe(true)
  })
})

describe('DEV-110 critical spare contract', () => {
  it('never decodes unknown stock as low or healthy and keeps the ERP reference only on approval', () => {
    expect(decodeSpare(spareFixture(), executionVessel).stock.state).toBe('LOW_STOCK')
    const row = spareFixture()
    expect(() => decodeSpare({ ...row, stock: { ...row.stock, state: 'ABOVE_MINIMUM', minimum: null } }, executionVessel)).toThrow()
    expect(() => decodeSpare({ ...row, stock: { ...row.stock, state: 'OK' } }, executionVessel)).toThrow()
    expect(() => decodeSpare({ ...row, stock: { ...row.stock, state: 'STOCK_UNKNOWN' } }, executionVessel)).toThrow()
    expect(() => decodeRequisition({ ...requisitionFixture(), erpReference: { kind: 'TechnicalRequisition' } }, executionVessel)).toThrow()
    expect(() => decodeRequisition({ ...requisitionFixture(), state: 'ORDERED' }, executionVessel)).toThrow()
    expect(() => decodeRequisition(requisitionFixture({ state: 'APPROVED_FOR_ERP_EXPORT', canDecide: false, approvedQuantity: '9', decidedAt: '2026-11-20T09:00:00Z' }), executionVessel)).toThrow()
    expect(JSON.parse(freezeProposal(executionVessel, spareFixture()).body)).toMatchObject({ spareId: spareFixture().id, spareRevision: 1 })
    expect(() => freezeProposal(executionVessel, spareFixture({ openRequisitionId: requisitionFixture().id }))).toThrow()
    expect(JSON.parse(freezeDecision(executionVessel, requisitionFixture(), 'REJECT', null, 'STOCK_COUNT_DISPUTED').body)).toMatchObject({ decision: 'REJECT', reasonCode: 'STOCK_COUNT_DISPUTED', approvedQuantity: null })
    expect(() => freezeDecision(executionVessel, requisitionFixture(), 'REJECT', null, null)).toThrow()
    expect(() => freezeDecision(executionVessel, requisitionFixture({ canDecide: false }), 'APPROVE', null, null)).toThrow()
  })
})

describe('DEV-057 offline capture contract', () => {
  const context = { id: '88888888-8888-4888-8888-888888888888', workOrderId: executionWorkOrder, keyId: '99999999-9999-4999-8999-999999999991', deviceId: '99999999-9999-4999-8999-999999999992',
    policyVersion: 'fictional', policyDigest: 'E'.repeat(64), sourceVersion: 'synthetic', operation: 'work-order.execution-evidence.v1', baseVersion: 12 } as WorkOrderOfflineContext
  const capture = (id: string, extra: Record<string, unknown> = {}) => ({ commandId: id, workOrderId: executionWorkOrder, keyId: context.keyId, deviceId: context.deviceId, policyVersion: 'fictional',
    policyDigest: 'E'.repeat(64), sourceVersion: 'synthetic', operation: context.operation, baseVersion: 12, capturedAt: '2026-11-20T10:00:00Z', offsetMinutes: 180,
    evidence: { readings: [], spares: [], files: [], workDone: 'FICTIONAL', findings: null }, ...extra })
  it('binds captures to the enrollment and keeps a conflict pending until an explicit human resolution is recorded', () => {
    const id = '12345678-1234-4234-8234-123456789012'
    expect(decodeWorkOrderCapture(capture(id), context, id).baseVersion).toBe(12)
    expect(() => decodeWorkOrderCapture(capture(id, { baseVersion: 11 }), context, id)).toThrow()
    expect(() => decodeWorkOrderCapture(capture(id, { deviceId: context.keyId }), context, id)).toThrow()
    expect(() => decodeWorkOrderCapture(capture(id, { parentCommandId: id }), context, id)).toThrow()
    const conflict = { commandId: id, result: 'MaterialConflict', workOrderId: executionWorkOrder, baseVersion: 12, serverVersion: 13, revision: 3, parentCommandId: null, receivedAt: '2026-11-20T10:05:00Z' } as const
    expect(decodeWorkOrderOutcome(conflict, executionWorkOrder).result).toBe('MaterialConflict')
    const child = '12345678-1234-4234-8234-123456789013'
    const rows = [{ commandId: id, revision: 1, body: JSON.stringify(capture(id)), outcome: conflict }]
    expect(projectWorkOrderQueue(rows)).toMatchObject({ pending: 1, conflicts: [id] })
    const resolved = [...rows, { commandId: child, revision: 1, body: JSON.stringify(capture(child, { evidence: null, parentCommandId: id, expectedServerVersion: 13, baseVersion: 13, choice: 'keepServer', reasonCode: 'CURRENT_SERVER_RETAINED' })),
      outcome: { ...conflict, commandId: child, result: 'ServerRetained' as const, baseVersion: 13, parentCommandId: id } }]
    expect(projectWorkOrderQueue(resolved)).toMatchObject({ pending: 0, conflicts: [] })
  })
})
