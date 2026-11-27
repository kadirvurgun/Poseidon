import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SignInGate, signInHref } from './SignInGate';

const json = (payload: unknown, ok = true, status = 200) => ({ ok, status, json: async () => payload }) as Response;
const signedOut = { authenticated: false, setupRequired: false, invitationSession: false, csrfToken: '' };
const go = (path: string) => window.history.replaceState(null, '', path);

describe('UAT-DEPLOY-01 full-page sign-in gate', () => {
  afterEach(() => { vi.unstubAllGlobals(); go('/'); });

  it('shows the branded sign-in screen, not the workspace, for an explicit signed-out session', async () => {
    go('/app/work-order-center');
    const fetchMock = vi.fn().mockResolvedValue(json(signedOut));
    vi.stubGlobal('fetch', fetchMock);
    render(<SignInGate><p>workspace</p></SignInGate>);
    const link = await screen.findByRole('link', { name: /Oturum aç/ });
    expect(link).toHaveAttribute('href', '/api/v1/auth/login?returnUrl=%2Fapp%2Fwork-order-center');
    expect(screen.getByRole('heading', { level: 1, name: 'Hoş geldiniz' })).toBeVisible();
    expect(screen.queryByText('workspace')).toBeNull();
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/auth/session', expect.objectContaining({ credentials: 'same-origin' }));
  });

  it('never flashes the sign-in button while the session is being checked', () => {
    go('/app');
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => { })));
    render(<SignInGate><p>workspace</p></SignInGate>);
    expect(screen.getByRole('status')).toHaveTextContent('Oturum denetleniyor');
    expect(screen.queryByRole('link', { name: /Oturum aç/ })).toBeNull();
    expect(screen.queryByText('workspace')).toBeNull();
  });

  it.each([
    ['an authenticated session', () => Promise.resolve(json({ ...signedOut, authenticated: true }))],
    ['identity setup required', () => Promise.resolve(json({ ...signedOut, setupRequired: true }))],
    ['an invitation session', () => Promise.resolve(json({ ...signedOut, invitationSession: true }))],
    ['a network failure (offline work keeps working)', () => Promise.reject(new TypeError('offline'))],
    ['an HTTP error', () => Promise.resolve(json({}, false, 503))],
    ['an unexpected payload', () => Promise.resolve(json({ service: 'poseidon' }))],
  ])('opens the page for %s', async (_, result) => {
    go('/app');
    vi.stubGlobal('fetch', vi.fn(result));
    render(<SignInGate><p>workspace</p></SignInGate>);
    expect(await screen.findByText('workspace')).toBeVisible();
    expect(screen.queryByRole('link', { name: /Oturum aç/ })).toBeNull();
  });

  it.each(['/app/accept-invitation', '/app/help'])('keeps %s public without a session check', path => {
    go(path);
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    render(<SignInGate><p>workspace</p></SignInGate>);
    expect(screen.getByText('workspace')).toBeVisible();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([['request_limit', 'Çok fazla oturum açma denemesi'], ['failed', 'Oturum açılamadı']])('explains an auth=%s return', async (code, text) => {
    go(`/app?auth=${code}`);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(signedOut)));
    render(<SignInGate><p>workspace</p></SignInGate>);
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(text));
  });

  it('only returns to same-site /app paths after sign-in', () => {
    expect(signInHref('/app/fleet/30000000-0000-0000-0000-000000000001')).toBe('/api/v1/auth/login?returnUrl=%2Fapp%2Ffleet%2F30000000-0000-0000-0000-000000000001');
    expect(signInHref('/agents')).toBe('/api/v1/auth/login?returnUrl=%2Fapp');
    expect(signInHref('//evil.invalid/app')).toBe('/api/v1/auth/login?returnUrl=%2Fapp');
  });
});
