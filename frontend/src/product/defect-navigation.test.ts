import { expect, it } from 'vitest'
import { decodeEnterpriseNavigation } from './enterprise-navigation'
import { decodeLenses, lensHref } from './workspace-lens-api'
import { decodeSearchPage, isSeparateWorkspaceDestination } from './workspace-search-api'
import { vesselContextHref } from './WorkspaceVesselSwitcher'

const tenant = '11111111-1111-1111-1111-111111111111'
const actor = '22222222-2222-2222-2222-222222222222'
const vessel = '33333333-3333-3333-3333-333333333333'
const role = '44444444-4444-4444-4444-444444444444'
const destination = { key: 'defects', href: '/app/defects' }

it('discovers only the actual canonical defect route returned by the server', () => {
  expect(decodeEnterpriseNavigation({ destinations: [destination], canSearch: true, canWriteRegulatory: false }).destinations).toEqual([destination])
  expect(decodeSearchPage({ items: [{ ...destination, kind: 'Destination', label: 'Defects' }], offset: 0, limit: 25, hasMore: false }, 0).items[0].href).toBe('/app/defects')
  expect(isSeparateWorkspaceDestination('/app/defects')).toBe(true)
  expect(isSeparateWorkspaceDestination(`/app/defects/${vessel}`)).toBe(true)
  expect(() => decodeEnterpriseNavigation({ destinations: [{ ...destination, href: '/app/defects?actor=other' }], canSearch: false, canWriteRegulatory: false })).toThrow()
})

it.each(['Ship', 'Tug'] as const)('preserves the real %s context without carrying a defect or source pin into a different vessel', mode => {
  const href = lensHref('defects', vessel)
  const lenses = decodeLenses({ tenantId: tenant, actorId: actor, vesselId: vessel, mode, state: 'Configured', version: 'source-v1', canSearch: false,
    lenses: [{ reference: role, tr: 'Kaynak rol', en: 'Source role', items: [{ key: 'defects', href, group: 'daily' }] }] }, tenant, actor, vessel)
  expect(lenses.lenses[0].items[0].href).toBe(`/app/defects/${vessel}`)
  expect(vesselContextHref(`/app/defects/${vessel}?id=${role}&assetId=${actor}&assetRevision=7`, tenant, mode)).toBe(`/app/defects/${tenant}`)
})
