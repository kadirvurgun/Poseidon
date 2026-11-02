import { afterEach, expect, it, vi } from 'vitest'
import { emptyTugInputs, tugApi } from './tug-api'

const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const row = () => ({ request: { id, vesselId: vessel, jobNumber: 'FICTIONAL-JOB', inputs: emptyTugInputs() }, assignedTugs: [], availableBollardPull: null,
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
