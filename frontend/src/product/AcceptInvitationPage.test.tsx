import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AcceptInvitationPage } from './AcceptInvitationPage'
import { changeAppLanguage } from '../i18n'

// USER-ADMIN-02 accept page. Mocked API only (UI behavior); the real binding is proven by the PostgreSQL API tests.
const token = 'pi1.55555555555545558555555555555555.' + 'B'.repeat(43)
const membershipId = '11111111-1111-4111-8111-111111111111'
const grant = { tenantName: 'FICTIONAL Tenant', bundles: ['read-only'], permissionCount: 11, tenantWideRead: true, tenantWideManage: false, vesselGrantCount: 0,
  companyScoped: false, expiresAt: '2026-11-28T08:00:00Z' }
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const fetcher = vi.fn()
let session: Record<string, unknown> = {}, previewReply: () => Response = () => json(grant), acceptReply: () => Response = () => json({ membershipId, grant, signInRequired: true })
let posts: { url: string; init: RequestInit }[] = []
let seenLocations: string[] = []

beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  sessionStorage.clear(); posts = []; seenLocations = []
  session = { authenticated: true, setupRequired: false, actorId: null, tenantId: null, canRead: false, csrfToken: 'synthetic-csrf', invitationSession: true }
  previewReply = () => json(grant); acceptReply = () => json({ membershipId, grant, signInRequired: true })
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    seenLocations.push(window.location.href)
    if (url.endsWith('auth/session')) return json(session)
    if (init?.method === 'POST') posts.push({ url, init })
    if (url.endsWith('invitations/preview')) return previewReply()
    if (url.endsWith('invitations/accept')) return acceptReply()
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
  history.replaceState(null, '', `/app/accept-invitation#token=${encodeURIComponent(token)}`)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('moves the token out of the address bar, previews the grant and accepts with CSRF and a key', async () => {
  const user = userEvent.setup()
  render(<AcceptInvitationPage/>)
  expect(window.location.hash).toBe('')
  expect(window.location.search).toBe('')
  expect(window.location.pathname).toBe('/app/accept-invitation')
  const grants = await screen.findByRole('region', { name: 'This invitation grants' })
  expect(grants.textContent).toContain('FICTIONAL Tenant')
  expect(grants.textContent).toContain('Read-only')
  expect(JSON.parse(String(posts[0].init.body))).toEqual({ token })
  await user.click(screen.getByRole('button', { name: 'Accept invitation' }))
  await waitFor(() => expect(posts.filter(p => p.url.endsWith('invitations/accept'))).toHaveLength(1))
  const accept = posts.find(p => p.url.endsWith('invitations/accept'))!
  const headers = accept.init.headers as Record<string, string>
  expect(headers['X-CSRF-TOKEN']).toBe('synthetic-csrf')
  expect(headers['Idempotency-Key'].length).toBeGreaterThanOrEqual(16)
  expect(await screen.findByText('Invitation accepted. Sign in again to start working in this organisation.')).toBeTruthy()
  expect(screen.getByRole('link', { name: 'Sign in to continue' }).getAttribute('href')).toBe('/api/v1/auth/login?returnUrl=%2Fapp')
  expect(sessionStorage.getItem('poseidon.invitation')).toBeNull()
})

it('asks an anonymous visitor to sign in and returns to the token-free page', async () => {
  session = { authenticated: false, setupRequired: false, actorId: null, tenantId: null, canRead: false, csrfToken: '' }
  render(<AcceptInvitationPage/>)
  const signIn = await screen.findByRole('link', { name: 'Sign in' })
  expect(signIn.getAttribute('href')).toBe('/api/v1/auth/login?returnUrl=%2Fapp%2Faccept-invitation')
  expect(signIn.getAttribute('href')).not.toContain(token)
  expect(posts).toHaveLength(0)
  expect(sessionStorage.getItem('poseidon.invitation')).toBe(token) // kept for the return from the IdP, in this tab only
})

it('shows one neutral message for any refusal and forgets the token', async () => {
  previewReply = () => json({ code: 'invitation_unavailable' }, 409)
  render(<AcceptInvitationPage/>)
  expect(await screen.findByText(/This invitation cannot be accepted\./)).toBeTruthy()
  expect(screen.queryByRole('button', { name: 'Accept invitation' })).toBeNull()
  expect(document.body.textContent).not.toContain('FICTIONAL Tenant')
})

it('never previews or accepts from an existing member session', async () => {
  session = { authenticated: true, setupRequired: false, actorId: '22222222-2222-4222-8222-222222222222', tenantId: '33333333-3333-4333-8333-333333333333',
    canRead: true, csrfToken: 'synthetic-csrf', invitationSession: false }
  render(<AcceptInvitationPage/>)
  expect(await screen.findByText(/You are signed in with an existing membership\./)).toBeTruthy()
  expect(posts).toHaveLength(0)
})

it('reports rate limiting and a missing link without calling the server', async () => {
  previewReply = () => json({ code: 'request_limit' }, 429)
  const { unmount } = render(<AcceptInvitationPage/>)
  expect(await screen.findByText('Too many attempts. Wait a few minutes and try again.')).toBeTruthy()
  unmount()
  sessionStorage.clear(); fetcher.mockClear()
  history.replaceState(null, '', '/app/accept-invitation#token=not-a-token')
  render(<AcceptInvitationPage/>)
  expect(await screen.findByText(/This page needs an invitation link\./)).toBeTruthy()
  expect(window.location.search).toBe('')
  expect(fetcher).not.toHaveBeenCalled()
})

// QA N1: the fragment is read and cleared before the first request; no request URL ever carries the token.
it('clears the token fragment before any request and never puts the token in a request URL', async () => {
  render(<AcceptInvitationPage/>)
  await screen.findByRole('region', { name: 'This invitation grants' })
  expect(fetcher).toHaveBeenCalled()
  expect(seenLocations.every(href => !href.includes('#') && !href.includes(token))).toBe(true)
  expect(fetcher.mock.calls.every(([url]) => !String(url).includes(token))).toBe(true)
})

it('strips and ignores a legacy query-string token', async () => {
  history.replaceState(null, '', `/app/accept-invitation?token=${encodeURIComponent(token)}`)
  render(<AcceptInvitationPage/>)
  expect(await screen.findByText(/This page needs an invitation link\./)).toBeTruthy()
  expect(window.location.search).toBe('')
  expect(fetcher).not.toHaveBeenCalled()
})
