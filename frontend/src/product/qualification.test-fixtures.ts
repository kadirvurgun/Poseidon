import { blank } from './qualification-api'
import { reviewAnchor } from './human-review.test-fixtures'
export const qualifiedFixture = () => ({ ruleId: reviewAnchor.controlId, revision: 3, version: 29, ruleRevision: 1, state: 'IN_FORCE',
  content: { ...blank(), publisher: 'FICTIONAL publisher', edition: '1', ownerId: reviewAnchor.controlId, ownerRole: reviewAnchor.controlId, validationReview: 'FICTIONAL software review', inputs: [],
    control: { ownerRole: reviewAnchor.controlId, frequency: 'FICTIONAL frequency', evidencePolicy: 'FICTIONAL evidence', verificationPolicy: 'FICTIONAL verification', maxAgeHours: 24, requireFile: false, requireAcknowledgment: false, acknowledgmentRole: null, requiredVerificationMechanism: null, verificationMechanismSourceLocator: null, sourceLocator: 'synthetic/only' } },
  sourceHash: 'A'.repeat(64), hash: 'B'.repeat(64), priorHash: 'C'.repeat(64), policyVersion: 'recorded-qualification-v1', actorId: reviewAnchor.controlId, roleId: reviewAnchor.controlId, authorityId: reviewAnchor.controlId, authorityHash: 'D'.repeat(64),
  contract: { sourceLocator: 'synthetic/source-contract', type: 'FICTIONAL_DECLARATION', requiredMechanism: null }, savedAt: '2026-11-02T00:00:00Z', blockers: [] })
