import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DailyReviewPanel, type DailyReviewPanelProps } from './DailyReviewPanel'
import { navigationTestIdentity } from './workspace-navigation.test-fixture'
import { dailyActor, dailyId, dailyRecordFixture, dailyVessel } from './daily-test-fixtures'
import { dailyReviewCapsFixture, dailyReviewFixture, reviewAuthorityId } from './daily-review-test-fixtures'
import type { DailyReview } from './daily-review-api'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const userIdentity = (actor = dailyActor) => ({ ...navigationTestIdentity, authenticated: true, setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false, canRead: true, actorId: actor, tenantId: dailyVessel, csrfToken: 'synthetic-review-csrf' })
const props = (): DailyReviewPanelProps => ({ owner: `${dailyVessel}/${dailyActor}`, session: userIdentity(), record: dailyRecordFixture('Noon'), language: 'en', enabled: true, onDirty: vi.fn(), onSaving: vi.fn(), onCommitted: vi.fn(), onReloadRecord: vi.fn() })
function transport(configured = true) {
  const state = { actor: dailyActor, caps: dailyReviewCapsFixture('Noon', configured), history: [] as DailyReview[], readFailed: false, write: undefined as ((init: RequestInit) => Promise<Response>) | undefined }
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return json(userIdentity(state.actor))
    if (init?.method === 'POST') {
      if (state.write) return state.write(init)
      const body = JSON.parse(init.body as string), event = { ...dailyReviewFixture('Noon'), rationale: body.rationale }
      state.history = [event]; state.caps.reviewVersion = event.version
      return json(event)
    }
    if (state.readFailed) return json({}, 503)
    if (url.includes('/review-capabilities?')) return json(state.caps)
    return json({ items: state.history, offset: 0, limit: 25, hasMore: false })
  })
  vi.stubGlobal('fetch', fetch); return { state, fetch }
}
async function enterReview(rationale = 'Synthetic review rationale') {
  const user = userEvent.setup()
  await user.selectOptions(await screen.findByLabelText('Authorized human appointment'), reviewAuthorityId)
  await user.selectOptions(screen.getByLabelText('Review entry'), 'REVIEWED')
  await user.type(screen.getByLabelText('Review rationale'), rationale)
  return user
}
beforeEach(() => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('T-OPS2 real review metadata interaction sources (authored; not executed)', () => {
  it('shows default absence of qualified appointment without a fake approval or review action', async () => {
    const { fetch } = transport(false); render(<DailyReviewPanel {...props()} />)
    expect(await screen.findByText('No current source-authorized human appointment is available for this record.')).toBeVisible()
    expect(screen.queryByRole('form')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Record review entry' })).not.toBeInTheDocument()
    expect(screen.getByText(/does not establish verification, submission/)).toBeVisible()
    expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(0)
  })
  it('records an explicit appointed review against exact saved record and review versions with no automatic write', async () => {
    const { fetch } = transport(), p = props(); render(<DailyReviewPanel {...p} />)
    await screen.findByLabelText('Authorized human appointment')
    expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
    const user = await enterReview('Line one\nLine two')
    await user.click(screen.getByRole('button', { name: 'Record review entry' }))
    expect(await screen.findByText('Human review recorded · unassessed. Current operational approval remains separate.')).toBeVisible()
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'POST')!, body = JSON.parse(call[1]!.body as string)
    expect(body).toEqual({ expectedVersion: 17, expectedReviewVersion: 0, reviewedRevision: 1, authorityId: reviewAuthorityId, decision: 'REVIEWED', reasonCode: 'REVIEW_RECORDED', rationale: 'Line one\nLine two' })
    expect(call[1]).toMatchObject({ credentials: 'same-origin', headers: { 'X-CSRF-TOKEN': 'synthetic-review-csrf', 'Idempotency-Key': expect.any(String) } })
    expect(p.onCommitted).toHaveBeenCalledTimes(1); expect(p.onDirty).toHaveBeenLastCalledWith(false); expect(p.onSaving).toHaveBeenLastCalledWith(false)
  })
  it('preserves the original unknown command across a later403 and only retries explicitly with the same key/body', async () => {
    const { state, fetch } = transport(), p = props(); let writes = 0
    state.write = async init => { writes++; if (writes === 1) return json({ code: 'command_outcome_withheld' }, 503); if (writes === 2) return json({ code: 'human_authority_required' }, 403); return json({ ...dailyReviewFixture('Noon'), rationale: JSON.parse(init.body as string).rationale }) }
    render(<DailyReviewPanel {...p} />); const user = await enterReview()
    await user.click(screen.getByRole('button', { name: 'Record review entry' })); await screen.findByText(/Review outcome unknown/)
    expect(screen.getByLabelText('Review rationale')).toBeDisabled(); expect(p.onSaving).toHaveBeenLastCalledWith(true)
    await user.click(screen.getByRole('button', { name: 'Retry exact review command' }))
    await waitFor(() => expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(2))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Retry exact review command' })).toBeEnabled())
    expect(p.onSaving).toHaveBeenLastCalledWith(true)
    await user.click(screen.getByRole('button', { name: 'Retry exact review command' }))
    await screen.findByText('Human review recorded · unassessed. Current operational approval remains separate.')
    const calls = fetch.mock.calls.filter(([, init]) => init?.method === 'POST')
    expect(calls).toHaveLength(3); expect(calls.every(call => call[1]?.body === calls[0][1]?.body)).toBe(true); expect(calls.every(call => JSON.stringify(call[1]?.headers) === JSON.stringify(calls[0][1]?.headers))).toBe(true)
  })
  it('hides a retained private review under a different identity and restores it only after original identity reauthorization', async () => {
    const { state, fetch } = transport(), p = props(); state.write = async () => json({ code: 'command_outcome_withheld' }, 503)
    const view = render(<DailyReviewPanel {...p} />), user = await enterReview('PRIVATE original review')
    await user.click(screen.getByRole('button', { name: 'Record review entry' })); await screen.findByText(/Review outcome unknown/)
    state.actor = dailyId; view.rerender(<DailyReviewPanel {...p} session={userIdentity(dailyId)} />)
    expect(screen.queryByDisplayValue('PRIVATE original review')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Retry exact review command' })).not.toBeInTheDocument()
    expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
    state.actor = dailyActor; view.rerender(<DailyReviewPanel {...p} session={userIdentity()} />)
    expect(screen.queryByDisplayValue('PRIVATE original review')).not.toBeInTheDocument()
    expect(await screen.findByDisplayValue('PRIVATE original review')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Retry exact review command' })).toBeEnabled()
    expect(p.onCommitted).not.toHaveBeenCalled()
  })
  it('keeps a late confirmed response uncertain if the committed identity boundary changed before it arrived', async () => {
    const { state } = transport(), p = props(); let complete: ((value: Response) => void) | undefined
    state.write = async () => new Promise<Response>(resolve => { complete = resolve })
    const view = render(<DailyReviewPanel {...p} />), user = await enterReview('PRIVATE late response rationale')
    await user.click(screen.getByRole('button', { name: 'Record review entry' })); await waitFor(() => expect(complete).toBeDefined())
    state.actor = dailyId; view.rerender(<DailyReviewPanel {...p} session={userIdentity(dailyId)} />)
    await act(async () => { complete!(json({ ...dailyReviewFixture('Noon'), rationale: 'PRIVATE late response rationale' })) })
    expect(p.onCommitted).not.toHaveBeenCalled(); expect(p.onSaving).toHaveBeenLastCalledWith(true)
    expect(screen.queryByDisplayValue('PRIVATE late response rationale')).not.toBeInTheDocument()
    state.actor = dailyActor; view.rerender(<DailyReviewPanel {...p} session={userIdentity()} />)
    expect(await screen.findByText(/Review outcome unknown/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Retry exact review command' })).toBeEnabled()
  })
  it('keeps historical review distinct from current saved revision and locks new recording while ordinary edits are pending', async () => {
    const { state, fetch } = transport(), p = props(); state.history = [dailyReviewFixture('Noon')]; state.caps = { ...state.caps, recordRevision: 2, recordVersion: 18 }
    render(<DailyReviewPanel {...p} record={{ ...p.record, revision: 2, version: 18 }} enabled={false} />)
    await screen.findByLabelText('Authorized human appointment')
    expect(screen.getByLabelText('Review rationale')).toBeDisabled(); expect(screen.getByRole('button', { name: 'Record review entry' })).toBeDisabled()
    const history = screen.getByRole('region', { name: 'Immutable review history' })
    await userEvent.setup().click(within(history).getByText(/Revision 1/, { selector: 'summary' }))
    expect(within(history).getByText(/Historical revision/)).toBeVisible()
    expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(0)
  })
  it('shows unavailable source separately from empty history and withdraws private values offline', async () => {
    const { state } = transport(), p = props(); render(<DailyReviewPanel {...p} />)
    await enterReview('Private rationale kept only in memory')
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false); act(() => { window.dispatchEvent(new Event('offline')) })
    await screen.findByText(/Review sources locked while offline/)
    expect(screen.queryByDisplayValue('Private rationale kept only in memory')).not.toBeInTheDocument()
    state.readFailed = true; vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); act(() => { window.dispatchEvent(new Event('online')) })
    expect(await screen.findByRole('alert')).toHaveTextContent('Review source or permission unavailable')
    expect(screen.queryByText(/No authorized review entries/)).not.toBeInTheDocument()
  })
})
