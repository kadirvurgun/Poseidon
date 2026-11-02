import { render, screen } from '@testing-library/react'
import { expect, it } from 'vitest'
import { TugOwningEvidence } from './TugOwningEvidence'
import type { PlanningEvidence } from './tug-planning-api'

it('shows current source/condition lineage and expiry separately from Master safety authority', () => {
  const value: PlanningEvidence = { availableBollardPull: '66.294', capacityState: 'CurrentPlanningCapacity', readiness: 'ReviewRequired', conflictState: 'Unknown', bp: [], conflicts: [], coverage: 'CurrentAuthorizedOwningFacts;DraftGeometry;MasterDecisionSeparate', ownedBp: [{ tugId: 'fictional-tug', state: 'CurrentPlanningCapacity', tonnes: '66.294', why: 'QualifiedMeasuredConditionFormula;MasterDecisionSeparate', evidenceId: 'fictional-evidence', evidenceRevision: 3, conditionId: 'fictional-condition', conditionRevision: 2, ruleId: 'fictional-rule', qualificationRevision: 4, qualificationHash: 'A'.repeat(64), validUntil: '2026-11-02T13:00:00Z', observedAt: '2026-11-01T12:00:00Z' }], readinessFacts: [{ tugId: 'fictional-tug', source: 'CrewCertificate', id: 'fictional-credential', revision: 2, state: 'Blocked', why: 'DeclaredCertificateExpiresBeforeWindowEnd', observedAt: null, expiresAt: null }] }
  const { rerender } = render(<TugOwningEvidence value={value} language="en" />)
  expect(screen.getByText(/66.294 t/)).toBeVisible(); expect(screen.getByText(/Condition revision.*2/)).toBeVisible()
  expect(screen.getByText(/Master safety authority remains separate/)).toBeVisible()
  expect(screen.getByText(/Blocked.*CrewCertificate/)).toBeVisible()
  rerender(<TugOwningEvidence value={null} language="tr" />)
  expect(screen.queryByText(/66.294/)).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Güncel kanıt kullanılamıyor')
})
