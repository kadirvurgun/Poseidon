// USER-GUIDE-01 UG-1 coverage allowlist: navigation keys whose guide is not written yet (UG-3).
// Shrink-only. help-coverage.test.ts fails when a listed key gains content (remove it here) and
// when the list grows past its UG-1 baseline. A new navigation destination therefore needs its
// TR + EN guide in the same change; it may not be parked here.
export const pendingHelpKeys: ReadonlySet<string> = new Set([
  'audit',
  'audit-ready',
  'certificate-command-center',
  'high-risk-work',
  'industry-assurance',
  'pilot-gate',
  'pilot-program',
  'pre-survey-checklist',
  'regulatory-review',
  'renewal-workflow',
  'requirements',
  'risk-assessment',
  'risk-template-library',
  'role-uat',
  'safety-events',
  'sources',
  'survey-planner',
  'voice-event-draft',
])

/** UG-1 baseline size: 53 destinations minus today, tug-job-board and companies. Only ever lowered. */
export const pendingHelpBaseline = 50
