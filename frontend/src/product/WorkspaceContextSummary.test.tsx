import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { WorkspaceContextBar } from './WorkspaceContextBar'
import { ApiError } from './api'
import { loadWorkspaceContext } from './workspace-context-api'
import type { Session } from './directory-contract'

vi.mock('./workspace-context-api', () => ({ loadWorkspaceContext: vi.fn() }))
const tenant = '10000000-0000-0000-0000-000000000011'
const actor = '10000000-0000-0000-0000-000000000012'
const vessel = '10000000-0000-0000-0000-000000000013'
const session: Session = { authenticated: true, canRead: true, actorId: actor, tenantId: tenant,
  tenantName: 'FICTIONAL workspace', csrfToken: 'synthetic-only', setupRequired: false,
  canReadCompanies: false, canCreate: false, canSearch: false }

describe('DEV-025 narrow-screen context summary (65.03 paragraph 48)', () => {
  beforeEach(() => {
    vi.mocked(loadWorkspaceContext).mockResolvedValue({ tenantId: tenant, actorId: actor,
      roleContext: { state: 'Configured', catalogVersion: 'synthetic-v1', roles: [{ reference: actor, tr: 'KURGUSAL kaptan', en: 'FICTIONAL Master' }] },
      company: { id: tenant, code: 'SYN-CO', name: 'FICTIONAL company' },
      vessel: { id: vessel, companyId: tenant, code: 'SYN-A', name: 'FICTIONAL Alpha', kind: 'Tug' } })
  })
  afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks() })

  it('DEV025-AC-07 names the summary button with the full context and toggles the details it controls', async () => {
    const user = userEvent.setup()
    render(<WorkspaceContextBar session={session} vessel={vessel} language="en" />)
    const summary = await screen.findByRole('button', { name: /^Current workspace context: FICTIONAL workspace/ })
    const name = summary.getAttribute('aria-label') ?? ''
    for (const part of ['SYN-CO · FICTIONAL company', 'SYN-A · FICTIONAL Alpha', 'Vessel mode: Tug', 'FICTIONAL Master', 'Online'])
      expect(name).toContain(part)
    // Visible label (vessel code) is part of the accessible name (label in name).
    expect(summary).toHaveTextContent('SYN-A')
    expect(summary).toHaveAttribute('aria-expanded', 'false')
    const details = document.getElementById(summary.getAttribute('aria-controls')!)
    expect(details).toHaveClass('context-details')
    expect(screen.getByRole('region', { name: 'Current workspace context' })).toHaveAttribute('data-expanded', 'false')
    await user.click(summary)
    expect(summary).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('region', { name: 'Current workspace context' })).toHaveAttribute('data-expanded', 'true')
    // The polite live summary stays in the DOM (only visually clipped when collapsed) and contains no nested live region.
    const live = details!.querySelector('[aria-live="polite"]')!
    expect(live).toHaveTextContent('SYN-A · FICTIONAL Alpha')
    expect(live.querySelector('[aria-live], [role="status"], [role="alert"]')).toBeNull()
  })

  it('DEV025-AC-07 renders no summary while the context is not verified, so errors stay visible', async () => {
    vi.mocked(loadWorkspaceContext).mockRejectedValue(new ApiError(503))
    render(<WorkspaceContextBar session={session} vessel={vessel} language="tr" />)
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Geçerli çalışma bağlamı:/ })).not.toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Geçerli çalışma bağlamı' })).not.toHaveAttribute('data-collapsible')
  })
})
