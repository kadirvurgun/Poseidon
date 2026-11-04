import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CompliancePassport } from './CompliancePassport'
import { OnboardingAssessmentView } from './VesselOnboarding'
import { ApplicabilityReasonView } from './ApplicabilityReasonView'
import { decodeEvaluationResults } from './applicability-api'
import { assessmentFixture, complianceVessel, otherComplianceVessel, passportFixture, recordedReviewFixture } from './compliance-test-fixtures'
const response = (value: unknown, status = 200) => ({ ok: status < 300, status, json: async () => value }) as Response
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('S5-EXPERIENCE-01 honest source review (authored; execution deferred)', () => {
  it('keeps incomplete and unsaved onboarding inputs out of submission authority', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => response(assessmentFixture())))
    const user = userEvent.setup(), view = render(<OnboardingAssessmentView vessel={complianceVessel} savedRevision={1} dirty language="en" />)
    await screen.findByText('Review required')
    expect(screen.getByText(/Unsaved inputs are not included/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Submit for review' })).toBeDisabled()
    expect(screen.getAllByText('Incomplete · review required')).toHaveLength(8)
    view.rerender(<OnboardingAssessmentView vessel={complianceVessel} savedRevision={2} dirty={false} language="en" />)
    await screen.findByRole('alert')
    expect(screen.getByRole('alert')).toHaveTextContent('saved profile revision changed')
    await user.click(screen.getByRole('button', { name: 'Refresh saved assessment' }))
    expect(screen.getByRole('button', { name: 'Submit for review' })).toBeDisabled()
  })
  it('shows complete declarations as unverified with submission still unavailable', async () => {
    const a = assessmentFixture(); vi.stubGlobal('fetch', vi.fn(async () => response({ ...a, state: 'DECLARED_COMPLETE_UNVERIFIED', fields: a.fields.map(f => ({ ...f, state: 'Known', complete: true })), blockers: ['VERIFICATION_NOT_ESTABLISHED'] })))
    render(<OnboardingAssessmentView vessel={complianceVessel} savedRevision={1} dirty={false} language="en" />)
    expect(await screen.findByText('Declared complete · unverified')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Submit for review' })).toBeDisabled()
    expect(screen.queryByText('Ready')).not.toBeInTheDocument()
  })
  it('shows missing certificate and critical coverage sources without a fake zero or approval', async () => {
    const fetch = vi.fn(async () => response(passportFixture())); vi.stubGlobal('fetch', fetch)
    const navigate = vi.fn(), user = userEvent.setup(); render(<CompliancePassport vessel={complianceVessel} language="en" onNavigate={navigate} />)
    await screen.findByRole('heading', { name: 'Certificates' })
    expect(screen.getByText(/Authoritative certificate catalog unavailable/)).toBeVisible()
    const coverage = screen.getByRole('heading', { name: 'Control-plan coverage' }).parentElement!
    expect(coverage).toHaveTextContent('Uncontrolled requirements: Unavailable')
    expect(coverage).toHaveTextContent('Critical requirement coverage: Unavailable')
    expect(screen.getByRole('button', { name: 'Manual override unavailable' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Review onboarding inputs' }))
    expect(navigate).toHaveBeenCalledWith(`/app/fleet/${complianceVessel}/onboarding`)
    expect(fetch).toHaveBeenCalledWith(`/api/v1/vessels/${complianceVessel}/compliance-passport?offset=0`, expect.objectContaining({ credentials: 'same-origin' }))
  })
  it('withdraws the prior passport after context change and preserves unavailable access instead of healthy empty', async () => {
    const fetch = vi.fn(async (url: string) => response(url.includes(otherComplianceVessel) ? {} : passportFixture(), url.includes(otherComplianceVessel) ? 403 : 200)); vi.stubGlobal('fetch', fetch)
    const view = render(<CompliancePassport vessel={complianceVessel} language="en" onNavigate={vi.fn()} />)
    await screen.findByText(/Authoritative certificate catalog unavailable/)
    view.rerender(<CompliancePassport vessel={otherComplianceVessel} language="en" onNavigate={vi.fn()} />)
    expect(screen.queryByText(/Authoritative certificate catalog unavailable/)).not.toBeInTheDocument()
    expect(await screen.findByRole('alert')).toHaveTextContent('access denied')
    expect(screen.queryByText('No authorized records returned on this page.')).not.toBeInTheDocument()
  })
  it('withdraws all passport content on offline and permission revalidation', async () => {
    let denied = false; vi.stubGlobal('fetch', vi.fn(async () => response(denied ? {} : passportFixture(), denied ? 403 : 200)))
    render(<CompliancePassport vessel={complianceVessel} language="en" onNavigate={vi.fn()} />)
    await screen.findByText(/Authoritative certificate catalog unavailable/)
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false); window.dispatchEvent(new Event('offline'))
    await screen.findByText('Offline. Current passport sources unavailable.')
    expect(screen.queryByText(/Authoritative certificate catalog unavailable/)).not.toBeInTheDocument()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); denied = true; window.dispatchEvent(new Event('online'))
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('access denied'))
  })
  it('renders exact retained captured facts and hashes with historical qualification separate', async () => {
    const p = passportFixture(); p.applicability = recordedReviewFixture()
    vi.stubGlobal('fetch', vi.fn(async () => response(p)))
    render(<CompliancePassport vessel={complianceVessel} language="en" onNavigate={vi.fn()} />)
    const region = await screen.findByRole('region', { name: 'Exact evaluated inputs' })
    expect(region).toHaveTextContent('A'.repeat(64)); expect(region).toHaveTextContent('B'.repeat(64))
    await userEvent.setup().click(within(region).getByText('Captured context facts', { selector: 'summary' }))
    expect(within(region).getByText('vessel.flag')).toBeVisible()
    expect(screen.getByText(/Retained recorded results remain historical/)).toBeVisible()
  })
  it('shows an Unknown predicate, pinned source, activations and rule integrity without granting current authority', async () => {
    const rows = decodeEvaluationResults([{ ruleId: otherComplianceVessel, revision: 2, hash: 'C'.repeat(64), sourceId: complianceVessel, sourceRevision: 3, requirementId: null, requirementRevision: null, prediction: { result: 'REVIEW_REQUIRED', currentResult: 'REVIEW_REQUIRED', authoritative: false, lifecycle: 'DRAFT', reason: { path: 'when', truth: 0, reason: 'Missing', field: 'vessel.flag', operator: 'EQ', children: [], reviewRequired: true }, predictedActivations: [{ type: 'Control', id: 'synthetic-control' }], blockers: ['preview_only'] } }], false)
    render(<ApplicabilityReasonView results={rows} language="en" />)
    await userEvent.setup().click(screen.getByText(/Rule .*\/ 2/, { selector: 'summary' }))
    expect(screen.getByText(/Preview only/)).toBeVisible()
    expect(screen.getByRole('list', { name: 'Applicability reasons' })).toHaveTextContent('vessel.flag · Unknown · Missing')
    expect(screen.getByText(/synthetic-control/)).toHaveTextContent('Projected need only')
    expect(screen.getByText('C'.repeat(64))).toBeVisible()
  })
})
