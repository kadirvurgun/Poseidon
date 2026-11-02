import { describe, expect, it } from 'vitest'
import { decodeReview, decodeTrace } from './human-review-api'

import { reviewAnchor, reviewRow, reviewTrace, recordedRow } from './human-review.test-fixtures'
const actor = reviewRow().actorId
describe('human trace closed wire boundary authored NOT RUN', () => {
  it('retains pending and unavailable instead of inferred success', () => {
    expect(decodeTrace(reviewTrace(), reviewAnchor, 0).complete).toBe(false)
    expect(decodeReview(reviewRow(), reviewAnchor).state).toBe('PENDING')
  })
  it.each([{ state: 'APPROVED' }, { currentState: 'READY' }, { actorContext: {} }, { signatoryName: 'fictional' }, { signature: 'fabricated' }, { evidenceRevision: 2 }, { vesselId: actor }, { version: 4294967296 }, { revision: Number.MAX_SAFE_INTEGER + 1 }, { state: 'VERIFIED', validUntil: null }, { kind: 'Decision' }, { savedAt: '2026-02-30T00:00:00Z' }, { rationale: 'forged\nline' }])('rejects authority expansion or malformed event %j', patch => {
    expect(() => decodeReview({ ...reviewRow(), ...patch }, reviewAnchor)).toThrow()
  })
  it.each([{ complete: true }, { disposition: 'READY' }, { blockers: [] }, { hasMore: true }, { offset: 25 }, { historical: true }, { authorities: [{ clientRole: 'Master' }] }, { privateEvidenceCount: 0 }, { segments: [{ kind: 'Evidence', availability: 'Unavailable', id: actor, revision: 1, state: 'UNKNOWN', why: 'REVISION_UNAVAILABLE', hash: null, savedAt: null }] }])('rejects fabricated completeness, scope inference or invalid authority %j', patch => {
    expect(() => decodeTrace({ ...reviewTrace(), ...patch }, reviewAnchor, 0)).toThrow()
  })
})

describe('authorized human recording wire boundary, NOT RUN', () => {
  it('accepts human text while retaining pending qualification and rejects fabricated effects', () => {
    expect(decodeReview(recordedRow(), reviewAnchor).recording?.qualification).toBe('UNASSESSED')
    for (const patch of [{ qualification: 'VERIFIED' }, { signature: 'fake' }, { profileRevision: 0 }]) {
      expect(() => decodeReview({ ...recordedRow(), recording: { ...recordedRow().recording, ...patch } }, reviewAnchor)).toThrow()
    }
    expect(() => decodeReview({ ...recordedRow(), recording: null }, reviewAnchor)).toThrow()
    expect(() => decodeReview({ ...recordedRow(), currentState: 'Effective' }, reviewAnchor)).toThrow()
    expect(() => decodeReview({ ...recordedRow(), recording: { ...recordedRow().recording, policy: { ...recordedRow().recording.policy, authorityRole: 'Admin' } } }, reviewAnchor)).toThrow()
    expect(() => decodeReview({ ...recordedRow(), recording: { ...recordedRow().recording, content: { ...recordedRow().recording.content, subjectId: actor } } }, reviewAnchor)).toThrow()
  })
})
