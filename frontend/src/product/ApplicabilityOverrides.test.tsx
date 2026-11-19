import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { changeAppLanguage } from '../i18n'
import { ApplicabilityOverrides } from './ApplicabilityOverrides'
import { ControlCandidates, PassportSources } from './ComplianceControlCandidates'
import { decodeCandidates, decodeOverride, decodeOverrideList } from './compliance-override-api'
import { decodePassport } from './compliance-api'
import { complianceVessel, otherComplianceVessel, passportFixture, recordedReviewFixture } from './compliance-test-fixtures'
import type { ApplicabilityReview } from './compliance-api'

// DEV-040/041/042 UI: authority stays UNCONFIGURED, no approved/active state, drafts only, UNKNOWN freshness.
const rule = '30000000-0000-4000-8000-000000000001', author = '20000000-0000-4000-8000-000000000001'
const response = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const item = (over: Record<string, unknown> = {}) => ({ id: '60000000-0000-4000-8000-000000000001', vesselId: complianceVessel, evaluationId: otherComplianceVessel, ruleId: rule, ruleRevision: 2,
  requirementId: null, requirementRevision: null, sourceId: null, sourceRevision: null, baseResult: 'APPLICABLE', purpose: 'COMPLIANCE_REVIEW', action: 'REQUIRE_ADDITIONAL_CONTROL',
  reasonCode: 'ADDITIONAL_PRECAUTION', rationale: 'FICTIONAL precaution rationale text', evidenceIds: [], validFrom: '2026-11-19T10:00:00+00:00', expiresAt: '2026-12-19T10:00:00+00:00',
  reviewAt: '2026-11-26T10:00:00+00:00', authorId: author, approverId: null, state: 'IN_REVIEW', currentValidity: 'NOT_ACTIVE', sequence: 1, version: 7, createdAt: '2026-11-19T10:00:00+00:00',
  events: [{ sequence: 1, fromState: null, toState: 'IN_REVIEW', eventCode: 'PROPOSE', actorId: author, rationale: null, savedAt: '2026-11-19T10:00:00+00:00' }], ...over })
const list = (items: unknown[] = [item()], over: Record<string, unknown> = {}) => ({ vesselId: complianceVessel, authority: 'UNCONFIGURED', authorityBlockers: ['OVERRIDE_AUTHORITY_POLICY_UNAVAILABLE'],
  canPropose: true, canDecide: false, canRevoke: false, currentEvaluationId: otherComplianceVessel, items, offset: 0, hasMore: false, source: '18.01/Backlog/row:41;60.03/paragraph:154-166,575-580', ...over })
const currentReview = (): ApplicabilityReview => ({ ...recordedReviewFixture(), current: true, blockers: [], reviewerState: 'QUALIFIED_CURRENT' })

beforeEach(async () => { await changeAppLanguage('en', false) })
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })

describe('override contract', () => {
  it('accepts an unconfigured authority list and rejects any active or approved state', () => {
    expect(decodeOverrideList(list(), complianceVessel, 0).items[0].state).toBe('IN_REVIEW')
    expect(() => decodeOverrideList(list([item({ state: 'ACTIVE', approverId: '20000000-0000-4000-8000-000000000002', currentValidity: 'CURRENT' })]), complianceVessel, 0)).toThrow()
    expect(() => decodeOverrideList(list([], { authority: 'CONFIGURED' }), complianceVessel, 0)).toThrow()
    expect(() => decodeOverride(item({ currentValidity: 'CURRENT' }), complianceVessel)).toThrow()
    expect(() => decodeOverride(item({ state: 'ACTIVE', approverId: author }), complianceVessel)).toThrow()
    expect(() => decodeOverride(item({ reasonCode: 'VERIFIED_LEGAL_INTERPRETATION' }), complianceVessel)).toThrow()
    expect(() => decodeOverride(item({ vesselId: otherComplianceVessel }), complianceVessel)).toThrow()
  })
  it('accepts only draft candidates with verification still pending', () => {
    const candidate = { controlId: '70000000-0000-4000-8000-000000000001', revision: 1, version: 3, ruleId: rule, ruleRevision: 2, requirementId: rule, requirementRevision: 1,
      sourceId: rule, sourceRevision: 1, title: 'FICTIONAL requirement', lifecycle: 'DRAFT', controlState: 'DRAFT', blockers: ['OWNER_MISSING', 'VERIFICATION_NOT_PERFORMED'] }
    const outcome = { vesselId: complianceVessel, evaluationId: otherComplianceVessel, created: [candidate], alreadyLinked: 0, blockers: ['CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE'] }
    expect(decodeCandidates(outcome, complianceVessel, otherComplianceVessel).created).toHaveLength(1)
    expect(() => decodeCandidates({ ...outcome, created: [{ ...candidate, controlState: 'PUBLISHED' }] }, complianceVessel, otherComplianceVessel)).toThrow()
    expect(() => decodeCandidates({ ...outcome, blockers: [] }, complianceVessel, otherComplianceVessel)).toThrow()
  })
  it('requires source freshness rows to stay UNKNOWN and an empty source set to be explicit', () => {
    expect(decodePassport(passportFixture(), complianceVessel).sources).toEqual([])
    const missing = passportFixture(); missing.blockers = missing.blockers.filter(b => b !== 'SOURCE_FRESHNESS_UNAVAILABLE')
    expect(() => decodePassport(missing, complianceVessel)).toThrow()
    const source = { sourceId: rule, sourceRevision: 1, ruleIds: [rule], qualifiedAt: null, freshness: 'UNKNOWN', blockers: ['SOURCE_FRESHNESS_POLICY_UNAVAILABLE'], href: `/app/sources/${rule}` }
    const withSource = { ...passportFixture(), sources: [source], blockers: ['QUALIFIED_EVALUATION_UNAVAILABLE', 'CERTIFICATE_READ_ACCESS_UNAVAILABLE', 'CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED', 'SOURCE_FRESHNESS_POLICY_UNAVAILABLE'] }
    expect(decodePassport(withSource, complianceVessel).sources[0].freshness).toBe('UNKNOWN')
    expect(() => decodePassport({ ...withSource, sources: [{ ...source, freshness: 'FRESH' }] }, complianceVessel)).toThrow()
  })
})

describe('ApplicabilityOverrides', () => {
  it('states that authority is not configured, hides approval for non-deciders and withdraws with a retained key', async () => {
    const calls: { url: string; init?: RequestInit }[] = []
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => { calls.push({ url, init }); return init?.method === 'POST' ? response(item({ state: 'REVOKED', currentValidity: 'REVOKED', sequence: 2 })) : response(list()) }))
    const user = userEvent.setup()
    render(<ApplicabilityOverrides vessel={complianceVessel} review={currentReview()} evidence={[]} language="en" csrf="synthetic-csrf-token-0001" offline={false}/>)
    expect(await screen.findByText(/An override requires a configured authority/)).toBeVisible()
    expect(screen.getByText('Authority not configured')).toBeVisible()
    const row = screen.getByText(/FICTIONAL precaution rationale text/).closest('li')!
    expect(within(row).queryByRole('button', { name: 'Approve override' })).not.toBeInTheDocument()
    expect(within(row).getByRole('button', { name: 'Withdraw' })).toBeDisabled()
    await user.type(within(row).getByLabelText('Decision reason'), 'FICTIONAL withdraw reason')
    await user.click(within(row).getByRole('button', { name: 'Withdraw' }))
    await screen.findByRole('status')
    const post = calls.find(c => c.init?.method === 'POST')!
    expect(post.url).toBe(`/api/v1/vessels/${complianceVessel}/applicability-overrides/60000000-0000-4000-8000-000000000001/decisions`)
    expect(JSON.parse(String(post.init!.body))).toEqual({ expectedVersion: 7, expectedSequence: 1, decision: 'WITHDRAW', rationale: 'FICTIONAL withdraw reason' })
    expect((post.init!.headers as Record<string, string>)['X-CSRF-TOKEN']).toBe('synthetic-csrf-token-0001')
  })
  it('shows the server refusal code for an approval attempt without an authority grant', async () => {
    vi.stubGlobal('fetch', vi.fn(async (_url: string, init?: RequestInit) => init?.method === 'POST' ? response({ code: 'override_authority_unavailable' }, 422) : response(list(undefined, { canDecide: true }))))
    const user = userEvent.setup()
    render(<ApplicabilityOverrides vessel={complianceVessel} review={currentReview()} evidence={[]} language="en" csrf="synthetic-csrf-token-0001" offline={false}/>)
    const row = (await screen.findByText(/FICTIONAL precaution rationale text/)).closest('li')!
    await user.type(within(row).getByLabelText('Decision reason'), 'FICTIONAL approval reason')
    await user.click(within(row).getByRole('button', { name: 'Approve override' }))
    expect(await screen.findByRole('alert')).toHaveAttribute('data-code', 'override_authority_unavailable')
    expect(screen.queryByText('Active')).not.toBeInTheDocument()
  })
  it('offers no proposal against a non-current applicability', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => response(list([]))))
    render(<ApplicabilityOverrides vessel={complianceVessel} review={recordedReviewFixture()} evidence={[]} language="en" csrf="synthetic-csrf-token-0001" offline={false}/>)
    expect(await screen.findByText(/an override can only be proposed after re-evaluation/)).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Submit proposal for review' })).not.toBeInTheDocument()
  })
})

describe('ControlCandidates and PassportSources', () => {
  it('generates drafts with the pinned evaluation and lists their open blockers', async () => {
    const fetch = vi.fn(async () => response({ vesselId: complianceVessel, evaluationId: otherComplianceVessel, alreadyLinked: 0, blockers: ['CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE'],
      created: [{ controlId: '70000000-0000-4000-8000-000000000001', revision: 1, version: 3, ruleId: rule, ruleRevision: 2, requirementId: rule, requirementRevision: 1, sourceId: rule, sourceRevision: 1,
        title: 'FICTIONAL requirement', lifecycle: 'DRAFT', controlState: 'DRAFT', blockers: ['OWNER_MISSING', 'VERIFICATION_NOT_PERFORMED'] }] }))
    vi.stubGlobal('fetch', fetch)
    const created = vi.fn()
    render(<ControlCandidates vessel={complianceVessel} review={currentReview()} uncontrolled={1} language="en" csrf="synthetic-csrf-token-0001" offline={false} onCreated={created}/>)
    await userEvent.setup().click(screen.getByRole('button', { name: 'Generate draft control candidates' }))
    await waitFor(() => expect(created).toHaveBeenCalled())
    expect(await screen.findByText(/nothing is published or verified/)).toBeVisible()
    const [, init] = fetch.mock.calls[0] as unknown as [string, RequestInit]
    expect(JSON.parse(String(init.body))).toEqual({ evaluationId: otherComplianceVessel, inputHash: 'A'.repeat(64), ruleSetHash: 'B'.repeat(64) })
  })
  it('renders nothing to generate without a current applicability or uncontrolled requirement', () => {
    const { container } = render(<ControlCandidates vessel={complianceVessel} review={recordedReviewFixture()} uncontrolled={1} language="en" offline={false} onCreated={vi.fn()}/>)
    expect(container).toBeEmptyDOMElement()
  })
  it('shows UNKNOWN source freshness with a drill-down', async () => {
    const navigate = vi.fn()
    render(<PassportSources sources={[{ sourceId: rule, sourceRevision: 1, ruleIds: [rule], qualifiedAt: null, freshness: 'UNKNOWN', blockers: ['SOURCE_FRESHNESS_POLICY_UNAVAILABLE'], href: `/app/sources/${rule}` }]} language="en" onNavigate={navigate}/>)
    expect(screen.getByText('Freshness unknown: no published source-freshness policy')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByText(new RegExp(rule), { selector: 'summary' }))
    await userEvent.setup().click(screen.getByRole('button', { name: 'Open source record' }))
    expect(navigate).toHaveBeenCalledWith(`/app/sources/${rule}`)
  })
})
