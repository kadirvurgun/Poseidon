import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { TodayWorkspace } from './TodayWorkspace'
import { decodeToday, decodeWhy } from './today-api'

const vesselId = '10000000-0000-0000-0000-000000000001'
const jobId = '20000000-0000-0000-0000-000000000001'
const planId = '30000000-0000-0000-0000-000000000001'
const now = '2026-11-02T00:00:00Z'
const vessel = { id: vesselId, companyId: planId, code: 'SYN-TT', name: 'FICTIONAL tug', kind: 'Tug', version: 1, canEdit: true }
const draft = { key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftAbsent',
  owner: null, nextAction: 'ReviewDraft', href: `/app/fleet/${vesselId}/profile` }
const card = { key: `tug-job:${jobId}`, priority: 'Review', source: 'TugJob', reason: 'TugPlanMissing',
  owner: null, nextAction: 'ReviewTugJob', href: `/app/tug-job-detail/${jobId}?vessel=${vesselId}` }
const page = { vessel, readAt: now,
  coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
    .map((source, i) => ({ source, status: i === 0 ? 'Missing' : i === 6 ? 'Available' : 'Unavailable' })),
  cards: { items: [card, draft], offset: 0, limit: 25, hasMore: false } }
const why = { vesselId, cardKey: card.key, rule: 'TW-TUG-01', reason: card.reason, inputs: null, readAt: now, savedAt: now,
  sourceObservedAt: null, freshness: 'Unverified', provenance: 'UserDeclaredUnverified',
  blockers: ['DraftPlanningNotOperationalApproval', 'OwningReadinessCoverageIncomplete', 'MasterDecisionRequired'],
  owner: null, nextAction: card.nextAction, href: card.href,
  tug: { jobId, requestRevision: 2, requestIncomplete: true, planRevision: null, plannedRequestRevision: null,
    plannedStart: null, plannedEnd: null, assignedTugs: [], currentOwnerRoleId: null, evidence: null } }
const evidence = { availableBollardPull: null, capacityState: 'ReviewRequired', bp: [], conflictState: 'Unknown', conflicts: [],
  coverage: 'CurrentAuthorizedOwningFacts;DraftGeometry;MasterDecisionSeparate', readiness: 'Blocked', ownedBp: [], operationalConflicts: [],
  readinessFacts: [{ tugId: vesselId, source: 'MaintenancePlan', id: planId, revision: 3, state: 'Blocked', why: 'CalendarBoundary', observedAt: null, expiresAt: null }] }
const blocked = { ...why, reason: 'TugReadinessBlocked', tug: { ...why.tug, planRevision: 4, plannedRequestRevision: 2,
  plannedStart: now, plannedEnd: '2026-11-02T01:00:00Z', assignedTugs: [vesselId], evidence } }
const blockedCard = { ...card, reason: 'TugReadinessBlocked', priority: 'Attention' }
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('shows a real missing-plan explanation in TR/EN and follows only its scoped job destination', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('why-engine') ? why : page)))
  const navigate = vi.fn(); const user = userEvent.setup()
  const view = render(<TodayWorkspace id={vesselId} language="tr" offline={false} onNavigate={navigate} />)
  expect(await screen.findByText('Bu iş için görünür taslak plan yok.')).toBeVisible()
  view.rerender(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={navigate} />)
  const article = screen.getByRole('heading', { name: 'Review required · Tug job' }).closest('article')!
  const button = within(article).getByRole('button', { name: 'Why?' }); button.focus(); await user.keyboard('{Enter}')
  expect(await screen.findByText('Current request / plan / planned request revision: 2 / Unknown / Unknown')).toBeVisible()
  expect(screen.getByText('A draft plan does not authorize dispatch or operation.')).toBeVisible()
  expect(screen.queryByText(/Declared \/ missing \/ unknown inputs/)).not.toBeInTheDocument()
  expect(within(article).getByRole('button', { name: 'Close Why' })).toHaveFocus()
  await user.click(within(article).getByRole('button', { name: 'Review tug job' }))
  expect(navigate).toHaveBeenCalledWith(card.href)
})

it('discloses current blocking facts and exact revisions without implying operational readiness', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('why-engine') ? blocked : { ...page, cards: { ...page.cards, items: [blockedCard, draft] } })))
  const user = userEvent.setup(); render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  await user.click((await screen.findAllByRole('button', { name: 'Why?' }))[0])
  expect(await screen.findByText('Current planning assessment: Blocked by current inputs')).toBeVisible()
  expect(screen.getByText('Qualified planning capacity (tonnes): Unknown')).toBeVisible()
  await user.click(screen.getByText('Current owning source facts'))
  expect(screen.getByText('Maintenance planning · Blocked by current inputs')).toBeVisible()
  expect(screen.getByText(/Revision: 3/)).toBeVisible()
  expect(screen.getByText('The authorized Master decision remains separate.')).toBeVisible()
})

it('shows the current server-owned role reference and withdraws it from a later explanation after revocation', async () => {
  let currentOwner: string | null = planId
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('why-engine') ? {
    ...why, owner: currentOwner, tug: { ...why.tug, currentOwnerRoleId: currentOwner }
  } : { ...page, cards: { ...page.cards, items: [{ ...card, owner: planId }, draft] } })))
  const user = userEvent.setup(); render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  const article = await screen.findByRole('article', { name: `Tug job ${jobId}` })
  await user.click(within(article).getByRole('button', { name: 'Why?' }))
  expect(await screen.findByText(`Current source owner role: ${planId}`)).toBeVisible()
  await user.click(within(article).getByRole('button', { name: 'Close Why' })); currentOwner = null
  await user.click(within(article).getByRole('button', { name: 'Why?' }))
  expect(await screen.findByText('Current source owner role: Unknown')).toBeVisible()
})

it.each([403, 404])('withdraws the prior entire feed after current job permission or link denial %s', async status => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('why-engine') ? {} : page, url.includes('why-engine') ? status : 200)))
  const user = userEvent.setup(); render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  await user.click((await screen.findAllByRole('button', { name: 'Why?' }))[0])
  expect(await screen.findByRole('alert')).toHaveFocus()
  expect(screen.queryByRole('button', { name: 'Review tug job' })).not.toBeInTheDocument()
  expect(screen.queryByText(/Current vessel/)).not.toBeInTheDocument()
})

it('aborts a late job explanation when a different vessel is selected', async () => {
  let complete!: (result: Response) => void; let signal: AbortSignal | undefined
  vi.stubGlobal('fetch', vi.fn((url: string, init?: RequestInit) => {
    if (url.includes('why-engine')) { signal = init?.signal as AbortSignal; return new Promise<Response>(resolve => { complete = resolve }) }
    if (url.includes(vesselId)) return Promise.resolve(response(page))
    return Promise.resolve(response({}))
  }))
  const user = userEvent.setup(); const props = { language: 'en' as const, offline: false, onNavigate: vi.fn() }
  const view = render(<TodayWorkspace {...props} id={vesselId} />)
  await user.click((await screen.findAllByRole('button', { name: 'Why?' }))[0]); await waitFor(() => expect(complete).toBeTypeOf('function'))
  view.rerender(<TodayWorkspace {...props} id={planId} />)
  expect(signal?.aborted).toBe(true)
  await act(async () => complete(response(why)))
  expect(screen.queryByText(/Current request \/ plan/)).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Review tug job' })).not.toBeInTheDocument()
})

it('clears the job explanation offline and rechecks source coverage after reconnect', async () => {
  let available = true
  const fetcher = vi.fn(async (url: string) => response(url.includes('why-engine') ? why : available ? page : {
    ...page, coverage: page.coverage.map(c => c.source === 'TugPlanning' ? { ...c, status: 'Unavailable' } : c), cards: { ...page.cards, items: [draft] } }))
  vi.stubGlobal('fetch', fetcher); const user = userEvent.setup(); const props = { id: vesselId, language: 'en' as const, onNavigate: vi.fn() }
  const view = render(<TodayWorkspace {...props} offline={false} />)
  await user.click((await screen.findAllByRole('button', { name: 'Why?' }))[0]); await screen.findByText(/Current request \/ plan/)
  view.rerender(<TodayWorkspace {...props} offline />)
  expect(screen.queryByText(/Current request \/ plan/)).not.toBeInTheDocument()
  for (const button of screen.getAllByRole('button', { name: 'Why?' })) expect(button).toBeDisabled()
  available = false; view.rerender(<TodayWorkspace {...props} offline={false} />)
  await screen.findByText('Tug draft planning: Unavailable in this context')
  expect(screen.queryByRole('button', { name: 'Review tug job' })).not.toBeInTheDocument()
})

it('rejects job cards from unavailable coverage, ship context, wrong destination or a false priority', () => {
  expect(decodeToday(page, vesselId)).toEqual(page)
  const invalid = [
    { ...page, coverage: page.coverage.map(c => c.source === 'TugPlanning' ? { ...c, status: 'Unavailable' } : c) },
    { ...page, vessel: { ...vessel, kind: 'Ship' } },
    { ...page, cards: { ...page.cards, items: [{ ...card, href: `/app/tug-job-detail/${jobId}?vessel=${planId}` }] } },
    { ...page, cards: { ...page.cards, items: [{ ...card, priority: 'Attention' }] } },
    { ...page, cards: { ...page.cards, items: [card, card] } }
  ]
  for (const candidate of invalid) expect(() => decodeToday(candidate, vesselId)).toThrow()
})

it('rejects forged readiness, partial hidden assignments, mismatched lineage and invented missing-plan evidence', () => {
  expect(decodeWhy(why, vesselId, card.key)).toEqual(why)
  expect(decodeWhy(blocked, vesselId, card.key)).toEqual(blocked)
  expect(decodeWhy({ ...why, owner: planId, tug: { ...why.tug, currentOwnerRoleId: planId } }, vesselId, card.key).owner).toBe(planId)
  const invalid = [
    { ...blocked, reason: 'TugPlanningReviewRequired' },
    { ...blocked, tug: { ...blocked.tug, plannedRequestRevision: 3 } },
    { ...blocked, tug: { ...blocked.tug, requestRevision: Number.MAX_SAFE_INTEGER + 1 } },
    { ...blocked, tug: { ...blocked.tug, assignedTugs: [planId] } },
    { ...blocked, tug: { ...blocked.tug, evidence: { ...evidence, readiness: 'Ready' } } },
    { ...why, tug: { ...why.tug, evidence } },
    { ...why, tug: { ...why.tug, assignedTugs: [vesselId] } },
    { ...why, tug: { ...why.tug, jobId: planId } },
    { ...why, owner: planId }, { ...why, sourceObservedAt: now },
    { ...why, blockers: ['MasterDecisionRecorded'] }, { ...why, maintenance: {} }
  ]
  for (const candidate of invalid) expect(() => decodeWhy(candidate, vesselId, card.key)).toThrow()
})
