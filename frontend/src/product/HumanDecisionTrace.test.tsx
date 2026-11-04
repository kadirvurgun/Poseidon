import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { HumanDecisionTrace } from './HumanDecisionTrace'
import { reviewAnchor, reviewRow, reviewTrace, recordingPolicy, recordedRow } from './human-review.test-fixtures'

const response = (value: unknown, status = 200) => ({ ok: status < 300, status, json: async () => value }) as Response
const props = () => ({ anchor: reviewAnchor, language: 'en' as const, csrf: 'synthetic-csrf', offline: false, onDirty: vi.fn(), onSaving: vi.fn() })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('human review accessible UI authored NOT RUN; mocked transport is UI evidence only', () => {
  it.each(['en', 'tr'] as const)('preserves known source/control lifecycle and explicit qualification blockers in %s', async language => {
    const pin = { id: reviewAnchor.controlId, revision: 3, availability: 'Available', hash: 'C'.repeat(64), savedAt: '2026-11-02T00:00:00Z' }
    vi.stubGlobal('fetch', vi.fn(async () => response({ ...reviewTrace(),
      segments: [
        { ...pin, kind: 'Source', state: 'IN_FORCE', why: 'RECORDED_QUALIFICATION' },
        { ...pin, kind: 'Rule', state: 'FUTURE_EFFECTIVE', why: 'HISTORICAL_INPUTS_ONLY' },
        { ...pin, kind: 'Control', state: 'COMPLETED', why: 'COMPLETION_AND_EFFECTIVENESS_UNKNOWN' }
      ],
      blockers: ['QUALIFICATION_LINEAGE_UNAVAILABLE', 'SOURCE_REVIEW_REQUIRED', 'ATTESTATION_EXPIRED', 'EXTERNAL_SIGNATURE_UNAVAILABLE']
    })))
    render(<HumanDecisionTrace {...props()} language={language} />)
    const trace = await screen.findByRole('list', { name: language === 'en' ? 'Source-grounded trace' : 'Kaynağa dayalı iz' })
    const source = within(trace).getByRole('article', { name: language === 'en' ? 'Source' : 'Kaynak' })
    expect(source).toHaveTextContent(language === 'en' ? 'In force' : 'Yürürlükte')
    expect(source).toHaveTextContent(language === 'en' ? 'Recorded source qualification' : 'Kaydedilmiş kaynak nitelendirmesi')
    expect(source).not.toHaveTextContent(language === 'en' ? 'Unknown' : 'Bilinmiyor')
    expect(within(trace).getByRole('article', { name: language === 'en' ? 'Rule' : 'Kural' })).toHaveTextContent(language === 'en' ? 'Future effective' : 'Gelecekte yürürlüğe girecek')
    expect(within(trace).getByRole('article', { name: language === 'en' ? 'Control' : 'Kontrol' })).toHaveTextContent(language === 'en' ? 'Completed (verification separate)' : 'Tamamlandı (doğrulama ayrı)')
    expect(screen.getByText(language === 'en' ? 'Qualification lineage unavailable' : 'Nitelendirme izi kullanılamıyor')).toBeVisible()
    expect(screen.getByText(language === 'en' ? 'Source review required' : 'Kaynak incelemesi gerekli')).toBeVisible()
    expect(screen.getByText(language === 'en' ? 'Source attestation expired' : 'Kaynak beyanının süresi doldu')).toBeVisible()
    expect(screen.queryByRole('button', { name: /approve/i })).not.toBeInTheDocument()
  })
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

describe('human decision recording EN/TR, authored NOT RUN', () => {
  it.each(['en', 'tr'] as const)('records a complete human text command through the actual consumer in %s', async language => {
    const fetch = vi.fn(async (_url: string, init?: RequestInit) => init?.method ? response({ ...recordedRow(), rationale: JSON.parse(init.body as string).rationale, recording: { ...recordedRow().recording, content: JSON.parse(init.body as string).recording } }) : response({ ...reviewTrace(), items: [reviewRow()], authorities: [{ ...reviewTrace().authorities[0], capability: 'human.decide', recording: recordingPolicy() }] }))
    vi.stubGlobal('fetch', fetch); const user = userEvent.setup(); render(<HumanDecisionTrace {...props()} language={language} />)
    await user.click(await screen.findByRole('button', { name: language === 'en' ? 'Record authorized human decision' : 'Yetkili insan kararını kaydet' }))
    await user.selectOptions(screen.getByLabelText(language === 'en' ? 'Provisioned authority' : 'Sunucuda atanmış yetki'), reviewTrace().authorities[0].id)
    const save = screen.getByRole('button', { name: language === 'en' ? 'Save human decision record' : 'İnsan karar kaydını sakla' }); expect(save).toBeDisabled()
    await user.type(screen.getByLabelText(language === 'en' ? 'Bound review reason' : 'Bağlı inceleme gerekçesi'), 'FICTIONAL reason')
    await user.type(screen.getByLabelText(language === 'en' ? 'Human decision text' : 'İnsan kararı metni'), 'FICTIONAL human text')
    await user.type(screen.getByLabelText(language === 'en' ? 'Decision context' : 'Karar bağlamı'), 'Incomplete context retained')
    await user.type(screen.getByLabelText(language === 'en' ? 'Trigger source' : 'Tetikleyici kaynak'), 'FICTIONAL trigger')
    await user.click(save)
    await waitFor(() => expect(fetch.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true))
    const [url, init] = fetch.mock.calls.find(([, init]) => init?.method === 'POST')!
    expect(url).toContain('/decisions?'); const cmd = JSON.parse(init!.body as string)
    expect(cmd.state).toBe('RECORDED'); expect(cmd.reasonCode).toBe('HUMAN_DECIDED'); expect(cmd.expectedProfileRevision).toBe(1)
    expect(cmd.recording.subjectId).toBe(reviewAnchor.controlId); expect(cmd.verificationRevision).toBe(1)
    expect(cmd).not.toHaveProperty('actorRole'); expect(cmd.recording).not.toHaveProperty('policy')
  })
  it('keeps source-gated types blocked and shows the exact conditional source', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => response({ ...reviewTrace(), items: [reviewRow()], authorities: [{ ...reviewTrace().authorities[0], capability: 'human.decide', recording: { ...recordingPolicy(), requiredGate: 'QualifiedSignature', gateSourceLocator: '07.01/Master Register/row:71/TR-013' } }] })))
    render(<HumanDecisionTrace {...props()} />); const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Record authorized human decision' }))
    await user.selectOptions(screen.getByLabelText('Provisioned authority'), reviewTrace().authorities[0].id)
    expect(screen.getByRole('alert')).toHaveTextContent('07.01/Master Register/row:71/TR-013')
    expect(screen.getByRole('button', { name: 'Save human decision record' })).toBeDisabled()
  })
})
