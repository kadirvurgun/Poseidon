import { render, screen, waitFor, fireEvent, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RegulatoryQualification } from './RegulatoryQualification'
import * as api from './qualification-api'
import { qualifiedFixture } from './qualification.test-fixtures'

beforeEach(() => vi.spyOn(api, 'loadSession').mockImplementation(async () => ({ owner: `10000000-0000-4000-8000-000000000001/${qualifiedFixture().actorId}`, csrf: crypto.randomUUID() })))
afterEach(() => vi.restoreAllMocks())
const id = '10000000-0000-4000-8000-000000000003'
const props = () => ({ rule: id, ruleRevision: 1, vessel: '', language: 'en' as const, csrf: 'test-csrf-sentinel', offline: false, onDirty: vi.fn(), onSaving: vi.fn() })
describe('Source qualification accessible command UI (authored, NOT RUN)', () => {
  it('saves untouched unknown policy choices as null, and records No and Yes only after explicit selection', async () => {
    vi.spyOn(api, 'load').mockResolvedValue({ hasMore: false, items: [], owners: [], actions: ['draft'], authorities: [{ action: 'draft', authorityId: id }] })
    let revision = 0
    const sent = vi.spyOn(api, 'send').mockImplementation(async (_rule, attempt) => {
      const body = JSON.parse(attempt.body)
      const saved = { ...qualifiedFixture(), revision: ++revision, state: 'DRAFT', content: body.content }
      vi.mocked(api.load).mockResolvedValue({ hasMore: false, items: [saved], owners: [], actions: ['draft'], authorities: [{ action: 'draft', authorityId: id }] })
      return saved
    })
    render(<RegulatoryQualification {...props()} />)
    await screen.findByText('No qualification. Incomplete drafts may be saved.')
    let file = screen.getByLabelText('Source requires an immutable file')
    let acknowledgment = screen.getByLabelText('Source requires exact revision acknowledgment')
    expect(file).toHaveValue(''); expect(acknowledgment).toHaveValue('')
    fireEvent.change(screen.getByLabelText('Frequency'), { target: { value: 'FICTIONAL frequency' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save qualification draft' }))
    await waitFor(() => expect(sent).toHaveBeenCalledTimes(1))
    const first = JSON.parse(sent.mock.calls[0][1].body)
    expect(first.content.control.requireFile).toBeNull(); expect(first.content.control.requireAcknowledgment).toBeNull()
    await waitFor(() => expect(screen.getByLabelText('Source requires an immutable file')).toBeEnabled())
    file = screen.getByLabelText('Source requires an immutable file'); acknowledgment = screen.getByLabelText('Source requires exact revision acknowledgment')
    fireEvent.change(file, { target: { value: 'false' } })
    fireEvent.change(acknowledgment, { target: { value: 'true' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save qualification draft' }))
    await waitFor(() => expect(sent).toHaveBeenCalledTimes(2))
    const second = JSON.parse(sent.mock.calls[1][1].body)
    expect(second.content.control.requireFile).toBe(false); expect(second.content.control.requireAcknowledgment).toBe(true)
    expect(second.content.control.acknowledgmentRole).toBeNull() // incomplete drafts remain possible
  })
  it('renders read-only source lineage without offering an unassigned qualification command', async () => {
    const current = qualifiedFixture(), sent = vi.spyOn(api, 'send')
    vi.spyOn(api, 'load').mockResolvedValue({ hasMore: false, items: [current], owners: [], actions: [], authorities: [] })
    render(<RegulatoryQualification {...props()} />)
    const lineage = await screen.findByText(/^IN_FORCE · 3 · /, { selector: '.regulatory-qualification > p' })
    expect(lineage).toBeVisible()
    expect(screen.getByRole('button', { name: 'Record assigned human qualification' })).toBeDisabled()
    expect(within(lineage).getByText(`${current.sourceHash} · ${current.hash}`, { exact: true })).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Record assigned human qualification' }))
    expect(sent).not.toHaveBeenCalled()
  })
  it('uses the exact current rule, xmin and server authority for an independent human lifecycle command', async () => {
    const row = { ...qualifiedFixture(), state: 'IN_REVIEW' }
    vi.spyOn(api, 'load').mockResolvedValue({ hasMore: false, items: [row], owners: [], actions: ['qualify'], authorities: [{ action: 'qualify', authorityId: id }] })
    const sent = vi.spyOn(api, 'send').mockImplementation(async () => {
      const saved = { ...row, revision: row.revision + 1, state: 'IN_FORCE' }
      vi.mocked(api.load).mockResolvedValue({ hasMore: false, items: [saved], owners: [], actions: ['qualify'], authorities: [{ action: 'qualify', authorityId: id }] })
      return saved
    })
    render(<RegulatoryQualification {...props()} />)
    await screen.findByText(/^IN_REVIEW/)
    fireEvent.change(screen.getByLabelText('Record lifecycle state'), { target: { value: 'IN_FORCE' } })
    fireEvent.click(screen.getByRole('button', { name: 'Record assigned human qualification' }))
    await waitFor(() => expect(sent).toHaveBeenCalledTimes(1))
    const attempt = sent.mock.calls[0][1]
    expect(JSON.parse(attempt.body)).toEqual({ expectedVersion: 29, ruleRevision: 1, authorityId: id, state: 'IN_FORCE', content: null })
    expect(attempt.key).toMatch(/^[a-f0-9-]{36}$/)
  })
  it('retains an ambiguous command for identical retry and disables commands offline', async () => {
    vi.spyOn(api, 'load').mockResolvedValue({ hasMore: false, items: [], owners: [], actions: ['draft'], authorities: [{ action: 'draft', authorityId: id }] })
    const sent = vi.spyOn(api, 'send').mockRejectedValue(new Error('synthetic response lost'))
    const p = props(); const view = render(<RegulatoryQualification {...p} />)
    await screen.findByText('No qualification. Incomplete drafts may be saved.')
    fireEvent.change(screen.getByLabelText('Publisher'), { target: { value: 'FICTIONAL publisher' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save qualification draft' }))
    await screen.findByRole('button', { name: 'Retry exact qualification command' })
    fireEvent.click(screen.getByRole('button', { name: 'Retry exact qualification command' }))
    await waitFor(() => expect(sent).toHaveBeenCalledTimes(2))
    expect(sent.mock.calls[0][1].body === sent.mock.calls[1][1].body).toBe(true)
    expect(sent.mock.calls[0][1].key === sent.mock.calls[1][1].key).toBe(true)
    view.rerender(<RegulatoryQualification {...p} offline />)
    expect(screen.getByRole('button', { name: 'Retry exact qualification command' })).toBeDisabled()
    expect(p.onDirty).toHaveBeenCalledWith(true)
  })
  it('provides Turkish labels and explicit missing authority state', async () => {
    vi.spyOn(api, 'load').mockResolvedValue({ hasMore: false, items: [], owners: [], actions: [], authorities: [] })
    render(<RegulatoryQualification {...props()} language="tr" />)
    expect(await screen.findByText('Nitelendirme yok. Eksik taslaklar kaydedilebilir.')).toBeVisible()
    expect(screen.getByLabelText('Yayıncı')).toBeDisabled()
  })
})
