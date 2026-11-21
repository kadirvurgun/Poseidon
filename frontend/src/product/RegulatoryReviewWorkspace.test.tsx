import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { RegulatoryReviewWorkspace, readReviewRoute } from './RegulatoryReviewWorkspace'
import { decodeReviewDetail, decodeReviewItem } from './regulatory-review-api'
import { changeAppLanguage } from '../i18n'

// S11 WP-C DEV-073/093 UI. Synthetic identities and FICTIONAL codes only; no confirmation is seeded or simulated by the system.
const vessel = '11111111-1111-4111-8111-111111111111', actorId = '44444444-4444-4444-8444-444444444444', author = '99999999-9999-4999-8999-999999999999'
const tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666', campaignId = '77777777-7777-4777-8777-777777777777'
const itemA = '88888888-8888-4888-8888-888888888881', itemB = '88888888-8888-4888-8888-888888888882'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const item = (patch: Record<string, unknown> = {}) => ({ id: itemA, ordinal: 1, kind: 'SOURCE_CANDIDATE', subjectKey: 'SRC-ISM', ruleId: null, ruleRevision: null,
  requirementId: null, requirementRevision: null, sourceId: null, sourceRevision: null, candidateSourceCode: 'SRC-ISM', candidateAuthority: 'STATUTORY', candidateUnverified: true,
  predictedResult: null, verdict: 'REVIEW_REQUIRED', effectiveVerdict: 'REVIEW_REQUIRED', stale: false, open: true, sourceAuthority: null, authorityCategory: 'STATUTORY_LAW',
  ownerRole: 'UNASSIGNED', sourceReference: null, sourceEdition: null, trackedIssueId: null, reviewerId: null, qualificationRef: null, verdictCount: 0, verdictAt: null,
  version: 7, blockers: ['AUTHORITATIVE_SOURCE_UNRESOLVED', 'CANDIDATE_UNVERIFIED', 'TRACKED_ISSUE_MISSING'], replayed: false, ...patch })
const limit = item({ id: itemB, ordinal: 2, kind: 'TUG_LIMIT', subjectKey: 'WIND_SPEED', candidateSourceCode: null, candidateAuthority: null, candidateUnverified: false,
  authorityCategory: 'UNASSIGNED', blockers: ['LIMIT_SOURCE_MISSING', 'TRACKED_ISSUE_MISSING'] })
const detail = (patch: Record<string, unknown> = {}, items = [item(), limit], qualified = true) => ({
  campaign: { id: campaignId, vesselId: vessel, scope: 'TUG', state: 'OPEN', profileRevision: 1, evaluationId: null, pinHash: 'A'.repeat(64), itemCount: items.length,
    openItems: items.filter(i => i.open).length, untrackedOpenItems: items.filter(i => i.open && !i.trackedIssueId).length, confirmedItems: items.filter(i => !i.open).length,
    staleItems: 0, profileStale: false, createdBy: author, createdAt: '2026-11-24T08:00:00Z', closedBy: null, closedAt: null, openItemsAtClose: null, provenance: 'Recorded',
    version: 3, blockers: ['APPLICABILITY_EVALUATION_UNAVAILABLE', 'OPEN_ITEMS_REVIEW_REQUIRED', 'OPEN_ITEMS_UNTRACKED'] },
  items, ownerMatrix: [{ authority: 'STATUTORY', category: 'STATUTORY_LAW', items: 1, open: 1, confirmed: 0, ownerRoles: ['UNASSIGNED'] },
    { authority: 'UNASSIGNED', category: 'UNASSIGNED', items: 1, open: 1, confirmed: 0, ownerRoles: ['UNASSIGNED'] }],
  canManage: false, canReview: true, reviewerAuthorities: [{ id: grant, scopes: ['SHIP', 'TUG'], sourceAuthorities: ['STATUTORY', 'MANUFACTURER'], qualificationVerified: qualified }],
  notices: ['NO_UNSOURCED_LIMIT_IS_A_LEGAL_DEFAULT', 'GUIDANCE_IS_NOT_STATUTORY_LAW', 'NO_CLASS_OR_TYPE_APPROVAL_CLAIM', 'REVIEW_NEVER_ACTIVATES_RULES'], replayed: false,
  policyVersion: 'regulatory-sme-review-v1', candidateSourcePlan: 'contracts/sources/pilot-ownership.v1.json', ...patch })
const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let body: unknown = detail(), listBody: unknown = null, commandStatus = 200, commandBody: unknown = {}
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; body = detail(); listBody = null; commandStatus = 200; commandBody = item({ verdict: 'AMBIGUOUS', effectiveVerdict: 'AMBIGUOUS', trackedIssueId: 'REG-1', reviewerId: actorId, verdictCount: 1 })
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST') { commands.push({ url, init }); return json(commandBody, commandStatus) }
    if (url.includes('regulatory-reviews?')) return typeof body === 'number' ? json({ code: 'permission_denied' }, body) : json(listBody ?? { items: [(body as { campaign: unknown }).campaign], canManage: false })
    if (url.includes(`regulatory-reviews/${campaignId}`)) return typeof body === 'number' ? json({ code: 'permission_denied' }, body) : json(body)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })
const open = (scope = 'TUG') => history.replaceState(null, '', `/app/regulatory-review/${vessel}?scope=${scope}`)

it('parses closed routes and refuses unknown scopes or parameters', () => {
  expect(readReviewRoute(`/app/regulatory-review/${vessel}?scope=TUG`)).toMatchObject({ vessel, scope: 'TUG', invalid: false })
  expect(readReviewRoute('/app/regulatory-review')).toMatchObject({ vessel: null, scope: 'SHIP', invalid: false })
  expect(readReviewRoute(`/app/regulatory-review/${vessel}?scope=PLATFORM`).invalid).toBe(true)
  expect(readReviewRoute(`/app/regulatory-review/${vessel}?scope=SHIP&extra=1`).invalid).toBe(true)
  expect(readReviewRoute('/app/regulatory-review/not-a-guid').invalid).toBe(true)
})

it('rejects a stale item shown as confirmed, a confirmation without reviewer or owner and inconsistent counts', () => {
  expect(() => decodeReviewItem(item({ stale: true, verdict: 'CONFIRMED', effectiveVerdict: 'CONFIRMED', open: false, reviewerId: actorId, verdictCount: 1, sourceAuthority: 'STATUTORY', ownerRole: 'DPA' }))).toThrow()
  expect(() => decodeReviewItem(item({ verdict: 'CONFIRMED', effectiveVerdict: 'CONFIRMED', open: false, reviewerId: actorId, verdictCount: 1, sourceAuthority: 'STATUTORY' }))).toThrow()
  expect(() => decodeReviewItem(item({ open: false }))).toThrow()
  expect(() => decodeReviewItem(item({ verdict: 'APPROVED' }))).toThrow()
  expect(() => decodeReviewDetail(detail({ campaign: { ...detail().campaign, openItems: 0 } }), vessel)).toThrow()
  expect(decodeReviewDetail(detail(), vessel).items).toHaveLength(2)
})

it('shows the tug boundary, unsourced limits as review required, a red untracked count and records a tracked ambiguity with the exact version', async () => {
  open()
  const user = userEvent.setup()
  render(<RegulatoryReviewWorkspace/>)
  expect(await screen.findByText('WIND_SPEED')).toBeInTheDocument()
  expect(screen.getByText(/No towage limit without a controlled source is a legal default/)).toBeInTheDocument()
  expect(document.querySelector('[data-item="WIND_SPEED"]')!.textContent).toContain('LIMIT_SOURCE_MISSING')
  expect(document.querySelector('[data-item="WIND_SPEED"] [data-verdict="REVIEW_REQUIRED"]')!.getAttribute('data-tone')).toBe('warning')
  expect(document.querySelector('[data-untracked]')!.getAttribute('data-tone')).toBe('danger')
  expect(document.querySelector('[data-open-items]')!.getAttribute('data-tone')).toBe('danger')
  expect(document.querySelector('[data-tone="success"]')).toBeNull()
  await user.click(screen.getAllByRole('radio')[0])
  await user.click(screen.getByRole('radio', { name: 'Ambiguous' }))
  await user.type(screen.getByLabelText('Tracked issue ID'), 'REG-1')
  await user.type(screen.getByLabelText('Reviewer note'), 'FICTIONAL edition unresolved for the pilot flag')
  await user.click(screen.getByRole('button', { name: 'Record' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  const sent = JSON.parse(String(commands[0].init.body))
  expect(sent).toEqual({ expectedVersion: 7, authorityId: grant, verdict: 'AMBIGUOUS', sourceAuthority: 'STATUTORY', ownerRole: 'UNASSIGNED', sourceReference: null,
    sourceEdition: null, trackedIssueId: 'REG-1', note: 'FICTIONAL edition unresolved for the pilot flag' })
  expect(commands[0].url).toContain(`regulatory-reviews/${campaignId}/items/${itemA}/verdicts`)
  expect(new Headers(commands[0].init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
})

it('never offers confirmation to an unverified reviewer and keeps the author read-only', async () => {
  body = detail({}, [item(), limit], false)
  open()
  const user = userEvent.setup()
  const view = render(<RegulatoryReviewWorkspace/>)
  expect(await screen.findByText('SRC-ISM')).toBeInTheDocument()
  await user.click(screen.getAllByRole('radio')[0])
  expect(screen.getByText(/qualification is unverified/)).toBeInTheDocument()
  expect(screen.getByRole('radio', { name: 'Confirmed' })).toBeDisabled()
  expect(screen.getByRole('radio', { name: 'Not applicable (confirmed)' })).toBeDisabled()
  view.unmount()
  body = detail({ canReview: false })
  open()
  render(<RegulatoryReviewWorkspace/>)
  expect(await screen.findByText(/Verdicts are read-only/)).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Record' })).toBeNull()
})

it('offers to open a campaign only to a manager and shows refusals with their code', async () => {
  listBody = { items: [], canManage: true }
  commandStatus = 409; commandBody = { code: 'review_campaign_open', correlationId: 'x' }
  open('SHIP')
  const user = userEvent.setup()
  render(<RegulatoryReviewWorkspace/>)
  expect(await screen.findByText('No review campaign for this vessel and scope.')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Open review campaign' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ vesselId: vessel, scope: 'SHIP' })
  expect(await screen.findByText('review_campaign_open')).toBeInTheDocument()
  expect(document.querySelector('[data-notice="refused"]')).not.toBeNull()
})

it('shows the forbidden state without commands', async () => {
  body = 403
  open()
  render(<RegulatoryReviewWorkspace/>)
  expect(await screen.findByText('You do not have access to regulatory review for this vessel.')).toBeInTheDocument()
  expect(commands).toHaveLength(0)
})
