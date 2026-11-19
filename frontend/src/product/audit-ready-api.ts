import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { decodeTrace, type Anchor, type Trace } from './human-review-api'

// DEV-032 / T-S3-007 (18.01 Backlog row:33). Closed wire shapes; an unexpected member, an
// invented locator or a forged availability is a contract breach, never silently accepted.
export type AuditReadySource = {
  sourceId: string; sourceRevision: number; requirementId: string; requirementRevision: number; ruleId: string | null; ruleRevision: number | null
  availability: 'Available' | 'Unavailable'; locatorState: 'Known' | 'Missing' | 'Unknown'; locator: string | null; titleState: 'Known' | 'Missing' | 'Unknown'; title: string | null
  provenance: 'DeclaredUnverified' | 'SyntheticUnverified' | 'Unavailable'; sourceHash: string | null; sourceSavedAt: string | null
}
export type AuditReadyChain = { evidenceId: string; evidenceRevision: number; controlId: string; controlRevision: number; sources: AuditReadySource[] }
export type AuditReadyChains = { vesselId: string; asOf: string; items: AuditReadyChain[]; unavailable: number; hasMore: boolean }

type Obj = Record<string, unknown>
const fail = (): never => { throw new ApiError(502, 'invalid_contract') }
const exact = (v: unknown, keys: string[]): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const positive = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const hash = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
const text = (v: unknown, max: number): v is string => typeof v === 'string' && v.length > 0 && v.length <= max && !/\p{Cc}/u.test(v)
const states = ['Known', 'Missing', 'Unknown']
const sourceKeys = ['sourceId', 'sourceRevision', 'requirementId', 'requirementRevision', 'ruleId', 'ruleRevision', 'availability', 'locatorState', 'locator', 'titleState', 'title', 'provenance', 'sourceHash', 'sourceSavedAt']

function decodeSource(s: unknown): AuditReadySource {
  if (!exact(s, sourceKeys) || !isGuid(s.sourceId) || !positive(s.sourceRevision) || !isGuid(s.requirementId) || !positive(s.requirementRevision) ||
    (s.ruleId === null) !== (s.ruleRevision === null) || s.ruleId !== null && (!isGuid(s.ruleId) || !positive(s.ruleRevision)) ||
    !states.includes(s.locatorState as string) || !states.includes(s.titleState as string)) return fail()
  // Text exists only for a Known state; Missing/Unknown are never filled in by the browser.
  if (s.locatorState === 'Known' ? !text(s.locator, 1000) : s.locator !== null) return fail()
  if (s.titleState === 'Known' ? !text(s.title, 200) : s.title !== null) return fail()
  if (s.availability === 'Available') {
    if (!['DeclaredUnverified', 'SyntheticUnverified'].includes(s.provenance as string) || !hash(s.sourceHash) || !timestamp(s.sourceSavedAt)) return fail()
  } else if (s.availability !== 'Unavailable' || s.provenance !== 'Unavailable' || s.sourceHash !== null || s.sourceSavedAt !== null || s.locatorState !== 'Unknown' || s.titleState !== 'Unknown') return fail()
  return s as AuditReadySource
}

export function decodeChains(v: unknown, vessel: string): AuditReadyChains {
  if (!exact(v, ['vesselId', 'asOf', 'items', 'unavailable', 'hasMore']) || typeof v.vesselId !== 'string' || v.vesselId.toLowerCase() !== vessel.toLowerCase() || !timestamp(v.asOf) ||
    !Array.isArray(v.items) || v.items.length > 32 || typeof v.unavailable !== 'number' || !Number.isSafeInteger(v.unavailable) || v.unavailable < 0 || v.unavailable > 32 || typeof v.hasMore !== 'boolean') return fail()
  const items = v.items.map(item => {
    if (!exact(item, ['evidenceId', 'evidenceRevision', 'controlId', 'controlRevision', 'sources']) || !isGuid(item.evidenceId) || !positive(item.evidenceRevision) ||
      !isGuid(item.controlId) || !positive(item.controlRevision) || !Array.isArray(item.sources) || item.sources.length > 32) return fail()
    return { evidenceId: item.evidenceId, evidenceRevision: item.evidenceRevision, controlId: item.controlId, controlRevision: item.controlRevision, sources: item.sources.map(decodeSource) } as AuditReadyChain
  })
  if (new Set(items.map(c => `${c.evidenceId.toLowerCase()}/${c.controlId.toLowerCase()}`)).size !== items.length) return fail()
  return { vesselId: v.vesselId, asOf: v.asOf, items, unavailable: v.unavailable, hasMore: v.hasMore }
}

export const chainsPath = (vessel: string) => {
  if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context')
  return `audit-ready/chains?vesselId=${vessel}`
}
export const auditReadyPath = (a: Anchor) => `audit-ready?vesselId=${a.vesselId}&evidenceId=${a.evidenceId}&evidenceRevision=${a.evidenceRevision}&controlId=${a.controlId}&controlRevision=${a.controlRevision}&offset=0`
export const chains = async (vessel: string, signal: AbortSignal) => decodeChains(await request<unknown>(chainsPath(vessel), signal), vessel)
export const auditReadyTrace = async (a: Anchor, signal: AbortSignal): Promise<Trace> => decodeTrace(await request<unknown>(auditReadyPath(a), signal), a, 0)

// Route: /app/audit-ready[/{vessel}][?evidenceId&evidenceRevision&controlId&controlRevision].
export type AuditReadyRoute = { invalid: boolean; vessel: string; anchor: Anchor | null }
export function readAuditReadyLocation(pathname: string, search: string): AuditReadyRoute {
  const match = /^\/app\/audit-ready(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(pathname)
  const invalid = { invalid: true, vessel: '', anchor: null }
  if (!match || match[1] !== undefined && !isGuid(match[1])) return invalid
  const vessel = match[1]?.toLowerCase() ?? '', query = new URLSearchParams(search), keys = [...query.keys()]
  if (keys.length === 0) return { invalid: false, vessel, anchor: null }
  const allowed = ['evidenceId', 'evidenceRevision', 'controlId', 'controlRevision']
  if (!vessel || keys.length !== 4 || !allowed.every(k => query.getAll(k).length === 1)) return invalid
  const revision = (k: string) => /^[1-9][0-9]{0,15}$/.test(query.get(k)!) ? Number(query.get(k)) : NaN
  const anchor = { vesselId: vessel, evidenceId: query.get('evidenceId')!.toLowerCase(), evidenceRevision: revision('evidenceRevision'), controlId: query.get('controlId')!.toLowerCase(), controlRevision: revision('controlRevision') }
  if (!isGuid(anchor.evidenceId) || !isGuid(anchor.controlId) || !positive(anchor.evidenceRevision) || !positive(anchor.controlRevision)) return invalid
  return { invalid: false, vessel, anchor }
}
export const auditReadyHref = (vessel: string, chain?: Pick<AuditReadyChain, 'evidenceId' | 'evidenceRevision' | 'controlId' | 'controlRevision'>) =>
  `/app/audit-ready${vessel ? '/' + vessel : ''}${chain ? '?' + new URLSearchParams({ evidenceId: chain.evidenceId, evidenceRevision: String(chain.evidenceRevision), controlId: chain.controlId, controlRevision: String(chain.controlRevision) }) : ''}`
