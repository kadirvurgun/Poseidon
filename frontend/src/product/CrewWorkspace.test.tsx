import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CrewWorkspace } from './CrewWorkspace'

const vessel = '11111111-1111-1111-1111-111111111111'
const id = '22222222-2222-2222-2222-222222222222'
const row = { id, vesselId: vessel, personId: id, version: 7, revision: 1, positionId: null,
  rank: null, capacity: null, department: null, from: null, to: null, contractEndDate: null,
  watchGroup: null, assignmentStatus: 'Draft', provenance: 'SyntheticUnverified', fullName: null,
  employeeNo: null, privacyScope: 'Withheld', sourceObservedAt: null, eligibilityState: 'Unknown' }
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
const fetcher = vi.fn()
let mutation: (init: RequestInit) => Promise<Response>
beforeEach(() => {
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  mutation = async () => json({ id, version: 8, revision: 2, replayed: false })
  fetcher.mockImplementation(async (path: string, init?: RequestInit) => {
    if (init?.method === 'POST' || init?.method === 'PUT') return mutation(init)
    if (path.endsWith('/auth/session')) return json({ authenticated: true, tenantId: vessel, actorId: id, csrfToken: 'synthetic-csrf' })
    if (path.endsWith('/positions') || path.endsWith('/certificates')) return json([])
    if (path.endsWith(`/${id}`)) return json({ assignment: row, personVersion: 12, employmentState: 'Unknown', fitnessState: 'Unknown', contactStatus: 'Withheld', healthStatus: 'Withheld' })
    return json({ items: [row], offset: 0, hasMore: false, canWrite: true, canWriteIdentity: true })
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset() })
const english = async () => { await userEvent.click(screen.getByRole('button', { name: 'EN', exact: true })); await screen.findByRole('button', { name: 'Add person and assignment' }) }

describe('CREW-ROSTER-01 authored component behavior; no backend execution claim', () => {
  it('renders server-withheld identity and Unknown operational values without readiness approval', async () => {
    render(<CrewWorkspace vessel={vessel} />); await english()
    expect(screen.getByRole('link', { name: 'Withheld' })).toHaveAttribute('href', `/app/crew-roster/${vessel}/crew-profile/${id}`)
    expect(screen.getByText('Draft · unverified')).toBeVisible()
    expect(screen.getAllByText('Unknown').length).toBeGreaterThan(1)
    expect(screen.queryByText('READY')).not.toBeInTheDocument()
  })
  it('denies direct profile without leaking a list or identifiers', async () => {
    fetcher.mockImplementation(async (path: string) => path.endsWith('/auth/session') ? json({ authenticated: true }) : json({ code: 'permission_denied' }, 403))
    render(<CrewWorkspace vessel={vessel} assignment={id} />)
    await screen.findByText('Mürettebat erişimi reddedildi.')
    expect(screen.queryByRole('table')).not.toBeInTheDocument(); expect(screen.queryByText(id)).not.toBeInTheDocument()
  })
  it('freezes an uncertain attempt and retries identical UUID, payload and key', async () => {
    let attempts = 0
    mutation = async () => { if (attempts++ === 0) throw new TypeError('synthetic network loss'); return json({ id, version: 8, revision: 2 }) }
    render(<CrewWorkspace vessel={vessel} />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add person and assignment' }))
    await userEvent.type(screen.getByLabelText('Full name'), 'FICTIONAL test person')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByText('Result uncertain. Retry the same attempt before editing.')
    expect(screen.getByLabelText('Full name')).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Retry', exact: true }))
    await screen.findByText('Draft saved.')
    const calls = fetcher.mock.calls.filter(([, init]) => init?.method === 'POST')
    expect(calls).toHaveLength(2); expect(calls[0][1].body).toEqual(calls[1][1].body)
    expect(calls[0][1].headers['Idempotency-Key']).toEqual(calls[1][1].headers['Idempotency-Key'])
  })
  it('does not overwrite a conflict and requires explicit discard/reload', async () => {
    mutation = async () => json({ code: 'version_conflict' }, 409)
    render(<CrewWorkspace vessel={vessel} />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add person and assignment' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByText('Conflict. Reload the current assignment and resolve the changes.')
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Discard and reload' }))
    await screen.findByRole('button', { name: 'Add person and assignment' })
    expect(fetcher.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
  })
  it('blocks unsupported offline capture explicitly and stores no browser cache or credentials', async () => {
    render(<CrewWorkspace vessel={vessel} />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add person and assignment' }))
    Object.defineProperty(navigator, 'onLine', { value: false, configurable: true }); fireEvent(window, new Event('offline'))
    await screen.findByText('Offline read/capture is unavailable pending the crew field and device policy. This form requires a connection.')
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeDisabled()
    expect(fetcher.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(0)
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
  })
  it('clears a revoked projection on reauthentication failure', async () => {
    render(<CrewWorkspace vessel={vessel} />); await english()
    fetcher.mockImplementation(async () => json({ code: 'authentication_required' }, 401))
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true }); fireEvent(document, new Event('visibilitychange'))
    await waitFor(() => expect(screen.queryByRole('table')).not.toBeInTheDocument())
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true }); fireEvent(document, new Event('visibilitychange'))
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Session expired. Sign in again.'))
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
