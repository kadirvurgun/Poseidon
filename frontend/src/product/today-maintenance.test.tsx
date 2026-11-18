import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { TodayWorkspace } from './TodayWorkspace'
import { decodeToday, decodeWhy } from './today-api'

const vesselId = '10000000-0000-0000-0000-000000000001'
const assetId = '30000000-0000-0000-0000-000000000001'
const planId = '20000000-0000-0000-0000-000000000001'
const now = '2026-11-02T00:00:00Z'
const vessel = { id: vesselId, companyId: assetId, code: 'SYNTHETIC', name: 'FICTIONAL tug', kind: 'Tug', version: 1, canEdit: true }
const card = { key: `maintenance-plan:${planId}`, priority: 'Review', source: 'MaintenancePlan', reason: 'MaintenanceReviewRequired',
  owner: null, nextAction: 'ReviewMaintenancePlan', href: `/app/maintenance-plan/${vesselId}?id=${planId}` }
const draft = { key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftUnverified', owner: null,
  nextAction: 'ReviewDraft', href: `/app/fleet/${vesselId}/profile` }
const page = { vessel, readAt: now, coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
  .map((source, i) => ({ source, status: i === 0 || i === 5 ? 'Available' : 'Unavailable' })),
cards: { items: [card, draft], offset: 0, limit: 25, hasMore: false } }
const why = { vesselId, cardKey: card.key, rule: 'TW-MAINTENANCE-01', reason: card.reason, inputs: null, readAt: now, savedAt: now,
  sourceObservedAt: null, freshness: 'Unverified', provenance: 'UserDeclaredUnverified', blockers: ['PlanningDraftNotVerified'],
  owner: null, nextAction: card.nextAction, href: card.href,
  maintenance: { planId, planRevision: 2, assetId, assetRevision: 1, currentAssetRevision: 1, jobAvailable: true, ownerEligible: false,
    calculation: { state: 'ReviewRequired', reason: 'MaintenanceOwnerUnavailable', nextDueDate: '2026-11-01', nextDueMeter: null, algorithm: 'planning-current-v1' }, observationState: 'NotRequested' } }
const draftWhy = { vesselId, cardKey: draft.key, rule: 'TW-DRAFT-01', reason: draft.reason,
  inputs: { known: 0, missing: 20, unknown: 0, reason: draft.reason }, readAt: now, savedAt: now,
  sourceObservedAt: null, freshness: 'Unverified', provenance: 'Synthetic', blockers: ['VerificationUnavailable', 'OperationalSourcesUnavailable'],
  owner: null, nextAction: draft.nextAction, href: draft.href }
const response = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

it('DEV-024 binds optional maintenance source lineage to the selected plan and exact revision', () => {
  const source = { kind: 'MaintenancePlan', id: planId, revision: 2 }
  expect(decodeWhy({ ...why, source }, vesselId, card.key).source).toEqual(source)
  for (const invalid of [{ ...source, id: assetId }, { ...source, revision: 1 }, { ...source, kind: 'TugJob' }, { ...source, revision: null }])
    expect(() => decodeWhy({ ...why, source: invalid }, vesselId, card.key)).toThrow()
})
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('binds every Why to its selected card, explains current lineage and navigates to the supported selected plan', async () => {
  const fetcher = vi.fn(async (url: string) => response(url.includes('why-engine') ? new URL(url, 'https://example.invalid').searchParams.get('cardKey') === card.key ? why : draftWhy : page))
  vi.stubGlobal('fetch', fetcher); const navigate = vi.fn(); const user = userEvent.setup()
  render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={navigate} />)
  const maintenance = (await screen.findByRole('heading', { name: 'Review required · Maintenance plan' })).closest('article')!
  await user.click(within(maintenance).getByRole('button', { name: 'Why?' }))
  expect(await screen.findByText('Plan / equipment revision: 2 / 1')).toBeVisible()
  expect(screen.getByText('Current source owner role: Unknown')).toBeVisible()
  expect(screen.queryByText(/Declared \/ missing/)).not.toBeInTheDocument()
  await user.click(within(maintenance).getByRole('button', { name: 'Review maintenance plan' }))
  expect(navigate).toHaveBeenCalledWith(card.href)
  const regulatory = screen.getByRole('heading', { name: 'Advisory · Regulatory draft' }).closest('article')!
  await user.click(within(regulatory).getByRole('button', { name: 'Why?' }))
  expect(await screen.findByText('Declared / missing / unknown inputs: 0 / 20 / 0')).toBeVisible()
  expect(screen.queryByText(/Plan \/ equipment revision/)).not.toBeInTheDocument()
  expect(within(maintenance).getByRole('button', { name: 'Why?' })).toHaveAttribute('aria-expanded', 'false')
  await user.click(within(maintenance).getByRole('button', { name: 'Why?' }))
  await screen.findByText('Plan / equipment revision: 2 / 1')
  expect(fetcher.mock.calls.filter(([url]) => url.includes('why-engine')).map(([url]) => new URL(url, 'https://example.invalid').searchParams.get('cardKey')))
    .toEqual([card.key, draft.key, card.key])
})

it('ignores an aborted late explanation from a different card in the same vessel', async () => {
  let finish!: (r: Response) => void; let signal: AbortSignal | undefined
  vi.stubGlobal('fetch', vi.fn((url: string, init?: RequestInit) => {
    if (url.includes('why-engine') && new URL(url, 'https://example.invalid').searchParams.get('cardKey') === card.key) {
      signal = init?.signal as AbortSignal
      return new Promise<Response>(resolve => { finish = resolve })
    }
    return Promise.resolve(response(url.includes('why-engine') ? draftWhy : page))
  }))
  const user = userEvent.setup(); render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  const buttons = await screen.findAllByRole('button', { name: 'Why?' }); await user.click(buttons[0])
  await waitFor(() => expect(finish).toBeTypeOf('function'))
  await user.click(buttons[1]); await screen.findByText('Declared / missing / unknown inputs: 0 / 20 / 0')
  expect(signal?.aborted).toBe(true)
  await act(async () => finish(response(why)))
  expect(screen.queryByText(/Plan \/ equipment revision/)).not.toBeInTheDocument()
  expect(screen.getByText('Declared / missing / unknown inputs: 0 / 20 / 0')).toBeVisible()
})

it('pages a complete mixed-source feed and clears the prior card explanation before reading the next page', async () => {
  const first = { ...page, cards: { items: Array.from({ length: 25 }, (_, i) => {
    const id = `20000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`
    return { ...card, key: `maintenance-plan:${id}`, href: `/app/maintenance-plan/${vesselId}?id=${id}` }
  }), offset: 0, limit: 25, hasMore: true } }
  const last = { ...page, cards: { items: [draft], offset: 25, limit: 25, hasMore: false } }
  const fetcher = vi.fn(async (url: string) => response(url.includes('why-engine') ? why : new URL(url, 'https://example.invalid').searchParams.get('offset') === '25' ? last : first))
  vi.stubGlobal('fetch', fetcher); const user = userEvent.setup()
  render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  await user.click((await screen.findAllByRole('button', { name: 'Why?' }))[0]); await screen.findByText('Plan / equipment revision: 2 / 1')
  await user.click(screen.getByRole('button', { name: 'Next alerts' }))
  await screen.findByText('Stored regulatory draft is unverified.')
  expect(screen.queryByText(/Plan \/ equipment revision/)).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Next alerts' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Previous alerts' })).toBeEnabled()
  await user.click(screen.getByRole('button', { name: 'Previous alerts' }))
  await screen.findAllByRole('button', { name: 'Why?' })
  expect(screen.queryByText(/Plan \/ equipment revision/)).not.toBeInTheDocument()
})

it.each([403, 404])('invalidates all earlier cards after maintenance Why denial %s', async status => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => url.includes('why-engine') ? response({}, status) : response(page)))
  const user = userEvent.setup(); render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  await user.click((await screen.findAllByRole('button', { name: 'Why?' }))[0])
  expect(await screen.findByRole('alert')).toHaveFocus()
  expect(screen.queryByRole('button', { name: 'Review maintenance plan' })).not.toBeInTheDocument()
  expect(screen.queryByText(/Current vessel/)).not.toBeInTheDocument()
})

it('rejects protected maintenance cards under unavailable coverage and duplicate or reordered paged identities', () => {
  expect(decodeToday(page, vesselId)).toEqual(page)
  expect(() => decodeToday({ ...page, coverage: page.coverage.map(c => c.source === 'TechnicalMaintenance' ? { ...c, status: 'Unavailable' } : c) }, vesselId)).toThrow()
  expect(() => decodeToday({ ...page, cards: { ...page.cards, items: [card, card] } }, vesselId)).toThrow()
  expect(() => decodeToday({ ...page, cards: { ...page.cards, items: [draft, card] } }, vesselId)).toThrow()
  expect(() => decodeToday({ ...page, cards: { ...page.cards, items: [{ ...card, href: `/app/maintenance-plan/${assetId}?id=${planId}` }] } }, vesselId)).toThrow()
})

it('rejects mismatched Why identity, false current context and a previously resolved revoked meter result', () => {
  expect(decodeWhy(why, vesselId, card.key)).toEqual(why)
  expect(() => decodeWhy({ ...why, cardKey: `maintenance-plan:${assetId}` }, vesselId, card.key)).toThrow()
  expect(() => decodeWhy({ ...why, owner: assetId }, vesselId, card.key)).toThrow()
  expect(() => decodeWhy({ ...why, reason: 'MaintenanceDue', maintenance: { ...why.maintenance, calculation: { ...why.maintenance.calculation, state: 'Due', reason: 'CalendarBoundary' } } }, vesselId, card.key)).toThrow()
  expect(() => decodeWhy({ ...why, maintenance: { ...why.maintenance, planRevision: Number.MAX_SAFE_INTEGER + 1 } }, vesselId, card.key)).toThrow()
  expect(() => decodeWhy({ ...why, maintenance: { ...why.maintenance, observationState: 'ObservationRevoked', calculation: { ...why.maintenance.calculation, reason: 'ObservationRevoked', nextDueMeter: '20' } } }, vesselId, card.key)).toThrow()
})

it.each(['\n', '\r', '\u2028', '\u2029'])('rejects a trailing line terminator in exact meter text %j', suffix => {
  expect(() => decodeWhy({ ...why, maintenance: { ...why.maintenance, calculation: { ...why.maintenance.calculation, nextDueMeter: '20' + suffix } } }, vesselId, card.key)).toThrow()
})
