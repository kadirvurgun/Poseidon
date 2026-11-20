import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { MeasurementPointsPanel, PostponementPanel } from './PmsPanels'
import { ApiError, request, session } from './api'
vi.mock('./api', async () => ({ ...(await vi.importActual('./api')), request: vi.fn(), session: vi.fn() }))
const mocked = vi.mocked(request), sessions = vi.mocked(session)
const vessel = '33333333-3333-3333-3333-333333333333', asset = '44444444-4444-4444-4444-444444444444', plan = '66666666-6666-6666-6666-666666666666'
const point = (over: Record<string, unknown> = {}) => ({ id: '77777777-7777-7777-7777-777777777777', assetId: asset, code: 'FICTIONAL-EXH', kind: 'TEMPERATURE', unit: 'CEL',
  source: 'MANUAL', precision: 1, rangeMin: null, rangeMax: null, parentPointId: null, revision: 1, version: 5, provenance: 'UserDeclaredUnverified',
  latest: { id: '88888888-8888-8888-8888-888888888888', pointId: '77777777-7777-7777-7777-777777777777', value: '460.0', unit: 'CEL', measuredAt: '2026-11-20T10:00:00Z',
    recordedAt: '2026-11-20T10:00:01Z', sourceType: 'MANUAL', sourceReference: null, evidenceFileId: null, previousReadingId: null, delta: null, propagatedFromReadingId: null,
    actorId: '99999999-9999-9999-9999-999999999999', provenance: 'UserDeclaredUnverified' },
  band: 'UNCONFIGURED', policyState: 'UNCONFIGURED', policyVersion: null, freshnessMinutes: null, freshness: 'UNCONFIGURED', ...over })
const points = (items: unknown[], canWrite = true) => ({ items, canWrite, kinds: ['RUNNING_HOURS', 'TEMPERATURE'], units: { RUNNING_HOURS: ['HOUR'], TEMPERATURE: ['CEL', 'KEL'] },
  sources: ['MANUAL', 'DEVICE', 'IMPORT'], sourceVersion: 'synthetic' })
const item = (over: Record<string, unknown> = {}) => ({ id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', planId: plan, planRevision: 1, assetId: asset, workOrderId: null, defectIds: [],
  requirementRefs: [], originalDue: '2026-12-01', requestedDeadline: '2026-12-20', extensionDays: 19, reason: 'FICTIONAL parts delayed', riskAssessmentId: null,
  criticality: 'STANDARD', state: 'REQUESTED', effectiveState: 'REQUESTED', readinessImpact: 'PENDING_DECISION', requestedBy: '99999999-9999-9999-9999-999999999999',
  requestedAt: '2026-11-20T10:00:00Z', decidedBy: null, decidedAt: null, decisionReason: null, policyRef: null, policyVersion: null, maxExtensionDays: null,
  revision: 1, version: 42, canDecide: true, decisionBlockers: [], ...over })
const page = (over: Record<string, unknown> = {}) => ({ planId: plan, planRevision: 1, originalDue: '2026-12-01', dueState: 'NotDue', criticality: 'STANDARD',
  policyState: 'UNCONFIGURED', policyRef: null, policyVersion: null, maxExtensionDays: null, canRequest: true, canDecide: true, items: [item()], sourceVersion: 'synthetic', ...over })
const posts = () => mocked.mock.calls.filter(call => call[2]?.method === 'POST')
beforeEach(() => {
  vi.clearAllMocks()
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  sessions.mockResolvedValue({ authenticated: true, tenantId: vessel, actorId: asset, csrfToken: 'synthetic-csrf' } as Awaited<ReturnType<typeof session>>)
})
afterEach(() => cleanup())

it('shows an unconfigured threshold band as unconfigured, never normal, and filters to the selected equipment', async () => {
  mocked.mockResolvedValue(points([point(), point({ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', assetId: plan, code: 'OTHER-ASSET' })]))
  render(<MeasurementPointsPanel vessel={vessel} asset={asset} language="en"/>)
  const badge = await screen.findByText('Unconfigured', { selector: '.pms-badge' })
  expect(badge).toHaveAttribute('data-state', 'UNCONFIGURED')
  expect(screen.queryByText('Normal', { selector: '.pms-badge' })).toBeNull()
  expect(screen.queryByText('OTHER-ASSET')).toBeNull()
  expect(screen.getByText('FICTIONAL-EXH')).toBeInTheDocument()
})

it('rejects a band outside the closed vocabulary instead of rendering it', async () => {
  mocked.mockResolvedValue(points([point({ band: 'READY' })]))
  render(<MeasurementPointsPanel vessel={vessel} asset={asset} language="en"/>)
  expect(await screen.findByRole('alert')).toHaveTextContent('The data is unavailable')
})

it('records a reading with an idempotency key and shows the 422 refusal code text', async () => {
  mocked.mockImplementation(async (_path, _signal, init) => {
    if (init?.method === 'POST') throw new ApiError(422, 'reading_out_of_range')
    return points([point()])
  })
  render(<MeasurementPointsPanel vessel={vessel} asset={asset} language="en"/>)
  fireEvent.change(await screen.findByLabelText('Measurement points', { selector: 'select' }), { target: { value: '77777777-7777-7777-7777-777777777777' } })
  fireEvent.change(screen.getByLabelText('Value'), { target: { value: '701.0' } })
  fireEvent.change(screen.getByLabelText('Measured at'), { target: { value: '2026-11-20T11:00' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save reading' }))
  expect(await screen.findByText('The value is outside the declared range.')).toBeInTheDocument()
  const [path, , init] = posts()[0]
  expect(path).toBe(`equipment-hierarchy/${vessel}/measurement-points/77777777-7777-7777-7777-777777777777/readings`)
  expect((init!.headers as Record<string, string>)['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
  expect(JSON.parse(init!.body as string)).toMatchObject({ value: '701.0', sourceType: 'MANUAL' })
})

it('states that an unconfigured tenant policy cannot approve and shows approval blockers', async () => {
  mocked.mockResolvedValue(page({ items: [item({ decisionBlockers: ['postponement_policy_unconfigured'] })] }))
  render(<PostponementPanel vessel={vessel} plan={plan} language="en"/>)
  expect(await screen.findByText(/can be recorded but cannot be approved/)).toBeInTheDocument()
  expect(screen.getByText('No maximum-extension policy is configured for this tenant; approval is closed.')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled()
  expect(screen.getByText('Pending decision', { selector: '.pms-badge' })).toBeInTheDocument()
})

it('does not offer a decision to the requester and marks critical readiness as review required', async () => {
  mocked.mockResolvedValue(page({ criticality: 'UNKNOWN', items: [item({ canDecide: false, criticality: 'UNKNOWN', readinessImpact: 'REVIEW_REQUIRED' })] }))
  render(<PostponementPanel vessel={vessel} plan={plan} language="en"/>)
  expect(await screen.findByText('Review required', { selector: '.pms-badge' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Reject' })).toBeNull()
})

it('retries an unknown decision outcome with the same key and body', async () => {
  let attempts = 0
  mocked.mockImplementation(async (_path, _signal, init) => {
    if (init?.method === 'POST') { attempts++; if (attempts === 1) throw new TypeError('synthetic acknowledgement loss'); return { postponement: item({ state: 'REJECTED' }) } }
    return page({ policyState: 'CONFIGURED', policyRef: 'FICTIONAL', policyVersion: 'v1', maxExtensionDays: 30 })
  })
  render(<PostponementPanel vessel={vessel} plan={plan} language="en"/>)
  fireEvent.change(await screen.findByLabelText('Decision reason'), { target: { value: 'FICTIONAL perform before departure' } })
  fireEvent.click(screen.getByRole('button', { name: 'Reject' }))
  expect(await screen.findByText(/The outcome is unknown/)).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Reject' }))
  await waitFor(() => expect(posts()).toHaveLength(2))
  const [first, second] = posts().map(call => call[2]!)
  expect((second.headers as Record<string, string>)['Idempotency-Key']).toBe((first.headers as Record<string, string>)['Idempotency-Key'])
  expect(second.body).toBe(first.body)
  expect(JSON.parse(first.body as string)).toEqual({ expectedVersion: 42, decision: 'REJECT', reason: 'FICTIONAL perform before departure' })
})
