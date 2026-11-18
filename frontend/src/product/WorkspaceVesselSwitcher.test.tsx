import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, vessels } from './api'
import { loadWorkspaceContext } from './workspace-context-api'
import { WorkspaceVesselSwitcher, vesselContextHref } from './WorkspaceVesselSwitcher'
import type { Session } from './directory-contract'
import type { WorkspaceContext } from './workspace-context-api'

vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), vessels: vi.fn() }))
vi.mock('./workspace-context-api', () => ({ loadWorkspaceContext: vi.fn() }))
const tenant = '10000000-0000-0000-0000-000000000001', actor = '10000000-0000-0000-0000-000000000002'
const first = '30000000-0000-0000-0000-000000000001', second = '30000000-0000-0000-0000-000000000002'
const session: Session = { authenticated: true, canRead: true, actorId: actor, tenantId: tenant, tenantName: 'FICTIONAL workspace',
  csrfToken: 'synthetic-only', setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false }
const rows = [first, second].map((id, i) => ({ id, companyId: tenant, code: `SYN-${i + 1}`, name: `FICTIONAL vessel ${i + 1}`, kind: 'Ship' as const, version: 1, canEdit: false }))
const page = { items: rows, offset: 0, limit: 25, hasMore: false }
const context = (): WorkspaceContext => ({ tenantId: tenant, actorId: actor, roleContext: { state: 'Missing', roles: [], catalogVersion: null }, vessel: { ...rows[1] } })
const props = { session, vessel: first, language: 'en' as const, onClose: vi.fn(), onNavigate: vi.fn() }
const target = () => screen.getByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' })
function pending<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(yes => { resolve = yes }); return { resolve, promise } }
beforeEach(() => { vi.mocked(vessels).mockResolvedValue(page); vi.mocked(loadWorkspaceContext).mockResolvedValue(context()); history.replaceState(null, '', `/app/today/${first}`) })
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

describe('DEV-021 / T-S2-002 authorized vessel context switch; authored, execution deferred', () => {
  it('rechecks the selected vessel and waits for the existing dirty-navigation guard to commit before closing', async () => {
    const next = pending<WorkspaceContext>(); vi.mocked(loadWorkspaceContext).mockReturnValue(next.promise)
    const user = userEvent.setup(); render(<WorkspaceVesselSwitcher {...props} />)
    await user.click(await screen.findByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' }))
    expect(loadWorkspaceContext).toHaveBeenCalledWith(tenant, actor, second, expect.any(AbortSignal))
    expect(screen.getByRole('status')).toHaveTextContent('Checking vessel access')
    expect(target()).toBeDisabled(); expect(props.onNavigate).not.toHaveBeenCalled()
    await act(async () => { next.resolve(context()); await next.promise })
    expect(props.onNavigate).toHaveBeenCalledWith(`/app/today/${second}`, expect.any(Function))
    expect(props.onClose).not.toHaveBeenCalled()
    expect(screen.getByRole('dialog', { name: 'Change vessel context' })).toBeVisible()
    await act(async () => props.onNavigate.mock.calls[0][1]())
    expect(props.onClose).toHaveBeenCalledTimes(1)
  })
  it.each([403, 404, 502])('keeps the current context on target denial/invalid projection %s and permits explicit retry', async status => {
    vi.mocked(loadWorkspaceContext).mockRejectedValueOnce(new ApiError(status))
    const user = userEvent.setup(); render(<WorkspaceVesselSwitcher {...props} />)
    await user.click(await screen.findByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Vessel access could not be confirmed')
    expect(props.onNavigate).not.toHaveBeenCalled(); expect(props.onClose).not.toHaveBeenCalled()
    expect(window.location.pathname).toBe(`/app/today/${first}`)
    await user.click(target()); await waitFor(() => expect(props.onNavigate).toHaveBeenCalledTimes(1))
  })
  it('does not navigate from a late grant belonging to the previous actor', async () => {
    const next = pending<WorkspaceContext>(); vi.mocked(loadWorkspaceContext).mockReturnValue(next.promise)
    const user = userEvent.setup(); const view = render(<WorkspaceVesselSwitcher {...props} />)
    await user.click(await screen.findByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' }))
    view.rerender(<WorkspaceVesselSwitcher {...props} session={{ ...session, actorId: second }} />)
    await act(async () => { next.resolve(context()); await next.promise })
    expect(props.onNavigate).not.toHaveBeenCalled(); expect(props.onClose).not.toHaveBeenCalled()
  })
  it('does not request vessels offline and withdraws an in-flight switch when disconnected', async () => {
    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    const next = pending<WorkspaceContext>(); vi.mocked(loadWorkspaceContext).mockReturnValue(next.promise)
    const user = userEvent.setup(); render(<WorkspaceVesselSwitcher {...props} />)
    expect(screen.getByRole('status')).toHaveTextContent('Offline. Reconnect')
    expect(vessels).not.toHaveBeenCalled(); expect(screen.getByRole('button', { name: 'Search vessels' })).toBeDisabled()
    online.mockReturnValue(true); act(() => window.dispatchEvent(new Event('online')))
    await user.click(await screen.findByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' }))
    online.mockReturnValue(false); act(() => window.dispatchEvent(new Event('offline')))
    await act(async () => { next.resolve(context()); await next.promise })
    expect(screen.queryByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' })).not.toBeInTheDocument()
    expect(props.onNavigate).not.toHaveBeenCalled()
  })
  it('submits a labeled bounded server search instead of searching a cached whole fleet', async () => {
    const user = userEvent.setup(); render(<WorkspaceVesselSwitcher {...props} />)
    await screen.findByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' })
    await user.type(screen.getByRole('searchbox', { name: 'Vessel name or code' }), ' SYN-2 ')
    await user.click(screen.getByRole('button', { name: 'Search vessels' }))
    await waitFor(() => expect(vessels).toHaveBeenLastCalledWith(0, '', expect.any(AbortSignal), 'SYN-2'))
    expect(screen.getByRole('button', { name: 'SYN-1 · FICTIONAL vessel 1 · Ship' })).toBeDisabled()
  })
  it('rejects malformed or unbounded vessel pages without exposing navigation controls', async () => {
    vi.mocked(vessels).mockResolvedValue({ ...page, items: [{ ...rows[1], id: 'untrusted/path' }] })
    render(<WorkspaceVesselSwitcher {...props} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Vessels could not be loaded')
    expect(screen.queryByRole('button', { name: /SYN-2/ })).not.toBeInTheDocument()
  })
  it.each([
    ['/app/today/' + first, 'Ship', '/app/today/' + second],
    ['/app/crew-roster/' + first + '/old-person', 'Ship', '/app/crew-roster/' + second],
    ['/app/maintenance-plan/' + first, 'Ship', '/app/maintenance-plan/' + second],
    ['/app/tug-command', 'Tug', '/app/tug-command?vessel=' + second],
    ['/app/tug-command', 'Ship', '/app/fleet/' + second],
    ['/app/tug-job-detail/old-job', 'Tug', '/app/fleet/' + second],
  ] as const)('keeps compatible workspace context without carrying old record references: %s', (path, kind, expected) => {
    expect(vesselContextHref(path, second, kind)).toBe(expected)
  })
})
