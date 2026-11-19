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
  // DEV-028 / QA B2: the control list carries the 62.01 state and effective revision; RETIRED has none.
  it('decodes the control list lifecycle and rejects a retired or mismatched pointer', async () => {
    const row = graphRow(); const listed = (lifecycle: unknown) => ({ items: [row], offset: 0, hasMore: false, canWrite: true, lifecycle })
    const entry = (patch: Record<string, unknown> = {}) => ({ id: row.id, state: 'RETIRED', publishedRevision: null, successorPending: false, ...patch })
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => listed([entry()]) })))
    expect((await page(vessel, 'Control', 0, new AbortController().signal)).lifecycle).toEqual([entry()])
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => listed([entry({ state: 'PUBLISHED', publishedRevision: 1 })]) })))
    expect((await page(vessel, 'Control', 0, new AbortController().signal)).lifecycle?.[0].publishedRevision).toBe(1)
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => ({ items: [row], offset: 0, hasMore: false, canWrite: true }) })))
    expect((await page(vessel, 'Control', 0, new AbortController().signal)).lifecycle).toBeNull()
    for (const bad of [[entry({ publishedRevision: 1 })], [entry({ state: 'PUBLISHED' })], [entry({ id: vessel })], [], [entry(), entry()], [entry({ state: 'ACTIVE' })], [{ ...entry(), verification: 'VERIFIED' }]]) {
      vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => listed(bad) })))
      await expect(page(vessel, 'Control', 0, new AbortController().signal)).rejects.toThrow()
    }
    // Only the control head list carries it; evidence and version history pages never do.
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => ({ ...listed([entry()]), items: [{ ...row, kind: 'Evidence' }] }) })))
    await expect(page(vessel, 'Evidence', 0, new AbortController().signal)).rejects.toThrow()
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => listed([entry()]) })))
    await expect(page(vessel, 'Control', 0, new AbortController().signal, row.id)).rejects.toThrow()
  })
  it('binds command outcome to resource, vessel and kind', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => graphRow() })))
    await expect(save(vessel, 'Control', vessel, { body: '{}', key: 'synthetic-key-0001' }, 'synthetic-csrf', new AbortController().signal)).rejects.toThrow()
  })
})
