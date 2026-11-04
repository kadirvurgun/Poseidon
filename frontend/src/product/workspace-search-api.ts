import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { isGuid } from './identifiers'

export type SearchItem = components['schemas']['SearchItemDto']
export type SearchPage = { items: SearchItem[]; offset: number; limit: number; hasMore: boolean }
export const searchPageSize = 25
export const searchMaxOffset = 100000
const destinations: Readonly<Record<string, string>> = {
  'noon-report': '/app/noon-report', 'deck-daily-log': '/app/deck-daily-log', 'engine-daily-log': '/app/engine-daily-log',
  today: '/app/today', companies: '/app/companies', fleet: '/app/fleet',
  sources: '/app/sources', requirements: '/app/requirements', audit: '/app/audit',
  'tug-command': '/app/tug-command', 'tug-job-board': '/app/tug-job-board', 'tug-job-intake': '/app/tug-job-intake',
  'system-health': '/app/system-health',
  'notification-escalation': '/app/notification-escalation',
}
export const isSeparateWorkspaceDestination = (href: string) =>
  href === '/app/noon-report' || href === '/app/deck-daily-log' || href === '/app/engine-daily-log' || /^\/app\/(noon-report|deck-daily-log|engine-daily-log)\/[0-9a-f-]{36}$/.test(href) || href === '/app/tug-command' || href === '/app/tug-job-board' || href === '/app/tug-job-intake' || href === '/app/system-health' || href === '/app/notification-escalation'
const object = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value)
const exact = (value: Record<string, unknown>, keys: string[]) =>
  Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key))
const safeText = (value: unknown, max: number): value is string =>
  typeof value === 'string' && value.trim().length > 0 && value.length <= max &&
  !Array.from(value).some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))
const offsetValid = (value: number) => Number.isSafeInteger(value) && value >= 0 && value <= searchMaxOffset
function invalid(): never { throw new ApiError(502, 'invalid_search_contract') }

// This verifies transport/context consistency; current server permissions still authorize every read.
export function decodeSearchPage(value: unknown, offset: number): SearchPage {
  if (!offsetValid(offset) || !object(value) || !exact(value, ['items', 'offset', 'limit', 'hasMore']) ||
    value.offset !== offset || value.limit !== searchPageSize || typeof value.hasMore !== 'boolean' ||
    !Array.isArray(value.items) || value.items.length > searchPageSize ||
    (value.hasMore && value.items.length !== searchPageSize)) invalid()
  const identities = new Set<string>()
  const items: SearchItem[] = []
  for (const item of value.items) {
    if (!object(item) || !exact(item, ['key', 'href', 'label', 'kind']) ||
      !safeText(item.key, 64) || !safeText(item.label, 267) || typeof item.href !== 'string') invalid()
    if (item.kind === 'Destination') {
      if (!Object.hasOwn(destinations, item.key) || destinations[item.key] !== item.href) invalid()
    } else if (item.kind === 'Ship' || item.kind === 'Tug') {
      if (!isGuid(item.key) || item.href !== `/app/fleet/${item.key}`) invalid()
    } else invalid()
    // A vessel cannot appear twice under different kinds or GUID casing.
    const identity = `${item.kind === 'Destination' ? 'destination' : 'vessel'}:${item.key.toLowerCase()}`
    if (identities.has(identity)) invalid()
    identities.add(identity)
    items.push({ key: item.key, href: item.href, label: item.label, kind: item.kind })
  }
  return { items, offset, limit: searchPageSize, hasMore: value.hasMore }
}

export async function searchWorkspace(query: string, offset: number, signal: AbortSignal): Promise<SearchPage> {
  if (!offsetValid(offset) || query.length > 100 || Array.from(query).some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))) invalid()
  return decodeSearchPage(await request<unknown>(`workspace/search?q=${encodeURIComponent(query)}&offset=${offset}&limit=${searchPageSize}`, signal), offset)
}
