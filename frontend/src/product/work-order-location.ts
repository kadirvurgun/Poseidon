import { useCallback, useEffect, useRef, useState } from 'react'
import { isGuid } from './identifiers'
export type WorkOrderPlanSelector = { id: string; revision: number }
export type WorkOrderLocation = { vessel: string | null; id: string | null; offset: number; plan?: WorkOrderPlanSelector; invalid?: boolean }
export function readWorkOrderLocation(path = window.location.pathname + window.location.search): WorkOrderLocation {
  const invalid: WorkOrderLocation = { vessel: null, id: null, offset: 0, invalid: true }
  const [pathname, query = '', extra] = path.split('?'), match = /^\/app\/work-order-center(?:\/([0-9a-f-]{36}))?\/?$/.exec(pathname)
  if (!match || extra !== undefined) return invalid
  const vessel = match[1] ?? null, params = new URLSearchParams(query), id = params.get('id'), offset = params.get('offset') ?? '0', plan = params.get('planId'), revision = params.get('planRevision'), hasPlan = plan !== null || revision !== null
  if (vessel !== null && !isGuid(vessel) || id !== null && (!vessel || !isGuid(id)) || !/^(?:0|[1-9][0-9]*)$/.test(offset) || Number(offset) > 100000 || id !== null && Number(offset) !== 0 || [...params.keys()].some(key => !['id', 'offset', 'planId', 'planRevision'].includes(key) || params.getAll(key).length !== 1)) return invalid
  if (hasPlan && (!vessel || id !== null || Number(offset) !== 0 || !isGuid(plan) || revision === null || !/^[1-9][0-9]*$/.test(revision) || !Number.isSafeInteger(Number(revision)))) return invalid
  return { vessel, id, offset: Number(offset), ...(hasPlan ? { plan: { id: plan!, revision: Number(revision) } } : {}) }
}
export function workOrderLocationPath(route: WorkOrderLocation) {
  const params = new URLSearchParams()
  if (route.id) params.set('id', route.id)
  if (route.offset) params.set('offset', String(route.offset))
  if (route.plan) { params.set('planId', route.plan.id); params.set('planRevision', String(route.plan.revision)) }
  return '/app/work-order-center' + (route.vessel ? '/' + route.vessel : '') + (params.size ? '?' + params : '')
}
// History entries contain an index only, never form values, identity or grants.
export function useWorkOrderLocation(guard: (action: () => void) => void, onMove: () => void) {
  const [route, setRoute] = useState(() => readWorkOrderLocation())
  const current = useRef({ index: Number(window.history.state?.poseidonIndex ?? 0), url: window.location.href }), callbacks = useRef({ guard, onMove })
  useEffect(() => { callbacks.current = { guard, onMove } })
  useEffect(() => {
    window.history.replaceState({ poseidonIndex: current.current.index }, '', window.location.href)
    let restoring = false, committed = false, pending: { delta: number } | null = null
    const pop = () => {
      if (restoring) { if (window.location.href !== current.current.url) return; restoring = false; const target = pending; pending = null; if (target) callbacks.current.guard(() => { committed = true; window.history.go(target.delta) }); return }
      const index = Number(window.history.state?.poseidonIndex ?? current.current.index - 1), delta = index - current.current.index
      if (committed) { committed = false; current.current = { index, url: window.location.href }; callbacks.current.onMove(); setRoute(readWorkOrderLocation()); return }
      if (!delta) return
      pending = { delta }; restoring = true; window.history.go(-delta)
    }
    window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop)
  }, [])
  const move = useCallback((next: WorkOrderLocation, replace = false) => {
    const path = workOrderLocationPath(next)
    if (window.location.pathname + window.location.search !== path) { const index = current.current.index + (replace ? 0 : 1); window.history[replace ? 'replaceState' : 'pushState']({ poseidonIndex: index }, '', path); current.current = { index, url: window.location.href } }
    setRoute(next)
  }, [])
  return { route, move }
}
