import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from './api'
import { RegulatoryProfile } from './RegulatoryProfile'
import * as api from './profile-api'
import { profileFields } from './profile-contract'
import type { Profile, ProfileContent, ProfileHistory } from './profile-contract'

vi.mock('./file-api', () => ({ filePage: vi.fn(async () => ({ items: [], offset: 0, hasMore: false, available: true, canWrite: false, maxBytes: 0, allowedTypes: [] })) }))

vi.mock('./profile-api', async original => ({ ...await original<typeof import('./profile-api')>(), loadProfile: vi.fn(), loadProfileHistory: vi.fn(), saveProfileAttempt: vi.fn(), loadProfileSession: vi.fn() }))

const firstId = '10000000-0000-0000-0000-000000000001'
const nextId = '10000000-0000-0000-0000-000000000002'
const content = { ...Object.fromEntries(profileFields.map(([key]) => [key, { state: 'Missing', value: null }])), constructionRecord: null } as ProfileContent
function profile(id = firstId): Profile {
  return { tenantId: '10000000-0000-0000-0000-000000000003', vessel: { id, companyId: nextId,
    code: id === firstId ? 'FICTIONAL-OLD' : 'FICTIONAL-NEW', name: 'Synthetic vessel', kind: 'Ship', canEdit: true, version: 1 },
    version: 1, revision: 1, savedAt: '2026-11-01T00:00:00Z', provenance: 'DeclaredUnverified',
    content: { ...content, sourceReference: { state: 'Known', value: id === firstId ? 'FICTIONAL OLD SOURCE' : 'FICTIONAL NEW SOURCE' } } }
}
const history: ProfileHistory = { items: [{ revision: 1, savedAt: '2026-11-01T00:00:00Z', provenance: 'DeclaredUnverified',
  content: { ...content, sourceReference: { state: 'Known', value: 'FICTIONAL OLD HISTORY' } } }], offset: 0, limit: 25, hasMore: false }
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (error: unknown) => void
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no })
  return { promise, resolve, reject }
}
const props = () => ({ id: firstId, language: 'en' as const, csrf: 'synthetic-csrf', onDirty: vi.fn(), onSaving: vi.fn() })

describe('regulatory profile current-access and context privacy', () => {
  beforeEach(() => {
    vi.mocked(api.loadProfile).mockImplementation(async id => profile(id))
    vi.mocked(api.loadProfileHistory).mockResolvedValue(history)
    vi.mocked(api.loadProfileSession).mockResolvedValue({ owner: profile().tenantId+'/10000000-0000-0000-0000-000000000004', tenant: profile().tenantId, csrf: crypto.randomUUID() })
    vi.mocked(api.saveProfileAttempt).mockImplementation(async command => ({ ...profile(), content: JSON.parse(command.body).content, revision: command.nextRevision, version: 2 }))
  })
  afterEach(() => { vi.resetAllMocks() })

  it.each([401, 403, 404])('withdraws form and history after save access denial %s', async status => {
    vi.mocked(api.saveProfileAttempt).mockRejectedValue(new ApiError(status))
    const user = userEvent.setup(); const callbacks = props()
    render(<RegulatoryProfile {...callbacks} />)
    await screen.findByDisplayValue('FICTIONAL OLD SOURCE'); await screen.findByText('FICTIONAL OLD HISTORY')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByRole('alert')
    expect(screen.queryByDisplayValue('FICTIONAL OLD SOURCE')).not.toBeInTheDocument()
    expect(screen.queryByText('FICTIONAL OLD HISTORY')).not.toBeInTheDocument()
    expect(screen.queryByText(/FICTIONAL-OLD/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument()
    // A definite first denial releases the command, retaining an UNSENT local
    // draft privately until its original owner proves a local discard.
    expect(callbacks.onDirty).toHaveBeenLastCalledWith(true)
    expect(callbacks.onSaving).toHaveBeenLastCalledWith(true)
    await user.click(screen.getByRole('button', { name: 'Discard local changes' }))
    await waitFor(() => expect(callbacks.onDirty).toHaveBeenLastCalledWith(false))
    expect(callbacks.onSaving).toHaveBeenLastCalledWith(false)
  })

  it('removes the previous vessel synchronously and retains its unresolved command across a forced vessel switch', async () => {
    const oldSave = deferred<Profile>(); const nextProfile = deferred<Profile>()
    vi.mocked(api.saveProfileAttempt).mockReturnValue(oldSave.promise)
    vi.mocked(api.loadProfile).mockImplementation(id => id === firstId ? Promise.resolve(profile()) : nextProfile.promise)
    const oldCallbacks = props(); const nextCallbacks = { ...props(), id: nextId }
    const user = userEvent.setup(); const view = render(<RegulatoryProfile {...oldCallbacks} />)
    await screen.findByDisplayValue('FICTIONAL OLD SOURCE')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    await waitFor(() => expect(api.saveProfileAttempt).toHaveBeenCalledTimes(1))
    view.rerender(<RegulatoryProfile {...nextCallbacks} />)
    expect(screen.queryByDisplayValue('FICTIONAL OLD SOURCE')).not.toBeInTheDocument()
    expect(screen.queryByText(/FICTIONAL-OLD/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument()
    await act(async () => { oldSave.resolve({ ...profile(), revision: 2 }); await oldSave.promise })
    expect(oldCallbacks.onDirty).toHaveBeenLastCalledWith(true)
    expect(oldCallbacks.onSaving).toHaveBeenLastCalledWith(true)
    expect(nextCallbacks.onDirty).toHaveBeenLastCalledWith(true)
    expect(nextCallbacks.onSaving).toHaveBeenLastCalledWith(true)
    await act(async () => { nextProfile.resolve(profile(nextId)); await nextProfile.promise })
    expect(screen.queryByDisplayValue('FICTIONAL NEW SOURCE')).not.toBeInTheDocument()
    expect(screen.queryByDisplayValue('FICTIONAL OLD SOURCE')).not.toBeInTheDocument()
    view.rerender(<RegulatoryProfile {...oldCallbacks} />)
    await screen.findByRole('button', { name: 'Retry same command' })
    expect(api.saveProfileAttempt).toHaveBeenCalledTimes(1) // Read/return cannot resend the lost original attempt.
    expect(screen.queryByRole('button', { name: 'Discard local changes' })).not.toBeInTheDocument()
  })

  it('ignores late successful history after current save access is withdrawn', async () => {
    const pendingHistory = deferred<ProfileHistory>()
    vi.mocked(api.loadProfileHistory).mockReturnValue(pendingHistory.promise)
    vi.mocked(api.saveProfileAttempt).mockRejectedValue(new ApiError(403))
    const user = userEvent.setup(); render(<RegulatoryProfile {...props()} />)
    await screen.findByDisplayValue('FICTIONAL OLD SOURCE')
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByRole('alert')
    await act(async () => { pendingHistory.resolve(history); await pendingHistory.promise })
    expect(screen.queryByText('FICTIONAL OLD HISTORY')).not.toBeInTheDocument()
    expect(screen.queryByDisplayValue('FICTIONAL OLD SOURCE')).not.toBeInTheDocument()
  })

  it('withholds initial profile/history until current proof completes and then withdraws the entire profile on history denial', async () => {
    const pending = deferred<Profile>()
    vi.mocked(api.loadProfile).mockReturnValue(pending.promise)
    vi.mocked(api.loadProfileHistory).mockRejectedValue(new ApiError(403))
    render(<RegulatoryProfile {...props()} />)
    await waitFor(() => expect(api.loadProfile).toHaveBeenCalled())
    expect(api.loadProfileHistory).not.toHaveBeenCalled()
    expect(screen.queryByDisplayValue('FICTIONAL OLD SOURCE')).not.toBeInTheDocument()
    await act(async () => { pending.resolve(profile()); await pending.promise })
    await screen.findByRole('alert')
    expect(screen.queryByDisplayValue('FICTIONAL OLD SOURCE')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument()
  })

  it('requires an explicit reload to restore a now-authorized same-vessel profile', async () => {
    vi.mocked(api.saveProfileAttempt).mockRejectedValueOnce(new ApiError(403))
    const user = userEvent.setup(); render(<RegulatoryProfile {...props()} />)
    await screen.findByDisplayValue('FICTIONAL OLD SOURCE')
    await user.click(screen.getByRole('button', { name: 'Save draft' })); await screen.findByRole('alert')
    expect(screen.queryByDisplayValue('FICTIONAL OLD SOURCE')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Retry loading' }))
    await waitFor(() => expect(api.loadProfile).toHaveBeenCalledTimes(3))
    expect(await screen.findByDisplayValue('FICTIONAL OLD SOURCE')).toBeVisible()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
