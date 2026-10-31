// OPS-DASH-UI-01 — user contract 2026-10-31; docs/hourly-automation.md,
// docs/nightly-automation.md. Local development operations only.
export const profiles = ['esra-pro', 'kadir-plus'] as const
export type ProfileId = typeof profiles[number]
export type WindowData = { usedPercent: number; windowDurationMins: number; resetsAt: number }
export type Profile = {
  state: 'known' | 'busy' | 'unavailable' | 'reconcile_required'; ready: boolean
  plan?: string; windows?: WindowData[]; lastKnown?: WindowData[]
}
export type OpsStatus = {
  schemaVersion: 1; generatedAt: string
  quota: { timestamp: string | null; next_check: string | null; selected: string | null; profiles: Record<ProfileId, Profile> }
  coordinator: { enabled: boolean; status: string; modelCalls: number; maxCalls: number; maxJobs: number; deadline: number | null
    jobs: { id: string; status: string; phase?: string; profile?: string; commit?: string; prUrl?: string; reason?: string; calls?: { profile: string; phase: string; usage?: Record<string, number> }[] }[] }
  activities: { id: string; profile: string; taskId: string; role: string; status: string; startedAt: string | null; finishedAt?: string; usage?: Record<string, number> }[]
  events: { id: string; at: string; type: string; profile?: string; taskId?: string; message?: string; from?: string; to?: string; usedPercent?: number; windowDurationMins?: number; resetAt?: number }[]
  alarms: { id: string; profile?: string; windowDurationMins?: number; usedPercent?: number; resetAt?: number; resumeAt?: number; state: 'armed' | 'waiting' | 'eligible' | 'unknown'; reason?: string }[]
  project: { completionPercent: number; acceptedStoryPoints: number; totalStoryPoints: number; acceptedTasks: number; totalTasks: number; stage: string }
}

type Check = (value: unknown) => boolean
const obj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const str: Check = v => typeof v === 'string'
const num: Check = v => typeof v === 'number' && Number.isFinite(v) && v >= 0
const percent: Check = v => num(v) && (v as number) <= 100
const bool: Check = v => typeof v === 'boolean'
const date: Check = v => typeof v === 'string' && /Z$/.test(v) && Number.isFinite(Date.parse(v))
const nullable = (check: Check): Check => v => v === null || check(v)
const array = (check: Check): Check => v => Array.isArray(v) && v.every(check)
const oneOf = (...values: string[]): Check => v => typeof v === 'string' && values.includes(v)
function shape(required: Record<string, Check>, optional: Record<string, Check> = {}): Check {
  return v => obj(v) && Object.entries(required).every(([key, check]) => check(v[key])) &&
    Object.entries(optional).every(([key, check]) => v[key] === undefined || check(v[key]))
}
const usage: Check = v => obj(v) && Object.values(v).every(num)
const windowData = shape({ usedPercent: percent, windowDurationMins: v => num(v) && (v as number) > 0, resetsAt: num })
const profile = shape({ state: oneOf('known', 'busy', 'unavailable', 'reconcile_required'), ready: bool },
  { plan: str, windows: array(windowData), lastKnown: array(windowData) })
const check = shape({
  schemaVersion: v => v === 1, generatedAt: date,
  quota: shape({ timestamp: nullable(date), next_check: nullable(date), selected: nullable(str), profiles: shape({ 'esra-pro': profile, 'kadir-plus': profile }) }),
  coordinator: shape({ enabled: bool, status: str, modelCalls: num, maxCalls: num, maxJobs: num, deadline: nullable(num), jobs: array(shape({ id: str, status: str },
    { phase: str, profile: str, commit: str, prUrl: str, reason: str, calls: array(shape({ profile: str, phase: str }, { usage })) })) }),
  activities: array(shape({ id: str, profile: str, taskId: str, role: str, status: str, startedAt: nullable(date) }, { finishedAt: date, usage })),
  events: array(shape({ id: str, at: date, type: str }, { profile: str, taskId: str, message: str, from: str, to: str, usedPercent: percent, windowDurationMins: num, resetAt: num })),
  alarms: array(shape({ id: str, state: oneOf('armed', 'waiting', 'eligible', 'unknown') }, { profile: str, windowDurationMins: num, usedPercent: percent, resetAt: num, resumeAt: num, reason: str })),
  project: shape({ completionPercent: percent, acceptedStoryPoints: num, totalStoryPoints: num, acceptedTasks: num, totalTasks: num, stage: str }),
})
export function parseStatus(value: unknown): OpsStatus {
  if (!check(value)) throw new Error('Invalid ops status schema')
  return value as OpsStatus
}
export const profileName = (id: string) => id === 'esra-pro' ? 'Esra Pro' : id === 'kadir-plus' ? 'Kadir Plus' : id
export function quotaFresh(data: OpsStatus, now: number) {
  const stamp = Date.parse(data.quota.timestamp ?? '')
  return Number.isFinite(stamp) && now >= stamp && now - stamp <= 75 * 60_000
}
