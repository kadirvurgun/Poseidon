import { afterEach, expect, it, vi } from 'vitest'
import { loadSession, send } from './qualification-api'
import { qualifiedFixture } from './qualification.test-fixtures'

afterEach(() => vi.unstubAllGlobals())
const other = '10000000-0000-4000-8000-000000000009'
function response(value: unknown) {
  const fetcher = vi.fn<typeof fetch>(async () => new Response(JSON.stringify(value), { status: 200, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetcher)
  return fetcher
}

it('reads actual authenticated actor/tenant and a fresh CSRF token instead of manufacturing an owner from a source role', async () => {
  const row = qualifiedFixture(), csrf = crypto.randomUUID()
  const fetcher = response({ authenticated: true, canRead: true, setupRequired: false, actorId: row.actorId, tenantId: other, csrfToken: csrf })
  expect(await loadSession(new AbortController().signal)).toEqual({ owner: `${other}/${row.actorId}`, csrf })
  expect(fetcher.mock.calls[0][0]).toBe('/api/v1/auth/session')
})

it.each([
  { authenticated: false },
  { authenticated: true, canRead: false },
  { authenticated: true, canRead: true, setupRequired: true, actorId: other, tenantId: other, csrfToken: crypto.randomUUID() },
  { authenticated: true, canRead: true, setupRequired: false, actorId: null, tenantId: other, csrfToken: crypto.randomUUID() },
  { authenticated: true, canRead: true, setupRequired: false, actorId: other, tenantId: null, csrfToken: crypto.randomUUID() },
  { authenticated: true, canRead: true, setupRequired: false, actorId: other, tenantId: other, csrfToken: '' },
])('rejects an unavailable or malformed session without a write', async value => {
  const fetcher = response(value)
  await expect(loadSession(new AbortController().signal)).rejects.toThrow()
  expect(fetcher).toHaveBeenCalledTimes(1)
  expect(fetcher.mock.calls[0][0]).toBe('/api/v1/auth/session')
})

it('binds a qualification receipt to the actual command rule revision, authority and lifecycle state', async () => {
  const row = qualifiedFixture(), attempt = { action: 'events' as const, key: crypto.randomUUID(),
    body: JSON.stringify({ expectedVersion: 28, ruleRevision: row.ruleRevision, authorityId: row.authorityId, state: row.state, content: null }) }
  const fetcher = response(row), csrf = crypto.randomUUID()
  expect(await send(row.ruleId, attempt, csrf, new AbortController().signal)).toEqual(row)
  const options = fetcher.mock.calls[0][1] as RequestInit
  expect(options.body === attempt.body).toBe(true)
  expect(options.headers).toMatchObject({ 'Idempotency-Key': attempt.key, 'X-CSRF-TOKEN': csrf })
  for (const changed of [{ ...row, ruleId: other }, { ...row, ruleRevision: row.ruleRevision + 1 },
    { ...row, authorityId: other }, { ...row, state: 'ADOPTED' }]) {
    response(changed)
    await expect(send(row.ruleId, attempt, csrf, new AbortController().signal)).rejects.toThrow()
  }
})

it('binds a server-created attestation receipt to its exact captured context while allowing normalized instant spelling', async () => {
  const row = qualifiedFixture(), fields = ['vessel.flag']
  const body = { authorityId: row.authorityId, vesselId: other, profileRevision: 6, profileVersion: 7,
    qualificationRevision: row.revision, qualificationVersion: row.version, fields, validUntil: '2026-11-13T12:00:00.0000000+03:00' }
  const receipt = { id: crypto.randomUUID(), ruleId: row.ruleId, qualificationRevision: row.revision, vesselId: other,
    profileRevision: 6, fields, hash: 'A'.repeat(64), savedAt: '2026-11-13T08:00:00Z', validUntil: '2026-11-13T09:00:00Z' }
  const attempt = { action: 'fact-attestations' as const, key: crypto.randomUUID(), body: JSON.stringify(body) }
  response(receipt)
  expect(await send(row.ruleId, attempt, crypto.randomUUID(), new AbortController().signal)).toEqual(receipt)
  for (const changed of [{ ...receipt, vesselId: row.ruleId }, { ...receipt, profileRevision: 5 },
    { ...receipt, qualificationRevision: 2 }, { ...receipt, fields: ['vessel.type'] },
    { ...receipt, validUntil: '2026-11-13T09:00:00.0000001Z' }]) {
    response(changed)
    await expect(send(row.ruleId, attempt, crypto.randomUUID(), new AbortController().signal)).rejects.toThrow()
  }
})
