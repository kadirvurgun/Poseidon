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

it('admits only exact server granted daily workspace destinations', () => {
  const value = { ...source, destinations: [{ key: 'noon-report', href: '/app/noon-report' }, { key: 'engine-daily-log', href: '/app/engine-daily-log' }] }
  expect(decodeEnterpriseNavigation(value).destinations).toEqual(value.destinations)
  expect(decodeEnterpriseNavigation(source).destinations.some(item => item.key === 'noon-report')).toBe(false)
  expect(() => decodeEnterpriseNavigation({ ...value, destinations: [{ key: 'noon-report', href: '/app/noon-report?role=admin' }] })).toThrow()
})

// DEV022-AC-07: the menu projection is closed; a hidden count or denied list is rejected, not ignored.
it.each([{ hiddenCount: 2 }, { totalCount: 9 }, { deniedDestinations: [{ key: 'audit', href: '/app/audit' }] }])('rejects an extra aggregate member %#', extra => {
  expect(() => decodeEnterpriseNavigation({ ...source, ...extra })).toThrow()
})

// DEV-022 / DEV-035 (T-S4-003): the conflict center appears only when the server projects it.
it('admits the exact server-projected offline conflict center destination and never invents it', () => {
  const center = { key: 'offline-conflict-center', href: '/app/offline-conflict-center' }
  expect(decodeEnterpriseNavigation(source).destinations).not.toContainEqual(center)
  expect(decodeEnterpriseNavigation({ ...source, destinations: [...source.destinations, center] }).destinations).toContainEqual(center)
  expect(() => decodeEnterpriseNavigation({ ...source, destinations: [{ ...center, href: '/app/offline-conflict-center?vessel=forged' }] })).toThrow()
})
