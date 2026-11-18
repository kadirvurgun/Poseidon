import { dailySourceVersion } from '../daily-operations-api'
import type { DailyOfflineContext, DailyCapture } from './daily-offline-contract'
export const offlineDailyVessel = '11111111-1111-4111-8111-111111111111', offlineDailyRecord = '22222222-2222-4222-8222-222222222222'
export const offlineDailyCommand = '33333333-3333-4333-8333-333333333333'
export const dailyOfflineFixture = (): DailyOfflineContext => ({ id: '44444444-4444-4444-8444-444444444444', tenantId: offlineDailyVessel, actorId: offlineDailyRecord,
  vesselId: offlineDailyVessel, kind: 'Noon', recordId: offlineDailyRecord, deviceId: '55555555-5555-4555-8555-555555555555', keyId: '66666666-6666-4666-8666-666666666666',
  challenge: 'A'.repeat(43) + '=', policyVersion: 'fictional-unapproved-classification-v1', policyDigest: 'A'.repeat(64), authorityDigest: 'B'.repeat(64), sourceVersion: dailySourceVersion, schemaHash: 'C'.repeat(64),
  operation: 'daily-operations.noon-draft.v1', allowedFields: ['position_lat'], baseVersion: 0, baseRevision: 0, baseFields: { position_lat: { state: 'Unknown', value: null } },
  serverTime: '2026-11-05T00:00:00Z', expiresAt: '2026-11-05T01:00:00Z', retainUntil: '2026-11-05T02:00:00Z', maxRecords: 8, maxEncryptedBytes: 131072, maxDeviceRecords: 16, maxDeviceEncryptedBytes: 1048576 })
export const dailyCaptureFixture = (): DailyCapture => { const c = dailyOfflineFixture(); return { commandId: offlineDailyCommand, kind: c.kind, recordId: c.recordId, keyId: c.keyId, deviceId: c.deviceId,
  policyVersion: c.policyVersion, policyDigest: c.policyDigest, sourceVersion: c.sourceVersion, schemaHash: c.schemaHash, operation: c.operation, baseVersion: c.baseVersion,
  capturedAt: '2026-11-05T00:01:00Z', offsetMinutes: 180, fields: { position_lat: { state: 'Known', value: '-0.000' } } } }
