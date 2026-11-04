import { afterEach, describe, expect, it, vi } from 'vitest'
import { dailyAttempt, dailyApi, decodeDailyCapabilities, decodeDailyPage, decodeDailyRecord } from './daily-operations-api'
import { dailyFields, dailyKinds, emptyDailyFields } from './daily-report-fields'
import { dailyCapsFixture, dailyId, dailyRecordFixture, dailyVessel } from './daily-test-fixtures'
import { dailyLocationPath, readDailyLocation } from './daily-location'
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('T-OPS2-001/002/003 exact owning boundaries (authored; execution deferred)', () => {
  it.each(dailyKinds)('admits only the actual closed %s schema and source-owned states', kind => {
    const caps = dailyCapsFixture(kind), row = dailyRecordFixture(kind)
    expect(decodeDailyCapabilities(caps, dailyVessel, kind).canSubmit).toBe(false)
    expect(decodeDailyRecord(row, dailyVessel, kind).state).toBe('DRAFT')
    for (const change of [{ vesselId: dailyId }, { state: 'READY' }, { reviewState: 'APPROVED' }, { fields: { ...row.fields, safetyApproval: { state: 'Known', value: 'YES' } } }, { fields: { ...row.fields, vessel_id: { state: 'Known', value: dailyId } } }]) expect(() => decodeDailyRecord({ ...row, ...change }, dailyVessel, kind)).toThrow()
    expect(() => decodeDailyCapabilities({ ...caps, schema: [caps.schema[0], ...caps.schema.slice(0, -1)] }, dailyVessel, kind)).toThrow()
    expect(() => decodeDailyCapabilities({ ...caps, canSubmit: true }, dailyVessel, kind)).toThrow()
    expect(() => decodeDailyCapabilities({ ...caps, offline: { ...caps.offline, supported: true, state: 'READY' } }, dailyVessel, kind)).toThrow()
  })
  it('keeps Missing, Unknown and exact numeric zero distinct and excludes source-owned fields from save', () => {
    const fields = emptyDailyFields('Noon'); fields.avg_speed_kn = { state: 'Known', value: '0' }; fields.position_lat = { state: 'Known', value: '-12.123456789012345678901234567890123456' }; fields.rob_summary = { state: 'Unknown', value: null }
    const attempt = dailyAttempt('Noon', dailyVessel, { ...fields, vessel_id: { state: 'Known', value: dailyVessel }, master_review: { state: 'Known', value: 'SUBMITTED' } })
    const body = JSON.parse(attempt.body)
    expect(body).toMatchObject({ expectedVersion: 0, reasonCode: 'INITIAL_AUTHORING', fields: { avg_speed_kn: { state: 'Known', value: '0' }, position_lat: fields.position_lat, rob_summary: fields.rob_summary, fuel_summary: { state: 'Missing', value: null } } })
    expect(Object.keys(body.fields)).toHaveLength(18); expect(body.fields).not.toHaveProperty('vessel_id'); expect(body.fields).not.toHaveProperty('master_review')
    const row = { ...dailyRecordFixture('Noon'), fields: { ...dailyRecordFixture('Noon').fields, ...fields } }
    expect(decodeDailyRecord(row, dailyVessel, 'Noon').fields.position_lat.value).toBe(fields.position_lat.value)
    expect(() => decodeDailyRecord({ ...row, fields: { ...row.fields, avg_speed_kn: { state: 'Unknown', value: '0' } } }, dailyVessel, 'Noon')).toThrow()
  })
  it('binds list/history records to the selected family and exact requested record', () => {
    const row = dailyRecordFixture('Engine'), page = { items: [row], offset: 0, limit: 25, hasMore: false }
    expect(decodeDailyPage(page, dailyVessel, 'Engine', 0, false, dailyId).items).toHaveLength(1)
    expect(() => decodeDailyPage(page, dailyVessel, 'Deck', 0, false, dailyId)).toThrow()
    expect(() => decodeDailyPage(page, dailyVessel, 'Engine', 0, false, dailyVessel)).toThrow()
    expect(() => decodeDailyPage({ ...page, items: [row, row] }, dailyVessel, 'Engine', 0, false, dailyId)).toThrow()
    expect(() => decodeDailyPage({ ...page, offset: 25 }, dailyVessel, 'Engine', 0, false, dailyId)).toThrow()
  })
  it('replays the same frozen key/body and version without rebuilding numeric text', async () => {
    const row = dailyRecordFixture('Engine'), fields = { ...row.fields, fuel_consumption: { state: 'Known' as const, value: '0.123456789012345678901234567890123456' } }, attempt = dailyAttempt('Engine', dailyVessel, fields, row)
    const fetch = vi.fn(async () => new Response(JSON.stringify({ ...row, fields }), { status: 200 })); vi.stubGlobal('fetch', fetch)
    await dailyApi.save(attempt, 'fixture-csrf', new AbortController().signal); await dailyApi.save(attempt, 'fixture-csrf', new AbortController().signal)
    expect(fetch.mock.calls).toHaveLength(2)
    expect(fetch).toHaveBeenCalledWith(`/api/v1/engine-daily-logs/${dailyId}?vesselId=${dailyVessel}`, expect.objectContaining({ method: 'PUT', credentials: 'same-origin', body: attempt.body, headers: expect.objectContaining({ 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'fixture-csrf', 'Idempotency-Key': attempt.key }) }))
    expect(JSON.parse(attempt.body).expectedVersion).toBe(17)
    expect(JSON.parse(attempt.body).fields.fuel_consumption.value).toBe(fields.fuel_consumption.value)
  })
  it('roundtrips only scoped app paths and refuses duplicate, encoded and unknown query inputs', () => {
    for (const kind of dailyKinds) {
      const route = { kind, vessel: dailyVessel, id: dailyId, offset: 25 }
      expect(readDailyLocation(dailyLocationPath(route))).toEqual(route)
    }
    for (const href of [`/app/noon-report/${dailyVessel}?id=${dailyId}&id=${dailyId}`, `/app/noon-report/${dailyVessel}?offset=-1`, `/app/noon-report/${dailyVessel}?vesselId=${dailyId}`, `/app/noon-report/${dailyVessel}%2F${dailyId}`, `/app/noon-report?id=${dailyId}`]) expect(readDailyLocation(href).invalid).toBe(true)
    expect(dailyFields('Engine')).toHaveLength(18)
  })
})
