import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CertificateWorkspace } from './CertificateWorkspace'
import { certificateActor, certificateAssignment, certificateCapsFixture, certificateFixture, certificatePageFixture, certificateVessel } from './certificate-test-fixtures'
import type { CertificateDto } from './certificates-api'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
function transport() {
  const state = { actor: certificateActor, writable: true, writeStatus: 200, crew: false, readBarrier: null as Promise<void> | null }
  const records = new Map<string, CertificateDto>([[certificateFixture().id, certificateFixture()]])
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return json({ authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, actorId: state.actor, tenantId: certificateVessel, csrfToken: 'synthetic-certificate-csrf' })
    if (init?.method === 'POST' || init?.method === 'PUT') {
      if (state.writeStatus !== 200) return json({ code: 'command_outcome_withheld' }, state.writeStatus)
      const body = JSON.parse(init.body as string), actual = { ...certificateFixture(), id: body.id, content: body.content, version: 20, revision: 2, href: '/app/certificate-command-center/' + certificateVessel + '?id=' + body.id }
      records.set(actual.id, actual)
      return json({ ...actual, currentRevision: false, canEdit: false })
    }
    if (url.includes('/certificate-command-center/capabilities?')) return json({ ...certificateCapsFixture(), canWriteVessel: state.writable, canReadCrew: state.crew, crewPurpose: state.crew ? 'synthetic-certificate-purpose' : null, scopes: state.crew ? ['Vessel', 'Crew', 'All'] : ['Vessel'] })
    if (url.includes('/certificate-command-center?')) {
      const query = new URL(url, 'https://synthetic.invalid').searchParams
      if (query.get('scope') === 'Crew') {
        const row = certificateFixture()
        const crew = { ...row, scope: 'Crew', assignmentId: certificateAssignment, personId: '10000000-0000-4000-8000-000000000077', content: { ...row.content, documentName: null, metadata: { ...row.content.metadata, declaration: { ...row.content.metadata.declaration, certificateNumber: 'SYN-CREW-DECL' } } }, savedAt: null, hash: null, canEdit: false, href: '/app/crew-roster/' + certificateVessel + '/crew-profile/' + certificateAssignment }
        return json({ ...certificatePageFixture(), items: [crew], scope: 'Crew', crewCoverage: 'AVAILABLE' })
      }
      return json(certificatePageFixture([...records.values()]))
    }
    if (url.endsWith('/files?offset=0')) return json({ items: [], offset: 0, hasMore: false, available: true, canWrite: false, maxBytes: 1048576, allowedTypes: ['application/pdf'] })
    const row = [...records.values()].find(record => url.includes('/certificates/' + record.id))
    if (row && url.includes('/history?')) return json({ items: [{ ...row, canEdit: false }], offset: 0, limit: 25, hasMore: false })
    if (row) { await state.readBarrier; return json({ ...row, canEdit: state.writable }) }
    if (url.endsWith('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    return json({}, 403)
  })
  vi.stubGlobal('fetch', fetch); return { state, fetch }
}
beforeEach(() => { history.replaceState(null, '', '/app/certificate-command-center/' + certificateVessel); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function english() { await userEvent.setup().click(screen.getByRole('button', { name: 'EN' })) }
describe('T-S6-001 source-owned center interactions (authored; not executed)', () => {
  it('saves an incomplete owning declaration with exact planning text and rereads current head after immutable receipt', async () => {
    const { fetch } = transport(), user = userEvent.setup(); render(<CertificateWorkspace />); await english()
    await user.click(await screen.findByRole('button', { name: 'New certificate draft' }))
    await user.type(screen.getByLabelText('Declared issuer'), 'Synthetic unverified issuer')
    await user.type(screen.getByLabelText('Declared renewal lead days'), '9007199254740993.123456789')
    expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Save unverified draft' }))
    await screen.findByText(/Draft outcome recorded/)
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'POST')!, body = JSON.parse(call[1]!.body as string)
    expect(body).toMatchObject({ expectedVersion: 0, reasonCode: 'INITIAL_AUTHORING', content: { metadata: { renewalLeadDaysNumeric: '9007199254740993.123456789', stcwReference: null, declaration: { issuer: 'Synthetic unverified issuer', issueDate: null, expiryDate: null } }, document: null } })
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url.endsWith('/certificates/' + body.id))).toBe(true))
    expect(call[1]).toMatchObject({ credentials: 'same-origin', headers: { 'X-CSRF-TOKEN': 'synthetic-certificate-csrf', 'Idempotency-Key': expect.any(String) } })
  })
  it('protects dirty cancel/keep navigation and beforeunload without clearing a declaration', async () => {
    transport(); const user = userEvent.setup(); render(<CertificateWorkspace />); await english()
    await user.click(await screen.findByRole('button', { name: 'New certificate draft' }))
    await user.type(screen.getByLabelText('Certificate number'), 'PRIVATE local declaration')
    await user.click(screen.getByRole('button', { name: 'Back to records' }))
    await user.click(screen.getByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('Certificate number')).toHaveValue('PRIVATE local declaration')
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(true)
  })
  it('hides private retained fields and prevents unknown command transfer after a live identity change', async () => {
    const { state, fetch } = transport(); state.writeStatus = 503
    const user = userEvent.setup(); render(<CertificateWorkspace />); await english()
    await user.click(await screen.findByRole('button', { name: 'New certificate draft' }))
    await user.type(screen.getByLabelText('Certificate number'), 'PRIVATE retained unknown body')
    await user.click(screen.getByRole('button', { name: 'Save unverified draft' }))
    await screen.findByText(/Outcome unknown/)
    expect(screen.getByLabelText('Certificate number')).toBeDisabled()
    state.actor = certificateAssignment; act(() => { window.dispatchEvent(new Event('focus')) })
    await screen.findByText(/Retained inputs belong to another verified identity/)
    expect(screen.queryByDisplayValue('PRIVATE retained unknown body')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Retry exact certificate command' })).not.toBeInTheDocument()
    expect(fetch.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
  })
  it('rejects a delayed source response when the identity changes before the projection is published', async () => {
    history.replaceState(null, '', '/app/certificate-command-center/' + certificateVessel + '?id=' + certificateFixture().id)
    const { state, fetch } = transport()
    let release = () => {}
    state.readBarrier = new Promise<void>(resolve => { release = resolve })
    render(<CertificateWorkspace />); await english()
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url.endsWith('/certificates/' + certificateFixture().id))).toBe(true))
    state.actor = certificateAssignment
    await act(async () => release())
    await screen.findByText(/Certificate source or access unavailable/)
    expect(screen.queryByLabelText('Certificate number')).not.toBeInTheDocument()
    expect(screen.queryByDisplayValue('Synthetic certificate declaration')).not.toBeInTheDocument()
  })
  it('uses only current purpose capability for protected crew scope and never fetches a broad person record', async () => {
    const { state, fetch } = transport(); state.crew = true
    const user = userEvent.setup(); render(<CertificateWorkspace />); await english()
    await user.selectOptions(await screen.findByLabelText('Certificate scope'), 'Crew')
    expect(await screen.findByRole('button', { name: 'SYN-CREW-DECL' })).toBeVisible()
    expect(fetch.mock.calls.some(([url]) => url.includes('scope=Crew') && url.includes('purpose=synthetic-certificate-purpose'))).toBe(true)
    expect(fetch.mock.calls.some(([url]) => /\/people|\/persons|personId=/.test(url))).toBe(false)
    expect(screen.queryByText('10000000-0000-4000-8000-000000000077')).not.toBeInTheDocument()
  })
})
