// Synthetic transport only: not provisioned appointments, signed safety decisions or UAT.
import { dailyActor, dailyId, dailyVessel } from './daily-test-fixtures'
import { dailySourceVersion } from './daily-operations-api'
import type { DailyKind } from './daily-report-fields'
import type { DailyReview, DailyReviewCapabilities, DailyReviewDuty } from './daily-review-api'
export const reviewAuthorityId = '10000000-0000-4000-8000-000000000004', reviewRoleId = '10000000-0000-4000-8000-000000000005'
const duty = (kind: DailyKind): DailyReviewDuty => kind === 'Noon' ? 'Master' : kind === 'Deck' ? 'ChiefOfficer' : 'ChiefEngineer'
export const dailyReviewCapsFixture = (kind: DailyKind, configured = false): DailyReviewCapabilities => ({ recordId: dailyId, vesselId: dailyVessel, kind, recordRevision: 1, recordVersion: 17, reviewVersion: 0, canRecord: configured, authorities: configured ? [{ id: reviewAuthorityId, roleId: reviewRoleId, duty: duty(kind), decisions: kind === 'Noon' ? ['REVIEWED'] : ['REVIEWED', 'RETURNED'] }] : [], blockers: configured ? [] : ['HUMAN_REVIEW_AUTHORITY_UNAVAILABLE'], sourceVersion: dailySourceVersion })
export const dailyReviewFixture = (kind: DailyKind): DailyReview => ({ id: '10000000-0000-4000-8000-000000000006', recordId: dailyId, vesselId: dailyVessel, kind, recordRevision: 1, sequence: 1, version: 23, actorId: dailyActor, roleId: reviewRoleId, authorityId: reviewAuthorityId, duty: duty(kind), decision: 'REVIEWED', reasonCode: 'REVIEW_RECORDED', rationale: 'Synthetic recorded human rationale', sourceVersion: dailySourceVersion, authorityHash: 'B'.repeat(64), recordHash: 'A'.repeat(64), savedAt: '2026-11-05T00:10:00Z', state: 'RECORDED_UNASSESSED' })
