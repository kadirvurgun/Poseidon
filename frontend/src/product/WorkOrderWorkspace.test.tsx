import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { WorkOrderWorkspace } from './WorkOrderWorkspace'
import { workOrderSourceVersion } from './work-order-fields'
import type { WorkOrderRecord } from './work-order-api'
import { workOrderActor, workOrderPlanFixture, workOrderPlanPinFixture, workOrderCapsFixture, workOrderId, workOrderRecordFixture, workOrderTenant, workOrderVessel } from './work-order-test-fixtures'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
function transport() {
  const state = { actor: workOrderActor, writable: true, writeStatus: 200, readStatus: 200, planStatus: 200, sessionDenied: false, denyAfterReceipt: false, linked: false }
  const rows = new Map<string, WorkOrderRecord>([[workOrderId, workOrderRecordFixture()]]), receipts = new Map<string, WorkOrderRecord>()
  const vessel = { id: workOrderVessel, companyId: workOrderTenant, code: 'SYN-WORK-ORDER', name: 'Synthetic technical vessel', kind: 'Tug', version: 1, canEdit: true }
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, 'https://synthetic.invalid').pathname
    if (path === '/api/v1/auth/session') return state.sessionDenied ? json({}, 403) : json({ authenticated: true, canRead: true, actorId: state.actor, tenantId: workOrderTenant, csrfToken: 'synthetic-workOrder-csrf' })
    if (path === '/api/v1/vessels') return json({ items: [vessel], offset: 0, limit: 25, hasMore: false })
    if (path === '/api/v1/vessels/' + workOrderVessel) return json(vessel)
    if (path.includes('/work-order-plan-origins/')) return state.planStatus === 200 ? json(workOrderPlanFixture()) : json({ code: state.planStatus === 409 ? 'plan_context_stale' : 'permission_denied' }, state.planStatus)
    if (path === '/api/v1/work-orders/capabilities') return json({ ...workOrderCapsFixture(), canWrite: state.writable })
    if (init?.method === 'POST' || init?.method === 'PUT') {
      if (state.writeStatus !== 200) return json({ code: 'command_outcome_withheld' }, state.writeStatus)
      const key = (init.headers as Record<string, string>)['Idempotency-Key']; let receipt = receipts.get(key)
      if (!receipt) { const body = JSON.parse(init.body as string), previous = rows.get(body.id), record = { ...workOrderRecordFixture(), id: body.id, fields: body.fields, plan: body.plan, revision: (previous?.revision ?? 0) + 1, version: (previous?.version ?? 0) + 1 }; rows.set(record.id, record); receipt = { ...record, currentRevision: false, canEdit: false }; receipts.set(key, receipt) }
      if (state.denyAfterReceipt) state.sessionDenied = true
      return json(receipt)
    }
    if (path === '/api/v1/work-orders') return json({ items: [...rows.values()], offset: 0, limit: 25, hasMore: false, canWrite: state.writable, sourceVersion: workOrderSourceVersion })
    const record = [...rows.values()].find(row => path === '/api/v1/work-orders/' + row.id || path === '/api/v1/work-orders/' + row.id + '/history')
    if (record && path.endsWith('/history')) return json({ items: [{ ...record, plan: state.linked ? workOrderPlanPinFixture() : record.plan, canEdit: false }], offset: 0, limit: 25, hasMore: false })
    if (record) return state.readStatus === 200 ? json({ ...record, plan: state.linked ? workOrderPlanPinFixture() : record.plan, canEdit: state.writable }) : json({ code: 'service_unavailable' }, state.readStatus)
    return json({}, 403)
  })
  vi.stubGlobal('fetch', fetch); return { state, fetch }
}
const writes = (fetch: ReturnType<typeof transport>['fetch']) => fetch.mock.calls.filter(([, init]) => init?.method === 'POST' || init?.method === 'PUT')
beforeEach(() => { history.replaceState(null, '', '/app/work-order-center/' + workOrderVessel); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function open() { render(<WorkOrderWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByRole('button', { name: 'New work order draft' }); return user }
async function editNew(user: ReturnType<typeof userEvent.setup>) { await user.click(screen.getByRole('button', { name: 'New work order draft' })); await user.selectOptions(screen.getByLabelText('Work order number value state'), 'Known'); await user.type(screen.getByLabelText('Work order number'), 'PRIVATE retained work order') }
describe('T-S8-003 workOrder workspace — authored, not executed', () => {
  it('saves an incomplete unassessed declaration then establishes current edit rights from owning GET', async () => {
    const { fetch } = transport(), user = await open(); await editNew(user)
    await user.click(screen.getByRole('button', { name: 'Save unassessed draft' })); await screen.findByText(/Draft outcome recorded for revision/)
    const body = JSON.parse(writes(fetch)[0][1]!.body as string)
    expect(body).toMatchObject({ expectedVersion: 0, plan: null, sourceVersion: workOrderSourceVersion, fields: { work_order_no: { state: 'Known', value: 'PRIVATE retained work order' }, equipment_id: { state: 'Missing', value: null }, actual_finish: { state: 'Missing', value: null } } })
    expect(Object.keys(body.fields)).toHaveLength(25); expect(body.actorId).toBeUndefined()
    await waitFor(() => expect(screen.getByLabelText('Work order number')).toBeEnabled()); expect(window.location.search).toBe('?id=' + body.id)
    expect(fetch.mock.calls.some(([url, init]) => url.startsWith('/api/v1/work-orders/' + body.id + '?vessel=') && !init?.method)).toBe(true)
  })
  it('pins confirmed identity before readback503 and reopens the same record without a new POST', async () => {
    const { state, fetch } = transport(); state.readStatus = 503
    const view = render(<WorkOrderWorkspace />), user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByRole('button', { name: 'New work order draft' }); await editNew(user)
    await user.click(screen.getByRole('button', { name: 'Save unassessed draft' })); await screen.findByText(/Work order records or current access are unavailable/)
    const body = JSON.parse(writes(fetch)[0][1]!.body as string); expect(window.location.search).toBe('?id=' + body.id)
    view.unmount(); render(<WorkOrderWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByText(/Work order records or current access are unavailable/)
    expect(writes(fetch)).toHaveLength(1); state.readStatus = 200
    await user.click(screen.getByRole('button', { name: 'Retry source read' })); await waitFor(() => expect(screen.getByLabelText('Work order number')).toBeEnabled())
    expect(window.location.search).toBe('?id=' + body.id); expect(writes(fetch)).toHaveLength(1)
  })
  it('keeps and explicitly discards unsaved declarations without automatic commands', async () => {
    const { fetch } = transport(), user = await open(); await editNew(user)
    await user.click(screen.getByRole('button', { name: 'Back to work orders' })); await user.click(screen.getByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('Work order number')).toHaveValue('PRIVATE retained work order')
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Back to work orders' })); await user.click(screen.getByRole('button', { name: 'Discard draft' })); await screen.findByRole('heading', { name: 'Recorded work orders' })
    expect(writes(fetch)).toHaveLength(0)
  })
  it('withdraws private inputs on identity change and restores only the original exact unknown command', async () => {
    const { state, fetch } = transport(); state.writeStatus = 503
    const user = await open(); await editNew(user); await user.click(screen.getByRole('button', { name: 'Save unassessed draft' })); await screen.findByRole('button', { name: 'Retry exact work order command' })
    state.actor = workOrderId; act(() => window.dispatchEvent(new Event('focus'))); await screen.findByText(/This retained draft belongs to another signed-in context/)
    expect(screen.queryByDisplayValue('PRIVATE retained work order')).not.toBeInTheDocument(); expect(writes(fetch)).toHaveLength(1)
    state.actor = workOrderActor; state.writeStatus = 200; act(() => window.dispatchEvent(new Event('focus')))
    await user.click(await screen.findByRole('button', { name: 'Retry exact work order command' })); await screen.findByText(/Draft outcome recorded for revision/)
    expect(writes(fetch)).toHaveLength(2); expect(writes(fetch)[1][1]!.body).toBe(writes(fetch)[0][1]!.body); expect(writes(fetch)[1][1]!.headers).toEqual(writes(fetch)[0][1]!.headers)
  })
  it('retains an owning receipt across post-response403 until original-context recovery', async () => {
    const { state, fetch } = transport(); state.denyAfterReceipt = true
    const user = await open(); await editNew(user); await user.click(screen.getByRole('button', { name: 'Save unassessed draft' })); await screen.findByText(/Work order records or current access are unavailable/)
    expect(screen.getByRole('button', { name: 'Back to work orders' })).toBeDisabled()
    state.sessionDenied = false; state.denyAfterReceipt = false
    await user.click(screen.getByRole('button', { name: 'Retry source read' })); await user.click(await screen.findByRole('button', { name: 'Retry exact work order command' })); await screen.findByText(/Draft outcome recorded for revision/)
    expect(writes(fetch)).toHaveLength(2); expect(writes(fetch)[1][1]!.body).toBe(writes(fetch)[0][1]!.body); expect(writes(fetch)[1][1]!.headers).toEqual(writes(fetch)[0][1]!.headers)
  })
  it('retains an old plan pin on context409 while allowing another declaration edit, without source criticality autofill', async () => {
    history.replaceState(null, '', '/app/work-order-center/' + workOrderVessel + '?id=' + workOrderId)
    const { state, fetch } = transport(); state.linked = true; state.planStatus = 409
    render(<WorkOrderWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByText(/Linked maintenance plan changed/)
    expect(screen.getByLabelText('Criticality value state')).toHaveValue('Missing'); expect(screen.queryByText('Synthetic plan equipment')).not.toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Work description value state'), 'Known'); await user.type(screen.getByLabelText('Work description'), 'Retain exact equipment provenance')
    await user.click(screen.getByRole('button', { name: 'Save unassessed draft' })); await screen.findByText(/Draft outcome recorded for revision/)
    expect(JSON.parse(writes(fetch)[0][1]!.body as string).plan).toEqual(workOrderPlanPinFixture())
  })
  it('does not invent a historical plan concurrency version for a new stale-context launch', async () => {
    const plan = workOrderPlanPinFixture(); history.replaceState(null, '', '/app/work-order-center/' + workOrderVessel + '?planId=' + plan.id + '&planRevision=' + plan.revision)
    const { state, fetch } = transport(); state.planStatus = 409
    render(<WorkOrderWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByText(/This maintenance plan context cannot be captured/)
    expect(screen.getByRole('button', { name: 'Save unassessed draft' })).toBeDisabled(); expect(writes(fetch)).toHaveLength(0)
  })
  it('provides an authorized root picker for either ship or tug contexts', async () => {
    history.replaceState(null, '', '/app/work-order-center'); transport(); render(<WorkOrderWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' }))
    await user.click(await screen.findByRole('button', { name: 'SYN-WORK-ORDER · Synthetic technical vessel' })); await screen.findByRole('button', { name: 'New work order draft' })
    expect(window.location.pathname).toBe('/app/work-order-center/' + workOrderVessel)
  })
})
