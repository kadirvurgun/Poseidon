import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import type { Command } from './tug-readiness-api'

// S11 WP-C DEV-073/093 client contract. Closed server codes only; an unknown verdict/state is a contract failure, never
// coerced to CONFIRMED. The system never confirms: only a qualified, appointed human reviewer records a verdict.
export const reviewScopes = ['SHIP', 'TUG'] as const
export type ReviewScope = typeof reviewScopes[number]
export const reviewStates = ['OPEN', 'IN_REVIEW', 'CLOSED_WITH_OPEN_ITEMS', 'CLOSED'] as const
export const reviewVerdicts = ['CONFIRMED', 'AMBIGUOUS', 'NOT_APPLICABLE_CONFIRMED', 'REVIEW_REQUIRED'] as const
export type ReviewVerdict = typeof reviewVerdicts[number]
export const reviewAuthorities = ['STATUTORY', 'FLAG', 'CLASS', 'PORT_LOCAL', 'COMPANY_SMS', 'MANUFACTURER', 'INDUSTRY_GUIDANCE'] as const
export const reviewOwnerRoles = ['UNASSIGNED', 'REGULATORY_LEAD', 'REGULATORY_SME', 'PRODUCT_OWNER', 'DPA', 'PILOT_CUSTOMER'] as const
export const reviewCategories = ['STATUTORY_LAW', 'CLASS_RULE', 'COMPANY_SMS', 'GUIDANCE_NOT_LAW', 'UNASSIGNED'] as const
export const reviewKinds = ['APPLICABILITY_RULE', 'SOURCE_CANDIDATE', 'TUG_LIMIT', 'OWNERSHIP_SLOT'] as const
export const closeOutcomes = ['CLOSED_WITH_OPEN_ITEMS', 'CLOSED'] as const

export type ReviewItem = { id: string; ordinal: number; kind: typeof reviewKinds[number]; subjectKey: string; ruleId: string | null; ruleRevision: number | null
  requirementId: string | null; requirementRevision: number | null; sourceId: string | null; sourceRevision: number | null; candidateSourceCode: string | null
  candidateAuthority: string | null; candidateUnverified: boolean; predictedResult: string | null; verdict: ReviewVerdict; effectiveVerdict: ReviewVerdict; stale: boolean
  open: boolean; sourceAuthority: string | null; authorityCategory: typeof reviewCategories[number]; ownerRole: typeof reviewOwnerRoles[number]; sourceReference: string | null
  sourceEdition: string | null; trackedIssueId: string | null; reviewerId: string | null; qualificationRef: string | null; verdictCount: number; verdictAt: string | null
  version: number; blockers: string[]; replayed: boolean }
export type ReviewSummary = { id: string; vesselId: string; scope: ReviewScope; state: typeof reviewStates[number]; profileRevision: number | null; evaluationId: string | null
  pinHash: string; itemCount: number; openItems: number; untrackedOpenItems: number; confirmedItems: number; staleItems: number; profileStale: boolean; createdBy: string
  createdAt: string; closedBy: string | null; closedAt: string | null; openItemsAtClose: number | null; provenance: string; version: number; blockers: string[] }
export type ReviewOwnerRow = { authority: string; category: typeof reviewCategories[number]; items: number; open: number; confirmed: number; ownerRoles: string[] }
export type ReviewDetail = { campaign: ReviewSummary; items: ReviewItem[]; ownerMatrix: ReviewOwnerRow[]; canManage: boolean; canReview: boolean
  reviewerAuthorities: { id: string; scopes: ReviewScope[]; sourceAuthorities: string[]; qualificationVerified: boolean }[]; notices: string[]; replayed: boolean
  policyVersion: string; candidateSourcePlan: string }
export type ReviewList = { items: ReviewSummary[]; canManage: boolean }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_regulatory_review_contract') }
const text = (v: unknown, max = 4000) => typeof v === 'string' && v.length <= max
const opt = (v: unknown, max = 400) => v === null || text(v, max)
const optGuid = (v: unknown) => v === null || isGuid(v)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const optInt = (v: unknown, min = 0) => v === null || int(v, min)
const list = (v: unknown, max = 512): v is unknown[] => Array.isArray(v) && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const codes = (v: unknown) => list(v, 64) && v.every(x => typeof x === 'string' && /^[A-Z0-9_]{1,64}$/.test(x))
const confirming = (v: unknown) => v === 'CONFIRMED' || v === 'NOT_APPLICABLE_CONFIRMED'

function item(v: unknown): ReviewItem {
  if (!object(v) || !isGuid(v.id) || !int(v.ordinal, 1) || !oneOf(v.kind, reviewKinds) || !text(v.subjectKey, 80) || !optGuid(v.ruleId) || !optInt(v.ruleRevision, 1) ||
    !optGuid(v.requirementId) || !optInt(v.requirementRevision, 1) || !optGuid(v.sourceId) || !optInt(v.sourceRevision, 1) || !opt(v.candidateSourceCode, 40) ||
    !(v.candidateAuthority === null || oneOf(v.candidateAuthority, reviewAuthorities)) || typeof v.candidateUnverified !== 'boolean' || !opt(v.predictedResult, 40) ||
    !oneOf(v.verdict, reviewVerdicts) || !oneOf(v.effectiveVerdict, reviewVerdicts) || typeof v.stale !== 'boolean' || typeof v.open !== 'boolean' ||
    !(v.sourceAuthority === null || oneOf(v.sourceAuthority, reviewAuthorities)) || !oneOf(v.authorityCategory, reviewCategories) || !oneOf(v.ownerRole, reviewOwnerRoles) ||
    !opt(v.sourceReference, 200) || !opt(v.sourceEdition, 80) || !opt(v.trackedIssueId, 64) || !optGuid(v.reviewerId) || !opt(v.qualificationRef, 200) ||
    !int(v.verdictCount, 0) || !(v.verdictAt === null || time(v.verdictAt)) || !int(v.version, 0) || !codes(v.blockers) || typeof v.replayed !== 'boolean') fail()
  // Defense in depth: a stale item is never effectively confirmed, an open item is never shown as confirmed, and a
  // confirmation always carries a reviewer, an authority label and an owner.
  if (v.stale && v.effectiveVerdict !== 'REVIEW_REQUIRED') fail()
  if (v.open === confirming(v.effectiveVerdict)) fail()
  if (confirming(v.verdict) && (v.reviewerId === null || v.sourceAuthority === null || v.ownerRole === 'UNASSIGNED')) fail()
  if ((v.verdictCount === 0) !== (v.reviewerId === null)) fail()
  return v as unknown as ReviewItem
}
export const decodeReviewItem = item

function summary(v: unknown): ReviewSummary {
  if (!object(v) || !isGuid(v.id) || !isGuid(v.vesselId) || !oneOf(v.scope, reviewScopes) || !oneOf(v.state, reviewStates) || !optInt(v.profileRevision, 1) ||
    !optGuid(v.evaluationId) || !text(v.pinHash, 64) || !int(v.itemCount) || !int(v.openItems) || !int(v.untrackedOpenItems) || !int(v.confirmedItems) || !int(v.staleItems) ||
    typeof v.profileStale !== 'boolean' || !isGuid(v.createdBy) || !time(v.createdAt) || !optGuid(v.closedBy) || !(v.closedAt === null || time(v.closedAt)) ||
    !optInt(v.openItemsAtClose) || !text(v.provenance, 32) || !int(v.version) || !codes(v.blockers)) fail()
  if ((v.untrackedOpenItems as number) > (v.openItems as number) || (v.openItems as number) + (v.confirmedItems as number) !== v.itemCount) fail()
  if (v.state === 'CLOSED' && v.openItemsAtClose !== 0) fail()
  return v as unknown as ReviewSummary
}

export function decodeReviewDetail(v: unknown, vessel: string): ReviewDetail {
  if (!object(v) || !list(v.items, 256) || !list(v.ownerMatrix, 16) || typeof v.canManage !== 'boolean' || typeof v.canReview !== 'boolean' ||
    !list(v.reviewerAuthorities, 64) || !codes(v.notices) || typeof v.replayed !== 'boolean' || !text(v.policyVersion, 80) || !text(v.candidateSourcePlan, 200)) fail()
  const campaign = summary(v.campaign)
  if (campaign.vesselId !== vessel) fail()
  const items = (v.items as unknown[]).map(item)
  if (items.length !== campaign.itemCount || items.filter(i => i.open).length !== campaign.openItems) fail()
  if ((v.ownerMatrix as unknown[]).some(r => !object(r) || !text(r.authority, 32) || !oneOf(r.category, reviewCategories) || !int(r.items) || !int(r.open) || !int(r.confirmed) ||
    !list(r.ownerRoles, 8) || (r.ownerRoles as unknown[]).some(o => !oneOf(o, reviewOwnerRoles)))) fail()
  if ((v.reviewerAuthorities as unknown[]).some(a => !object(a) || !isGuid(a.id) || !list(a.scopes, 2) || (a.scopes as unknown[]).some(s => !oneOf(s, reviewScopes)) ||
    !list(a.sourceAuthorities, 7) || (a.sourceAuthorities as unknown[]).some(s => !oneOf(s, reviewAuthorities)) || typeof a.qualificationVerified !== 'boolean')) fail()
  return { ...(v as unknown as ReviewDetail), campaign, items }
}

export function decodeReviewList(v: unknown, vessel: string): ReviewList {
  if (!object(v) || !list(v.items, 20) || typeof v.canManage !== 'boolean') fail()
  const items = (v.items as unknown[]).map(summary)
  if (items.some(i => i.vesselId !== vessel)) fail()
  return { items, canManage: v.canManage }
}

export type VerdictInput = { verdict: ReviewVerdict; sourceAuthority: string | null; ownerRole: string; sourceReference: string | null; sourceEdition: string | null
  trackedIssueId: string | null; note: string }

export const regulatoryReviewApi = {
  list: async (vessel: string, scope: ReviewScope, signal: AbortSignal) => decodeReviewList(await request<unknown>(`regulatory-reviews?vessel=${vessel}&scope=${scope}`, signal), vessel),
  detail: async (vessel: string, id: string, signal: AbortSignal) => decodeReviewDetail(await request<unknown>(`regulatory-reviews/${id}`, signal), vessel),
  create: (vessel: string, scope: ReviewScope): Command => ({ key: crypto.randomUUID(), method: 'POST', path: 'regulatory-reviews', body: { vesselId: vessel, scope } }),
  verdict: (campaign: string, i: ReviewItem, authorityId: string, input: VerdictInput): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `regulatory-reviews/${campaign}/items/${i.id}/verdicts`, body: { expectedVersion: i.version, authorityId, ...input } }),
  close: (c: ReviewSummary, outcome: typeof closeOutcomes[number], reason: string): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `regulatory-reviews/${c.id}/close`, body: { expectedVersion: c.version, outcome, reason } }),
}
