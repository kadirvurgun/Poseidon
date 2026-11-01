import { webcrypto } from 'node:crypto'
import { describe, expect, it } from 'vitest'
import { decodeOfflineCapture, decodeOfflineSnapshot, offlineKeys, offlineOperation, offlineSource } from './offline-capture-contract'
import { openOfflineRecord, sealOfflineRecord } from './offline-cipher'
import { decodeOfflineContext } from './offline-application'

// FR46/TC46-crew-profile-medical_expiry; supported watch-group boundary.
// Real WebCrypto below is primitive+consumer coverage, not real-host coverage.
const id = '11111111-1111-4111-8111-111111111111', parent = '22222222-2222-4222-8222-222222222222'
const scope = { keyId: '33333333-3333-4333-8333-333333333333', deviceId: '44444444-4444-4444-8444-444444444444',
  policyVersion: 'synthetic-v1', baseVersion: 7, baseRevision: 2, watchGroups: ['SYNTHETIC-A', 'SYNTHETIC-B'] }
const command = () => ({ commandId: id, keyId: scope.keyId, deviceId: scope.deviceId, policyVersion: scope.policyVersion,
  sourceVersion: offlineSource, operation: offlineOperation, baseVersion: scope.baseVersion,
  capturedAt: '2026-11-02T00:00:00.000Z', offsetMinutes: 180, watchGroup: 'SYNTHETIC-A' })
const snapshot = () => ({ kind: 'snapshot', watchGroup: null, baseVersion: 7, revision: 2 })

describe('closed supported offline plaintext (AUTHORED / NOT RUN)', () => {
  it('retains unknown and exact captured bytes while returning only the supported snapshot fields', () => {
    expect(decodeOfflineSnapshot(snapshot(), scope)).toEqual({ watchGroup: null, baseVersion: 7, revision: 2 })
    const body = command(), frozen = JSON.stringify(body)
    expect(decodeOfflineCapture(body, id, scope)).toBe(body)
    expect(JSON.stringify(body)).toBe(frozen)
    expect(decodeOfflineCapture({ ...body, watchGroup: null }, id, scope).watchGroup).toBeNull()
  })
  it.each(['medicalExpiry', 'medical_expiry', 'fullName', 'fitness', 'photoReference'])('rejects %s in both snapshot and replay material', field => {
    expect(() => decodeOfflineSnapshot({ ...snapshot(), [field]: 'PRIVATE-SYNTHETIC' }, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
    expect(() => decodeOfflineCapture({ ...command(), [field]: 'PRIVATE-SYNTHETIC' }, id, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
  })
  it('rejects scope substitution, unsupported fields, missing values, provenance and integer coercion', () => {
    for (const change of [{ keyId: parent }, { deviceId: parent }, { commandId: parent }, { policyVersion: 'changed' },
      { sourceVersion: 'changed' }, { operation: 'crew.health.v1' }, { watchGroup: 'UNCONFIGURED' }, { watchGroup: undefined },
      { baseVersion: 8 }, { baseVersion: '7' }, { capturedAt: '2026-11-02T03:00:00+03:00' }, { offsetMinutes: 841 }])
      expect(() => decodeOfflineCapture({ ...command(), ...change }, id, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
    expect(() => decodeOfflineSnapshot({ ...snapshot(), revision: 3 }, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
    expect(() => decodeOfflineSnapshot({ ...snapshot(), watchGroup: { toString: () => 'SYNTHETIC-A' } }, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
  })
  it('requires complete explicit resolution lineage and reason without relaxing the supported field set', () => {
    const resolution = { ...command(), baseVersion: 9, parentCommandId: parent, expectedServerVersion: 9, choice: 'useCaptured', reason: 'Fictional explicit reconciliation' }
    expect(decodeOfflineCapture(resolution, id, scope)).toBe(resolution)
    for (const field of ['parentCommandId', 'expectedServerVersion', 'choice', 'reason']) {
      const incomplete: Record<string, unknown> = { ...resolution }; delete incomplete[field]
      expect(() => decodeOfflineCapture(incomplete, id, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
    }
    for (const change of [{ parentCommandId: id }, { expectedServerVersion: 8 }, { reason: ' ' }, { reason: 'x'.repeat(501) }, { choice: 'automatic' }])
      expect(() => decodeOfflineCapture({ ...resolution, ...change }, id, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
  })
  it('rejects unexpected protected fields before a returned context can enter the local device key/capture flow', () => {
    const context = { ...scope, id, tenantId: parent, actorId: parent, vesselId: id, assignmentId: parent,
      sessionBinding: id, keyChallenge: 'A'.repeat(43) + '=', sourceVersion: offlineSource, baseWatchGroup: null,
      serverTime: '2026-11-02T00:00:00Z', expiresAt: '2026-11-02T01:00:00Z', maxRecords: 8, maxEncryptedBytes: 32768,
      maxDeviceRecords: 16, maxDeviceEncryptedBytes: 65536 }
    expect(decodeOfflineContext(context, id, parent)).toBe(context)
    expect(() => decodeOfflineContext({ ...context, medicalExpiry: '2031-02-03' }, id, parent)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
    expect(() => decodeOfflineContext({ ...context, serverTime: '2026-02-30T00:00:00Z' }, id, parent)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
  })
  it('authenticates and decrypts real bytes before rejecting a sensitive payload; encryption alone does not authorize fields', async () => {
    const provider = webcrypto as unknown as Crypto
    const key = await provider.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    const context = { tenantId: id, actorId: parent, vesselId: id, deviceId: scope.deviceId, sessionBinding: id, policyVersion: scope.policyVersion, commandId: id }
    const limits = { plaintextBytes: 4096, contextBytes: 2048 }
    const data = new TextEncoder().encode(JSON.stringify({ ...command(), medicalExpiry: '2031-02-03' }))
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    const decrypted: unknown = JSON.parse(new TextDecoder().decode(await openOfflineRecord(provider, key, context, sealed, limits)))
    expect(() => decodeOfflineCapture(decrypted, id, scope)).toThrow(expect.objectContaining({ status: 502, code: 'invalid_offline_contract' }))
    expect(key.extractable).toBe(false)
    expect(offlineKeys({ body: '{}', medicalExpiry: '2031-02-03' }, ['body'], ['outcome'])).toBe(false)
  })
})
