import { expect, it } from 'vitest'
import { decodeEnterpriseNavigation } from './enterprise-navigation'

const source = { destinations: [{ key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' }], canSearch: false, canWriteRegulatory: false }
it('only exposes implemented destinations returned by the current server capability projection', () => {
  expect(decodeEnterpriseNavigation(source)).toEqual({ destinations: source.destinations, canSearch: false })
  expect(decodeEnterpriseNavigation({ ...source, destinations: [] }).destinations).toEqual([])
})
it.each([
  { ...source, destinations: [{ key: 'fleet', href: 'https://outside.invalid/app/fleet' }] },
  { ...source, destinations: [{ key: 'fleet', href: '/app/fleet?tenant=other' }] },
  { ...source, destinations: [{ key: 'medical', href: '/app/medical' }] },
  { ...source, destinations: [source.destinations[0], source.destinations[0]] },
  { ...source, destinations: [{ ...source.destinations[0], actorId: 'forged' }] },
  { ...source, canSearch: 'true' }, { ...source, canWriteRegulatory: null }
])('rejects forged, duplicate, foreign or untyped capability destinations %#', value => {
  expect(() => decodeEnterpriseNavigation(value)).toThrow()
})
