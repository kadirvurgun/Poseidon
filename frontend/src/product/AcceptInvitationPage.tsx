import { useCallback, useEffect, useRef, useState } from 'react'
import { localizedCopy } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { LanguageSelector } from '../components/LanguageSelector'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { ApiError, session as readSession } from './api'
import { useOnline } from './UatWorkspace'
import { decodeInvitationAccepted, decodeInvitationGrant, isInvitationToken, sendAdmin, type InvitationGrant } from './user-admin-api'
import './user-admin.css'

// USER-ADMIN-02 invitation acceptance (/app/accept-invitation#token=…). Not a navigation destination: it is reached only
// through the link an administrator hands out. The token is in the URL fragment (never sent to the server or in a
// Referer); on the first render, before any request, it moves into this tab's session storage and the fragment is
// cleared, so it stays out of history and bookmarks. A legacy ?token= query is stripped and never used. The server decides everything;
// every refusal is one neutral message that reveals nothing about other organisations.
export const acceptInvitationCopy = localizedCopy('AcceptInvitation_tsx_copy')
type Copy = (typeof acceptInvitationCopy)[AppLocale]
const word = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key
const page = '/app/accept-invitation'
const storageKey = 'poseidon.invitation'
export const loginHref = (returnUrl: string) => `/api/v1/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`

function storage(): Storage | null { try { return window.sessionStorage } catch { return null } }

// Reads the token once: from the query (then removed from the address bar) or from this tab's session storage.
export function takeInvitationToken(): string | null {
  const url = new URL(window.location.href)
  const fromFragment = new URLSearchParams(url.hash.replace(/^#/, '')).get('token')
  if (url.hash !== '' || url.search !== '') history.replaceState(null, '', page)
  if (url.searchParams.has('token') && fromFragment === null) { forgetInvitationToken(); return null }
  if (fromFragment !== null) {
    if (!isInvitationToken(fromFragment)) { forgetInvitationToken(); return null }
    try { storage()?.setItem(storageKey, fromFragment) } catch { /* The token then lives in memory only. */ }
    return fromFragment
  }
  let stored: string | null
  try { stored = storage()?.getItem(storageKey) ?? null } catch { stored = null }
  return stored && isInvitationToken(stored) ? stored : null
}
function forgetInvitationToken() { try { storage()?.removeItem(storageKey) } catch { /* Storage unavailable. */ } }

type State = { kind: 'loading' } | { kind: 'no-token' } | { kind: 'sign-in' } | { kind: 'member' } | { kind: 'preview'; grant: InvitationGrant; csrf: string }
  | { kind: 'accepted'; grant: InvitationGrant } | { kind: 'unavailable' } | { kind: 'rate-limited' } | { kind: 'denied' } | { kind: 'error' } | { kind: 'offline' }

const failure = (e: unknown): State => !(e instanceof ApiError) ? { kind: navigator.onLine ? 'error' : 'offline' }
  : e.status === 409 ? { kind: 'unavailable' } : e.status === 429 ? { kind: 'rate-limited' } : e.status === 401 ? { kind: 'sign-in' }
  : e.status === 403 ? { kind: 'denied' } : e.status === 0 ? { kind: 'offline' } : { kind: 'error' }

export function AcceptInvitationPage() {
  const [lang, setLanguage] = useAppLanguage(), t = acceptInvitationCopy[lang]
  const [token] = useState(takeInvitationToken)
  const [state, setState] = useState<State>(() => token ? { kind: 'loading' } : { kind: 'no-token' }), [attempt, setAttempt] = useState(0), [busy, setBusy] = useState(false)
  const offline = useOnline()
  const lock = useRef(false)
  useEffect(() => { document.title = `POSEIDON · ${t.title}` }, [t.title])
  useEffect(() => {
    if (!token) return
    const c = new AbortController()
    ;(async () => {
      await Promise.resolve()
      if (c.signal.aborted) return
      setState({ kind: 'loading' })
      const s = await readSession(c.signal)
      if (!s.authenticated) { setState({ kind: 'sign-in' }); return }
      // A member's session already holds its one membership; it never previews or accepts (server-enforced too).
      if (!s.invitationSession || !s.csrfToken) { setState({ kind: 'member' }); return }
      const grant = decodeInvitationGrant(await sendAdmin({ key: crypto.randomUUID(), path: 'invitations/preview', body: { token } }, s.csrfToken, c.signal))
      if (!c.signal.aborted) setState({ kind: 'preview', grant, csrf: s.csrfToken })
    })().catch(e => { if (!c.signal.aborted) setState(failure(e)) })
    return () => c.abort()
  }, [token, attempt])
  const accept = useCallback(async () => {
    if (state.kind !== 'preview' || !token || lock.current || offline) return
    lock.current = true; setBusy(true)
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000)
    try {
      const result = decodeInvitationAccepted(await sendAdmin({ key: crypto.randomUUID(), path: 'invitations/accept', body: { token } }, state.csrf, c.signal))
      forgetInvitationToken()
      setState({ kind: 'accepted', grant: result.grant })
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) forgetInvitationToken()
      setState(failure(e))
    } finally { clearTimeout(timeout); lock.current = false; setBusy(false) }
  }, [state, token, offline])
  const grant = state.kind === 'preview' || state.kind === 'accepted' ? state.grant : null
  return <div className="product-shell accept-invitation-page">
    <header className="topbar"><span className="breadcrumb">POSEIDON <span aria-hidden="true">/</span> {t.title}</span>
      <div className="topbar-actions"><LanguageSelector locale={lang} onChange={setLanguage}/></div></header>
    <main id="accept-invitation-main" tabIndex={-1} aria-busy={state.kind === 'loading' || busy} data-testid="accept-invitation" data-state={state.kind}>
      <p className="eyebrow">{t.eyebrow}</p>
      <h1>{t.title}</h1>
      <p>{t.intro}</p>
      <p role="note"><small>{t.idpNote}</small></p>
      {offline && state.kind !== 'accepted' && <p role="alert" data-state="offline">{t.offline}</p>}
      {state.kind === 'loading' && <p role="status">{t.loading}</p>}
      {state.kind === 'no-token' && <p role="alert">{t.noToken}</p>}
      {state.kind === 'sign-in' && <div role="status"><p>{t.signInNeeded}</p><a className="button-link" href={loginHref(page)}>{t.signIn}</a></div>}
      {state.kind === 'member' && <p role="alert">{t.memberSession}</p>}
      {state.kind === 'unavailable' && <p role="alert" data-code="invitation_unavailable">{t.unavailable}</p>}
      {state.kind === 'rate-limited' && <p role="alert">{t.rateLimited}</p>}
      {state.kind === 'denied' && <p role="alert">{t.denied}</p>}
      {(state.kind === 'error' || state.kind === 'offline') && <div role="alert"><p>{state.kind === 'offline' ? t.offline : t.error}</p>
        <Button type="button" variant="outline" disabled={offline} onClick={() => setAttempt(x => x + 1)}>{t.retry}</Button></div>}
      {grant && <section aria-label={t.grants}>
        <h2>{t.grants}</h2>
        <dl className="user-admin-facts">
          {grant.tenantName && <><dt>{t.organisation}</dt><dd>{grant.tenantName}</dd></>}
          <dt>{t.bundles}</dt><dd>{grant.bundles.map(b => <Badge key={b} tone="neutral" data-bundle={b}>{word(t, 'bundle_' + b)}</Badge>)}</dd>
          <dt>{t.permissionCount}</dt><dd>{grant.permissionCount}</dd>
          <dt>{t.tenantWideRead}</dt><dd>{grant.tenantWideRead ? t.yes : t.no}</dd>
          <dt>{t.tenantWideManage}</dt><dd>{grant.tenantWideManage ? t.yes : t.no}</dd>
          <dt>{t.vesselCount}</dt><dd>{grant.vesselGrantCount}</dd>
          <dt>{t.companyScoped}</dt><dd>{grant.companyScoped ? t.yes : t.no}</dd>
          {state.kind === 'preview' && <><dt>{t.expires}</dt><dd><time dateTime={grant.expiresAt}>{grant.expiresAt}</time></dd></>}
        </dl>
        {state.kind === 'preview' && <Button type="button" disabled={busy || offline} onClick={() => void accept()}>{busy ? t.accepting : t.accept}</Button>}
      </section>}
      {state.kind === 'accepted' && <div role="status" data-state="accepted"><p>{t.accepted}</p><a className="button-link" href={loginHref('/app')}>{t.continue}</a></div>}
    </main>
  </div>
}
