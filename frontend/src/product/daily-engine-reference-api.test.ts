import { describe, expect, it } from 'vitest'
import { decodeCandidatePage, decodeEngineReferenceEvent, decodeEngineReferences, engineReferenceAttempt, engineReferenceSource, owningSources, type EngineReferences } from './daily-engine-reference-api'

const vessel = '30000000-0000-4000-8000-000000000001', record = '30000000-0000-4000-8000-000000000002', wo = '30000000-0000-4000-8000-000000000003', other = '30000000-0000-4000-8000-000000000004'
const pin = { kind: 'WorkOrder' as const, id: wo, revision: 2, version: 9, hash: 'a'.repeat(64), sourceVersion: owningSources.WorkOrder }
const references = (extra: Partial<EngineReferences> = {}): EngineReferences => ({ recordId: record, vesselId: vessel, recordRevision: 1, recordVersion: 5, referenceVersion: 7, sequence: 1, canLink: true,
  sources: [{ kind: 'WorkOrder', state: 'AVAILABLE' }, { kind: 'Defect', state: 'UNAVAILABLE' }],
  items: [{ kind: 'WorkOrder', pin, sequence: 1, state: 'LINKED_CURRENT', qualification: 'REVIEW_REQUIRED', label: 'FICTIONAL-WO-1', href: `/app/work-order-center/${vessel}?id=${wo}`, reason: null }],
  offline: 'ONLINE_ONLY', sourceVersion: engineReferenceSource, ...extra })

describe('DEV-099 / T-OPS2-003 engine log reference contract (authored)', () => {
  it('accepts exact pinned links and keeps every link REVIEW_REQUIRED', () => {
    expect(decodeEngineReferences(references(), vessel, record).items[0].pin?.revision).toBe(2)
    for (const change of [{ qualification: 'VERIFIED' }, { state: 'CLOSED' }, { href: `/app/work-order-center/${other}?id=${wo}` }, { pin: { ...pin, sourceVersion: owningSources.Defect } }, { pin: { ...pin, hash: 'free text' } }, { reason: 'REFERENCE_CHANGED_SINCE_LINK' }])
      expect(() => decodeEngineReferences(references({ items: [{ ...references().items[0], ...change } as never] }), vessel, record)).toThrow()
  })
  it('keeps UNKNOWN links free of identifiers and never converts them to a known state', () => {
    const unknown = { kind: 'WorkOrder' as const, pin: null, sequence: 1, state: 'UNKNOWN' as const, qualification: 'REVIEW_REQUIRED' as const, label: null, href: null, reason: 'REFERENCE_NOT_READABLE' as const }
    expect(decodeEngineReferences(references({ items: [unknown] }), vessel, record).items[0].state).toBe('UNKNOWN')
    expect(() => decodeEngineReferences(references({ items: [{ ...unknown, label: 'leak' }] }), vessel, record)).toThrow()
    expect(() => decodeEngineReferences(references({ items: [{ ...unknown, pin } as never] }), vessel, record)).toThrow()
    expect(() => decodeEngineReferences(references({ items: [{ ...unknown, reason: null } as never] }), vessel, record)).toThrow()
  })
  it('rejects foreign scope, missing offline boundary and fabricated source states', () => {
    for (const change of [{ vesselId: other }, { recordId: other }, { offline: 'SUPPORTED' }, { sources: [] }, { referenceVersion: 0 }, { sourceVersion: 'x' }])
      expect(() => decodeEngineReferences(references(change as never), vessel, record)).toThrow()
  })
  it('decodes only same-kind candidate pages and freezes one exact command per attempt', () => {
    const page = { vesselId: vessel, kind: 'WorkOrder', offset: 0, limit: 25, hasMore: false, items: [{ pin, label: 'FICTIONAL-WO-1', detail: null, provenance: 'UserDeclaredUnverified', savedAt: '2026-11-19T10:00:00Z', qualification: 'REVIEW_REQUIRED', href: `/app/work-order-center/${vessel}?id=${wo}` }] }
    expect(decodeCandidatePage(page, vessel, 'WorkOrder', 0).items).toHaveLength(1)
    expect(() => decodeCandidatePage(page, vessel, 'Defect', 0)).toThrow()
    expect(() => decodeCandidatePage({ ...page, items: [{ ...page.items[0], provenance: 'Verified' }] }, vessel, 'WorkOrder', 0)).toThrow()
    const attempt = engineReferenceAttempt(references(), 'LINK', pin)
    expect(JSON.parse(attempt.body)).toEqual({ expectedRecordVersion: 5, recordRevision: 1, expectedReferenceVersion: 7, operation: 'LINK', target: pin })
    const event = { id: other, recordId: record, vesselId: vessel, sequence: 2, referenceVersion: 8, recordRevision: 1, operation: 'LINK', target: pin, actorId: other, savedAt: '2026-11-19T10:00:00Z', qualification: 'REVIEW_REQUIRED', sourceVersion: engineReferenceSource }
    expect(decodeEngineReferenceEvent(event, attempt).sequence).toBe(2)
    expect(() => decodeEngineReferenceEvent({ ...event, target: { ...pin, revision: 3 } }, attempt)).toThrow()
    expect(() => decodeEngineReferenceEvent({ ...event, operation: 'UNLINK' }, attempt)).toThrow()
  })
})
