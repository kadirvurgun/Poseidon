import { describe, expect, it } from 'vitest'
import { decodeDefectAssetContext, decodeDefectCapabilities, decodeDefectRecord, freezeDefectCommand, validDefectAssetPin } from './defect-api'
import { emptyDefectFields } from './defect-fields'
import { defectActor, defectAssetFixture, defectAssetPinFixture, defectCapsFixture, defectId, defectRecordFixture, defectTenant, defectVessel } from './defect-test-fixtures'
describe('T-S8-004 declaration contracts — sources only', () => {
  it('saves incomplete missing/unknown facts without an approval, actor or guessed source reference', () => {
    const fields = emptyDefectFields(); fields.temporary_measure_expires_at = { state: 'Unknown', value: null }
    const attempt = freezeDefectCommand(defectVessel, defectId, defectTenant + '/' + defectActor, fields, null, null)
    const body = JSON.parse(attempt.body)
    expect(body.fields.temporary_measure).toEqual({ state: 'Missing', value: null }); expect(body.fields.temporary_measure_expires_at).toEqual({ state: 'Unknown', value: null })
    expect(Object.keys(body.fields)).toHaveLength(11); expect(Object.keys(body).sort()).toEqual(['asset', 'expectedVersion', 'fields', 'id', 'sourceVersion'])
    expect(body.expectedVersion).toBe(0); expect(body.asset).toBeNull()
  })
  it('keeps a declared Verify phase unassessed and rejects Closed, Effective or generic verification authority', () => {
    const record = defectRecordFixture(); record.fields.lifecycle_stage = { state: 'Known', value: 'Verify' }
    const decoded = decodeDefectRecord(record, defectVessel)
    expect(decoded.stage).toBe('DECLARATION'); expect(decoded.qualification).toBe('REVIEW_REQUIRED'); expect(decoded.temporal.closureAvailable).toBe(false)
    expect(() => decodeDefectRecord({ ...record, stage: 'Closed' }, defectVessel)).toThrow()
    expect(() => decodeDefectRecord({ ...record, qualification: 'Effective' }, defectVessel)).toThrow()
    expect(() => decodeDefectCapabilities({ ...defectCapsFixture(), canVerify: true }, defectVessel)).toThrow()
    expect(() => decodeDefectCapabilities({ ...defectCapsFixture(), canClose: true }, defectVessel)).toThrow()
  })
  it('does not turn an unknown deadline into not-due or a cleared temporary measure', () => {
    const record = defectRecordFixture(); record.fields.temporary_measure = { state: 'Known', value: 'Unverified temporary declaration' }; record.fields.temporary_measure_expires_at = { state: 'Unknown', value: null }; record.temporal.temporaryMeasureState = 'DEADLINE_UNKNOWN'
    expect(decodeDefectRecord(record, defectVessel).temporal.temporaryMeasureState).toBe('DEADLINE_UNKNOWN')
    expect(() => decodeDefectRecord({ ...record, temporal: { ...record.temporal, temporaryMeasureState: 'NOT_DUE_DECLARED' } }, defectVessel)).toThrow()
  })
  it('allows fresh server temporal enrichment of unchanged immutable receipt facts', () => {
    const record = defectRecordFixture(); record.currentRevision = false; record.canEdit = false
    record.fields.temporary_measure = { state: 'Known', value: 'Declared temporary action' }; record.fields.temporary_measure_expires_at = { state: 'Known', value: '2026-11-06T01:30:00Z' }; record.temporal.temporaryMeasureState = 'NOT_DUE_DECLARED'
    const first = decodeDefectRecord(record, defectVessel), second = decodeDefectRecord({ ...record, temporal: { ...record.temporal, assessedAt: '2026-11-06T02:00:00Z', temporaryMeasureState: 'EXPIRED_DECLARED' } }, defectVessel)
    expect(second.fields).toEqual(first.fields); expect(second.hash).toBe(first.hash); expect(second.version).toBe(first.version)
    expect(second.temporal.temporaryMeasureState).toBe('EXPIRED_DECLARED'); expect(second.temporal.closureAvailable).toBe(false)
  })
  it('requires an actual current asset context, exact captured pin and no private expansion', () => {
    const pin = defectAssetPinFixture(), value = defectAssetFixture()
    expect(decodeDefectAssetContext(value, defectVessel, pin.id, pin.revision, pin).pin).toEqual(pin)
    expect(() => decodeDefectAssetContext({ ...value, current: false }, defectVessel, pin.id, pin.revision)).toThrow()
    expect(() => decodeDefectAssetContext(value, defectVessel, pin.id, pin.revision, { ...pin, version: pin.version + 1 })).toThrow()
    expect(() => decodeDefectAssetContext({ ...value, asset: { ...value.asset, notes: 'private source expansion' } }, defectVessel, pin.id, pin.revision)).toThrow()
    expect(validDefectAssetPin({ ...pin, hash: pin.hash.toLowerCase() })).toBe(false)
  })
})
