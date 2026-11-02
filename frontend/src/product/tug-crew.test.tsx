import { render, screen, within } from '@testing-library/react'
import { expect, it } from 'vitest'
import { TugCrewCoverage } from './TugCrewCoverage'
import { decodeTugCrewCoverage } from './tug-crew-contract'
import { decodePlanningEvidence } from './tug-planning-api'

const tug = '10000000-0000-0000-0000-000000000001'
const position = '20000000-0000-0000-0000-000000000001'
const document = '30000000-0000-0000-0000-000000000001'
const role = { positionId: position, required: 1, declared: 1, qualified: 1, unknown: 0, state: 'Current', reason: 'SourceQualifiedRoleCountMet' }
const coverage = { tugId: tug, state: 'CurrentMinimumRoleCoverage', reason: 'SourceQualifiedMinimumRolesOnly',
  windowStart: '2026-11-02T10:05:00Z', windowEnd: '2026-11-02T10:10:00Z', assessedAt: '2026-11-02T10:00:00Z',
  msmdId: document, msmdRevision: 2, roles: [role], validUntil: '2026-11-02T11:00:00Z',
  coverage: 'SourceQualifiedMinimumRolesOnly;OtherCrewGatesAndMasterDecisionSeparate' }
const unavailable = { ...coverage, state: 'Unavailable', reason: 'CurrentProtectedCrewSourceAccessRequired', msmdId: null, msmdRevision: null, roles: [], validUntil: null }
const evidence = { availableBollardPull: null, capacityState: 'ReviewRequired', bp: [], conflictState: 'Unknown', conflicts: [],
  coverage: 'CurrentAuthorizedOwningFacts;DraftGeometry;MasterDecisionSeparate', readiness: 'ReviewRequired', ownedBp: [], operationalConflicts: [],
  crew: [coverage], readinessFacts: [{ tugId: tug, source: 'CrewMinimumManning', id: document, revision: 2, state: 'Current', why: coverage.reason,
    observedAt: null, expiresAt: coverage.validUntil }] }

it('shows source-qualified role counts in TR/EN while keeping the other crew and Master gates explicit', () => {
  const view = render(<TugCrewCoverage items={decodeTugCrewCoverage([coverage], [tug])} language="tr" />)
  expect(screen.getByText(/Nitelendirilmiş asgari rol sayıları karşılanıyor/)).toBeVisible()
  expect(screen.getByText(/Sağlık uygunluğu, çalışma\/dinlenme, iş brifingi ve kaptan kararı ayrıdır/)).toBeVisible()
  view.rerender(<TugCrewCoverage items={[coverage]} language="en" />)
  expect(screen.getByText(/Qualified minimum role counts met/)).toBeVisible()
  const table = screen.getByRole('table')
  expect(within(table).getByRole('columnheader', { name: 'Source-qualified' })).toBeVisible()
  expect(within(table).getByRole('rowheader', { name: position })).toBeVisible()
  expect(screen.getByText(`Issued MSMD reference / revision: ${document} / 2`)).toBeVisible()
})

it('withdraws every previous private reference and count when the current source response is unavailable', () => {
  const view = render(<TugCrewCoverage items={[coverage]} language="en" />)
  expect(screen.getByRole('table')).toBeVisible()
  view.rerender(<TugCrewCoverage items={[unavailable]} language="en" />)
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
  expect(screen.queryByText(position)).not.toBeInTheDocument()
  expect(screen.queryByText(new RegExp(document))).not.toBeInTheDocument()
  expect(screen.getAllByText(/Current protected crew\/source access is required/).length).toBeGreaterThan(0)
})

it('shows human allocation review instead of treating two occupied roles as two different people', () => {
  const second = { ...role, positionId: document }
  const current = { ...coverage, state: 'ReviewRequired', reason: 'CrossRoleAllocationReviewRequired', roles: [role, second] }
  render(<TugCrewCoverage items={decodeTugCrewCoverage([current], [tug])} language="en" />)
  expect(screen.getByText('A person occurs in more than one required role; human allocation review is required.')).toBeVisible()
  expect(screen.queryByText(/Qualified minimum role counts met/)).not.toBeInTheDocument()
})

it('binds each source fact to its exact protected crew summary and never changes full readiness to Ready', () => {
  expect(decodePlanningEvidence(evidence, [tug]).crew).toEqual([coverage])
  expect(() => decodePlanningEvidence({ ...evidence, readiness: 'Ready' }, [tug])).toThrow()
  expect(() => decodePlanningEvidence({ ...evidence, crew: [unavailable] }, [tug])).toThrow()
  expect(() => decodePlanningEvidence({ ...evidence, readinessFacts: [] }, [tug])).toThrow()
  expect(() => decodePlanningEvidence({ ...evidence, crew: undefined }, [tug])).toThrow()
})

it.each([
  { ...coverage, state: 'Ready' }, { ...coverage, validUntil: coverage.windowEnd },
  { ...coverage, msmdRevision: Number.MAX_SAFE_INTEGER + 1 }, { ...coverage, crewNames: ['private'] },
  { ...coverage, roles: [role, role] }, { ...coverage, roles: [{ ...role, required: 2 }] },
  { ...coverage, roles: [{ ...role, qualified: '1' }] }, { ...coverage, roles: [{ ...role, unknown: 1 }] },
  { ...coverage, roles: [{ ...role, declared: 0 }] }, { ...coverage, windowEnd: coverage.windowStart },
  { ...unavailable, roles: [role] }, { ...unavailable, msmdId: document },
  { ...coverage, roles: [] }, { ...coverage, tugId: position }
])('rejects contradictory, forged or private crew coverage %#', candidate => {
  expect(() => decodeTugCrewCoverage([candidate], [tug])).toThrow()
})

it('accepts an explicitly unknown role qualification without treating it as a false or successful qualification', () => {
  const unknown = { ...coverage, state: 'ReviewRequired', reason: 'CurrentRoleQualificationIncomplete',
    roles: [{ ...role, qualified: 0, unknown: 1, state: 'ReviewRequired', reason: 'CurrentRoleQualificationIncomplete' }] }
  expect(decodeTugCrewCoverage([unknown], [tug])).toEqual([unknown])
  expect(() => decodeTugCrewCoverage([{ ...unknown, state: 'Blocked' }], [tug])).toThrow()
})
