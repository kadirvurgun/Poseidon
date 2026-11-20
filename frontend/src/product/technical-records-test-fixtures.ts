// FICTIONAL synthetic wire shapes for DEV-055/DEV-056/DEV-103 component tests only.
export const techVessel = '00000000-0000-4000-8000-0000000055a1'
export const techDefect = '00000000-0000-4000-8000-0000000055d1'
export const techReport = '00000000-0000-4000-8000-0000000055e1'
export const techActor = '00000000-0000-4000-8000-0000000055f1'
export const techTenant = '00000000-0000-4000-8000-0000000055f2'
const hash = 'a'.repeat(64)

export const lifecycleFixture = (patch: Record<string, unknown> = {}) => ({
  defectId: techDefect, vesselId: techVessel, started: true, revision: 2, stage: 'ASSESS', assessedCriticality: 'Critical', declaredCriticality: 'Critical', critical: true,
  defectRevision: 1, records: [
    { id: '00000000-0000-4000-8000-000000005501', sequence: 1, kind: 'START', stageAfter: 'DETECT', defectRevision: 1, criticality: null, measureKind: null, temporary: false,
      expiresAt: null, measureId: null, outcome: null, text: '', actorId: techActor, recordedAt: '2026-11-20T08:00:00Z' },
    { id: '00000000-0000-4000-8000-000000005502', sequence: 2, kind: 'ASSESS', stageAfter: 'ASSESS', defectRevision: 1, criticality: 'Critical', measureKind: null, temporary: false,
      expiresAt: null, measureId: null, outcome: null, text: 'FICTIONAL propulsion risk', actorId: techActor, recordedAt: '2026-11-20T08:05:00Z' }],
  measures: [], alerts: [{ code: 'CRITICAL_DEFECT_WITHOUT_ACTIVE_SAFEGUARD', blocking: true, measureId: null, at: null }],
  closeBlockers: ['CRITICAL_SAFEGUARD_NOT_RECORDED', 'CORRECTIVE_ACTION_NOT_RECORDED'], actionOwners: [], closedBy: null, closedAt: null, assessedAt: '2026-11-20T09:00:00Z',
  can: { start: false, assess: true, safeguard: true, lift: false, action: false, verify: false, effectiveness: false, close: false }, ...patch,
})

export const serviceFixture = (patch: Record<string, unknown> = {}) => ({
  id: techReport, vesselId: techVessel, reportNo: 'FICTIONAL-SR-1', revision: 2, version: 7, state: 'SUBMITTED',
  content: { serviceProvider: 'FICTIONAL Marine Services', engineerName: 'FICTIONAL Engineer', serviceDate: '2026-11-01', reasonScope: 'FICTIONAL scope', workPerformed: 'FICTIONAL work',
    parts: [], measurements: [], defectsFound: null, recommendations: null, nextActionDue: null },
  equipment: { kind: 'Equipment', id: '00000000-0000-4000-8000-0000000055b1', revision: 1, currency: 'CURRENT' }, workOrder: null, defects: [], evidence: [],
  missing: [], hash, verificationState: 'PENDING_VERIFICATION', verifiedRevision: null, verifications: [], revisions: [], authors: [techActor],
  canEdit: false, canSubmit: false, canVerify: true, canAmend: false, provenance: 'UserDeclaredUnverified', savedAt: '2026-11-20T08:00:00Z', ...patch,
})

const unknown = (key: string, reason: string, state = 'UNKNOWN') => ({ key, state, count: null, quietCount: 0, items: [], asOf: null, freshness: 'UNKNOWN', reason })
export const centerFixture = (patch: Record<string, unknown> = {}) => ({
  vesselId: techVessel, readinessState: 'BLOCKED', lastRefreshAt: '2026-11-20T09:00:00Z', advisory: 'ADVISORY_EXCEPTION_VIEW_NOT_A_FITNESS_DECISION',
  sections: [
    { key: 'critical_overdue_jobs', state: 'QUIET', count: 0, quietCount: 3, items: [], asOf: '2026-11-20T09:00:00Z', freshness: 'CURRENT', reason: null },
    { key: 'overdue_jobs', state: 'EXCEPTIONS', count: 1, quietCount: 0, asOf: '2026-11-20T09:00:00Z', freshness: 'STALE', reason: null, items: [
      { kind: 'MaintenancePlan', sourceId: '00000000-0000-4000-8000-0000000055c1', sourceRevision: 2, severity: 'REVIEW', why: [{ code: 'MAINTENANCE_REVIEW_REQUIRED', detail: 'ReadingFreshnessPolicyUnresolved' }],
        observedAt: '2026-11-10T09:00:00Z', freshness: 'STALE', assetId: null }] },
    unknown('open_work_orders', 'WORK_ORDER_STATE_SOURCE_UNAVAILABLE'),
    { key: 'condition_alerts', state: 'EXCEPTIONS', count: 1, quietCount: 2, asOf: '2026-11-20T09:00:00Z', freshness: 'CURRENT', reason: null, items: [
      { kind: 'Defect', sourceId: techDefect, sourceRevision: 1, severity: 'CRITICAL', why: [{ code: 'DEFECT_LIFECYCLE_NOT_STARTED', detail: null }, { code: 'DECLARED_CRITICAL', detail: null }],
        observedAt: '2026-11-20T08:00:00Z', freshness: 'CURRENT', assetId: null }] },
    unknown('critical_spare_gaps', 'SPARE_GAP_SOURCE_UNAVAILABLE')],
  ...patch,
})
