import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { UserAdminWorkspace, readUserAdminRoute } from './UserAdminWorkspace'
import { decodeAccess, decodeCatalog, decodeInvitationIssue, decodeSummary, listPath } from './user-admin-api'
import { changeAppLanguage } from '../i18n'

// USER-ADMIN-01 UI. Synthetic identities only; no IdP account, credential or verified grant is fabricated.
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555'
const memberId = '11111111-1111-4111-8111-111111111111', accountId = '22222222-2222-4222-8222-222222222222'
const vesselId = '33333333-3333-4333-8333-333333333333', companyId = '66666666-6666-4666-8666-666666666666'
const at = '2026-11-21T08:00:00Z'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const catalog = (patch: Record<string, unknown> = {}) => ({
  bundles: [
    { code: 'read-only', roleId: '77777777-7777-4777-8777-777777777777', locator: '13.03 paragraph:11 (least privilege)', permissions: ['directory.read', 'crew.read'], grantable: true },
    { code: 'master', roleId: '88888888-8888-4888-8888-888888888888', locator: '13.03 §2 paragraph:25-48', permissions: ['directory.read', 'departure.readiness.read'], grantable: false }],
  permissions: [
    { code: 'crew.read', module: 'crew', decisionAuthority: false, delegable: true, grantable: true },
    { code: 'departure.decide', module: 'departure', decisionAuthority: true, delegable: true, grantable: false },
    { code: 'departure.readiness.read', module: 'departure', decisionAuthority: false, delegable: true, grantable: false },
    { code: 'directory.read', module: 'directory', decisionAuthority: false, delegable: false, grantable: true }],
  canManage: true, canSecuritySuspend: false, grantorTenantWideRead: false, grantorTenantWideManage: true,
  vessels: [{ id: vesselId, code: 'DEMO-SHIP', name: 'FICTIONAL ship', kind: 'Ship' }], companies: [{ id: companyId, code: 'DEMO', name: 'FICTIONAL company' }],
  delegationReasons: ['PlannedAbsence', 'Handover'], trustedIssuer: 'https://idp.example.invalid', actorId, ...patch })
const summary = (patch: Record<string, unknown> = {}) => ({ id: memberId, accountId, status: 'Active', statusReason: 'AdminProvisioning', source: 'AdminProvisioned',
  managedBy: 'Database', bundles: ['read-only'], permissionCount: 2, tenantWideRead: false, tenantWideManage: false, vesselGrantCount: 1, administrator: false, self: false,
  statusChangedAt: at, version: 7, accessRevision: 0, grantRevision: 1, invitationState: null, invitationExpiresAt: null, invitationSubjectExpected: false, ...patch })
const access = (patch: Record<string, unknown> = {}, member: Record<string, unknown> = {}) => ({ membership: summary(member), individualPermissions: [],
  permissions: ['crew.read', 'directory.read'], activeCompanyId: null, vesselGrants: [{ vesselId, canEdit: false }], editable: true, editBlockedReason: null,
  history: [{ revision: 1, bundles: ['read-only'], individualPermissions: [], permissionCount: 2, tenantWideRead: false, tenantWideManage: false, activeCompanyId: null,
    vesselGrantCount: 1, grantedBy: actorId, createdAt: at }], ...patch })
const fetcher = vi.fn()
let posts: { url: string; init: RequestInit }[] = []
let catalogStatus = 200, detail: unknown = access(), postReply: () => Response = () => json(access())
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  posts = []; catalogStatus = 200; detail = access(); postReply = () => json(access())
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST') { posts.push({ url, init }); return postReply() }
    if (url.endsWith('memberships/catalog')) return catalogStatus === 200 ? json(catalog()) : json({ code: 'permission_denied' }, catalogStatus)
    if (url.includes('memberships/access?')) return json({ items: [summary()], offset: 0, limit: 25, hasMore: false, total: 1 })
    if (url.endsWith(`memberships/${memberId}/access`)) return json(detail)
    if (url.endsWith(`memberships/${memberId}/delegations`)) return json([])
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
  history.replaceState(null, '', '/app/user-admin')
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('rejects unknown enum values, extra members and impossible combinations', () => {
  expect(() => decodeSummary(summary({ status: 'Dormant' }))).toThrow()
  expect(() => decodeSummary({ ...summary(), issuer: 'https://idp.example.invalid' })).toThrow()
  expect(() => decodeSummary(summary({ managedBy: 'Configuration' }))).toThrow() // configuration grants carry no grant revision
  expect(() => decodeSummary(summary({ administrator: true, status: 'Suspended' }))).toThrow()
  expect(() => decodeCatalog(catalog({ bundles: [{ ...catalog().bundles[0], code: 'superuser' }] }))).toThrow()
  expect(() => decodeCatalog(catalog({ bundles: [{ ...catalog().bundles[0], permissions: ['hidden.permission'] }] }))).toThrow()
  expect(() => decodeAccess(access({ editable: true, editBlockedReason: 'self_modification' }))).toThrow()
  expect(() => decodeAccess(access({}, { id: '99999999-9999-4999-8999-999999999999' }), memberId)).toThrow()
  expect(decodeAccess(access(), memberId).membership.bundles).toEqual(['read-only'])
})

it('reads closed routes and builds the list query', () => {
  expect(readUserAdminRoute('/app/user-admin?status=Suspended&bundle=master').invalid).toBe(false)
  expect(readUserAdminRoute('/app/user-admin?status=Dormant').invalid).toBe(true)
  expect(readUserAdminRoute('/app/user-admin?extra=1').invalid).toBe(true)
  expect(readUserAdminRoute(`/app/user-admin/${memberId}`)).toEqual({ view: 'detail', id: memberId, invalid: false })
  expect(readUserAdminRoute('/app/user-admin/new').view).toBe('new')
  expect(readUserAdminRoute('/app/user-admin/not-a-guid').invalid).toBe(true)
  expect(listPath({ status: 'Active', bundle: 'master', q: ' abc ', offset: 25 })).toBe('memberships/access?status=Active&bundle=master&q=abc&offset=25&limit=25')
})

it('shows only a forbidden message to a non-administrator and no permission names', async () => {
  catalogStatus = 403
  render(<UserAdminWorkspace/>)
  expect(await screen.findByText('You are not authorized to administer memberships.')).toBeTruthy()
  expect(document.body.textContent).not.toContain('crew.read')
  expect(document.body.textContent).not.toContain('directory.read')
  expect(fetcher.mock.calls.some(([url]) => String(url).includes('memberships/access'))).toBe(false)
})

it('lists members and sends the selected filters as the server query', async () => {
  const user = userEvent.setup()
  render(<UserAdminWorkspace/>)
  const list = await screen.findByRole('region', { name: 'Members' })
  expect(within(list).getByText('22222222')).toBeTruthy()
  expect(within(list).getByText('Managed here')).toBeTruthy()
  const filters = screen.getByRole('search', { name: 'Search' })
  await user.selectOptions(within(filters).getByLabelText('Status'), 'Suspended')
  await user.selectOptions(within(filters).getByLabelText('Role bundle'), 'master')
  await user.type(within(filters).getByRole('searchbox'), 'abc')
  await user.click(within(filters).getByRole('button', { name: 'Apply' }))
  await waitFor(() => expect(fetcher.mock.calls.some(([url]) => String(url).endsWith('memberships/access?status=Suspended&bundle=master&q=abc&offset=0&limit=25'))).toBe(true))
  expect(window.location.search).toBe('?status=Suspended&bundle=master&q=abc')
})

it('creates a membership with only grantable bundles, CSRF and an idempotency key, then shows field errors', async () => {
  const user = userEvent.setup()
  history.replaceState(null, '', '/app/user-admin/new')
  postReply = () => json({ code: 'validation_failed', errors: { permissions: ['exceeds_grantor_authority'] } }, 400)
  render(<UserAdminWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Create membership' })
  expect((within(form).getByLabelText('IdP issuer') as HTMLInputElement).value).toBe('https://idp.example.invalid')
  expect((within(form).getByLabelText(/^Master/) as HTMLInputElement).disabled).toBe(true)
  expect((within(form).getByLabelText(/departure\.decide/) as HTMLInputElement).disabled).toBe(true)
  await user.type(within(form).getByLabelText('Expected IdP subject (optional)'), 'synthetic-subject')
  await user.click(within(form).getByLabelText(/^Read-only/))
  await user.click(within(form).getByLabelText(/DEMO-SHIP/))
  await user.click(within(form).getByRole('button', { name: 'Create membership' }))
  await waitFor(() => expect(posts).toHaveLength(1))
  const sent = posts[0]
  expect(sent.url).toBe('/api/v1/memberships')
  const headers = sent.init.headers as Record<string, string>
  expect(headers['X-CSRF-TOKEN']).toBe('synthetic-only')
  expect(headers['Idempotency-Key'].length).toBeGreaterThanOrEqual(16)
  expect(JSON.parse(String(sent.init.body))).toEqual({ issuer: 'https://idp.example.invalid', subject: 'synthetic-subject', bundles: ['read-only'], permissions: [],
    tenantWideRead: false, tenantWideManage: false, activeCompanyId: null, vesselGrants: [{ vesselId, canEdit: false }] })
  await waitFor(() => expect(document.querySelector('[data-notice=refused]')).not.toBeNull())
  expect(document.querySelector('[data-notice]')!.textContent).toContain('Permissions: Exceeds your own authority')
})

it('hides the grant editor for the own membership and explains why', async () => {
  history.replaceState(null, '', `/app/user-admin/${memberId}`)
  detail = access({ editable: false, editBlockedReason: 'self_modification' }, { self: true })
  render(<UserAdminWorkspace/>)
  expect(await screen.findByText('This is your own membership. Another administrator must change it.')).toBeTruthy()
  expect(screen.queryByRole('button', { name: 'Edit grant' })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Suspend' })).toBeNull()
})

it('shows a conflict with a reload action when the grant revision is stale', async () => {
  const user = userEvent.setup()
  history.replaceState(null, '', `/app/user-admin/${memberId}`)
  postReply = () => json({ code: 'version_conflict' }, 409)
  render(<UserAdminWorkspace/>)
  await user.click(await screen.findByRole('button', { name: 'Edit grant' }))
  await user.click(screen.getByRole('button', { name: 'Save grant' }))
  await waitFor(() => expect(posts).toHaveLength(1))
  expect(posts[0].url).toBe(`/api/v1/memberships/${memberId}/grant`)
  expect(JSON.parse(String(posts[0].init.body)).expectedVersion).toBe(1)
  await waitFor(() => expect(document.querySelector('[data-notice=conflict]')).not.toBeNull())
  expect(document.querySelector('[data-code]')?.getAttribute('data-code')).toBe('version_conflict')
  expect(screen.getByRole('button', { name: 'Reload current state' })).toBeTruthy()
})

// USER-ADMIN-02: invitation state, one-time link and reissue. The token below is synthetic and grants nothing.
const token = 'pi1.55555555555545558555555555555555.' + 'A'.repeat(43)
const invited = (patch: Record<string, unknown> = {}) => summary({ status: 'Invited', statusReason: 'AdminInvitation', invitationState: 'Pending',
  invitationExpiresAt: '2026-11-28T08:00:00Z', ...patch })

it('decodes invitation states and the one-time token strictly', () => {
  expect(decodeSummary(invited()).invitationState).toBe('Pending')
  expect(() => decodeSummary(invited({ invitationState: null, invitationExpiresAt: null }))).toThrow() // an invited membership has its invitation
  expect(() => decodeSummary(invited({ invitationState: 'Lost' }))).toThrow()
  expect(() => decodeSummary(invited({ source: 'ConfigurationImport', managedBy: 'Configuration', grantRevision: null, bundles: [] }))).toThrow()
  expect(() => decodeSummary(summary({ invitationSubjectExpected: true }))).toThrow()
  const issue = decodeInvitationIssue({ membership: access({}, invited()), invitationToken: token, expiresAt: '2026-11-28T08:00:00Z' })
  expect(issue.token).toBe(token)
  expect(decodeInvitationIssue({ membership: access({}, invited()), invitationToken: null, expiresAt: null }).token).toBeNull()
  expect(() => decodeInvitationIssue({ membership: access({}, invited()), invitationToken: 'pi1.short', expiresAt: '2026-11-28T08:00:00Z' })).toThrow()
  expect(() => decodeInvitationIssue({ membership: access({}, invited()), invitationToken: token, expiresAt: null })).toThrow()
})

it('invites without a subject, then shows the one-time link with copy and never grants before acceptance', async () => {
  const user = userEvent.setup()
  const writeText = vi.fn(async () => undefined)
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
  history.replaceState(null, '', '/app/user-admin/new')
  detail = access({}, invited())
  postReply = () => json({ membership: access({}, invited()), invitationToken: token, expiresAt: '2026-11-28T08:00:00Z' })
  render(<UserAdminWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Create membership' })
  await user.click(within(form).getByLabelText(/^Read-only/))
  await user.click(within(form).getByLabelText('Tenant-wide manage'))
  await user.click(within(form).getByRole('button', { name: 'Create membership' }))
  await waitFor(() => expect(posts).toHaveLength(1))
  expect(JSON.parse(String(posts[0].init.body)).subject).toBeNull()
  await waitFor(() => expect(window.location.pathname).toBe(`/app/user-admin/${memberId}`))
  const link = await screen.findByRole('textbox', { name: 'Invitation link' }) as HTMLInputElement
  expect(link.value).toBe(`${window.location.origin}/app/accept-invitation?token=${encodeURIComponent(token)}`)
  expect(screen.getByText('Invitation pending')).toBeTruthy()
  expect(document.querySelector('[data-status=Invited]')).not.toBeNull()
  await user.click(screen.getByRole('button', { name: 'Copy invitation link' }))
  expect(writeText).toHaveBeenCalledWith(link.value)
  expect(await screen.findByText('Invitation link copied.')).toBeTruthy()
  expect(screen.queryByRole('button', { name: 'Suspend' })).toBeNull()
  expect(screen.getByRole('button', { name: 'Revoke invitation' })).toBeTruthy()
})

it('reissues an expired invitation with the membership version and hides the old link', async () => {
  const user = userEvent.setup()
  history.replaceState(null, '', `/app/user-admin/${memberId}`)
  detail = access({}, invited({ invitationState: 'Expired' }))
  postReply = () => json({ membership: access({}, invited()), invitationToken: token, expiresAt: '2026-11-28T08:00:00Z' })
  render(<UserAdminWorkspace/>)
  expect(await screen.findByText('Invitation expired')).toBeTruthy()
  expect(screen.queryByRole('textbox', { name: 'Invitation link' })).toBeNull() // the server keeps only a hash; nothing to show
  await user.click(screen.getByRole('button', { name: 'Create a new invitation link' }))
  await waitFor(() => expect(posts).toHaveLength(1))
  expect(posts[0].url).toBe(`/api/v1/memberships/${memberId}/invitation`)
  expect(JSON.parse(String(posts[0].init.body))).toEqual({ expectedVersion: 7 })
  expect(((await screen.findByRole('textbox', { name: 'Invitation link' })) as HTMLInputElement).value).toContain(encodeURIComponent(token))
})
