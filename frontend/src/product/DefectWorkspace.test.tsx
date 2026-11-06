import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DefectWorkspace } from './DefectWorkspace'
import { defectSourceVersion } from './defect-fields'
import type { DefectRecord } from './defect-api'
import { defectActor, defectAssetFixture, defectAssetPinFixture, defectCapsFixture, defectId, defectRecordFixture, defectTenant, defectVessel } from './defect-test-fixtures'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
function transport() {
  const state = { actor: defectActor, writable: true, writeStatus: 200, readStatus: 200, assetStatus: 200, sessionDenied: false, denyAfterReceipt: false, linked: false }
  const rows = new Map<string, DefectRecord>([[defectId, defectRecordFixture()]]), receipts = new Map<string, DefectRecord>()
  const vessel = { id: defectVessel, companyId: defectTenant, code: 'SYN-DEFECT', name: 'Synthetic technical vessel', kind: 'Tug', version: 1, canEdit: true }
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, 'https://synthetic.invalid').pathname
    if (path === '/api/v1/auth/session') return state.sessionDenied ? json({}, 403) : json({ authenticated: true, canRead: true, actorId: state.actor, tenantId: defectTenant, csrfToken: 'synthetic-defect-csrf' })
    if (path === '/api/v1/vessels') return json({ items: [vessel], offset: 0, limit: 25, hasMore: false })
    if (path === '/api/v1/vessels/' + defectVessel) return json(vessel)
    if (path.includes('/defect-asset-context/')) return state.assetStatus === 200 ? json(defectAssetFixture()) : json({ code: state.assetStatus === 409 ? 'asset_context_stale' : 'permission_denied' }, state.assetStatus)
    if (path === '/api/v1/defects/capabilities') return json({ ...defectCapsFixture(), canWrite: state.writable })
    if (init?.method === 'POST' || init?.method === 'PUT') {
      if (state.writeStatus !== 200) return json({ code: 'command_outcome_withheld' }, state.writeStatus)
      const key = (init.headers as Record<string, string>)['Idempotency-Key']; let receipt = receipts.get(key)
      if (!receipt) { const body = JSON.parse(init.body as string), previous = rows.get(body.id), record = { ...defectRecordFixture(), id: body.id, fields: body.fields, asset: body.asset, revision: (previous?.revision ?? 0) + 1, version: (previous?.version ?? 0) + 1 }; rows.set(record.id, record); receipt = { ...record, currentRevision: false, canEdit: false }; receipts.set(key, receipt) }
      if (state.denyAfterReceipt) state.sessionDenied = true
      return json(receipt)
    }
    if (path === '/api/v1/defects') return json({ items: [...rows.values()], offset: 0, limit: 25, hasMore: false, canWrite: state.writable, sourceVersion: defectSourceVersion })
    const record = [...rows.values()].find(row => path === '/api/v1/defects/' + row.id || path === '/api/v1/defects/' + row.id + '/history')
    if (record && path.endsWith('/history')) return json({ items: [{ ...record, asset: state.linked ? defectAssetPinFixture() : record.asset, canEdit: false }], offset: 0, limit: 25, hasMore: false })
    if (record) return state.readStatus === 200 ? json({ ...record, asset: state.linked ? defectAssetPinFixture() : record.asset, canEdit: state.writable }) : json({ code: 'service_unavailable' }, state.readStatus)
    return json({}, 403)
  })
  vi.stubGlobal('fetch', fetch); return { state, fetch }
}
const writes = (fetch: ReturnType<typeof transport>['fetch']) => fetch.mock.calls.filter(([, init]) => init?.method === 'POST' || init?.method === 'PUT')
beforeEach(() => { history.replaceState(null, '', '/app/defects/' + defectVessel); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function open() { render(<DefectWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByRole('button', { name: 'New defect declaration' }); return user }
async function editNew(user: ReturnType<typeof userEvent.setup>) { await user.click(screen.getByRole('button', { name: 'New defect declaration' })); await user.selectOptions(screen.getByLabelText('Defect number value state'), 'Known'); await user.type(screen.getByLabelText('Defect number'), 'PRIVATE retained declaration') }
describe('T-S8-004 defect workspace — authored, not executed', () => {
  it('saves an incomplete unassessed declaration then establishes current edit rights from owning GET', async () => {
    const { fetch } = transport(), user = await open(); await editNew(user)
    await user.click(screen.getByRole('button', { name: 'Save unassessed declaration' })); await screen.findByText(/Declaration outcome recorded for revision/)
    const body = JSON.parse(writes(fetch)[0][1]!.body as string)
    expect(body).toMatchObject({ expectedVersion: 0, asset: null, sourceVersion: defectSourceVersion, fields: { defect_no: { state: 'Known', value: 'PRIVATE retained declaration' }, safeguard: { state: 'Missing', value: null }, temporary_measure_expires_at: { state: 'Missing', value: null } } })
    expect(Object.keys(body.fields)).toHaveLength(11); expect(body.actorId).toBeUndefined()
    await waitFor(() => expect(screen.getByLabelText('Defect number')).toBeEnabled()); expect(window.location.search).toBe('?id=' + body.id)
    expect(fetch.mock.calls.some(([url, init]) => url.startsWith('/api/v1/defects/' + body.id + '?vessel=') && !init?.method)).toBe(true)
  })
  it('pins confirmed identity before readback503 and reopens the same record without a new POST', async () => {
    const { state, fetch } = transport(); state.readStatus = 503
    const view = render(<DefectWorkspace />), user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByRole('button', { name: 'New defect declaration' }); await editNew(user)
    await user.click(screen.getByRole('button', { name: 'Save unassessed declaration' })); await screen.findByText(/Defect records or current access are unavailable/)
    const body = JSON.parse(writes(fetch)[0][1]!.body as string); expect(window.location.search).toBe('?id=' + body.id)
    view.unmount(); render(<DefectWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByText(/Defect records or current access are unavailable/)
    expect(writes(fetch)).toHaveLength(1); state.readStatus = 200
    await user.click(screen.getByRole('button', { name: 'Retry source read' })); await waitFor(() => expect(screen.getByLabelText('Defect number')).toBeEnabled())
    expect(window.location.search).toBe('?id=' + body.id); expect(writes(fetch)).toHaveLength(1)
  })
  it('keeps and explicitly discards unsaved declarations without automatic commands', async () => {
    const { fetch } = transport(), user = await open(); await editNew(user)
    await user.click(screen.getByRole('button', { name: 'Back to declarations' })); await user.click(screen.getByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('Defect number')).toHaveValue('PRIVATE retained declaration')
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Back to declarations' })); await user.click(screen.getByRole('button', { name: 'Discard draft' })); await screen.findByRole('heading', { name: 'Recorded declarations' })
    expect(writes(fetch)).toHaveLength(0)
  })
  it('withdraws private inputs on identity change and restores only the original exact unknown command', async () => {
    const { state, fetch } = transport(); state.writeStatus = 503
    const user = await open(); await editNew(user); await user.click(screen.getByRole('button', { name: 'Save unassessed declaration' })); await screen.findByRole('button', { name: 'Retry exact defect command' })
    state.actor = defectId; act(() => window.dispatchEvent(new Event('focus'))); await screen.findByText(/This retained draft belongs to another signed-in context/)
    expect(screen.queryByDisplayValue('PRIVATE retained declaration')).not.toBeInTheDocument(); expect(writes(fetch)).toHaveLength(1)
    state.actor = defectActor; state.writeStatus = 200; act(() => window.dispatchEvent(new Event('focus')))
    await user.click(await screen.findByRole('button', { name: 'Retry exact defect command' })); await screen.findByText(/Declaration outcome recorded for revision/)
    expect(writes(fetch)).toHaveLength(2); expect(writes(fetch)[1][1]!.body).toBe(writes(fetch)[0][1]!.body); expect(writes(fetch)[1][1]!.headers).toEqual(writes(fetch)[0][1]!.headers)
  })
  it('retains an owning receipt across post-response403 until original-context recovery', async () => {
    const { state, fetch } = transport(); state.denyAfterReceipt = true
    const user = await open(); await editNew(user); await user.click(screen.getByRole('button', { name: 'Save unassessed declaration' })); await screen.findByText(/Defect records or current access are unavailable/)
    expect(screen.getByRole('button', { name: 'Back to declarations' })).toBeDisabled()
    state.sessionDenied = false; state.denyAfterReceipt = false
    await user.click(screen.getByRole('button', { name: 'Retry source read' })); await user.click(await screen.findByRole('button', { name: 'Retry exact defect command' })); await screen.findByText(/Declaration outcome recorded for revision/)
    expect(writes(fetch)).toHaveLength(2); expect(writes(fetch)[1][1]!.body).toBe(writes(fetch)[0][1]!.body); expect(writes(fetch)[1][1]!.headers).toEqual(writes(fetch)[0][1]!.headers)
  })
  it('retains an old asset pin on context409 while allowing another declaration edit, without source criticality autofill', async () => {
    history.replaceState(null, '', '/app/defects/' + defectVessel + '?id=' + defectId)
    const { state, fetch } = transport(); state.linked = true; state.assetStatus = 409
    render(<DefectWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByText(/Equipment source changed/)
    expect(screen.getByLabelText('Declared criticality value state')).toHaveValue('Missing'); expect(screen.queryByText('Synthetic equipment')).not.toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Declaration notes value state'), 'Known'); await user.type(screen.getByLabelText('Declaration notes'), 'Retain exact equipment provenance')
    await user.click(screen.getByRole('button', { name: 'Save unassessed declaration' })); await screen.findByText(/Declaration outcome recorded for revision/)
    expect(JSON.parse(writes(fetch)[0][1]!.body as string).asset).toEqual(defectAssetPinFixture())
  })
  it('does not invent a historical asset concurrency version for a new stale-context launch', async () => {
    const asset = defectAssetPinFixture(); history.replaceState(null, '', '/app/defects/' + defectVessel + '?assetId=' + asset.id + '&assetRevision=' + asset.revision)
    const { state, fetch } = transport(); state.assetStatus = 409
    render(<DefectWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' })); await screen.findByText(/This equipment context cannot be captured/)
    expect(screen.getByRole('button', { name: 'Save unassessed declaration' })).toBeDisabled(); expect(writes(fetch)).toHaveLength(0)
  })
  it('provides an authorized root picker for either ship or tug contexts', async () => {
    history.replaceState(null, '', '/app/defects'); transport(); render(<DefectWorkspace />); const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' }))
    await user.click(await screen.findByRole('button', { name: 'SYN-DEFECT · Synthetic technical vessel' })); await screen.findByRole('button', { name: 'New defect declaration' })
    expect(window.location.pathname).toBe('/app/defects/' + defectVessel)
  })
})
