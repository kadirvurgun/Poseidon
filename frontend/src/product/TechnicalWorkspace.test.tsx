import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TechnicalWorkspace } from './TechnicalWorkspace'

const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const child = '33333333-3333-3333-3333-333333333333'
const asset = { id, vesselId: vessel, parentId: null, name: 'FICTIONAL engine', code: 'SYNTHETIC', level: 'System', version: 7, revision: 1,
  systemFamily: null, equipmentType: null, criticality: null, maker: null, model: null, serialNumber: null, location: null,
  commissionDate: null, responsibleRoleId: null, notes: null, status: 'Draft', provenance: 'SyntheticUnverified' }
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const fetcher = vi.fn()
let mutate: (init: RequestInit) => Promise<Response>
beforeEach(() => {
  window.history.replaceState({}, '', `/app/equipment-hierarchy/${vessel}`)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  mutate = async () => json({ id, version: 8, revision: 2, replayed: false })
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (init?.method === 'POST') return mutate(init)
    if (url.endsWith('/auth/session')) return json({ authenticated: true, tenantId: vessel, actorId: id, csrfToken: 'synthetic-only' })
    if (url.endsWith('/owners')) return json([])
    if (url.endsWith('/history')) return json([{ revision: 1, savedAt: '2026-11-01T00:00:00Z' }])
    if (url.endsWith('/references')) return json({ jobs: [], owners: [], policy: 'DraftDeclarationsOnly' })
    if (url.includes('/maintenance-plan/')) return json({ items: [], canWrite: true, canReadHistory: true })
    return json({ items: [asset, { ...asset, id: child, parentId: id, level: 'Equipment', name: 'FICTIONAL pump' }], complete: true, canWrite: true, canReadHistory: true })
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset() })
async function english() { await userEvent.click(screen.getByRole('button', { name: 'EN', exact: true })); await screen.findByRole('button', { name: 'Add draft', exact: true }) }

describe('DEV-052/053 authored product behavior (mock UI only; execution deferred)', () => {
  it('connects the actual forest to deep-link selection and reveals selected ancestry', async () => {
    window.history.replaceState({}, '', `/app/equipment-hierarchy/${vessel}?id=${child}`)
    render(<TechnicalWorkspace />); await english()
    expect(screen.getByRole('treeitem', { name: 'FICTIONAL pump' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('treeitem', { name: 'FICTIONAL engine' })).toHaveAttribute('aria-expanded', 'true')
    await userEvent.click(screen.getByRole('treeitem', { name: 'FICTIONAL engine' }))
    expect(location.search).toBe(`?id=${id}`)
  })
  it('keeps the complete forest when filtering and selects matching records', async () => {
    render(<TechnicalWorkspace />); await english()
    await userEvent.type(screen.getByLabelText('Filter list by name or code'), 'pump')
    await userEvent.click(screen.getByRole('button', { name: 'FICTIONAL pump', exact: true }))
    expect(screen.getByRole('treeitem', { name: 'FICTIONAL engine' })).toBeVisible()
    expect(location.search).toBe(`?id=${child}`)
  })
  it('preserves dirty drafts when navigation is declined', async () => {
    render(<TechnicalWorkspace />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add draft', exact: true }))
    await userEvent.type(screen.getByLabelText('Equipment name'), 'FICTIONAL retained')
    await userEvent.click(screen.getByRole('button', { name: 'Plans', exact: true }))
    await userEvent.click(screen.getByRole('button', { name: 'Stay', exact: true }))
    expect(screen.getByLabelText('Equipment name')).toHaveValue('FICTIONAL retained')
  })
  it('freezes uncertain payload and reuses exact command ID and idempotency key', async () => {
    let attempts = 0; mutate = async () => { if (attempts++ === 0) throw new TypeError('synthetic transport loss'); return json({ id, version: 8, revision: 2 }) }
    render(<TechnicalWorkspace />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add draft', exact: true }))
    await userEvent.type(screen.getByLabelText('Equipment name'), 'FICTIONAL new')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft', exact: true }))
    await screen.findByText('Result uncertain. Retry the same attempt before editing.')
    expect(screen.getByLabelText('Equipment name')).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Retry same attempt', exact: true }))
    await screen.findByText('Draft saved.')
    const posts = fetcher.mock.calls.filter(([, init]) => init?.method === 'POST')
    expect(posts).toHaveLength(2); expect(posts[0][1].body).toBe(posts[1][1].body)
    expect(posts[0][1].headers['Idempotency-Key']).toBe(posts[1][1].headers['Idempotency-Key'])
  })
  it('blocks stale or revoked writes and retains entered values until discard', async () => {
    mutate = async () => json({ code: 'version_conflict' }, 409)
    render(<TechnicalWorkspace />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add draft', exact: true }))
    await userEvent.type(screen.getByLabelText('Equipment name'), 'FICTIONAL conflict')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft', exact: true }))
    await screen.findByText('The record changed. Reload and resolve your changes.')
    expect(screen.getByRole('button', { name: 'Save draft', exact: true })).toBeDisabled()
    expect(screen.getByLabelText('Equipment name')).toHaveValue('FICTIONAL conflict')
  })
  it('rejects incomplete/paged snapshots instead of fabricating a valid forest', async () => {
    fetcher.mockImplementation(async (url: string) => url.endsWith('/auth/session') ? json({ authenticated: true }) : json({ items: [asset], complete: false, canWrite: true }))
    render(<TechnicalWorkspace />)
    await screen.findByText('Ekipman yüklenemedi.')
    expect(screen.queryByRole('tree')).not.toBeInTheDocument()
  })
  it('uses exact strings for decimal declarations and does not fill missing readings with zero', async () => {
    window.history.replaceState({}, '', `/app/maintenance-plan/${vessel}`)
    render(<TechnicalWorkspace />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Add draft', exact: true }))
    await userEvent.selectOptions(screen.getByLabelText('Trigger', { exact: true }), 'Meter')
    await userEvent.type(screen.getByLabelText('Interval value (exact decimal)'), '0.100000000000000002')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft', exact: true }))
    await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true))
    const post = fetcher.mock.calls.find(([, init]) => init?.method === 'POST')!
    const body = JSON.parse(post[1].body)
    expect(body.inputs.intervalValue).toBe('0.100000000000000002'); expect(body.inputs.currentMeter).toBeUndefined()
    expect(body.ownerRoleId).toBeNull(); expect(body.jobId).toBeNull()
  })
  it('clears unauthorized snapshot data and shows operator load errors', async () => {
    fetcher.mockImplementation(async (url: string) => url.endsWith('/auth/session') ? json({ authenticated: true }) : json({ code: 'permission_denied' }, 403))
    render(<TechnicalWorkspace />); await screen.findByText('Erişim kullanılamıyor.')
    expect(screen.queryByText('FICTIONAL engine')).not.toBeInTheDocument()
    expect(screen.queryByRole('tree')).not.toBeInTheDocument()
  })
})
