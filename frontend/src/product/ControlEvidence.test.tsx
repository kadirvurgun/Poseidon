import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ControlEvidence } from './ControlEvidence'
import { blank } from './control-evidence-api'

const vessel = '10000000-0000-4000-8000-000000000001', id = '10000000-0000-4000-8000-000000000002'
const row = () => ({ id, vesselId: vessel, kind: 'Control', revision: 1, version: 5, content: blank(), hash: 'A'.repeat(64), lifecycle: 'DRAFT', completion: 'UNKNOWN', verification: 'UNVERIFIED', effectiveness: 'UNKNOWN', timeState: 'UNKNOWN', evidenceExists: false, file: null, requirements: [], links: [], blockers: ['SOURCE_ADMISSION_UNAVAILABLE', 'VERIFICATION_NOT_PERFORMED'], savedAt: '2026-11-02T00:00:00Z', asOf: '2026-11-02T00:00:00Z' })
const response = (value: unknown, status = 200) => ({ ok: status < 300, status, json: async () => value }) as Response
const props = () => ({ language: 'en' as const, csrf: 'synthetic-csrf', offline: false, onDirty: vi.fn(), onSaving: vi.fn() })
function transport(write: (init: RequestInit) => Promise<Response> = async () => response(row()), denied = false) {
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (init?.method) return write(init)
    if (url.includes('/vessels?')) return response({ items: [{ id: vessel, name: 'FICTIONAL vessel', companyId: id, code: 'F1', kind: 'Ship', version: 1, canEdit: true }], offset: 0, limit: 25, hasMore: false })
    if (denied) return response({}, 403)
    if (url.includes('/owners?')) return response({ items: [] })
    if (url.includes('/orphans?')) return response({ available: false, reason: 'AUTHORITATIVE_APPLICABILITY_UNAVAILABLE', draftCriticalOrphans: [], offset: 0, hasMore: false })
    if (url.includes('/rules') || url.includes('/requirement-drafts')) return response({ items: [], offset: 0, limit: 25, hasMore: false })
    if (url.includes('/files')) return response({ items: [], offset: 0, hasMore: false, canWrite: false, available: false, maxBytes: 0, allowedTypes: ['image/png'] })
    return response({ items: [], offset: 0, hasMore: false, canWrite: true })
  }); vi.stubGlobal('fetch', fetch); return fetch
}
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('CE TR/EN accessible draft behavior authored / NOT RUN', () => {
  it('retains same frozen command through ambiguous response and TR language change', async () => {
    let count = 0; const fetch = transport(async () => ++count === 1 ? Promise.reject(new TypeError('network')) : response(row()))
    const p = props(); const { rerender } = render(<ControlEvidence {...p} />); const user = userEvent.setup()
    await user.selectOptions(await screen.findByLabelText('Vessel context'), vessel)
    await screen.findByText('Draft authoring available'); await user.type(screen.getByLabelText('Title'), 'retained declaration')
    await user.click(screen.getByRole('button', { name: 'Save incomplete draft' })); await screen.findByRole('button', { name: 'Retry same command' })
    expect(screen.getByLabelText('Title')).toBeDisabled(); rerender(<ControlEvidence {...p} language="tr" />)
    expect(screen.getByLabelText('Başlık')).toHaveValue('retained declaration')
    await user.click(screen.getByRole('button', { name: 'Aynı komutu tekrar dene' }))
    await screen.findByText('Taslak kaydedildi; doğrulama ve etkinlik koşulları karşılanmadı.')
    const calls = fetch.mock.calls.filter(([, init]) => init?.method); expect(calls).toHaveLength(2); expect(calls[0][1]?.body).toBe(calls[1][1]?.body); expect(calls[0][1]?.headers).toEqual(calls[1][1]?.headers)
  })
  it('keeps UNKNOWN and incomplete owner/frequency instead of inventing a catalog or allclear', async () => {
    const fetch = transport(); render(<ControlEvidence {...props()} />); const user = userEvent.setup(); await user.selectOptions(await screen.findByLabelText('Vessel context'), vessel)
    await screen.findByText('Draft authoring available'); expect(screen.getByLabelText('Criticality')).toHaveValue('UNKNOWN')
    await user.click(screen.getByRole('button', { name: 'Save incomplete draft' })); await screen.findByText('Draft saved; verification and effectiveness remain unmet.')
    const call = fetch.mock.calls.find(([, init]) => init?.method)!; expect(JSON.parse(call[1]!.body as string).content).toMatchObject({ critical: null, ownerId: null, frequency: null })
    expect(await screen.findByText(/Authoritative applicability report unavailable/)).toBeVisible()
  })
  it('retains a dirty draft across offline transition and asks before register change', async () => {
    transport(); const p = props(); const { rerender } = render(<ControlEvidence {...p} />); const user = userEvent.setup(); await user.selectOptions(await screen.findByLabelText('Vessel context'), vessel)
    await screen.findByText('Draft authoring available'); await user.type(screen.getByLabelText('Title'), 'keep me')
    rerender(<ControlEvidence {...p} offline />); expect(screen.getByRole('button', { name: 'Save incomplete draft' })).toBeDisabled(); expect(screen.getByLabelText('Title')).toHaveValue('keep me')
    await user.selectOptions(screen.getByLabelText('Register'), 'Evidence'); await screen.findByRole('dialog'); await user.click(screen.getByRole('button', { name: 'Keep editing' }))
    expect(screen.getByLabelText('Register')).toHaveValue('Control'); expect(screen.getByLabelText('Title')).toHaveValue('keep me')
  })
  it('shows permission failure without querying scoped files owners or reports', async () => {
    const fetch = transport(undefined, true); render(<ControlEvidence {...props()} />); const user = userEvent.setup(); await user.selectOptions(await screen.findByLabelText('Vessel context'), vessel)
    await screen.findByText('Current context or permission unavailable.'); await waitFor(() => expect(screen.getByRole('button', { name: 'Save incomplete draft' })).toBeDisabled())
    expect(fetch.mock.calls.some(([url]) => url.includes('/owners') || url.includes('/files') || url.includes('/orphans'))).toBe(false)
  })
  it('keeps local values after conflict and offers explicit discard', async () => {
    transport(async () => response({ code: 'version_conflict' }, 409)); render(<ControlEvidence {...props()} />); const user = userEvent.setup(); await user.selectOptions(await screen.findByLabelText('Vessel context'), vessel)
    await screen.findByText('Draft authoring available'); await user.type(screen.getByLabelText('Title'), 'conflicted draft'); await user.click(screen.getByRole('button', { name: 'Save incomplete draft' }))
    await screen.findByText('Version conflict. Draft retained; inspect history before discarding.'); expect(screen.getByLabelText('Title')).toHaveValue('conflicted draft'); expect(screen.getByLabelText('Title')).toBeDisabled()
  })
})
