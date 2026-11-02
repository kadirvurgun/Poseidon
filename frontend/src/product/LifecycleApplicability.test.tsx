import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LifecycleApplicability } from './LifecycleApplicability'
import { ruleContract } from './applicability-api'

// Mock transport tests only. Actual signed-host journeys are separate and DEFERRED.
const row = () => ({ version: 7, current: { id: '10000000-0000-4000-8000-000000000001', revision: 1, contract: ruleContract, ruleJson: null, rule: null, hash: 'A'.repeat(64), sourceId: null, sourceRevision: null, requirementId: null, requirementRevision: null, lifecycle: 'DRAFT', requestedLifecycle: 'DRAFT', reviewStatus: 'NOT_REQUESTED', savedAt: '2026-11-02T00:00:00Z' } })
const response = (value: unknown, status = 200) => ({ ok: status < 300, status, json: async () => value }) as Response
const page = <T,>(items: T[]) => ({ items, offset: 0, limit: 25, hasMore: false })
const props = () => ({ language: 'en' as const, csrf: 'synthetic-csrf', offline: false, onDirty: vi.fn(), onSaving: vi.fn() })
function transport(save: (init: RequestInit) => Promise<Response> = async () => response(row()), allowed = true) {
  const fetch = vi.fn((url: string, init?: RequestInit) => init?.method ? save(init) : Promise.resolve(response(url.includes('capabilities') ? { canRead: allowed, canWrite: allowed, canReview: allowed, canEvaluate: allowed, canActivate: false, contract: ruleContract } : url.includes('/vessels') ? page([]) : page([row()]))))
  vi.stubGlobal('fetch', fetch); return fetch
}
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('LA-08 TR/EN preparation UI (AUTHORED / NOT RUN)', () => {
  it('labels preview authority separately, saves incomplete draft and retains explicit pending lifecycle', async () => {
    const fetch = transport(); const p = props(); render(<LifecycleApplicability {...p} />); const user = userEvent.setup()
    await screen.findByRole('button', { name: 'Save draft revision' })
    await user.click(screen.getByRole('button', { name: 'Save draft revision' }))
    await screen.findByText('Saved as unverified preparation. Current obligations remain unavailable.')
    const call = fetch.mock.calls.find(([, init]) => init?.method)!
    expect(JSON.parse(call[1]!.body as string)).toMatchObject({ rule: null, expectedVersion: 0, sourceId: null, sourceRevision: null, reasonCode: 'INITIAL_AUTHORING' })
    expect(screen.queryByRole('button', { name: /Approve|Activate|Publish/ })).not.toBeInTheDocument()
  })
  it('freezes uncertain author bytes/key through TR language change and retries exactly', async () => {
    let n = 0; const fetch = transport(async () => ++n === 1 ? Promise.reject(new TypeError('network')) : response(row())); const p = props(); const user = userEvent.setup()
    const { rerender } = render(<LifecycleApplicability {...p} />)
    await screen.findByRole('button', { name: 'Save draft revision' }); await user.click(screen.getByRole('button', { name: 'Save draft revision' }))
    await screen.findByRole('button', { name: 'Retry exact retained command' }); expect(screen.getByLabelText('Rule document')).toBeDisabled()
    rerender(<LifecycleApplicability {...p} language="tr" />); await user.click(screen.getByRole('button', { name: 'Korunan aynı komutu tekrar dene' }))
    await screen.findByText('Doğrulanmamış hazırlık olarak kaydedildi. Güncel yükümlülükler kullanılamıyor.')
    const commands = fetch.mock.calls.filter(([, init]) => init?.method)
    expect(commands).toHaveLength(2); expect(commands[0][1]?.body).toBe(commands[1][1]?.body); expect(commands[0][1]?.headers).toEqual(commands[1][1]?.headers)
  })
  it('does not query source/rule/vessel details without explicit read capability', async () => {
    const fetch = transport(undefined, false); render(<LifecycleApplicability {...props()} />)
    await screen.findByText('No applicability read permission.')
    expect(fetch).toHaveBeenCalledTimes(1); expect(screen.queryByLabelText('Rule document')).not.toBeInTheDocument()
  })
  it('retains edits on version conflict and requires explicit discard before context change', async () => {
    transport(async () => response({ code: 'version_conflict' }, 409)); const user = userEvent.setup(); const p = props(); render(<LifecycleApplicability {...p} />)
    await screen.findByLabelText('Rule document'); await user.type(screen.getByLabelText('Source record ID'), '10000000-0000-4000-8000-000000000001'); await user.type(screen.getByLabelText('Exact source revision'), '1')
    await user.click(screen.getByRole('button', { name: 'Save draft revision' })); await screen.findByRole('alert')
    expect(screen.getByLabelText('Source record ID')).toHaveValue('10000000-0000-4000-8000-000000000001'); expect(screen.getByLabelText('Rule document')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'New incomplete draft' })).toBeDisabled(); expect(p.onDirty).toHaveBeenLastCalledWith(true)
    await user.click(screen.getByRole('button', { name: 'Discard edits and reload' })); await waitFor(() => expect(p.onDirty).toHaveBeenLastCalledWith(false))
  })
  it('keeps offline edits while denying network commands and supporting keyboard labels', async () => {
    const fetch = transport(); const user = userEvent.setup(); const p = props(); const { rerender } = render(<LifecycleApplicability {...p} />)
    await screen.findByLabelText('Rule document'); await user.type(screen.getByLabelText('Rule document'), 'retained')
    rerender(<LifecycleApplicability {...p} offline />)
    expect(screen.getByLabelText('Rule document')).toHaveValue('retained'); expect(screen.getByRole('button', { name: 'Save draft revision' })).toBeDisabled()
    screen.getByLabelText('Rule document').focus(); expect(screen.getByLabelText('Rule document')).toHaveFocus()
    expect(fetch.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0)
  })
  it('submits exact numeric DSL lexemes without a JS round trip', async () => {
    const fetch = transport(); const user = userEvent.setup(); render(<LifecycleApplicability {...props()} />)
    const document = '{"ruleId":"SYNTHETIC","version":1,"source":{"sourceId":"UNADMITTED","locator":"synthetic","authorityLayer":"PRODUCT_CONTROL"},"lifecycle":{"state":"DRAFT","effectiveFrom":"2000-01-01"},"scope":{},"when":{"field":"vessel.gt","operator":"GT","value":9007199254740992.0000000001},"then":{"result":"APPLICABLE"},"unknownPolicy":"REVIEW_REQUIRED"}'
    await screen.findByLabelText('Rule document'); await user.click(screen.getByLabelText('Rule document')); await user.paste(document)
    await user.click(screen.getByRole('button', { name: 'Save draft revision' })); await screen.findByText('Saved as unverified preparation. Current obligations remain unavailable.')
    const body = fetch.mock.calls.find(([, init]) => init?.method)?.[1]?.body as string
    expect(body).toContain('"value":9007199254740992.0000000001')
    expect(body).toContain(`"rule":${document}`)
  })
})
