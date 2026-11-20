// FICTIONAL synthetic wire shapes for DEV-062/DEV-063/DEV-114 component tests only.
export const s9Vessel = '00000000-0000-4000-8000-0000000062a1'
export const s9Draft = '00000000-0000-4000-8000-0000000062d1'
export const s9Document = '00000000-0000-4000-8000-0000000114d1'
export const s9Actor = '00000000-0000-4000-8000-0000000062f1'
export const s9Tenant = '00000000-0000-4000-8000-0000000062f2'
const hash = 'a'.repeat(64)
const note = 'FICTIONAL near miss on the main deck at 09:15. We cordoned off the area.'

export const voiceCapsFixture = (patch: Record<string, unknown> = {}) => ({
  vesselId: s9Vessel, canRead: true, canWrite: true, canConfirm: true, speechAvailable: false, speechUnavailableReason: 'LOCAL_ENGINE_NOT_CONFIGURED', speechEngineVersion: null,
  maxAudioBytes: 2097152, audioRetention: 'NOT_RETAINED', extractorVersion: 'voice-rule-extractor/1.0.0', ruleSetVersion: 'voice-event-rules-tr-en/2026-11-20',
  eventTypes: ['NEAR_MISS', 'INCIDENT', 'HAZARDOUS_SITUATION', 'NON_CONFORMITY'],
  locations: ['ENGINE_ROOM', 'BRIDGE', 'MAIN_DECK', 'FORECASTLE', 'AFT_DECK', 'CARGO_HOLD', 'PUMP_ROOM', 'GALLEY', 'ACCOMMODATION', 'GANGWAY', 'MOORING_STATION', 'STEERING_GEAR_ROOM', 'TOWING_DECK', 'OTHER'],
  rejectionReasons: ['TRANSCRIPT_WRONG', 'FIELDS_WRONG', 'NOT_AN_EVENT', 'DUPLICATE', 'OTHER'],
  requiredFields: ['eventType', 'occurredAt', 'location', 'description', 'immediateAction'], canAttachEvidence: false, ...patch,
})
export const voiceMetricsFixture = () => ({ vesselId: s9Vessel, state: 'OPERATIONAL_OBSERVATION_NOT_ACCURACY_CLAIM', confirmed: 0, rejected: 1, open: 1,
  fieldCompleteness: null, correctionRate: null, usability: null, rejectionRate: 1, rejectionsByReason: { NOT_AN_EVENT: 1 }, sampleLimit: 1000, extractorVersion: 'voice-rule-extractor/1.0.0' })
export const voiceDraftFixture = (patch: Record<string, unknown> = {}) => ({
  id: s9Draft, vesselId: s9Vessel, revision: 1, version: 11, state: 'DRAFT', origin: 'TYPED', transcript: note, transcriptSha256: hash, referenceDate: '2026-11-19', speech: null,
  extractorVersion: 'voice-rule-extractor/1.0.0', ruleSetVersion: 'voice-event-rules-tr-en/2026-11-20',
  candidate: { protocol: 'POSEIDON_VOICE_EVENT_DRAFT_V1', extractorVersion: 'voice-rule-extractor/1.0.0', ruleSetVersion: 'voice-event-rules-tr-en/2026-11-20', transcriptSha256: hash,
    fields: [
      { key: 'eventType', value: 'NEAR_MISS', spans: [{ start: 10, end: 19 }], confidence: 0.9, rule: 'TYPE_EXPLICIT' },
      { key: 'location', value: 'MAIN_DECK', spans: [{ start: 27, end: 36 }], confidence: 0.85, rule: 'LOCATION_LEXICON' },
      { key: 'description', value: note, spans: [{ start: 0, end: note.length }], confidence: 1, rule: 'VERBATIM_TRANSCRIPT' },
      { key: 'immediateAction', value: 'We cordoned off the area.', spans: [{ start: 47, end: 71 }], confidence: 0.7, rule: 'ACTION_VERB_SENTENCE' }],
    hints: [{ code: 'TIME_WITHOUT_DATE', span: { start: 40, end: 45 } }] },
  fields: { eventType: 'NEAR_MISS', occurredAt: null, location: 'MAIN_DECK', locationDetail: null, description: note, immediateAction: 'We cordoned off the area.' },
  fieldOrigins: { eventType: 'EXTRACTED', occurredAt: 'NONE', location: 'EXTRACTED', locationDetail: 'NONE', description: 'EXTRACTED', immediateAction: 'EXTRACTED' },
  evidence: [], missing: ['occurredAt'], hash, corrections: null, confirmedBy: null, confirmedAt: null, rejection: null, revisions: [], createdBy: s9Actor,
  canEdit: true, canConfirm: false, handoff: 'NOT_READY', safetyEventId: null, handoffReason: null, audioRetention: 'NOT_RETAINED', provenance: 'UserDeclaredUnverified', savedAt: '2026-11-20T08:00:00Z', ...patch,
})
export const voicePageFixture = () => ({ items: [{ id: s9Draft, state: 'DRAFT', origin: 'TYPED', eventType: 'NEAR_MISS', location: 'MAIN_DECK', occurredAt: null, missingCount: 1,
  revision: 1, savedAt: '2026-11-20T08:00:00Z' }], offset: 0, limit: 25, hasMore: false, canWrite: true })

const revision = (n: number, patch: Record<string, unknown> = {}) => ({ revision: n, fileId: `00000000-0000-4000-8000-00000001140${n}`, contentSha256: String(n).repeat(64),
  contentLength: 1000 + n, mime: 'image/png', intakeState: 'RELEASED', publicationState: n === 2 ? 'EFFECTIVE' : 'SUPERSEDED', verificationState: 'UNVERIFIED',
  uploadedBy: s9Actor, registeredBy: s9Actor, registeredAt: `2026-11-1${n}T08:00:00Z`, releasedAt: `2026-11-1${n}T09:00:00Z`, publishedBy: s9Tenant,
  publishedAt: `2026-11-1${n}T10:00:00Z`, verifications: [], acknowledgedByMeAt: null, canAcknowledge: n === 2, ...patch })
export const documentFixture = (patch: Record<string, unknown> = {}) => ({
  id: s9Document, vesselId: s9Vessel, code: 'FICTIONAL-SSP', title: 'FICTIONAL security plan', classification: 'RESTRICTED', currentRevision: 2, latestRevision: 2, version: 21,
  holdActive: false, holdPlacedAt: null, retentionState: 'UNCONFIGURED', retentionPolicyRef: null, retainUntil: null, disposalState: 'NONE', physicalDeletion: 'NOT_STARTED',
  provenance: 'DeclaredUnverified', revisions: [revision(1), revision(2)],
  custody: [{ position: 1, revision: 1, eventType: 'PUBLISHED', source: 'COMMAND', subjectActorId: null, occurredAt: '2026-11-11T10:00:00Z', recordedAt: '2026-11-11T10:00:00Z' },
    { position: 2, revision: 2, eventType: 'PUBLISHED', source: 'COMMAND', subjectActorId: null, occurredAt: '2026-11-12T10:00:00Z', recordedAt: '2026-11-12T10:00:00Z' }],
  links: [], asOf: '2026-11-20T08:00:00Z', ispsRestricted: true,
  contentPolicy: { classification: 'RESTRICTED', download: 'DENIED_RESTRICTED', print: 'CONTROLLED_COPY_RENDERER_UNAVAILABLE', basis: 'RESTRICTED_EXPLICIT_PERMISSION', attributionRequired: true, ispsRestricted: true },
  acknowledgements: [], ...patch,
})
export const documentListFixture = () => ({ items: [{ id: s9Document, vesselId: s9Vessel, code: 'FICTIONAL-SSP', title: 'FICTIONAL security plan', classification: 'RESTRICTED',
  currentRevision: 2, latestRevision: 2, version: 21, holdActive: false, retentionState: 'UNCONFIGURED', disposalState: 'NONE', provenance: 'DeclaredUnverified', ispsRestricted: true }],
  offset: 0, hasMore: false, canWrite: false })
