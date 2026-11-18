import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useRef, useState } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { vessels } from './api'
import { DraftConfirmation } from './DraftConfirmation'
import { loadWorkspaceContext } from './workspace-context-api'
import { WorkspaceVesselSwitcher } from './WorkspaceVesselSwitcher'
import { currentUnsavedState, registerUnsavedSource, unsavedState, useUnsavedChangesSource } from './unsaved-changes'
import type { Session } from './directory-contract'

// DEV-021 / T-S2-002, 65.03 paragraph 64 + UX-038 (paragraph 619): a real guarded form,
// the shared DraftConfirmation and the vessel switcher wired exactly like a workspace.
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), vessels: vi.fn() }))
vi.mock('./workspace-context-api', () => ({ loadWorkspaceContext: vi.fn() }))
const tenant = '10000000-0000-0000-0000-000000000001', actor = '10000000-0000-0000-0000-000000000002'
const first = '30000000-0000-0000-0000-000000000001', second = '30000000-0000-0000-0000-000000000002'
const session: Session = { authenticated: true, canRead: true, actorId: actor, tenantId: tenant, tenantName: 'FICTIONAL workspace',
  csrfToken: 'synthetic-only', setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false }
const rows = [first, second].map((id, i) => ({ id, companyId: tenant, code: `SYN-${i + 1}`, name: `FICTIONAL vessel ${i + 1}`, kind: 'Ship' as const, version: 1, canEdit: false }))

function GuardedWorkspace({ onCommitted }: { onCommitted: (href: string) => void }) {
  const dirty = useRef(false)
  const [note, setNote] = useState('')
  const [pending, setPending] = useState<(() => void) | null>(null)
  const [switching, setSwitching] = useState(true)
  useUnsavedChangesSource(() => unsavedState(dirty.current))
  // Same shape as the workspace guards: dirty → Stay/Discard dialog, clean → act.
  const navigate = (href: string, committed?: () => void) => {
    const action = () => { committed?.(); dirty.current = false; setNote(''); onCommitted(href) }
    if (dirty.current) setPending(() => action); else action()
  }
  return <>
    <label htmlFor="synthetic-note">Synthetic note</label>
    <input id="synthetic-note" value={note} onChange={event => { dirty.current = true; setNote(event.target.value) }} />
    {switching && <WorkspaceVesselSwitcher session={session} vessel={first} language="en" onClose={() => setSwitching(false)} onNavigate={navigate} />}
    {pending && <DraftConfirmation language="en" onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); action() }} />}
  </>
}

beforeEach(() => {
  vi.mocked(vessels).mockResolvedValue({ items: rows, offset: 0, limit: 25, hasMore: false })
  history.replaceState(null, '', `/app/today/${first}`)
})
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

describe('DEV-021 unsaved critical form and context switch', () => {
  it('DEV021-AC-06 Stay keeps the typed draft and old context with no switch request; Discard commits explicitly', async () => {
    const committed = vi.fn()
    render(<GuardedWorkspace onCommitted={committed} />)
    const user = userEvent.setup()
    await screen.findByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' })
    // jsdom does not make the page inert under a modal; typing stands in for a draft made before opening.
    await user.type(screen.getByLabelText('Synthetic note'), 'draft kept')
    await user.click(await screen.findByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' }))
    const confirm = await screen.findByRole('dialog', { name: /discard/i })
    expect(loadWorkspaceContext).not.toHaveBeenCalled()
    await user.click(screen.getAllByRole('button').find(button => confirm.contains(button) && !/discard/i.test(button.textContent ?? ''))!)
    expect(screen.queryByRole('dialog', { name: /discard/i })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Synthetic note')).toHaveValue('draft kept')
    expect(committed).not.toHaveBeenCalled(); expect(loadWorkspaceContext).not.toHaveBeenCalled()
    expect(currentUnsavedState()).toBe('dirty')

    await user.click(screen.getByRole('button', { name: 'SYN-2 · FICTIONAL vessel 2 · Ship' }))
    const secondConfirm = await screen.findByRole('dialog', { name: /discard/i })
    await user.click(screen.getAllByRole('button').find(button => secondConfirm.contains(button) && /discard/i.test(button.textContent ?? ''))!)
    await waitFor(() => expect(committed).toHaveBeenCalledWith(`/app/today/${second}`))
    expect(screen.queryByRole('dialog', { name: 'Change vessel context' })).not.toBeInTheDocument()
    expect(currentUnsavedState()).toBe('clean')
    // Server verification of the new context happens at the destination (context bar), never here before the guard.
    expect(loadWorkspaceContext).not.toHaveBeenCalled()
  })

  it('reports the strongest state across surfaces and fails closed when a source cannot answer', () => {
    const releases = [registerUnsavedSource(() => 'clean')]
    expect(currentUnsavedState()).toBe('clean')
    releases.push(registerUnsavedSource(() => 'dirty'))
    expect(currentUnsavedState()).toBe('dirty')
    releases.push(registerUnsavedSource(() => 'saving'))
    expect(currentUnsavedState()).toBe('saving')
    releases.pop()!(); releases.pop()!()
    releases.push(registerUnsavedSource(() => { throw new Error('synthetic') }))
    expect(currentUnsavedState()).toBe('dirty')
    releases.forEach(release => release())
    expect(currentUnsavedState()).toBe('clean')
  })
})
