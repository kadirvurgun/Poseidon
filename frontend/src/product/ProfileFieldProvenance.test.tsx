import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ProfileFieldProvenance } from './ProfileFieldProvenance'
import * as profileApi from './profile-api'
import { decodeFields } from './profile-field-api'

// DEV-020 / T-S2-001 mocked API component coverage; not a real backend or browser journey.
const vessel = '10000000-0000-0000-0000-000000000001'
const company = '10000000-0000-0000-0000-000000000002'
const actor = '10000000-0000-0000-0000-000000000004'
const checkedAt = '2026-11-18T12:00:00+00:00'
const unknown = (reasonCode: string, observedAt: string | null = null) => ({ state: 'UNKNOWN', reasonCode, observedAt, checkedAt, ageSeconds: null })
const source = { sequence: 1, valueRevision: 2, sourceKind: 'DOCUMENT', reference: { state: 'Known', value: 'Synthetic certificate ref' },
  observedAt: { state: 'Known', value: '2026-01-01T00:00:00.0000000+00:00' }, declaredBy: actor, stewardCompanyId: company,
  recordedAt: '2026-11-18T10:00:00+00:00', provenance: 'DeclaredUnverified', verificationState: 'NOT_VERIFIED' }
const field = (fieldPath: string, value: { state: string; value: string | null }, freshness: object, extra: object = {}) => ({
  fieldPath, kind: 'text', unit: null, value, valueRevision: value.state === 'Missing' ? 1 : 2, latestSequence: 0, source: null, priorValueSource: null,
  freshness, verificationState: 'NOT_VERIFIED', ...extra })
function fields(overrides: object = {}) {
  return { tenantId: '10000000-0000-0000-0000-000000000003', vesselId: vessel,
    owner: { legalEntityId: company, binding: 'LEGACY_COMPANY_BINDING', registeredOwnerState: 'NOT_ESTABLISHED' },
    profileRevision: 2, profileSavedAt: '2026-11-18T09:00:00+00:00', checkedAt, policy: { status: 'UNCONFIGURED', policyRef: null },
    summary: { state: 'UNKNOWN', reasonCode: 'FIELDS_UNKNOWN', oldestObservedAt: null, checkedAt, fresh: 0, stale: 0, unknown: 3, unavailable: 0 },
    fields: [
      field('gt', { state: 'Known', value: '100.5' }, unknown('FRESHNESS_POLICY_UNCONFIGURED', '2026-01-01T00:00:00+00:00'), { kind: 'number', unit: 'TONNAGE_INDEX', source, latestSequence: 1 }),
      field('flag', { state: 'Known', value: 'Synthetic flag' }, unknown('SOURCE_NOT_DECLARED')),
      field('imo', { state: 'Missing', value: null }, unknown('VALUE_NOT_RECORDED')),
    ], canDeclare: true, ...overrides }
}
const reply = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response
const csrf = crypto.randomUUID()
beforeEach(() => {
  vi.spyOn(profileApi, 'loadProfileSession').mockResolvedValue({ owner: 'tenant/actor', tenant: 'tenant', csrf })
})
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('ProfileFieldProvenance (DEV-020)', () => {
  it('shows owner binding, unconfigured policy and per-field reasons without a positive ready state', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => reply(fields())))
    render(<ProfileFieldProvenance vessel={vessel} language="en" revision={2}/>)
    const panel = await screen.findByRole('region', { name: 'Field sources and freshness' })
    expect(await within(panel).findByText('Legacy company binding — not a registered owner or operator')).toBeVisible()
    expect(within(panel).getByText(/No approved freshness policy is configured/)).toBeVisible()
    const rows = within(panel).getAllByRole('row')
    const gt = rows.find(row => within(row).queryByRole('rowheader', { name: 'GT' }))!
    expect(within(gt).getByText('No approved freshness policy')).toBeVisible()
    expect(within(gt).getByText('Synthetic certificate ref', { exact: false })).toBeVisible()
    expect(within(gt).getByText('Not verified')).toBeVisible()
    expect(within(panel).getByText('No source declared for this value')).toBeVisible()
    expect(within(panel).getByText('Value not recorded')).toBeVisible()
    expect(within(panel).queryByText(/within policy age \(not verified\)/i)).not.toBeInTheDocument()
    expect(panel.querySelector('[data-state="FRESH"]')).toBeNull()
    // Missing values cannot get a declared source.
    expect(within(panel).getAllByRole('button', { name: /^Declare source/ })).toHaveLength(2)
  })

  it('renders Turkish copy and the empty state before a profile is saved', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => reply(fields({ profileRevision: 0, profileSavedAt: null }))))
    render(<ProfileFieldProvenance vessel={vessel} language="tr" revision={0}/>)
    expect(await screen.findByText('Henüz kayıtlı profil yok. Alan kaynağı beyan etmeden önce profili kaydedin.')).toBeVisible()
    expect(screen.getByRole('region', { name: 'Alan kaynakları ve tazelik' })).toBeVisible()
    expect(screen.queryByRole('button', { name: /Kaynak beyan et/ })).not.toBeInTheDocument()
  })

  it('declares a source with fences, CSRF and an idempotency key then reloads', async () => {
    let declared = false
    const fetchMock = vi.fn(async (_url: string, init?: RequestInit) => {
      if (init?.method === 'POST') { declared = true; return reply({ tenantId: '10000000-0000-0000-0000-000000000003', vesselId: vessel, fieldPath: 'flag', source: { ...source, sourceKind: 'REGISTRY_RECORD' } }) }
      return reply(declared ? fields({ fields: [field('flag', { state: 'Known', value: 'Synthetic flag' }, unknown('FRESHNESS_POLICY_UNCONFIGURED'), { source: { ...source, sourceKind: 'REGISTRY_RECORD' }, latestSequence: 1 })] }) : fields())
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<ProfileFieldProvenance vessel={vessel} language="en" revision={2}/>)
    await user.click(await screen.findByRole('button', { name: 'Declare source: Flag' }))
    const form = screen.getByRole('form', { name: 'Declare source: Flag' })
    await user.selectOptions(within(form).getByLabelText('Source kind'), 'REGISTRY_RECORD')
    await user.type(within(form).getByLabelText('Source reference'), 'Synthetic registry extract')
    await user.type(within(form).getByLabelText('Observed at (with timezone)'), '2026-01-01T00:00:00Z')
    await user.click(within(form).getByRole('button', { name: 'Record declared source' }))
    expect(await screen.findByText('Declared source recorded. It is unverified.')).toBeVisible()
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')!
    expect(post[0]).toBe(`/api/v1/vessels/${vessel}/regulatory-profile/field-sources`)
    const headers = post[1]!.headers as Record<string, string>
    expect(headers['X-CSRF-TOKEN']).toBe(csrf); expect(headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
    expect(JSON.parse(post[1]!.body as string)).toEqual({ fieldPath: 'flag', expectedValueRevision: 2, expectedSequence: 0, sourceKind: 'REGISTRY_RECORD',
      reference: { state: 'Known', value: 'Synthetic registry extract' }, observedAt: { state: 'Known', value: '2026-01-01T00:00:00Z' } })
    await waitFor(() => expect(fetchMock.mock.calls.filter(([, init]) => init?.method !== 'POST').length).toBe(2))
  })

  it('shows a conflict with a reload action and never retries a 409 automatically', async () => {
    const fetchMock = vi.fn(async (_url: string, init?: RequestInit) => init?.method === 'POST' ? reply({ code: 'version_conflict' }, 409) : reply(fields()))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<ProfileFieldProvenance vessel={vessel} language="en" revision={2}/>)
    await user.click(await screen.findByRole('button', { name: 'Declare source: Flag' }))
    const form = screen.getByRole('form', { name: 'Declare source: Flag' })
    await user.selectOptions(within(form).getByLabelText('Observed at (with timezone) — Data state'), 'Unknown')
    await user.click(within(form).getByRole('button', { name: 'Record declared source' }))
    expect(await within(form).findByText(/changed since you loaded them/)).toBeVisible()
    expect(within(form).getByRole('button', { name: 'Record declared source' })).toBeDisabled()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
    await user.click(within(form).getByRole('button', { name: 'Reload' }))
    await waitFor(() => expect(fetchMock.mock.calls.filter(([, init]) => init?.method !== 'POST').length).toBe(2))
  })

  it('retries an unknown outcome with the same key and body', async () => {
    let attempts = 0
    const fetchMock = vi.fn(async (_url: string, init?: RequestInit) => {
      if (init?.method !== 'POST') return reply(fields())
      attempts++
      if (attempts === 1) throw new TypeError('network')
      return reply({ tenantId: '10000000-0000-0000-0000-000000000003', vesselId: vessel, fieldPath: 'flag', source })
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<ProfileFieldProvenance vessel={vessel} language="en" revision={2}/>)
    await user.click(await screen.findByRole('button', { name: 'Declare source: Flag' }))
    const form = screen.getByRole('form', { name: 'Declare source: Flag' })
    await user.type(within(form).getByLabelText('Source reference'), 'Synthetic ref')
    await user.selectOptions(within(form).getByLabelText('Observed at (with timezone) — Data state'), 'Unknown')
    await user.click(within(form).getByRole('button', { name: 'Record declared source' }))
    const retry = await within(form).findByRole('button', { name: 'Retry the same declaration' })
    expect(within(form).getByLabelText('Source reference')).toBeDisabled()
    await user.click(retry)
    expect(await screen.findByText('Declared source recorded. It is unverified.')).toBeVisible()
    const posts = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')
    expect(posts).toHaveLength(2)
    expect((posts[0][1]!.headers as Record<string, string>)['Idempotency-Key']).toBe((posts[1][1]!.headers as Record<string, string>)['Idempotency-Key'])
    expect(posts[0][1]!.body).toBe(posts[1][1]!.body)
  })

  it('removes shown sources when access is withdrawn and reports denial', async () => {
    let denied = false
    vi.stubGlobal('fetch', vi.fn(async () => denied ? reply({ code: 'permission_denied' }, 403) : reply(fields())))
    const onDenied = vi.fn()
    const view = render(<ProfileFieldProvenance vessel={vessel} language="en" revision={2} onDenied={onDenied}/>)
    expect(await screen.findByText('Synthetic certificate ref', { exact: false })).toBeVisible()
    denied = true
    view.rerender(<ProfileFieldProvenance vessel={vessel} language="en" revision={3} onDenied={onDenied}/>)
    expect(await screen.findByRole('alert')).toHaveTextContent('You no longer have permission')
    expect(screen.queryByText('Synthetic certificate ref', { exact: false })).not.toBeInTheDocument()
    expect(onDenied).toHaveBeenCalledTimes(1)
  })

  it('shows a dirty-form note, loads field history, and a stale state uses the warning tone', async () => {
    const staleFields = fields({ policy: { status: 'RESOLVED', policyRef: 'synthetic' },
      summary: { state: 'STALE', reasonCode: 'FIELDS_STALE', oldestObservedAt: null, checkedAt, fresh: 0, stale: 1, unknown: 0, unavailable: 0 },
      fields: [field('gt', { state: 'Known', value: '100.5' }, { state: 'STALE', reasonCode: 'POLICY_MAX_AGE_EXCEEDED', observedAt: '2026-01-01T00:00:00+00:00', checkedAt, ageSeconds: 864000 }, { source, latestSequence: 1 })] })
    const fetchMock = vi.fn(async (url: string) => url.includes('/history') ? reply({ items: [
      { valueRevision: 2, recordedAt: '2026-11-18T09:00:00+00:00', profileProvenance: 'DeclaredUnverified', value: { state: 'Known', value: '100.5' }, sources: [source], moreSources: false },
      { valueRevision: 1, recordedAt: '2026-11-17T09:00:00+00:00', profileProvenance: 'Synthetic', value: { state: 'Missing', value: null }, sources: [], moreSources: false },
    ], offset: 0, limit: 25, hasMore: false }) : reply(staleFields))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<ProfileFieldProvenance vessel={vessel} language="en" revision={2} dirty/>)
    expect(await screen.findByText(/Unsaved form edits are not shown here/)).toBeVisible()
    expect(screen.getAllByText('Stale')[0].closest('[data-state]')).toHaveAttribute('data-state', 'STALE')
    await user.click(screen.getByRole('button', { name: 'History: GT' }))
    expect(await screen.findByText('revision 1')).toBeVisible()
    expect(fetchMock.mock.calls.some(([url]) => url === `/api/v1/vessels/${vessel}/regulatory-profile/fields/gt/history?offset=0`)).toBe(true)
  })

  it('rejects a contract that reports FRESH while a field is not fresh', () => {
    expect(() => decodeFields(fields({ summary: { state: 'FRESH', reasonCode: 'ALL_FIELDS_WITHIN_POLICY', oldestObservedAt: null, checkedAt, fresh: 0, stale: 0, unknown: 3, unavailable: 0 } }), vessel)).toThrow()
    expect(() => decodeFields(fields({ fields: [field('gt', { state: 'Known', value: '1' }, unknown('SOURCE_NOT_DECLARED'), { verificationState: 'VERIFIED' })] }), vessel)).toThrow()
    expect(() => decodeFields(fields(), '10000000-0000-0000-0000-0000000000ff')).toThrow()
  })
})
