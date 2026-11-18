import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CertificateAssertionPanel } from './CertificateAssertionPanel'
import { assertionAuthority, assertionCaps, assertionCertificate, assertionReceipt } from './certificate-assertion-test-fixtures'
import { certificateActor, certificateAssignment, certificateVessel } from './certificate-test-fixtures'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
function transport() {
  const state = { actor: certificateActor, canRecord: true, writeStatus: 200, losePostReceiptSession: false, sessionDenied: false, history: false }
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return state.sessionDenied ? json({}, 403) : json({ authenticated: true, canRead: true, actorId: state.actor, tenantId: certificateVessel, csrfToken: 'synthetic-csrf-only' })
    if (url.endsWith('/verification-capabilities')) return json({ ...assertionCaps(), canRecord: state.canRecord, authorities: state.canRecord ? assertionCaps().authorities : [] })
    if (url.includes('/verification-assertions?')) return json({ items: state.history ? [assertionReceipt()] : [], offset: 0, limit: 25, hasMore: false })
    if (url.endsWith('/verification-assertions') && init?.method === 'POST') {
      if (state.writeStatus !== 200) return json({ code: 'command_outcome_withheld' }, state.writeStatus)
      state.history = true
      if (state.losePostReceiptSession) state.sessionDenied = true
      return json(assertionReceipt())
    }
    if (url.endsWith('/certificates/' + assertionCertificate().id)) return json(assertionCertificate())
    return json({}, 403)
  })
  vi.stubGlobal('fetch', fetch); return { state, fetch }
}
const posts = (fetch: ReturnType<typeof transport>['fetch']) => fetch.mock.calls.filter(([, init]) => init?.method === 'POST')
beforeEach(() => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function open() {
  const onDirty = vi.fn(), onSaving = vi.fn(), onRecorded = vi.fn()
  render(<CertificateAssertionPanel vessel={certificateVessel} record={assertionCertificate()} enabled language="en" onDirty={onDirty} onSaving={onSaving} onRecorded={onRecorded} />)
  await screen.findByRole('option', { name: /Assigned Auditor 1/ })
  return { user: userEvent.setup(), onDirty, onSaving, onRecorded }
}
async function fill(user: ReturnType<typeof userEvent.setup>) {
  await user.selectOptions(screen.getByLabelText('Assigned Auditor authority'), assertionAuthority)
  await user.selectOptions(screen.getByLabelText('Human assertion state'), 'Verified')
  await user.type(screen.getByLabelText('Reason and source review'), 'PRIVATE source comparison; human claim remains unassessed')
}
describe('S6 exact human assertion workflow — authored, not run', () => {
  it('keeps absent appointments unavailable without manufacturing authority or an approved history', async () => {
    const { state, fetch } = transport(); state.canRecord = false
    render(<CertificateAssertionPanel vessel={certificateVessel} record={assertionCertificate()} enabled language="en" onDirty={vi.fn()} onSaving={vi.fn()} onRecorded={vi.fn()} />)
    expect(await screen.findByText(/No current assigned human authority/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Record unassessed assertion' })).toBeDisabled()
    expect(screen.getByText('No authorized assertion rows returned on this page.')).toBeVisible()
    expect(posts(fetch)).toHaveLength(0)
  })
  it('requires explicit authority, source claim and reason, then records only the unassessed immutable result', async () => {
    const { fetch } = transport(), { user, onRecorded } = await open()
    await user.click(screen.getByRole('button', { name: 'Record unassessed assertion' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Choose an assigned authority')
    expect(posts(fetch)).toHaveLength(0)
    await fill(user)
    expect(posts(fetch)).toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Record unassessed assertion' }))
    await waitFor(() => expect(onRecorded).toHaveBeenCalledWith(assertionReceipt()))
    expect(JSON.parse(posts(fetch)[0][1]!.body as string)).toMatchObject({ expectedVersion: 19, certificateRevision: 1, expectedAssertionVersion: 0, authorityId: assertionAuthority, state: 'Verified' })
    expect((await screen.findAllByText('Human assertion: Verified'))[0]).toBeVisible()
    expect(screen.getAllByText(/does not establish legal validity/).length).toBeGreaterThan(0)
  })
  it('retains the original unknown command across a later403 and reauthorization, with no automatic retry', async () => {
    const { state, fetch } = transport(); state.writeStatus = 503
    const { user, onSaving } = await open(); await fill(user)
    await user.click(screen.getByRole('button', { name: 'Record unassessed assertion' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Assertion outcome unknown')
    const first = posts(fetch)[0][1]!
    state.writeStatus = 403
    await user.click(screen.getByRole('button', { name: 'Retry exact assertion command' }))
    await screen.findByText(/Human assertion source or access unavailable/)
    expect(posts(fetch)).toHaveLength(2); expect(onSaving).toHaveBeenLastCalledWith(true)
    state.writeStatus = 200
    await user.click(screen.getByRole('button', { name: 'Reauthorize assertion source' }))
    await user.click(await screen.findByRole('button', { name: 'Retry exact assertion command' }))
    await waitFor(() => expect(posts(fetch)).toHaveLength(3))
    for (const [, init] of posts(fetch)) { expect(init!.body).toBe(first.body); expect(init!.headers).toEqual(first.headers) }
  })
  it('hides retained private reason on an actor change and restores only the original owner before exact retry', async () => {
    const { state, fetch } = transport(); state.writeStatus = 503
    const { user } = await open(); await fill(user)
    await user.click(screen.getByRole('button', { name: 'Record unassessed assertion' })); await screen.findByRole('button', { name: 'Retry exact assertion command' })
    state.actor = certificateAssignment; act(() => window.dispatchEvent(new Event('focus')))
    await screen.findByText(/Assertion access is locked/)
    expect(screen.queryByDisplayValue('PRIVATE source comparison; human claim remains unassessed')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Retry exact assertion command' })).not.toBeInTheDocument()
    expect(posts(fetch)).toHaveLength(1)
    state.actor = certificateActor; state.writeStatus = 200; act(() => window.dispatchEvent(new Event('focus')))
    await user.click(await screen.findByRole('button', { name: 'Retry exact assertion command' }))
    await waitFor(() => expect(posts(fetch)).toHaveLength(2))
    expect(posts(fetch)[1][1]!.body).toBe(posts(fetch)[0][1]!.body)
  })
  it('keeps a command frozen when the receipt arrives but the following session read is denied', async () => {
    const { state, fetch } = transport(); state.losePostReceiptSession = true
    const { user, onRecorded, onSaving } = await open(); await fill(user)
    await user.click(screen.getByRole('button', { name: 'Record unassessed assertion' }))
    await screen.findByText(/Human assertion source or access unavailable/)
    expect(posts(fetch)).toHaveLength(1); expect(onRecorded).not.toHaveBeenCalled(); expect(onSaving).toHaveBeenLastCalledWith(true)
    state.sessionDenied = false; state.losePostReceiptSession = false
    await user.click(screen.getByRole('button', { name: 'Reauthorize assertion source' }))
    await user.click(await screen.findByRole('button', { name: 'Retry exact assertion command' }))
    await waitFor(() => expect(onRecorded).toHaveBeenCalledWith(assertionReceipt()))
    expect(posts(fetch)[1][1]!.body).toBe(posts(fetch)[0][1]!.body)
    expect(posts(fetch)[1][1]!.headers).toEqual(posts(fetch)[0][1]!.headers)
  })
})
