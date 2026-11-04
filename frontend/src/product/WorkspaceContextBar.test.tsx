import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { WorkspaceContextBar } from './WorkspaceContextBar'
import { ApiError } from './api'
import { loadWorkspaceContext } from './workspace-context-api'
import type { WorkspaceContext } from './workspace-context-api'
import type { Session } from './directory-contract'

vi.mock('./workspace-context-api', () => ({ loadWorkspaceContext: vi.fn() }))
const tenant = '10000000-0000-0000-0000-000000000001'
const actor = '10000000-0000-0000-0000-000000000002'
const vessel = '10000000-0000-0000-0000-000000000003'
const second = '10000000-0000-0000-0000-000000000004'
const session: Session = { authenticated: true, canRead: true, actorId: actor, tenantId: tenant,
  tenantName: 'FICTIONAL workspace', csrfToken: 'synthetic-only', setupRequired: false,
  canReadCompanies: false, canCreate: false, canSearch: false }
function context(selected = vessel, who = actor): WorkspaceContext {
  return { tenantId: tenant, actorId: who, roleContext: { state: 'Configured', catalogVersion: 'synthetic-v1',
    roles: [{ reference: second, tr: 'KURGUSAL yerel rol', en: 'FICTIONAL local role' }] },
    vessel: { id: selected, code: selected === vessel ? 'FICTIONAL-OLD' : 'FICTIONAL-NEW', name: 'Synthetic vessel', kind: 'Ship' } }
}
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(yes => { resolve = yes })
  return { resolve, promise }
}
describe('shared current workspace context', () => {
  beforeEach(() => { vi.mocked(loadWorkspaceContext).mockResolvedValue(context()) })
  afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks() })
  it('DEV-021 shows a deterministic initial offline state without starting a context read', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    expect(screen.getByRole('status')).toHaveTextContent('Offline · reconnect to load context.')
    expect(screen.queryByText('Loading context…')).not.toBeInTheDocument()
    expect(loadWorkspaceContext).not.toHaveBeenCalled()
  })
  it('DEV-025 keeps hidden-window context paused instead of presenting an endless loading state', () => {
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden')
    render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    expect(screen.getByRole('status')).toHaveTextContent('Context paused while this window is hidden.')
    expect(loadWorkspaceContext).not.toHaveBeenCalled()
  })
  it('shows actual configured role/vessel labels in TR/EN without exposing IDs or credentials', async () => {
    const view = render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    expect(await screen.findByText(/FICTIONAL local role/)).toBeVisible()
    expect(screen.getByText(/FICTIONAL-OLD/)).toBeVisible()
    expect(screen.getByText('FICTIONAL workspace')).toBeVisible()
    expect(view.container).not.toHaveTextContent(second); expect(view.container).not.toHaveTextContent(session.csrfToken)
    view.rerender(<WorkspaceContextBar session={session} vessel={vessel} language="tr" />)
    expect(screen.getByText(/KURGUSAL yerel rol/)).toBeVisible(); expect(loadWorkspaceContext).toHaveBeenCalledTimes(1)
  })
  it('removes old vessel synchronously and ignores a late previous-context response', async () => {
    const old = deferred<WorkspaceContext>(), next = deferred<WorkspaceContext>()
    vi.mocked(loadWorkspaceContext).mockImplementation((_, __, id) => id === vessel ? old.promise : next.promise)
    const view = render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    view.rerender(<WorkspaceContextBar session={session} vessel={second} language="en" />)
    await act(async () => { next.resolve(context(second)); await next.promise })
    expect(await screen.findByText(/FICTIONAL-NEW/)).toBeVisible()
    await act(async () => { old.resolve(context()); await old.promise })
    expect(screen.queryByText(/FICTIONAL-OLD/)).not.toBeInTheDocument()
    expect(screen.getByText(/FICTIONAL-NEW/)).toBeVisible()
  })
  it('hides previous role data immediately when actor changes within the same tenant', async () => {
    const next = deferred<WorkspaceContext>(); const view = render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    await screen.findByText(/FICTIONAL local role/); vi.mocked(loadWorkspaceContext).mockReturnValue(next.promise)
    view.rerender(<WorkspaceContextBar session={{ ...session, actorId: second }} vessel={vessel} language="en" />)
    expect(screen.queryByText(/FICTIONAL local role/)).not.toBeInTheDocument()
    expect(screen.queryByText('FICTIONAL workspace')).not.toBeInTheDocument()
    await act(async () => { next.resolve({ ...context(vessel, second), roleContext: { state: 'Missing', catalogVersion: null, roles: [] } }); await next.promise })
    expect(await screen.findByText('Role context: Not assigned')).toBeVisible()
  })
  it('clears the entire context on logout without requesting data for an anonymous identity', async () => {
    const view = render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    await screen.findByText(/FICTIONAL local role/)
    view.rerender(<WorkspaceContextBar session={null} vessel={vessel} language="en" />)
    expect(screen.queryByRole('region')).not.toBeInTheDocument(); expect(loadWorkspaceContext).toHaveBeenCalledTimes(1)
  })
  it('does not restore cached roles after logout and re-login with the same actor/tenant/vessel', async () => {
    const pending = deferred<WorkspaceContext>()
    const view = render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    await screen.findByText(/FICTIONAL local role/)
    view.rerender(<WorkspaceContextBar session={null} vessel={vessel} language="en" />)
    vi.mocked(loadWorkspaceContext).mockReturnValue(pending.promise)
    view.rerender(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    expect(screen.queryByText(/FICTIONAL local role/)).not.toBeInTheDocument()
    expect(screen.queryByText('FICTIONAL workspace')).not.toBeInTheDocument()
    await act(async () => { pending.resolve({ ...context(), roleContext: { state: 'Unavailable', catalogVersion: null, roles: [] } }); await pending.promise })
    expect(await screen.findByText('Role context: Unavailable')).toBeVisible()
    expect(screen.queryByText(/FICTIONAL local role/)).not.toBeInTheDocument()
  })
  it('revalidates a refreshed session object even when actor/tenant/vessel identifiers are unchanged', async () => {
    const pending = deferred<WorkspaceContext>()
    const view = render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    await screen.findByText(/FICTIONAL local role/)
    vi.mocked(loadWorkspaceContext).mockReturnValue(pending.promise)
    view.rerender(<WorkspaceContextBar session={{ ...session }} vessel={vessel} language="en" />)
    expect(screen.queryByText(/FICTIONAL local role/)).not.toBeInTheDocument()
    await act(async () => { pending.resolve({ ...context(), roleContext: { state: 'Missing', catalogVersion: null, roles: [] } }); await pending.promise })
    expect(await screen.findByText('Role context: Not assigned')).toBeVisible()
    expect(loadWorkspaceContext).toHaveBeenCalledTimes(2)
  })
  it('shows safe access denial and retries explicitly without displaying stale role/workspace data', async () => {
    vi.mocked(loadWorkspaceContext).mockRejectedValueOnce(new ApiError(403))
    const user = userEvent.setup(); render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    expect(await screen.findByText('Context access unavailable.')).toBeVisible()
    expect(screen.queryByText('FICTIONAL workspace')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Retry context' }))
    expect(await screen.findByText(/FICTIONAL local role/)).toBeVisible()
  })
  it.each(['Missing', 'Unavailable'] as const)('keeps %s role context explicit rather than inventing admin/master', async state => {
    vi.mocked(loadWorkspaceContext).mockResolvedValue({ ...context(), roleContext: { state, catalogVersion: null, roles: [] } })
    render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    expect(await screen.findByText(state === 'Missing' ? 'Role context: Not assigned' : 'Role context: Unavailable')).toBeVisible()
    expect(screen.queryByText(/admin|master/i)).not.toBeInTheDocument()
  })
  it('marks disconnected context as potentially stale without claiming sync completion or readiness', async () => {
    render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    await screen.findByText(/FICTIONAL local role/)
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    act(() => { window.dispatchEvent(new Event('offline')) })
    expect(screen.getByText('Offline · context may be stale')).toBeVisible()
    expect(screen.queryByText(/synced|ready|verified/i)).not.toBeInTheDocument()
  })
})
