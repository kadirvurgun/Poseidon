import { webcrypto } from 'node:crypto'
import { expect, it } from 'vitest'
import { openOfflineRecord, sealOfflineRecord } from '../offline-cipher'
import { dailyCipherContext } from './daily-offline-application'
import { decodeDailyOfflineContext, decodeDailyCapture, decodeDailyOutcome } from './daily-offline-contract'
import { dailyOfflineFixture, dailyCaptureFixture, offlineDailyCommand, offlineDailyRecord, offlineDailyVessel } from './daily-offline.test-fixture'

it('keeps a new exact-resource zero base distinct from Unknown field values and lexical zero', () => {
  const context = decodeDailyOfflineContext(dailyOfflineFixture(), 'Noon', offlineDailyVessel, offlineDailyRecord)
  expect(context.baseVersion).toBe(0); expect(context.baseFields.position_lat).toEqual({ state: 'Unknown', value: null })
  expect(decodeDailyCapture(dailyCaptureFixture(), context, offlineDailyCommand).fields.position_lat.value).toBe('-0.000')
})
it.each([
  { operation: 'crew.draft.watch-group.v1' }, { operation: 'tug.draft.window.v1' }, { kind: 'Deck' },
  { sourceVersion: 'old' }, { authorityDigest: '' }, { allowedFields: ['master_review'], baseFields: { master_review: { state: 'Known', value: 'REVIEWED' } } },
  { allowedFields: ['vessel_id'], baseFields: { vessel_id: { state: 'Known', value: offlineDailyVessel } } },
])('rejects substituted authority/domain/source and system or review field policies %#', patch => {
  expect(() => decodeDailyOfflineContext({ ...dailyOfflineFixture(), ...patch }, 'Noon', offlineDailyVessel, offlineDailyRecord)).toThrow()
})
it('never widens the classified subset or silently rebases an encrypted command', () => {
  const context = dailyOfflineFixture(), original = dailyCaptureFixture()
  for (const patch of [{ baseVersion: 7 }, { fields: { ...original.fields, weather_summary: { state: 'Known', value: 'unclassified' } } },
    { fields: { position_lat: { state: 'Unknown', value: '0' } } }, { fields: { position_lat: { state: 'Known', value: 0 } } },
    { recordId: offlineDailyVessel }, { keyId: offlineDailyVessel }, { schemaHash: 'D'.repeat(64) }, { capturedAt: '2026-11-05T00:01:00' }]) {
    expect(() => decodeDailyCapture({ ...original, ...patch }, context, offlineDailyCommand)).toThrow()
  }
})
it('requires a new child identity, explicit current version and controlled correction reason for resolution', () => {
  const context = dailyOfflineFixture(), child = { ...dailyCaptureFixture(), fields: {}, baseVersion: 7, parentCommandId: offlineDailyRecord, expectedServerVersion: 7, choice: 'useCaptured', reasonCode: 'INPUT_CORRECTED' }
  expect(decodeDailyCapture(child, context, offlineDailyCommand).parentCommandId).toBe(offlineDailyRecord)
  for (const patch of [{ parentCommandId: offlineDailyCommand }, { choice: 'approve' }, { expectedServerVersion: 8 }, { reasonCode: '' }, { fields: dailyCaptureFixture().fields }]) expect(() => decodeDailyCapture({ ...child, ...patch }, context, offlineDailyCommand)).toThrow()
  expect(() => decodeDailyOutcome({ commandId: offlineDailyCommand, result: 'Committed', recordId: offlineDailyRecord, kind: 'Noon', baseVersion: 0, serverVersion: 0, revision: 0, parentCommandId: null, receivedAt: context.serverTime }, context)).toThrow()
})
it('uses real nonextractable WebCrypto and rejects changed resource, kind, role, policy or source context', async () => {
  const crypto = webcrypto as unknown as Crypto, context = dailyOfflineFixture(), limits = { plaintextBytes: 131072, contextBytes: 4096 }
  const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
  const bytes = new TextEncoder().encode(JSON.stringify(dailyCaptureFixture())), aad = dailyCipherContext(context, offlineDailyCommand)
  const sealed = await sealOfflineRecord(crypto, key, aad, bytes, limits)
  expect(new TextDecoder().decode(await openOfflineRecord(crypto, key, aad, sealed, limits))).toBe(new TextDecoder().decode(bytes))
  expect(key.extractable).toBe(false); expect(sealed).not.toHaveProperty('fields')
  for (const patch of [{ recordId: offlineDailyVessel }, { kind: 'Deck' as const }, { authorityDigest: 'D'.repeat(64) }, { policyDigest: 'E'.repeat(64) }, { sourceVersion: 'other-source' }]) {
    await expect(openOfflineRecord(crypto, key, dailyCipherContext({ ...context, ...patch }, offlineDailyCommand), sealed, limits)).rejects.toMatchObject({ code: 'context_mismatch' })
  }
})

it('represents explicitly retaining an absent server without claiming that a zero-version report was committed', () => {
  const context = dailyOfflineFixture(), result = { commandId: offlineDailyCommand, result: 'ServerRetained', recordId: context.recordId, kind: context.kind,
    baseVersion: 0, serverVersion: 0, revision: 0, parentCommandId: offlineDailyRecord, receivedAt: context.serverTime }
  expect(decodeDailyOutcome(result, context).result).toBe('ServerRetained')
  expect(() => decodeDailyOutcome({ ...result, parentCommandId: null }, context)).toThrow()
  expect(() => decodeDailyOutcome({ ...result, result: 'Committed' }, context)).toThrow()
})
