import { timestamp as date } from './date-validation'
import { isGuid as guid } from './identifiers'
import type { components } from '../generated/api'
import type { Page, Vessel } from './directory-contract'
import { ApiError, request } from './api'

export type Today = components['schemas']['TodayDto']
export type Why = components['schemas']['TodayWhyDto']
const pageLimit = 25
const todayOffset = 0
const object = (x: unknown): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x)
const exact = (x: Record<string, unknown>, keys: string[]) => Object.keys(x).length === keys.length && keys.every(k => k in x)
const integer = (x: unknown) => typeof x === 'number' && Number.isSafeInteger(x) && x >= 0
const one = (x: unknown, values: string[]) => typeof x === 'string' && values.includes(x)
const text = (x: unknown, max: number) => typeof x === 'string' && x.length > 0 && x.length <= max && !Array.from(x).some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))
const href = (x: unknown, id: string) => x === `/app/fleet/${id}/profile`
function validVessel(x: unknown): x is Vessel {
  return object(x) && exact(x, ['id', 'companyId', 'code', 'name', 'kind', 'version', 'canEdit']) && guid(x.id) && guid(x.companyId) &&
    text(x.code, 64) && text(x.name, 200) && one(x.kind, ['Ship', 'Tug']) && integer(x.version) &&
    Number(x.version) > 0 && Number(x.version) <= 4294967295 && typeof x.canEdit === 'boolean'
}
function fail(): never { throw new ApiError(502, 'invalid_today_contract') }
export function decodeTodayVessels(x: unknown, offset: number): Page<Vessel> {
  if (!integer(offset) || offset > 100000 || !object(x) || !exact(x, ['items', 'offset', 'limit', 'hasMore']) || x.offset !== offset || x.limit !== pageLimit ||
    typeof x.hasMore !== 'boolean' || !Array.isArray(x.items) || x.items.length > pageLimit || (x.hasMore && x.items.length !== pageLimit) || !x.items.every(validVessel) ||
    new Set(x.items.map(v => v.id.toLowerCase())).size !== x.items.length) fail()
  return x as Page<Vessel>
}
export function decodeToday(x: unknown, id: string): Today {
  if (!object(x) || !exact(x, ['vessel', 'readAt', 'coverage', 'cards']) || !validVessel(x.vessel) || x.vessel.id !== id || !guid(id) || !date(x.readAt) ||
    !Array.isArray(x.coverage) || x.coverage.length !== 5 || !object(x.cards) || !Array.isArray(x.cards.items) || x.cards.items.length > 1 ||
    !exact(x.cards, ['items', 'offset', 'limit', 'hasMore']) || x.cards.offset !== todayOffset ||
    x.cards.limit !== pageLimit || x.cards.hasMore !== false) fail()
  const sources = ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification']
  if (!x.coverage.every((c, i) => object(c) && exact(c, ['source', 'status']) && c.source === sources[i] && one(c.status, i === 0 ? ['Available', 'Missing'] : ['Unavailable']))) fail()
  const draftMissing = x.coverage[0].status === 'Missing'
  const canEdit = x.vessel.canEdit
  if (!x.cards.items.every(c => object(c) && exact(c, ['key', 'priority', 'source', 'reason', 'owner', 'nextAction', 'href']) &&
    c.key === 'regulatory-draft' && c.priority === 'Advisory' && c.source === 'RegulatoryDraft' &&
    c.reason === (draftMissing ? 'DraftAbsent' : 'DraftUnverified') && c.owner === null && c.nextAction === (canEdit ? 'ReviewDraft' : 'ViewDraft') && href(c.href, id))) fail()
  return x as Today
}
export function decodeWhy(x: unknown, id: string): Why {
  if (!object(x) || !guid(id) || !exact(x, ['vesselId', 'cardKey', 'rule', 'reason', 'inputs', 'readAt', 'savedAt', 'sourceObservedAt', 'freshness', 'provenance', 'blockers', 'owner', 'nextAction', 'href']) ||
    x.vesselId !== id || x.cardKey !== 'regulatory-draft' || x.rule !== 'TW-DRAFT-01' ||
    !one(x.reason, ['DraftAbsent', 'DraftUnverified']) || !object(x.inputs) || x.inputs.reason !== x.reason ||
    !exact(x.inputs, ['known', 'missing', 'unknown', 'reason']) ||
    ![x.inputs.known, x.inputs.missing, x.inputs.unknown].every(integer) ||
    Number(x.inputs.known) + Number(x.inputs.missing) + Number(x.inputs.unknown) !== (x.reason === 'DraftAbsent' ? 0 : 17) ||
    !date(x.readAt) || !(x.savedAt === null || date(x.savedAt)) || !(x.sourceObservedAt === null || date(x.sourceObservedAt)) ||
    x.freshness !== 'Unverified' || (x.reason === 'DraftAbsent' ? x.provenance !== 'Missing' || x.savedAt !== null || x.sourceObservedAt !== null
      : !one(x.provenance, ['Unknown', 'Synthetic', 'DeclaredUnverified']) || x.savedAt === null) ||
    !Array.isArray(x.blockers) || x.blockers.length !== 2 || x.blockers[0] !== (x.reason === 'DraftAbsent' ? 'DraftAbsent' : 'VerificationUnavailable') ||
    x.blockers[1] !== 'OperationalSourcesUnavailable' || x.owner !== null || !one(x.nextAction, ['ViewDraft', 'ReviewDraft']) || !href(x.href, id)) fail()
  return x as Why
}
export const today = async (id: string, signal: AbortSignal) => decodeToday(await request<unknown>(`poseidon-today?vesselId=${encodeURIComponent(id)}&offset=${todayOffset}&limit=${pageLimit}`, signal), id)
export const why = async (id: string, signal: AbortSignal) => decodeWhy(await request<unknown>(`why-engine?vesselId=${encodeURIComponent(id)}&cardKey=regulatory-draft`, signal), id)
export const todayVessels = async (offset: number, signal: AbortSignal) => decodeTodayVessels(await request<unknown>(`vessels?offset=${offset}&limit=${pageLimit}`, signal), offset)
