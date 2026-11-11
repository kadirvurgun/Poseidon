import { afterEach, expect, it, vi } from 'vitest'
import { decodeObservationSnapshot, observationOwner, observationPath, sendObservation, type ObservationAttempt } from './observation-api'
import type { Session } from './api'

// MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01: authored, NOT RUN until bundle gate.
const tenant = '11111111-1111-1111-1111-111111111111', actor = '22222222-2222-2222-2222-222222222222'
const vessel = '33333333-3333-3333-3333-333333333333', asset = '44444444-4444-4444-4444-444444444444', id = '55555555-5555-5555-5555-555555555555'
const scope = { owner: `${tenant}/${actor}`, vessel, asset }
const source = () => ({ tenantId: tenant, vesselId: vessel, assetId: asset, source: 'synthetic-meter', version: 'synthetic-v1', kind: 'Meter', unit: 'hours', freshnessSeconds: 3600, maxScale: 4 })
const row = () => ({ id, source: 'synthetic-meter', policyVersion: 'synthetic-v1', sourceRevision: 17, kind: 'Meter', unit: 'hours', value: '19.5000', observedAt: '2026-11-01T10:00:00+03:00', provenance: 'OperatorDeclaredUnverified', revoked: false })
const snapshot = () => ({ sources: [source()], items: [row()], canWrite: true, state: 'ConfiguredDeclarationsOnly' })
afterEach(() => vi.unstubAllGlobals())

it('retains exact declarations, revoked history and missing policy without inferring verified observations', () => {
  const current = decodeObservationSnapshot({ ...snapshot(), items: [{ ...row(), value: null, observedAt: null, revoked: true }] }, scope)
  expect(current.items[0]).toMatchObject({ value: null, observedAt: null, revoked: true, provenance: 'OperatorDeclaredUnverified' })
  expect(decodeObservationSnapshot(snapshot(), scope).items[0].value).toBe('19.5000')
  expect(decodeObservationSnapshot({ ...snapshot(), sources: [], state: 'PolicyUnavailable' }, scope).state).toBe('PolicyUnavailable')
})
it.each([
  { ...snapshot(), sources: [{ ...source(), tenantId: actor }] },
  { ...snapshot(), sources: [{ ...source(), vesselId: asset }] },
  { ...snapshot(), sources: [{ ...source(), assetId: vessel }] },
  { ...snapshot(), sources: [source(), source()] },
  { ...snapshot(), sources: [{ ...source(), maxScale: null }] },
  { ...snapshot(), sources: [{ ...source(), freshnessSeconds: 0 }] },
  { ...snapshot(), state: 'Approved' },
  { ...snapshot(), state: 'PolicyUnavailable' },
  { ...snapshot(), items: [row(), { ...row(), id: id.toUpperCase() }] },
  { ...snapshot(), items: [{ ...row(), revoked: 'false' }] },
  { ...snapshot(), items: [{ ...row(), observedAt: '2026-11-01T10:00:00' }] },
  { ...snapshot(), items: [{ ...row(), sourceRevision: Number.MAX_SAFE_INTEGER + 1 }] },
])('withdraws malformed or out-of-scope projections instead of trusting a typed cast: %j', value => {
  expect(() => decodeObservationSnapshot(value, scope)).toThrow('Request failed (502)')
})
it.each(['00000000-0000-0000-0000-000000000000', actor + '\n', 'invalid'])('does not bind identity to malformed actor %j', actorId => {
  expect(observationOwner({ authenticated: true, tenantId: tenant, actorId } as Session)).toBeNull()
})
it.each(['capture', 'revoke'] as const)('checks the actual %s response target and retains exact frozen transport bytes', async kind => {
  const body = kind === 'capture' ? JSON.stringify({ id, value: '19.5000', sourceRevision: 17 }) : '{}'
  const attempt: ObservationAttempt = { ...scope, id, key: actor, kind, path: kind === 'capture' ? observationPath(scope) : `${observationPath(scope)}/${id}/revoke`, body }
  const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify(kind === 'capture' ? { id, replayed: true } : { id, state: 'Revoked' }), { status: 200 }))
  vi.stubGlobal('fetch', fetcher)
  expect(await sendObservation(attempt, 'synthetic-fresh-token', new AbortController().signal)).toEqual({ id })
  const init = fetcher.mock.calls[0][1] as RequestInit
  expect(init.body).toBe(body)
  expect(new Headers(init.headers).get('Idempotency-Key')).toBe(actor)
  expect(new Headers(init.headers).get('X-CSRF-TOKEN')).toBe('synthetic-fresh-token')
})
it.each([
  { kind: 'capture', response: { id: asset, replayed: true } },
  { kind: 'capture', response: { id } },
  { kind: 'capture', response: { id, replayed: 'true' } },
  { kind: 'revoke', response: { id, state: 'Verified' } },
  { kind: 'revoke', response: { id: asset, state: 'Revoked' } },
  { kind: 'revoke', response: null },
] as const)('does not confirm a malformed HTTP200 outcome: %j', async ({ kind, response }) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(response), { status: 200 })))
  await expect(sendObservation({ ...scope, id, key: actor, kind, path: observationPath(scope), body: '{}' }, 'synthetic-token', new AbortController().signal))
    .rejects.toMatchObject({ status: 502, code: 'invalid_observation_outcome' })
})
