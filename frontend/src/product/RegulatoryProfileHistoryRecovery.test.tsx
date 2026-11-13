import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { RegulatoryProfile } from './RegulatoryProfile'
import { profileOwner, profileResponse, profileTransport, profileVessel } from './profile-recovery.test-fixtures'

const props = () => ({ id: profileVessel, language: 'en' as const, csrf: crypto.randomUUID(), owner: profileOwner, onDirty: vi.fn(), onSaving: vi.fn() })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

function validationTransport() {
  const { state, fetch } = profileTransport()
  state.writeStatus = 400
  state.row.content.flag = { state: 'Known', value: 'FICTIONAL retained history value' }
  const transport = vi.fn(async (url: string, init?: RequestInit) => {
    const response = await fetch(url, init)
    return init?.method === 'PUT' ? profileResponse({ code: 'validation_failed', errors: { imo: ['seven_digits_required'] } }, 400) : response
  })
  const historyReads = () => fetch.mock.calls.filter(([url]) => url.includes('/history?')).length
  const historyPage = () => ({ items: [{ revision: state.row.revision, savedAt: state.row.savedAt, provenance: state.row.provenance, content: structuredClone(state.row.content) }], offset: 0, limit: 25, hasMore: false })
  return { state, transport, historyReads, historyPage }
}

async function invalidImo(user: ReturnType<typeof userEvent.setup>) {
  await user.selectOptions(await screen.findByLabelText('IMO — Data state'), 'Known')
  await user.type(screen.getByLabelText('IMO'), '123')
}

describe('PROFILE-DEFINITE-VALIDATION-HISTORY-01 source-authored regressions, execution DEFERRED', () => {
  it('reloads authorized history immediately after initial400 while retaining invalid draft and errors with one PUT', async () => {
    const { state, transport, historyReads, historyPage } = validationTransport(); vi.stubGlobal('fetch', transport)
    const p = props(), user = userEvent.setup(); render(<RegulatoryProfile {...p}/>)
    await screen.findByText(/^Version 1 ·/)
    await invalidImo(user)
    const initialReads = historyReads(), initialSessions = state.sessions
    let finishHistory!: (response: Response) => void
    state.history = new Promise(resolve => { finishHistory = resolve })
    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    await waitFor(() => expect(historyReads()).toBeGreaterThan(initialReads))
    expect(screen.queryByText(/^Version 1 ·/)).not.toBeInTheDocument()
    expect(screen.getByLabelText('IMO')).toHaveValue('123')
    expect(screen.getByLabelText('IMO')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('IMO — Data state')).toHaveAttribute('aria-invalid', 'true')
    await act(async () => { finishHistory(profileResponse(historyPage())) })
    await screen.findByText(/^Version 1 ·/)
    expect(state.sessions).toBeGreaterThan(initialSessions)
    expect(state.writes).toHaveLength(1)
    expect(JSON.parse(state.writes[0].body).content.imo).toEqual({ state: 'Known', value: '123' })
    expect(state.row.revision).toBe(1)
    expect(screen.getByLabelText('IMO')).toHaveValue('123')
    expect(screen.getByLabelText('IMO')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeEnabled()
    expect(screen.queryByText('Draft saved.')).not.toBeInTheDocument()
    expect(p.onDirty).toHaveBeenLastCalledWith(true)
  })

  it.each(['foreign-owner', 'same-owner-private-withdrawal'] as const)('never restores late validation history after %s', async boundary => {
    const { state, transport, historyReads, historyPage } = validationTransport()
    const file = { id: crypto.randomUUID(), vesselId: profileVessel, name: 'FICTIONAL-history-record.pdf', state: 'CleanTransport', revision: 1, version: 1, checksum: 'A'.repeat(64), length: 1 }
    state.row.content.constructionRecord = { id: file.id, revision: 1, checksum: file.checksum }
    state.file = Promise.resolve(profileResponse({ items: [file], offset: 0, hasMore: false, available: true, canWrite: false, maxBytes: 1024, allowedTypes: ['application/pdf'] }))
    vi.stubGlobal('fetch', transport)
    const p = props(), user = userEvent.setup(); render(<RegulatoryProfile {...p}/>)
    await screen.findByText(/^Version 1 ·/)
    await screen.findByRole('option', { name: file.name+' / 1' })
    await invalidImo(user)
    const initialReads = historyReads(), savedHistory = historyPage()
    let finishHistory!: (response: Response) => void, denySelectedFile!: (response: Response) => void
    state.history = new Promise(resolve => { finishHistory = resolve })
    if (boundary === 'same-owner-private-withdrawal') state.file = new Promise(resolve => { denySelectedFile = resolve })
    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    await waitFor(() => expect(historyReads()).toBeGreaterThan(initialReads))
    if (boundary === 'foreign-owner') state.session.actorId = crypto.randomUUID()
    else {
      await act(async () => { denySelectedFile(profileResponse({}, 403)) })
      await waitFor(() => expect(screen.queryByLabelText('IMO')).not.toBeInTheDocument())
    }
    // Fetch intentionally ignores cancellation: the consumer must fence publication.
    await act(async () => { finishHistory(profileResponse(savedHistory)) })
    await waitFor(() => expect(screen.queryByLabelText('IMO')).not.toBeInTheDocument())
    expect(screen.queryByText(/^Version 1 ·/)).not.toBeInTheDocument()
    expect(screen.queryByText('FICTIONAL retained history value')).not.toBeInTheDocument()
    expect(screen.queryByRole('option', { name: file.name+' / 1' })).not.toBeInTheDocument()
    expect(state.writes).toHaveLength(1)
    expect(state.row.revision).toBe(1)
    expect(p.onDirty).toHaveBeenLastCalledWith(true)
  })
})
