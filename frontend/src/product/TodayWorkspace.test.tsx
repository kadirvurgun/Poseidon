import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { TodayWorkspace } from './TodayWorkspace'
import { decodeToday, decodeWhy, decodeTodayVessels, today, todayVessels, why as loadWhy } from './today-api'
import { ProductApp } from './ProductApp'

const id = '10000000-0000-0000-0000-000000000001'
const vessel = { id, companyId: '10000000-0000-0000-0000-000000000002', code: 'T1', name: 'Synthetic Tug', kind: 'Tug', version: 1, canEdit: true }
const vesselPage = (count: number) => Array.from({ length: count }, (_, i) => ({ ...vessel, id: `10000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, code: `T${i + 1}` }))
const now = '2026-11-01T00:00:00Z'
const href = `/app/fleet/${id}/profile`
const data = { vessel, readAt: now, coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification'].map((source, i) => ({ source, status: i ? 'Unavailable' : 'Available' })), cards: { items: [{ key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftUnverified', owner: null, nextAction: 'ReviewDraft', href }], offset: 0, limit: 25, hasMore: false } }
const why = { vesselId: id, cardKey: 'regulatory-draft', rule: 'TW-DRAFT-01', reason: 'DraftUnverified', inputs: { known: 0, missing: 16, unknown: 1, reason: 'DraftUnverified' }, readAt: now, savedAt: now, sourceObservedAt: null, freshness: 'Unverified', provenance: 'Synthetic', blockers: ['VerificationUnavailable', 'OperationalSourcesUnavailable'], owner: null, nextAction: 'ReviewDraft', href }
const response = (body: unknown, status = 200) => ({ ok: status === 200, status, json: async () => body }) as Response
afterEach(() => vi.restoreAllMocks())
it('shows honest TR/EN coverage, keyboard disclosure and fresh authorized Why on every expansion', async () => {
  const fetch = vi.fn(async (url: string) => response(url.includes('why-engine') ? why : data)); vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); const navigate = vi.fn()
  const view = render(<TodayWorkspace id={id} language="tr" offline={false} onNavigate={navigate} />)
  expect(await screen.findByText('Kayıtlı mevzuat taslağı doğrulanmamış.')).toBeVisible()
  expect(screen.getByText(/Etkin araç/)).toHaveTextContent('Römorkör')
  view.rerender(<TodayWorkspace id={id} language="en" offline={false} onNavigate={navigate} />)
  expect(screen.getAllByText(/Unavailable: source not connected/)).toHaveLength(4)
  const button = screen.getByRole('button', { name: 'Why?' }); button.focus(); await user.keyboard('{Enter}')
  expect(await screen.findByText(/Declared \/ missing \/ unknown inputs: 0 \/ 16 \/ 1/)).toBeVisible()
  expect(screen.getByText(/Input source observation: Unknown/)).toBeVisible()
  expect(button).toHaveFocus()
  await user.click(screen.getByRole('button', { name: 'Close Why' })); await user.click(screen.getByRole('button', { name: 'Why?' }))
  await waitFor(() => expect(fetch.mock.calls.filter(([u]) => u.includes('why-engine'))).toHaveLength(2))
  await user.click(screen.getByRole('button', { name: 'Review draft' })); expect(navigate).toHaveBeenCalledWith(href)
  view.rerender(<TodayWorkspace id={id} language="en" offline onNavigate={navigate} />)
  expect(screen.getByText(/Offline. Displayed data is stale/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Refresh' })).toBeDisabled()
})
it.each([401, 403, 404, 503])('handles API failure %s without source details and can retry', async code => {
  const fetch = vi.fn().mockResolvedValueOnce(response({}, code)).mockResolvedValue(response(data)); vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); render(<TodayWorkspace id={id} language="en" offline={false} onNavigate={vi.fn()} />)
  expect(await screen.findByRole('alert')).toBeVisible(); expect(screen.queryByText(/Synthetic Tug/)).not.toBeInTheDocument()
  if (code === 401) expect(screen.getByRole('link', { name: 'Sign in to continue' })).toBeVisible()
  else { await user.click(screen.getByRole('button', { name: 'Retry' })); expect(await screen.findByText(/Current vessel/)).toBeVisible() }
})
it('distinguishes an empty returned page from unavailable coverage', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ ...data, cards: { ...data.cards, items: [] } })))
  render(<TodayWorkspace id={id} language="en" offline={false} onNavigate={vi.fn()} />)
  expect(await screen.findByText(/No returned items/)).toHaveTextContent('does not mean all clear')
  expect(screen.getByText(/Permits: Unavailable/)).toBeVisible()
})
it('removes the earlier snapshot when Why observes revoked access', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('why-engine') ? {} : data, url.includes('why-engine') ? 403 : 200)))
  const user = userEvent.setup(); render(<TodayWorkspace id={id} language="en" offline={false} onNavigate={vi.fn()} />)
  await user.click(await screen.findByRole('button', { name: 'Why?' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Access denied.')
  expect(screen.getByRole('alert')).toHaveFocus()
  expect(screen.queryByText(/Current vessel/)).not.toBeInTheDocument()
  expect(screen.queryByText(/Permits: Unavailable/)).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Review draft' })).not.toBeInTheDocument()
})
it('retries a failed Why source and refreshes without retaining the earlier explanation', async () => {
  let whyReads = 0; let reads = 0
  const fetch = vi.fn(async (url: string) => {
    if (url.includes('why-engine')) return ++whyReads === 1 ? response({}, 503) : response(why)
    reads++; return response(data)
  })
  vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); render(<TodayWorkspace id={id} language="en" offline={false} onNavigate={vi.fn()} />)
  await user.click(await screen.findByRole('button', { name: 'Why?' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Source unavailable')
  await user.click(screen.getByRole('button', { name: 'Retry' }))
  expect(await screen.findByText(/Declared \/ missing/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Close Why' })).toHaveFocus()
  await user.click(screen.getByRole('button', { name: 'Refresh' }))
  expect(screen.queryByText(/Declared \/ missing/)).not.toBeInTheDocument()
  expect(await screen.findByRole('button', { name: 'Why?' })).toHaveAttribute('aria-expanded', 'false')
  expect(reads).toBe(2); expect(whyReads).toBe(2)
})
it('hides old Why data offline and rechecks Today on reconnect', async () => {
  const fetch = vi.fn(async (url: string) => response(url.includes('why-engine') ? why : data)); vi.stubGlobal('fetch', fetch)
  const props = { id, language: 'en' as const, onNavigate: vi.fn() }
  const user = userEvent.setup(); const view = render(<TodayWorkspace {...props} offline={false} />)
  await user.click(await screen.findByRole('button', { name: 'Why?' }))
  await screen.findByText(/Declared \/ missing/)
  view.rerender(<TodayWorkspace {...props} offline />)
  expect(screen.queryByText(/Declared \/ missing/)).not.toBeInTheDocument()
  expect(screen.getByText(/Offline. Displayed data is stale/)).toBeVisible()
  view.rerender(<TodayWorkspace {...props} offline={false} />)
  expect(await screen.findByRole('button', { name: 'Why?' })).toHaveAttribute('aria-expanded', 'false')
  expect(fetch.mock.calls.filter(([u]) => u.includes('poseidon-today'))).toHaveLength(2)
})
it('starts offline without issuing a source request or claiming freshness', () => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  render(<TodayWorkspace id={id} language="en" offline onNavigate={vi.fn()} />)
  expect(screen.getByText(/Offline. No server snapshot is available/)).toBeVisible()
  expect(fetch).not.toHaveBeenCalled()
  expect(screen.getByRole('button', { name: 'Refresh' })).toBeDisabled()
})
it('aborts the previous vessel request and ignores a late response when the context changes', async () => {
  let resolveOld!: (value: Response) => void
  let oldSignal: AbortSignal | undefined
  const otherId = '10000000-0000-0000-0000-000000000003'
  const fetch = vi.fn((url: string, init: RequestInit) => {
    if (url.includes(id)) { oldSignal = init.signal as AbortSignal; return new Promise<Response>(resolve => { resolveOld = resolve }) }
    return Promise.resolve(response({ ...data, vessel: { ...vessel, id: otherId, name: 'Other synthetic vessel' }, cards: { ...data.cards, items: [{ ...data.cards.items[0], href: `/app/fleet/${otherId}/profile` }] } }))
  })
  vi.stubGlobal('fetch', fetch)
  const props = { language: 'en' as const, offline: false, onNavigate: vi.fn() }
  const view = render(<TodayWorkspace {...props} id={id} />)
  expect(screen.getByText('Loading…')).toBeVisible()
  view.rerender(<TodayWorkspace {...props} id={otherId} />)
  expect(oldSignal?.aborted).toBe(true)
  expect(await screen.findByText(/Other synthetic vessel/)).toBeVisible()
  await act(async () => resolveOld(response(data)))
  expect(screen.queryByText(/Synthetic Tug/)).not.toBeInTheDocument()
})
it('pages the authorized vessel selector and navigates only to a selected Today context', async () => {
  const fetch = vi.fn(async (url: string) => response({ items: url.includes('offset=25') ? [] : vesselPage(25), offset: url.includes('offset=25') ? 25 : 0, limit: 25, hasMore: !url.includes('offset=25') }))
  vi.stubGlobal('fetch', fetch)
  const navigate = vi.fn(); const user = userEvent.setup()
  render(<TodayWorkspace id={null} language="en" offline={false} onNavigate={navigate} />)
  await user.click(await screen.findByRole('button', { name: /T1 · Synthetic Tug/ }))
  expect(navigate).toHaveBeenCalledWith(`/app/today/${id}`)
  await user.click(screen.getByRole('button', { name: 'Next vessels' }))
  expect(await screen.findByText(/No returned items/)).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Previous vessels' }))
  expect(await screen.findByRole('button', { name: /T1 · Synthetic Tug/ })).toBeVisible()
})
it.each(['2026-02-30T00:00:00Z', '2026-11-01', '2026-11-01T00:00:00+15:00', '2026-11-01T25:00:00Z'])('rejects ambiguous or invalid source timestamps %s', timestamp => {
  expect(() => decodeToday({ ...data, readAt: timestamp }, id)).toThrow()
  expect(() => decodeWhy({ ...why, sourceObservedAt: timestamp }, id)).toThrow()
})
it.each([{ offset: 1 }, { offset: 25 }, { offset: -1 }, { offset: 100001 }, { offset: 0.5 }, { offset: '0' }, { limit: 1 }, { limit: 24 }, { limit: 26 }, { limit: '25' }])('rejects mismatched Today pagination %# even on empty pages', async changes => {
  for (const items of [[], data.cards.items]) {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ ...data, cards: { ...data.cards, items, ...changes } })))
    await expect(today(id, new AbortController().signal)).rejects.toMatchObject({ status: 502, code: 'invalid_today_contract' })
  }
})
it('requests the exact first Today page and preserves valid empty and populated results', async () => {
  const fetch = vi.fn().mockResolvedValueOnce(response(data)).mockResolvedValueOnce(response({ ...data, cards: { ...data.cards, items: [] } }))
  vi.stubGlobal('fetch', fetch)
  const signal = new AbortController().signal
  expect(await today(id, signal)).toEqual(data)
  expect((await today(id, signal)).cards.items).toEqual([])
  const url = new URL(fetch.mock.calls[0][0], 'https://example.invalid')
  expect(url.searchParams.get('offset')).toBe('0')
  expect(url.searchParams.get('limit')).toBe('25')
})
it.each([0, 1, 24])('rejects vessel continuation with only %s items on first and later pages', async count => {
  for (const offset of [0, 25]) {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ items: vesselPage(count), offset, limit: 25, hasMore: true })))
    await expect(todayVessels(offset, new AbortController().signal)).rejects.toMatchObject({ status: 502, code: 'invalid_today_contract' })
  }
})
it.each([-1, 0.5, 100001, Number.MAX_SAFE_INTEGER + 1, NaN, Infinity])('rejects vessel offset outside supported bounds %s even if it matches the request', offset => {
  expect(() => decodeTodayVessels({ items: [], offset, limit: 25, hasMore: false }, offset)).toThrow()
})
it('preserves bounded vessel pages and requires full continuation pages', () => {
  for (const offset of [0, 25, 100000]) {
    for (const count of [0, 1, 24, 25]) {
      const page = { items: vesselPage(count), offset, limit: 25, hasMore: false }
      expect(decodeTodayVessels(page, offset)).toEqual(page)
    }
    const page = { items: vesselPage(25), offset, limit: 25, hasMore: true }
    expect(decodeTodayVessels(page, offset)).toEqual(page)
  }
})
it.each(['cards', 'vessels'])('renders malformed %s pagination as an error without a scoped empty success', async kind => {
  const body = kind === 'cards' ? { ...data, cards: { ...data.cards, items: [], offset: 1 } } : { items: [], offset: 0, limit: 25, hasMore: true }
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response(body)))
  render(<TodayWorkspace id={kind === 'cards' ? id : null} language="en" offline={false} onNavigate={vi.fn()} />)
  expect(await screen.findByRole('alert')).toBeVisible()
  expect(screen.queryByText(/No returned items/)).not.toBeInTheDocument()
  expect(screen.queryByText(/Current vessel/)).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Next vessels' })).not.toBeInTheDocument()
})
it('rejects unknown members, contradictory coverage, fabricated pagination and absent-draft provenance', () => {
  expect(() => decodeToday({ ...data, sourcePath: 'unsupported' }, id)).toThrow()
  expect(() => decodeToday({ ...data, cards: { ...data.cards, hasMore: true } }, id)).toThrow()
  expect(() => decodeToday({ ...data, coverage: data.coverage.map((c, i) => i ? c : { ...c, status: 'Missing' }) }, id)).toThrow()
  expect(() => decodeWhy({ ...why, reason: 'DraftAbsent', inputs: { known: 0, missing: 0, unknown: 0, reason: 'DraftAbsent' }, blockers: ['DraftAbsent', 'OperationalSourcesUnavailable'] }, id)).toThrow()
  expect(() => decodeToday({ ...data, vessel: { ...vessel, canEdit: false } }, id)).toThrow()
  expect(decodeToday({ ...data, vessel: { ...vessel, code: 'S'.repeat(64) } }, id).vessel.code).toHaveLength(64)
  const page = { items: [vessel], offset: 0, limit: 25, hasMore: false }
  expect(decodeTodayVessels(page, 0).items).toHaveLength(1)
  expect(() => decodeTodayVessels({ ...page, items: [vessel, vessel] }, 0)).toThrow()
  expect(() => decodeTodayVessels({ ...page, items: [{ ...vessel, id: '../other' }] }, 0)).toThrow()
  expect(() => decodeTodayVessels(page, 25)).toThrow()
})
it('rejects foreign context, invented safe state, unsafe destinations and malformed Why data', () => {
  expect(() => decodeToday({ ...data, vessel: { ...vessel, id: vessel.companyId } }, id)).toThrow()
  expect(() => decodeToday({ ...data, cards: { ...data.cards, items: [{ ...data.cards.items[0], reason: 'Ready' }] } }, id)).toThrow()
  expect(() => decodeWhy({ ...why, href: '/app/companies' }, id)).toThrow()
  expect(() => decodeWhy({ ...why, freshness: 'Fresh' }, id)).toThrow()
  expect(() => decodeWhy({ ...why, inputs: { ...why.inputs, missing: -1 } }, id)).toThrow()
})
it('guards navigation from an unsaved directory draft into Today', async () => {
  window.history.replaceState(null, '', `/app/fleet/${id}`)
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, csrfToken: 'synthetic' } : url.includes('vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel)))
  const user = userEvent.setup(); render(<ProductApp />)
  await user.click(await screen.findByRole('button', { name: /^Düzenle$/ }))
  await user.type(screen.getByLabelText('Ad *'), ' retained')
  await user.click(screen.getByRole('button', { name: /^Bugün$/ }))
  expect(await screen.findByRole('dialog')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Düzenlemeye devam et' }))
  expect(screen.getByLabelText('Ad *')).toHaveValue('Synthetic Tug retained')
  expect(window.location.pathname).toBe(`/app/fleet/${id}`)
})

// TW-03 / REG-BOUNDARY-03: remote identifiers and timestamps must consume the full string.
const lineTerminators = [{ name: 'LF', suffix: '\n' }, { name: 'CR', suffix: '\r' }, { name: 'LS', suffix: '\u2028' }, { name: 'PS', suffix: '\u2029' }]
it.each(lineTerminators.flatMap(t => (['id', 'companyId'] as const).map(field => ({ ...t, field }))))('rejects trailing $name in vessel $field through the real transport decoder', async ({ suffix, field }) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ items: [{ ...vessel, [field]: vessel[field] + suffix }], offset: 0, limit: 25, hasMore: false })))
  await expect(todayVessels(0, new AbortController().signal)).rejects.toMatchObject({ code: 'invalid_today_contract' })
})
it.each(lineTerminators)('rejects trailing $name in Today readAt rather than normalizing it', async ({ suffix }) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ ...data, readAt: now + suffix })))
  await expect(today(id, new AbortController().signal)).rejects.toMatchObject({ code: 'invalid_today_contract' })
})
it.each(lineTerminators.flatMap(t => (['readAt', 'savedAt', 'sourceObservedAt'] as const).map(field => ({ ...t, field }))))('rejects trailing $name in Why $field through the real transport decoder', async ({ suffix, field }) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ ...why, [field]: now + suffix })))
  await expect(loadWhy(id, new AbortController().signal)).rejects.toMatchObject({ code: 'invalid_today_contract' })
})
it('retains complete mixed-case nonempty GUIDs on a legitimate terminal vessel page', async () => {
  const upper = { ...vessel, id: 'ABCDEF12-3456-789A-BCDE-F0123456789A', companyId: 'ABCDEF12-3456-789A-BCDE-F0123456789B' }
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ items: [upper], offset: 0, limit: 25, hasMore: false })))
  expect((await todayVessels(0, new AbortController().signal)).items).toEqual([upper])
})
