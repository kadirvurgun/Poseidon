import { afterEach, describe, expect, it, vi } from 'vitest'
import { loadWorkspaceContext } from './workspace-context-api'

const tenant = '10000000-0000-0000-0000-000000000001'
const actor = '10000000-0000-0000-0000-000000000002'
const vessel = '10000000-0000-0000-0000-000000000003'
const other = '10000000-0000-0000-0000-000000000004'
const response = () => ({ tenantId: tenant, actorId: actor, roleContext: { state: 'Configured', catalogVersion: 'synthetic-v1',
  roles: [{ reference: other, tr: 'KURGUSAL rol', en: 'FICTIONAL role' }] }, vessel: { id: vessel, code: 'FICTIONAL', name: 'Synthetic vessel', kind: 'Ship' } })
const stub = (value: unknown) => vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => value }))
const load = () => loadWorkspaceContext(tenant, actor, vessel, new AbortController().signal)
afterEach(() => vi.unstubAllGlobals())
describe('workspace context wire boundary', () => {
  it('validates exact current context and projects only the supported safe fields', async () => {
    stub({ ...response(), unsupportedPrivateValue: 'FICTIONAL SHOULD NOT PROJECT' })
    const value = await load()
    expect(value.vessel?.code).toBe('FICTIONAL'); expect(JSON.stringify(value)).not.toContain('SHOULD NOT PROJECT')
  })
  it.each(['tenantId', 'actorId'] as const)('rejects a response for another %s before exposing labels', async field => {
    stub({ ...response(), [field]: other }); await expect(load()).rejects.toMatchObject({ code: 'invalid_contract' })
  })
  it('rejects a different vessel even if it belongs to the current tenant', async () => {
    const value = response(); value.vessel.id = other; stub(value)
    await expect(load()).rejects.toMatchObject({ code: 'invalid_contract' })
  })
  it.each(['state-array', 'kind-array', 'duplicate-role', 'blank-label', 'partial-role', 'unexpected-vessel'])('rejects malformed context %s', async scenario => {
    const value: Record<string, unknown> = response()
    if (scenario === 'state-array') value.roleContext = { ...response().roleContext, state: ['Configured'] }
    if (scenario === 'kind-array') value.vessel = { ...response().vessel, kind: ['Ship'] }
    if (scenario === 'duplicate-role') value.roleContext = { ...response().roleContext, roles: [response().roleContext.roles[0], response().roleContext.roles[0]] }
    if (scenario === 'blank-label') value.roleContext = { ...response().roleContext, roles: [{ reference: other, tr: '', en: 'FICTIONAL' }] }
    if (scenario === 'partial-role') value.roleContext = { state: 'Unavailable', catalogVersion: null, roles: response().roleContext.roles }
    if (scenario === 'unexpected-vessel') value.vessel = null
    stub(value); await expect(load()).rejects.toMatchObject({ code: 'invalid_contract' })
  })
  it('requires a null vessel for the unselected workspace endpoint', async () => {
    stub(response()); await expect(loadWorkspaceContext(tenant, actor, null, new AbortController().signal)).rejects.toMatchObject({ code: 'invalid_contract' })
  })
})
