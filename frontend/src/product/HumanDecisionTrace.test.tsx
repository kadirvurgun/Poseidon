import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { HumanDecisionTrace } from './HumanDecisionTrace'
import { reviewAnchor, reviewRow, reviewTrace } from './human-review.test-fixtures'

const response = (value: unknown, status = 200) => ({ ok: status < 300, status, json: async () => value }) as Response
const props = () => ({ anchor: reviewAnchor, language: 'en' as const, csrf: 'synthetic-csrf', offline: false, onDirty: vi.fn(), onSaving: vi.fn() })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('human review accessible UI authored NOT RUN; mocked transport is UI evidence only', () => {
  it('shows unavailable signatures, empty review and keyboard reachable human reason in EN/TR', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => response(reviewTrace())))
    const p = props(); const { rerender } = render(<HumanDecisionTrace {...p} />); const user = userEvent.setup()
    await screen.findByText('No human review recorded in this context.')
    expect(screen.getByText('External signature mechanism unavailable')).toBeVisible()
    await user.selectOptions(screen.getByLabelText('Provisioned authority'), reviewTrace().authorities[0].id)
    screen.getByLabelText('Bound review reason').focus(); expect(screen.getByLabelText('Bound review reason')).toHaveFocus()
    await user.type(screen.getByLabelText('Bound review reason'), 'FICTIONAL reason')
    await waitFor(() => expect(p.onDirty).toHaveBeenLastCalledWith(true))
    rerender(<HumanDecisionTrace {...p} language="tr" />)
    expect(screen.getByLabelText('Bağlı inceleme gerekçesi')).toHaveValue('FICTIONAL reason')
    expect(screen.getByText('Harici imza mekanizması kullanılamıyor')).toBeVisible()
    expect(screen.queryByRole('button', { name: /approve/i })).toBeNull()
  })
  it('retains exact body and idempotency key across ambiguous response and translation', async () => {
    let count = 0
    const fetch = vi.fn(async (_url: string, init?: RequestInit) => {
      if (init?.method) { if (++count === 1) throw new TypeError('synthetic lost response'); return response(reviewRow()) }
      return response(reviewTrace())
    }); vi.stubGlobal('fetch', fetch)
    const p = props(); const { rerender } = render(<HumanDecisionTrace {...p} />); const user = userEvent.setup()
    await user.selectOptions(await screen.findByLabelText('Provisioned authority'), reviewTrace().authorities[0].id)
    await user.type(screen.getByLabelText('Bound review reason'), 'FICTIONAL pending request')
    await user.click(screen.getByRole('button', { name: 'Record pending request' })); await screen.findByRole('button', { name: 'Retry exact human command' })
    expect(screen.getByLabelText('Bound review reason')).toBeDisabled()
    rerender(<HumanDecisionTrace {...p} language="tr" />)
    await user.click(screen.getByRole('button', { name: 'Aynı insan komutunu tekrar dene' }))
    await waitFor(() => expect(count).toBe(2))
    const calls = fetch.mock.calls.filter(([, init]) => init?.method)
    expect(calls[0][1]?.body).toBe(calls[1][1]?.body); expect(calls[0][1]?.headers).toEqual(calls[1][1]?.headers)
    expect(JSON.parse(calls[0][1]!.body as string)).not.toHaveProperty('actorContext')
  })
  it.each([403, 404, 409, 503])('retains reason and blocks inappropriate new command on %i', async status => {
    vi.stubGlobal('fetch', vi.fn(async (_url: string, init?: RequestInit) => init?.method ? response({}, status) : response(reviewTrace())))
    render(<HumanDecisionTrace {...props()} />); const user = userEvent.setup()
    await user.selectOptions(await screen.findByLabelText('Provisioned authority'), reviewTrace().authorities[0].id)
    await user.type(screen.getByLabelText('Bound review reason'), 'keep unresolved draft')
    await user.click(screen.getByRole('button', { name: 'Record pending request' })); await screen.findByRole('alert')
    expect(screen.getByLabelText('Bound review reason')).toHaveValue('keep unresolved draft')
    expect(screen.getByRole('button', { name: 'Record pending request' })).toBeDisabled()
    if (status !== 503) expect(screen.getByRole('button', { name: 'Retry exact human command' })).toBeDisabled()
  })
  it('retains dirty context offline, and historical mode offers no command', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('asOf=') ? { ...reviewTrace(), historical: true, authorities: [] } : reviewTrace())))
    const p = props(); const { rerender } = render(<HumanDecisionTrace {...p} />); const user = userEvent.setup()
    await screen.findByLabelText('Bound review reason'); await user.type(screen.getByLabelText('Bound review reason'), 'retain')
    rerender(<HumanDecisionTrace {...p} offline />); expect(screen.getByText('Offline. Human review requires server connectivity.')).toBeVisible()
    rerender(<HumanDecisionTrace {...p} />); await screen.findByLabelText('Bound review reason'); expect(screen.getByLabelText('Bound review reason')).toHaveValue('retain')
    await user.click(screen.getByRole('button', { name: 'Discard local review draft' }))
    await user.type(screen.getByLabelText('Trace as of with UTC offset'), '2026-11-01T00:00:00Z'); await user.click(screen.getByRole('button', { name: 'Read trace history' }))
    await waitFor(() => expect(screen.queryByLabelText('Bound review reason')).toBeNull())
  })
})
