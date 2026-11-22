import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { localizedCopy, localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { ApiError, session as readSession, type Session } from './api'
import { isGuid } from './identifiers'
import { useOnline } from './UatWorkspace'
import './user-admin.css'
import { bundleCodes, decodeInvitationIssue, invitationLink, localToIso, plausibleEmail, sendAdmin, shortId, statuses, userAdminApi, type AdminCommand, type Catalog,
  type Delegation, type GrantInput, type InvitationSettings, type ListQuery, type MemberAccess, type MemberPage, type MemberSummary, type VesselGrant } from './user-admin-api'

// USER-ADMIN-01 "Kullanıcılar ve yetkiler" (13.03 §9 paragraph:102-106). Server authority only: the page renders
// nothing about permissions until the membership catalog read succeeds, never grants in the browser, and keeps the
// IdP account (credentials, MFA) outside POSEIDON. Membership changes take effect on the member's next request.
export const userAdminCopy = localizedCopy('UserAdmin_tsx_copy')
type Copy = (typeof userAdminCopy)[AppLocale]
const word = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key
const base = '/app/user-admin'
// USER-ADMIN-02: a freshly issued invitation link, kept only in this page's memory (never stored or logged).
type IssuedLink = { membershipId: string; link: string; expiresAt: string }

export type UserAdminRoute = { view: 'list'; query: ListQuery; invalid: boolean } | { view: 'new'; invalid: boolean } | { view: 'detail'; id: string; invalid: boolean }
export function readUserAdminRoute(href: string): UserAdminRoute {
  const url = new URL(href, 'http://local'), p = url.searchParams
  const m = /^\/app\/user-admin(?:\/([^/]+))?\/?$/.exec(url.pathname)
  const empty: ListQuery = { status: '', bundle: '', q: '', offset: 0 }
  if (!m) return { view: 'list', query: empty, invalid: true }
  if (m[1] === 'new') return { view: 'new', invalid: url.search !== '' }
  if (m[1] !== undefined) return isGuid(m[1]) ? { view: 'detail', id: m[1].toLowerCase(), invalid: url.search !== '' } : { view: 'list', query: empty, invalid: true }
  const status = p.get('status') ?? '', bundle = p.get('bundle') ?? '', q = p.get('q') ?? '', offsetText = p.get('offset') ?? '0'
  const invalid = [...p.keys()].some(k => !['status', 'bundle', 'q', 'offset'].includes(k) || p.getAll(k).length !== 1) ||
    status !== '' && !(statuses as readonly string[]).includes(status) || bundle !== '' && !(bundleCodes as readonly string[]).includes(bundle) ||
    !/^\d{1,6}$/.test(offsetText) || q.length > 255
  return { view: 'list', query: invalid ? empty : { status, bundle, q, offset: Number(offsetText) }, invalid }
}
export function listHref(q: ListQuery) {
  const p = new URLSearchParams()
  if (q.status) p.set('status', q.status)
  if (q.bundle) p.set('bundle', q.bundle)
  if (q.q.trim()) p.set('q', q.q.trim())
  if (q.offset) p.set('offset', String(q.offset))
  const s = p.toString()
  return s ? `${base}?${s}` : base
}

type Notice = '' | 'done' | 'conflict' | 'refused' | 'denied' | 'failed' | 'uncertain'
function useAdminCommand(user: Session | null, offline: boolean, onDone: (body: unknown) => void) {
  const [busy, setBusy] = useState(false), [notice, setNotice] = useState<Notice>(''), [code, setCode] = useState('')
  const [errors, setErrors] = useState<Record<string, string[]>>({}), [retained, setRetained] = useState<AdminCommand | null>(null)
  const lock = useRef(false), mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const run = useCallback(async (command: AdminCommand) => {
    if (lock.current || offline || !navigator.onLine) return
    lock.current = true; setBusy(true); setNotice(''); setCode(''); setErrors({}); setRetained(command)
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000)
    let sent = false
    try {
      const s = await readSession(c.signal)
      if (!s.authenticated || !s.csrfToken || user && `${s.tenantId}/${s.actorId}` !== `${user.tenantId}/${user.actorId}`) throw new ApiError(403, 'identity_boundary_changed')
      sent = true
      const body = await sendAdmin(command, s.csrfToken, c.signal)
      if (!mounted.current) return
      setRetained(null); setNotice('done'); onDone(body)
    } catch (e) {
      if (!mounted.current) return
      const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status)
      if (!sent || definite) setRetained(null)
      setCode(e instanceof ApiError ? e.code : ''); setErrors(e instanceof ApiError ? e.errors : {})
      setNotice(sent && !definite ? 'uncertain' : e instanceof ApiError && e.status === 409 && ['version_conflict', 'conflict_changed', 'lineage_conflict'].includes(e.code) ? 'conflict'
        : e instanceof ApiError && [400, 409, 422].includes(e.status) ? 'refused' : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'failed')
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false) }
  }, [offline, user, onDone])
  return { run, busy, notice, code, errors, retained, clear: useCallback(() => { setNotice(''); setCode(''); setErrors({}) }, []) }
}
type Cmd = ReturnType<typeof useAdminCommand>

function Notice({ t, cmd, offline, reload }: { t: Copy; cmd: Cmd; offline: boolean; reload: () => void }) {
  if (!cmd.notice) return null
  const fields = Object.entries(cmd.errors)
  return <div role={cmd.notice === 'done' ? 'status' : 'alert'} data-notice={cmd.notice}>
    <p>{word(t, 'notice_' + cmd.notice)}{cmd.code && cmd.notice !== 'done' && <> · <span data-code={cmd.code}>{word(t, 'code_' + cmd.code)}</span></>}</p>
    {fields.length > 0 && <ul aria-label={t.code_validation_failed}>{fields.map(([field, codes]) =>
      <li key={field} data-field={field}>{word(t, 'field_' + field)}: {codes.map(c => word(t, 'err_' + c)).join(', ')}</li>)}</ul>}
    {cmd.notice === 'uncertain' && cmd.retained && <Button type="button" disabled={cmd.busy || offline} onClick={() => void cmd.run(cmd.retained!)}>{t.retry}</Button>}
    {cmd.notice === 'conflict' && <Button type="button" variant="outline" onClick={reload}>{t.reload}</Button>}
  </div>
}

export function UserAdminWorkspace() {
  const [lang, setLanguage] = useAppLanguage(), t = userAdminCopy[lang]
  const title = localizedText(lang, 'pairs.m_7c3d9a1e5b2f4a68')
  const [route, setRoute] = useState(() => readUserAdminRoute(window.location.href))
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true), [error, setError] = useState<ApiError | null>(null)
  const [catalog, setCatalog] = useState<Catalog | null>(null), [page, setPage] = useState<MemberPage | null>(null)
  const [access, setAccess] = useState<MemberAccess | null>(null), [delegations, setDelegations] = useState<Delegation[]>([])
  const [active, setActive] = useState<MemberSummary[]>([])
  const [issued, setIssued] = useState<IssuedLink | null>(null)
  const [settings, setSettings] = useState<InvitationSettings | null>(null)
  const offline = useOnline()
  const refresh = useCallback(() => setReload(x => x + 1), [])
  const go = useCallback((href: string) => { history.pushState(null, '', href); setRoute(readUserAdminRoute(window.location.href)); refresh() }, [refresh])
  const onDone = useCallback((body: unknown) => {
    // USER-ADMIN-02: create and reissue return the membership plus a one-time invitation token.
    let id: unknown = null
    if (body && typeof body === 'object' && 'invitationToken' in body) {
      try {
        const issue = decodeInvitationIssue(body)
        id = issue.access.membership.id
        setIssued(issue.token && issue.expiresAt ? { membershipId: issue.access.membership.id, link: invitationLink(window.location.origin, issue.token), expiresAt: issue.expiresAt } : null)
      } catch { setIssued(null) }
    } else id = body && typeof body === 'object' && 'membership' in body && body.membership && typeof body.membership === 'object' && 'id' in body.membership ? body.membership.id : null
    // Receipt confirmed: keep the canonical resource identity in the URL before the optional current read.
    if (route.view === 'new' && isGuid(id)) go(`${base}/${id}`)
    else refresh()
  }, [route.view, go, refresh])
  const cmd = useAdminCommand(user, offline, onDone)
  const { clear } = cmd
  useEffect(() => {
    const pop = () => { setRoute(readUserAdminRoute(window.location.href)); refresh() }
    addEventListener('popstate', pop); return () => removeEventListener('popstate', pop)
  }, [refresh])
  const move = useCallback((href: string) => {
    if (cmd.busy) return
    if (!/^\/app\/user-admin(?:\/|\?|$)/.test(href)) { window.location.assign(href); return }
    clear(); go(href)
  }, [cmd.busy, clear, go])
  useEffect(() => {
    const c = new AbortController()
    ;(async () => {
      await Promise.resolve()
      if (c.signal.aborted) return
      setLoading(true); setError(null); setPage(null); setAccess(null); setDelegations([]); setActive([]); setSettings(null)
      if (route.invalid) throw new ApiError(400, 'invalid_route')
      setUser(await readSession(c.signal))
      // The catalog gates everything: a 403 here leaves no permission name on the page.
      const cat = await userAdminApi.catalog(c.signal)
      setCatalog(cat)
      if (route.view === 'list') {
        const [p, s] = await Promise.all([userAdminApi.list(route.query, c.signal), userAdminApi.invitationSettings(c.signal)])
        setPage(p); setSettings(s)
      }
      // USER-ADMIN-03: whether e-mail delivery exists decides the create/reissue hint (never a delivered claim).
      if (route.view !== 'list') setSettings(await userAdminApi.invitationSettings(c.signal))
      if (route.view === 'detail') {
        const [a, d, members] = await Promise.all([userAdminApi.access(route.id, c.signal), userAdminApi.delegations(route.id, c.signal),
          userAdminApi.list({ status: 'Active', bundle: '', q: '', offset: 0 }, c.signal)])
        setAccess(a); setDelegations(d); setActive(members.items)
      }
    })().then(() => { if (!c.signal.aborted) setLoading(false) })
      .catch(e => { if (c.signal.aborted) return; setCatalog(null); setError(e instanceof ApiError ? e : new ApiError(0, 'network')); setLoading(false) })
    return () => c.abort()
  }, [route, reload])
  const disabled = cmd.busy || offline || !!cmd.retained && cmd.notice !== 'uncertain'
  return <div className="product-shell enterprise-shell user-admin-workspace">
    <EnterpriseChrome session={user} language={lang} onLanguageChange={setLanguage} onNavigate={move} title={title} mainId="user-admin-main"/>
    <main id="user-admin-main" tabIndex={-1} aria-busy={loading || cmd.busy} data-testid="user-admin">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span>{t.boundary}</span>}
        actions={<EnterpriseActions>
          {route.view !== 'list' && <Button variant="outline" disabled={cmd.busy} onClick={() => move(base)}>{t.back}</Button>}
          {route.view === 'list' && catalog?.canManage && <Button disabled={cmd.busy || offline} onClick={() => move(`${base}/new`)}>{t.newMember}</Button>}
          <Button variant="outline" disabled={offline || cmd.busy} onClick={refresh}>{t.refresh}</Button></EnterpriseActions>}/>
      {offline && <p role="alert" data-state="offline">{t.offline}</p>}
      <Notice t={t} cmd={cmd} offline={offline} reload={refresh}/>
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error && <div role="alert" data-state={error.status === 403 ? 'forbidden' : 'error'}>
        <p>{error.code === 'invalid_route' ? t.invalidRoute : error.status === 403 ? t.forbidden : error.status === 404 ? t.notFound : error.status === 0 ? t.offline : t.error}</p>
        {error.code !== 'invalid_route' && error.status !== 403 && <Button type="button" variant="outline" onClick={refresh}>{t.retry}</Button>}</div>}
      {!loading && !error && catalog && route.view === 'list' && settings &&
        <InvitationSettingsPanel key={`${settings.version}-${settings.lifetimeHours}`} t={t} settings={settings} disabled={disabled} run={cmd.run}/>}
      {!loading && !error && catalog && route.view === 'list' && page &&
        <MemberList t={t} catalog={catalog} page={page} query={route.query} open={id => move(`${base}/${id}`)} filter={q => move(listHref(q))}/>}
      {!loading && !error && catalog && route.view === 'new' && (catalog.canManage
        ? <CreateForm t={t} catalog={catalog} disabled={disabled} run={cmd.run} deliveryConfigured={settings?.deliveryConfigured ?? false}/>
        : <p role="alert" data-state="forbidden">{t.blocked_not_manager}</p>)}
      {!loading && !error && catalog && route.view === 'detail' && access &&
        <MemberDetail t={t} catalog={catalog} access={access} delegations={delegations} active={active} disabled={disabled} run={cmd.run} open={id => move(`${base}/${id}`)}
          issued={issued?.membershipId === access.membership.id ? issued : null} deliveryConfigured={settings?.deliveryConfigured ?? false}/>}
    </main>
  </div>
}

const bundleLabel = (t: Copy, code: string) => word(t, 'bundle_' + code)
function StatusBadge({ t, status }: { t: Copy; status: string }) {
  return <Badge tone={status === 'Active' ? 'info' : status === 'Suspended' || status === 'Invited' ? 'warning' : 'danger'} data-status={status}>{word(t, 'status_' + status)}</Badge>
}
// An invitation is never shown as an active grant: Pending/Expired are warnings until the identity holder accepts.
function InvitationBadge({ t, m }: { t: Copy; m: MemberSummary }) {
  if (!m.invitationState || m.status !== 'Invited') return null
  return <> <Badge tone={m.invitationState === 'Pending' ? 'neutral' : 'warning'} data-invitation={m.invitationState}>{word(t, 'invitation_' + m.invitationState)}</Badge>
    <DeliveryBadge t={t} m={m}/></>
}
// USER-ADMIN-03: e-mail delivery of the latest invitation. SENT = accepted by the mail server (not read/accepted by the person);
// FAILED and NOT_CONFIGURED are warnings: the copy link remains the way to hand the invitation over.
function DeliveryBadge({ t, m }: { t: Copy; m: MemberSummary }) {
  if (!m.invitationDeliveryState) return null
  const tone = m.invitationDeliveryState === 'SENT' ? 'info' : m.invitationDeliveryState === 'QUEUED' ? 'neutral' : 'warning'
  return <> <Badge tone={tone} data-delivery={m.invitationDeliveryState}>{word(t, 'delivery_' + m.invitationDeliveryState)}</Badge></>
}

// USER-ADMIN-03: 54.01 account.invite_hours (1-168, default 24) for this tenant; applies to invitations issued afterwards.
function InvitationSettingsPanel({ t, settings, disabled, run }: { t: Copy; settings: InvitationSettings; disabled: boolean; run: (c: AdminCommand) => Promise<void> }) {
  const [hours, setHours] = useState(String(settings.lifetimeHours))
  const value = /^\d{1,3}$/.test(hours) ? Number(hours) : NaN
  const valid = Number.isInteger(value) && value >= settings.minHours && value <= settings.maxHours
  return <section aria-label={t.lifetime} data-state="invitation-settings">
    <h2>{t.lifetime}</h2>
    <p data-lifetime={settings.lifetimeHours}>{settings.lifetimeHours} h{!settings.tenantValue && <> · {t.lifetimeDefault}</>}
      {' · '}<span data-delivery-configured={settings.deliveryConfigured}>{settings.deliveryConfigured ? t.deliveryConfigured : t.deliveryNotConfigured}</span></p>
    {settings.canManage && <form aria-label={t.lifetime} className="user-admin-filters" onSubmit={e => { e.preventDefault()
      if (valid && !disabled && value !== settings.lifetimeHours) void run(userAdminApi.updateInvitationSettings(settings.version, value)) }}>
      <label htmlFor="ua-lifetime">{t.lifetimeHours}</label>
      <input id="ua-lifetime" type="number" inputMode="numeric" min={settings.minHours} max={settings.maxHours} step={1} required value={hours}
        aria-invalid={!valid} aria-describedby="ua-lifetime-hint" onChange={e => setHours(e.target.value)}/>
      <small id="ua-lifetime-hint">{t.lifetimeHint}</small>
      <EnterpriseActions layout="form" aria-label={t.lifetime}>
        <Button type="submit" disabled={disabled || !valid || value === settings.lifetimeHours}>{t.lifetimeSave}</Button></EnterpriseActions>
    </form>}
  </section>
}

// Optional invitation e-mail recipient (create and reissue). An empty value means copy link only.
function DeliveryEmailField({ t, id, value, onChange, configured }: { t: Copy; id: string; value: string; onChange: (v: string) => void; configured: boolean }) {
  const invalid = value.trim() !== '' && !plausibleEmail(value.trim())
  return <>
    <label htmlFor={id}>{t.deliveryEmail}</label>
    <input id={id} type="email" maxLength={254} autoComplete="off" value={value} aria-invalid={invalid} aria-describedby={id + '-hint'} onChange={e => onChange(e.target.value)}/>
    <small id={id + '-hint'}>{configured ? t.deliveryEmailHint : t.deliveryNotConfiguredNote}</small>
  </>
}
function MemberBadges({ t, m }: { t: Copy; m: MemberSummary }) {
  return <><StatusBadge t={t} status={m.status}/><InvitationBadge t={t} m={m}/> <Badge tone={m.managedBy === 'Database' ? 'neutral' : m.managedBy === 'Configuration' ? 'info' : 'warning'} data-managed={m.managedBy}>
    {word(t, 'managed_' + m.managedBy)}</Badge>
    {m.administrator && <> <Badge tone="info" data-admin="true">{t.admin}</Badge></>}{m.self && <> <Badge tone="neutral" data-self="true">{t.you}</Badge></>}</>
}

function MemberList({ t, catalog, page, query, open, filter }: { t: Copy; catalog: Catalog; page: MemberPage; query: ListQuery; open: (id: string) => void
  filter: (q: ListQuery) => void }) {
  const [q, setQ] = useState(query.q), [status, setStatus] = useState(query.status), [bundle, setBundle] = useState(query.bundle)
  return <>
    <form role="search" aria-label={t.search} className="user-admin-filters" onSubmit={e => { e.preventDefault(); filter({ q, status, bundle, offset: 0 }) }}>
      <label htmlFor="ua-q">{t.search}</label>
      <input id="ua-q" type="search" maxLength={255} value={q} aria-describedby="ua-q-hint" onChange={e => setQ(e.target.value)}/>
      <small id="ua-q-hint">{t.searchHint}</small>
      <label htmlFor="ua-status">{t.status}</label>
      <select id="ua-status" value={status} onChange={e => setStatus(e.target.value)}><option value="">{t.all}</option>
        {statuses.map(s => <option key={s} value={s}>{word(t, 'status_' + s)}</option>)}</select>
      <label htmlFor="ua-bundle">{t.bundle}</label>
      <select id="ua-bundle" value={bundle} onChange={e => setBundle(e.target.value)}><option value="">{t.all}</option>
        {catalog.bundles.map(b => <option key={b.code} value={b.code}>{bundleLabel(t, b.code)}</option>)}</select>
      <EnterpriseActions layout="form" aria-label={t.apply}><Button type="submit">{t.apply}</Button></EnterpriseActions>
    </form>
    <section aria-label={t.members}>
      <h2>{t.members} <small>· {t.total}: <span data-count="total">{page.total}</span></small></h2>
      {page.items.length === 0 ? <p data-state="empty">{t.empty}</p> :
        <ul className="user-admin-members">{page.items.map(m => <li key={m.id} data-member={m.id}>
          <Button type="button" variant="link" onClick={() => open(m.id)}>{t.account} <bdi dir="ltr"><code>{shortId(m.accountId)}</code></bdi></Button>{' '}
          <MemberBadges t={t} m={m}/>
          <p>{m.bundles.length > 0 ? m.bundles.map(b => bundleLabel(t, b)).join(', ') : t.none} · {t.permissionCount}: {m.permissionCount} · {t.vesselCount}: {m.vesselGrantCount}
            {m.tenantWideRead && <> · {t.tenantWideRead}</>}{m.tenantWideManage && <> · {t.tenantWideManage}</>}</p>
        </li>)}</ul>}
      <EnterpriseActions align="start" aria-label={t.members}>
        <Button type="button" variant="outline" disabled={page.offset === 0} onClick={() => filter({ ...query, offset: Math.max(0, page.offset - page.limit) })}>{t.previous}</Button>
        <Button type="button" variant="outline" disabled={!page.hasMore} onClick={() => filter({ ...query, offset: page.offset + page.limit })}>{t.next}</Button>
      </EnterpriseActions>
    </section>
  </>
}

function groupByModule(catalog: Catalog, codes: string[]) {
  const module = new Map(catalog.permissions.map(p => [p.code, p.module]))
  const groups = new Map<string, string[]>()
  for (const code of [...codes].sort()) { const m = module.get(code) ?? code.split('.')[0]; groups.set(m, [...groups.get(m) ?? [], code]) }
  return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))
}

function PermissionGroups({ t, catalog, codes }: { t: Copy; catalog: Catalog; codes: string[] }) {
  const decision = new Set(catalog.permissions.filter(p => p.decisionAuthority).map(p => p.code))
  if (codes.length === 0) return <p data-state="empty">{t.noPermissions}</p>
  return <div className="user-admin-permission-groups">{groupByModule(catalog, codes).map(([module, list]) => <div key={module} data-module={module}>
    <h4><code>{module}</code></h4>
    <ul>{list.map(p => <li key={p}><code>{p}</code>{decision.has(p) && <> <Badge tone="warning">{t.decisionAuthority}</Badge></>}</li>)}</ul>
  </div>)}</div>
}

function GrantFields({ t, catalog, value, onChange, prefix }: { t: Copy; catalog: Catalog; value: GrantInput; onChange: (g: GrantInput) => void; prefix: string }) {
  const toggle = (list: string[], item: string, on: boolean) => on ? [...new Set([...list, item])].sort() : list.filter(x => x !== item)
  const modules = useMemo(() => groupByModule(catalog, catalog.permissions.map(p => p.code)), [catalog])
  const option = new Map(catalog.permissions.map(p => [p.code, p]))
  const fromBundles = new Set(value.bundles.flatMap(b => catalog.bundles.find(x => x.code === b)?.permissions ?? []))
  return <>
    <fieldset><legend>{t.bundles}</legend>
      {catalog.bundles.map(b => { const checked = value.bundles.includes(b.code), id = `${prefix}-b-${b.code}`
        return <div key={b.code} data-bundle={b.code}>
          <input id={id} type="checkbox" checked={checked} disabled={!b.grantable && !checked}
            onChange={e => onChange({ ...value, bundles: toggle(value.bundles, b.code, e.target.checked) })}/>
          <label htmlFor={id}>{bundleLabel(t, b.code)} <small>({b.locator} · {b.permissions.length})</small></label>
          {!b.grantable && <small data-state="not-grantable"> · {t.notGrantable}</small>}
        </div> })}
    </fieldset>
    <fieldset><legend>{t.individualPermissions}</legend>
      <p><small>{t.decisionHint}</small></p>
      {modules.map(([module, list]) => <details key={module} data-module={module}>
        <summary><code>{module}</code> ({list.filter(p => value.permissions.includes(p)).length}/{list.length})</summary>
        {list.map(p => { const o = option.get(p)!, checked = value.permissions.includes(p), id = `${prefix}-p-${p}`
          return <div key={p}>
            <input id={id} type="checkbox" checked={checked} disabled={!o.grantable && !checked}
              onChange={e => onChange({ ...value, permissions: toggle(value.permissions, p, e.target.checked) })}/>
            <label htmlFor={id}><code>{p}</code>{o.decisionAuthority && <> <Badge tone="warning">{t.decisionAuthority}</Badge></>}
              {fromBundles.has(p) && <small> · {t.bundles}</small>}{!o.grantable && <small> · {t.notGrantablePermission}</small>}</label>
          </div> })}
      </details>)}
    </fieldset>
    <fieldset><legend>{t.vesselGrants}</legend>
      <div><input id={`${prefix}-twr`} type="checkbox" checked={value.tenantWideRead} disabled={!catalog.grantorTenantWideRead && !catalog.grantorTenantWideManage && !value.tenantWideRead}
        onChange={e => onChange({ ...value, tenantWideRead: e.target.checked })}/><label htmlFor={`${prefix}-twr`}>{t.tenantWideRead}</label></div>
      <div><input id={`${prefix}-twm`} type="checkbox" checked={value.tenantWideManage} disabled={!catalog.grantorTenantWideManage && !value.tenantWideManage}
        onChange={e => onChange({ ...value, tenantWideManage: e.target.checked })}/><label htmlFor={`${prefix}-twm`}>{t.tenantWideManage}</label></div>
      <label htmlFor={`${prefix}-company`}>{t.company}</label>
      <select id={`${prefix}-company`} value={value.activeCompanyId ?? ''} onChange={e => onChange({ ...value, activeCompanyId: e.target.value || null })}>
        <option value="">{t.none}</option>{catalog.companies.map(c => <option key={c.id} value={c.id}>{c.code} · {c.name}</option>)}</select>
      <ul className="user-admin-vessels">{catalog.vessels.map(v => { const grant = value.vesselGrants.find(g => g.vesselId === v.id), id = `${prefix}-v-${v.id}`
        const set = (next: VesselGrant | null) => onChange({ ...value, vesselGrants: [...value.vesselGrants.filter(g => g.vesselId !== v.id), ...next ? [next] : []] })
        return <li key={v.id} data-vessel={v.id}>
          <input id={id} type="checkbox" checked={!!grant} onChange={e => set(e.target.checked ? { vesselId: v.id, canEdit: false } : null)}/>
          <label htmlFor={id}>{v.code} · {v.name} <small>({v.kind})</small></label>
          {grant && <> <input id={id + '-e'} type="checkbox" checked={grant.canEdit} onChange={e => set({ vesselId: v.id, canEdit: e.target.checked })}/>
            <label htmlFor={id + '-e'}>{t.canEdit}</label></>}
        </li> })}</ul>
    </fieldset>
  </>
}

const emptyGrant: GrantInput = { bundles: [], permissions: [], tenantWideRead: false, tenantWideManage: false, activeCompanyId: null, vesselGrants: [] }
const sortedGrants = (g: VesselGrant[]) => [...g].sort((a, b) => a.vesselId.localeCompare(b.vesselId))

function CreateForm({ t, catalog, disabled, run, deliveryConfigured }: { t: Copy; catalog: Catalog; disabled: boolean; run: (c: AdminCommand) => Promise<void>
  deliveryConfigured: boolean }) {
  const [issuer, setIssuer] = useState(catalog.trustedIssuer ?? ''), [subject, setSubject] = useState(''), [grant, setGrant] = useState<GrantInput>(emptyGrant)
  const [email, setEmail] = useState('')
  // USER-ADMIN-02: the subject is optional; when given only that IdP identity can accept the invitation.
  const ok = issuer.trim().length > 0 && grant.bundles.length > 0 && (email.trim() === '' || plausibleEmail(email.trim()))
  return <form aria-label={t.create} className="user-admin-form" onSubmit={e => { e.preventDefault()
    if (ok && !disabled) void run(userAdminApi.create(issuer.trim(), subject.trim() || null, { ...grant, vesselGrants: sortedGrants(grant.vesselGrants) }, email.trim() || null)) }}>
    <h2>{t.create}</h2>
    <p role="note">{t.idpNote}</p>
    <p role="note">{t.invitationNote}</p>
    <label htmlFor="ua-issuer">{t.issuer}</label>
    <input id="ua-issuer" required maxLength={1024} inputMode="url" value={issuer} onChange={e => setIssuer(e.target.value)}/>
    <label htmlFor="ua-subject">{t.expectedSubject}</label>
    <input id="ua-subject" maxLength={255} autoComplete="off" value={subject} aria-describedby="ua-subject-hint" onChange={e => setSubject(e.target.value)}/>
    <small id="ua-subject-hint">{t.expectedSubjectHint}</small>
    <DeliveryEmailField t={t} id="ua-delivery-email" value={email} onChange={setEmail} configured={deliveryConfigured}/>
    <GrantFields t={t} catalog={catalog} value={grant} onChange={setGrant} prefix="ua-new"/>
    <EnterpriseActions layout="form" aria-label={t.create}><Button type="submit" disabled={disabled || !ok}>{t.create}</Button></EnterpriseActions>
  </form>
}

function Section({ label, children }: { label: string; children: ReactNode }) { return <section aria-label={label}><h3>{label}</h3>{children}</section> }

// USER-ADMIN-02: invitation state, the one-time link (copy) and a reissue. The link is shown only right after it was
// issued; afterwards the server holds only a hash, so a new link means a reissue that invalidates the previous one.
function InvitationPanel({ t, m, issued, canManage, disabled, run, deliveryConfigured }: { t: Copy; m: MemberSummary; issued: IssuedLink | null; canManage: boolean
  disabled: boolean; run: (c: AdminCommand) => Promise<void>; deliveryConfigured: boolean }) {
  const [copied, setCopied] = useState<'' | 'copied' | 'failed'>(''), [email, setEmail] = useState('')
  const emailOk = email.trim() === '' || plausibleEmail(email.trim())
  if (!m.invitationState) return null
  const copy = async () => {
    if (!issued) return
    try { await navigator.clipboard.writeText(issued.link); setCopied('copied') } catch { setCopied('failed') }
  }
  return <Section label={t.invitation}>
    <p data-invitation-state={m.invitationState}>{word(t, 'invitation_' + m.invitationState)}
      {m.invitationExpiresAt && m.status === 'Invited' && <> · {t.invitationExpires} <time dateTime={m.invitationExpiresAt}>{m.invitationExpiresAt}</time></>}
      {m.invitationSubjectExpected && <> · {t.invitationSubjectBound}</>}</p>
    {m.invitationDeliveryState && <p data-delivery-state={m.invitationDeliveryState}>{t.delivery}: {word(t, 'delivery_' + m.invitationDeliveryState)}
      {m.invitationDeliveryState === 'SENT' && <> · <small>{t.deliverySentNote}</small></>}
      {(m.invitationDeliveryState === 'FAILED' || m.invitationDeliveryState === 'NOT_CONFIGURED') && m.status === 'Invited' && <> · <small>{t.deliveryFallbackNote}</small></>}</p>}
    {m.status === 'Invited' && <p><small>{t.invitationPendingNote}</small></p>}
    {issued && m.status === 'Invited' && <div data-state="invitation-link">
      <label htmlFor="ua-invitation-link">{t.invitationLink}</label>
      <input id="ua-invitation-link" readOnly value={issued.link} aria-describedby="ua-invitation-link-hint" onFocus={e => e.currentTarget.select()}/>
      <small id="ua-invitation-link-hint">{t.invitationLinkHint}</small>
      <EnterpriseActions align="start" aria-label={t.invitationLink}>
        <Button type="button" onClick={() => void copy()}>{t.copyInvitationLink}</Button>
      </EnterpriseActions>
      {copied && <p role="status" data-copy={copied}>{copied === 'copied' ? t.invitationCopied : t.invitationCopyFailed}</p>}
    </div>}
    {canManage && m.status === 'Invited' && <>
      <DeliveryEmailField t={t} id="ua-reissue-email" value={email} onChange={setEmail} configured={deliveryConfigured}/>
      <EnterpriseActions align="start" aria-label={t.invitation}>
        <Button type="button" variant="outline" disabled={disabled || !emailOk}
          onClick={() => void run(userAdminApi.reissueInvitation(m.id, m.version, email.trim() || null))}>{t.reissueInvitation}</Button>
      </EnterpriseActions></>}
  </Section>
}

function MemberDetail({ t, catalog, access, delegations, active, disabled, run, open, issued, deliveryConfigured }: { t: Copy; catalog: Catalog; access: MemberAccess; delegations: Delegation[]
  active: MemberSummary[]; disabled: boolean; run: (c: AdminCommand) => Promise<void>; open: (id: string) => void; issued: IssuedLink | null
  deliveryConfigured: boolean }) {
  const m = access.membership
  const vessel = (id: string) => catalog.vessels.find(v => v.id === id)
  const company = access.activeCompanyId ? catalog.companies.find(c => c.id === access.activeCompanyId) : null
  const [editing, setEditing] = useState(false)
  const [grant, setGrant] = useState<GrantInput>(() => ({ bundles: [...m.bundles], permissions: [...access.individualPermissions], tenantWideRead: m.tenantWideRead,
    tenantWideManage: m.tenantWideManage, activeCompanyId: access.activeCompanyId, vesselGrants: access.vesselGrants.map(v => ({ ...v })) }))
  const transition = (action: 'suspend' | 'resume' | 'revoke' | 'security-suspend') => void run(userAdminApi.transition(m.id, action, m.version))
  return <>
    <section aria-label={t.summary} data-member={m.id}>
      <h2>{t.account} <bdi dir="ltr"><code>{m.accountId}</code></bdi></h2>
      <p><MemberBadges t={t} m={m}/></p>
      <dl className="user-admin-facts">
        <dt>{t.managedBy}</dt><dd>{word(t, 'managed_' + m.managedBy)}</dd>
        <dt>{t.source}</dt><dd><code>{m.source}</code></dd>
        <dt>{t.statusReason}</dt><dd><code>{m.statusReason}</code> · <time dateTime={m.statusChangedAt}>{m.statusChangedAt}</time></dd>
        <dt>{t.accessRevision}</dt><dd>{m.accessRevision}</dd>
        {m.grantRevision !== null && <><dt>{t.grantRevision}</dt><dd>{m.grantRevision}</dd></>}
        <dt>{t.bundles}</dt><dd>{m.bundles.length ? m.bundles.map(b => bundleLabel(t, b)).join(', ') : t.none}</dd>
        <dt>{t.tenantWideRead}</dt><dd>{m.tenantWideRead ? '✓' : '—'}</dd>
        <dt>{t.tenantWideManage}</dt><dd>{m.tenantWideManage ? '✓' : '—'}</dd>
        <dt>{t.company}</dt><dd>{company ? `${company.code} · ${company.name}` : access.activeCompanyId ? <code>{access.activeCompanyId}</code> : t.none}</dd>
      </dl>
      <p><a href={`/app/audit?resourceId=${m.id}`}>{t.auditLink}</a></p>
    </section>
    <InvitationPanel t={t} m={m} issued={issued} canManage={catalog.canManage} disabled={disabled} run={run} deliveryConfigured={deliveryConfigured}/>
    <Section label={t.vesselGrants}>
      {access.vesselGrants.length === 0 ? <p data-state="empty">{t.none}</p> : <ul>{access.vesselGrants.map(g => { const v = vessel(g.vesselId)
        return <li key={g.vesselId} data-vessel={g.vesselId}>{v ? `${v.code} · ${v.name}` : <code>{g.vesselId}</code>} · {g.canEdit ? t.canEdit : t.readOnly}</li> })}</ul>}
    </Section>
    <Section label={t.effectivePermissions}><PermissionGroups t={t} catalog={catalog} codes={access.permissions}/></Section>
    {access.individualPermissions.length > 0 && <Section label={t.individualPermissions}><PermissionGroups t={t} catalog={catalog} codes={access.individualPermissions}/></Section>}
    {catalog.canManage && <Section label={t.lifecycle}>
      <p><small>{t.lifecycleNote}</small></p>
      <EnterpriseActions align="start" aria-label={t.lifecycle}>
        {m.status === 'Active' && !m.self && <Button type="button" variant="outline" disabled={disabled} onClick={() => transition('suspend')}>{t.suspend}</Button>}
        {m.status === 'Suspended' && <Button type="button" variant="outline" disabled={disabled} onClick={() => transition('resume')}>{t.resume}</Button>}
        {m.status !== 'Revoked' && !m.self && <Button type="button" variant="destructive" disabled={disabled} onClick={() => transition('revoke')}>
          {m.status === 'Invited' ? t.revokeInvitation : t.revokeMembership}</Button>}
        {m.status === 'Active' && catalog.canSecuritySuspend && <Button type="button" variant="destructive" disabled={disabled} onClick={() => transition('security-suspend')}>{t.securitySuspend}</Button>}
      </EnterpriseActions>
    </Section>}
    <Section label={t.editGrant}>
      {!access.editable ? <p role="note" data-state="edit-blocked" data-reason={access.editBlockedReason ?? ''}>{word(t, 'blocked_' + access.editBlockedReason)}</p>
        : !editing ? <Button type="button" disabled={disabled} onClick={() => setEditing(true)}>{t.editGrant}</Button>
        : <form aria-label={t.editGrant} className="user-admin-form" onSubmit={e => { e.preventDefault()
            if (!disabled && m.grantRevision !== null) void run(userAdminApi.updateGrant(m.id, m.grantRevision, { ...grant, vesselGrants: sortedGrants(grant.vesselGrants) })) }}>
          <GrantFields t={t} catalog={catalog} value={grant} onChange={setGrant} prefix="ua-edit"/>
          <EnterpriseActions layout="form" aria-label={t.editGrant}><Button type="submit" disabled={disabled || grant.bundles.length === 0}>{t.save}</Button></EnterpriseActions>
        </form>}
    </Section>
    <Section label={t.history}>
      {access.history.length === 0 ? <p data-state="empty">{t.noHistory}</p> : <ol reversed>{access.history.map(h => <li key={h.revision} data-revision={h.revision}>
        {t.revision} {h.revision} · <time dateTime={h.createdAt}>{h.createdAt}</time> · {t.grantedBy} <bdi dir="ltr"><code>{shortId(h.grantedBy)}</code></bdi> ·{' '}
        {h.bundles.map(b => bundleLabel(t, b)).join(', ')} · {t.permissionCount}: {h.permissionCount} · {t.vesselCount}: {h.vesselGrantCount}
        {h.tenantWideRead && <> · {t.tenantWideRead}</>}{h.tenantWideManage && <> · {t.tenantWideManage}</>}</li>)}</ol>}
    </Section>
    <Section label={t.delegations}>
      <p><small>{t.delegationNote}</small></p>
      {delegations.length === 0 ? <p data-state="empty">{t.noDelegations}</p> : <ul>{delegations.map(d => <li key={d.id} data-delegation={d.id} data-state={d.state}>
        {t.delegator} <Button type="button" variant="link" onClick={() => open(d.delegatorMembershipId)}><code>{shortId(d.delegatorMembershipId)}</code></Button> →{' '}
        {t.delegate} <Button type="button" variant="link" onClick={() => open(d.delegateMembershipId)}><code>{shortId(d.delegateMembershipId)}</code></Button>{' '}
        <Badge tone={d.state === 'Effective' ? 'info' : d.state === 'Pending' ? 'neutral' : 'warning'}>{d.state}</Badge>
        <p>{word(t, 'reason_' + d.reasonCode)} · <time dateTime={d.validFrom}>{d.validFrom}</time> – <time dateTime={d.validUntil}>{d.validUntil}</time> · {d.permissions.map(p => <code key={p}>{p} </code>)}</p>
        {catalog.canManage && d.status === 'Active' && <Button type="button" variant="outline" disabled={disabled} onClick={() => void run(userAdminApi.revokeDelegation(d.id, d.version))}>{t.revoke}</Button>}
      </li>)}</ul>}
      {catalog.canManage && m.status === 'Active' && !m.self && <DelegationForm t={t} catalog={catalog} access={access} active={active} disabled={disabled} run={run}/>}
    </Section>
  </>
}

function DelegationForm({ t, catalog, access, active, disabled, run }: { t: Copy; catalog: Catalog; access: MemberAccess; active: MemberSummary[]; disabled: boolean
  run: (c: AdminCommand) => Promise<void> }) {
  const source = access.membership
  const delegable = new Set(catalog.permissions.filter(p => p.delegable).map(p => p.code))
  const offered = access.permissions.filter(p => delegable.has(p))
  const candidates = active.filter(x => x.id !== source.id && !x.self)
  const [delegate, setDelegate] = useState(''), [permissions, setPermissions] = useState<string[]>([]), [from, setFrom] = useState(''), [until, setUntil] = useState('')
  const [reason, setReason] = useState(''), [useCompany, setUseCompany] = useState(false), [vessels, setVessels] = useState<VesselGrant[]>([])
  const validFrom = localToIso(from), validUntil = localToIso(until)
  const ok = !!delegate && permissions.length > 0 && !!validFrom && !!validUntil && !!reason && (useCompany || vessels.length > 0)
  return <form aria-label={t.createDelegation} className="user-admin-form" onSubmit={e => { e.preventDefault()
    if (ok && !disabled) void run(userAdminApi.delegate(source.id, { delegateMembershipId: delegate, permissions: [...permissions].sort(), validFrom: validFrom!, validUntil: validUntil!,
      reasonCode: reason, companyId: useCompany ? access.activeCompanyId : null, vesselScope: sortedGrants(vessels) })) }}>
    <h4>{t.createDelegation}</h4>
    <label htmlFor="ua-d-delegate">{t.delegate}</label>
    <select id="ua-d-delegate" required value={delegate} onChange={e => setDelegate(e.target.value)}><option value="">—</option>
      {candidates.map(c => <option key={c.id} value={c.id}>{shortId(c.accountId)} · {c.bundles.map(b => bundleLabel(t, b)).join(', ') || word(t, 'managed_' + c.managedBy)}</option>)}</select>
    <fieldset><legend>{t.delegationPermissions}</legend>
      {offered.length === 0 ? <p data-state="empty">{t.noPermissions}</p> : offered.map(p => <div key={p}>
        <input id={`ua-d-p-${p}`} type="checkbox" checked={permissions.includes(p)}
          onChange={e => setPermissions(list => e.target.checked ? [...list, p] : list.filter(x => x !== p))}/><label htmlFor={`ua-d-p-${p}`}><code>{p}</code></label></div>)}
    </fieldset>
    <label htmlFor="ua-d-from">{t.validFrom}</label><input id="ua-d-from" type="datetime-local" required value={from} onChange={e => setFrom(e.target.value)}/>
    <label htmlFor="ua-d-until">{t.validUntil}</label><input id="ua-d-until" type="datetime-local" required value={until} onChange={e => setUntil(e.target.value)}/>
    <label htmlFor="ua-d-reason">{t.reason}</label>
    <select id="ua-d-reason" required value={reason} onChange={e => setReason(e.target.value)}><option value="">—</option>
      {catalog.delegationReasons.map(r => <option key={r} value={r}>{word(t, 'reason_' + r)}</option>)}</select>
    {access.activeCompanyId && <div><input id="ua-d-company" type="checkbox" checked={useCompany} onChange={e => setUseCompany(e.target.checked)}/>
      <label htmlFor="ua-d-company">{t.company}</label></div>}
    {access.vesselGrants.length > 0 && <fieldset><legend>{t.vesselGrants}</legend>{access.vesselGrants.map(g => { const v = catalog.vessels.find(x => x.id === g.vesselId)
      const chosen = vessels.find(x => x.vesselId === g.vesselId)
      return <div key={g.vesselId}>
        <input id={`ua-d-v-${g.vesselId}`} type="checkbox" checked={!!chosen}
          onChange={e => setVessels(list => e.target.checked ? [...list, { vesselId: g.vesselId, canEdit: false }] : list.filter(x => x.vesselId !== g.vesselId))}/>
        <label htmlFor={`ua-d-v-${g.vesselId}`}>{v ? `${v.code} · ${v.name}` : g.vesselId}</label>
        {chosen && g.canEdit && <> <input id={`ua-d-ve-${g.vesselId}`} type="checkbox" checked={chosen.canEdit}
          onChange={e => setVessels(list => list.map(x => x.vesselId === g.vesselId ? { ...x, canEdit: e.target.checked } : x))}/>
          <label htmlFor={`ua-d-ve-${g.vesselId}`}>{t.canEdit}</label></>}
      </div> })}</fieldset>}
    <EnterpriseActions layout="form" aria-label={t.createDelegation}><Button type="submit" disabled={disabled || !ok}>{t.createDelegation}</Button></EnterpriseActions>
  </form>
}
