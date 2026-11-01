import { request } from './api'

export type PlanInputs = { plannedStart: string | null; plannedEnd: string | null; assignedTugs: string[] }
export type Plan = { jobId: string; revision: number; version: number | null; requestRevision: number; policyVersion: string; sourceVersion: string
  inputs: PlanInputs; operation: string; provenance: string; savedAt: string; replayed: boolean }
export type PlanChange = { operation: 'Window' | 'Assign' | 'Remove' | 'Reassign'; plannedStart?: string | null; plannedEnd?: string | null; tugId?: string; replacementTugId?: string }
export type PlanCommand = { baseVersion: number | null; requestVersion: number; policyVersion: string; change: PlanChange }
export type PlanAttempt = { key: string; body: PlanCommand }
export type PlanningEvidence = { availableBollardPull: string | null; capacityState: string; readiness: string; conflictState: string; coverage: string
  bp: { tugId: string; state: string; declaredValue: string | null; source: string | null; policyVersion: string | null; sourceRevision: number | null; observationId: string | null; observedAt: string | null; freshness: string; provenance: string }[]
  conflicts: { jobId: string; revision: number; relation: string; source: string; observedAt: string }[] }
export type PlanningOptions = { state: string; policyVersion: string | null; canWrite: boolean; tugs: { id: string; code: string }[]; types: { code: string; tr: string; en: string }[]; offline: string }
export type PlanHistory = { items: Pick<Plan, 'revision' | 'requestRevision' | 'operation' | 'sourceVersion' | 'policyVersion' | 'provenance' | 'savedAt'>[]; coverage: string }
const root = (id: string, vessel: string, suffix = '') => `tug-jobs/${id}/plan${suffix}?vessel=${vessel}`
export const planningApi = {
  detail: (id: string, vessel: string, signal: AbortSignal) => request<{ state: string; requestState?: string; plan: Plan | null; evidence: PlanningEvidence | null }>(root(id, vessel), signal),
  options: (id: string, vessel: string, signal: AbortSignal) => request<PlanningOptions>(root(id, vessel, '/options'), signal),
  history: (id: string, vessel: string, signal: AbortSignal) => request<PlanHistory>(root(id, vessel, '/history'), signal),
  revision: (id: string, vessel: string, revision: number, signal: AbortSignal) => request<Plan>(root(id, vessel, `/history/${revision}`), signal),
  save: (id: string, vessel: string, attempt: PlanAttempt, csrf: string, signal: AbortSignal) => request<Plan>(root(id, vessel), signal, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify(attempt.body),
  }),
}
