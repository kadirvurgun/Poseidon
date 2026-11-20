import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RiskAssessmentWorkspace, readRiskRoute } from './RiskAssessmentWorkspace'
import { riskActor, riskAssessmentFixture, riskId, riskTenant, riskVessel } from './risk-test-fixtures'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
function transport(reviewStatus = 200) {
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, 'https://synthetic.invalid').pathname
    if (path === '/api/v1/auth/session') return json({ authenticated: true, canRead: true, actorId: riskActor, tenantId: riskTenant, csrfToken: 'synthetic-risk-csrf' })
    if (path === '/api/v1/hse/risk-assessments/capabilities') return json({ vesselId: riskVessel, canRead: true, canWrite: true, canApprove: true, canAcknowledge: false,
      methodConfigured: false, publishedMethods: [], linkKinds: [], workTypes: [], sourceVersion: 'synthetic' })
    if (path === '/api/v1/hse/risk-methods') return json({ items: [], canWrite: false, canPublish: false })
    if (path === '/api/v1/hse/risk-assessments' && !init?.method) return json({ items: [{ id: riskId, assessmentNo: 'FICTIONAL-RA-1', title: 'FICTIONAL mast work', state: 'IN_REVIEW',
      revision: 2, stopWorkRequired: null, approvedRevision: null, savedAt: '2026-11-20T00:00:00Z', provenance: 'UserDeclaredUnverified' }], offset: 0, limit: 25, hasMore: false, canWrite: true })
    if (path === '/api/v1/hse/risk-assessments/' + riskId && !init?.method) return json(riskAssessmentFixture())
    if (path.endsWith('/review')) return reviewStatus === 200 ? json(riskAssessmentFixture({ state: 'RETURNED', canReview: false, revision: 3, version: 8 }))
      : json({ code: 'version_conflict', correlationId: 'x' }, reviewStatus)
    return json({ code: 'permission_denied' }, 403)
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
beforeEach(() => { history.replaceState(null, '', `/app/risk-assessment/${riskVessel}?id=${riskId}`); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function open() {
  render(<RiskAssessmentWorkspace />); const user = userEvent.setup()
  await user.selectOptions(screen.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }), 'en')
  await screen.findByRole('heading', { name: /FICTIONAL-RA-1/ })
  return user
}

describe('DEV-105 risk assessment workspace', () => {
  it('keeps the canonical vessel/id route and rejects malformed identifiers', () => {
    expect(readRiskRoute({ pathname: `/app/risk-assessment/${riskVessel}`, search: `?id=${riskId}` })).toEqual({ vessel: riskVessel, id: riskId })
    expect(readRiskRoute({ pathname: '/app/risk-assessment/not-a-guid', search: '?id=x' })).toEqual({ vessel: null, id: null })
  })
  it('shows an unconfigured method as not assessed, never as an approved or rated state', async () => {
    transport(); await open()
    expect(screen.getAllByText('Method not configured').length).toBeGreaterThan(0)
    expect(screen.queryByText(/Approved \(human record\)/)).toBeNull()
    expect(screen.getByText('stop_work_unknown')).toBeTruthy()
  })
  it('sends a human return decision with reason, CSRF and an Idempotency-Key only after a valid reason', async () => {
    const fetch = transport(), user = await open()
    const approve = screen.getByRole('button', { name: 'Approve (human review record)' })
    expect((approve as HTMLButtonElement).disabled).toBe(true)
    await user.type(screen.getByLabelText('Reason (at least 10 characters)'), 'FICTIONAL residual rationale missing')
    await user.click(screen.getByRole('button', { name: 'Return' }))
    const call = fetch.mock.calls.find(([u, i]) => String(u).includes('/review') && i?.method === 'POST')!
    const headers = call[1]!.headers as Record<string, string>
    expect(headers['X-CSRF-TOKEN']).toBe('synthetic-risk-csrf'); expect(headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
    expect(JSON.parse(call[1]!.body as string)).toMatchObject({ decision: 'RETURN', expectedVersion: 7, contentHash: 'A'.repeat(64) })
  })
  it('surfaces a version conflict with a reload action instead of retrying silently', async () => {
    const fetch = transport(409), user = await open()
    await user.type(screen.getByLabelText('Reason (at least 10 characters)'), 'FICTIONAL review against SMS')
    await user.click(screen.getByRole('button', { name: 'Approve (human review record)' }))
    expect(await screen.findByText(/changed elsewhere/)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Reload' })).toBeTruthy()
    expect(fetch.mock.calls.filter(([u, i]) => String(u).includes('/review') && i?.method === 'POST')).toHaveLength(1)
  })
})
