import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { AuditWorkspace } from './AuditWorkspace'
import { changes, decodeAuditPage, decodeAuditRecord } from './audit-api'
import { locationPath, readLocation } from './workspace-location'

const id = '10000000-0000-0000-0000-000000000001'
const vessel = '10000000-0000-0000-0000-000000000002'
const record = { id, occurredAt: '2026-11-01T00:00:00Z', actorReference: null, actorType: 'User', action: 'Modified', entityType: 'RegulatoryDraft', resourceId: vessel, vesselId: vessel, roleContext: null, capabilityContext: 'audit.read,directory.read', changeSummary: '[{"field":"ContentJson","before":null,"after":null,"evidence":"RevisionReference"}]', beforeVersion: 21, afterVersion: 22, beforeRevision: 1, afterRevision: 2, reasonStatus: 'NotRequiredForCommand', correlationId: null, metadataStatus: 'Recorded', revisionHref: `/api/v1/audit/${id}/revision` }
const page = { items: [record], total: 1, limit: 25, nextCursor: null }
const response = (value: unknown, status = 200) => ({ ok: status === 200, status, json: async () => value }) as Response
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('renders honest TR/EN details, withheld values, resource versions and exact authorized revision link', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('?') ? page : record)))
  const navigate = vi.fn(); const user = userEvent.setup()
  const view = render(<AuditWorkspace language="tr" selected={id} allowed onNavigate={navigate} />)
  expect(await screen.findByRole('heading', { name: 'Kayıt ayrıntıları' })).toBeVisible()
  expect(screen.getByText('21 → 22')).toBeVisible()
  expect(screen.getByRole('link', { name: 'Değişmez revizyonu aç' })).toHaveAttribute('href', record.revisionHref)
  view.rerender(<AuditWorkspace language="en" selected={id} allowed onNavigate={navigate} />)
  expect(screen.getByText(/does not require a decision reason/)).toBeVisible()
  const close = screen.getByRole('button', { name: 'Close', exact: true }); close.focus(); await user.keyboard('{Enter}')
  expect(navigate).toHaveBeenCalledWith('/app/audit')
})
it('does not fetch or expose records without trusted capability', () => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  render(<AuditWorkspace language="en" selected={id} allowed={false} onNavigate={vi.fn()} />)
  expect(screen.getByRole('alert')).toHaveTextContent('Audit permission is required.')
  expect(fetch).not.toHaveBeenCalled()
})
it.each([401, 403, 404, 503])('clears results on failure %s and retries without stale detail', async status => {
  const fetch = vi.fn().mockResolvedValueOnce(response({}, status)).mockResolvedValue(response(page)); vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup()
  render(<AuditWorkspace language="en" selected={null} allowed onNavigate={vi.fn()} />)
  expect(await screen.findByRole('alert')).toBeVisible()
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Retry' }))
  expect(await screen.findByRole('table')).toBeVisible()
})
it('distinguishes empty authorized history and missing historical values', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('?') ? { ...page, items: [], total: 0 } : { ...record, actorReference: null, roleContext: null, capabilityContext: null, changeSummary: null, beforeVersion: null, afterVersion: null, beforeRevision: null, afterRevision: null, revisionHref: null, reasonStatus: null, metadataStatus: 'HistoricalMetadataUnavailable' })))
  render(<AuditWorkspace language="en" selected={id} allowed onNavigate={vi.fn()} />)
  expect(await screen.findByText('No authorized records match these filters.')).toBeVisible()
  expect(screen.getByText('Historical values are unavailable.')).toBeVisible()
  expect(screen.getByText('Historical reason unavailable')).toBeVisible()
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
})
it('keeps filters and selected record in shareable URL context and clears cursor on filter changes', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response(page)))
  const navigate = vi.fn(); const user = userEvent.setup()
  render(<AuditWorkspace language="en" selected={null} query={`vesselId=${vessel}&cursor=old`} allowed onNavigate={navigate} />)
  await screen.findByRole('table')
  expect(screen.getByLabelText('Vessel ID')).toHaveValue(vessel)
  await user.selectOptions(screen.getByLabelText('Action'), 'Modified')
  await user.click(screen.getByRole('button', { name: 'Filter', exact: true }))
  expect(navigate).toHaveBeenCalledWith(`/app/audit?vesselId=${vessel}&action=Modified`)
  const path = `/app/audit/${id}?vesselId=${vessel}&action=Modified`
  expect(locationPath(readLocation(path))).toBe(path)
})
it('discards late response after permission revocation', async () => {
  let resolve!: (v: Response) => void
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(r => { resolve = r })))
  const view = render(<AuditWorkspace language="en" selected={null} allowed onNavigate={vi.fn()} />)
  view.rerender(<AuditWorkspace language="en" selected={null} allowed={false} onNavigate={vi.fn()} />)
  resolve(response(page))
  await waitFor(() => expect(screen.getByRole('alert')).toBeVisible())
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
})
it.each([
  { ...record, actorReference: 'personal@example.invalid' },
  { ...record, actorType: ['User'] },
  { ...record, action: ['Modified'] },
  { ...record, metadataStatus: ['Recorded'] },
  { ...record, revisionHref: 'https://outside.example/export' },
  { ...record, id: id + '\n' },
  { ...record, roleContext: null, capabilityContext: 'external-admin' },
  { ...record, payload: 'unexpected body' },
  { ...record, changeSummary: '[{"field":"AccessToken","before":null,"after":"secret","evidence":"Allowlisted"}]' },
  { ...record, changeSummary: '[{"field":"Name","before":null,"after":"unexpected text","evidence":"Allowlisted"}]' },
  { ...record, afterVersion: 4294967296 },
  { ...record, afterRevision: 1.5 },
  { ...record, occurredAt: '2026-02-30T00:00:00Z' },
])('rejects invalid or sensitive runtime audit DTO %j', value => {
  expect(() => decodeAuditRecord(value)).toThrow()
})
it('rejects count/page inconsistency, duplicate rows and unsafe cursors', () => {
  for (const value of [{ ...page, total: 0 }, { ...page, items: [record, record] }, { ...page, limit: 100 }, { ...page, nextCursor: 'arbitrary' }])
    expect(() => decodeAuditPage(value, 25)).toThrow()
})
it.each(['\n', '\r', '\u2028', '\u2029', '\r\n'])('rejects complete numeric-string suffix %j in audit change evidence', suffix => {
  for (const field of ['GrossTonnage', 'BollardPullTonnes', 'Revision']) {
    expect(() => changes(JSON.stringify([{ field, before: null, after: '12' + suffix, evidence: 'Allowlisted' }]))).toThrow()
    expect(changes(JSON.stringify([{ field, before: null, after: '12', evidence: 'Allowlisted' }]))).toHaveLength(1)
  }
})
it('rejects arrays, objects and coercible values at every wire enum and role boundary', () => {
  for (const [field, value] of [['actorType', 'User'], ['action', 'Modified'], ['metadataStatus', 'Recorded']]) {
    for (const invalid of [[value], { toString: () => value }, { value }, null, 1]) expect(() => decodeAuditRecord({ ...record, [field]: invalid })).toThrow()
  }
  for (const evidence of [['Allowlisted'], { value: 'Allowlisted' }, 1])
    expect(() => changes(JSON.stringify([{ field: 'Revision', before: null, after: '1', evidence }]))).toThrow()
  for (const roleContext of [['operator'], 'directory.write', vessel + '\n', `${vessel},${vessel}`])
    expect(() => decodeAuditRecord({ ...record, roleContext })).toThrow()
  expect(decodeAuditRecord({ ...record, roleContext: vessel }).roleContext).toBe(vessel)
})
