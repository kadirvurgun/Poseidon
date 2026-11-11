import { afterEach, describe, expect, it, vi } from 'vitest'
import { addJob, save, type EquipmentCommand, type PlanCommand } from './technical-api'

// TECHNICAL-COMMAND-OWNER-RECOVERY-01: authored; execution deferred to bundle gate.
const vessel = '11111111-1111-1111-1111-111111111111'
const id = 'abcdefab-2222-2222-2222-222222222222'
const key = '33333333-3333-3333-3333-333333333333'
const command = (): PlanCommand => ({ id, baseVersion: 7, assetId: vessel, jobId: null, ownerRoleId: null,
  inputs: { intervalValue: '0.100000000000000002', condition: null }, referenceAt: '2026-11-01T12:00:00Z' })
const equipment = (): EquipmentCommand => ({ id, baseVersion: 7, parentId: null, code: 'FICTIONAL-ASSET', name: 'Synthetic equipment',
  level: 'Equipment', systemFamily: null, equipmentType: null, criticality: null, maker: null, model: null,
  serialNumber: null, location: null, commissionDate: null, responsibleRoleId: null, notes: null })
const receipt = () => ({ id, revision: 8, version: 123, replayed: false })
const respond = (body: unknown) => {
  const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetcher)
  return fetcher
}
afterEach(() => vi.unstubAllGlobals())

describe('technical command receipt boundary', () => {
  it.each([false, true])('confirms only the intended resource while preserving exact request bytes (plan=%s)', async plan => {
    const fetcher = respond(receipt()), body = plan ? command() : equipment(), signal = new AbortController().signal
    expect(await save(vessel, plan, body, 'fresh-same-owner-token', key, signal)).toEqual(receipt())
    expect(fetcher).toHaveBeenCalledWith(`/api/v1/${plan ? 'maintenance-plan' : 'equipment-hierarchy'}/${vessel}`, expect.objectContaining({
      method: 'POST', body: JSON.stringify(body), signal, credentials: 'same-origin',
      headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'fresh-same-owner-token', 'Idempotency-Key': key }),
    }))
    expect(body.baseVersion).toBe(7)
    if ('inputs' in body) expect(body.inputs.intervalValue).toBe('0.100000000000000002')
  })

  it('accepts an immutable replay and equivalent GUID casing without changing the frozen command', async () => {
    respond({ ...receipt(), replayed: true })
    const body = { ...command(), id: id.toUpperCase() }, before = JSON.stringify(body)
    expect((await save(vessel, true, body, 'refreshed-token', key, new AbortController().signal)).replayed).toBe(true)
    expect(JSON.stringify(body)).toBe(before)
  })

  it.each([
    null, [], {}, { ...receipt(), id: vessel }, { ...receipt(), id: id + '\n' },
    { ...receipt(), id: '00000000-0000-0000-0000-000000000000' },
    { ...receipt(), revision: 0 }, { ...receipt(), revision: -1 }, { ...receipt(), revision: 1.5 },
    { ...receipt(), version: 0 }, { ...receipt(), version: '123' }, { ...receipt(), version: Number.MAX_SAFE_INTEGER + 1 },
    { ...receipt(), replayed: 'false' }, { id, revision: 1, version: 1 },
  ])('does not consume a malformed/wrong-resource HTTP success as a confirmed receipt: %j', async value => {
    const fetcher = respond(value), body = command(), before = JSON.stringify(body)
    await expect(save(vessel, true, body, 'token', key, new AbortController().signal))
      .rejects.toMatchObject({ status: 502, code: 'invalid_technical_save_receipt' })
    expect(fetcher).toHaveBeenCalledTimes(1)
    expect(JSON.stringify(body)).toBe(before)
  })

  it('keeps the actual job UUID/body protocol without inventing a command receipt header', async () => {
    const fetcher = respond({ id }), body = { id, code: 'FICTIONAL-JOB', title: 'Synthetic declaration' }
    expect(await addJob(vessel, body, 'fresh-token', new AbortController().signal)).toEqual({ id })
    const init = fetcher.mock.calls[0][1] as RequestInit
    expect(init.body).toBe(JSON.stringify(body))
    expect(init.headers).toEqual({ Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'fresh-token' })
  })

  it.each([null, [], {}, { id: vessel }, { id: id + '\n' }])('retains an unknown job outcome for invalid success: %j', async value => {
    respond(value)
    await expect(addJob(vessel, { id, code: 'FICTIONAL-JOB', title: 'Synthetic declaration' }, 'token', new AbortController().signal))
      .rejects.toMatchObject({ status: 502, code: 'invalid_technical_job_receipt' })
  })
})
