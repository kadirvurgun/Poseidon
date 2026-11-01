import { ApiError, request } from './api'
import { isGuid } from './identifiers'

export type WorkspaceContext = Readonly<{
  tenantId: string
  actorId: string
  roleContext: { state: 'Configured' | 'Missing' | 'Unavailable'; catalogVersion: string | null; roles: { reference: string; tr: string; en: string }[] }
  vessel: { id: string; code: string; name: string; kind: 'Ship' | 'Tug' } | null
}>
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const text = (v: unknown, max: number): v is string => typeof v === 'string' && v.length > 0 && v.length <= max && v.trim() === v && !/\p{Cc}/u.test(v)
export async function loadWorkspaceContext(tenant: string, actor: string, vessel: string | null, signal: AbortSignal): Promise<WorkspaceContext> {
  if (!isGuid(tenant) || !isGuid(actor) || vessel !== null && !isGuid(vessel)) throw new ApiError(400, 'invalid_context')
  const value = await request<unknown>('workspace/context' + (vessel ? '/' + encodeURIComponent(vessel) : ''), signal)
  if (!object(value) || typeof value.tenantId !== 'string' || value.tenantId.toLowerCase() !== tenant.toLowerCase() ||
      typeof value.actorId !== 'string' || value.actorId.toLowerCase() !== actor.toLowerCase() || !object(value.roleContext))
    throw new ApiError(502, 'invalid_contract')
  const role = value.roleContext
  if (typeof role.state !== 'string' || !['Configured', 'Missing', 'Unavailable'].includes(role.state) || !Array.isArray(role.roles) || role.roles.length > 16 ||
      (role.state === 'Configured' ? !text(role.catalogVersion, 64) || role.roles.length === 0 : role.catalogVersion !== null || role.roles.length !== 0))
    throw new ApiError(502, 'invalid_contract')
  const roles: WorkspaceContext['roleContext']['roles'] = []
  for (const row of role.roles) {
    if (!object(row)) throw new ApiError(502, 'invalid_contract')
    const reference = row.reference
    if (!isGuid(reference) || !text(row.tr, 200) || !text(row.en, 200) || roles.some(r => r.reference === reference.toLowerCase()))
      throw new ApiError(502, 'invalid_contract')
    roles.push({ reference: reference.toLowerCase(), tr: row.tr, en: row.en })
  }
  let selected: WorkspaceContext['vessel'] = null
  if (vessel === null ? value.vessel !== null : !object(value.vessel)) throw new ApiError(502, 'invalid_contract')
  if (object(value.vessel)) {
    const row = value.vessel
    if (!isGuid(row.id) || row.id.toLowerCase() !== vessel?.toLowerCase() || !text(row.code, 64) || !text(row.name, 200) || typeof row.kind !== 'string' || !['Ship', 'Tug'].includes(row.kind))
      throw new ApiError(502, 'invalid_contract')
    selected = { id: row.id, code: row.code, name: row.name, kind: row.kind as 'Ship' | 'Tug' }
  }
  return { tenantId: value.tenantId, actorId: value.actorId, roleContext: { state: role.state as WorkspaceContext['roleContext']['state'], catalogVersion: role.catalogVersion as string | null, roles }, vessel: selected }
}
