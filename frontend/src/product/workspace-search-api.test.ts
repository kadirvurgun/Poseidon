import { afterEach, expect, it, vi } from 'vitest'
import { decodeSearchPage, searchWorkspace } from './workspace-search-api'

const id = '10000000-0000-0000-0000-000000000001'
const item = { key: id, href: `/app/fleet/${id}`, label: 'T1 · Synthetic Tug', kind: 'Tug' }
const page = (changes: Record<string, unknown> = {}) => ({ items: [item], offset: 0, limit: 25, hasMore: false, ...changes })
afterEach(() => vi.unstubAllGlobals())

it.each([
  ['today', '/app/today'], ['companies', '/app/companies'], ['fleet', '/app/fleet'],
  ['sources', '/app/sources'], ['requirements', '/app/requirements'], ['audit', '/app/audit'],
  ['tug-command', '/app/tug-command'], ['tug-job-board', '/app/tug-job-board'], ['tug-job-intake', '/app/tug-job-intake'],
])('accepts the actual server destination %s without granting permission', (key, href) => {
  const result = { key, href, label: key, kind: 'Destination' }
  expect(decodeSearchPage(page({ items: [result] }), 0).items).toEqual([result])
})

it.each([
  { offset: 25 }, { offset: '0' }, { limit: 24 }, { limit: '25' }, { hasMore: 'false' },
  { items: [], hasMore: true }, { items: [item], hasMore: true }, { totalCount: 10 },
])('rejects contradictory, unbounded or unexpected page metadata %#', changes => {
  expect(() => decodeSearchPage(page(changes), 0)).toThrow()
})

it.each([
  { key: 'fleet', href: '/app/companies', label: 'fleet', kind: 'Destination' },
  { key: 'constructor', href: '/app/companies', label: 'constructor', kind: 'Destination' },
  { ...item, href: `/app/fleet/${id}?tenant=elsewhere` },
  { ...item, href: `/app/fleet/${id}\n` },
  { ...item, key: '00000000-0000-0000-0000-000000000000' },
  { ...item, key: id.replace('1', '2') },
  { ...item, label: '' }, { ...item, label: '   ' }, { ...item, label: 'private\nvalue' },
  { ...item, label: 'x'.repeat(268) }, { ...item, kind: 'Ready' }, { ...item, sourceSecret: 'unexpected' },
])('rejects inconsistent destinations, context and display contracts %#', result => {
  expect(() => decodeSearchPage(page({ items: [result] }), 0)).toThrow()
})

it('rejects duplicate vessel identities even when kind and GUID casing differ', () => {
  const vessel = { ...item, key: 'aaaaaaaa-0000-0000-0000-000000000001', href: '/app/fleet/aaaaaaaa-0000-0000-0000-000000000001' }
  expect(() => decodeSearchPage(page({ items: [vessel, { ...vessel, key: vessel.key.toUpperCase(), href: vessel.href.toUpperCase().replace('/APP/FLEET/', '/app/fleet/'), kind: 'Ship' }] }), 0)).toThrow()
})

it('retains complete continuation pages and bounded terminal pages', () => {
  const items = Array.from({ length: 25 }, (_, i) => ({ ...item, key: `10000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, href: `/app/fleet/10000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}` }))
  expect(decodeSearchPage(page({ items, offset: 25, hasMore: true }), 25).items).toHaveLength(25)
  expect(decodeSearchPage(page({ items: [], offset: 100000 }), 100000).items).toEqual([])
})

it('encodes the actual query and checks the returned page against the requested offset', async () => {
  const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => page({ offset: 25 }) })
  vi.stubGlobal('fetch', fetch)
  const signal = new AbortController().signal
  await expect(searchWorkspace('Tug & Ship', 25, signal)).resolves.toMatchObject({ offset: 25 })
  expect(fetch).toHaveBeenCalledWith('/api/v1/workspace/search?q=Tug%20%26%20Ship&offset=25&limit=25', expect.objectContaining({ signal }))
  await expect(searchWorkspace('Tug & Ship', 0, signal)).rejects.toMatchObject({ code: 'invalid_search_contract' })
})

it.each([-1, 0.5, 100001, NaN, Infinity])('rejects unsupported offset %s before transport', async offset => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  await expect(searchWorkspace('', offset, new AbortController().signal)).rejects.toThrow()
  expect(fetch).not.toHaveBeenCalled()
})
