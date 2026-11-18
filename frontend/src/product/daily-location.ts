import { useCallback, useEffect, useRef, useState } from 'react'
import { dailyRoutes } from './daily-operations-api'
import { isGuid } from './identifiers'
import type { DailyKind } from './daily-report-fields'
export type DailyLocation = { kind: DailyKind; vessel: string | null; id: string | null; draft?: string; offset: number; invalid?: boolean }
export function readDailyLocation(path = window.location.pathname + window.location.search): DailyLocation {
  const invalid: DailyLocation = { kind: 'Noon', vessel: null, id: null, offset: 0, invalid: true }
  const [pathname, query = '', extra] = path.split('?')
  const match = /^\/app\/(noon-report|deck-daily-log|engine-daily-log)(?:\/([0-9a-f-]{36}))?\/?$/.exec(pathname)
  if (!match || extra !== undefined) return invalid
  const kind = (Object.entries(dailyRoutes).find(([, route]) => route.app === match[1])?.[0]) as DailyKind | undefined
  const params = new URLSearchParams(query), vessel = match[2] ?? null, id = params.get('id'), draft = params.get('draft'), rawOffset = params.get('offset') ?? '0'
  if (!kind || draft !== null && (!vessel || !isGuid(draft) || id !== null || params.has('offset')) || vessel !== null && !isGuid(vessel) || id !== null && (!vessel || !isGuid(id)) || !/^(?:0|[1-9][0-9]*)$/.test(rawOffset) || Number(rawOffset) > 100000 || [...params.keys()].some(key => !['id', 'draft', 'offset'].includes(key) || params.getAll(key).length !== 1)) return invalid
  return { kind, vessel, id, ...(draft !== null ? { draft } : {}), offset: Number(rawOffset) }
}
export function dailyLocationPath(route: DailyLocation) {
  const query = new URLSearchParams()
  if (route.id) query.set('id', route.id)
  else if (route.draft) query.set('draft', route.draft)
  if (route.offset) query.set('offset', String(route.offset))
  return `/app/${dailyRoutes[route.kind].app}${route.vessel ? `/${route.vessel}` : ''}${query.size ? `?${query}` : ''}`
}
// Entries carry only an index, never tenant/account data or form contents. Nothing is persisted in storage.
export function useDailyLocation(guard: (action: () => void) => void, onMove: () => void) {
  const [route, setRoute] = useState(() => readDailyLocation(window.location.pathname + window.location.search))
  const current = useRef({ index: Number(window.history.state?.poseidonIndex ?? 0), url: window.location.href })
  const callbacks = useRef({ guard, onMove })
  useEffect(() => { callbacks.current = { guard, onMove } })
  useEffect(() => {
    window.history.replaceState({ poseidonIndex: current.current.index }, '', window.location.href)
    let restoring = false
    let committed = false
    let pending: { index: number; url: string; delta: number } | null = null
    const pop = () => {
      if (restoring) {
        if (window.location.href !== current.current.url) return
        restoring = false
        const target = pending
        pending = null
        if (target) callbacks.current.guard(() => { committed = true; window.history.go(target.delta) })
        return
      }
      const index = Number(window.history.state?.poseidonIndex ?? current.current.index - 1)
      const delta = index - current.current.index
      if (committed) {
        committed = false
        current.current = { index, url: window.location.href }
        callbacks.current.onMove(); setRoute(readDailyLocation(window.location.pathname + window.location.search)); return
      }
      if (!delta) return
      pending = { index, url: window.location.href, delta }
      restoring = true
      window.history.go(-delta)
    }
    window.addEventListener('popstate', pop)
    return () => window.removeEventListener('popstate', pop)
  }, [])
  const move = useCallback((next: DailyLocation, replace = false) => {
    const url = dailyLocationPath(next)
    if (window.location.pathname + window.location.search !== url) {
      const index = current.current.index + (replace ? 0 : 1)
      window.history[replace ? 'replaceState' : 'pushState']({ poseidonIndex: index }, '', url)
      current.current = { index, url: window.location.href }
    }
    setRoute(next)
  }, [])
  return { route, move }
}
