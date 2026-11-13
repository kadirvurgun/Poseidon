import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { HumanDecisionTrace } from './HumanDecisionTrace'
import { reviewAnchor, reviewRow, reviewTrace, reviewSession } from './human-review.test-fixtures'

// HRO01..04/08; component transport only, not actual host/provider evidence.
const response = (value: unknown, status = 200) => ({ ok: status < 300, status, json: async () => value }) as Response
const owner = `${reviewSession().tenantId}/${reviewSession().actorId}`
const props = () => ({ anchor: reviewAnchor, owner, language: 'en' as const, csrf: crypto.randomUUID(), offline: false, onDirty: vi.fn(), onSaving: vi.fn() })
const reason = 'FICTIONAL private retained review reason'
const result = (body: string) => ({ ...reviewRow(), ...Object.fromEntries(Object.entries(JSON.parse(body)).filter(([key]) =>
  ['state', 'reasonCode', 'rationale', 'authorityId', 'operationId', 'evaluationId', 'verificationId', 'verificationRevision'].includes(key))) })
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done }); return { promise, resolve } }
async function compose() {
  const user = userEvent.setup()
  await user.selectOptions(await screen.findByLabelText('Provisioned authority'), reviewTrace().authorities[0].id)
  await user.type(screen.getByLabelText('Bound review reason'), reason)
  return user
}
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('owner-bound human command recovery, authored execution DEFERRED', () => {
  it.each([401, 403, 404, 409])('unknown then %i retains exact envelope and fresh-token retry without Discard or automatic POST', async denial => {
    let sessions = 0, posts = 0
    const tokens: string[] = []
    const attempts: { body: string; key: string; csrf: string; sessions: number }[] = []
    const fetch = vi.fn(async (url: string, init?: RequestInit) => {
      if (url.endsWith('/auth/session')) { sessions++; tokens.push(crypto.randomUUID()); return response({ ...reviewSession(), csrfToken: tokens.at(-1) }) }
      if (!init?.method) return response(reviewTrace())
      const headers = init.headers as Record<string, string>
      attempts.push({ body: init.body as string, key: headers['Idempotency-Key'], csrf: headers['X-CSRF-TOKEN'], sessions })
      if (++posts === 1) throw new TypeError('controlled response loss')
      return posts === 2 ? response({}, denial) : response(result(init.body as string))
    }); vi.stubGlobal('fetch', fetch)
    const p = props(); render(<HumanDecisionTrace {...p} />); const user = await compose()
    await user.click(screen.getByRole('button', { name: 'Record pending request' }))
    await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')
    expect(screen.queryByRole('button', { name: 'Discard local review draft' })).toBeNull()
    expect(screen.queryByLabelText('Bound review reason')).toBeNull()
    expect(p.onSaving).toHaveBeenLastCalledWith(true)
    expect(posts).toBe(1)
    for (let index = 0; index < 2; index++) {
      await waitFor(() => expect(screen.getByRole('button', { name: 'Retry source read' })).toBeEnabled())
      await user.click(screen.getByRole('button', { name: 'Retry source read' }))
      await waitFor(() => expect(screen.getByRole('button', { name: 'Retry exact human command' })).toBeEnabled())
      expect(posts).toBe(index+1)
      expect(screen.getByLabelText('Bound review reason')).toHaveValue(reason)
      expect(screen.getByLabelText('Bound review reason')).toBeDisabled()
      await user.click(screen.getByRole('button', { name: 'Retry exact human command' }))
      if (index === 0) await waitFor(() => expect(posts).toBe(2))
    }
    await screen.findByText('Draft saved.')
    expect(attempts).toHaveLength(3)
    for (const attempt of attempts) {
      expect(attempt.body).toBe(attempts[0].body); expect(attempt.key).toBe(attempts[0].key)
      expect(attempt.csrf).toBe(tokens[attempt.sessions-1])
      expect(attempt.csrf).not.toBe(p.csrf)
      expect(JSON.parse(attempt.body)).not.toHaveProperty('actorId')
    }
    expect(attempts[1].sessions).toBeGreaterThan(attempts[0].sessions)
    expect(attempts[2].sessions).toBeGreaterThan(attempts[1].sessions)
    expect(screen.getByRole('button', { name: 'Record pending request' })).toBeDisabled()
  })

  it('withdraws private draft immediately on owner drift and recovers only its original session', async () => {
    let user = reviewSession()
    const fetch = vi.fn(async (url: string, _init?: RequestInit) => response(url.endsWith('/auth/session') ? user : reviewTrace()))
    vi.stubGlobal('fetch', fetch)
    const p = props(), view = render(<HumanDecisionTrace {...p} />), keyboard = await compose()
    user = { ...user, actorId: reviewAnchor.controlId }
    view.rerender(<HumanDecisionTrace {...p} owner={`${user.tenantId}/${user.actorId}`} />)
    expect(screen.queryByDisplayValue(reason)).toBeNull()
    await screen.findByRole('alert')
    expect(p.onSaving).toHaveBeenLastCalledWith(true)
    expect(fetch.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method)).toBe(false)
    user = reviewSession(); view.rerender(<HumanDecisionTrace {...p} />)
    expect(await screen.findByLabelText('Bound review reason')).toHaveValue(reason)
    await keyboard.click(screen.getByRole('button', { name: 'Discard local review draft' }))
    expect(await screen.findByLabelText('Bound review reason')).toHaveValue('')
  })

  it('retains a confirmed receipt read-only after failed optional GET and does not retry the write', async () => {
    let committed = false, readsFail = true, posts = 0
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      if (url.endsWith('/auth/session')) return response(reviewSession())
      if (init?.method) { posts++; committed = true; return response(result(init.body as string)) }
      return committed && readsFail ? response({}, 503) : response(reviewTrace())
    }))
    render(<HumanDecisionTrace {...props()} />); const user = await compose()
    await user.click(screen.getByRole('button', { name: 'Record pending request' }))
    await screen.findByText('Draft saved.'); await screen.findByRole('alert')
    expect(posts).toBe(1); expect(screen.queryByRole('button', { name: 'Retry exact human command' })).toBeNull()
    readsFail = false; await user.click(screen.getByRole('button', { name: 'Retry source read' }))
    expect(await screen.findByLabelText('Bound review reason')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Record pending request' })).toBeDisabled()
    expect(posts).toBe(1)
    await user.click(screen.getByRole('button', { name: 'Discard local review draft' }))
    expect(await screen.findByLabelText('Bound review reason')).toBeEnabled()
    expect(screen.getByLabelText('Bound review reason')).toHaveValue('')
  })

  it('withdraws a bound private draft when its exact current authority disappears and restores only that authority', async () => {
    let granted = true, posts = 0
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method) posts++
      return response(url.endsWith('/auth/session') ? reviewSession() : { ...reviewTrace(), authorities: granted ? reviewTrace().authorities : [] })
    }))
    const p = props(); render(<HumanDecisionTrace {...p} />); const user = await compose()
    granted = false; await user.click(screen.getByRole('button', { name: 'Retry source read' }))
    await screen.findByRole('alert')
    expect(screen.queryByDisplayValue(reason)).toBeNull(); expect(p.onSaving).toHaveBeenLastCalledWith(true)
    granted = true; await user.click(screen.getByRole('button', { name: 'Retry source read' }))
    expect(await screen.findByLabelText('Bound review reason')).toHaveValue(reason)
    expect(posts).toBe(0)
  })

  it.each([false, true])('fresh actual original owner can discard an UNSENT authority-denied draft; foreign actual owner=%s cannot', async foreignBeforeDiscard => {
    let granted = true, posts = 0, sessions = 0, session = reviewSession()
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      if (url.endsWith('/auth/session')) { sessions++; return response(session) }
      if (init?.method) posts++
      return response({ ...reviewTrace(), authorities: granted ? reviewTrace().authorities : [] })
    }))
    const p = props(); render(<HumanDecisionTrace {...p} />); const user = await compose()
    granted = false; await user.click(screen.getByRole('button', { name: 'Retry source read' }))
    await screen.findByRole('alert'); expect(screen.queryByDisplayValue(reason)).toBeNull()
    if (foreignBeforeDiscard) session = { ...session, actorId: reviewAnchor.controlId }
    const beforeDiscard = sessions
    await user.click(screen.getByRole('button', { name: 'Discard local review draft' }))
    await waitFor(() => expect(sessions).toBeGreaterThan(beforeDiscard))
    if (foreignBeforeDiscard) {
      expect(p.onSaving).toHaveBeenLastCalledWith(true)
      expect(screen.queryByDisplayValue(reason)).toBeNull()
      session = reviewSession(); await user.click(screen.getByRole('button', { name: 'Retry source read' }))
      await screen.findByRole('button', { name: 'Discard local review draft' })
      await user.click(screen.getByRole('button', { name: 'Discard local review draft' }))
    }
    await waitFor(() => expect(p.onSaving).toHaveBeenLastCalledWith(false))
    expect(screen.queryByDisplayValue(reason)).toBeNull(); expect(posts).toBe(0)
    expect(screen.queryByText('Outcome uncertain. The exact original attempt is locked until retry.')).toBeNull()
  })

  it('initial pre-send trace denial permits only a fresh-owner local Discard, with zero dispatched commands', async () => {
    let denied = false, posts = 0, sessions = 0
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      if (url.endsWith('/auth/session')) { sessions++; return response(reviewSession()) }
      if (init?.method) posts++
      return denied ? response({}, 403) : response(reviewTrace())
    }))
    const p = props(); render(<HumanDecisionTrace {...p} />); const user = await compose()
    denied = true; await user.click(screen.getByRole('button', { name: 'Record pending request' }))
    await screen.findByRole('alert')
    expect(screen.queryByDisplayValue(reason)).toBeNull(); expect(posts).toBe(0)
    const beforeDiscard = sessions
    await user.click(screen.getByRole('button', { name: 'Discard local review draft' }))
    await waitFor(() => expect(p.onSaving).toHaveBeenLastCalledWith(false))
    expect(sessions).toBeGreaterThan(beforeDiscard); expect(posts).toBe(0)
    expect(screen.queryByDisplayValue(reason)).toBeNull()
  })

  it('rejects a forged actor receipt as UNKNOWN even when body and route match', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => response(url.endsWith('/auth/session') ? reviewSession() :
      init?.method ? { ...result(init.body as string), actorId: reviewAnchor.controlId } : reviewTrace())))
    render(<HumanDecisionTrace {...props()} />); const user = await compose()
    await user.click(screen.getByRole('button', { name: 'Record pending request' }))
    await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')
    expect(screen.queryByText('Draft saved.')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Discard local review draft' })).toBeNull()
  })

  it('late source response cannot restore private DOM after scope/owner withdrawal, and unmount ignores completion', async () => {
    const waiting = deferred<Response>(); let reads = 0
    vi.stubGlobal('fetch', vi.fn(async (url: string) => url.endsWith('/auth/session') ? response(reviewSession()) : ++reads === 1 ? waiting.promise : response(reviewTrace())))
    const p = props(), view = render(<HumanDecisionTrace {...p} />)
    await waitFor(() => expect(reads).toBe(1))
    view.rerender(<HumanDecisionTrace {...p} owner={null} />)
    expect(screen.queryByLabelText('Provisioned authority')).toBeNull()
    view.unmount()
    const dirtyCalls = p.onDirty.mock.calls.length, savingCalls = p.onSaving.mock.calls.length
    await act(async () => { waiting.resolve(response(reviewTrace())); await waiting.promise })
    expect(screen.queryByLabelText('Provisioned authority')).toBeNull()
    expect(p.onDirty.mock.calls).toHaveLength(dirtyCalls); expect(p.onSaving.mock.calls).toHaveLength(savingCalls)
  })

  it('late POST receipt after owner drift remains UNKNOWN and cannot expose or confirm the former owner text', async () => {
    const waiting = deferred<Response>(); let body = '', posts = 0, user = reviewSession()
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      if (url.endsWith('/auth/session')) return response(user)
      if (init?.method) { posts++; body = init.body as string; return waiting.promise }
      return response(reviewTrace())
    }))
    const p = props(), view = render(<HumanDecisionTrace {...p} />), keyboard = await compose()
    await keyboard.click(screen.getByRole('button', { name: 'Record pending request' }))
    await waitFor(() => expect(posts).toBe(1))
    user = { ...user, actorId: reviewAnchor.controlId }
    view.rerender(<HumanDecisionTrace {...p} owner={`${user.tenantId}/${user.actorId}`} />)
    expect(screen.queryByDisplayValue(reason)).toBeNull()
    await act(async () => { waiting.resolve(response(result(body))); await waiting.promise })
    await screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')
    expect(screen.queryByText('Draft saved.')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Discard local review draft' })).toBeNull()
    expect(p.onSaving).toHaveBeenLastCalledWith(true)
    user = reviewSession(); view.rerender(<HumanDecisionTrace {...p} />)
    expect(await screen.findByLabelText('Bound review reason')).toHaveValue(reason)
    expect(screen.getByLabelText('Bound review reason')).toBeDisabled()
    expect(posts).toBe(1)
  })
})
