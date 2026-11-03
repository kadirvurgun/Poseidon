import { expect, it } from 'vitest'
import { decodeTechnicalPlans } from './technical-api'

const plan = () => ({ id: '11111111-1111-1111-1111-111111111111', assetId: '22222222-2222-2222-2222-222222222222', jobId: null, ownerRoleId: null,
  version: 1, revision: 2, assetRevision: 1, status: 'Draft', provenance: 'SyntheticUnverified', inputs: { currentMeter: '0.100000000000000002' },
  due: { state: 'ReviewRequired', reason: 'IncompletePlanContext', nextDueDate: null, nextDueMeter: '0.100000000000000002', algorithm: 'planning-current-v1' },
  referenceAt: '2026-11-02T12:00:00Z', savedAt: '2026-11-01T12:00:00Z' })
const page = (items: unknown[]) => ({ items, canWrite: false, canReadHistory: false })

it('preserves unresolved context, exact computed boundaries and genuinely incomplete draft inputs', () => {
  const value = decodeTechnicalPlans(page([plan()]))
  expect(value.items[0].inputs.currentMeter).toBe('0.100000000000000002')
  expect(value.items[0].due.nextDueMeter).toBe('0.100000000000000002')
  expect(value.items[0].ownerRoleId).toBeNull()
  expect(value.items[0].due.state).toBe('ReviewRequired')
})

it.each([{}, { items: null, canWrite: true, canReadHistory: false }, { ...page([]), canWrite: 'true' }, page([plan(), plan()])])('rejects invalid current plan inventory and duplicate identities', value => {
  expect(() => decodeTechnicalPlans(value)).toThrow('Request failed (502)')
})

it.each([
  { ...plan(), due: null },
  { ...plan(), due: { ...plan().due, state: 'Ready' } },
  { ...plan(), due: { ...plan().due, nextDueMeter: 0.1 } },
  { ...plan(), due: { ...plan().due, nextDueDate: '2026-02-30' } },
  { ...plan(), inputs: { currentMeter: 0 } },
  { ...plan(), inputs: [] },
  { ...plan(), referenceAt: '2026-11-02T12:00:00' },
  { ...plan(), ownerRoleId: 'forged-reference' },
  { ...plan(), status: 'Approved' }
])('rejects malformed source/calculation data instead of rendering an invented healthy state', row => {
  expect(() => decodeTechnicalPlans(page([row]))).toThrow('Request failed (502)')
})
