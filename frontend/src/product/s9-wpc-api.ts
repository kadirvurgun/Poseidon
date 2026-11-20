import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { isGuid } from './identifiers'

// S9 WP-C: DEV-062 voice-to-event draft, DEV-063 evaluation metrics, DEV-114 controlled document
// compare/security. Closed wire decoding: a voice draft that claims a confirmation without its human
// record, or a document policy outside the closed vocabularies, is rejected instead of rendered.
type S = components['schemas']
export type VoiceDraft = S['VoiceDraftDto']
export type VoiceDraftFields = S['VoiceDraftFields']
export type VoiceDraftPage = S['VoiceDraftPageDto']
export type VoiceDraftCapabilities = S['VoiceDraftCapabilitiesDto']
export type VoiceDraftMetrics = S['VoiceDraftMetricsDto']
export type VoiceTranscription = S['VoiceDraftTranscriptionDto']
export type ControlledDocument = S['ControlledDocumentDto']
export type ControlledDocumentRevision = S['ControlledDocumentRevisionDto']
export type ControlledDocumentList = S['ControlledDocumentListDto']
export type ContentAccess = S['ControlledDocumentContentAccessDto']

export const voiceStates = ['DRAFT', 'CONFIRMED', 'REJECTED'] as const
export const voiceOrigins = ['TYPED', 'LOCAL_SPEECH'] as const
export const fieldOrigins = ['EXTRACTED', 'USER_EDITED', 'USER_ENTERED', 'NONE'] as const
export const voiceFieldKeys = ['eventType', 'occurredAt', 'location', 'locationDetail', 'description', 'immediateAction'] as const
export type VoiceFieldKey = typeof voiceFieldKeys[number]
export const downloadPolicies = ['ALLOWED', 'DENIED_RESTRICTED'] as const
export const printPolicies = ['UNCONTROLLED_COPY_ONLY', 'CONTROLLED_COPY_RENDERER_UNAVAILABLE'] as const

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const oneOf = (v: unknown, values: readonly string[]) => typeof v === 'string' && values.includes(v)
const hash = (v: unknown) => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
function invalid(): never { throw new ApiError(502, 'invalid_s9_contract') }

export function decodeVoiceDraft(value: unknown, vessel: string, id?: string): VoiceDraft {
  if (!object(value) || !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel || !oneOf(value.state, voiceStates) ||
    !oneOf(value.origin, voiceOrigins) || !hash(value.hash) || !hash(value.transcriptSha256) || typeof value.transcript !== 'string' ||
    !object(value.candidate) || !Array.isArray(value.candidate.fields) || !object(value.fields) || !object(value.fieldOrigins) || !Array.isArray(value.missing) ||
    value.audioRetention !== 'NOT_RETAINED' ||
    !voiceFieldKeys.every(k => oneOf((value.fieldOrigins as Record<string, unknown>)[k], fieldOrigins)) ||
    // Every candidate field is traced: spans, a confidence in [0,1], a rule id.
    !value.candidate.fields.every(f => object(f) && Array.isArray(f.spans) && f.spans.length > 0 && typeof f.confidence === 'number' && f.confidence >= 0 && f.confidence <= 1 && typeof f.rule === 'string') ||
    // A confirmation always names its human; nothing is ever handed off without one.
    value.state === 'CONFIRMED' && (!isGuid(value.confirmedBy) || !Array.isArray(value.corrections) || value.handoff !== 'PENDING_EVENT_DRAFT_INTEGRATION') ||
    value.state !== 'CONFIRMED' && (value.confirmedBy !== null || value.handoff !== 'NOT_READY') ||
    value.state === 'REJECTED' && !object(value.rejection) || value.state !== 'DRAFT' && (value.canEdit === true || value.canConfirm === true) ||
    value.origin === 'LOCAL_SPEECH' && !object(value.speech)) invalid()
  return value as unknown as VoiceDraft
}

export function decodeDocument(value: unknown, vessel: string, id: string): ControlledDocument {
  if (!object(value) || value.id !== id || value.vesselId !== vessel || !Array.isArray(value.revisions) || !Array.isArray(value.custody) ||
    !value.revisions.every(r => object(r) && typeof r.revision === 'number' && hash(r.contentSha256)) ||
    !object(value.contentPolicy) || !oneOf(value.contentPolicy.download, downloadPolicies) || !oneOf(value.contentPolicy.print, printPolicies) ||
    // An ISPS-sensitive document is always RESTRICTED.
    value.ispsRestricted === true && value.classification !== 'RESTRICTED') invalid()
  return value as unknown as ControlledDocument
}

const scoped = (vessel: string, path: string, extra = '') => {
  if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context')
  return `${path}?vesselId=${vessel}${extra}`
}
export const voicePath = (vessel: string, suffix = '') => scoped(vessel, `voice-event-drafts${suffix}`)
const documentPath = (vessel: string, suffix = '') => {
  if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context')
  return `vessels/${vessel}/controlled-documents${suffix}`
}

// A frozen command keeps its Idempotency-Key and exact body until its outcome is known.
export type S9Attempt = { method: 'POST' | 'PUT'; path: string; key: string; body: string; ifMatch?: number }
export function freezeS9Command(method: 'POST' | 'PUT', path: string, body: unknown, ifMatch?: number): S9Attempt {
  if (!/^(voice-event-drafts(\/[0-9a-f-]{36}(\/(confirm|reject))?)?\?vesselId=[0-9a-f-]{36}|vessels\/[0-9a-f-]{36}\/controlled-documents\/[0-9a-f-]{36}\/revisions\/\d{1,9}\/(acknowledgements|content-access))$/.test(path))
    throw new ApiError(400, 'invalid_context')
  return { method, path, key: crypto.randomUUID(), body: JSON.stringify(body), ifMatch }
}
export function sendS9Command<T>(attempt: S9Attempt, csrf: string, signal: AbortSignal): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }
  if (attempt.ifMatch !== undefined) headers['If-Match'] = `"${attempt.ifMatch}"`
  return request<T>(attempt.path, signal, { method: attempt.method, headers, body: attempt.body })
}

export const s9Api = {
  voiceCapabilities: (vessel: string, signal: AbortSignal) => request<VoiceDraftCapabilities>(voicePath(vessel, '/capabilities'), signal),
  voiceMetrics: (vessel: string, signal: AbortSignal) => request<VoiceDraftMetrics>(voicePath(vessel, '/metrics'), signal),
  voiceList: (vessel: string, offset: number, signal: AbortSignal) => request<VoiceDraftPage>(scoped(vessel, 'voice-event-drafts', '&offset=' + Math.max(0, Math.trunc(offset))), signal),
  voiceRead: async (vessel: string, id: string, signal: AbortSignal) => {
    if (!isGuid(id)) throw new ApiError(400, 'invalid_context')
    return decodeVoiceDraft(await request<unknown>(voicePath(vessel, '/' + id), signal), vessel, id)
  },
  // Local-engine transcription only (never browser Web Speech / cloud speech); audio is not stored.
  transcribe: (vessel: string, audio: Blob, csrf: string, signal: AbortSignal) => request<VoiceTranscription>(voicePath(vessel, '/transcriptions'), signal,
    { method: 'POST', headers: { 'Content-Type': audio.type.split(';')[0] || 'audio/webm', 'X-CSRF-TOKEN': csrf }, body: audio }),
  documents: (vessel: string, signal: AbortSignal) => request<ControlledDocumentList>(documentPath(vessel), signal),
  document: async (vessel: string, id: string, signal: AbortSignal) => {
    if (!isGuid(id)) throw new ApiError(400, 'invalid_context')
    return decodeDocument(await request<unknown>(documentPath(vessel, '/' + id), signal), vessel, id)
  },
  // Bytes only through the classification-aware access record; the capability travels in a header.
  content: async (vessel: string, document: string, revision: number, csrf: string, signal: AbortSignal) => {
    const access = await sendS9Command<ContentAccess>(freezeS9Command('POST', documentPath(vessel, `/${document}/revisions/${revision}/content-access`), { purpose: 'VIEW' }), csrf, signal)
    const response = await fetch(`/api/v1/vessels/${vessel}/files/${access.fileId}/bytes`, { credentials: 'same-origin', signal, headers: { 'X-File-Capability': access.capability } })
    if (!response.ok) throw new ApiError(response.status, response.status === 403 ? 'document_download_restricted' : 'file_bytes_unavailable')
    return { access, blob: await response.blob() }
  },
  acknowledgePath: (vessel: string, document: string, revision: number) => documentPath(vessel, `/${document}/revisions/${revision}/acknowledgements`),
}

// 58.02 §33.3 ComparisonViewV1: a local, read-only view. Fields are compared only when both sides
// carry them; a hash match states byte equality only and is never a content review.
export type CompareStatus = 'SAME' | 'CHANGED' | 'NOT_COMPARABLE'
export type CompareRow = { field: string; left: string | null; right: string | null; status: CompareStatus }
export type Comparison = { state: 'SAME_REVISION' | 'READY'; rows: CompareRow[]; bytes: 'BYTE_IDENTICAL' | 'BYTE_DIFFERENT' }
const compareFields = ['mime', 'contentLength', 'contentSha256', 'intakeState', 'publicationState', 'verificationState', 'registeredAt', 'releasedAt', 'publishedAt'] as const
export function compareRevisions(left: ControlledDocumentRevision, right: ControlledDocumentRevision): Comparison {
  const value = (r: ControlledDocumentRevision, f: typeof compareFields[number]) => { const v = (r as Record<string, unknown>)[f]; return v === undefined ? undefined : v === null ? null : String(v) }
  const rows = compareFields.map(field => {
    const l = value(left, field), r = value(right, field)
    return { field, left: l ?? null, right: r ?? null, status: (l === undefined || r === undefined ? 'NOT_COMPARABLE' : l === r ? 'SAME' : 'CHANGED') as CompareStatus }
  })
  return { state: left.revision === right.revision ? 'SAME_REVISION' : 'READY', rows,
    bytes: left.contentSha256.toUpperCase() === right.contentSha256.toUpperCase() ? 'BYTE_IDENTICAL' : 'BYTE_DIFFERENT' }
}

export type S9Issue = 'offline' | 'conflict' | 'incomplete' | 'forbidden' | 'stale' | 'unavailable' | 'error' | null
export const s9Failure = (error: unknown): Exclude<S9Issue, null> =>
  typeof navigator !== 'undefined' && navigator.onLine === false ? 'offline' : error instanceof ApiError ?
    error.status === 409 || error.status === 412 ? (error.code === 'version_conflict' || error.code === 'idempotency_key_reused' ? 'conflict' : 'stale') :
    error.status === 422 ? 'incomplete' : error.status === 403 ? 'forbidden' : error.status === 503 ? 'unavailable' : 'error' : error instanceof TypeError ? 'offline' : 'error'
export const textOrNull = (value: string) => value.trim() === '' ? null : value
