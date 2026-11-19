import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { cleanup, render, screen } from '@testing-library/react'
import { CrewCompetencePanel, CrewComplianceWorkspace } from './CrewComplianceWorkspace'
import { credentialTone, decodeEligibility, decodeRegister, decodeSafeManning, eligibilityTone } from './crew-compliance-api'

// S7 WP-A DEV-048/049/050 UI and decoder contract. Unknown/unverified never green or positive.
const vessel = '11111111-1111-4111-8111-111111111111', assignment = '22222222-2222-4222-8222-222222222222'
const credential = '33333333-3333-4333-8333-333333333333', position = '44444444-4444-4444-8444-444444444444'
const response = (v: unknown, status = 200) => new Response(JSON.stringify(v), { status, headers: { 'Content-Type': 'application/json' } })
const reason = { code: 'WORK_REST_SOURCE_UNAVAILABLE', dimension: 'WORK_REST', severity: 'REVIEW_REQUIRED', requirementId: null, sourceId: null }
const member = (state = 'REVIEW_REQUIRED', reasons = [reason]) => ({ assignmentId: assignment, positionId: position, state, reasons, gaps: [], qualifications: ['ADVISORY_ONLY'], authority: 'ADVISORY_ONLY_NOT_A_DECISION' })
const manning = (overrides: Record<string, unknown> = {}) => ({
  evaluation: { vesselId: vessel, state: 'REVIEW_REQUIRED', reasons: [{ code: 'MSMD_SOURCE_QUALIFIED_MSMD_REQUIRED', dimension: 'MSMD', severity: 'REVIEW_REQUIRED' }], msmdState: 'SOURCE_QUALIFIED_MSMD_REQUIRED',
    msmdId: null, msmdRevision: null, roles: [], members: [member()], window: { from: '2026-12-01', to: '2026-12-01' }, evaluatedAt: '2026-12-01T00:00:00Z',
    hash: 'a'.repeat(64), qualifications: ['ADVISORY_ONLY'], authority: 'ADVISORY_ONLY_NOT_A_DECISION', ...overrides },
  authorities: [], canDecide: false, sequence: 0, latestDecision: null, decisions: ['MANNING_ACCEPTED', 'MANNING_NOT_ACCEPTED'],
})
beforeEach(async () => { await changeAppLanguage('en', false) })
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

describe('crew compliance decoders', () => {
  it('refuses an ELIGIBLE result that still carries a blocking reason', () => {
    expect(() => decodeEligibility(member('ELIGIBLE'))).toThrow()
    expect(decodeEligibility(member('ELIGIBLE', [])).state).toBe('ELIGIBLE')
    expect(() => decodeEligibility({ ...member(), state: 'GO' })).toThrow()
    expect(() => decodeEligibility({ ...member(), authority: 'DECISION' })).toThrow()
  })
  it('refuses a vessel ELIGIBLE without a current MSMD and a malformed hash', () => {
    expect(() => decodeSafeManning(manning({ state: 'ELIGIBLE', reasons: [] }))).toThrow()
    expect(() => decodeSafeManning(manning({ hash: 'x' }))).toThrow()
    expect(decodeSafeManning(manning()).evaluation.state).toBe('REVIEW_REQUIRED')
  })
  it('refuses VERIFIED_CURRENT without a current human Verified assertion', () => {
    const row = { credentialId: credential, assignmentId: assignment, positionId: null, certificateTypeReference: 'FICTIONAL-COC', expiryDate: '2030-01-01',
      endorsementRequired: false, endorsementExpiry: null, revision: 1, assertionState: null, assertionCurrent: false, status: 'VERIFIED_CURRENT' }
    expect(() => decodeRegister({ vesselId: vessel, referenceDate: '2026-12-01', items: [row], truncated: false, qualifications: [] })).toThrow()
    expect(decodeRegister({ vesselId: vessel, referenceDate: '2026-12-01', items: [{ ...row, status: 'UNVERIFIED' }], truncated: false, qualifications: [] }).items).toHaveLength(1)
  })
  it('never maps any state to a success (green) tone', () => {
    for (const s of ['ELIGIBLE', 'ATTENTION', 'NOT_ELIGIBLE', 'REVIEW_REQUIRED']) expect(['info', 'warning', 'danger']).toContain(eligibilityTone(s))
    expect(credentialTone('UNVERIFIED')).toBe('warning')
    expect(credentialTone('EXPIRED')).toBe('danger')
  })
})

describe('crew compliance screens', () => {
  it('shows Safe Manning as advisory review-required and offers no decision without an appointment', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(response(String(url).includes('auth/session')
      ? { authenticated: true, actorId: assignment, tenantId: vessel, csrfToken: 'csrf' } : manning()))))
    render(<CrewComplianceWorkspace view="safe-manning" vessel={vessel}/>)
    expect((await screen.findAllByText('Review required')).length).toBeGreaterThan(0)
    expect(screen.getByText(/never a GO or release/)).toBeVisible()
    expect(screen.getByText('You have no current appointment for this action on this vessel.')).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Record Safe Manning decision' })).not.toBeInTheDocument()
  })
  it('shows an unverified credential as unverified in the register', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(response(String(url).includes('auth/session')
      ? { authenticated: true, actorId: assignment, tenantId: vessel, csrfToken: 'csrf' }
      : { vesselId: vessel, referenceDate: '2026-12-01', truncated: false, qualifications: [], items: [{ credentialId: credential, assignmentId: assignment, positionId: null,
        certificateTypeReference: 'FICTIONAL-COC', expiryDate: '2030-01-01', endorsementRequired: false, endorsementExpiry: null, revision: 1, assertionState: null, assertionCurrent: false, status: 'UNVERIFIED' }] }))))
    render(<CrewComplianceWorkspace view="certificates" vessel={vessel}/>)
    expect(await screen.findByText('Unverified')).toBeVisible()
    expect(screen.getByText('FICTIONAL-COC')).toBeVisible()
  })
  it('lists competence gaps with the training-is-not-competence reason', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(response({ vesselId: vessel, assignmentId: assignment, positionId: position, window: { from: '2026-12-01', to: '2026-12-01' }, records: [],
      evaluation: { ...member('NOT_ELIGIBLE', [{ code: 'TRAINING_COMPLETION_IS_NOT_COMPETENCE', dimension: 'COMPETENCE', severity: 'NOT_ELIGIBLE', requirementId: null, sourceId: null }]),
        gaps: [{ requirementId: credential, type: 'COMPETENCE', mandatory: true, state: 'GAP', reason: 'TRAINING_COMPLETION_IS_NOT_COMPETENCE', satisfiedBy: null }] },
      authorities: [], canWrite: false, canSignOff: false, kinds: [], qualifications: [] }))))
    render(<CrewCompetencePanel vessel={vessel} assignment={assignment} language="en"/>)
    expect((await screen.findAllByText('Not eligible')).length).toBeGreaterThan(0)
    expect(screen.getAllByText('TRAINING_COMPLETION_IS_NOT_COMPETENCE').length).toBeGreaterThan(0)
  })
  it('shows a denied state instead of data on 403', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(response({ code: 'permission_denied' }, 403))))
    render(<CrewCompetencePanel vessel={vessel} assignment={assignment} language="en"/>)
    expect(await screen.findByText('Not available for your current access.')).toBeVisible()
  })
})
