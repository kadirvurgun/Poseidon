import { afterEach, expect, it, vi } from 'vitest'
import { decodeTugRequest, emptyTugInputs, tugApi, tugRequestSource } from './tug-api'

const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222', other = '33333333-3333-3333-3333-333333333333'
const row = () => ({ id, vesselId: vessel, jobNumber: 'FICTIONAL-ONLY', revision: 3, version: 12, inputs: { ...emptyTugInputs(), requiredBollardPull: '45.01' }, provenance: 'UserDeclaredUnverified', savedAt: '2026-11-05T10:00:00Z', missing: ['requested_start'], replayed: false, sourceVersion: tugRequestSource })
afterEach(() => vi.unstubAllGlobals())

it('preserves exact decimal declaration and missing conditional fields in the owning response', () => {
  const result = decodeTugRequest(row(), vessel, id)
  expect(result.inputs.requiredBollardPull).toBe('45.01')
  expect(result.inputs.requestedEnd).toBeNull()
})

it.each([{ vesselId: other }, { id: other }, { version: 0 }, { version: 4294967296 }, { revision: 1.5 }, { provenance: 'Verified' }, { sourceVersion: 'unrecognized' }, { savedAt: 'not-an-instant' }, { inputs: {} }, { inputs: { ...emptyTugInputs(), notes: ['malformed'] } }, { inputs: { ...emptyTugInputs(), requiredBollardPull: 45.01 } }, { inputs: { ...emptyTugInputs(), requestOwner: 'unscoped-label' } }])('rejects unpinned or malformed owning request data: %j', patch => {
  expect(() => decodeTugRequest({ ...row(), ...patch }, vessel, id)).toThrow()
})

it('pins both detail reads and save receipts to the requested resource before returning product data', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ ...row(), id: other }), { headers: { 'Content-Type': 'application/json' } })))
  const signal = new AbortController().signal
  await expect(tugApi.detail(id, vessel, signal)).rejects.toMatchObject({ status: 502, code: 'invalid_tug_request_contract' })
  await expect(tugApi.save(vessel, { key: crypto.randomUUID(), update: false, body: { clientId: id, baseVersion: null, inputs: emptyTugInputs() } }, 'synthetic-csrf', signal)).rejects.toMatchObject({ status: 502, code: 'invalid_tug_request_contract' })
})

it('does not silently relabel a non-UTC requested window as UTC in the editor', () => {
  expect(() => decodeTugRequest({ ...row(), inputs: { ...emptyTugInputs(), requestedStart: '2026-11-05T12:00:00+03:00' } }, vessel, id)).toThrow()
  expect(decodeTugRequest({ ...row(), inputs: { ...emptyTugInputs(), requestedStart: '2026-11-05T09:00:00+00:00' } }, vessel, id).inputs.requestedStart).toBe('2026-11-05T09:00:00+00:00')
})

it('preserves ordinary narrative line breaks while refusing control characters and multiline identifiers', () => {
  const notes = 'FICTIONAL first line\nSecond line'
  expect(decodeTugRequest({ ...row(), inputs: { ...emptyTugInputs(), notes } }, vessel, id).inputs.notes).toBe(notes)
  expect(() => decodeTugRequest({ ...row(), inputs: { ...emptyTugInputs(), notes: 'FICTIONAL\0Hidden' } }, vessel, id)).toThrow()
  expect(() => decodeTugRequest({ ...row(), inputs: { ...emptyTugInputs(), assistedVessel: notes } }, vessel, id)).toThrow()
})
