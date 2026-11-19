import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ControlLifecycle } from './ControlLifecycle'
import { blank, decodeRow, lifecycle, orphans, supportingEvidence, transition, type Row } from './control-evidence-api'

// DEV-028 lifecycle and DEV-029 supporting-evidence consumers. Mocked transport: UI behavior only.
const vessel = '10000000-0000-4000-8000-000000000001', control = '10000000-0000-4000-8000-000000000002', evidence = '10000000-0000-4000-8000-000000000003'
const actor = '10000000-0000-4000-8000-000000000004'
const controlRow = (): Row => decodeRow({ id: control, vesselId: vessel, kind: 'Control', revision: 2, version: 7, content: blank(), hash: 'A'.repeat(64), lifecycle: 'DRAFT', completion: 'UNKNOWN', verification: 'UNVERIFIED', effectiveness: 'UNKNOWN', timeState: 'UNKNOWN', evidenceExists: false, file: null, requirements: [], links: [], blockers: ['TITLE_MISSING', 'OWNER_MISSING', 'REQUIREMENT_LINK_MISSING', 'FREQUENCY_UNSPECIFIED', 'EVIDENCE_POLICY_MISSING', 'VERIFICATION_POLICY_MISSING', 'CRITICALITY_UNKNOWN'], savedAt: '2026-11-19T00:00:00Z', asOf: '2026-11-19T00:00:00Z' }, vessel, 'Control')
const evidenceRow = (timeState: string, pinned: number) => ({ id: evidence, vesselId: vessel, kind: 'Evidence', revision: 1, version: 3, content: { ...blank(), title: 'FICTIONAL evidence', provenance: 'Synthetic' }, hash: 'B'.repeat(64), lifecycle: 'DRAFT', completion: 'UNKNOWN', verification: 'UNVERIFIED', effectiveness: 'UNKNOWN', timeState, evidenceExists: true, file: { id: evidence, revision: 1, checksum: 'C'.repeat(64) }, requirements: [], links: [{ kind: 'Control', id: control, revision: pinned, expectedVersion: 7 }], blockers: ['OWNER_MISSING', 'REQUIREMENT_LINK_MISSING', 'OBSERVATION_TIME_MISSING'], savedAt: '2026-11-19T00:00:00Z', asOf: '2026-11-19T00:00:00Z' })
const state = (value: string, overrides: Record<string, unknown> = {}) => ({ id: control, vesselId: vessel, revision: 2, version: 7, state: value, effectiveness: 'UNKNOWN', events: [], offset: 0, hasMore: false, canWrite: true, canReview: false, ...overrides })
const ok = (value: unknown) => ({ ok: true, status: 200, json: async () => value }) as Response
const fail = (status: number, code: string) => ({ ok: false, status, json: async () => ({ code }) }) as Response
const labels = (s: string) => s
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('DEV-028/029 lifecycle consumer', () => {
  it('loads only on demand, shows the declared stale state and pins to an earlier revision', async () => {
    const fetch = vi.fn(async (url: string) => url.includes('/lifecycle') ? ok(state('DRAFT')) : ok({ controlId: control, vesselId: vessel, controlRevision: 2, items: [{ evidence: evidenceRow('DECLARED_STALE', 1), pinnedControlRevision: 1, pinCurrent: false }], offset: 0, hasMore: false }))
    vi.stubGlobal('fetch', fetch)
    render(<ControlLifecycle vessel={vessel} control={controlRow()} language="en" csrf="synthetic-csrf" offline={false} locked={false} onChanged={vi.fn()} timeLabel={labels}/>)
    expect(fetch).not.toHaveBeenCalled()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Open control lifecycle' }))
    expect(await screen.findByText('Draft')).toBeTruthy()
    expect(await screen.findByText('DECLARED_STALE')).toBeTruthy()
    expect(screen.getByText(/Pinned to an earlier control revision/)).toBeTruthy()
    expect(screen.getByText(/not verification, completion, effectiveness/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Approve for implementation' })).toBeNull()
  })

  it('requires a reason, keeps the same key for an unresolved retry and reloads after acceptance', async () => {
    const posts: RequestInit[] = []; let attempt = 0; const changed = vi.fn()
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        posts.push(init)
        if (++attempt === 1) throw new TypeError('network')
        return ok({ id: control, vesselId: vessel, revision: 2, version: 8, state: 'IN_REVIEW', effectiveness: 'UNKNOWN', event: { sequence: 1, controlRevision: 2, fromState: 'DRAFT', toState: 'IN_REVIEW', reasonCode: 'SUBMIT_FOR_REVIEW', actorId: actor, rationale: 'FICTIONAL reason', savedAt: '2026-11-19T00:00:01Z' } })
      }
      return url.includes('/lifecycle') ? ok(state(attempt > 1 ? 'IN_REVIEW' : 'DRAFT')) : ok({ controlId: control, vesselId: vessel, controlRevision: 2, items: [], offset: 0, hasMore: false })
    }))
    const user = userEvent.setup()
    render(<ControlLifecycle vessel={vessel} control={controlRow()} language="en" csrf="synthetic-csrf" offline={false} locked={false} onChanged={changed} timeLabel={labels}/>)
    await user.click(screen.getByRole('button', { name: 'Open control lifecycle' }))
    const submit = await screen.findByRole('button', { name: 'Submit for review' })
    expect((submit as HTMLButtonElement).disabled).toBe(true)
    await user.type(screen.getByLabelText('Reason for this change'), 'FICTIONAL reason')
    await user.click(submit)
    expect(await screen.findByRole('alert')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Submit for review' }))
    await screen.findByText('Lifecycle change recorded')
    expect(posts).toHaveLength(2)
    const keys = posts.map(p => (p.headers as Record<string, string>)['Idempotency-Key'])
    expect(keys[0]).toBe(keys[1])
    expect(JSON.parse(posts[0].body as string)).toEqual({ expectedVersion: 7, revision: 2, reasonCode: 'SUBMIT_FOR_REVIEW', rationale: 'FICTIONAL reason' })
    expect(changed).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(screen.getByText('In review')).toBeTruthy())
  })

  it('offers review actions only to a reviewer and never offers anything for a retired control', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => url.includes('/lifecycle') ? ok(state('IN_REVIEW', { canReview: true })) : ok({ controlId: control, vesselId: vessel, controlRevision: 2, items: [], offset: 0, hasMore: false })))
    const user = userEvent.setup()
    const { unmount } = render(<ControlLifecycle vessel={vessel} control={controlRow()} language="en" csrf="synthetic-csrf" offline={false} locked={false} onChanged={vi.fn()} timeLabel={labels}/>)
    await user.click(screen.getByRole('button', { name: 'Open control lifecycle' }))
    expect(await screen.findByRole('button', { name: 'Approve for implementation' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Return for correction' })).toBeTruthy()
    unmount()
    vi.stubGlobal('fetch', vi.fn(async (url: string) => url.includes('/lifecycle') ? ok(state('RETIRED', { canReview: true })) : ok({ controlId: control, vesselId: vessel, controlRevision: 2, items: [], offset: 0, hasMore: false })))
    render(<ControlLifecycle vessel={vessel} control={controlRow()} language="tr" csrf="synthetic-csrf" offline={false} locked={false} onChanged={vi.fn()} timeLabel={labels}/>)
    await user.click(screen.getByRole('button', { name: 'Kontrol yaşam döngüsünü aç' }))
    expect(await screen.findByText('Kullanımdan kaldırıldı')).toBeTruthy()
    expect(screen.queryByLabelText('Bu değişikliğin gerekçesi')).toBeNull()
  })

  it('shows a server refusal without claiming the change', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => init?.method === 'POST' ? fail(409, 'separation_of_duties')
      : url.includes('/lifecycle') ? ok(state('IN_REVIEW', { canReview: true })) : ok({ controlId: control, vesselId: vessel, controlRevision: 2, items: [], offset: 0, hasMore: false })))
    const user = userEvent.setup()
    render(<ControlLifecycle vessel={vessel} control={controlRow()} language="en" csrf="synthetic-csrf" offline={false} locked={false} onChanged={vi.fn()} timeLabel={labels}/>)
    await user.click(screen.getByRole('button', { name: 'Open control lifecycle' }))
    await user.type(await screen.findByLabelText('Reason for this change'), 'FICTIONAL approval')
    await user.click(screen.getByRole('button', { name: 'Approve for implementation' }))
    expect(await screen.findByText(/was not accepted/)).toBeTruthy()
    expect(screen.queryByText('Lifecycle change recorded')).toBeNull()
  })
})

describe('DEV-028/029 strict decoders', () => {
  it('rejects lifecycle authority expansion, unknown states and an evidence link that does not pin the control', async () => {
    for (const patch of [{ state: 'ACTIVE' }, { effectiveness: 'EFFECTIVE' }, { verification: 'VERIFIED' }, { id: evidence }]) {
      vi.stubGlobal('fetch', vi.fn(async () => ok(state('DRAFT', patch))))
      await expect(lifecycle(vessel, control, 0, new AbortController().signal)).rejects.toThrow()
    }
    vi.stubGlobal('fetch', vi.fn(async () => ok({ controlId: control, vesselId: vessel, controlRevision: 2, items: [{ evidence: { ...evidenceRow('UNKNOWN', 2), links: [] }, pinnedControlRevision: 2, pinCurrent: true }], offset: 0, hasMore: false })))
    await expect(supportingEvidence(vessel, control, 0, new AbortController().signal)).rejects.toThrow()
    vi.stubGlobal('fetch', vi.fn(async () => ok({ controlId: control, vesselId: vessel, controlRevision: 2, items: [{ evidence: evidenceRow('UNKNOWN', 1), pinnedControlRevision: 1, pinCurrent: true }], offset: 0, hasMore: false })))
    await expect(supportingEvidence(vessel, control, 0, new AbortController().signal)).rejects.toThrow()
  })

  it('binds a transition outcome to the sent reason and revision', async () => {
    const body = JSON.stringify({ expectedVersion: 7, revision: 2, reasonCode: 'PUBLISH', rationale: 'FICTIONAL' })
    vi.stubGlobal('fetch', vi.fn(async () => ok({ id: control, vesselId: vessel, revision: 2, version: 8, state: 'RETIRED', effectiveness: 'UNKNOWN', event: { sequence: 3, controlRevision: 2, fromState: 'PUBLISHED', toState: 'RETIRED', reasonCode: 'RETIRE', actorId: actor, rationale: null, savedAt: '2026-11-19T00:00:01Z' } })))
    await expect(transition(vessel, control, { body, key: 'k'.repeat(16) }, 'csrf', new AbortController().signal)).rejects.toThrow()
  })

  it('keeps critical orphan reasons closed and an omitted list distinct from no findings', async () => {
    const base = { available: false, reason: 'AUTHORITATIVE_APPLICABILITY_UNAVAILABLE', draftCriticalOrphans: [], uncontrolledObligations: [], offset: 0, hasMore: false }
    vi.stubGlobal('fetch', vi.fn(async () => ok(base)))
    expect((await orphans(vessel, 0, new AbortController().signal)).criticalOrphans).toBeNull()
    vi.stubGlobal('fetch', vi.fn(async () => ok({ ...base, criticalOrphans: [{ id: control, revision: 2, controlState: 'DRAFT', reasons: ['CRITICALITY_UNKNOWN', 'OWNER_MISSING'] }] })))
    expect((await orphans(vessel, 0, new AbortController().signal)).criticalOrphans).toEqual([{ id: control, revision: 2, controlState: 'DRAFT', reasons: ['CRITICALITY_UNKNOWN', 'OWNER_MISSING'] }])
    for (const item of [{ id: control, revision: 2, controlState: 'DRAFT', reasons: [] }, { id: control, revision: 2, controlState: 'RETIRED', reasons: ['OWNER_MISSING'] }, { id: control, revision: 2, controlState: 'DRAFT', reasons: ['ALL_CLEAR'] }]) {
      vi.stubGlobal('fetch', vi.fn(async () => ok({ ...base, criticalOrphans: [item] })))
      await expect(orphans(vessel, 0, new AbortController().signal)).rejects.toThrow()
    }
  })
})
