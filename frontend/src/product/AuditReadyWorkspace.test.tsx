import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import * as api from './api'
import { AuditReadyWorkspace } from './AuditReadyWorkspace'
import { auditReadyTrace, chains } from './audit-ready-api'
import { arChains, arControl, arEvidence, arTrace, arVessel } from './audit-ready.test-fixture'
import { changeAppLanguage } from '../i18n'

// DEV-032 / T-S3-007 (18.01 Backlog row:33). Mocked API verifies UI behavior only.
vi.mock('./EnterpriseChrome', () => ({ EnterpriseChrome: () => <nav aria-label="chrome" /> }))
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), session: vi.fn(), vessels: vi.fn() }))
vi.mock('./audit-ready-api', async original => ({ ...await original<typeof import('./audit-ready-api')>(), chains: vi.fn(), auditReadyTrace: vi.fn() }))
const tenant = '10000000-0000-4000-8000-000000000002'
const chainHref = `/app/audit-ready/${arVessel}?evidenceId=${arEvidence}&evidenceRevision=2&controlId=${arControl}&controlRevision=1`
beforeEach(async () => {
  await changeAppLanguage('en', false)
  history.replaceState(null, '', `/app/audit-ready/${arVessel}`)
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  vi.mocked(api.session).mockResolvedValue({ authenticated: true, canRead: true, actorId: tenant, tenantId: tenant, tenantName: 'FICTIONAL tenant', csrfToken: 'synthetic-only', setupRequired: false, canCreate: false, canReadCompanies: false, canSearch: false })
  vi.mocked(api.vessels).mockResolvedValue({ items: [{ id: arVessel, companyId: tenant, code: 'SYN-AR', name: 'FICTIONAL vessel', kind: 'Ship', version: 1, canEdit: false }], offset: 0, limit: 25, hasMore: false })
  vi.mocked(chains).mockResolvedValue(arChains())
  vi.mocked(auditReadyTrace).mockResolvedValue(arTrace())
})
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('drills down requirement to decision with keyboard, source locator, timestamps and no green/ready state', async () => {
  const user = userEvent.setup(); render(<AuditReadyWorkspace />)
  await user.click(await screen.findByRole('button', { name: new RegExp(`Evidence ${arEvidence}`) }))
  expect(window.location.pathname + window.location.search).toBe(chainHref)
  const trace = await screen.findByTestId('audit-ready-trace')
  expect(trace).toHaveAttribute('data-disposition', 'REVIEW_REQUIRED')
  expect(within(trace).getByText('Review required')).toHaveAttribute('data-tone', 'warning')
  const stages = within(trace).getAllByRole('listitem').filter(li => li.hasAttribute('data-stage')).map(li => li.getAttribute('data-stage'))
  expect(stages).toEqual(['requirement', 'control', 'evidence', 'verification', 'decision'])
  // Keyboard drill-down: focus the requirement stage toggle and open it with Enter.
  const requirement = within(trace).getByRole('button', { name: /^Requirement/ })
  expect(requirement).toHaveAttribute('aria-expanded', 'false')
  requirement.focus(); await user.keyboard('{Enter}')
  expect(requirement).toHaveAttribute('aria-expanded', 'true')
  const region = screen.getByRole('region', { name: 'Requirement · Details' })
  expect(within(region).getByText('FICTIONAL Code/Chapter 9/para 4').tagName).toBe('CITE')
  expect(within(region).getByText('Source locator (declared, unverified)')).toBeVisible()
  // Unavailable applicability is review-required, never ready.
  expect(within(region).getAllByText('Unavailable')[0]).toHaveAttribute('data-tone', 'warning')
  const decision = within(trace).getByRole('button', { name: /^Decision/ })
  await user.click(decision)
  const decided = screen.getByRole('region', { name: 'Decision · Details' })
  expect(within(decided).getByText('HUMAN_DECIDED')).toBeVisible(); expect(within(decided).getByText('DPA')).toBeVisible()
  expect(within(decided).getByText('2'.repeat(64))).toBeVisible()
  expect(within(decided).getByText('2026-11-18T11:00:00+00:00').tagName).toBe('TIME')
  // No element anywhere uses a success/green tone or a ready/approved label.
  expect(document.querySelector('[data-tone="success"], [data-tone="info"]')).toBeNull()
  expect(screen.queryByText(/^(ready|approved|compliant)$/i)).not.toBeInTheDocument()
})

it('shows UNKNOWN evidence time state and missing verification as warning, not as recorded', async () => {
  history.replaceState(null, '', chainHref)
  vi.mocked(auditReadyTrace).mockResolvedValue(arTrace({ auditItems: [] }))
  const user = userEvent.setup(); render(<AuditReadyWorkspace />)
  const trace = await screen.findByTestId('audit-ready-trace')
  const verification = within(trace).getByRole('button', { name: /^Verification/ })
  expect(within(verification).getAllByText('Not recorded')[0]).toHaveAttribute('data-tone', 'warning')
  await user.click(within(trace).getByRole('button', { name: /^Evidence/ }))
  expect(within(screen.getByRole('region', { name: 'Evidence · Details' })).getByText('UNKNOWN')).toHaveAttribute('data-tone', 'warning')
})

it('renders Turkish drill-down copy', async () => {
  await changeAppLanguage('tr', false)
  history.replaceState(null, '', chainHref)
  render(<AuditReadyWorkspace />)
  const trace = await screen.findByTestId('audit-ready-trace')
  expect(within(trace).getByText('İnceleme gerekli')).toBeVisible()
  expect(within(trace).getByRole('button', { name: /^Gereksinim/ })).toBeVisible()
  expect(within(trace).getByRole('button', { name: /^Karar/ })).toBeVisible()
  expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Denetime hazır iz')
})

it('shows empty, unavailable and not-found chain states', async () => {
  vi.mocked(chains).mockResolvedValue(arChains({ items: [], unavailable: 2 }))
  history.replaceState(null, '', chainHref)
  render(<AuditReadyWorkspace />)
  expect(await screen.findByText('No trace chains are assigned to you for this vessel.')).toHaveAttribute('data-state', 'empty')
  expect(screen.getByText('Assigned chains currently unavailable: 2')).toBeVisible()
  expect(screen.getByRole('alert')).toHaveTextContent('Chain not found or no longer visible to you.')
  expect(auditReadyTrace).not.toHaveBeenCalled()
})

it('shows denial and error states without trace content', async () => {
  vi.mocked(chains).mockRejectedValue(new api.ApiError(403))
  const { unmount } = render(<AuditReadyWorkspace />)
  expect(await screen.findByRole('alert')).toHaveTextContent('You are not authorized for this view.')
  unmount()
  vi.mocked(chains).mockResolvedValue(arChains())
  vi.mocked(auditReadyTrace).mockRejectedValue(new api.ApiError(503))
  history.replaceState(null, '', chainHref)
  render(<AuditReadyWorkspace />)
  expect(await screen.findByText('The trace could not be loaded.')).toBeVisible()
  expect(screen.queryByTestId('audit-ready-trace')).not.toBeInTheDocument()
})

it('flags stale superseded inputs and keeps prior data marked stale while offline', async () => {
  history.replaceState(null, '', chainHref)
  const trace = arTrace(); trace.auditItems![1] = { ...trace.auditItems![1], currentState: 'SUPERSEDED_REVIEW_REQUIRED' }
  vi.mocked(auditReadyTrace).mockResolvedValue(trace)
  render(<AuditReadyWorkspace />)
  const shown = await screen.findByTestId('audit-ready-trace')
  expect(within(shown).getByText('Stale: an input was superseded or expired; review required.')).toHaveAttribute('data-state', 'stale')
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
  act(() => window.dispatchEvent(new Event('offline')))
  expect(within(screen.getByTestId('audit-ready-trace')).getByText(/You are offline/)).toHaveAttribute('data-state', 'offline-stale')
  expect(screen.getByRole('button', { name: 'Retry' })).toBeDisabled()
})

it('shows loading then invalid route without requesting data', async () => {
  history.replaceState(null, '', `/app/audit-ready/${arVessel}?actorId=${arEvidence}`)
  render(<AuditReadyWorkspace />)
  expect(screen.getByRole('alert')).toHaveTextContent('This address is not a valid audit-ready trace.')
  expect(chains).not.toHaveBeenCalled(); expect(api.session).not.toHaveBeenCalled()
})
