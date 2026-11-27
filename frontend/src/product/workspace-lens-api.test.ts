import { describe, expect, it } from 'vitest'
import { decodeLenses, reviewKeys } from './workspace-lens-api'
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


it('admits actual scoped daily destinations without manufacturing missing role work', () => {
  const base = value()
  const daily = { ...base, lenses: [{ ...base.lenses[0], items: [{ key: 'noon-report', href: `/app/noon-report/${vessel}`, group: 'daily' }, { key: 'deck-daily-log', href: `/app/deck-daily-log/${vessel}`, group: 'daily' }, { key: 'engine-daily-log', href: `/app/engine-daily-log/${vessel}`, group: 'daily' }] }] }
  expect(decodeLenses(daily, tenant, actor, vessel).lenses[0].items).toHaveLength(3)
  expect(decodeLenses(base, tenant, actor, vessel).lenses[0].items.some(item => item.key === 'noon-report')).toBe(false)
  expect(() => decodeLenses({ ...daily, lenses: [{ ...daily.lenses[0], items: [{ ...daily.lenses[0].items[0], href: `/app/noon-report/${actor}` }] }] }, tenant, actor, vessel)).toThrow()
})

// DEV022-AC-07: closed lens shape at every level; no hidden count, denied list or extra role data.
it('rejects extra members on the envelope, a lens or an item', () => {
  expect(() => decodeLenses({ ...value(), hiddenCount: 1 }, tenant, actor, vessel)).toThrow()
  const lens = value(); (lens.lenses[0] as Record<string, unknown>).deniedItems = ['audit']
  expect(() => decodeLenses(lens, tenant, actor, vessel)).toThrow()
  const item = value(); (item.lenses[0].items[0] as Record<string, unknown>).badge = 3
  expect(() => decodeLenses(item, tenant, actor, vessel)).toThrow()
  expect(decodeLenses(value(), tenant, actor, vessel).lenses).toHaveLength(1)
})

// UAT-DEPLOY-01: the server marks these Fleet destinations "review"; rejecting them made the whole role menu unavailable.
it.each(['data-trust-center', 'industry-assurance', 'regulatory-review'])('admits the server review group for %s', key => {
  const fleet = { ...value(), vesselId: null, mode: 'Fleet', lenses: [{ ...value().lenses[0], items: [{ key, href: `/app/${key}`, group: 'review' }] }] }
  expect(decodeLenses(fleet, tenant, actor, null).lenses[0].items[0].group).toBe('review')
  expect(() => decodeLenses({ ...fleet, lenses: [{ ...fleet.lenses[0], items: [{ key, href: `/app/${key}`, group: 'daily' }] }] }, tenant, actor, null)).toThrow()
})

it('keeps the client review set identical to the server group rule', async () => {
  const { readFileSync } = await import('node:fs')
  const { dirname, join } = await import('node:path')
  const { fileURLToPath } = await import('node:url')
  const server = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '../../../backend/Poseidon.Api/WorkspaceLensEndpoints.cs'), 'utf8')
  const rule = server.slice(server.indexOf('x.Key is "sources"'), server.indexOf('? "review" : "daily"'))
  const fleetReview = [...rule.matchAll(/"([a-z-]+)"/g)].map(m => m[1])
  const vesselReview = [...server.matchAll(/items\.Add\(new\("([a-z-]+)", [^)]*"review"\)\)/g)].map(m => m[1])
  expect(fleetReview.length).toBeGreaterThan(5)
  expect([...new Set([...fleetReview, ...vesselReview])].sort()).toEqual([...reviewKeys].sort())
})
