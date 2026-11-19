import { afterEach, expect, it, vi } from 'vitest'
import { emptyTugInputs, tugApi, tugRequestSource } from './tug-api'

const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const row = () => ({ request: { id, vesselId: vessel, jobNumber: 'FICTIONAL-JOB', revision: 1, version: 4, inputs: emptyTugInputs(), provenance: 'SyntheticUnverified', savedAt: '2026-11-01T00:00:00Z', missing: ['requested_start'], replayed: false, sourceVersion: tugRequestSource, state: 'Draft' }, assignedTugs: [], availableBollardPull: null,
  readiness: 'Unknown', conflict: 'Unknown', declaredComparisons: [], blockingReasons: [], detailHref: `/app/tug-job-detail/${id}?vessel=${vessel}` })
const load = (items: unknown[]) => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ items, hasMore: false }), { headers: { 'Content-Type': 'application/json' } })))
  return tugApi.board(vessel, '', '', 0, new AbortController().signal)
}
afterEach(() => vi.unstubAllGlobals())

it('retains a scoped unknown observation without a fake readiness conclusion', async () => {
  const result = await load([row()])
  expect(result.items[0].readiness).toBe('Unknown')
  expect(result.items[0].availableBollardPull).toBeNull()
})

it.each(['https://foreign.invalid/app', 'javascript:alert(1)', `/app/tug-job-detail/${id}?vessel=${id}`, `/app/tug-job-detail/${id}?vessel=${vessel}&vessel=${id}`])('rejects an unpinned job destination: %s', async detailHref => {
  await expect(load([{ ...row(), detailHref }])).rejects.toMatchObject({ status: 502, code: 'invalid_tug_board_contract' })
})

it('rejects duplicate jobs and repeated or invalid assignment references before exposing the board', async () => {
  await expect(load([row(), row()])).rejects.toMatchObject({ status: 502 })
  await expect(load([{ ...row(), assignedTugs: [vessel, vessel] }])).rejects.toMatchObject({ status: 502 })
  await expect(load([{ ...row(), assignedTugs: ['not-a-guid'] }])).rejects.toMatchObject({ status: 502 })
})

it('rejects malformed explanation fields rather than losing the page in the renderer', async () => {
  await expect(load([{ ...row(), declaredComparisons: [null] }])).rejects.toMatchObject({ status: 502 })
  await expect(load([{ ...row(), blockingReasons: [null] }])).rejects.toMatchObject({ status: 502 })
})

// DEV-082: the client sends closed filter codes and rejects rows outside the requested state set.
it('sends state/window filters and rejects cancelled rows on the default active board or rows of another state', async () => {
  const cancelled = () => ({ ...row(), request: { ...row().request, state: 'Cancelled' } })
  await expect(load([cancelled()])).rejects.toMatchObject({ status: 502, code: 'invalid_tug_board_contract' })
  const fetcher = vi.fn(async () => new Response(JSON.stringify({ items: [cancelled()], hasMore: false }), { headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetcher)
  const result = await tugApi.board(vessel, '', '', 0, new AbortController().signal, 'Cancelled', 'next24h')
  expect(result.items[0].request.state).toBe('Cancelled')
  expect(String((fetcher.mock.calls[0] as unknown[])[0])).toContain('state=Cancelled&window=next24h')
  await expect(tugApi.board(vessel, '', '', 0, new AbortController().signal, 'Submitted')).rejects.toMatchObject({ status: 502 })
})
