import { afterEach, describe, expect, it, vi } from 'vitest'
import { blank, decodeRow, orphans, page, save } from './control-evidence-api'

const vessel = '10000000-0000-4000-8000-000000000001'
export const graphRow = () => ({ id: '10000000-0000-4000-8000-000000000002', vesselId: vessel, kind: 'Control', revision: 1, version: 5, content: blank(), hash: 'A'.repeat(64), lifecycle: 'DRAFT', completion: 'UNKNOWN', verification: 'UNVERIFIED', effectiveness: 'UNKNOWN', timeState: 'UNKNOWN', evidenceExists: false, file: null, requirements: [], links: [], blockers: ['TITLE_MISSING', 'OWNER_MISSING', 'REQUIREMENT_LINK_MISSING', 'FREQUENCY_UNSPECIFIED', 'EVIDENCE_POLICY_MISSING', 'VERIFICATION_POLICY_MISSING', 'CRITICALITY_UNKNOWN', 'SOURCE_ADMISSION_UNAVAILABLE', 'VERIFICATION_NOT_PERFORMED'], savedAt: '2026-11-02T00:00:00Z', asOf: '2026-11-02T00:00:00Z' })
afterEach(() => vi.unstubAllGlobals())
describe('CE strict consumer authored / NOT RUN', () => {
  it('retains unknown rather than interpreting existence as successful verification', () => {
    expect(decodeRow(graphRow(), vessel, 'Control').verification).toBe('UNVERIFIED')
    expect(decodeRow(graphRow(), vessel, 'Control').content.critical).toBeNull()
  })
  it.each([
    { verification: 'VERIFIED' }, { effectiveness: 'EFFECTIVE' }, { completion: 'COMPLETED' }, { lifecycle: 'ACTIVE' },
    { actorContext: {} }, { checksum: 'private' }, { evidenceExists: true }, { version: 0 }, { version: 4294967296 },
    { vesselId: '10000000-0000-4000-8000-000000000099' }, { revision: Number.MAX_SAFE_INTEGER + 1 },
    { savedAt: '2026-02-30T00:00:00Z' }, { timeState: 'FRESH' }, { blockers: [] },
    { file: { id: vessel, revision: 1, checksum: 'A'.repeat(64) } },
    { content: { ...blank(), critical: 'false' } }, { content: { ...blank(), ownerId: vessel } },
    { content: { ...blank(), freshUntil: '2026-11-02T00:00:00Z\n' } },
  ])('rejects authority expansion, leak, coercion or invalid lineage %j', patch => {
    expect(() => decodeRow({ ...graphRow(), ...patch }, vessel, 'Control')).toThrow()
  })
  it('does not accept malformed scoped empty pages or all-clear authority', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => ({ items: [], offset: 25, hasMore: false, canWrite: true }) })))
    await expect(page(vessel, 'Control', 0, new AbortController().signal)).rejects.toThrow()
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => ({ available: true, reason: 'AUTHORITATIVE_APPLICABILITY_UNAVAILABLE', draftCriticalOrphans: [], offset: 0, hasMore: false }) })))
    await expect(orphans(vessel, 0, new AbortController().signal)).rejects.toThrow()
  })
  it('binds command outcome to resource, vessel and kind', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => graphRow() })))
    await expect(save(vessel, 'Control', vessel, { body: '{}', key: 'synthetic-key-0001' }, 'synthetic-csrf', new AbortController().signal)).rejects.toThrow()
  })
})
