import { useCallback, useEffect, useRef, useState } from 'react'

export type WorkspaceLocation = { tab: 'companies' | 'fleet' | 'today' | 'sources' | 'requirements'; selected: string | null; profile: boolean; invalid?: boolean }
const idPattern = /^[a-zA-Z0-9-]{1,64}$/
export function loginHref() {
  const path = window.location.pathname
  return `/api/v1/auth/login?returnUrl=${encodeURIComponent(readLocation(path).invalid ? '/app' : path)}`
}
export function readLocation(path = window.location.pathname): WorkspaceLocation {
  if (path === '/app' || path === '/app/') return { tab: 'companies', selected: null, profile: false }
  // Reject encoded separators, malformed encodings and unknown routes without decoding into a new path.
  const match = /^\/app\/(companies|fleet|today|sources|requirements)(?:\/([a-zA-Z0-9-]{1,64})(\/profile)?)?\/?$/.exec(path)
  if (!match || (match[3] && match[1] !== 'fleet')) return { tab: 'fleet', selected: null, profile: false, invalid: true }
  return { tab: match[1] as WorkspaceLocation['tab'], selected: match[2] ?? null, profile: !!match[3] }
}
export function locationPath(route: WorkspaceLocation) {
  return `/app/${route.tab}${route.selected && idPattern.test(route.selected) ? `/${route.selected}${route.profile ? '/profile' : ''}` : ''}`
}

// Entries carry only an index, never tenant/account data or form contents. Nothing is persisted in storage.
export function useWorkspaceLocation(guard: (action: () => void) => void, onMove: () => void) {
  const [route, setRoute] = useState(readLocation)
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
        callbacks.current.onMove(); setRoute(readLocation()); return
      }
      if (!delta) return
      pending = { index, url: window.location.href, delta }
      restoring = true
      window.history.go(-delta)
    }
    window.addEventListener('popstate', pop)
    return () => window.removeEventListener('popstate', pop)
  }, [])
  const move = useCallback((next: WorkspaceLocation, replace = false) => {
    const url = locationPath(next)
    if (window.location.pathname !== url) {
      const index = current.current.index + (replace ? 0 : 1)
      window.history[replace ? 'replaceState' : 'pushState']({ poseidonIndex: index }, '', url)
      current.current = { index, url: window.location.href }
    }
    setRoute(next)
  }, [])
  return { route, move }
}
