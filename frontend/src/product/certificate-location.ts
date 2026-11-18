import { useCallback, useEffect, useRef, useState } from 'react'
import { isGuid } from './identifiers'
import { temporalStates, type CertificateScope, type CertificateTemporalState } from './certificates-api'
export type CertificateLocation = { page: 'center' | 'renewal'; vessel: string | null; id: string | null; certificateId?: string; scope?: CertificateScope; temporal?: CertificateTemporalState; offset: number; invalid?: boolean }
export function readCertificateLocation(path = window.location.pathname + window.location.search): CertificateLocation {
  const invalid: CertificateLocation = { page: 'center', vessel: null, id: null, offset: 0, invalid: true }, [pathname, query = '', extra] = path.split('?')
  const match = /^\/app\/(certificate-command-center|renewal-workflow)(?:\/([0-9a-f-]{36}))?\/?$/.exec(pathname)
  if (!match || extra !== undefined) return invalid
  const page = match[1] === 'renewal-workflow' ? 'renewal' : 'center', vessel = match[2] ?? null, params = new URLSearchParams(query), id = params.get('id'), certificateId = params.get('certificateId'), scope = params.get('scope'), temporal = params.get('temporalState'), offset = params.get('offset') ?? '0'
  if (vessel !== null && !isGuid(vessel) || (id !== null || certificateId !== null) && !vessel || id !== null && !isGuid(id) || certificateId !== null && (!isGuid(certificateId) || page !== 'renewal' || id !== null) || scope !== null && (page !== 'center' || id !== null || !['Vessel', 'Crew', 'All'].includes(scope)) || temporal !== null && (page !== 'center' || id !== null || !temporalStates.some(state => state === temporal)) || !/^(?:0|[1-9][0-9]*)$/.test(offset) || Number(offset) > 100000 || (id !== null || certificateId !== null) && Number(offset) !== 0 || [...params.keys()].some(key => !['id', 'certificateId', 'scope', 'temporalState', 'offset'].includes(key) || params.getAll(key).length !== 1)) return invalid
  return { page, vessel, id, offset: Number(offset), ...(certificateId ? { certificateId } : {}), ...(scope ? { scope: scope as CertificateScope } : {}), ...(temporal ? { temporal: temporal as CertificateTemporalState } : {}) }
}
export function certificateLocationPath(route: CertificateLocation) {
  const params = new URLSearchParams()
  if (route.id) params.set('id', route.id)
  if (route.certificateId) params.set('certificateId', route.certificateId)
  if (route.scope) params.set('scope', route.scope)
  if (route.temporal) params.set('temporalState', route.temporal)
  if (route.offset) params.set('offset', String(route.offset))
  return `/app/${route.page === 'center' ? 'certificate-command-center' : 'renewal-workflow'}${route.vessel ? `/${route.vessel}` : ''}${params.size ? `?${params}` : ''}`
}
// Entries carry only an index, never tenant/account data or form contents. Nothing is persisted in storage.
export function useCertificateLocation(guard: (action: () => void) => void, onMove: () => void) {
  const [route, setRoute] = useState(() => readCertificateLocation(window.location.pathname + window.location.search))
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
        callbacks.current.onMove(); setRoute(readCertificateLocation(window.location.pathname + window.location.search)); return
      }
      if (!delta) return
      pending = { index, url: window.location.href, delta }
      restoring = true
      window.history.go(-delta)
    }
    window.addEventListener('popstate', pop)
    return () => window.removeEventListener('popstate', pop)
  }, [])
  const move = useCallback((next: CertificateLocation, replace = false) => {
    const url = certificateLocationPath(next)
    if (window.location.pathname + window.location.search !== url) {
      const index = current.current.index + (replace ? 0 : 1)
      window.history[replace ? 'replaceState' : 'pushState']({ poseidonIndex: index }, '', url)
      current.current = { index, url: window.location.href }
    }
    setRoute(next)
  }, [])
  return { route, move }
}
