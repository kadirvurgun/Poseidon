import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DataReconciliationWorkspace, reconciliationRoute } from './DataReconciliationWorkspace'
import { decodeListView, decodeReportView } from './data-reconciliation-api'
import { changeAppLanguage } from '../i18n'

// S11 WP-A DEV-069/070 UI. Synthetic identities and FICTIONAL data only; the mocked API proves UI behaviour only and
// no sign-off here is a real data owner acceptance.
const vessel = '11111111-1111-4111-8111-111111111111', report = '22222222-2222-4222-8222-222222222222', batch = '33333333-3333-4333-8333-333333333333'
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', grant = '66666666-6666-4666-8666-666666666666'
const asset = '77777777-7777-4777-8777-777777777777', author = '88888888-8888-4888-8888-888888888888'
const H = 'c'.repeat(64), T = '2026-11-22T08:00:00Z'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const counts = { source: 4, accepted: 2, rejected: 1, duplicate: 1, notCommitted: 1, missingCritical: 1 }
const target = { batchId: batch, rowNumber: 2, targetKind: 'Asset', targetId: asset }
const reportView = (patch: Record<string, unknown> = {}, reportPatch: Record<string, unknown> = {}) => ({
  report: { id: report, vesselId: vessel, revision: 1, version: 9, state: 'AWAITING_SIGN_OFF', policyVersion: 'DEV-070-reconciliation-v1', criticalFieldVersion: 'DEV-070-critical-fields-v1',
    batches: [{ batchId: batch, category: 'equipment', revision: 3, state: 'Committed', sourceHash: 'AB', templateFingerprint: 'FP', previewId: null, sourceRows: 4, acceptedRows: 2,
      rejectedRows: 1, duplicateRows: 1, uploadedBy: author }], counts, missingCritical: [{ ...target, field: 'criticality', reason: 'MISSING' }], missingCriticalTruncated: false,
    sampleSeed: 'seed', sample: [target], hash: H, createdBy: author, createdAt: T, supersededAt: null, provenance: 'UserDeclaredUnverified', ...reportPatch },
  signOff: null, stale: false, openItems: ['MISSING_CRITICAL', 'REJECTED_ROWS', 'DUPLICATE_ROWS', 'NOT_COMMITTED', 'SAMPLE_UNVERIFIED'], authorities: [{ id: grant, duty: 'DataOwner' }],
  canSignOff: true, signOffBlockers: [], decisions: [], exceptionKinds: [], sourceLocator: '18.01/Backlog/row:71', ...patch })
const listView = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, items: [{ id: report, state: 'AWAITING_SIGN_OFF', revision: 1, counts, createdAt: T, decision: null,
  signOffValidity: 'NONE' }], batches: [{ id: batch, category: 'equipment', state: 'Committed', rowCount: 4, createdAt: T, committedAt: T, templateVersion: 'DEV-069-declaration-v3/equipment' }],
  canCreate: true, authorities: [], templateVersions: { equipment: 'DEV-069-declaration-v3/equipment', crew: 'DEV-069-declaration-v4/crew' }, maxBatches: 50, maxSample: 100,
  sourceLocator: '18.01/Backlog/row:71', ...patch })

const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let bodies: Record<string, unknown> = {}, status: Record<string, number> = {}
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; bodies = { list: listView(), report: reportView() }; status = {}
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST') { commands.push({ url, init }); return json(bodies.report, status.command ?? 200) }
    if (url.endsWith(`reconciliation-reports/${report}`)) return json(bodies.report, status.report ?? 200)
    if (url.endsWith('reconciliation-reports')) return json(bodies.list, status.list ?? 200)
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('parses the closed route and refuses unknown or malformed parameters', () => {
  expect(reconciliationRoute(`/app/data-reconciliation/${vessel}`)).toMatchObject({ vessel, report: '', invalid: false })
  expect(reconciliationRoute(`/app/data-reconciliation/${vessel}?report=${report}`)).toMatchObject({ report, invalid: false })
  expect(reconciliationRoute(`/app/data-reconciliation/${vessel}?report=nope`).invalid).toBe(true)
  expect(reconciliationRoute(`/app/data-reconciliation/${vessel}?batch=${batch}`).invalid).toBe(true)
})

it('rejects contracts with unexplained counts, unknown states or an effective sign-off on a superseded report', () => {
  expect(() => decodeReportView(reportView({}, { counts: { ...counts, accepted: 3 } }), vessel)).toThrow()
  expect(() => decodeReportView(reportView({}, { state: 'ACCEPTED' }), vessel)).toThrow()
  expect(() => decodeReportView(reportView({ signOff: { id: grant, decision: 'ACCEPTED', reason: 'FICTIONAL reason', exceptions: [], sampleVerified: [], duty: 'DataOwner',
    signedBy: actorId, signedAt: T, reportHash: H, validity: 'EFFECTIVE' } }, { state: 'SUPERSEDED' }), vessel)).toThrow()
  expect(() => decodeReportView(reportView({}, { vesselId: batch }), vessel)).toThrow()
  expect(() => decodeListView(listView({ templateVersions: { drydock: 'x' } }), vessel)).toThrow()
  expect(decodeReportView(reportView(), vessel).report.counts.source).toBe(4)
})

it('lists template versions and an unsigned report as awaiting sign-off, never as accepted', async () => {
  history.replaceState(null, '', `/app/data-reconciliation/${vessel}`)
  render(<DataReconciliationWorkspace/>)
  expect(await screen.findByText('DEV-069-declaration-v4/crew')).toBeInTheDocument()
  const badge = screen.getByText('Awaiting sign-off')
  expect(badge).toHaveAttribute('data-tone', 'warning')
  expect(screen.queryByText('Signed by data owner')).not.toBeInTheDocument()
})

it('creates a report from selected batches with an idempotency key and moves its id into the URL', async () => {
  history.replaceState(null, '', `/app/data-reconciliation/${vessel}`)
  const user = userEvent.setup()
  render(<DataReconciliationWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Create report' })
  await user.click(within(form).getByRole('checkbox'))
  await user.clear(within(form).getByLabelText('Sample size')); await user.type(within(form).getByLabelText('Sample size'), '5')
  await user.click(within(form).getByRole('button', { name: 'Create report' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  const body = JSON.parse(String(commands[0].init.body))
  expect(body).toMatchObject({ batchIds: [batch], sampleSize: 5 }); expect(body.id).toMatch(/^[0-9a-f-]{36}$/)
  expect(new Headers(commands[0].init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
  await waitFor(() => expect(window.location.search).toBe(`?report=${body.id}`))
})

it('records the data owner sign-off with exceptions on the exact report version and hash', async () => {
  history.replaceState(null, '', `/app/data-reconciliation/${vessel}?report=${report}`)
  const user = userEvent.setup()
  render(<DataReconciliationWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Data owner sign-off' })
  expect(screen.getByText('criticality')).toBeInTheDocument()
  await user.selectOptions(within(form).getByLabelText('Decision'), 'ACCEPTED_WITH_EXCEPTIONS')
  await user.click(within(form).getByRole('button', { name: 'Add exception' }))
  await user.type(within(form).getByLabelText('Reference'), 'FICTIONAL row 2')
  await user.type(within(form).getByLabelText('Disposition'), 'FICTIONAL owner will complete criticality')
  await user.click(screen.getByRole('checkbox'))
  await user.type(within(form).getByLabelText('Reason'), 'FICTIONAL owner reviewed counts and sample')
  await user.click(within(form).getByRole('button', { name: 'Data owner sign-off' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toContain(`vessels/${vessel}/reconciliation-reports/${report}/sign-off`)
  expect(JSON.parse(String(commands[0].init.body))).toMatchObject({ expectedVersion: 9, reportHash: H, decision: 'ACCEPTED_WITH_EXCEPTIONS', authorityId: grant,
    sampleVerified: [asset], exceptions: [{ kind: 'MISSING_CRITICAL', reference: 'FICTIONAL row 2', disposition: 'FICTIONAL owner will complete criticality' }] })
})

it('shows blockers instead of a sign-off form for the author and an invalidated sign-off after supersession', async () => {
  bodies.report = reportView({ canSignOff: false, authorities: [], signOffBlockers: ['data_owner_authority_required', 'separation_of_duties'] })
  history.replaceState(null, '', `/app/data-reconciliation/${vessel}?report=${report}`)
  const { unmount } = render(<DataReconciliationWorkspace/>)
  expect(await screen.findByText('The report author or a batch uploader cannot sign.')).toBeInTheDocument()
  expect(screen.queryByRole('form', { name: 'Data owner sign-off' })).not.toBeInTheDocument()
  unmount()
  bodies.report = reportView({ canSignOff: false, signOffBlockers: ['reconciliation_superseded'], signOff: { id: grant, decision: 'ACCEPTED', reason: 'FICTIONAL reason',
    exceptions: [], sampleVerified: [asset], duty: 'DataOwner', signedBy: actorId, signedAt: T, reportHash: H, validity: 'INVALIDATED_SUPERSEDED' } },
    { state: 'SUPERSEDED', revision: 3, supersededAt: T })
  render(<DataReconciliationWorkspace/>)
  expect(await screen.findByText('Sign-off invalidated (superseded)')).toBeInTheDocument()
  expect(screen.getByText('Superseded')).toHaveAttribute('data-tone', 'danger')
})

it('shows a conflict notice when the server refuses a stale sign-off and a forbidden state when denied', async () => {
  status.command = 409
  history.replaceState(null, '', `/app/data-reconciliation/${vessel}?report=${report}`)
  const user = userEvent.setup()
  const { unmount } = render(<DataReconciliationWorkspace/>)
  const form = await screen.findByRole('form', { name: 'Data owner sign-off' })
  await user.selectOptions(within(form).getByLabelText('Decision'), 'REJECTED')
  await user.type(within(form).getByLabelText('Reason'), 'FICTIONAL counts do not match the source')
  await user.click(within(form).getByRole('button', { name: 'Data owner sign-off' }))
  expect(await screen.findByText('Changed elsewhere or superseded; reload.')).toBeInTheDocument()
  unmount()
  status.report = 403
  render(<DataReconciliationWorkspace/>)
  expect(await screen.findByText("You do not have access to this vessel's reconciliation.")).toBeInTheDocument()
})
