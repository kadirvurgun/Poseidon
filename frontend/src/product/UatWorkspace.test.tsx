import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { UatWorkspace, readUatRoute } from './UatWorkspace'
import { decodeUatSummary } from './pilot-gate-api'
import { changeAppLanguage } from '../i18n'

// S11 WP-D DEV-074/094 UI. Synthetic identities and FICTIONAL codes only; no UAT acceptance fixture.
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555', other = '66666666-6666-4666-8666-666666666666'
const runId = '77777777-7777-4777-8777-777777777777', findingId = '88888888-8888-4888-8888-888888888888'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const scenario = (id: string, scope: string) => ({ id, scope, journey: 'FICTIONAL journey', passProof: 'FICTIONAL pass proof ' + id, locator: '19.01/paragraph:1' })
const catalog = { version: '19.01-v46.3-2026-10-22', scenarios: [scenario('S-UAT-01', 'SHIP'), scenario('S-UAT-04', 'SHIP'), scenario('T-UAT-01', 'TUG')],
  shipRoles: ['MASTER', 'DPA', 'CHIEF_ENGINEER', 'CREW'], tugRoles: ['TUG_MASTER'], environments: ['UAT', 'LAB'], outcomes: [], severities: [], authority: 'x' }
const run = (patch: Record<string, unknown> = {}) => ({ id: runId, scope: 'SHIP', role: 'MASTER', environment: 'UAT', buildCommit: 'f1c2d51', participantPseudonym: 'P-MASTER-01',
  catalogVersion: catalog.version, state: 'SUBMITTED', facilitatorActorId: other, createdAt: '2026-11-24T08:00:00Z', submittedAt: '2026-11-24T09:00:00Z', attestedBy: null,
  attestedAt: null, attestationStatement: null, provenance: 'UserDeclaredUnverified', version: 7, ...patch })
const finding = (patch: Record<string, unknown> = {}) => ({ id: findingId, runId, scope: 'SHIP', scenarioId: 'S-UAT-04', severity: 'BLOCKER', isUx: true, designConformance: false,
  title: 'FICTIONAL blocker', state: 'OPEN', reportedBy: other, reportedAt: '2026-11-24T08:30:00Z', resolvedBy: null, resolvedAt: null, resolution: null, resolutionReference: null, version: 3, ...patch })
const summary = (patch: Record<string, unknown> = {}) => ({ scope: 'SHIP', catalogVersion: catalog.version, threshold: null, thresholdSequence: 0,
  gate: { scope: 'SHIP', state: 'CRITERIA_NOT_MET', gateState: 'FAIL', reasons: ['BLOCKER_FINDINGS_OPEN', 'THRESHOLD_UNSET'], scenarios: [
    { scenarioId: 'S-UAT-01', attestedRuns: 0, passed: 0, failed: 0, blocked: 0, notRun: 0, passPercent: null }], openBlockers: 1, openMajors: 0, openDesignConformance: 0,
    attestedRuns: 0, pendingAttestation: 1, authority: 'HumanUatEvidenceOnly' },
  runs: [run()], openFindings: [finding()], canRecord: true, canRecordThreshold: true, canAttest: true, authority: 'HumanUatEvidenceOnly', ...patch })
const detail = () => ({ run: run(), results: [{ id: '99999999-9999-4999-8999-999999999999', runId, scenarioId: 'S-UAT-04', attempt: 1, outcome: 'PASS', durationSeconds: null,
  assisted: false, automatedEvidenceReference: 'FICTIONAL-PW-1', note: null, recordedBy: other, recordedAt: '2026-11-24T08:10:00Z', evidenceRule: 'AutomatedEvidenceIsAttachmentOnly' }],
  findings: [finding()], canRecord: false, canAttest: true })
const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
let body: unknown = summary(), commandStatus = 200
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; body = summary(); commandStatus = 200
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST') { commands.push({ url, init }); return json(commandStatus === 200 ? run({ state: 'ATTESTED', attestedBy: actorId }) : { code: 'separation_of_duties_required' }, commandStatus) }
    if (url.includes('uat/scenarios')) return json(catalog)
    if (url.includes('uat/summary')) return typeof body === 'number' ? json({ code: 'permission_denied' }, body) : json(body)
    if (url.includes(`uat/runs/${runId}`)) return json(detail())
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('parses closed routes and refuses unknown scopes, keys or run ids', () => {
  expect(readUatRoute('/app/role-uat?scope=TUG')).toMatchObject({ scope: 'TUG', run: null, invalid: false })
  expect(readUatRoute(`/app/role-uat?scope=SHIP&run=${runId}`)).toMatchObject({ run: runId, invalid: false })
  expect(readUatRoute('/app/role-uat?scope=BOTH').invalid).toBe(true)
  expect(readUatRoute('/app/role-uat?scope=SHIP&extra=1').invalid).toBe(true)
  expect(readUatRoute('/app/role-uat?run=not-a-guid').invalid).toBe(true)
})

it('rejects a PASS without threshold, a non-FAIL gate with open blockers and a risk-accepted blocker', () => {
  expect(() => decodeUatSummary(summary({ gate: { ...summary().gate, gateState: 'PASS', state: 'CRITERIA_MET_PENDING_HUMAN_GO', openBlockers: 0 } }), 'SHIP')).toThrow()
  expect(() => decodeUatSummary(summary({ gate: { ...summary().gate, gateState: 'UNKNOWN', state: 'THRESHOLD_UNSET' } }), 'SHIP')).toThrow()
  expect(() => decodeUatSummary(summary({ openFindings: [finding({ state: 'ACCEPTED_RISK' })] }), 'SHIP')).toThrow()
  expect(() => decodeUatSummary(summary({ runs: [run({ state: 'ATTESTED', attestedBy: other })] }), 'SHIP')).toThrow()
  expect(decodeUatSummary(summary(), 'SHIP').gate.gateState).toBe('FAIL')
})

it('shows a failed gate, the unset threshold and the source pass proof, never a green accepted state', async () => {
  history.replaceState(null, '', '/app/role-uat?scope=SHIP')
  render(<UatWorkspace/>)
  expect(await screen.findByText('Fail')).toHaveAttribute('data-gate-state', 'FAIL')
  expect(screen.getByText(/No human-agreed threshold is recorded/)).toBeInTheDocument()
  expect(screen.getByText('FICTIONAL pass proof S-UAT-01')).toBeInTheDocument()
  expect(screen.getByText('Not a UAT acceptance or pilot GO')).toBeInTheDocument()
  expect(document.querySelector('[data-tone="success"]')).toBeNull()
  // A blocker can only be resolved, never risk-accepted, and the reporter cannot resolve their own finding.
  const options = Array.from(document.querySelectorAll('[data-severity="BLOCKER"] option')).map(o => o.getAttribute('value'))
  expect(options).toEqual(['', 'RESOLVED'])
  expect(screen.queryByText('T-UAT-01')).toBeNull()
})

it('lets another human attest a submitted run with the exact version and keeps the attestation field empty until then', async () => {
  history.replaceState(null, '', `/app/role-uat?scope=SHIP&run=${runId}`)
  render(<UatWorkspace/>)
  expect(await screen.findByText('Empty: awaiting another human')).toBeInTheDocument()
  expect(screen.getByText(/attached evidence only/)).toBeInTheDocument()
  await userEvent.type(screen.getByLabelText('Attestation statement'), 'FICTIONAL witness attests the outcomes')
  await userEvent.click(within(screen.getByRole('form', { name: 'Attest run' })).getByRole('button', { name: 'Record' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toBe(`/api/v1/uat/runs/${runId}/attest`)
  expect(JSON.parse(String(commands[0].init.body))).toEqual({ statement: 'FICTIONAL witness attests the outcomes', expectedVersion: 7 })
  expect(new Headers(commands[0].init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
})

it('reports a separation-of-duties refusal and a forbidden summary', async () => {
  commandStatus = 403
  history.replaceState(null, '', `/app/role-uat?scope=SHIP&run=${runId}`)
  render(<UatWorkspace/>)
  await userEvent.type(await screen.findByLabelText('Attestation statement'), 'FICTIONAL self attestation attempt')
  await userEvent.click(within(screen.getByRole('form', { name: 'Attest run' })).getByRole('button', { name: 'Record' }))
  expect(await screen.findByText('separation_of_duties_required')).toBeInTheDocument()
})

it('shows the forbidden state when the reader lacks UAT access', async () => {
  body = 403
  history.replaceState(null, '', '/app/role-uat?scope=TUG')
  render(<UatWorkspace/>)
  expect(await screen.findByText('You do not have access to these records.')).toBeInTheDocument()
})
