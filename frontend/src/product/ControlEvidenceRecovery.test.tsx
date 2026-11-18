import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ControlEvidence } from './ControlEvidence'
import type { Command, Kind, Row } from './control-evidence-api'
import { controlId, controlRow, controlSession, controlVessel, echoControl, loadedVesselContext } from './control-evidence-recovery.test-fixtures'
import { qualifiedFixture } from './qualification.test-fixtures'

// CEO01..05/08. Mocked UI transport only; real hostPG/browser sources are separate.
const response = (value: unknown, status = 200) => ({ ok: status < 300, status, json: async () => value }) as Response
const owner = `${controlSession().tenantId}/${controlSession().actorId}`
const props = () => ({ owner, language: 'en' as const, csrf: crypto.randomUUID(), offline: false, onDirty: vi.fn(), onSaving: vi.fn() })
const text = 'FICTIONAL retained owning declaration'
type Attempt = { path: string; method: string; body: string; key: string; csrf: string; sessions: number }
type Fixture = { rows: Row[]; user: ReturnType<typeof controlSession>; sessions: number; attempts: Attempt[]; failReads: boolean;
  acknowledgment: Promise<Response> | null; acknowledgmentReads: number; tokens: string[];
  owners: { actorId: string; roleId: string }[]; ownerResponse: Promise<Response> | null }
function fixture(post: (command: Command, attempt: Attempt) => Promise<Response>, initial: Row[] = []): Fixture {
  const state: Fixture = { rows: initial, user: controlSession(), sessions: 0, attempts: [], failReads: false,
    acknowledgment: null as Promise<Response> | null, acknowledgmentReads: 0, tokens: [] as string[], owners: [], ownerResponse: null }
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) { state.sessions++; state.tokens.push(crypto.randomUUID()); return response({ ...state.user, csrfToken: state.tokens.at(-1) }) }
    if (init?.method) {
      const headers = init.headers as Record<string, string>, attempt = { path: url, method: init.method, body: init.body as string,
        key: headers['Idempotency-Key'], csrf: headers['X-CSRF-TOKEN'], sessions: state.sessions }
      state.attempts.push(attempt); return post(JSON.parse(attempt.body), attempt)
    }
    if (url.includes('/vessels?')) return response({ items: [{ id: controlVessel, name: 'FICTIONAL owning Ship', companyId: controlId, code: 'F1', kind: 'Ship', version: 1, canEdit: true }], offset: 0, limit: 25, hasMore: false })
    if (url.includes('/regulatory-source-governance/')) { state.acknowledgmentReads++; return state.acknowledgment ?? response({ items: [], hasMore: false, actions: [], authorities: [], owners: [] }) }
    if (url.includes('/owners?')) return state.ownerResponse ?? response({ items: state.owners })
    if (url.includes('/orphans?')) return response({ available: false, reason: 'AUTHORITATIVE_APPLICABILITY_UNAVAILABLE', draftCriticalOrphans: [], uncontrolledObligations: [], offset: 0, hasMore: false })
    if (url.includes('/rules') || url.includes('/requirement-drafts')) return response({ items: [], offset: 0, limit: 25, hasMore: false })
    if (url.includes('/files')) return response({ items: [], offset: 0, hasMore: false, canWrite: false, available: false, maxBytes: 0, allowedTypes: ['image/png'] })
    if (state.failReads) return response({}, 503)
    const kind: Kind = url.includes('/evidence') ? 'Evidence' : 'Control'
    return response({ items: state.rows.filter(row => row.kind === kind), offset: 0, hasMore: false, canWrite: true })
  }); vi.stubGlobal('fetch', fetch); return state
}
async function open(kind: Kind = 'Control') {
  const user = userEvent.setup()
  await user.selectOptions(await loadedVesselContext(controlVessel), controlVessel)
  if (kind === 'Evidence') await user.selectOptions(screen.getByLabelText('Register'), 'Evidence')
  await screen.findByLabelText('Title')
  return user
}
async function recheck(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getAllByRole('button', { name: 'Retry source read' })[0])
}
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('Control/Evidence original-owner recovery, authored execution DEFERRED', () => {
  it.each([400, 401, 403, 404, 409, 503])('unknown then %i keeps exact nullable route/body/key, fresh CSRF and one known server identity', async denial => {
    let committed: Row | null = null
    const state = fixture(async (command) => {
      if (!committed) { committed = echoControl(command); state.rows = [committed]; throw new TypeError('controlled loss after modeled commit') }
      return state.attempts.length === 2 ? response({}, denial) : response(committed)
    })
    const p = props(); render(<ControlEvidence {...p} />); const user = await open()
    await user.type(screen.getByLabelText('Title'), text)
    const beforeSends = [state.sessions]
    await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')
    expect(screen.queryByLabelText('Title')).toBeNull(); expect(p.onSaving).toHaveBeenLastCalledWith(true)
    expect(screen.queryByRole('button', { name: 'Discard local changes' })).toBeNull()
    expect(screen.getByLabelText('Register')).toBeDisabled()
    for (let index = 0; index < 2; index++) {
      await waitFor(() => expect(screen.getByRole('button', { name: 'Retry same command' })).toBeDisabled())
      await recheck(user)
      await waitFor(() => expect(screen.getByRole('button', { name: 'Retry same command' })).toBeEnabled())
      expect(state.attempts).toHaveLength(index+1)
      expect(screen.getByLabelText('Title')).toHaveValue(text); expect(screen.getByLabelText('Title')).toBeDisabled()
      beforeSends.push(state.sessions)
      await user.click(screen.getByRole('button', { name: 'Retry same command' }))
      await waitFor(() => expect(state.attempts).toHaveLength(index+2))
    }
    await screen.findByText('Draft saved; verification and effectiveness remain unmet.')
    expect(await screen.findByLabelText('Title')).toBeDisabled()
    for (const [index, attempt] of state.attempts.entries()) {
      expect(attempt.path === state.attempts[0].path && attempt.method === 'POST' && attempt.body === state.attempts[0].body && attempt.key === state.attempts[0].key).toBe(true)
      const issued = state.tokens.indexOf(attempt.csrf)+1
      expect(issued > beforeSends[index] && issued <= attempt.sessions && attempt.csrf !== p.csrf).toBe(true)
      expect(JSON.parse(attempt.body)).not.toHaveProperty('id')
    }
    expect(state.attempts[1].sessions).toBeGreaterThan(state.attempts[0].sessions)
    expect(state.attempts[2].sessions).toBeGreaterThan(state.attempts[1].sessions)
    expect(state.rows).toHaveLength(1)
  })

  it('retains known ID read-only through optional read loss and resumes the same existing PUT after explicit current read', async () => {
    const state = fixture(async command => { const row = echoControl(command, 'Control', state.attempts.length); state.rows = [row]; state.failReads = true; return response(row) })
    render(<ControlEvidence {...props()} />); const user = await open()
    await user.type(screen.getByLabelText('Title'), text); await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await screen.findByText('Draft saved; verification and effectiveness remain unmet.'); await screen.findByRole('alert')
    expect(state.attempts).toHaveLength(1); expect(screen.queryByLabelText('Title')).toBeNull()
    state.failReads = false; await recheck(user)
    expect(await screen.findByLabelText('Title')).toBeDisabled()
    const discard = screen.getByRole('button', { name: 'Discard local changes' })
    await user.click(discard); await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: /Discard/ }))
    expect(await screen.findByLabelText('Title')).toBeEnabled()
    state.failReads = false
    await user.type(screen.getByLabelText('Title'), ' updated')
    await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await waitFor(() => expect(state.attempts).toHaveLength(2))
    expect(state.attempts[1].method === 'PUT' && state.attempts[1].path.includes(`/controls/${controlId}?`)).toBe(true)
  })

  it('owner drift withdraws private unsent state and restores only the original actual owner', async () => {
    const state = fixture(async () => response({}, 403)), p = props(), view = render(<ControlEvidence {...p} />), user = await open()
    await user.type(screen.getByLabelText('Title'), text)
    state.user = { ...state.user, actorId: controlId }
    view.rerender(<ControlEvidence {...p} owner={`${state.user.tenantId}/${state.user.actorId}`} />)
    expect(screen.queryByDisplayValue(text)).toBeNull()
    await screen.findByRole('alert'); expect(p.onSaving).toHaveBeenLastCalledWith(true)
    state.user = controlSession(); view.rerender(<ControlEvidence {...p} />)
    expect(await screen.findByLabelText('Title')).toHaveValue(text)
    expect(state.attempts).toHaveLength(0)
  })

  it('does not reintroduce old picker owners while the newly authenticated owner read is pending', async () => {
    const state = fixture(async () => response({}, 403)), previous = { actorId: state.user.actorId, roleId: crypto.randomUUID() }
    state.owners = [previous]
    const p = props(), view = render(<ControlEvidence {...p} />); await open()
    const oldLabel = `${previous.actorId} / ${previous.roleId}`
    await screen.findByRole('option', { name: new RegExp(`^${oldLabel}$`) })
    const next = { actorId: controlId, roleId: crypto.randomUUID() }
    let finishOwners!: (value: Response) => void
    state.ownerResponse = new Promise(resolve => { finishOwners = resolve })
    state.user = { ...state.user, actorId: next.actorId }
    view.rerender(<ControlEvidence {...p} owner={`${state.user.tenantId}/${state.user.actorId}`} />)
    expect(screen.queryByRole('option', { name: new RegExp(`^${oldLabel}$`) })).not.toBeInTheDocument()
    await screen.findByLabelText('Title')
    expect(screen.queryByRole('option', { name: new RegExp(`^${oldLabel}$`) })).not.toBeInTheDocument()
    await act(async () => { finishOwners(response({ items: [next] })) })
    await screen.findByRole('option', { name: new RegExp(`^${next.actorId} / ${next.roleId}$`) })
    expect(screen.queryByRole('option', { name: new RegExp(`^${oldLabel}$`) })).not.toBeInTheDocument()
    expect(state.attempts).toHaveLength(0)
  })

  it('replays a valid historical PUT receipt after a later edit and resumes only the same current server ID', async () => {
    const initial = controlRow(); initial.content.title = 'FICTIONAL baseline'
    let original: Row | null = null
    const state = fixture(async command => {
      if (!original) { original = echoControl(command, 'Control', 2); state.rows = [original]; throw new TypeError('controlled response loss') }
      return response(original)
    }, [initial])
    render(<ControlEvidence {...props()} />); const user = await open()
    await user.click(screen.getByRole('button', { name: /FICTIONAL baseline · 1/ }))
    await user.clear(await screen.findByLabelText('Title')); await user.type(screen.getByLabelText('Title'), text)
    await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')
    const sent = JSON.parse(state.attempts[0].body) as Command
    state.rows = [echoControl({ ...sent, content: { ...sent.content, title: 'FICTIONAL later current revision' } }, 'Control', 3)]
    await recheck(user); await waitFor(() => expect(screen.getByRole('button', { name: 'Retry same command' })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: 'Retry same command' }))
    await screen.findByText('Draft saved; verification and effectiveness remain unmet.')
    expect(await screen.findByLabelText('Title')).toHaveValue(text); expect(screen.getByLabelText('Title')).toBeDisabled()
    expect(state.attempts[0].path === state.attempts[1].path && state.attempts[1].method === 'PUT' && state.attempts[0].body === state.attempts[1].body && state.attempts[0].key === state.attempts[1].key).toBe(true)
    expect(state.acknowledgmentReads).toBe(0)
    await user.click(screen.getByRole('button', { name: 'Discard local changes' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: /Discard/ }))
    expect(await screen.findByLabelText('Title')).toHaveValue('FICTIONAL later current revision')
    expect(screen.getByLabelText('Title')).toBeEnabled(); expect(state.attempts).toHaveLength(2)
  })

  it('late valid qualification acknowledgment cannot change own UNKNOWN body or reintroduce its source notice', async () => {
    const source = qualifiedFixture(), evidence = controlRow('Evidence')
    evidence.requirements = [{ requirementId: crypto.randomUUID(), requirementRevision: 1, sourceId: crypto.randomUUID(), sourceRevision: 1, ruleId: source.ruleId, ruleRevision: 1 }]
    let finish!: (value: Response) => void
    const waiting = new Promise<Response>(resolve => { finish = resolve })
    const state = fixture(async () => { throw new TypeError('modeled loss') }, [evidence]); state.acknowledgment = waiting
    render(<ControlEvidence {...props()} />); const user = await open('Evidence')
    await user.click(screen.getByRole('button', { name: new RegExp(`${controlId} · 1`) }))
    await user.click(await screen.findByRole('button', { name: /Acknowledge exact source revision/ }))
    await waitFor(() => expect(state.acknowledgmentReads).toBe(1))
    await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')
    const original = state.attempts[0].body
    await act(async () => { finish(response({ items: [source], actions: [], authorities: [], owners: [], hasMore: false })); await waiting })
    expect(screen.queryByText(/: A{64}$/)).toBeNull()
    expect(state.attempts.length === 1 && state.attempts[0].body === original).toBe(true)
    expect(JSON.parse(original).content.acknowledgments).toBeNull()
  })

  it.each(['owner', 'rule', 'unmount'] as const)('pending acknowledgment cannot apply after %s context changes', async change => {
    const source = qualifiedFixture(), evidence = controlRow('Evidence'); evidence.content.title = text
    evidence.requirements = [{ requirementId: crypto.randomUUID(), requirementRevision: 1, sourceId: crypto.randomUUID(), sourceRevision: 1, ruleId: source.ruleId, ruleRevision: 1 }]
    let finish!: (value: Response) => void
    const waiting = new Promise<Response>(resolve => { finish = resolve })
    const state = fixture(async command => { const row = echoControl(command, 'Evidence', 2); state.rows = [row]; return response(row) }, [evidence]); state.acknowledgment = waiting
    const p = props(), view = render(<ControlEvidence {...p} />), user = await open('Evidence')
    await user.click(screen.getByRole('button', { name: new RegExp(`${text} · 1`) }))
    await user.click(await screen.findByRole('button', { name: /Acknowledge exact source revision/ }))
    await waitFor(() => expect(state.acknowledgmentReads).toBe(1))
    if (change === 'owner') { state.user = { ...state.user, actorId: controlId }; view.rerender(<ControlEvidence {...p} owner={`${state.user.tenantId}/${state.user.actorId}`} />) }
    if (change === 'rule') await user.selectOptions(screen.getByDisplayValue(`${source.ruleId}/1`), '')
    if (change === 'unmount') view.unmount()
    const dirtyCalls = p.onDirty.mock.calls.length, savingCalls = p.onSaving.mock.calls.length
    await act(async () => { finish(response({ items: [source], actions: [], authorities: [], owners: [], hasMore: false })); await waiting })
    expect(screen.queryByText(/: A{64}$/)).toBeNull(); expect(state.attempts).toHaveLength(0)
    if (change === 'unmount') { expect(p.onDirty.mock.calls).toHaveLength(dirtyCalls); expect(p.onSaving.mock.calls).toHaveLength(savingCalls); return }
    if (change === 'owner') {
      expect(screen.queryByDisplayValue(text)).toBeNull()
      state.user = controlSession(); view.rerender(<ControlEvidence {...p} />)
      expect(await screen.findByLabelText('Title')).toHaveValue(text)
    }
    await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await screen.findByText('Draft saved; verification and effectiveness remain unmet.')
    const sent = JSON.parse(state.attempts[0].body)
    expect(sent.content.acknowledgments).toBeNull()
    if (change === 'rule') expect(sent.requirements[0].ruleId).toBeNull()
  })

  it('late POST after actual owner drift stays UNKNOWN, hides its receipt and never automatically resends', async () => {
    let finish!: (value: Response) => void, pending: Command | null = null
    const waiting = new Promise<Response>(resolve => { finish = resolve })
    const state = fixture(async command => { pending = command; return waiting })
    const p = props(), view = render(<ControlEvidence {...p} />), user = await open()
    await user.type(screen.getByLabelText('Title'), text); await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await waitFor(() => expect(state.attempts).toHaveLength(1))
    state.user = { ...state.user, actorId: controlId }; view.rerender(<ControlEvidence {...p} owner={`${state.user.tenantId}/${state.user.actorId}`} />)
    expect(screen.queryByDisplayValue(text)).toBeNull()
    await act(async () => { finish(response(echoControl(pending!))); await waiting })
    await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')
    expect(screen.queryByText('Draft saved; verification and effectiveness remain unmet.')).toBeNull()
    expect(state.attempts).toHaveLength(1); expect(p.onSaving).toHaveBeenLastCalledWith(true)
  })
})
