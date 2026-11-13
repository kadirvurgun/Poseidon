import { vi } from 'vitest'
import { profileFields, type Profile, type ProfileContent } from './profile-contract'
export const profileVessel = '10000000-0000-0000-0000-000000000001'
export const profileTenant = '10000000-0000-0000-0000-000000000002'
export const profileActor = '10000000-0000-0000-0000-000000000003'
export const profileOwner = profileTenant+'/'+profileActor
export const profileRoute = '/api/v1/vessels/'+profileVessel+'/regulatory-profile'
export const profileContent = (): ProfileContent => ({ ...Object.fromEntries(profileFields.map(([key]) => [key, { state: 'Missing', value: null }])), constructionRecord: null }) as ProfileContent
export const profileRow = (): Profile => ({ tenantId: profileTenant, vessel: { id: profileVessel, companyId: crypto.randomUUID(), code: 'FICTIONAL-PROFILE', name: 'FICTIONAL isolated profile', kind: 'Ship', version: 7, canEdit: true }, version: 12, revision: 1, provenance: 'Synthetic', savedAt: '2026-11-13T00:00:00Z', content: profileContent() })
export const profileSession = () => ({ authenticated: true, canRead: true, setupRequired: false, tenantId: profileTenant, actorId: profileActor, csrfToken: crypto.randomUUID() })
export const profileResponse = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
export function profileTransport() {
  const state = { row: profileRow(), session: profileSession(), sessions: 0, readStatus: 200, writeStatus: 200, historyStatus: 200, loseFirst: false,
    loseHistoryAfterSave: false, writes: [] as { key: string; body: string; path: string; method: string; csrf: string; sessions: number }[],
    pendingSession: null as Promise<Response> | null, file: null as Promise<Response> | null, history: null as Promise<Response> | null, receipt: null as Profile | null }
  const fetch = vi.fn(async (url: string, init?: RequestInit): Promise<Response> => {
    if (url.endsWith('/auth/session')) { state.sessions++; if (state.pendingSession) return state.pendingSession; state.session.csrfToken = crypto.randomUUID(); return profileResponse(state.session) }
    if (url.includes('/files?')) return state.file ?? profileResponse({ items: [], offset: 0, hasMore: false, available: true, canWrite: false, maxBytes: 1024, allowedTypes: ['image/png'] })
    if (url.includes('/history?')) {
      if (state.history) return state.history
      return state.historyStatus === 200 ? profileResponse({ items: [{ revision: state.row.revision, provenance: state.row.provenance, savedAt: state.row.savedAt, content: state.row.content }], offset: 0, limit: 25, hasMore: false }) : profileResponse({}, state.historyStatus)
    }
    if (init?.method === 'PUT') {
      const headers = init.headers as Record<string, string>
      state.writes.push({ key: headers['Idempotency-Key'], body: String(init.body), path: url, method: init.method, csrf: headers['X-CSRF-TOKEN'], sessions: state.sessions })
      if (state.writeStatus !== 200) return profileResponse({ code: state.writeStatus === 409 ? 'version_conflict' : 'permission_denied' }, state.writeStatus)
      if (!state.receipt) {
        const body = JSON.parse(String(init.body))
        state.row = { ...state.row, revision: state.row.revision+1, version: state.row.version+1, provenance: 'Missing', content: body.content }
        state.receipt = structuredClone(state.row)
      }
      if (state.loseFirst && state.writes.length === 1) throw new TypeError('controlled response loss')
      if (state.loseHistoryAfterSave) state.historyStatus = 503
      return profileResponse(state.receipt)
    }
    return state.readStatus === 200 ? profileResponse(state.row) : profileResponse({}, state.readStatus)
  })
  return { state, fetch }
}
