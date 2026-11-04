import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DailyReportWorkspace } from './DailyReportWorkspace'
import { dailyActor, dailyCapsFixture, dailyId, dailyPageFixture, dailyRecordFixture, dailyVessel } from './daily-test-fixtures'
import { dailyRoutes, dailySourceVersion } from './daily-operations-api'
import type { DailyKind } from './daily-report-fields'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const identity = (actor = dailyActor) => ({ authenticated: true, setupRequired: false, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, csrfToken: 'synthetic-daily-csrf', actorId: actor, tenantId: dailyVessel, tenantName: 'Synthetic workspace' })
function transport(kind: DailyKind, options: { writable?: boolean; row?: boolean; user?: () => Promise<Response>; write?: (init: RequestInit) => Promise<Response> } = {}) {
  const route = dailyRoutes[kind].api
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return options.user ? options.user() : json(identity())
    if (init?.method === 'POST' || init?.method === 'PUT') {
      if (options.write) return options.write(init)
      const body = JSON.parse(init.body as string), row = dailyRecordFixture(kind)
      return json({ ...row, id: body.id, revision: 2, version: 18, fields: { ...row.fields, ...body.fields } })
    }
    if (url.includes(`/${route}/capabilities?`)) return json(dailyCapsFixture(kind, options.writable !== false))
    if (url.includes(`/${route}/${dailyId}/review-capabilities?`)) return json({ recordId: dailyId, vesselId: dailyVessel, kind, recordRevision: 1, recordVersion: 17, reviewVersion: 0, canRecord: false, authorities: [], blockers: ['HUMAN_REVIEW_AUTHORITY_UNAVAILABLE'], sourceVersion: dailySourceVersion })
    if (url.includes(`/${route}/${dailyId}/reviews?`)) return json({ items: [], offset: 0, limit: 25, hasMore: false })
    if (url.includes(`/${route}/${dailyId}/history?`)) return json({ items: [dailyRecordFixture(kind)], offset: 0, limit: 25, hasMore: false })
    if (url.includes(`/${route}/${dailyId}?`)) return json(dailyRecordFixture(kind))
    if (url.includes(`/${route}?`)) return json({ ...dailyPageFixture(kind, options.row ? [dailyRecordFixture(kind)] : []), canWrite: options.writable !== false })
    if (url.endsWith('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    return json({}, 403)
  })
  vi.stubGlobal('fetch', fetch); return fetch
}
async function english() { await userEvent.setup().click(screen.getByRole('button', { name: 'EN' })) }
async function newDraft() { await userEvent.setup().click(await screen.findByRole('button', { name: 'New draft' })); return screen.getByRole('form', { name: 'Daily declaration form' }) }
beforeEach(() => { window.history.replaceState(null, '', `/app/noon-report/${dailyVessel}`); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('T-OPS2 daily source interactions (authored; execution deferred)', () => {
  it('saves incomplete declarations with lexical zero/precision and no source-owned review payload', async () => {
    const fetch = transport('Noon'), user = userEvent.setup(); render(<DailyReportWorkspace />); await english(); const form = await newDraft()
    expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
    await user.selectOptions(within(form).getByLabelText('Average speed (kn) — Data state'), 'Known')
    await user.type(within(form).getByLabelText('Average speed (kn)'), '0')
    await user.selectOptions(within(form).getByLabelText('Latitude — Data state'), 'Known')
    await user.type(within(form).getByLabelText('Latitude'), '-12.123456789012345678901234567890123456')
    await user.selectOptions(within(form).getByLabelText('ROB summary — Data state'), 'Unknown')
    expect(within(within(form).getByLabelText('Voyage reference — Data state')).getByRole('option', { name: 'Known' })).toBeDisabled()
    await user.click(within(form).getByRole('button', { name: 'Save draft' }))
    await screen.findByText('Draft saved. Human review and operational approval remain separate.')
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'POST')!, body = JSON.parse(call[1]!.body as string)
    expect(body.fields.avg_speed_kn).toEqual({ state: 'Known', value: '0' })
    expect(body.fields.position_lat.value).toBe('-12.123456789012345678901234567890123456')
    expect(body.fields.rob_summary).toEqual({ state: 'Unknown', value: null }); expect(body.fields.fuel_summary).toEqual({ state: 'Missing', value: null })
    expect(body.fields).not.toHaveProperty('master_review'); expect(body.fields).not.toHaveProperty('vessel_id')
    expect(call[1]).toMatchObject({ credentials: 'same-origin', headers: { 'X-CSRF-TOKEN': 'synthetic-daily-csrf', 'Idempotency-Key': expect.any(String) } })
  })
  it('retains an uncertain command and freezes fields until exact retry confirms the original attempt', async () => {
    let writes = 0
    const fetch = transport('Noon', { write: async init => { if (++writes === 1) throw new TypeError('lost response'); const body = JSON.parse(init.body as string), row = dailyRecordFixture('Noon'); return json({ ...row, id: body.id, fields: { ...row.fields, ...body.fields } }) } })
    const user = userEvent.setup(); render(<DailyReportWorkspace />); await english(); const form = await newDraft()
    await user.selectOptions(within(form).getByLabelText('Weather / sea summary — Data state'), 'Known'); await user.type(within(form).getByLabelText('Weather / sea summary'), 'Synthetic retained weather')
    await user.click(within(form).getByRole('button', { name: 'Save draft' }))
    await screen.findByText(/Save outcome unknown/)
    expect(within(form).getByLabelText('Weather / sea summary')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Back to daily records' })).toBeDisabled()
    await user.click(within(form).getByRole('button', { name: 'Retry exact saved command' }))
    await screen.findByText('Draft saved. Human review and operational approval remain separate.')
    const calls = fetch.mock.calls.filter(([, init]) => init?.method === 'POST')
    expect(calls).toHaveLength(2); expect(calls[0][1]?.body).toBe(calls[1][1]?.body); expect(calls[0][1]?.headers).toEqual(calls[1][1]?.headers)
  })
  it('keeps the original unknown receipt identity/key/body after a later permission denial', async () => {
    let writes = 0
    const fetch = transport('Noon', { write: async init => {
      writes++
      if (writes === 1) return json({ code: 'command_outcome_withheld' }, 503)
      if (writes === 2) return json({ code: 'permission_denied' }, 403)
      const body = JSON.parse(init.body as string), row = dailyRecordFixture('Noon')
      return json({ ...row, id: body.id, fields: { ...row.fields, ...body.fields } })
    } })
    const user = userEvent.setup(); render(<DailyReportWorkspace />); await english(); await newDraft()
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByText(/Save outcome unknown/)
    await user.click(screen.getByRole('button', { name: 'Retry exact saved command' }))
    await waitFor(() => expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(2))
    const retry = await screen.findByRole('button', { name: 'Retry exact saved command' }); await waitFor(() => expect(retry).toBeEnabled())
    expect(screen.getByRole('button', { name: 'Back to daily records' })).toBeDisabled()
    await user.click(retry); await screen.findByText('Draft saved. Human review and operational approval remain separate.')
    const calls = fetch.mock.calls.filter(([, init]) => init?.method === 'POST')
    expect(calls).toHaveLength(3)
    expect(calls.every(call => call[1]?.body === calls[0][1]?.body)).toBe(true)
    expect(calls.every(call => JSON.stringify(call[1]?.headers) === JSON.stringify(calls[0][1]?.headers))).toBe(true)
  })
  it('confirms keep/discard exactly once for an internal back action and retains beforeunload protection', async () => {
    transport('Noon'); const user = userEvent.setup(); render(<DailyReportWorkspace />); await english(); const form = await newDraft()
    await user.selectOptions(within(form).getByLabelText('Current activity — Data state'), 'Known'); await user.selectOptions(within(form).getByLabelText('Current activity'), 'At Sea')
    await user.click(screen.getByRole('button', { name: 'Back to daily records' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Keep editing' }))
    expect(within(form).getByLabelText('Current activity')).toHaveValue('At Sea')
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Back to daily records' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Discard draft' }))
    await screen.findByRole('heading', { name: 'Recorded drafts' })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(); expect(screen.queryByRole('form')).not.toBeInTheDocument()
  })
  it.each(['Deck', 'Engine'] as const)('uses the server-gated %s schema, readonly state and immutable history', async kind => {
    history.replaceState(null, '', `/app/${dailyRoutes[kind].app}/${dailyVessel}?id=${dailyId}`)
    transport(kind, { writable: false }); render(<DailyReportWorkspace />); await english()
    await screen.findByText('Read-only access. Editing and saving are not granted.')
    expect(screen.getByLabelText('Log date')).toBeDisabled(); expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Daily immutable history' })).toBeVisible()
    expect(await screen.findByText('No current source-authorized human appointment is available for this record.')).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Record review entry' })).not.toBeInTheDocument()
    expect(screen.getByText(/Crew and tug grants cannot authorize these fields/)).toBeVisible()
    expect(screen.queryByText('Ready', { exact: true })).not.toBeInTheDocument()
  })
  it('withdraws private inputs during a delayed identity change and never transfers an uncertain body to the new actor', async () => {
    let actor = dailyActor, deferredSession: ((value: Response) => void) | undefined, delay = false
    const fetch = transport('Noon', { user: async () => delay ? new Promise<Response>(resolve => { deferredSession = resolve }) : json(identity(actor)), write: async () => { throw new TypeError('unknown original result') } })
    const user = userEvent.setup(); render(<DailyReportWorkspace />); await english(); const form = await newDraft()
    await user.selectOptions(within(form).getByLabelText('Weather / sea summary — Data state'), 'Known'); await user.type(within(form).getByLabelText('Weather / sea summary'), 'PRIVATE original identity draft')
    await user.click(within(form).getByRole('button', { name: 'Save draft' })); await screen.findByText(/Save outcome unknown/)
    delay = true; act(() => { window.dispatchEvent(new Event('focus')) })
    await waitFor(() => expect(deferredSession).toBeDefined())
    expect(screen.queryByDisplayValue('PRIVATE original identity draft')).not.toBeInTheDocument()
    actor = dailyId; delay = false; await act(async () => { deferredSession!(json(identity(actor))) })
    expect(await screen.findByText(/Retained inputs belong to another verified identity/)).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Retry exact saved command' })).not.toBeInTheDocument()
    expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
    actor = dailyActor; act(() => { window.dispatchEvent(new Event('focus')) })
    expect(await screen.findByDisplayValue('PRIVATE original identity draft')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Retry exact saved command' })).toBeEnabled()
    expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
  })
  it('does not convert failure or offline state into an empty healthy record set', async () => {
    const fetch = transport('Noon'); const user = userEvent.setup(); render(<DailyReportWorkspace />); await english(); await newDraft()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false); act(() => { window.dispatchEvent(new Event('offline')) })
    await screen.findByText(/Current daily sources unavailable while offline or hidden/)
    expect(screen.queryByRole('form')).not.toBeInTheDocument()
    expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
    expect(screen.queryByText(/No authorized daily drafts/)).not.toBeInTheDocument()
  })
})
