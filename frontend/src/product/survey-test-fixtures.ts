import type { PreSurveyCapabilities, PreSurveyChecklist, PreSurveyItem } from './survey-api'

// Fictional, unverified fixtures for DEV-104/DEV-112 component tests only.
export const surveyVessel = '11111111-1111-4111-8111-111111111111'
export const surveyActor = '22222222-2222-4222-8222-222222222222'
export const surveyChecklistId = '33333333-3333-4333-8333-333333333333'
export const surveyCertificateId = '44444444-4444-4444-8444-444444444444'
export const surveyOwnerRole = '55555555-5555-4555-8555-555555555555'
const hash = (c: string) => c.repeat(64)
export const surveyItem = (code: string, patch: Partial<PreSurveyItem> = {}): PreSurveyItem => ({
  itemId: `6666666${code.length % 10}-6666-4666-8666-${String(code.length).padStart(12, '0')}`, itemCode: code, critical: true, required: true, evidenceRequired: true, allowNotApplicable: false,
  ownerRoleId: null, dueDate: null, answer: 'OPEN', evidence: [], finding: null, comment: null, itemHash: hash('a'), verificationState: 'PENDING', verifiedAt: null, verificationReason: null,
  answeredByCurrentActor: false, verifiedByCurrentActor: false, blockers: ['ITEM_NOT_ANSWERED', 'OWNER_UNASSIGNED', 'DUE_DATE_UNKNOWN'], ...patch,
})
export const checklistFixture = (patch: Partial<PreSurveyChecklist> = {}): PreSurveyChecklist => ({
  id: surveyChecklistId, vesselId: surveyVessel, certificateId: surveyCertificateId, certificateRevision: 1, certificateVersion: 7, revision: 2, version: 41,
  templateCode: 'STARTER-ANNUAL', templateRevision: '1', templateStatus: 'STARTER_UNAPPROVED', surveyType: 'ANNUAL',
  plan: { plannedStart: null, plannedEnd: null, portCallId: null, portCallRevision: null, locationText: null },
  items: [surveyItem('CERTIFICATE_ORIGINALS_AVAILABLE')],
  readiness: { state: 'BLOCKED', qualification: 'ADVISORY_NOT_SURVEY_APPROVAL', blockers: ['CRITICAL_ITEM_UNRESOLVED', 'TEMPLATE_NOT_APPROVED'], criticalUnresolved: 1, requiredUnresolved: 0, verified: 0, total: 1 },
  window: { state: 'UNKNOWN', start: null, end: null, daysToOpen: null, daysToClose: null, daysOverdue: null, qualifiers: ['WINDOW_NOT_DECLARED'] },
  voyageFit: { state: 'UNKNOWN', reasons: ['WINDOW_UNKNOWN', 'PLANNED_DATES_MISSING'], basis: ['VOYAGE_REFERENCE_UNAVAILABLE'] },
  portCall: null, transition: 'UPDATE', sourceVersion: 'synthetic', hash: hash('b'), savedAt: '2026-11-20T08:00:00Z', provenance: 'UserDeclaredUnverified',
  currentRevision: true, canEdit: true, canVerify: false, qualification: 'ADVISORY_NOT_SURVEY_APPROVAL', voyageReference: 'UNKNOWN_NO_VOYAGE_MODULE', ...patch,
})
export const surveyCapsFixture = (patch: Partial<PreSurveyCapabilities> = {}): PreSurveyCapabilities => ({
  vesselId: surveyVessel, canWrite: true, canVerify: false, ownerOptions: [{ roleId: surveyOwnerRole }],
  templates: [{ code: 'STARTER-ANNUAL', revision: '1', surveyType: 'ANNUAL', status: 'STARTER_UNAPPROVED', items: [{ itemCode: 'CERTIFICATE_ORIGINALS_AVAILABLE', critical: true, required: true, evidenceRequired: true, allowNotApplicable: false }] }],
  canReadPortCalls: false, sourceVersion: 'synthetic', answers: ['OPEN', 'READY', 'ATTENTION', 'BLOCKED', 'NOT_APPLICABLE'],
  rejectReasons: ['EVIDENCE_INSUFFICIENT', 'ANSWER_NOT_SUPPORTED', 'NOT_APPLICABLE_NOT_JUSTIFIED', 'OTHER_SEE_COMMENT'], qualification: 'ADVISORY_NOT_SURVEY_APPROVAL', ...patch,
})
