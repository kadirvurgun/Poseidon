import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { DefectLifecyclePanel } from './DefectLifecyclePanel'
import { PmsCommandCenterWorkspace, readPmsRoute } from './PmsCommandCenterWorkspace'
import { ServiceReportWorkspace, readServiceRoute } from './ServiceReportWorkspace'
import { centerFixture, lifecycleFixture, serviceFixture, techActor, techDefect, techReport, techTenant, techVessel } from './technical-records-test-fixtures'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
type Handler = (path: string, init?: RequestInit) => Response | undefined
function transport(handler: Handler) {
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, 'https://synthetic.invalid').pathname
    if (path === '/api/v1/auth/session') return json({ authenticated: true, canRead: true, actorId: techActor, tenantId: techTenant, csrfToken: 'synthetic-tech-csrf' })
    return handler(path, init) ?? json({ code: 'permission_denied' }, 403)
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
beforeEach(async () => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); await changeAppLanguage('en', false) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('DEV-055 defect lifecycle panel', () => {
  it('shows blocking alerts and closure blockers and records an assessment with CSRF and an idempotency key', async () => {
    const fetch = transport((path, init) => path.endsWith('/lifecycle') ? json(lifecycleFixture())
      : path.endsWith('/lifecycle/assess') && init?.method === 'POST' ? json(lifecycleFixture({ revision: 3 })) : undefined)
    render(<DefectLifecyclePanel vessel={techVessel} defect={techDefect} defectRevision={1} csrf="synthetic-tech-csrf" language="en"/>)
    const panel = await screen.findByTestId('defect-lifecycle')
    expect(within(panel).getByRole('alert')).toHaveTextContent('Critical defect without an active safeguard')
    expect(panel).toHaveTextContent('CRITICAL_SAFEGUARD_NOT_RECORDED')
    expect(within(panel).queryByRole('button', { name: 'Close and return to service' })).toBeNull()
    const user = userEvent.setup()
    await user.click(within(panel).getByRole('button', { name: 'Record assessment' }))
    const form = within(panel).getByRole('form', { name: 'Assessment' })
    const record = within(form).getByRole('button', { name: 'Record' })
    expect(record).toBeDisabled()
    await user.selectOptions(within(form).getByRole('combobox', { name: 'Criticality' }), 'Critical')
    await user.type(within(form).getByRole('textbox', { name: /Statement/ }), 'FICTIONAL re-assessed after inspection')
    await user.click(record)
    const call = fetch.mock.calls.find(([url, init]) => String(url).includes('/lifecycle/assess') && init?.method === 'POST')!
    const headers = call[1]!.headers as Record<string, string>
    expect(headers['X-CSRF-TOKEN']).toBe('synthetic-tech-csrf'); expect(headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
    expect(JSON.parse(String(call[1]!.body))).toMatchObject({ expectedRevision: 2, criticality: 'Critical' })
  })
  it('shows a closed lifecycle as a recorded human decision, not an editable state', async () => {
    const closer = '00000000-0000-4000-8000-0000000055f9'
    transport(path => path.endsWith('/lifecycle') ? json(lifecycleFixture({ stage: 'CLOSED', closedBy: closer, closedAt: '2026-11-20T10:00:00Z', alerts: [], closeBlockers: [],
      records: [...lifecycleFixture().records, { id: '00000000-0000-4000-8000-000000005509', sequence: 9, kind: 'CLOSE', stageAfter: 'CLOSED', defectRevision: 1, criticality: null,
        measureKind: null, temporary: false, expiresAt: null, measureId: null, outcome: 'RETURNED_TO_SERVICE', text: 'FICTIONAL returned', actorId: closer, recordedAt: '2026-11-20T10:00:00Z' }],
      can: { start: false, assess: false, safeguard: false, lift: false, action: false, verify: false, effectiveness: false, close: false } })) : undefined)
    render(<DefectLifecyclePanel vessel={techVessel} defect={techDefect} defectRevision={1} csrf="x" language="en"/>)
    const panel = await screen.findByTestId('defect-lifecycle')
    expect(panel).toHaveTextContent('Closed (recorded human decision)')
    expect(within(panel).queryAllByRole('button')).toHaveLength(0)
  })
})

describe('DEV-056 PMS command center', () => {
  beforeEach(() => history.replaceState(null, '', `/app/pms-command-center/${techVessel}`))
  it('parses only its own route', () => {
    expect(readPmsRoute(`/app/pms-command-center/${techVessel}`)).toEqual({ vessel: techVessel, invalid: false })
    expect(readPmsRoute('/app/pms-command-center/not-a-guid').invalid).toBe(true)
  })
  it('keeps unknown sources unknown, healthy items quiet, stale data visible and the why chain reachable', async () => {
    transport(path => path === '/api/v1/pms/command-center' ? json(centerFixture()) : undefined)
    render(<PmsCommandCenterWorkspace/>)
    const main = await screen.findByTestId('pms-command-center')
    await within(main).findByText('Blocked')
    const workOrders = main.querySelector('[data-section="open_work_orders"]')!
    expect(workOrders).toHaveAttribute('data-state', 'UNKNOWN')
    expect(workOrders).toHaveTextContent('Source unavailable: unknown, not zero')
    expect(workOrders.textContent).not.toMatch(/\(0\)/)
    expect(main.querySelector('[data-section="critical_overdue_jobs"]')).toHaveTextContent('3 healthy items not listed')
    expect(main.querySelector('[data-section="overdue_jobs"]')).toHaveTextContent('Stale data')
    const alerts = main.querySelector('[data-section="condition_alerts"]') as HTMLElement
    expect(within(alerts).getByText('Why')).toBeInTheDocument()
    expect(alerts).toHaveTextContent('DEFECT_LIFECYCLE_NOT_STARTED')
  })
  it('shows a forbidden state when the server denies the vessel', async () => {
    transport(() => undefined)
    render(<PmsCommandCenterWorkspace/>)
    expect(await screen.findByText('You do not have the server permission for this action or record.')).toBeInTheDocument()
  })
})

describe('DEV-103 service and engineer report', () => {
  beforeEach(() => history.replaceState(null, '', `/app/service-engineer-report/${techVessel}?id=${techReport}`))
  it('parses the canonical report route', () => {
    expect(readServiceRoute({ pathname: `/app/service-engineer-report/${techVessel}`, search: `?id=${techReport}` })).toEqual({ vessel: techVessel, id: techReport })
  })
  it('requires a reason before a non-author records verification and keeps the canonical id after the receipt', async () => {
    const fetch = transport((path, init) => {
      if (path === '/api/v1/service-reports/capabilities') return json({ vesselId: techVessel, canRead: true, canWrite: true, canVerify: true, canLinkWorkOrder: false,
        canLinkDefect: true, canAttachEvidence: false, measurementResults: [] })
      if (path === '/api/v1/service-reports' && !init?.method) return json({ items: [{ id: techReport, reportNo: 'FICTIONAL-SR-1', serviceProvider: 'FICTIONAL Marine Services',
        serviceDate: '2026-11-01', state: 'SUBMITTED', revision: 2, assetId: null, workOrderId: null, verifiedRevision: null, savedAt: '2026-11-20T08:00:00Z', provenance: 'UserDeclaredUnverified' }],
        offset: 0, limit: 25, hasMore: false, canWrite: true })
      if (path === `/api/v1/service-reports/${techReport}` && !init?.method) return json(serviceFixture())
      if (path.endsWith('/verify') && init?.method === 'POST') return json(serviceFixture({ state: 'VERIFIED', verificationState: 'VERIFIED', revision: 3, verifiedRevision: 3, canVerify: false }))
      return undefined
    })
    render(<ServiceReportWorkspace/>)
    await screen.findByRole('heading', { name: 'FICTIONAL-SR-1' })
    const verify = screen.getByRole('button', { name: 'Record verification' })
    expect(verify).toBeDisabled()
    const user = userEvent.setup()
    await user.type(screen.getByRole('textbox', { name: /Reason/ }), 'FICTIONAL checked against service sheet')
    await user.click(verify)
    const call = fetch.mock.calls.find(([url, init]) => String(url).includes('/verify') && init?.method === 'POST')!
    expect(JSON.parse(String(call[1]!.body))).toMatchObject({ decision: 'VERIFY', expectedVersion: 7 })
    expect(window.location.search).toBe(`?id=${techReport}`)
  })
})
