import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import type { Command } from './tug-readiness-api'

// S11 WP-A DEV-069/070 client contracts. Closed server codes only: an unknown state or decision is a contract failure,
// never coerced into a signed or accepted state. A report is an advisory count snapshot; only the appointed data
// owner's separate human record signs it, and a superseded report's sign-off is invalidated.
export const reportStates = ['AWAITING_SIGN_OFF', 'SIGNED_OFF', 'SUPERSEDED'] as const
export const decisions = ['ACCEPTED', 'ACCEPTED_WITH_EXCEPTIONS', 'REJECTED'] as const
export const exceptionKinds = ['MISSING_CRITICAL', 'REJECTED_ROWS', 'DUPLICATE_ROWS', 'NOT_COMMITTED', 'SAMPLE_UNVERIFIED', 'SAMPLE_MISMATCH', 'OTHER'] as const
export const validities = ['EFFECTIVE', 'INVALIDATED_SUPERSEDED', 'NONE'] as const
export const batchCategories = ['vessel', 'crew', 'certificate', 'equipment'] as const

export type Counts = { source: number; accepted: number; rejected: number; duplicate: number; notCommitted: number; missingCritical: number }
export type BatchPin = { batchId: string; category: string; revision: number; state: string; sourceHash: string; templateFingerprint: string; previewId: string | null
  sourceRows: number; acceptedRows: number; rejectedRows: number; duplicateRows: number; uploadedBy: string }
export type Target = { batchId: string; rowNumber: number; targetKind: string; targetId: string }
export type Missing = Target & { field: string; reason: 'MISSING' | 'UNKNOWN' }
export type ReconException = { kind: typeof exceptionKinds[number]; reference: string; disposition: string }
export type Report = { id: string; vesselId: string; revision: number; version: number; state: typeof reportStates[number]; policyVersion: string; criticalFieldVersion: string
  batches: BatchPin[]; counts: Counts; missingCritical: Missing[]; missingCriticalTruncated: boolean; sampleSeed: string; sample: Target[]; hash: string; createdBy: string
  createdAt: string; supersededAt: string | null; provenance: string }
export type SignOff = { id: string; decision: typeof decisions[number]; reason: string; exceptions: ReconException[]; sampleVerified: string[]; duty: string; signedBy: string
  signedAt: string; reportHash: string; validity: typeof validities[number] }
export type Authority = { id: string; duty: 'DataOwner' }
export type ReportView = { report: Report; signOff: SignOff | null; stale: boolean; openItems: string[]; authorities: Authority[]; canSignOff: boolean; signOffBlockers: string[]
  decisions: string[]; exceptionKinds: string[]; sourceLocator: string }
export type Summary = { id: string; state: typeof reportStates[number]; revision: number; counts: Counts; createdAt: string; decision: typeof decisions[number] | null
  signOffValidity: typeof validities[number] }
export type Candidate = { id: string; category: string; state: string; rowCount: number; createdAt: string; committedAt: string | null; templateVersion: string }
export type ListView = { vesselId: string; items: Summary[]; batches: Candidate[]; canCreate: boolean; authorities: Authority[]; templateVersions: Record<string, string>
  maxBatches: number; maxSample: number; sourceLocator: string }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_reconciliation_contract') }
const count = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v >= 0
const str = (v: unknown, max = 200) => typeof v === 'string' && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && (values as readonly string[]).includes(v)
const strings = (v: unknown) => Array.isArray(v) && v.every(x => str(x, 64))

function decodeCounts(v: unknown): Counts {
  if (!object(v) || !['source', 'accepted', 'rejected', 'duplicate', 'notCommitted', 'missingCritical'].every(k => count(v[k]))) fail()
  const c = v as Counts
  // Every source row is explained once; a report that cannot explain its rows is not shown as a count snapshot.
  if (c.source !== c.accepted + c.rejected + c.notCommitted || c.duplicate > c.rejected) fail()
  return { source: c.source, accepted: c.accepted, rejected: c.rejected, duplicate: c.duplicate, notCommitted: c.notCommitted, missingCritical: c.missingCritical }
}
function decodeTarget(v: unknown): Target {
  if (!object(v) || !isGuid(v.batchId) || !count(v.rowNumber) || !str(v.targetKind, 32) || !isGuid(v.targetId)) fail()
  return { batchId: v.batchId as string, rowNumber: v.rowNumber as number, targetKind: v.targetKind as string, targetId: v.targetId as string }
}
function decodeAuthorities(v: unknown): Authority[] {
  if (!Array.isArray(v)) fail()
  return v.map(a => { if (!object(a) || !isGuid(a.id) || a.duty !== 'DataOwner') fail(); return { id: a.id as string, duty: 'DataOwner' as const } })
}
export function decodeReportView(v: unknown, vessel: string): ReportView {
  if (!object(v) || !object(v.report) || typeof v.stale !== 'boolean' || typeof v.canSignOff !== 'boolean' || !strings(v.openItems) || !strings(v.signOffBlockers)) fail()
  const r = v.report
  if (!isGuid(r.id) || r.vesselId !== vessel || !count(r.revision) || !count(r.version) || !oneOf(r.state, reportStates) || !str(r.hash, 64) || !Array.isArray(r.batches) ||
      !Array.isArray(r.missingCritical) || !Array.isArray(r.sample) || typeof r.missingCriticalTruncated !== 'boolean' || !isGuid(r.createdBy) && r.createdBy !== '00000000-0000-0000-0000-000000000000') fail()
  const batches = r.batches.map(b => {
    if (!object(b) || !isGuid(b.batchId) || !str(b.category, 16) || !count(b.sourceRows) || !count(b.acceptedRows) || !count(b.rejectedRows) || !count(b.duplicateRows)) fail()
    return b as unknown as BatchPin
  })
  const missing = r.missingCritical.map(m => { if (!object(m) || !str(m.field, 32) || !oneOf(m.reason, ['MISSING', 'UNKNOWN'] as const)) fail(); return { ...decodeTarget(m), field: m.field as string, reason: m.reason } })
  let signOff: SignOff | null = null
  if (v.signOff !== null) {
    const s = v.signOff
    if (!object(s) || !isGuid(s.id) || !oneOf(s.decision, decisions) || !oneOf(s.validity, validities) || !str(s.reason, 2000) || !Array.isArray(s.exceptions) || !strings(s.sampleVerified)) fail()
    // A sign-off is effective only on the exact signed report; anything else is shown as invalidated or absent.
    if (s.validity === 'EFFECTIVE' && r.state !== 'SIGNED_OFF') fail()
    signOff = s as unknown as SignOff
  }
  return { report: { ...(r as unknown as Report), counts: decodeCounts(r.counts), batches, missingCritical: missing, sample: r.sample.map(decodeTarget) }, signOff, stale: v.stale,
    openItems: v.openItems as string[], authorities: decodeAuthorities(v.authorities), canSignOff: v.canSignOff, signOffBlockers: v.signOffBlockers as string[],
    decisions: [...decisions], exceptionKinds: [...exceptionKinds], sourceLocator: str(v.sourceLocator) ? v.sourceLocator as string : '' }
}
export function decodeListView(v: unknown, vessel: string): ListView {
  if (!object(v) || v.vesselId !== vessel || !Array.isArray(v.items) || !Array.isArray(v.batches) || typeof v.canCreate !== 'boolean' || !object(v.templateVersions) ||
      !count(v.maxBatches) || !count(v.maxSample)) fail()
  const items = v.items.map(i => {
    if (!object(i) || !isGuid(i.id) || !oneOf(i.state, reportStates) || !oneOf(i.signOffValidity, validities) || i.decision !== null && !oneOf(i.decision, decisions)) fail()
    return { id: i.id as string, state: i.state, revision: i.revision as number, counts: decodeCounts(i.counts), createdAt: String(i.createdAt), decision: i.decision as Summary['decision'],
      signOffValidity: i.signOffValidity }
  })
  const batches = v.batches.map(b => {
    if (!object(b) || !isGuid(b.id) || !oneOf(b.category, batchCategories) || !str(b.state, 32) || !count(b.rowCount) || !str(b.templateVersion, 64)) fail()
    return b as unknown as Candidate
  })
  const versions: Record<string, string> = {}
  for (const [k, value] of Object.entries(v.templateVersions)) { if (!oneOf(k, batchCategories) || !str(value, 64)) fail(); versions[k] = value as string }
  return { vesselId: vessel, items, batches, canCreate: v.canCreate, authorities: decodeAuthorities(v.authorities), templateVersions: versions,
    maxBatches: v.maxBatches as number, maxSample: v.maxSample as number, sourceLocator: str(v.sourceLocator) ? v.sourceLocator as string : '' }
}

const base = (vessel: string) => `vessels/${vessel}/reconciliation-reports`
export const reconciliationApi = {
  list: async (vessel: string, signal: AbortSignal) => decodeListView(await request<unknown>(base(vessel), signal), vessel),
  report: async (vessel: string, id: string, signal: AbortSignal) => decodeReportView(await request<unknown>(`${base(vessel)}/${id}`, signal), vessel),
  create: (vessel: string, id: string, batchIds: string[], sampleSize: number): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: base(vessel), body: { id, batchIds, sampleSize } }),
  signOff: (vessel: string, r: Report, decision: typeof decisions[number], reason: string, exceptions: ReconException[], sampleVerified: string[], authorityId: string): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `${base(vessel)}/${r.id}/sign-off`,
      body: { expectedVersion: r.version, reportHash: r.hash, decision, reason, exceptions, sampleVerified, authorityId } }),
}
