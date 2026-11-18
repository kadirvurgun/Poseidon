import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { dailyRoutes, dailySourceVersion, type DailyRecord } from './daily-operations-api'
import type { DailyKind } from './daily-report-fields'
export type DailyReviewDuty = 'Master' | 'ChiefOfficer' | 'ChiefEngineer'
export type DailyReviewChoice = 'REVIEWED' | 'RETURNED'
export type DailyReviewer = { id: string; roleId: string; duty: DailyReviewDuty; decisions: DailyReviewChoice[] }
export type DailyReviewCapabilities = { recordId: string; vesselId: string; kind: DailyKind; recordRevision: number; recordVersion: number; reviewVersion: number; canRecord: boolean; authorities: DailyReviewer[]; blockers: string[]; sourceVersion: string }
export type DailyReview = { id: string; recordId: string; vesselId: string; kind: DailyKind; recordRevision: number; sequence: number; version: number; actorId: string; roleId: string; authorityId: string; duty: DailyReviewDuty; decision: DailyReviewChoice; reasonCode: 'REVIEW_RECORDED' | 'RETURNED_FOR_CORRECTION'; rationale: string; sourceVersion: string; authorityHash: string; recordHash: string; savedAt: string; state: 'RECORDED_UNASSESSED' }
export type DailyReviewPage = { items: DailyReview[]; offset: number; limit: 25; hasMore: boolean }
export type DailyReviewAttempt = { key: string; owner: string; actorId: string; vessel: string; kind: DailyKind; recordId: string; recordRevision: number; recordHash: string; authority: DailyReviewer; decision: DailyReviewChoice; rationale: string; body: string }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const integer = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const hash = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
const text = (v: unknown, max: number) => typeof v === 'string' && v.length > 0 && v.length <= max && !/\p{Cc}/u.test(v)
export const reviewRationale = (v: unknown): v is string => typeof v === 'string' && v.length <= 2000 && v.trim().length > 0 && !/\p{Cc}/u.test(v.replace(/[\r\n\t]/g, ''))
const duties = (kind: DailyKind): readonly string[] => kind === 'Noon' ? ['Master'] : kind === 'Deck' ? ['Master', 'ChiefOfficer'] : ['ChiefEngineer']
const choices = (kind: DailyKind): readonly string[] => kind === 'Noon' ? ['REVIEWED'] : ['REVIEWED', 'RETURNED']
export const reviewReason = (decision: DailyReviewChoice) => decision === 'RETURNED' ? 'RETURNED_FOR_CORRECTION' : 'REVIEW_RECORDED'
function fail(): never { throw new ApiError(502, 'invalid_daily_review_contract') }
export function decodeDailyReviewCapabilities(value: unknown, vessel: string, kind: DailyKind, recordId: string): DailyReviewCapabilities {
  if (!isGuid(vessel) || !isGuid(recordId) || !exact(value, ['recordId', 'vesselId', 'kind', 'recordRevision', 'recordVersion', 'reviewVersion', 'canRecord', 'authorities', 'blockers', 'sourceVersion']) || value.recordId !== recordId || value.vesselId !== vessel || value.kind !== kind || !integer(value.recordRevision) || !integer(value.recordVersion, 1, 4294967295) || !integer(value.reviewVersion, 0, 4294967295) || typeof value.canRecord !== 'boolean' || !Array.isArray(value.authorities) || value.authorities.length > 256 || value.canRecord !== (value.authorities.length > 0) || !Array.isArray(value.blockers) || value.blockers.length > 128 || !value.blockers.every(code => text(code, 200)) || value.sourceVersion !== dailySourceVersion) fail()
  const authorities: DailyReviewer[] = []
  for (const row of value.authorities) {
    if (!exact(row, ['id', 'roleId', 'duty', 'decisions']) || !isGuid(row.id) || !isGuid(row.roleId) || typeof row.duty !== 'string' || !duties(kind).includes(row.duty) || !Array.isArray(row.decisions) || row.decisions.length !== choices(kind).length || !row.decisions.every(choice => typeof choice === 'string' && choices(kind).includes(choice)) || new Set(row.decisions).size !== row.decisions.length || authorities.some(a => a.id === row.id)) fail()
    authorities.push(row as DailyReviewer)
  }
  return { ...value, authorities } as DailyReviewCapabilities
}
export function decodeDailyReview(value: unknown, vessel: string, kind: DailyKind, recordId: string): DailyReview {
  if (!exact(value, ['id', 'recordId', 'vesselId', 'kind', 'recordRevision', 'sequence', 'version', 'actorId', 'roleId', 'authorityId', 'duty', 'decision', 'reasonCode', 'rationale', 'sourceVersion', 'authorityHash', 'recordHash', 'savedAt', 'state']) || !isGuid(value.id) || value.recordId !== recordId || value.vesselId !== vessel || value.kind !== kind || !integer(value.recordRevision) || !integer(value.sequence) || !integer(value.version, 1, 4294967295) || !isGuid(value.actorId) || !isGuid(value.roleId) || !isGuid(value.authorityId) || typeof value.duty !== 'string' || !duties(kind).includes(value.duty) || typeof value.decision !== 'string' || !choices(kind).includes(value.decision) || value.reasonCode !== reviewReason(value.decision as DailyReviewChoice) || !reviewRationale(value.rationale) || value.sourceVersion !== dailySourceVersion || !hash(value.authorityHash) || !hash(value.recordHash) || !timestamp(value.savedAt) || value.state !== 'RECORDED_UNASSESSED') fail()
  return value as DailyReview
}
export function decodeDailyReviewPage(value: unknown, vessel: string, kind: DailyKind, recordId: string, offset: number): DailyReviewPage {
  if (!exact(value, ['items', 'offset', 'limit', 'hasMore']) || !Array.isArray(value.items) || value.items.length > 25 || value.offset !== offset || value.limit !== 25 || typeof value.hasMore !== 'boolean' || value.hasMore && value.items.length !== 25) fail()
  const items = value.items.map(row => decodeDailyReview(row, vessel, kind, recordId))
  if (new Set(items.map(row => row.id)).size !== items.length || new Set(items.map(row => row.sequence)).size !== items.length) fail()
  return { items, offset, limit: 25, hasMore: value.hasMore }
}
const root = (vessel: string, kind: DailyKind, recordId: string) => { if (!isGuid(vessel) || !isGuid(recordId)) throw new ApiError(400, 'invalid_context'); return `${dailyRoutes[kind].api}/${recordId}` }
export const dailyReviewApi = {
  capabilities: async (vessel: string, kind: DailyKind, recordId: string, signal: AbortSignal) => decodeDailyReviewCapabilities(await request<unknown>(`${root(vessel, kind, recordId)}/review-capabilities?vesselId=${vessel}`, signal), vessel, kind, recordId),
  history: async (vessel: string, kind: DailyKind, recordId: string, offset: number, signal: AbortSignal) => { if (!integer(offset, 0, 100000)) throw new ApiError(400, 'invalid_context'); return decodeDailyReviewPage(await request<unknown>(`${root(vessel, kind, recordId)}/reviews?vesselId=${vessel}&offset=${offset}`, signal), vessel, kind, recordId, offset) },
  record: async (attempt: DailyReviewAttempt, csrf: string, signal: AbortSignal) => {
    const value = decodeDailyReview(await request<unknown>(`${root(attempt.vessel, attempt.kind, attempt.recordId)}/reviews?vesselId=${attempt.vessel}`, signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body }), attempt.vessel, attempt.kind, attempt.recordId)
    if (value.actorId !== attempt.actorId || value.authorityId !== attempt.authority.id || value.roleId !== attempt.authority.roleId || value.duty !== attempt.authority.duty || value.recordRevision !== attempt.recordRevision || value.recordHash !== attempt.recordHash || value.decision !== attempt.decision || value.rationale !== attempt.rationale) fail()
    return value
  },
}
export function freezeDailyReview({ record, caps, actorId, tenantId, authorityId, decision, rationale }: { record: DailyRecord; caps: DailyReviewCapabilities; actorId: string; tenantId: string; authorityId: string; decision: DailyReviewChoice; rationale: string }): DailyReviewAttempt {
  const authority = caps.authorities.find(a => a.id === authorityId)
  if (!isGuid(actorId) || !isGuid(tenantId) || !caps.canRecord || !authority || !authority.decisions.includes(decision) || !reviewRationale(rationale) || record.vesselId !== caps.vesselId || record.kind !== caps.kind || record.id !== caps.recordId || record.revision !== caps.recordRevision || record.version !== caps.recordVersion) throw new ApiError(400, 'invalid_review_context')
  return { key: crypto.randomUUID(), owner: `${tenantId}/${actorId}`, actorId, vessel: record.vesselId, kind: record.kind, recordId: record.id, recordRevision: record.revision, recordHash: record.hash, authority, decision, rationale, body: JSON.stringify({ expectedVersion: caps.recordVersion, expectedReviewVersion: caps.reviewVersion, reviewedRevision: caps.recordRevision, authorityId, decision, reasonCode: reviewReason(decision), rationale }) }
}
