import { expect, it } from 'vitest'
import { decodeEnterpriseNavigation } from './enterprise-navigation'

const source = { destinations: [{ key: 'today', href: '/app/today' }, { key: 'fleet', href: '/app/fleet' }], canSearch: false, canWriteRegulatory: false }
it('only exposes implemented destinations returned by the current server capability projection', () => {
  expect(decodeEnterpriseNavigation(source)).toEqual({ destinations: source.destinations, canSearch: false })
  expect(decodeEnterpriseNavigation({ ...source, destinations: [] }).destinations).toEqual([])
})
it('DEV-037 admits the exact server-provisioned health destination without inventing it in ordinary navigation', () => {
  expect(decodeEnterpriseNavigation(source).destinations.some(item => item.key === 'system-health')).toBe(false)
  const health = { key: 'system-health', href: '/app/system-health' }
  expect(decodeEnterpriseNavigation({ ...source, destinations: [...source.destinations, health] }).destinations).toContainEqual(health)
  expect(() => decodeEnterpriseNavigation({ ...source, destinations: [{ ...health, href: '/app/system-health?tenant=foreign' }] })).toThrow()
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
