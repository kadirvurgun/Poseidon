// Mocked panel contract proves parent orchestration only. Real encryption, proof,
// IndexedDB persistence and live-policy tests are separately authored by its owner.
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ComponentProps } from 'react'
import type { DailyOfflinePanel } from './daily-offline/DailyOfflinePanel'
import { dailyActor, dailyCapsFixture, dailyId, dailyRecordFixture, dailyVessel } from './daily-test-fixtures'
import { dailySourceVersion } from './daily-operations-api'
import { DailyReportWorkspace } from './DailyReportWorkspace'
const panel = vi.hoisted(() => ({ current: null as ComponentProps<typeof DailyOfflinePanel> | null }))
vi.mock('./daily-offline/DailyOfflinePanel', async () => {
  const { useState } = await import('react')
  return { DailyOfflinePanel: (props: ComponentProps<typeof DailyOfflinePanel>) => {
    panel.current = props
    const [instance] = useState(() => crypto.randomUUID())
    return <section aria-label="Mock encrypted panel orchestration"><p>Resource: {props.recordId}</p><p>Instance: {instance}</p><p>{props.enabled ? 'UI lock open; no grant inferred' : 'UI locked'}</p><button disabled={!props.enabled} onClick={() => props.onDirty(true)}>Simulate pending classified capture</button><button disabled={!props.enabled} onClick={() => { props.onDirty(false); props.onCommitted() }}>Simulate confirmed offline receipt</button></section>
  } }
})
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
function transport() {
  const state = { actor: dailyActor, row: dailyRecordFixture('Noon'), writable: true, writeFailure: 0 }
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return json({ authenticated: true, canRead: true, canSearch: false, canReadCompanies: false, canCreate: false, actorId: state.actor, tenantId: dailyVessel, csrfToken: 'synthetic-only' })
    if (init?.method === 'POST' || init?.method === 'PUT') { if (state.writeFailure) return json({ code: 'version_conflict' }, state.writeFailure); return json(state.row) }
    if (url.includes('/noon-reports/capabilities?')) return json(dailyCapsFixture('Noon', state.writable))
    if (url.includes('/review-capabilities?')) return json({ recordId: state.row.id, vesselId: dailyVessel, kind: 'Noon', recordRevision: state.row.revision, recordVersion: state.row.version, reviewVersion: 0, canRecord: false, authorities: [], blockers: ['HUMAN_REVIEW_AUTHORITY_UNAVAILABLE'], sourceVersion: dailySourceVersion })
    if (url.includes('/history?')) return json({ items: [state.row], offset: 0, limit: 25, hasMore: false })
    if (url.includes('/reviews?')) return json({ items: [], offset: 0, limit: 25, hasMore: false })
    if (url.includes(`/noon-reports/${dailyId}?`)) return json(state.row)
    if (url.includes('/noon-reports?')) return json({ items: [], offset: 0, limit: 25, hasMore: false, canWrite: state.writable, canReview: false, canSubmit: false, offline: dailyCapsFixture('Noon').offline })
    if (url.endsWith('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    return json({}, 403)
  })
  vi.stubGlobal('fetch', fetch); return { state, fetch }
}
beforeEach(() => { history.replaceState(null, '', `/app/noon-report/${dailyVessel}?id=${dailyId}`); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); panel.current = null })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function english() { await userEvent.setup().click(screen.getByRole('button', { name: 'EN' })) }
describe('S5 daily offline parent orchestration (authored; not executed)', () => {
  it('keeps the same resource panel mounted through offline source withdrawal and locks it on known identity mismatch', async () => {
    const { state, fetch } = transport(); render(<DailyReportWorkspace />); await english()
    const region = await screen.findByRole('region', { name: 'Mock encrypted panel orchestration' }), instance = within(region).getByText(/Instance:/).textContent ?? ''
    expect(region).toHaveTextContent('UI lock open; no grant inferred')
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false); act(() => { window.dispatchEvent(new Event('offline')) })
    await screen.findByText(/Current daily sources unavailable while offline/)
    expect(screen.getByRole('region', { name: 'Mock encrypted panel orchestration' })).toHaveTextContent(instance)
    expect(region).toHaveTextContent(`Resource: ${dailyId}`); expect(region).toHaveTextContent('UI lock open; no grant inferred')
    state.actor = dailyId; vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); act(() => { window.dispatchEvent(new Event('online')) })
    await waitFor(() => expect(region).toHaveTextContent('UI locked'))
    expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
  })
  it('does not let a confirmed offline callback clear unrelated ordinary edits or advance their original base version', async () => {
    const { state, fetch } = transport(), user = userEvent.setup(); render(<DailyReportWorkspace />); await english()
    const form = await screen.findByRole('form', { name: 'Daily declaration form' })
    await user.selectOptions(within(form).getByLabelText('Average speed (kn) — Data state'), 'Known'); await user.type(within(form).getByLabelText('Average speed (kn)'), '42.000000000000000000000001')
    await user.click(screen.getByRole('button', { name: 'Simulate pending classified capture' }))
    expect(within(form).getByRole('button', { name: 'Save draft' })).toBeDisabled()
    state.row = { ...state.row, version: 18, revision: 2 }
    await user.click(screen.getByRole('button', { name: 'Simulate confirmed offline receipt' }))
    await screen.findByText(/An offline command was resolved/)
    const reloaded = await screen.findByRole('form', { name: 'Daily declaration form' })
    expect(within(reloaded).getByLabelText('Average speed (kn)')).toHaveValue('42.000000000000000000000001')
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(true)
    state.writeFailure = 409; await user.click(within(reloaded).getByRole('button', { name: 'Save draft' }))
    await screen.findByText(/The record changed/)
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(JSON.parse(call[1]!.body as string)).toMatchObject({ expectedVersion: 17, fields: { avg_speed_kn: { value: '42.000000000000000000000001' } } })
  })
  it('retains only the noncredential draft UUID across restart and does not read an absent saved record or grant automatically', async () => {
    const { fetch } = transport(); const draft = '10000000-0000-4000-8000-000000000044'
    history.replaceState(null, '', `/app/noon-report/${dailyVessel}?draft=${draft}`)
    const first = render(<DailyReportWorkspace />); await english()
    expect(await screen.findByRole('region', { name: 'Mock encrypted panel orchestration' })).toHaveTextContent(`Resource: ${draft}`)
    expect(await screen.findByLabelText('Weather / sea summary — Data state')).toHaveValue('Missing')
    expect(fetch.mock.calls.some(([url]) => url.includes(`/noon-reports/${draft}?`))).toBe(false)
    first.unmount(); render(<DailyReportWorkspace />); await english()
    expect(await screen.findByRole('region', { name: 'Mock encrypted panel orchestration' })).toHaveTextContent(`Resource: ${draft}`)
    expect(screen.getByLabelText('Weather / sea summary')).toHaveValue('')
    expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
  })
  it('blocks a URL-only new draft when current create permission is absent', async () => {
    const { state } = transport(); state.writable = false
    history.replaceState(null, '', `/app/noon-report/${dailyVessel}?draft=${dailyId}`)
    render(<DailyReportWorkspace />); await english()
    expect(await screen.findByText(/A new draft context requires current create permission/)).toBeVisible()
    expect(screen.queryByRole('form')).not.toBeInTheDocument(); expect(screen.queryByRole('region', { name: 'Mock encrypted panel orchestration' })).not.toBeInTheDocument()
  })
})
