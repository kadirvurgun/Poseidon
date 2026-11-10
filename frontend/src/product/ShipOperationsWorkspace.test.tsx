import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ShipOperationsWorkspace } from './ShipOperationsWorkspace'
import { shipOpsSourceVersion, type ShipOpsKind } from './ship-operation-fields'
import type { ShipOpsRecord } from './ship-operations-api'
import { shipOpsActor, shipOpsCapsFixture, shipOpsOriginFixture, shipOpsOriginPinFixture, shipOpsRecordFixture, shipOpsRecordId, shipOpsTenant, shipOpsVessel } from './ship-operation-test-fixtures'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
function transport(kind: ShipOpsKind = 'Fuel') {
  const state = { actor: shipOpsActor, writable: true, writeStatus: 200, sessionDenied: false, denyAfterReceipt: false, originCurrent: true, readStatus: 200 }
  const rows = new Map<string, ShipOpsRecord>([[shipOpsRecordId, shipOpsRecordFixture(kind)]]), receipts = new Map<string, ShipOpsRecord>()
  const api = kind === 'Fuel' ? '/api/v1/fuel-bunker-logs' : '/api/v1/port-activity-sof'
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, 'https://synthetic.invalid').pathname
    if (path === '/api/v1/auth/session') return state.sessionDenied ? json({}, 403) : json({ authenticated: true, canRead: true, actorId: state.actor, tenantId: shipOpsTenant, csrfToken: 'synthetic-operational-csrf' })
    if (path === '/api/v1/vessels/' + shipOpsVessel) return json({ id: shipOpsVessel, companyId: shipOpsTenant, code: 'SYN-OPS', name: 'Synthetic operations vessel', kind: 'Ship', version: 1, canEdit: state.writable })
    if (path.includes('/ship-operation-origins/')) return json({ ...shipOpsOriginFixture(), current: state.originCurrent })
    if (path === api + '/capabilities') return json({ ...shipOpsCapsFixture(kind), canWrite: state.writable })
    if (init?.method === 'POST' || init?.method === 'PUT') {
      if (state.writeStatus !== 200) return json({ code: 'command_outcome_withheld' }, state.writeStatus)
      const key = (init.headers as Record<string, string>)['Idempotency-Key']
      let receipt = receipts.get(key)
      if (!receipt) { const body = JSON.parse(init.body as string), previous = rows.get(body.id), record = { ...shipOpsRecordFixture(kind), id: body.id, revision: (previous?.revision ?? 0) + 1, version: (previous?.version ?? 0) + 1, fields: { ...shipOpsRecordFixture(kind).fields, ...body.fields }, origin: body.origin }; rows.set(record.id, record); receipt = { ...record, currentRevision: false, canEdit: false }; receipts.set(key, receipt) }
      if (state.denyAfterReceipt) state.sessionDenied = true
      return json(receipt)
    }
    if (path === api) return json({ items: [...rows.values()], offset: 0, limit: 25, hasMore: false, canWrite: state.writable, sourceVersion: shipOpsSourceVersion, offline: shipOpsCapsFixture(kind).offline })
    const record = [...rows.values()].find(row => path === api + '/' + row.id || path === api + '/' + row.id + '/history')
    if (record && path.endsWith('/history')) return json({ items: [record], offset: 0, limit: 25, hasMore: false })
    if (record) return state.readStatus === 200 ? json({ ...record, canEdit: state.writable }) : json({ code: 'service_unavailable' }, state.readStatus)
    return json({}, 403)
  })
  vi.stubGlobal('fetch', fetch); return { state, fetch }
}
const writes = (fetch: ReturnType<typeof transport>['fetch']) => fetch.mock.calls.filter(([, init]) => init?.method === 'POST' || init?.method === 'PUT')
beforeEach(() => { history.replaceState(null, '', '/app/fuel-bunker-log/' + shipOpsVessel); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function open() { render(<ShipOperationsWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByRole('button', { name: 'New draft record' }); return user }
async function newFuel(user: ReturnType<typeof userEvent.setup>) { await user.click(screen.getByRole('button', { name: 'New draft record' })); await user.selectOptions(screen.getByLabelText('BDN number value state'), 'Known'); await user.type(screen.getByLabelText('BDN number'), 'PRIVATE declared BDN') }
describe('T-OPS2-004/005 scoped operational workspace — sources only', () => {
  it('saves an incomplete declaration with exact values then reads current source before another edit', async () => {
    const { fetch } = transport(), user = await open(); await newFuel(user)
    await user.selectOptions(screen.getByLabelText('Quantity received (mt) value state'), 'Known')
    await user.click(screen.getByLabelText('Quantity received (mt)'))
    await user.paste('9007199254740993.123456789')
    expect(screen.getByLabelText('Quantity received (mt)')).toHaveValue('9007199254740993.123456789')
    await user.click(screen.getByRole('button', { name: 'Save unverified draft' }))
    await screen.findByText(/Command outcome recorded for revision/)
    const request = writes(fetch)[0], body = JSON.parse(request[1]!.body as string)
    expect(body).toMatchObject({ expectedVersion: 0, origin: null, fields: { quantity_received_mt: { state: 'Known', value: '9007199254740993.123456789' }, supplier: { state: 'Missing', value: null } } })
    expect(body.fields.compliance_state).toBeUndefined()
    await waitFor(() => expect(screen.getByLabelText('Quantity received (mt)')).not.toBeDisabled())
    expect(window.location.search).toBe('?id=' + body.id)
    expect(fetch.mock.calls.some(([url, init]) => url.includes('/fuel-bunker-logs/' + body.id + '?') && !init?.method)).toBe(true)
  })
  it('pins a confirmed origin-created id before failed readback and reopens it without another create', async () => {
    const origin = shipOpsOriginPinFixture(); history.replaceState(null, '', '/app/fuel-bunker-log/' + shipOpsVessel + '?originKind=Noon&originId=' + origin.id + '&originRevision=2')
    const { state, fetch } = transport(); state.readStatus = 503
    const first = render(<ShipOperationsWorkspace />), user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' }))
    await user.click(await screen.findByRole('button', { name: 'Save unverified draft' }))
    await screen.findByText(/Operational source or access unavailable/)
    const body = JSON.parse(writes(fetch)[0][1]!.body as string)
    expect(window.location.search).toBe('?id=' + body.id); expect(body.origin).toEqual(origin); expect(writes(fetch)).toHaveLength(1)
    first.unmount(); render(<ShipOperationsWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
    await screen.findByText(/Operational source or access unavailable/)
    expect(window.location.search).toBe('?id=' + body.id); expect(writes(fetch)).toHaveLength(1)
    state.readStatus = 200; await user.click(screen.getByRole('button', { name: 'Retry source read' }))
    await waitFor(() => expect(screen.getByLabelText('BDN number value state')).toBeEnabled())
    expect(window.location.search).toBe('?id=' + body.id); expect(writes(fetch)).toHaveLength(1)
  })
  it('keeps and explicitly discards local form edits through the shared guard without auto saving', async () => {
    const { fetch } = transport(), user = await open(); await newFuel(user)
    await user.click(screen.getByRole('button', { name: 'Back to records' })); await user.click(screen.getByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('BDN number')).toHaveValue('PRIVATE declared BDN')
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Back to records' })); await user.click(screen.getByRole('button', { name: 'Discard draft' }))
    await screen.findByRole('heading', { name: 'Fuel and ROB records' }); expect(writes(fetch)).toHaveLength(0)
  })
  it('withdraws retained private inputs after identity change and never transfers an unknown command', async () => {
    const { state, fetch } = transport(); state.writeStatus = 503
    const user = await open(); await newFuel(user); await user.click(screen.getByRole('button', { name: 'Save unverified draft' })); await screen.findByRole('button', { name: 'Retry exact operational command' })
    state.actor = shipOpsRecordId; act(() => window.dispatchEvent(new Event('focus')))
    await screen.findByText(/Retained fields and commands belong to another verified identity/)
    expect(screen.queryByDisplayValue('PRIVATE declared BDN')).not.toBeInTheDocument(); expect(writes(fetch)).toHaveLength(1)
    state.actor = shipOpsActor; state.writeStatus = 200; act(() => window.dispatchEvent(new Event('focus')))
    await user.click(await screen.findByRole('button', { name: 'Retry exact operational command' }))
    await screen.findByText(/Command outcome recorded for revision/)
    expect(writes(fetch)).toHaveLength(2); expect(writes(fetch)[1][1]!.body).toBe(writes(fetch)[0][1]!.body); expect(writes(fetch)[1][1]!.headers).toEqual(writes(fetch)[0][1]!.headers)
  })
  it('retains a received receipt across a following403 session fence and recovers only its original command', async () => {
    const { state, fetch } = transport(); state.denyAfterReceipt = true
    const user = await open(); await newFuel(user); await user.click(screen.getByRole('button', { name: 'Save unverified draft' }))
    await screen.findByText(/Operational source or access unavailable/)
    expect(screen.getByRole('button', { name: 'Back to records' })).toBeDisabled()
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(true)
    state.sessionDenied = false; state.denyAfterReceipt = false
    await user.click(screen.getByRole('button', { name: 'Retry source read' })); await user.click(await screen.findByRole('button', { name: 'Retry exact operational command' }))
    await screen.findByText(/Command outcome recorded for revision/)
    expect(writes(fetch)).toHaveLength(2); expect(writes(fetch)[1][1]!.body).toBe(writes(fetch)[0][1]!.body); expect(writes(fetch)[1][1]!.headers).toEqual(writes(fetch)[0][1]!.headers)
  })
  it('links only the exact read-only daily revision without copying its unknown-unit summary into quantities', async () => {
    const origin = shipOpsOriginPinFixture(); history.replaceState(null, '', '/app/fuel-bunker-log/' + shipOpsVessel + '?originKind=Noon&originId=' + origin.id + '&originRevision=2')
    const { fetch } = transport(); render(<ShipOperationsWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' }))
    await screen.findByText('Unqualified summary 99 units')
    expect(screen.getByLabelText('Quantity consumed (mt) value state')).toHaveValue('Missing')
    expect(screen.getByLabelText('Quantity consumed (mt)')).toHaveValue('')
    await user.click(screen.getByRole('button', { name: 'Save unverified draft' })); await screen.findByText(/Command outcome recorded for revision/)
    const body = JSON.parse(writes(fetch)[0][1]!.body as string)
    expect(body.origin).toEqual(origin); expect(body.fields.quantity_consumed_mt).toEqual({ state: 'Missing', value: null })
  })
  it('keeps a historical origin readable but blocks its use for a new command', async () => {
    const origin = shipOpsOriginPinFixture(); history.replaceState(null, '', '/app/fuel-bunker-log/' + shipOpsVessel + '?originKind=Noon&originId=' + origin.id + '&originRevision=2')
    const { state, fetch } = transport(); state.originCurrent = false
    render(<ShipOperationsWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' }))
    await screen.findByText(/A new command cannot bind this historical/)
    expect(screen.getByRole('button', { name: 'Save unverified draft' })).toBeDisabled(); expect(writes(fetch)).toHaveLength(0)
  })
})
