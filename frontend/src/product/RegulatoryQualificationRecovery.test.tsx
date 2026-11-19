import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { ApiError } from './api'
import * as api from './qualification-api'
import { qualifiedFixture } from './qualification.test-fixtures'
import { RegulatoryQualification } from './RegulatoryQualification'
import { localizedText } from '../i18n/messages'

afterEach(() => vi.restoreAllMocks())
const tenant = '10000000-0000-4000-8000-000000000001'
const other = '10000000-0000-4000-8000-000000000009'
function setup() {
  const row = { ...qualifiedFixture(), state: 'DRAFT' }
  const originalOwner = `${tenant}/${row.actorId}`
  const state = { owner: originalOwner, sessionReads: 0, denied: false, unavailable: false,
    page: { hasMore: false, items: [row], owners: [], actions: ['draft'], authorities: [{ action: 'draft', authorityId: row.authorityId }] } as api.Page }
  vi.spyOn(api, 'loadSession').mockImplementation(async () => {
    state.sessionReads++
    return { owner: state.owner, csrf: crypto.randomUUID() }
  })
  vi.spyOn(api, 'load').mockImplementation(async () => {
    if (state.denied) throw new ApiError(403)
    if (state.unavailable) throw new ApiError(503)
    return state.page
  })
  const atPost: number[] = []
  const sent = vi.spyOn(api, 'send').mockImplementation(async (_rule, attempt) => {
    atPost.push(state.sessionReads)
    const saved = { ...row, revision: 4, version: 30, content: JSON.parse(attempt.body).content }
    state.page = { ...state.page, items: [saved] }
    return saved
  })
  const props = { rule: row.ruleId, ruleRevision: 1, vessel: '', language: 'en' as const,
    csrf: crypto.randomUUID(), offline: false, onDirty: vi.fn(), onSaving: vi.fn() }
  return { state, row, originalOwner, sent, atPost, props }
}
const save = () => fireEvent.click(screen.getByRole('button', { name: 'Save qualification draft' }))
const retry = () => fireEvent.click(screen.getByRole('button', { name: 'Retry exact qualification command' }))
const recheck = () => fireEvent.click(screen.getByRole('button', { name: 'Retry source read' }))
const unknown = () => screen.findByText('Outcome uncertain. The exact original attempt is locked until retry.')

it.each(['http', 'capability', 'replacement-authority'] as const)('retains a committed-unknown envelope through %s denial and same-owner reauthorization', async denial => {
  const h = setup(), successful = h.sent.getMockImplementation()!
  h.sent.mockImplementationOnce(async () => { h.atPost.push(h.state.sessionReads); throw new Error('synthetic response lost') })
  const view = render(<RegulatoryQualification {...h.props} />)
  fireEvent.change(await screen.findByLabelText('Publisher'), { target: { value: `FICTIONAL-${crypto.randomUUID()}` } })
  const location = window.location.href, beforeFirst = h.state.sessionReads
  save(); await unknown()
  const original = h.sent.mock.calls[0][1]
  expect(h.atPost[0]).toBeGreaterThan(beforeFirst)
  expect(h.sent.mock.calls[0][2] === h.props.csrf).toBe(false)
  if (denial === 'http') h.sent.mockImplementationOnce(async () => { h.atPost.push(h.state.sessionReads); throw new ApiError(403) })
  else if (denial === 'capability') h.state.page = { ...h.state.page, actions: [], authorities: [] }
  else h.state.page = { ...h.state.page, authorities: [{ action: 'draft', authorityId: other }] }
  const beforeDenied = h.state.sessionReads
  retry()
  await screen.findByText('Assigned authority required.')
  expect(h.state.sessionReads).toBeGreaterThan(beforeDenied)
  expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeDisabled()
  expect(screen.queryByLabelText('Publisher')).not.toBeInTheDocument()
  expect(h.props.onSaving).toHaveBeenLastCalledWith(true)
  h.state.page = { ...h.state.page, actions: ['draft'], authorities: [{ action: 'draft', authorityId: h.row.authorityId }] }
  h.sent.mockImplementation(successful)
  recheck()
  expect(await screen.findByLabelText('Publisher')).toBeDisabled()
  const beforeRetry = h.state.sessionReads
  retry()
  await screen.findByText('Human record saved. No operational release follows.')
  await waitFor(() => expect(screen.getByLabelText('Publisher')).toBeEnabled())
  expect(h.atPost.at(-1)!).toBeGreaterThan(beforeRetry)
  expect(h.sent.mock.calls.every(call => call[1].body === original.body && call[1].key === original.key)).toBe(true)
  expect(new Set(h.sent.mock.calls.map(call => call[2])).size).toBe(h.sent.mock.calls.length)
  expect(window.location.href).toBe(location)
  await waitFor(() => expect(h.props.onSaving).toHaveBeenLastCalledWith(false))
  view.unmount()
})

it.each([401, 404, 409])('a later %s does not turn the earlier uncertain write into a rollback or discardable draft', async code => {
  const h = setup()
  h.sent.mockRejectedValueOnce(new Error('synthetic loss')).mockRejectedValueOnce(new ApiError(code))
  render(<RegulatoryQualification {...h.props} />)
  await screen.findByLabelText('Publisher'); save(); await unknown(); retry()
  await waitFor(() => expect(h.sent).toHaveBeenCalledTimes(2))
  await screen.findByRole('alert')
  await waitFor(() => expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeDisabled())
  expect(h.sent.mock.calls[0][1].body === h.sent.mock.calls[1][1].body).toBe(true)
  expect(h.sent.mock.calls[0][1].key === h.sent.mock.calls[1][1].key).toBe(true)
  expect(screen.getByText('Outcome uncertain. The exact original attempt is locked until retry.')).toBeVisible()
  if (code === 401) expect(screen.getByRole('link', { name: 'Sign in again' })).toHaveAttribute('target', '_blank')
})

it.each(['actor', 'tenant', 'rule', 'vessel'] as const)('fences a retained command and private DOM when the %s changes', async changed => {
  const h = setup(), successful = h.sent.getMockImplementation()!
  h.sent.mockRejectedValueOnce(new Error('synthetic loss'))
  const view = render(<RegulatoryQualification {...h.props} />)
  const privateDraft = `FICTIONAL-${crypto.randomUUID()}`
  fireEvent.change(await screen.findByLabelText('Publisher'), { target: { value: privateDraft } })
  save(); await unknown()
  const original = h.sent.mock.calls[0][1]
  if (changed === 'actor') h.state.owner = `${tenant}/${other}`
  else if (changed === 'tenant') h.state.owner = `${other}/${h.row.actorId}`
  else view.rerender(<RegulatoryQualification {...h.props} {...(changed === 'rule' ? { rule: other } : { vessel: other })} />)
  recheck()
  await screen.findByText('Assigned authority required.')
  expect(screen.queryByDisplayValue(privateDraft)).not.toBeInTheDocument()
  expect(screen.queryByText(h.row.hash)).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Retry exact qualification command' })).toBeDisabled()
  expect(h.sent).toHaveBeenCalledTimes(1)
  h.state.owner = h.originalOwner
  view.rerender(<RegulatoryQualification {...h.props} />)
  recheck()
  expect(await screen.findByLabelText('Publisher')).toBeDisabled()
  h.sent.mockImplementation(successful)
  retry()
  await waitFor(() => expect(h.sent).toHaveBeenCalledTimes(2))
  expect(h.sent.mock.calls[1][1].body === original.body && h.sent.mock.calls[1][1].key === original.key).toBe(true)
})

it('a confirmed receipt remains read-only at the same rule/route after readback failure and source recheck never resends', async () => {
  const h = setup(), successful = h.sent.getMockImplementation()!
  h.sent.mockImplementation(async (...args) => { const result = await successful(...args); h.state.unavailable = true; return result })
  render(<RegulatoryQualification {...h.props} />)
  await screen.findByLabelText('Publisher')
  const location = window.location.href
  save(); await screen.findByText('Human record saved. No operational release follows.')
  await screen.findByRole('alert')
  expect(screen.queryByLabelText('Publisher')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Retry exact qualification command' })).not.toBeInTheDocument()
  expect(h.sent).toHaveBeenCalledTimes(1)
  expect(window.location.href).toBe(location)
  h.state.unavailable = false; recheck()
  expect(await screen.findByLabelText('Publisher')).toBeEnabled()
  expect(h.sent).toHaveBeenCalledTimes(1)
})

it('does not confirm a valid receipt for a different post-write session owner', async () => {
  const h = setup(), successful = h.sent.getMockImplementation()!
  h.sent.mockImplementationOnce(async (...args) => { const result = await successful(...args); h.state.owner = `${tenant}/${other}`; return result })
  render(<RegulatoryQualification {...h.props} />)
  await screen.findByLabelText('Publisher'); save(); await unknown()
  expect(screen.queryByText('Human record saved. No operational release follows.')).not.toBeInTheDocument()
  expect(screen.queryByLabelText('Publisher')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeDisabled()
  const original = h.sent.mock.calls[0][1]
  h.state.owner = h.originalOwner; recheck()
  await screen.findByLabelText('Publisher'); retry()
  await screen.findByText('Human record saved. No operational release follows.')
  expect(h.sent.mock.calls[1][1].body === original.body && h.sent.mock.calls[1][1].key === original.key).toBe(true)
})

it('does not accept a prior event projection as the new command receipt', async () => {
  const h = setup()
  h.sent.mockResolvedValueOnce(h.row)
  render(<RegulatoryQualification {...h.props} />)
  await screen.findByLabelText('Publisher'); save(); await unknown()
  expect(screen.queryByText('Human record saved. No operational release follows.')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeDisabled()
  retry()
  await screen.findByText('Human record saved. No operational release follows.')
  expect(h.sent.mock.calls[0][1].body === h.sent.mock.calls[1][1].body && h.sent.mock.calls[0][1].key === h.sent.mock.calls[1][1].key).toBe(true)
})

it('withdraws readable private draft/history on source denial and treats initial validation failure separately', async () => {
  const h = setup()
  render(<RegulatoryQualification {...h.props} />)
  await screen.findByDisplayValue(h.row.content.publisher!)
  h.state.denied = true
  await act(async () => { window.dispatchEvent(new Event('focus')) })
  await screen.findByText('Assigned authority required.')
  expect(screen.queryByDisplayValue(h.row.content.publisher!)).not.toBeInTheDocument()
  expect(screen.queryByText(h.row.hash)).not.toBeInTheDocument()
  h.state.denied = false; recheck()
  await screen.findByLabelText('Publisher')
  h.sent.mockRejectedValueOnce(new ApiError(400))
  save(); await screen.findByRole('alert')
  expect(screen.queryByRole('button', { name: 'Retry exact qualification command' })).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeEnabled()
})


it('binds a selected lifecycle choice before capture and withdraws it from another session owner', async () => {
  const h = setup()
  h.state.page = { ...h.state.page, items: [{ ...h.row, state: 'IN_REVIEW' }], actions: ['qualify'], authorities: [{ action: 'qualify', authorityId: h.row.authorityId }] }
  render(<RegulatoryQualification {...h.props} />)
  fireEvent.change(await screen.findByLabelText('Record lifecycle state'), { target: { value: 'IN_FORCE' } })
  h.state.owner = `${tenant}/${other}`
  await act(async () => { window.dispatchEvent(new Event('focus')) })
  await screen.findByText('Assigned authority required.')
  expect(screen.queryByLabelText('Record lifecycle state')).not.toBeInTheDocument()
  expect(h.props.onSaving).toHaveBeenLastCalledWith(true)
  expect(h.sent).not.toHaveBeenCalled()
  h.state.owner = h.originalOwner; recheck()
  expect(await screen.findByLabelText('Record lifecycle state')).toHaveValue('IN_FORCE')
  expect(h.sent).not.toHaveBeenCalled()
})

it('retains a private draft on current-version drift without silently rebasing its original command', async () => {
  const h = setup(), privateDraft = `FICTIONAL-${crypto.randomUUID()}`
  render(<RegulatoryQualification {...h.props} />)
  fireEvent.change(await screen.findByLabelText('Publisher'), { target: { value: privateDraft } })
  h.state.page = { ...h.state.page, items: [{ ...h.row, version: h.row.version + 1 }] }
  recheck()
  expect(await screen.findByDisplayValue(privateDraft)).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Save qualification draft' })).toBeDisabled()
  expect(h.sent).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole('button', { name: 'Discard local changes and reload' }))
  expect(await screen.findByDisplayValue(h.row.content.publisher!)).toBeEnabled()
})

it('retains a malformed successful response as uncertain until an explicit exact retry', async () => {
  const h = setup()
  h.sent.mockRejectedValueOnce(new ApiError(502, 'invalid_qualification_contract'))
  render(<RegulatoryQualification {...h.props} />)
  await screen.findByLabelText('Publisher'); save(); await unknown()
  expect(h.sent).toHaveBeenCalledTimes(1)
  expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeDisabled()
  recheck()
  await screen.findByLabelText('Publisher')
  expect(h.sent).toHaveBeenCalledTimes(1)
  retry()
  await screen.findByText('Human record saved. No operational release follows.')
  expect(h.sent.mock.calls[0][1].body === h.sent.mock.calls[1][1].body && h.sent.mock.calls[0][1].key === h.sent.mock.calls[1][1].key).toBe(true)
})

it('keeps the same draft nodes and captured command when the presentation language changes', async () => {
  const h = setup(), successful = h.sent.getMockImplementation()!
  const view = render(<RegulatoryQualification {...h.props} />)
  const publisher = await screen.findByLabelText('Publisher'), value = `FICTIONAL-${crypto.randomUUID()}`
  fireEvent.change(publisher, { target: { value } })
  view.rerender(<RegulatoryQualification {...h.props} language="tr" />)
  expect(screen.getByDisplayValue(value)).toBe(publisher)
  h.sent.mockRejectedValueOnce(new Error('synthetic response lost'))
  fireEvent.click(screen.getByRole('button', { name: localizedText('tr', 'pairs.m_1da533f3cdfb3b33') }))
  await screen.findByText(localizedText('tr', 'pairs.m_62930366e47b200e'))
  const original = h.sent.mock.calls[0][1]
  view.rerender(<RegulatoryQualification {...h.props} />)
  h.sent.mockImplementation(successful)
  retry()
  await screen.findByText('Human record saved. No operational release follows.')
  expect(h.sent.mock.calls[1][1].body === original.body && h.sent.mock.calls[1][1].key === original.key).toBe(true)
})

it.each(['unknown', 'confirmed-readback-unavailable', 'initial-rejection'] as const)(
  'does not resurrect a late selected file after the command becomes %s', async outcome => {
    const h = setup(), successful = h.sent.getMockImplementation()!
    if (outcome === 'unknown') h.sent.mockRejectedValueOnce(new Error('synthetic response loss'))
    else if (outcome === 'initial-rejection') h.sent.mockRejectedValueOnce(new ApiError(400))
    else h.sent.mockImplementationOnce(async (...args) => {
      const result = await successful(...args)
      h.state.unavailable = true
      return result
    })
    render(<RegulatoryQualification {...h.props} />)
    await waitFor(() => expect(screen.getByLabelText('Publisher')).toBeEnabled())
    let finish!: (value: ArrayBuffer) => void
    const reading = new Promise<ArrayBuffer>(resolve => { finish = resolve })
    const file = Object.assign(new File([new Uint8Array([1, 2, 3])], 'fictional-pending-source.txt'), {
      arrayBuffer: vi.fn(() => reading),
    })
    fireEvent.change(screen.getByLabelText('Exact source bytes (maximum 2 MiB)'), { target: { files: [file] } })
    expect(file.arrayBuffer).toHaveBeenCalledOnce()
    save()
    if (outcome === 'unknown') await unknown()
    else {
      await screen.findByRole('alert')
      if (outcome === 'confirmed-readback-unavailable')
        await screen.findByText('Human record saved. No operational release follows.')
    }
    await waitFor(() => expect(h.props.onDirty).toHaveBeenLastCalledWith(outcome === 'unknown'))
    expect(screen.queryByLabelText('Publisher')).not.toBeInTheDocument()
    const original = h.sent.mock.calls[0][1], dirtyNotifications = h.props.onDirty.mock.calls.length
    await act(async () => { finish(new Uint8Array([1, 2, 3]).buffer); await reading })
    // A late read must neither rebind a private draft nor produce another write.
    // The UI's public navigation guard must retain its already settled state.
    expect(h.props.onDirty).toHaveBeenCalledTimes(dirtyNotifications)
    expect(h.sent).toHaveBeenCalledTimes(1)
    expect(screen.queryByLabelText('Publisher')).not.toBeInTheDocument()
    if (outcome === 'unknown') {
      expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeDisabled()
      retry()
      await screen.findByText('Human record saved. No operational release follows.')
      expect(h.sent.mock.calls[1][1].body === original.body && h.sent.mock.calls[1][1].key === original.key).toBe(true)
    }
  },
)

it('reports a current source-file read failure without a mutation or uncertain command', async () => {
  const h = setup()
  render(<RegulatoryQualification {...h.props} />)
  await waitFor(() => expect(screen.getByLabelText('Publisher')).toBeEnabled())
  const file = Object.assign(new File([new Uint8Array([1])], 'fictional-unreadable-source.txt'), {
    arrayBuffer: vi.fn(async () => { throw new Error('synthetic file read unavailable') }),
  })
  fireEvent.change(screen.getByLabelText('Exact source bytes (maximum 2 MiB)'), { target: { files: [file] } })
  await screen.findByRole('alert')
  expect(h.sent).not.toHaveBeenCalled()
  expect(h.props.onDirty).toHaveBeenLastCalledWith(false)
  expect(screen.queryByText('Outcome uncertain. The exact original attempt is locked until retry.')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Save qualification draft' })).toBeEnabled()
})

// S3-WPA / DEV-026 open QOR finding: a hard reload previously destroyed the in-memory
// UNKNOWN envelope. Mocked transport only; real browser reload journey DEFERRED.
const storedKey = (rule: string) => `poseidon.qualification.unknown.v1:${rule}`
it('retains the UNKNOWN envelope across a hard reload and retries only the exact original attempt', async () => {
  const h = setup(), successful = h.sent.getMockImplementation()!
  h.sent.mockImplementationOnce(async () => { throw new Error('synthetic response lost') })
  const first = render(<RegulatoryQualification {...h.props} />)
  fireEvent.change(await screen.findByLabelText('Publisher'), { target: { value: 'FICTIONAL-reload-publisher' } })
  save(); await unknown()
  const original = h.sent.mock.calls[0][1]
  first.unmount() // Hard reload: every React state, ref and closure is gone.
  const stored = JSON.parse(window.sessionStorage.getItem(storedKey(h.row.ruleId))!) as Record<string, unknown>
  expect(Object.keys(stored).sort()).toEqual(['action', 'body', 'context', 'key', 'nextRevision', 'owner', 'rule', 'unknown', 'version'])
  expect(stored.unknown).toBe(true); expect(JSON.stringify(stored)).not.toContain(h.props.csrf)
  const reloaded = render(<RegulatoryQualification {...h.props} />)
  await unknown()
  expect(await screen.findByLabelText('Publisher')).toBeDisabled()
  expect(screen.getByLabelText('Publisher')).toHaveValue('FICTIONAL-reload-publisher')
  expect(screen.getByRole('button', { name: 'Discard local changes and reload' })).toBeDisabled()
  expect(h.props.onSaving).toHaveBeenLastCalledWith(true)
  expect(h.sent).toHaveBeenCalledTimes(1) // Nothing is resent implicitly on reload.
  h.sent.mockImplementation(successful)
  retry()
  await screen.findByText('Human record saved. No operational release follows.')
  expect(h.sent).toHaveBeenCalledTimes(2)
  expect(h.sent.mock.calls[1][1].body).toBe(original.body); expect(h.sent.mock.calls[1][1].key).toBe(original.key)
  expect(window.sessionStorage.getItem(storedKey(h.row.ruleId))).toBeNull()
  reloaded.unmount()
})

it('does not expose or replay a reloaded UNKNOWN envelope for a different authenticated owner', async () => {
  const h = setup()
  h.sent.mockImplementationOnce(async () => { throw new Error('synthetic response lost') })
  const first = render(<RegulatoryQualification {...h.props} />)
  fireEvent.change(await screen.findByLabelText('Publisher'), { target: { value: 'FICTIONAL-private-publisher' } })
  save(); await unknown(); first.unmount()
  h.state.owner = `${other}/${h.row.actorId}`
  const reloaded = render(<RegulatoryQualification {...h.props} />)
  await screen.findByRole('alert')
  await waitFor(() => expect(window.sessionStorage.getItem(storedKey(h.row.ruleId))).toBeNull())
  expect(screen.queryByDisplayValue('FICTIONAL-private-publisher')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Retry exact qualification command' })).toBeDisabled()
  expect(h.sent).toHaveBeenCalledTimes(1)
  reloaded.unmount()
})

it.each([
  '{not json', JSON.stringify({ unknown: true }),
  JSON.stringify({ owner: 'x', context: 'c', version: 0, rule: 'other-rule', action: 'events', body: '{}', key: crypto.randomUUID(), unknown: true, nextRevision: 1 }),
])('discards a malformed stored envelope instead of inventing an attempt: %s', async raw => {
  const h = setup()
  window.sessionStorage.setItem(storedKey(h.row.ruleId), raw)
  const view = render(<RegulatoryQualification {...h.props} />)
  expect(await screen.findByLabelText('Publisher')).toBeEnabled()
  expect(screen.queryByText('Outcome uncertain. The exact original attempt is locked until retry.')).not.toBeInTheDocument()
  expect(window.sessionStorage.getItem(storedKey(h.row.ruleId))).toBeNull()
  view.unmount()
})
