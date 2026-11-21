import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// USER-ADMIN-01 "Kullanıcılar ve yetkiler" client contract (13.03 §9 paragraph:102-106). Closed server shapes only:
// an unknown enum value, an extra member or an impossible combination is a contract failure (502), never coerced.
// The account and its credentials stay in the IdP; the server never returns issuer/subject values.
export const bundleCodes = ['master', 'tug-master', 'dpa-hseq', 'chief-engineer', 'crew-oow', 'shore-ops', 'regulatory-assurance', 'tenant-admin', 'read-only'] as const
export type BundleCode = typeof bundleCodes[number]
export const statuses = ['Active', 'Suspended', 'Revoked'] as const
export type MembershipStatus = typeof statuses[number]
export const statusReasons = ['ConfigurationImport', 'SyntheticFixture', 'AdministrativeSuspension', 'AdministrativeResume', 'AdministrativeRevocation',
  'SecuritySuspension', 'SecurityResume', 'AdminProvisioning'] as const
export const sources = ['ConfigurationImport', 'SyntheticUnverified', 'AdminProvisioned'] as const
export const managedBy = ['Configuration', 'Database', 'None'] as const
export type ManagedBy = typeof managedBy[number]
export const editBlockedReasons = ['self_modification', 'configuration_managed', 'revoked', 'not_manager'] as const
export type EditBlockedReason = typeof editBlockedReasons[number]
export const delegationStates = ['Pending', 'Effective', 'Ineffective', 'Expired', 'Revoked'] as const
export const delegationRevocations = ['AdministrativeRevocation', 'MembershipSuspended', 'MembershipRevoked'] as const
export const vesselKinds = ['Ship', 'Tug'] as const

export type RoleBundle = { code: BundleCode; roleId: string; locator: string; permissions: string[]; grantable: boolean }
export type PermissionOption = { code: string; module: string; decisionAuthority: boolean; delegable: boolean; grantable: boolean }
export type VesselOption = { id: string; code: string; name: string; kind: typeof vesselKinds[number] }
export type CompanyOption = { id: string; code: string; name: string }
export type Catalog = { bundles: RoleBundle[]; permissions: PermissionOption[]; canManage: boolean; canSecuritySuspend: boolean; grantorTenantWideRead: boolean
  grantorTenantWideManage: boolean; vessels: VesselOption[]; companies: CompanyOption[]; delegationReasons: string[]; trustedIssuer: string | null; actorId: string }
export type MemberSummary = { id: string; accountId: string; status: MembershipStatus; statusReason: typeof statusReasons[number]; source: typeof sources[number]
  managedBy: ManagedBy; bundles: BundleCode[]; permissionCount: number; tenantWideRead: boolean; tenantWideManage: boolean; vesselGrantCount: number
  administrator: boolean; self: boolean; statusChangedAt: string; version: number; accessRevision: number; grantRevision: number | null }
export type MemberPage = { items: MemberSummary[]; offset: number; limit: number; hasMore: boolean; total: number }
export type VesselGrant = { vesselId: string; canEdit: boolean }
export type GrantRevision = { revision: number; bundles: BundleCode[]; individualPermissions: string[]; permissionCount: number; tenantWideRead: boolean
  tenantWideManage: boolean; activeCompanyId: string | null; vesselGrantCount: number; grantedBy: string; createdAt: string }
export type MemberAccess = { membership: MemberSummary; individualPermissions: string[]; permissions: string[]; activeCompanyId: string | null
  vesselGrants: VesselGrant[]; editable: boolean; editBlockedReason: EditBlockedReason | null; history: GrantRevision[] }
export type Delegation = { id: string; delegatorMembershipId: string; delegateMembershipId: string; permissions: string[]; validFrom: string; validUntil: string
  status: 'Active' | 'Revoked'; state: typeof delegationStates[number]; revocationReason: typeof delegationRevocations[number] | null; version: number
  reasonCode: string; companyId: string | null; vesselScope: VesselGrant[] }

export type AdminCommand = { key: string; path: string; body: Record<string, unknown> }
export type GrantInput = { bundles: string[]; permissions: string[]; tenantWideRead: boolean; tenantWideManage: boolean; activeCompanyId: string | null
  vesselGrants: VesselGrant[] }

function fail(): never { throw new ApiError(502, 'invalid_user_admin_contract') }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> =>
  object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const text = (v: unknown, max = 1024): v is string => typeof v === 'string' && v.length > 0 && v.length <= max
const int = (v: unknown, min = 0): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const bool = (v: unknown): v is boolean => typeof v === 'boolean'
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const guid = (v: unknown): v is string => isGuid(v)
const time = (v: unknown): v is string => typeof v === 'string' && timestamp(v)
const permission = (v: unknown): v is string => typeof v === 'string' && /^[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+$/.test(v) && v.length <= 64
const list = <T,>(v: unknown, item: (x: unknown) => T, max = 2000): T[] => { if (!Array.isArray(v) || v.length > max) fail(); return v.map(item) }
const strings = (v: unknown, ok: (x: unknown) => boolean, max = 512): string[] => list(v, x => { if (!ok(x)) fail(); return x as string }, max)

function vesselGrant(v: unknown): VesselGrant {
  if (!exact(v, ['vesselId', 'canEdit']) || !guid(v.vesselId) || !bool(v.canEdit)) fail()
  return { vesselId: v.vesselId, canEdit: v.canEdit }
}

export function decodeCatalog(v: unknown): Catalog {
  if (!exact(v, ['bundles', 'permissions', 'canManage', 'canSecuritySuspend', 'grantorTenantWideRead', 'grantorTenantWideManage', 'vessels', 'companies',
    'delegationReasons', 'trustedIssuer', 'actorId'])) fail()
  const bundles = list(v.bundles, b => {
    if (!exact(b, ['code', 'roleId', 'locator', 'permissions', 'grantable']) || !oneOf(b.code, bundleCodes) || !guid(b.roleId) || !text(b.locator, 200) ||
      !bool(b.grantable)) fail()
    return { code: b.code, roleId: b.roleId, locator: b.locator, permissions: strings(b.permissions, permission), grantable: b.grantable }
  }, 32)
  const permissions = list(v.permissions, p => {
    if (!exact(p, ['code', 'module', 'decisionAuthority', 'delegable', 'grantable']) || !permission(p.code) || !text(p.module, 64) ||
      !bool(p.decisionAuthority) || !bool(p.delegable) || !bool(p.grantable)) fail()
    return { code: p.code, module: p.module, decisionAuthority: p.decisionAuthority, delegable: p.delegable, grantable: p.grantable }
  }, 1024)
  const known = new Set(permissions.map(p => p.code))
  // A bundle naming a permission outside the published allow-list is an impossible combination.
  if (bundles.some(b => b.permissions.some(p => !known.has(p)))) fail()
  const vessels = list(v.vessels, x => {
    if (!exact(x, ['id', 'code', 'name', 'kind']) || !guid(x.id) || !text(x.code, 200) || !text(x.name, 400) || !oneOf(x.kind, vesselKinds)) fail()
    return { id: x.id, code: x.code, name: x.name, kind: x.kind }
  }, 500)
  const companies = list(v.companies, x => {
    if (!exact(x, ['id', 'code', 'name']) || !guid(x.id) || !text(x.code, 200) || !text(x.name, 400)) fail()
    return { id: x.id, code: x.code, name: x.name }
  }, 500)
  if (!bool(v.canManage) || !bool(v.canSecuritySuspend) || !bool(v.grantorTenantWideRead) || !bool(v.grantorTenantWideManage) ||
    !(v.trustedIssuer === null || text(v.trustedIssuer)) || !guid(v.actorId)) fail()
  return { bundles, permissions, canManage: v.canManage, canSecuritySuspend: v.canSecuritySuspend, grantorTenantWideRead: v.grantorTenantWideRead,
    grantorTenantWideManage: v.grantorTenantWideManage, vessels, companies, delegationReasons: strings(v.delegationReasons, x => text(x, 32), 32),
    trustedIssuer: v.trustedIssuer as string | null, actorId: v.actorId }
}

const summaryKeys = ['id', 'accountId', 'status', 'statusReason', 'source', 'managedBy', 'bundles', 'permissionCount', 'tenantWideRead', 'tenantWideManage',
  'vesselGrantCount', 'administrator', 'self', 'statusChangedAt', 'version', 'accessRevision', 'grantRevision'] as const
export function decodeSummary(v: unknown): MemberSummary {
  if (!exact(v, summaryKeys) || !guid(v.id) || !guid(v.accountId) || !oneOf(v.status, statuses) || !oneOf(v.statusReason, statusReasons) ||
    !oneOf(v.source, sources) || !oneOf(v.managedBy, managedBy) || !int(v.permissionCount) || !bool(v.tenantWideRead) || !bool(v.tenantWideManage) ||
    !int(v.vesselGrantCount) || !bool(v.administrator) || !bool(v.self) || !time(v.statusChangedAt) || !int(v.version) || !int(v.accessRevision) ||
    !(v.grantRevision === null || int(v.grantRevision, 1))) fail()
  const bundles = strings(v.bundles, x => oneOf(x, bundleCodes), 16) as BundleCode[]
  // Only a database-managed grant carries bundles and a grant revision; an administrator is never inactive.
  if ((v.managedBy === 'Database') !== (v.grantRevision !== null) || v.managedBy !== 'Database' && bundles.length > 0 ||
    v.administrator && v.status !== 'Active' || v.managedBy === 'None' && (v.permissionCount > 0 || v.administrator)) fail()
  return { id: v.id, accountId: v.accountId, status: v.status, statusReason: v.statusReason, source: v.source, managedBy: v.managedBy, bundles,
    permissionCount: v.permissionCount, tenantWideRead: v.tenantWideRead, tenantWideManage: v.tenantWideManage, vesselGrantCount: v.vesselGrantCount,
    administrator: v.administrator, self: v.self, statusChangedAt: v.statusChangedAt, version: v.version, accessRevision: v.accessRevision,
    grantRevision: v.grantRevision as number | null }
}

export function decodePage(v: unknown): MemberPage {
  if (!exact(v, ['items', 'offset', 'limit', 'hasMore', 'total']) || !int(v.offset) || !int(v.limit, 1) || !bool(v.hasMore) || !int(v.total)) fail()
  const items = list(v.items, decodeSummary, 100)
  if (items.length > v.limit) fail()
  return { items, offset: v.offset, limit: v.limit, hasMore: v.hasMore, total: v.total }
}

export function decodeAccess(v: unknown, id?: string): MemberAccess {
  if (!exact(v, ['membership', 'individualPermissions', 'permissions', 'activeCompanyId', 'vesselGrants', 'editable', 'editBlockedReason', 'history']) ||
    !(v.activeCompanyId === null || guid(v.activeCompanyId)) || !bool(v.editable) || !(v.editBlockedReason === null || oneOf(v.editBlockedReason, editBlockedReasons))) fail()
  const membership = decodeSummary(v.membership)
  if (id && membership.id.toLowerCase() !== id.toLowerCase()) fail()
  if (v.editable !== (v.editBlockedReason === null) || membership.self && v.editable) fail()
  const history = list(v.history, h => {
    if (!exact(h, ['revision', 'bundles', 'individualPermissions', 'permissionCount', 'tenantWideRead', 'tenantWideManage', 'activeCompanyId', 'vesselGrantCount',
      'grantedBy', 'createdAt']) || !int(h.revision, 1) || !int(h.permissionCount) || !bool(h.tenantWideRead) || !bool(h.tenantWideManage) ||
      !(h.activeCompanyId === null || guid(h.activeCompanyId)) || !int(h.vesselGrantCount) || !guid(h.grantedBy) || !time(h.createdAt)) fail()
    return { revision: h.revision, bundles: strings(h.bundles, x => oneOf(x, bundleCodes), 16) as BundleCode[],
      individualPermissions: strings(h.individualPermissions, permission), permissionCount: h.permissionCount, tenantWideRead: h.tenantWideRead,
      tenantWideManage: h.tenantWideManage, activeCompanyId: h.activeCompanyId as string | null, vesselGrantCount: h.vesselGrantCount, grantedBy: h.grantedBy,
      createdAt: h.createdAt }
  }, 100)
  return { membership, individualPermissions: strings(v.individualPermissions, permission), permissions: strings(v.permissions, permission),
    activeCompanyId: v.activeCompanyId as string | null, vesselGrants: list(v.vesselGrants, vesselGrant, 64), editable: v.editable,
    editBlockedReason: v.editBlockedReason as EditBlockedReason | null, history }
}

export function decodeDelegations(v: unknown): Delegation[] {
  return list(v, d => {
    if (!exact(d, ['id', 'delegatorMembershipId', 'delegateMembershipId', 'permissions', 'validFrom', 'validUntil', 'status', 'state', 'revocationReason', 'version',
      'reasonCode', 'companyId', 'vesselScope']) || !guid(d.id) || !guid(d.delegatorMembershipId) || !guid(d.delegateMembershipId) || !time(d.validFrom) ||
      !time(d.validUntil) || !oneOf(d.status, ['Active', 'Revoked'] as const) || !oneOf(d.state, delegationStates) ||
      !(d.revocationReason === null || oneOf(d.revocationReason, delegationRevocations)) || !int(d.version) || !text(d.reasonCode, 32) ||
      !(d.companyId === null || guid(d.companyId))) fail()
    if ((d.status === 'Revoked') !== (d.state === 'Revoked' || d.revocationReason !== null) || d.status === 'Revoked' && d.revocationReason === null) fail()
    return { id: d.id, delegatorMembershipId: d.delegatorMembershipId, delegateMembershipId: d.delegateMembershipId, permissions: strings(d.permissions, permission, 32),
      validFrom: d.validFrom, validUntil: d.validUntil, status: d.status, state: d.state, revocationReason: d.revocationReason as Delegation['revocationReason'],
      version: d.version, reasonCode: d.reasonCode, companyId: d.companyId as string | null, vesselScope: list(d.vesselScope, vesselGrant, 64) }
  }, 100)
}

export type ListQuery = { status: string; bundle: string; q: string; offset: number }
export function listPath(query: ListQuery) {
  const p = new URLSearchParams()
  if (query.status) p.set('status', query.status)
  if (query.bundle) p.set('bundle', query.bundle)
  if (query.q.trim()) p.set('q', query.q.trim())
  p.set('offset', String(query.offset)); p.set('limit', '25')
  return `memberships/access?${p.toString()}`
}

const key = () => crypto.randomUUID()
const grantBody = (g: GrantInput) => ({ bundles: g.bundles, permissions: g.permissions, tenantWideRead: g.tenantWideRead, tenantWideManage: g.tenantWideManage,
  activeCompanyId: g.activeCompanyId, vesselGrants: g.vesselGrants.map(v => ({ vesselId: v.vesselId, canEdit: v.canEdit })) })

export const userAdminApi = {
  catalog: async (signal: AbortSignal) => decodeCatalog(await request<unknown>('memberships/catalog', signal)),
  list: async (query: ListQuery, signal: AbortSignal) => decodePage(await request<unknown>(listPath(query), signal)),
  access: async (id: string, signal: AbortSignal) => decodeAccess(await request<unknown>(`memberships/${id}/access`, signal), id),
  delegations: async (id: string, signal: AbortSignal) => decodeDelegations(await request<unknown>(`memberships/${id}/delegations`, signal)),
  create: (issuer: string, subject: string, grant: GrantInput): AdminCommand => ({ key: key(), path: 'memberships', body: { issuer, subject, ...grantBody(grant) } }),
  updateGrant: (id: string, expectedVersion: number, grant: GrantInput): AdminCommand =>
    ({ key: key(), path: `memberships/${id}/grant`, body: { expectedVersion, ...grantBody(grant) } }),
  transition: (id: string, action: 'suspend' | 'resume' | 'revoke' | 'security-suspend', expectedVersion: number): AdminCommand =>
    ({ key: key(), path: `memberships/${id}/${action}`, body: { expectedVersion } }),
  delegate: (sourceId: string, body: { delegateMembershipId: string; permissions: string[]; validFrom: string; validUntil: string; reasonCode: string
    companyId: string | null; vesselScope: VesselGrant[] }): AdminCommand => ({ key: key(), path: `memberships/${sourceId}/delegations`, body: { ...body } }),
  revokeDelegation: (id: string, expectedVersion: number): AdminCommand => ({ key: key(), path: `delegations/${id}/revoke`, body: { expectedVersion } }),
}

// Sends a command and keeps the server's field errors (validation_failed) for inline display.
export async function sendAdmin(command: AdminCommand, csrf: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`/api/v1/${command.path}`, { credentials: 'same-origin', signal, method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: JSON.stringify(command.body) })
  let body: unknown = null
  try { body = await response.json() } catch { /* Proxy errors need not be JSON. */ }
  if (!response.ok) {
    const f = object(body) ? body : {}
    const errors: Record<string, string[]> = {}
    if (object(f.errors)) for (const [k, val] of Object.entries(f.errors)) if (Array.isArray(val)) errors[k] = val.filter((x): x is string => typeof x === 'string')
    throw new ApiError(response.status, typeof f.code === 'string' ? f.code : 'request_failed', errors, typeof f.correlationId === 'string' ? f.correlationId : undefined)
  }
  return body
}

export const shortId = (id: string) => id.slice(0, 8)
// datetime-local (browser local time) → ISO UTC; invalid input yields null (never a guessed time).
export function localToIso(value: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) return null
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? null : d.toISOString()
}
