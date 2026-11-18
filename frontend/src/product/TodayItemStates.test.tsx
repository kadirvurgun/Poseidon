import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { TodayWorkspace } from './TodayWorkspace'
import { decodeToday } from './today-api'

// DEV-023 / T-S2-004 (61.03 paragraph 401-428, §33). Mocked transport: UI behavior only.
const vesselId = '40000000-0000-0000-0000-000000000001'
const planId = '40000000-0000-0000-0000-000000000002'
const jobId = '40000000-0000-0000-0000-000000000003'
const now = '2026-11-18T08:00:00Z'
const saved = '2026-11-17T21:15:00Z'
const vessel = { id: vesselId, companyId: planId, code: 'SYN-23', name: 'FICTIONAL Today tug', kind: 'Tug', version: 1, canEdit: false }
const sources = ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
const coverage = (available: number[], missing: number[] = []) => sources.map((source, i) => ({ source, status: available.includes(i) ? 'Available' : missing.includes(i) ? 'Missing' : 'Unavailable' }))
const maintenance = { key: `maintenance-plan:${planId}`, priority: 'Attention', source: 'MaintenancePlan', reason: 'MaintenanceDue', owner: null,
  nextAction: 'ViewMaintenancePlan', href: `/app/maintenance-plan/${vesselId}?id=${planId}`, sourceRef: { owner: 'PMS', kind: 'MaintenancePlan', id: planId, revision: 4, savedAt: saved } }
const tug = { key: `tug-job:${jobId}`, priority: 'Review', source: 'TugJob', reason: 'TugPlanMissing', owner: null,
  nextAction: 'ViewTugJob', href: `/app/tug-job-detail/${jobId}?vessel=${vesselId}`, sourceRef: { owner: 'TUG', kind: 'TugJob', id: jobId, revision: 2, savedAt: saved } }
const draft = { key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftAbsent', owner: null,
  nextAction: 'ViewDraft', href: `/app/fleet/${vesselId}/profile`, sourceRef: { owner: 'REGULATORY', kind: 'RegulatoryDraft', id: vesselId, revision: null, savedAt: null } }
const page = { vessel, readAt: now, coverage: coverage([5, 6], [0]), cards: { items: [maintenance, tug, draft], offset: 0, limit: 25, hasMore: false }, viewState: 'Partial' }
const response = (body: unknown, status = 200) => ({ ok: status === 200, status, json: async () => body }) as Response
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('DEV023-AC-01/02/04 shows priority, owning module, record, revision and save time on every item', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response(page)))
  render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  const pms = await screen.findByRole('article', { name: 'Planning attention · Maintenance plan' })
  const pmsSource = pms.querySelector<HTMLElement>('.today-card-source')!
  expect(pmsSource).toHaveTextContent('Source owner')
  expect(pmsSource).toHaveTextContent('Planned maintenance')
  expect(pmsSource).toHaveTextContent(`Maintenance plan · ${planId}`)
  expect(within(pmsSource).getByText('Source revision', { selector: 'dt' }).nextElementSibling).toHaveTextContent('4')
  expect(within(pmsSource).getByText(saved)).toHaveAttribute('dateTime', saved)
  const tugSource = screen.getByRole('article', { name: `Tug job ${jobId}` }).querySelector<HTMLElement>('.today-card-source')!
  expect(tugSource).toHaveTextContent('Tug operations'); expect(tugSource).toHaveTextContent('2')
  // An absent draft names no invented revision or time.
  const draftSource = screen.getByRole('article', { name: 'Advisory · Regulatory draft' }).querySelector<HTMLElement>('.today-card-source')!
  expect(draftSource).toHaveTextContent('Regulatory')
  expect(within(draftSource).getAllByText('No stored revision')).toHaveLength(2)
  expect(screen.getByRole('heading', { name: 'Planning attention · Maintenance plan' })).toBeVisible()
})

it('DEV023-AC-03 labels only the authorized next action and navigates to the owning record', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response(page)))
  const navigate = vi.fn(); const user = userEvent.setup()
  render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={navigate} />)
  await user.click(await screen.findByRole('button', { name: 'View maintenance plan' }))
  expect(navigate).toHaveBeenCalledWith(maintenance.href)
  expect(screen.getByRole('button', { name: 'View tug job' })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Review maintenance plan' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Review tug job' })).not.toBeInTheDocument()
})

it('DEV023-AC-05 a partial composition is never all clear and zero counters say so', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response({ ...page, cards: { ...page.cards, items: [draft] } })))
  render(<TodayWorkspace id={vesselId} language="tr" offline={false} onNavigate={vi.fn()} />)
  const notice = await screen.findByText(/Kısmi görünüm/)
  expect(notice).toHaveAttribute('data-view-state', 'Partial')
  expect(notice.querySelector('svg')).not.toBeNull() // text + icon, not color only
  expect(screen.getAllByText('Kısmi kaynaklar: sıfır, her şeyin uygun olduğu anlamına gelmez')).toHaveLength(2)
})

it('DEV023-AC-05 shows an explicit unavailable state when no owning source is readable', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response({ ...page, coverage: coverage([]), cards: { ...page.cards, items: [] }, viewState: 'Unavailable' })))
  render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  expect(await screen.findByText(/No owning source could be read/)).toHaveAttribute('data-view-state', 'Unavailable')
  expect(screen.getByText(/No returned items/)).toHaveTextContent('does not mean all clear')
  expect(screen.queryByRole('button', { name: 'Why?' })).not.toBeInTheDocument()
})

it('DEV023-AC-06 rejects a server-promoted green state and a card without an exact source', () => {
  expect(decodeToday(page, vesselId).viewState).toBe('Partial')
  for (const viewState of ['Ready', 'Empty', 'Unavailable', 'READY', undefined])
    expect(() => decodeToday({ ...page, viewState }, vesselId)).toThrow()
  const withoutRef = { ...maintenance } as Partial<typeof maintenance>; delete withoutRef.sourceRef
  for (const item of [withoutRef, { ...maintenance, sourceRef: { ...maintenance.sourceRef, id: jobId } }, { ...maintenance, sourceRef: { ...maintenance.sourceRef, owner: 'TUG' } },
    { ...maintenance, sourceRef: { ...maintenance.sourceRef, revision: 0 } }, { ...maintenance, sourceRef: { ...maintenance.sourceRef, savedAt: null } },
    { ...draft, sourceRef: { ...draft.sourceRef, revision: 1 } }, { ...maintenance, nextAction: 'ApproveMaintenancePlan' }, { ...tug, nextAction: 'ReleaseTug' }])
    expect(() => decodeToday({ ...page, cards: { ...page.cards, items: [item] } }, vesselId)).toThrow()
  // A complete (hypothetical) composition still distinguishes Empty from Ready deterministically.
  const all = coverage([0, 1, 2, 3, 4, 5, 6])
  expect(() => decodeToday({ ...page, coverage: all, cards: { ...page.cards, items: [] }, viewState: 'Empty' }, vesselId)).toThrow() // placeholders stay Unavailable
})

it.each([
  [429, 'Too many requests. Wait, then retry.'],
  [503, 'Source unavailable. Retry when connected.'],
  [500, 'Source unavailable. Retry when connected.']
])('DEV023-AC-07 maps HTTP %s deterministically and retries without stale data', async (status, message) => {
  const fetch = vi.fn().mockResolvedValueOnce(response({}, status)).mockResolvedValue(response(page)); vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  const alert = await screen.findByRole('alert')
  expect(alert).toHaveTextContent(message); expect(alert).toHaveFocus(); expect(alert).toHaveAttribute('data-error-status', String(status))
  expect(screen.queryByText(/FICTIONAL Today tug/)).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Retry' }))
  expect(await screen.findByText(/Partial view/)).toBeVisible()
})

it('DEV023-AC-07 an invalid contract and a network failure show distinct fixed messages', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response({ ...page, viewState: 'Ready' })))
  const view = render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  expect(await screen.findByRole('alert')).toHaveTextContent('The server response failed validation; nothing from it is shown.')
  expect(screen.queryByText(/Planned maintenance/)).not.toBeInTheDocument()
  view.unmount()
  vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('synthetic network failure') }))
  render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  expect(await screen.findByRole('alert')).toHaveTextContent('Source unavailable. Retry when connected.')
})

it('DEV023-AC-08 loading and offline states are exposed deterministically without a source request offline', async () => {
  let resolve!: (r: Response) => void
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(r => { resolve = r })))
  const view = render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  const region = screen.getByRole('region', { name: 'Today' })
  expect(region).toHaveAttribute('aria-busy', 'true'); expect(region).toHaveAttribute('data-today-state', 'loading')
  expect(screen.getByRole('status')).toHaveTextContent('Loading…')
  resolve(response(page))
  expect(await screen.findByText(/Partial view/)).toBeVisible()
  expect(region).toHaveAttribute('aria-busy', 'false'); expect(region).toHaveAttribute('data-today-state', 'ok')
  view.rerender(<TodayWorkspace id={vesselId} language="en" offline onNavigate={vi.fn()} />)
  expect(region).toHaveAttribute('data-today-state', 'offline')
  expect(screen.getByText('Offline. Displayed data is stale and unverified.')).toBeVisible()
  view.unmount()
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  render(<TodayWorkspace id={vesselId} language="en" offline onNavigate={vi.fn()} />)
  expect(screen.getByRole('region', { name: 'Today' })).toHaveAttribute('data-today-state', 'offline')
  expect(fetch).not.toHaveBeenCalled()
})
