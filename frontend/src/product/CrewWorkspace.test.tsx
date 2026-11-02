import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CrewWorkspace } from './CrewWorkspace'

const vessel = '11111111-1111-1111-1111-111111111111'
const id = '22222222-2222-2222-2222-222222222222'
const row = { id, vesselId: vessel, personId: id, version: 7, revision: 1, positionId: null,
  rank: null, capacity: null, department: null, from: null, to: null, contractEndDate: null,
  watchGroup: null, assignmentStatus: 'Draft', provenance: 'SyntheticUnverified', fullName: null,
  employeeNo: null, privacyScope: 'Withheld', sourceObservedAt: null, eligibilityState: 'Unknown', workRestState: 'Unknown', certificateState: 'Unknown', competenceState: 'Unknown', contactStatus: 'Unavailable', nationality: null, nationalityState: 'Withheld', presenceState: 'Draft', presenceReason: 'incomplete_declaration', positionState: 'Missing' }
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
const fetcher = vi.fn()
let mutation: (init: RequestInit) => Promise<Response>
beforeEach(() => {
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  mutation = async () => json({ id, version: 8, revision: 2, replayed: false })
  fetcher.mockImplementation(async (path: string, init?: RequestInit) => {
    if (init?.method === 'POST' || init?.method === 'PUT') return mutation(init)
    if (path.endsWith('/auth/session')) return json({ authenticated: true, tenantId: vessel, actorId: id, csrfToken: 'synthetic-csrf' })
    if (path.endsWith('/current-assignment')) return json({ state: 'PolicyUnavailable' })
    if (path.endsWith('/personal-files')) return json({ code: 'permission_denied' }, 404)
    if (path.includes('/personal-profile')) return json({ code: 'field_policy_denied' }, 403)
    if (path.endsWith('/position-policy')) return json({ state: 'Unavailable', version: null, items: [] })
    if (path.endsWith('/positions') || path.endsWith('/certificates')) return json([])
    if (path.endsWith(`/${id}`)) return json({ assignment: row, personVersion: 12, identityStatus: 'Withheld', employmentState: 'Unknown', fitnessState: 'Unknown', contactStatus: 'Withheld', healthStatus: 'Withheld' })
    return json({ items: [row], offset: 0, hasMore: false, canWrite: true, canWriteIdentity: true, identityPolicy: { version: 'synthetic-v1', purpose: 'synthetic', employeeNumberMode: 'MutableDeclaration' } })
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset() })
const english = async () => { await userEvent.click(screen.getByRole('button', { name: 'EN' })); await screen.findByRole('button', { name: 'Add person and assignment' }) }

describe('CREW-ROSTER-01 authored component behavior; no backend execution claim', () => {
  it('shows server onboard and planned declarations without promoting them to eligibility', async () => {
    const previous = fetcher.getMockImplementation()!
    fetcher.mockImplementation(async (path: string, init?: RequestInit) => path.includes('/crew?offset=') ? json({
      items: [{ ...row, presenceState: 'OnboardDeclaration', presenceReason: 'unverified_onboard_declaration' },
        { ...row, id: vessel, presenceState: 'PlannedDeclaration', presenceReason: 'unverified_planned_declaration' }],
      offset: 0, hasMore: false, canWrite: false, canWriteIdentity: false,
    }) : previous(path, init))
    render(<CrewWorkspace vessel={vessel} />)
    await userEvent.click(screen.getByRole('button', { name: 'EN' }))
    expect(await screen.findByText('Onboard · unverified declaration')).toBeVisible()
    expect(screen.getByText('Planned · unverified declaration')).toBeVisible()
    expect(screen.queryByText('ELIGIBLE')).not.toBeInTheDocument()
  })
  it('rejects a malformed response that exposes a stale rank', async () => {
    const previous = fetcher.getMockImplementation()!
    fetcher.mockImplementation(async (path: string, init?: RequestInit) => path.includes('/crew?offset=') ? json({
      items: [{ ...row, positionState: 'Stale', rank: 'SYNTHETIC INVALID DISCLOSURE' }], offset: 0, hasMore: false, canWrite: false, canWriteIdentity: false,
    }) : previous(path, init))
    render(<CrewWorkspace vessel={vessel} />)
    await userEvent.click(screen.getByRole('button', { name: 'EN' }))
    expect(await screen.findByText('Crew service unavailable.')).toBeVisible()
    expect(screen.queryByText('SYNTHETIC INVALID DISCLOSURE')).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
  it('requires explicit conversion for legacy identity and retries an uncertain conversion without sending identity values', async () => {
    const previous = fetcher.getMockImplementation()!
    fetcher.mockImplementation(async (path: string, init?: RequestInit) => path.endsWith(`/${id}`) && !init?.method
      ? json({ employmentState: 'Unknown', fitnessState: 'Unknown', identityStatus: 'LegacyUnavailable', contactStatus: 'Withheld', healthStatus: 'Withheld', assignment: { ...row, privacyScope: 'LegacyUnavailable' }, personVersion: 12, canConvertIdentity: true,
        identityPolicy: { version: 'synthetic-v1', purpose: 'synthetic', employeeNumberMode: 'ImmutableAfterCapture' } }) : previous(path, init))
    let count = 0; mutation = async () => { if (count++ === 0) throw new TypeError('synthetic interrupted conversion'); return json({ version: 13 }) }
    render(<CrewWorkspace vessel={vessel} assignment={id} />)
    await userEvent.click(screen.getByRole('button', { name: 'EN' }))
    const convert = await screen.findByRole('button', { name: 'Convert legacy identity to protected storage' })
    expect(screen.getByRole('button', { name: 'Edit person' })).toBeDisabled()
    await userEvent.click(convert)
    expect(screen.queryByLabelText('Full name')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByText('Result uncertain. Retry the same attempt before editing.')
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    await screen.findByText('Draft saved.')
    const calls = fetcher.mock.calls.filter(([path]) => path.endsWith('/person/convert'))
    expect(calls).toHaveLength(2); expect(calls[0][1].body).toEqual(calls[1][1].body); expect(calls[0][1].headers).toEqual(calls[1][1].headers)
    expect(JSON.parse(calls[0][1].body)).toEqual({ baseVersion: 12, policyVersion: 'synthetic-v1', purpose: 'synthetic' })
  })
  it('keeps configured immutable employee identity while allowing ordinary name changes with the exact policy', async () => {
    const previous = fetcher.getMockImplementation()!
    fetcher.mockImplementation(async (path: string, init?: RequestInit) => path.endsWith(`/${id}`) && !init?.method
      ? json({ employmentState: 'Unknown', fitnessState: 'Unknown', identityStatus: 'IdentityPermitted', contactStatus: 'Withheld', healthStatus: 'Withheld', assignment: { ...row, fullName: 'FICTIONAL original', employeeNo: '0001', privacyScope: 'IdentityPermitted' }, personVersion: 12,
        identityPolicy: { version: 'synthetic-v1', purpose: 'synthetic', employeeNumberMode: 'ImmutableAfterCapture' } }) : previous(path, init))
    render(<CrewWorkspace vessel={vessel} assignment={id} />)
    await userEvent.click(screen.getByRole('button', { name: 'EN' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Edit person' }))
    expect(screen.getByLabelText('Employee number')).toBeDisabled()
    await userEvent.clear(screen.getByLabelText('Full name')); await userEvent.type(screen.getByLabelText('Full name'), 'FICTIONAL renamed')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByText('Draft saved.')
    const body = JSON.parse(fetcher.mock.calls.find(([, init]) => init?.method === 'PUT')![1].body)
    expect(body).toEqual({ baseVersion: 12, fullName: 'FICTIONAL renamed', employeeNo: '0001', policyVersion: 'synthetic-v1', purpose: 'synthetic' })
  })
  it('shows controlled lookup unavailability while retaining an explicit Unknown choice for an incomplete draft', async () => {
    render(<CrewWorkspace vessel={vessel} />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add person and assignment' }))
    expect(screen.getByText('Controlled position lookup unavailable. An incomplete draft may keep position unknown.')).toBeVisible()
    expect(screen.getByLabelText('Position / rank / capacity')).toHaveValue('')
    expect(screen.getAllByRole('option').map(o => o.textContent)).toEqual(['Unknown'])
  })
  it('selects a server configured position tuple with its department and freezes the exact lookup version in the command', async () => {
    const previous = fetcher.getMockImplementation()!
    fetcher.mockImplementation(async (path: string, init?: RequestInit) => path.endsWith('/position-policy')
      ? json({ state: 'ConfiguredUnverified', version: 'synthetic-position-v7', items: [{ id, code: 'SYNTHETIC', rank: 'SYNTHETIC-R', capacity: null, department: 'SYNTHETIC-D', provenance: 'SyntheticUnverified' }] }) : previous(path, init))
    render(<CrewWorkspace vessel={vessel} />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add person and assignment' }))
    await userEvent.selectOptions(screen.getByLabelText('Position / rank / capacity'), id)
    expect(screen.getByRole('option', { name: 'SYNTHETIC · SYNTHETIC-R · Unknown · SYNTHETIC-D' })).toBeVisible()
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByText('Draft saved.')
    const body = JSON.parse(fetcher.mock.calls.find(([, init]) => init?.method === 'POST')![1].body)
    expect(body.positionId).toBe(id); expect(body.positionPolicyVersion).toBe('synthetic-position-v7')
  })
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
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
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
    await screen.findByText('Ordinary forms require a connection. Use Durable offline drafts for explicitly configured capture.')
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
