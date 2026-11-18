import { describe, expect, it } from 'vitest'
import { locationPath, readLocation } from './workspace-location'
const vessel = '10000000-0000-4000-8000-000000000001'
describe('S5-ROUTE-01 scoped compliance navigation (authored; execution deferred)', () => {
  it.each(['onboarding', 'passport'] as const)('round-trips the fleet %s route', screen => {
    const path = `/app/fleet/${vessel}/${screen}`
    const route = readLocation(path)
    expect(route).toMatchObject({ tab: 'fleet', selected: vessel, profile: false, [screen]: true })
    expect(locationPath(route)).toBe(path)
  })
  it('preserves legacy profile and rejects unsupported or encoded context routes', () => {
    expect(locationPath(readLocation(`/app/fleet/${vessel}/profile`))).toBe(`/app/fleet/${vessel}/profile`)
    for (const path of [`/app/companies/${vessel}/passport`, `/app/fleet/${vessel}/onboarding/approve`, `/app/fleet/${vessel}%2Fpassport`, `/app/fleet/${vessel}/ready`, '/app/passport', `/app/fleet/${vessel}/passport?vesselId=${vessel}&vesselId=${vessel}`, `/app/fleet/${vessel}/onboarding?offset=0`]) expect(readLocation(path).invalid).toBe(true)
  })
})
