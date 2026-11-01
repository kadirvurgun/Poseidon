import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ImportWorkspace } from './ImportWorkspace'
import * as api from './import-api'

const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222', previewId = '33333333-3333-3333-3333-333333333333'
const template: api.ImportTemplate = { category: 'equipment', version: 'v1/equipment', fingerprint: 'ABC', fields: [{ key: 'record_key', requiredInput: true, userWritable: true }, { key: 'level', requiredInput: true, userWritable: true }], allowSourceColumnReuse: false, format: 'xlsx-single-sheet', maxBytes: 2097152, maxRows: 500, maxColumns: 40, maxCellCharacters: 4096, numericPolicy: 'exact', commitPolicy: 'atomic trial' }
const batch: api.ImportBatch = { id, category: 'equipment', revision: 2, version: 7, state: 'MappingDraft', template, headers: ['PRIVATE HEADER', 'Level'], bindings: [{ sourceColumn: 0, targetField: 'record_key' }, { sourceColumn: 1, targetField: 'level' }], rowCount: 26, sourceHash: 'HASH', createdAt: '2026-11-01T00:00:00Z', reviewerState: 'Unassigned', committedAt: null, committedPreviewId: null }
const page = (offset = 0): api.Preview => ({ previewId, revision: '2', templateVersion: template.version, templateFingerprint: template.fingerprint, totalRows: 26, validRows: 26, rejectedRows: 0, offset, limit: 25, hasMore: offset === 0, rows: Array.from({ length: offset === 0 ? 25 : 1 }, (_, i) => ({ rowNumber: offset + i + 2, outcome: 'valid', issues: [] })) })
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const fetcher = vi.fn()
let commit: (init: RequestInit) => Promise<Response>
beforeEach(() => {
  history.replaceState(null, '', `/app/excel-migration/${vessel}?batch=${id}`)
  commit = async () => json({ batchId: id, state: 'Committed', importedRows: 26, previewId, replayed: false })
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return json({ authenticated: true, canRead: true, tenantId: vessel, actorId: id, csrfToken: 'SYNTHETIC-SESSION' })
    if (url.endsWith('/templates')) return json([template])
    if (url.endsWith('/mapping')) return json({ ...batch, state: 'MappingDraft' })
    if (url.endsWith('/commit')) return commit(init!)
    if (url.endsWith('/preview')) return json(page())
    if (url.includes('/previews/')) return json(page(url.includes('offset=25') ? 25 : 0))
    if (url.includes('/imports?')) return json({ items: [], hasMore: false })
    return json(batch)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset() })
async function english() { await userEvent.click(screen.getByRole('button', { name: 'EN', exact: true })); await screen.findByRole('button', { name: 'Validate and preview', exact: true }) }

describe('DEV-069 connected importer (authored UI mocks; execution DEFERRED)', () => {
  it('makes both handoff components reachable and never commits during preview', async () => {
    render(<ImportWorkspace vessel={vessel} />); await english()
    await userEvent.click(screen.getByRole('button', { name: 'Validate and preview', exact: true }))
    await screen.findByRole('table', { name: 'Source row outcomes' })
    expect(fetcher.mock.calls.some(([url]) => String(url).endsWith('/mapping'))).toBe(true)
    expect(fetcher.mock.calls.some(([url]) => String(url).endsWith('/commit'))).toBe(false)
    expect(location.search).toContain(`preview=${previewId}`)
  })
  it('loads real offset pages with the exact schema and preview context', async () => {
    render(<ImportWorkspace vessel={vessel} />); await english(); await userEvent.click(screen.getByRole('button', { name: 'Validate and preview', exact: true }))
    await userEvent.click(await screen.findByRole('button', { name: 'Next page', exact: true }))
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => String(url).includes('offset=25') && String(url).includes('fingerprint=ABC'))).toBe(true))
    expect(await screen.findByText('27', { selector: 'th' })).toBeVisible()
  })
  it('retains an uncertain commit key and does not enable mapping edits', async () => {
    const keys: unknown[] = []
    commit = async init => { keys.push((init.headers as Record<string, string>)['Idempotency-Key']); if (keys.length === 1) throw new TypeError('network'); return json({ batchId: id, state: 'Committed', importedRows: 26, previewId, replayed: true }) }
    render(<ImportWorkspace vessel={vessel} />); await english(); await userEvent.click(screen.getByRole('button', { name: 'Validate and preview', exact: true }))
    await userEvent.click(await screen.findByRole('button', { name: 'Commit atomic trial import', exact: true }))
    await userEvent.click(await screen.findByRole('button', { name: 'Retry same commit', exact: true }))
    expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]); expect(await screen.findByText(/Committed rows/)).toHaveTextContent('26')
  })
  it('clears result authorization on denied deep links without exposing headers', async () => {
    fetcher.mockImplementation(async (url: string) => url.endsWith('/auth/session') ? json({ authenticated: true, canRead: true, tenantId: vessel, actorId: id, csrfToken: 'SYNTHETIC' }) : json({ code: 'permission_denied', raw: 'PRIVATE SERVER CONTENT' }, 403))
    render(<ImportWorkspace vessel={vessel} />); await userEvent.click(screen.getByRole('button', { name: 'EN', exact: true }))
    expect(await screen.findByText('Import access denied.')).toBeVisible(); expect(screen.queryByText(/PRIVATE/)).not.toBeInTheDocument(); expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
  it('invalidates preview immediately when the parent-controlled mapping changes', async () => {
    render(<ImportWorkspace vessel={vessel} />); await english(); await userEvent.click(screen.getByRole('button', { name: 'Validate and preview', exact: true }))
    await screen.findByRole('table'); await userEvent.selectOptions(screen.getByLabelText('Record key (UUID) (required)'), '')
    expect(screen.queryByRole('table')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Commit atomic trial import' })).not.toBeInTheDocument()
  })
  it('maps unknown codes to generic presentation and refuses schema substitution', () => {
    const p = page(); p.rows[0] = { rowNumber: 2, outcome: 'rejected', issues: [{ code: 'PRIVATE SERVER DIAGNOSTIC', fieldOrdinal: 0 }] }; p.validRows--; p.rejectedRows++
    const result = api.results(p, template, 'actor/tenant/session/vessel/upload/schema')
    expect(result.rows[0].issues[0].code).toBe('unknown'); expect(JSON.stringify(result)).not.toContain('PRIVATE')
    expect(() => api.results({ ...p, templateFingerprint: 'CHANGED' }, template, 'context')).toThrow()
  })
  it('aborts pending template and batch requests on unmount', async () => {
    const signals: AbortSignal[] = []
    fetcher.mockImplementation(async (url: string, init: RequestInit) => { if (url.endsWith('/auth/session')) return json({ authenticated: true, canRead: true, actorId: id, tenantId: vessel, csrfToken: 'SYNTHETIC' }); signals.push(init.signal! as AbortSignal); return new Promise(() => {}) })
    const view = render(<ImportWorkspace vessel={vessel} />); await waitFor(() => expect(signals.length).toBeGreaterThan(0)); view.unmount(); expect(signals.every(s => s.aborted)).toBe(true)
  })
})
