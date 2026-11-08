import { expect, it } from 'vitest'
import { decodeEnterpriseNavigation } from './enterprise-navigation'
import { decodeLenses, lensHref } from './workspace-lens-api'
import { decodeSearchPage, isSeparateWorkspaceDestination } from './workspace-search-api'
import { vesselContextHref } from './WorkspaceVesselSwitcher'

const tenant = '11111111-1111-1111-1111-111111111111', actor = '22222222-2222-2222-2222-222222222222'
const vessel = '33333333-3333-3333-3333-333333333333', role = '44444444-4444-4444-4444-444444444444'
const item = { key: 'operational-downtime', href: '/app/operational-downtime' }
it('admits only the exact server-projected downtime destination and its Ship scope', () => {
  expect(decodeEnterpriseNavigation({ destinations: [item], canSearch: false, canWriteRegulatory: false }).destinations).toEqual([item])
  expect(decodeSearchPage({ items: [{ ...item, kind: 'Destination', label: 'Operational downtime' }], offset: 0, limit: 25, hasMore: false }, 0).items[0].href).toBe(item.href)
  expect(isSeparateWorkspaceDestination(item.href)).toBe(true)
  const value = { tenantId: tenant, actorId: actor, vesselId: vessel, mode: 'Ship', state: 'Configured', version: 'source-v1', canSearch: false,
    lenses: [{ reference: role, tr: 'Kaynak rol', en: 'Source role', items: [{ key: item.key, href: lensHref(item.key, vessel), group: 'daily' }] }] }
  expect(decodeLenses(value, tenant, actor, vessel).lenses[0].items[0].href).toBe(`${item.href}/${vessel}`)
  expect(() => decodeLenses({ ...value, mode: 'Tug' }, tenant, actor, vessel)).toThrow()
  expect(vesselContextHref(`${item.href}/${vessel}?id=${actor}`, tenant, 'Ship')).toBe(`${item.href}/${tenant}`)
  expect(vesselContextHref(`${item.href}/${vessel}?originId=${actor}`, tenant, 'Tug')).toBe(`/app/fleet/${tenant}`)
})
