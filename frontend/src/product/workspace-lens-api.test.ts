import { describe, expect, it } from 'vitest'
import { decodeLenses } from './workspace-lens-api'
const tenant = '10000000-0000-0000-0000-000000000001', actor = '10000000-0000-0000-0000-000000000002'
const vessel = '30000000-0000-0000-0000-000000000001'
const value = () => ({ tenantId: tenant, actorId: actor, vesselId: vessel, mode: 'Ship', state: 'Configured', version: 'synthetic-v1', canSearch: false,
  lenses: [{ reference: '20000000-0000-0000-0000-000000000001', tr: 'KURGUSAL', en: 'FICTIONAL', items: [{ key: 'crew', href: `/app/crew-roster/${vessel}`, group: 'daily' }] }] })
describe('role menu untrusted wire boundary', () => {
  it('projects known implemented paths only', () => expect(decodeLenses(value(), tenant, actor, vessel).lenses[0].items[0].key).toBe('crew'))
  it.each(['tenantId', 'actorId', 'vesselId'])('rejects mismatched %s before returning labels', field => {
    expect(() => decodeLenses({ ...value(), [field]: actor }, tenant, field === 'actorId' ? tenant : actor, vessel)).toThrow()
  })
  it.each(['//external.invalid', '/app/fleet/foreign', '/app/crew-roster/' + vessel + '?role=admin'])('rejects substituted href %s', href => {
    const v = value(); v.lenses[0].items[0].href = href; expect(() => decodeLenses(v, tenant, actor, vessel)).toThrow()
  })
  it('rejects duplicate roles/items, malformed labels, unknown modes, hidden tug items and state envelopes', () => {
    const a = value(); a.lenses.push(a.lenses[0]); expect(() => decodeLenses(a, tenant, actor, vessel)).toThrow()
    const b = value(); b.lenses[0].items.push(b.lenses[0].items[0]); expect(() => decodeLenses(b, tenant, actor, vessel)).toThrow()
    const c = value(); c.lenses[0].en = 'bad\nlabel'; expect(() => decodeLenses(c, tenant, actor, vessel)).toThrow()
    expect(() => decodeLenses({ ...value(), mode: 'Master' }, tenant, actor, vessel)).toThrow()
    expect(() => decodeLenses({ ...value(), mode: ['Ship'] }, tenant, actor, vessel)).toThrow()
    expect(() => decodeLenses({ ...value(), state: ['Configured'] }, tenant, actor, vessel)).toThrow()
    expect(() => decodeLenses({ ...value(), state: 'Missing' }, tenant, actor, vessel)).toThrow()
    const d = value(); d.lenses[0].items[0] = { key: 'tug-command', href: `/app/tug-command?vessel=${vessel}`, group: 'daily' }
    expect(() => decodeLenses(d, tenant, actor, vessel)).toThrow()
    const e = value(); e.lenses[0].items[0].key = 'unknown'
    expect(() => decodeLenses(e, tenant, actor, vessel)).toThrow()
  })
})
