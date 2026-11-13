import { afterEach, describe, expect, it, vi } from 'vitest'
import { decodeReview, decodeTrace, loadSession, save } from './human-review-api'

import { reviewAnchor, reviewRow, reviewTrace, recordedRow, reviewSession } from './human-review.test-fixtures'
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
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

describe('HRO01/03 fresh session and exact non-recording receipts, authored NOT RUN', () => {
  const response = (value: unknown) => ({ ok: true, status: 200, json: async () => value }) as Response
  const command = () => ({ expectedVersion: 0, evidenceId: reviewAnchor.evidenceId, evidenceRevision: reviewAnchor.evidenceRevision,
    evidenceVersion: 2, controlId: reviewAnchor.controlId, controlRevision: reviewAnchor.controlRevision, controlVersion: 3,
    expectedProfileRevision: null, recording: null,
    ...Object.fromEntries(['state', 'reasonCode', 'rationale', 'authorityId', 'operationId', 'verificationId', 'verificationRevision', 'evaluationId'].map(key => [key, reviewRow()[key as keyof ReturnType<typeof reviewRow>]])) })
  it('uses the exact serialized command/key/CSRF and accepts its unpromoted PENDING receipt', async () => {
    const fetch = vi.fn(async (_url: string, _init?: RequestInit) => response(reviewRow())); vi.stubGlobal('fetch', fetch)
    const body = JSON.stringify(command()), key = crypto.randomUUID(), csrf = crypto.randomUUID()
    const row = await save(reviewAnchor, { kind: 'Verification', id: null, body, key }, csrf, new AbortController().signal)
    expect(row.state).toBe('PENDING')
    expect(fetch.mock.calls[0][1]?.body).toBe(body)
    expect(fetch.mock.calls[0][1]?.headers).toMatchObject({ 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key })
  })
  it.each(['rationale', 'authorityId', 'operationId', 'evaluationId', 'reasonCode', 'state'] as const)('rejects a structurally valid PENDING receipt with altered %s', async field => {
    const row = { ...reviewRow(), [field]: field === 'rationale' ? 'Different valid private reason' : field === 'reasonCode' ? 'HUMAN_REJECTED' : field === 'state' ? 'REJECTED' : reviewAnchor.controlId }
    if (field === 'state') row.currentState = 'REJECTED'
    vi.stubGlobal('fetch', vi.fn(async () => response(row)))
    await expect(save(reviewAnchor, { kind: 'Verification', id: null, body: JSON.stringify(command()), key: crypto.randomUUID() }, crypto.randomUUID(), new AbortController().signal)).rejects.toMatchObject({ status: 502 })
  })
  it('binds PENDING decision verification lineage before declaring success', async () => {
    const row = { ...reviewRow(), kind: 'Decision', verificationId: reviewAnchor.controlId, verificationRevision: 2 }
    const body = JSON.stringify({ ...command(), verificationId: reviewRow().id, verificationRevision: 1 })
    vi.stubGlobal('fetch', vi.fn(async () => response(row)))
    await expect(save(reviewAnchor, { kind: 'Decision', id: null, body, key: crypto.randomUUID() }, crypto.randomUUID(), new AbortController().signal)).rejects.toMatchObject({ status: 502 })
  })
  it('requires authenticated readable canonical owner and fresh bounded CSRF, never a caller role', async () => {
    const user = reviewSession(); vi.stubGlobal('fetch', vi.fn(async () => response(user)))
    expect(await loadSession(new AbortController().signal)).toMatchObject({ owner: `${user.tenantId}/${user.actorId}`, csrf: user.csrfToken })
    for (const patch of [{ authenticated: false }, { setupRequired: true }, { canRead: false }, { actorId: user.actorId+'\n' }, { tenantId: null }, { csrfToken: '' }, { csrfToken: crypto.randomUUID()+'\n' }]) {
      vi.stubGlobal('fetch', vi.fn(async () => response({ ...user, ...patch })))
      await expect(loadSession(new AbortController().signal)).rejects.toBeInstanceOf(Error)
    }
  })
})
