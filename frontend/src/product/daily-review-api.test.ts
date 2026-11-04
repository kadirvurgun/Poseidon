import { afterEach, describe, expect, it, vi } from 'vitest'
import { dailyReviewApi, decodeDailyReview, decodeDailyReviewCapabilities, decodeDailyReviewPage, freezeDailyReview } from './daily-review-api'
import { dailyActor, dailyId, dailyRecordFixture, dailyVessel } from './daily-test-fixtures'
import { dailyReviewCapsFixture, dailyReviewFixture, reviewAuthorityId } from './daily-review-test-fixtures'
import { dailyKinds } from './daily-report-fields'
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('T-OPS2 human review metadata contract (authored; not executed)', () => {
  it.each(dailyKinds)('keeps %s unavailable by default and rejects client-made authority expansion', kind => {
    expect(decodeDailyReviewCapabilities(dailyReviewCapsFixture(kind), dailyVessel, kind, dailyId).canRecord).toBe(false)
    const configured = dailyReviewCapsFixture(kind, true)
    expect(decodeDailyReviewCapabilities(configured, dailyVessel, kind, dailyId).authorities).toHaveLength(1)
    for (const change of [{ vesselId: dailyId }, { recordId: dailyVessel }, { sourceVersion: 'unapproved' }, { authorities: [] }, { authorities: [{ ...configured.authorities[0], duty: 'Administrator' }] }, { authorities: [{ ...configured.authorities[0], decisions: ['APPROVED'] }] }, { authorities: [configured.authorities[0], configured.authorities[0]] }]) expect(() => decodeDailyReviewCapabilities({ ...configured, ...change }, dailyVessel, kind, dailyId)).toThrow()
  })
  it('does not admit approved/verified states, wrong revisions or changed human/record lineage', () => {
    const event = dailyReviewFixture('Engine')
    expect(decodeDailyReview(event, dailyVessel, 'Engine', dailyId).state).toBe('RECORDED_UNASSESSED')
    for (const change of [{ state: 'VERIFIED' }, { state: 'APPROVED' }, { recordRevision: 0 }, { duty: 'Master' }, { reasonCode: 'RETURNED_FOR_CORRECTION' }, { recordHash: 'wrong' }, { authorityHash: 'wrong' }, { rationale: 'hidden\u0000value' }, { actorId: '00000000-0000-0000-0000-000000000000' }]) expect(() => decodeDailyReview({ ...event, ...change }, dailyVessel, 'Engine', dailyId)).toThrow()
    expect(() => decodeDailyReview(event, dailyVessel, 'Noon', dailyId)).toThrow()
    expect(() => decodeDailyReview(event, dailyVessel, 'Engine', dailyVessel)).toThrow()
  })
  it('captures exact record and review versions without sending client actor or source authority claims', () => {
    const record = dailyRecordFixture('Deck'), caps = { ...dailyReviewCapsFixture('Deck', true), reviewVersion: 19 }, rationale = 'First line\nSecond line\tretained'
    const command = freezeDailyReview({ record, caps, actorId: dailyActor, tenantId: dailyVessel, authorityId: reviewAuthorityId, decision: 'RETURNED', rationale })
    expect(JSON.parse(command.body)).toEqual({ expectedVersion: 17, expectedReviewVersion: 19, reviewedRevision: 1, authorityId: reviewAuthorityId, decision: 'RETURNED', reasonCode: 'RETURNED_FOR_CORRECTION', rationale })
    expect(command.owner).toBe(`${dailyVessel}/${dailyActor}`)
    expect(() => freezeDailyReview({ record: { ...record, revision: 2 }, caps, actorId: dailyActor, tenantId: dailyVessel, authorityId: reviewAuthorityId, decision: 'RETURNED', rationale })).toThrow()
    expect(() => freezeDailyReview({ record, caps: dailyReviewCapsFixture('Deck'), actorId: dailyActor, tenantId: dailyVessel, authorityId: reviewAuthorityId, decision: 'RETURNED', rationale })).toThrow()
  })
  it('preserves old revision history without declaring it current and rejects duplicated events', () => {
    const event = dailyReviewFixture('Noon'), page = { items: [event], offset: 0, limit: 25, hasMore: false }
    expect(decodeDailyReviewPage(page, dailyVessel, 'Noon', dailyId, 0).items[0].recordRevision).toBe(1)
    expect(() => decodeDailyReviewPage({ ...page, items: [event, event] }, dailyVessel, 'Noon', dailyId, 0)).toThrow()
    expect(() => decodeDailyReviewPage(page, dailyVessel, 'Noon', dailyId, 25)).toThrow()
  })
  it('requires a confirmed receipt to match frozen actor, role, appointment, rationale and record hash', async () => {
    const event = dailyReviewFixture('Noon'), command = freezeDailyReview({ record: dailyRecordFixture('Noon'), caps: dailyReviewCapsFixture('Noon', true), actorId: dailyActor, tenantId: dailyVessel, authorityId: reviewAuthorityId, decision: 'REVIEWED', rationale: event.rationale })
    const fetch = vi.fn(async () => new Response(JSON.stringify(event), { status: 200 })); vi.stubGlobal('fetch', fetch)
    await expect(dailyReviewApi.record(command, 'fixture-csrf', new AbortController().signal)).resolves.toMatchObject({ state: 'RECORDED_UNASSESSED' })
    fetch.mockImplementation(async () => new Response(JSON.stringify({ ...event, actorId: dailyId }), { status: 200 }))
    await expect(dailyReviewApi.record(command, 'fixture-csrf', new AbortController().signal)).rejects.toMatchObject({ status: 502 })
    expect(fetch).toHaveBeenCalledWith(`/api/v1/noon-reports/${dailyId}/reviews?vesselId=${dailyVessel}`, expect.objectContaining({ method: 'POST', credentials: 'same-origin', body: command.body, headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'fixture-csrf', 'Idempotency-Key': command.key }) }))
  })
})
