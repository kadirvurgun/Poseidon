import { afterEach, describe, expect, it, vi } from 'vitest'
import { currentAssignment, decodeCrewRow, history, profile, roster } from './crew-api'

const vessel = '11111111-1111-4111-8111-111111111111', id = '22222222-2222-4222-8222-222222222222'
const row = { id, vesselId: vessel, personId: id, version: 3, revision: 1, positionId: null, rank: null, capacity: null, department: null,
  from: null, to: null, contractEndDate: null, watchGroup: null, assignmentStatus: 'Draft', provenance: 'SyntheticUnverified',
  fullName: null, employeeNo: null, privacyScope: 'Withheld', sourceObservedAt: null, eligibilityState: 'Unknown', workRestState: 'Unknown',
  certificateState: 'Unknown', competenceState: 'Unknown', contactStatus: 'Unavailable', nationality: null, nationalityState: 'Withheld',
  presenceState: 'Draft', presenceReason: 'incomplete_declaration', positionState: 'Missing' }
const json = (v: unknown) => new Response(JSON.stringify(v), { headers: { 'Content-Type': 'application/json' } })
afterEach(() => vi.unstubAllGlobals())
describe('DEV-047 strict roster consumers, authored not run', () => {
  it('retains explicit missing values and fails closed for every omitted property and scalar coercion', () => {
    expect(decodeCrewRow(row, vessel)).toEqual(row)
    for (const key of Object.keys(row)) { const omitted: Record<string, unknown> = { ...row }; delete omitted[key]; expect(() => decodeCrewRow(omitted, vessel)).toThrow() }
    for (const patch of [{ version: '3' }, { revision: true }, { version: 0 }, { version: 4294967296 }, { presenceState: 'Ready' },
      { positionState: 'Verified' }, { from: '2026-02-30' }, { sourceObservedAt: 'yesterday' }, { vesselId: id },
      { eligibilityState: 'ELIGIBLE' },
      { privacyScope: 'Withheld', fullName: 'FICTIONAL hidden value' }, { positionState: 'Stale', rank: 'SYNTHETIC-R' }])
      expect(() => decodeCrewRow({ ...row, ...patch }, vessel)).toThrow()
  })
  it('rejects mismatched profile ownership and malformed list flags rather than casting wire data', async () => {
    const fetcher = vi.fn().mockResolvedValue(json({ items: [row], offset: 0, hasMore: 'false', canWrite: true, canWriteIdentity: false }))
    vi.stubGlobal('fetch', fetcher); await expect(roster(vessel, 0, new AbortController().signal)).rejects.toThrow()
    fetcher.mockResolvedValue(json({ assignment: { ...row, id: vessel }, employmentState: 'Withheld', fitnessState: 'Unknown', identityStatus: 'Withheld', contactStatus: 'Withheld', healthStatus: 'Withheld', personVersion: null }))
    await expect(profile(vessel, id, new AbortController().signal)).rejects.toThrow()
  })
  it('requires ordered complete history with nullable historical policy lineage', async () => {
    const revision = { revision: 1, from: null, to: null, contractEndDate: null, positionId: null, status: 'Draft', provenance: 'SyntheticUnverified',
      savedAt: '2026-11-02T10:00:00Z', sourceObservedAt: null, assignmentPolicyVersion: null, positionPolicyVersion: null }
    const fetcher = vi.fn().mockResolvedValue(json({ items: [revision], historicalIdentity: 'Unavailable', offset: 0, hasMore: false })); vi.stubGlobal('fetch', fetcher)
    expect((await history(vessel, id, new AbortController().signal)).items).toEqual([revision])
    for (const items of [[revision, revision], [{ ...revision, revision: '1' }], [{ ...revision, savedAt: '2026-99-99' }]]) {
      fetcher.mockResolvedValue(json({ items, historicalIdentity: 'Unavailable', offset: 0, hasMore: false })); await expect(history(vessel, id, new AbortController().signal)).rejects.toThrow()
    }
  })
  it('does not turn a stale or ambiguous current-selection response into a named effective assignment', async () => {
    const selection = { state: 'PolicyStale', assignmentId: null, vesselId: null, position: null, policyVersion: 'synthetic-v1',
      referenceDate: '2026-11-02', timeZone: 'UTC', authority: 'UnverifiedDeclaration;NoQualificationOrSafetyDecision' }
    const fetcher = vi.fn().mockResolvedValue(json(selection)); vi.stubGlobal('fetch', fetcher)
    expect((await currentAssignment(vessel, id, new AbortController().signal)).state).toBe('PolicyStale')
    for (const patch of [{ state: 'Ready' }, { state: 'Ambiguous', assignmentId: id }, { referenceDate: '2026-02-30' }, { authority: 'Verified' }]) {
      fetcher.mockResolvedValue(json({ ...selection, ...patch }))
      await expect(currentAssignment(vessel, id, new AbortController().signal)).rejects.toThrow()
    }
  })
})
