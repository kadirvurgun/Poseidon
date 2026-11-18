import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CriticalStateExplanationView, CriticalStatesPanel } from './CriticalStatePanel'
import { decodeCriticalStates } from './critical-state-api'

// DEV-024 / T-S2-005 mocked API component coverage; not a real backend or browser journey.
const vessel = '10000000-0000-0000-0000-000000000024'
const evaluation = '10000000-0000-0000-0000-000000000025'
const at = '2026-11-18T12:00:00+00:00'
const freshness = (state = 'UNKNOWN', reasonCode = 'FIELDS_UNKNOWN', observedAt: string | null = null) => ({ state, reasonCode, observedAt, checkedAt: at })
const onboarding = (extra: object = {}) => ({ key: 'vessel.onboarding', subjectId: vessel, state: 'BLOCKED', disclosure: 'FULL', asOf: at, verification: 'NOT_VERIFIED',
  source: { kind: 'RegulatoryDraft', id: vessel, revision: 3, recordedAt: at, provenance: 'DeclaredUnverified' },
  reasonCodes: ['REQUIRED_DATA_MISSING', 'REQUIRED_DATA_UNKNOWN', 'VERIFICATION_NOT_ESTABLISHED'], freshness: freshness(),
  blockers: [{ fieldPath: 'profile.imo', reasonCode: 'REQUIRED_DATA_MISSING', resolutionType: 'REQUIRED_DECLARATION' },
    { fieldPath: 'profile.flag', reasonCode: 'REQUIRED_DATA_UNKNOWN', resolutionType: 'POLICY_OR_EVIDENCE' },
    { fieldPath: 'profile', reasonCode: 'VERIFICATION_NOT_ESTABLISHED', resolutionType: 'QUALIFIED_EVIDENCE' }],
  href: `/app/fleet/${vessel}/onboarding`, ...extra })
const applicability = (extra: object = {}) => ({ key: 'vessel.compliance.applicability', subjectId: vessel, state: 'STALE', disclosure: 'FULL', asOf: at, verification: 'NOT_VERIFIED',
  source: { kind: 'ApplicabilityEvaluation', id: evaluation, revision: 1, recordedAt: at, provenance: 'QualifiedEvaluation' },
  reasonCodes: ['PROFILE_CHANGED'], freshness: freshness('STALE', 'PROFILE_CHANGED', at),
  blockers: [{ fieldPath: 'applicability', reasonCode: 'PROFILE_CHANGED', resolutionType: 'RE_EVALUATION' }],
  href: `/app/fleet/${vessel}/passport#applicability`, ...extra })
const redacted = (key = 'vessel.profile.freshness', state = 'UNKNOWN') => ({ key, subjectId: vessel, state, disclosure: 'STATE_ONLY', asOf: at })
const page = (states: object[]) => ({ vesselId: vessel, readAt: at, scope: 'VISIBLE_ONLY', states })
const reply = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('CriticalStatesPanel (DEV-024)', () => {
  it('shows source revision, reasons, freshness and resolution-typed blockers without a ready state', async () => {
    const fetch = vi.fn(async () => reply(page([onboarding()]))); vi.stubGlobal('fetch', fetch)
    render(<CriticalStatesPanel vessel={vessel} language="en" revision={3}/>)
    const article = await screen.findByRole('article', { name: 'Onboarding readiness' })
    expect(within(article).getByText('Blocked')).toBeVisible()
    expect(article).toHaveTextContent('Vessel profile · revision: 3')
    expect(article).toHaveTextContent('Not verified')
    expect(within(article).getByText('Unknown')).toBeVisible()
    expect(article).toHaveTextContent('Some fields have unknown freshness')
    const blockers = within(article).getByRole('region', { name: 'Blockers' })
    expect(blockers).toHaveTextContent('IMO'); expect(blockers).toHaveTextContent('Resolution: Declare the required value')
    expect(blockers).toHaveTextContent('Resolution: Policy or evidence needed'); expect(blockers).toHaveTextContent('Qualified evidence needed')
    expect(within(article).getByRole('link', { name: 'Open the owning record' })).toHaveAttribute('href', `/app/fleet/${vessel}/onboarding`)
    expect(screen.queryByText(/^Ready$/)).not.toBeInTheDocument()
    expect(fetch).toHaveBeenCalledWith(`/api/v1/vessels/${vessel}/critical-states`, expect.objectContaining({ credentials: 'same-origin' }))
  })

  it('shows a stale applicability evaluation with its evaluation source and re-evaluation path', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => reply(page([applicability()]))))
    render(<CriticalStatesPanel vessel={vessel} language="en"/>)
    const article = await screen.findByRole('article', { name: 'Compliance applicability' })
    expect(article).toHaveTextContent('Stale'); expect(article).toHaveTextContent('Applicability evaluation · revision: 1')
    expect(article).toHaveTextContent('Profile changed after evaluation'); expect(article).toHaveTextContent('Resolution: Re-evaluate')
  })

  it('renders a redacted entry as existence only, with no source, reasons, blockers or link', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => reply(page([redacted('vessel.onboarding', 'REVIEW_REQUIRED'), redacted()]))))
    render(<CriticalStatesPanel vessel={vessel} language="en"/>)
    const article = await screen.findByRole('article', { name: 'Onboarding readiness' })
    expect(article).toHaveAttribute('data-disclosure', 'STATE_ONLY')
    expect(article).toHaveTextContent('Review required'); expect(article).toHaveTextContent('Details are restricted for your access')
    expect(within(article).queryByRole('link')).toBeNull(); expect(within(article).queryByText('Source')).toBeNull()
    expect(within(article).queryByRole('region', { name: 'Blockers' })).toBeNull()
    expect(screen.getByRole('article', { name: 'Profile freshness' })).toHaveTextContent('Unknown')
    expect(screen.getByText('Only states you are allowed to view are listed.')).toBeVisible()
  })

  it('withdraws previously shown explanations when access is revoked and reports the denial', async () => {
    const fetch = vi.fn(async () => reply(page([onboarding()]))); vi.stubGlobal('fetch', fetch)
    const denied = vi.fn(), user = userEvent.setup()
    render(<CriticalStatesPanel vessel={vessel} language="en" onDenied={denied}/>)
    await screen.findByRole('article', { name: 'Onboarding readiness' })
    fetch.mockImplementation(async () => reply({ code: 'permission_denied' }, 403))
    await user.click(screen.getByRole('button', { name: 'Reload critical states' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('You do not have permission')
    expect(screen.queryByRole('article')).toBeNull(); expect(denied).toHaveBeenCalledTimes(1)
  })

  it('rejects contracts that leak details into a redacted entry or claim a positive state', async () => {
    expect(() => decodeCriticalStates(page([{ ...redacted(), source: onboarding().source }]), vessel)).toThrow()
    expect(() => decodeCriticalStates(page([redacted('vessel.onboarding', 'STALE')]), vessel)).toThrow()
    expect(() => decodeCriticalStates(page([onboarding({ state: 'READY' })]), vessel)).toThrow()
    expect(() => decodeCriticalStates(page([onboarding({ verification: 'VERIFIED' })]), vessel)).toThrow()
    expect(() => decodeCriticalStates(page([onboarding({ subjectId: evaluation })]), vessel)).toThrow()
    expect(() => decodeCriticalStates(page([onboarding(), onboarding()]), vessel)).toThrow()
    expect(() => decodeCriticalStates(page([onboarding({ blockers: [{ fieldPath: 'profile.imo', reasonCode: 'X', resolutionType: 'AUTO_APPROVE' }] })]), vessel)).toThrow()
    const unavailable: Record<string, unknown> = applicability({ state: 'UNAVAILABLE' }); delete unavailable.source
    expect(decodeCriticalStates(page([unavailable]), vessel).states).toHaveLength(1)
    vi.stubGlobal('fetch', vi.fn(async () => reply(page([{ ...redacted(), reasonCodes: ['VALUE_UNKNOWN'] }]))))
    render(<CriticalStatesPanel vessel={vessel} language="en"/>)
    expect(await screen.findByRole('alert')).toHaveTextContent('could not be loaded')
  })

  it('does not show cached states offline and localizes to Turkish', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => reply(page([onboarding({ state: 'DECLARED_UNVERIFIED', blockers: [] })]))))
    const view = render(<CriticalStatesPanel vessel={vessel} language="tr" dirty/>)
    const article = await screen.findByRole('article', { name: 'Onboarding hazırlığı' })
    expect(article).toHaveTextContent('Beyan edildi · doğrulanmadı'); expect(article).toHaveTextContent('Listelenen engel yok')
    expect(screen.getByText(/Kaydedilmemiş form değişiklikleri dahil değildir/)).toBeVisible()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    window.dispatchEvent(new Event('offline'))
    await waitFor(() => expect(screen.queryByRole('article')).toBeNull())
    expect(screen.getByText(/Çevrimdışı/)).toBeVisible()
    view.unmount()
  })

  it('renders a single explanation for reuse by other Why panels', () => {
    render(<CriticalStateExplanationView state={decodeCriticalStates(page([applicability({ state: 'QUALIFIED_CURRENT_UNVERIFIED', reasonCodes: [], blockers: [] })]), vessel).states[0]} language="en" headingLevel={3}/>)
    expect(screen.getByRole('heading', { level: 3, name: 'Compliance applicability' })).toBeVisible()
    expect(screen.getByText('Qualified evaluation current · not verified')).toBeVisible()
    expect(screen.getByText('No blockers listed')).toBeVisible()
  })
})
