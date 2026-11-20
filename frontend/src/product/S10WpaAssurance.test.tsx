import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { changeAppLanguage } from '../i18n'
import { DataTrustCenterWorkspace, dataTrustRoute } from './DataTrustCenterWorkspace'
import { IndustryAssuranceWorkspace, assuranceRoute, parseBindings } from './IndustryAssuranceWorkspace'
import { AuditPackPanel } from './AuditPackPanel'
import { decodePack, decodeTrust, readQueue } from './assurance-api'

// S10 WP-A DEV-064 / DEV-065 / DEV-113 UI. Synthetic identifiers and FICTIONAL content only; no fixture is a verified
// record, a closed finding or an official audit record.
const vessel = '11111111-1111-4111-8111-111111111111', inspectionId = '22222222-2222-4222-8222-222222222222'
const actorId = '44444444-4444-4444-8444-444444444444', tenantId = '55555555-5555-4555-8555-555555555555'
const evidenceId = '66666666-6666-4666-8666-666666666666', controlId = '77777777-7777-4777-8777-777777777777', packId = '88888888-8888-4888-8888-888888888888'
const H = 'A'.repeat(64), T = '2026-11-20T08:00:00+00:00'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const trustItem = (patch: Record<string, unknown> = {}) => ({ subjectKind: 'Evidence', subjectId: evidenceId, fieldPath: null, revision: 2, title: 'FICTIONAL hose test record',
  provenance: 'SYNTHETIC', sourceKind: null, observedAt: T, freshUntil: null, freshness: 'UNAVAILABLE', verification: 'UNVERIFIED', disposition: 'REVIEW_REQUIRED',
  signals: [{ kind: 'STALE', reason: 'FRESHNESS_POLICY_UNAVAILABLE', material: true }, { kind: 'UNVERIFIED', reason: 'VERIFICATION_NOT_PERFORMED', material: true }], savedAt: T, ...patch })
const trust = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, asOf: T, state: 'AVAILABLE', disposition: 'REVIEW_REQUIRED', readinessEffect: 'READINESS_FLOOR_REVIEW_REQUIRED',
  counts: { missing: 0, stale: 1, conflicting: 0, unverified: 1, orphan: 0 }, items: [trustItem()], profileFields: [], offset: 0, limit: 50, total: 1, hasMore: false,
  withheld: 1, truncated: false, sourceVersion: 'synthetic', ...patch })
const packItem = (patch: Record<string, unknown> = {}) => ({ ordinal: 0, kind: 'Evidence', resourceId: evidenceId, revision: 1, state: 'Superseded', hash: H, evidenceId, controlId,
  savedAt: T, integrity: 'Match', fileHref: null, historyHref: `/api/v1/audit?resourceId=${evidenceId}`, ...patch })
const pack = (patch: Record<string, unknown> = {}) => ({ id: packId, vesselId: vessel, label: 'SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD', notice: 'n', sourceVersion: 's', asOf: T, createdAt: T,
  createdByMe: true, manifestHash: H, itemCount: 3, included: 2, excluded: 1, unavailableChains: 0, truncated: false, previousPackId: null, superseded: 1,
  items: [packItem(), packItem({ ordinal: 1, state: 'Current', revision: 2, integrity: 'Mismatch' })], ...patch })
const assurancePage = (patch: Record<string, unknown> = {}) => ({ vesselId: vessel, classification: 'TANKER', applicability: 'APPLICABLE', sourceVersion: 's',
  inspections: [{ id: inspectionId, referenceNo: 'FICTIONAL-SIRE-001', inspectionDate: '2026-11-01', state: 'DRAFT', applicability: 'APPLICABLE', items: 1, openFindings: 1, pendingImport: true }],
  mappings: [{ id: '99999999-9999-4999-8999-999999999999', key: 'sire2-synthetic', version: 1, sourceVersion: 's', bindings: [{ header: 'Q', field: 'question_ref' }], hash: H, createdAt: T }],
  capabilities: { canImport: true, canAccept: true, canCaptureEvidence: true, canLinkAction: false, canWriteMapping: false, mediaKinds: ['PHOTO', 'VIDEO'], availableMediaKinds: ['PHOTO'],
    fields: ['question_ref'], requiredFields: ['question_ref'] }, notice: 'n', ...patch })
const inspection = (patch: Record<string, unknown> = {}) => ({ id: inspectionId, vesselId: vessel, programme: 'SIRE_2_0', referenceNo: 'FICTIONAL-SIRE-001', inspectionDate: '2026-11-01',
  state: 'DRAFT', classification: 'TANKER', applicability: 'APPLICABLE', revision: 2, provenance: 'ImportedUnverified', sourceVersion: 's',
  items: [{ questionRef: '2.1.1', revision: 1, content: { questionRef: '2.1.1', chapter: null, observationType: 'NEGATIVE', observationCategory: 'UNKNOWN', observationText: 'FICTIONAL observation', inspectorComment: null }, hash: H, evidence: 0, savedAt: T }],
  findings: [{ id: '12121212-1212-4121-8121-121212121212', questionRef: '2.1.1', firstItemRevision: 1, latestItemRevision: 1, observationPresent: true, state: 'OPEN', verification: 'UNVERIFIED',
    actionLinkState: 'UNLINKED', actionId: null, revision: 1, version: 3 }], evidence: [],
  imports: [{ id: '13131313-1313-4131-8131-131313131313', state: 'PENDING_REVIEW', mappingVersion: 1, sourceVersion: 's', diffHash: H, importedByMe: false, importedAt: T, decidedAt: null, version: 4,
    new: 0, changed: 1, unchanged: 0, absent: 0, diff: [{ questionRef: '2.1.1', change: 'CHANGED', fields: ['observation_text'], currentRevision: 1 }] }], notice: 'n', ...patch })

const fetcher = vi.fn()
let commands: { url: string; init: RequestInit }[] = []
beforeEach(async () => {
  await changeAppLanguage('en', false)
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  commands = []; localStorage.clear()
  fetcher.mockReset()
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (init?.method === 'POST') { commands.push({ url, init }); return url.includes('audit-ready/packs') ? json(pack()) : json(inspection()) }
    if (url.includes('data-trust?')) return json(trust())
    if (url.includes('audit-ready/packs?')) return json({ vesselId: vessel, label: 'SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD', notice: 'n', canCompile: true, items: [], hasMore: false })
    if (url.includes(`industry-assurance/inspections/${inspectionId}?`)) return json(inspection())
    if (url.includes('industry-assurance?')) return json(assurancePage())
    return json({}, 404)
  })
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('refuses malformed data-trust and assurance routes', () => {
  expect(dataTrustRoute(`/app/data-trust-center?vessel=${vessel}&offset=50`)).toMatchObject({ vessel, offset: 50, invalid: false })
  expect(dataTrustRoute(`/app/data-trust-center?vessel=${vessel}&offset=-1`).invalid).toBe(true)
  expect(dataTrustRoute('/app/data-trust-center?vessel=x').invalid).toBe(true)
  expect(assuranceRoute(`/app/industry-assurance?vessel=${vessel}&inspection=${inspectionId}`).invalid).toBe(false)
  expect(assuranceRoute(`/app/industry-assurance?inspection=${inspectionId}`).invalid).toBe(true)
  expect(parseBindings('Question=question_ref\nType = observation_type\n')).toEqual([{ header: 'Question', field: 'question_ref' }, { header: 'Type', field: 'observation_type' }])
})

it('rejects a false green: a material signal with a non-review disposition is a contract failure', () => {
  expect(() => decodeTrust(trust({ disposition: 'NO_MATERIAL_SIGNAL' }), vessel)).toThrow()
  expect(() => decodeTrust(trust({ items: [trustItem({ disposition: 'NO_MATERIAL_SIGNAL' })] }), vessel)).toThrow()
  expect(decodeTrust(trust(), vessel).disposition).toBe('REVIEW_REQUIRED')
})

it('shows data trust signals with provenance and freshness and never a ready state', async () => {
  history.replaceState(null, '', `/app/data-trust-center?vessel=${vessel}`)
  render(<DataTrustCenterWorkspace/>)
  expect(await screen.findByText('FICTIONAL hose test record', { exact: false })).toBeTruthy()
  expect(screen.getAllByText('Review required').length).toBeGreaterThan(0)
  expect(screen.getByText('No freshness policy')).toBeTruthy()
  expect(screen.getByText(/1 subjects you cannot read/)).toBeTruthy()
  expect(screen.queryByText(/^Ready$/i)).toBeNull()
})

it('labels an audit pack as a system compilation, shows superseded revisions and only counts excluded items', async () => {
  expect(() => decodePack(pack({ label: 'OFFICIAL' }), vessel)).toThrow()
  expect(() => decodePack(pack({ excluded: 0 }), vessel)).toThrow() // included + excluded must equal the manifest count
  const user = userEvent.setup()
  render(<AuditPackPanel vessel={vessel} csrf="synthetic-only" online language="en"/>)
  expect(await screen.findByText('System compilation; not an official record')).toBeTruthy()
  await user.click(await screen.findByRole('button', { name: 'Compile new pack' }))
  await waitFor(() => expect(commands).toHaveLength(1))
  expect(commands[0].url).toContain(`/api/v1/audit-ready/packs?vesselId=${vessel}`)
  expect(new Headers(commands[0].init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
  expect(await screen.findByText('Superseded (kept for trace)')).toBeTruthy()
  expect(screen.getByText('Differs from source')).toBeTruthy()
  expect(screen.getByText(/1 items outside your current appointments are excluded/)).toBeTruthy()
  expect(screen.queryByText(evidenceId.replace('6', '0'))).toBeNull()
})

it('requires a decision for every changed import item, keeps findings open and queues offline photo evidence', async () => {
  history.replaceState(null, '', `/app/industry-assurance?vessel=${vessel}&inspection=${inspectionId}`)
  const user = userEvent.setup()
  render(<IndustryAssuranceWorkspace/>)
  const accept = await screen.findByRole('button', { name: 'Accept import' })
  await user.type(screen.getByLabelText('Reason (at least 10 characters)'), 'FICTIONAL reviewed against the report')
  expect((accept as HTMLButtonElement).disabled).toBe(true) // the changed item is not decided yet
  await user.click(screen.getByLabelText('Keep current'))
  expect((accept as HTMLButtonElement).disabled).toBe(false)
  await user.click(accept)
  await waitFor(() => expect(commands).toHaveLength(1))
  const body = JSON.parse(String(commands[0].init.body))
  expect(body).toMatchObject({ decision: 'ACCEPT', acceptChanged: [], rejectChanged: ['2.1.1'], diffHash: H, expectedVersion: 4 })
  expect(screen.getAllByText('Open').length).toBeGreaterThan(0)
  expect(screen.getAllByText('Unverified').length).toBeGreaterThan(0)
  // Offline: the capture is stored with its exact key and body for an idempotent replay.
  Object.defineProperty(navigator, 'onLine', { value: false, configurable: true }); window.dispatchEvent(new Event('offline'))
  await user.type(await screen.findByLabelText('Clean file id'), '14141414-1414-4141-8141-141414141414')
  await user.type(screen.getByLabelText('File checksum (SHA-256)'), H)
  await user.click(screen.getByRole('button', { name: 'Save on this device' }))
  const queued = readQueue(vessel)
  expect(queued).toHaveLength(1)
  expect(queued[0].body).toMatchObject({ questionRef: '2.1.1', mediaKind: 'PHOTO', origin: 'OFFLINE' })
  expect(commands).toHaveLength(1)
  expect(await screen.findByText(/1 offline captures are waiting/)).toBeTruthy()
})

it('refuses SIRE import for a vessel that is not declared as a tanker', async () => {
  fetcher.mockImplementation(async (url: string) => {
    if (url.endsWith('auth/session')) return json({ authenticated: true, canRead: true, actorId, tenantId, csrfToken: 'synthetic-only' })
    if (url.includes('/workspace')) return json({ destinations: [], canSearch: false, canWriteRegulatory: false })
    if (url.includes('industry-assurance?')) return json(assurancePage({ classification: 'NOT_TANKER', applicability: 'NOT_APPLICABLE', inspections: [] }))
    return json({}, 404)
  })
  history.replaceState(null, '', `/app/industry-assurance?vessel=${vessel}`)
  render(<IndustryAssuranceWorkspace/>)
  expect(await screen.findByText(/applies to tankers only/)).toBeTruthy()
  expect(screen.queryByRole('button', { name: 'Stage for review' })).toBeNull()
})
