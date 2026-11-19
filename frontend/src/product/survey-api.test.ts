import { afterEach, describe, expect, it, vi } from 'vitest'
import { decodeChecklist, decodeFit, decodePlanner, decodeWindow, surveyApi, VerificationRejected, type Planner } from './survey-api'
import { checklistFixture, surveyCertificateId, surveyItem, surveyVessel } from './survey-test-fixtures'

const plannerFixture = (): Planner => ({
  from: '2026-10-21', to: '2027-11-20', referenceDate: '2026-11-20', truncated: false, vesselsTruncated: false,
  items: [{ vesselId: surveyVessel, vesselCode: 'FICT-1', vesselName: 'Fictional One', vesselKind: 'Ship', certificateId: surveyCertificateId, certificateRevision: 1, certificateVersion: 7,
    certificateType: 'SYNTHETIC-CERT', certificateNumber: null, issuer: null, declaredFlag: null, regulatoryBasis: null, expiryDate: '2027-06-01',
    window: { state: 'OPEN', start: '2026-11-01', end: '2027-02-01', daysToOpen: 0, daysToClose: 73, daysOverdue: null, qualifiers: ['DECLARED_WINDOW_NOT_SOURCE_POLICY'] },
    fit: { state: 'UNKNOWN', reasons: ['NO_PREPARATION_PLAN'], basis: ['VOYAGE_REFERENCE_UNAVAILABLE'] }, checklist: null, coordination: 'EXTERNAL_NOT_SIMULATED',
    href: `/app/certificate-command-center/${surveyVessel}?id=${surveyCertificateId}` }],
  counts: { windowUnknown: 0, windowUpcoming: 0, windowOpen: 1, windowOverdue: 0, fitConditional: 0, fitNotFit: 0, fitUnknown: 1, checklistBlocked: 0, checklistMissing: 1 },
  qualification: 'ADVISORY_PLANNING_NOT_AUTHORITY_SCHEDULE', fitStates: ['CONDITIONAL', 'NOT_FIT', 'UNKNOWN'], sourceVersion: 'synthetic',
})

describe('DEV-104 checklist contract', () => {
  it('accepts an advisory checklist and rejects any READY, FIT or manufactured verification', () => {
    expect(decodeChecklist(checklistFixture(), surveyVessel).readiness.state).toBe('BLOCKED')
    const row = checklistFixture()
    expect(() => decodeChecklist({ ...row, readiness: { ...row.readiness, state: 'READY' } }, surveyVessel)).toThrow()
    expect(() => decodeChecklist({ ...row, voyageFit: { ...row.voyageFit, state: 'FIT' } }, surveyVessel)).toThrow()
    // BLOCKED requires an unresolved critical item; a non-blocked state may not hide one.
    expect(() => decodeChecklist({ ...row, readiness: { ...row.readiness, criticalUnresolved: 0 } }, surveyVessel)).toThrow()
    expect(() => decodeChecklist({ ...row, readiness: { ...row.readiness, state: 'PREPARED_PENDING_HUMAN_CONFIRMATION' } }, surveyVessel)).toThrow()
    expect(() => decodeChecklist({ ...row, items: [surveyItem('X_ITEM', { verificationState: 'VERIFIED' })] }, surveyVessel)).toThrow()
    expect(() => decodeChecklist({ ...row, items: [surveyItem('X_ITEM', { answer: 'NOT_APPLICABLE' })] }, surveyVessel)).toThrow()
    expect(() => decodeChecklist({ ...row, templateStatus: 'APPROVED' }, surveyVessel)).toThrow()
    expect(() => decodeChecklist({ ...row, voyageReference: 'VOY-1' }, surveyVessel)).toThrow()
    expect(() => decodeChecklist(row, '99999999-9999-4999-8999-999999999999')).toThrow()
  })
  it('requires the port-call reference to match the plan pin', () => {
    const row = checklistFixture({ plan: { plannedStart: null, plannedEnd: null, portCallId: surveyCertificateId, portCallRevision: 2, locationText: null } })
    expect(() => decodeChecklist(row, surveyVessel)).toThrow()
    const linked = { ...row, portCall: { id: surveyCertificateId, revision: 2, reference: 'CURRENT', from: '2026-12-01', to: '2026-12-02', qualification: 'PORT_CALL_DRAFT_UNVERIFIED_PORT_IDENTITY_UNRESOLVED' } }
    expect(decodeChecklist(linked, surveyVessel).portCall?.reference).toBe('CURRENT')
    expect(() => decodeChecklist({ ...linked, portCall: { ...linked.portCall, revision: 3 } }, surveyVessel)).toThrow()
  })
  it('keeps window and fit closed', () => {
    expect(() => decodeWindow({ state: 'OPEN', start: null, end: null, daysToOpen: null, daysToClose: null, daysOverdue: null, qualifiers: [] })).toThrow()
    expect(() => decodeFit({ state: 'CONDITIONAL', reasons: [], basis: [] })).toThrow()
    expect(decodeFit({ state: 'NOT_FIT', reasons: ['WINDOW_CLOSED'], basis: [] }).state).toBe('NOT_FIT')
  })
})

describe('DEV-112 planner contract', () => {
  it('accepts the advisory calendar and rejects FIT rows or a simulated coordination', () => {
    expect(decodePlanner(plannerFixture()).items).toHaveLength(1)
    const p = plannerFixture()
    expect(() => decodePlanner({ ...p, items: [{ ...p.items[0], fit: { ...p.items[0].fit, state: 'FIT' } }] })).toThrow()
    expect(() => decodePlanner({ ...p, items: [{ ...p.items[0], coordination: 'CONFIRMED_WITH_CLASS' }] })).toThrow()
    expect(() => decodePlanner({ ...p, fitStates: ['FIT', 'CONDITIONAL', 'NOT_FIT', 'UNKNOWN'] })).toThrow()
    expect(() => decodePlanner({ ...p, qualification: 'AUTHORITY_SCHEDULE' })).toThrow()
  })
})

describe('DEV-104 batch verification refusal', () => {
  afterEach(() => vi.restoreAllMocks())
  it('surfaces every failing item from the 422 body instead of hiding one behind another', async () => {
    const items = [{ itemId: surveyCertificateId, code: 'separation_of_duties' }, { itemId: surveyVessel, code: 'evidence_required' }]
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ code: 'verification_rejected', items }), { status: 422, headers: { 'Content-Type': 'application/json' } }))
    const error = await surveyApi.verify({ key: surveyCertificateId, id: surveyCertificateId, vessel: surveyVessel, body: '{}' }, 'csrf', new AbortController().signal).catch(e => e)
    expect(error).toBeInstanceOf(VerificationRejected)
    expect((error as VerificationRejected).items).toEqual(items)
  })
})
