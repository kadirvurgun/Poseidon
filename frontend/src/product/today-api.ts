import type { components } from '../generated/api'
import type { Page, Vessel } from './directory-contract'
import { ApiError, request } from './api'

export type Today = components['schemas']['TodayDto']
export type Why = components['schemas']['TodayWhyDto']
const object = (x: unknown): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x)
const exact = (x: Record<string, unknown>, keys: string[]) => Object.keys(x).length === keys.length && keys.every(k => k in x)
const date = (x: unknown) => {
  if (typeof x !== 'string') return false
  const m = /^([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.[0-9]{1,7})?(Z|[+-]([0-9]{2}):([0-9]{2}))$/.exec(x)
  if (!m) return false
  const y = +m[1]; const month = +m[2]; const day = +m[3]
  const leap = y % 4 === 0 && (y % 100 !== 0 || y % 400 === 0)
  if (y < 1 || month < 1 || month > 12 || day < 1 || day > [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1] ||
    +m[4] > 23 || +m[5] > 59 || +m[6] > 59 || (m[7] !== 'Z' && (+m[8] > 14 || +m[9] > 59 || (+m[8] === 14 && +m[9] !== 0)))) return false
  const instant = Date.parse(x)
  return Number.isFinite(instant) && instant >= -62135596800000 && instant <= 253402300799999
}
const integer = (x: unknown) => typeof x === 'number' && Number.isSafeInteger(x) && x >= 0
const one = (x: unknown, values: string[]) => typeof x === 'string' && values.includes(x)
const guid = (x: unknown) => typeof x === 'string' && /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(x) && x !== '00000000-0000-0000-0000-000000000000'
const text = (x: unknown, max: number) => typeof x === 'string' && x.length > 0 && x.length <= max && !Array.from(x).some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))
const href = (x: unknown, id: string) => x === `/app/fleet/${id}/profile`
function validVessel(x: unknown): x is Vessel {
  return object(x) && exact(x, ['id', 'companyId', 'code', 'name', 'kind', 'version', 'canEdit']) && guid(x.id) && guid(x.companyId) &&
    text(x.code, 64) && text(x.name, 200) && one(x.kind, ['Ship', 'Tug']) && integer(x.version) &&
    Number(x.version) > 0 && Number(x.version) <= 4294967295 && typeof x.canEdit === 'boolean'
}
function fail(): never { throw new ApiError(502, 'invalid_today_contract') }
export function decodeTodayVessels(x: unknown, offset: number): Page<Vessel> {
  if (!object(x) || !exact(x, ['items', 'offset', 'limit', 'hasMore']) || x.offset !== offset || x.limit !== 25 ||
    typeof x.hasMore !== 'boolean' || !Array.isArray(x.items) || x.items.length > 25 || !x.items.every(validVessel) ||
    new Set(x.items.map(v => v.id.toLowerCase())).size !== x.items.length) fail()
  return x as Page<Vessel>
}
export function decodeToday(x: unknown, id: string): Today {
  if (!object(x) || !exact(x, ['vessel', 'readAt', 'coverage', 'cards']) || !validVessel(x.vessel) || x.vessel.id !== id || !guid(id) || !date(x.readAt) ||
    !Array.isArray(x.coverage) || x.coverage.length !== 5 || !object(x.cards) || !Array.isArray(x.cards.items) || x.cards.items.length > 1 ||
    !exact(x.cards, ['items', 'offset', 'limit', 'hasMore']) || !integer(x.cards.offset) || Number(x.cards.offset) > 100000 ||
    !integer(x.cards.limit) || Number(x.cards.limit) < 1 || Number(x.cards.limit) > 25 || x.cards.hasMore !== false ||
    (Number(x.cards.offset) > 0 && x.cards.items.length > 0)) fail()
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
export const today = async (id: string, signal: AbortSignal) => decodeToday(await request<unknown>(`poseidon-today?vesselId=${encodeURIComponent(id)}`, signal), id)
export const why = async (id: string, signal: AbortSignal) => decodeWhy(await request<unknown>(`why-engine?vesselId=${encodeURIComponent(id)}&cardKey=regulatory-draft`, signal), id)
export const todayVessels = async (offset: number, signal: AbortSignal) => decodeTodayVessels(await request<unknown>(`vessels?offset=${offset}&limit=25`, signal), offset)
