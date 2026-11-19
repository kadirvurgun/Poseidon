import { describe, expect, it } from 'vitest'
import { auditReadyHref, decodeChains, readAuditReadyLocation } from './audit-ready-api'
import { decodeTrace } from './human-review-api'
import { arChains, arControl, arEvidence, arTrace, arVessel } from './audit-ready.test-fixture'

// DEV-032 / T-S3-007: closed contract and route parsing for the audit-ready drill-down.
describe('audit-ready chains contract', () => {
  it('accepts exact appointed chains with declared source locators', () => {
    const value = decodeChains(structuredClone(arChains()), arVessel)
    expect(value.items[0].sources[0].locator).toBe('FICTIONAL Code/Chapter 9/para 4')
  })
  it('rejects extra members, another vessel, invented locator text and forged availability', () => {
    expect(() => decodeChains({ ...arChains(), total: 9 }, arVessel)).toThrow()
    expect(() => decodeChains(arChains(), '30000000-0000-4000-8000-0000000000ff')).toThrow()
    const invented = arChains(); invented.items[0].sources[0] = { ...invented.items[0].sources[0], locatorState: 'Missing', locator: 'guessed' }
    expect(() => decodeChains(invented, arVessel)).toThrow()
    const forged = arChains(); forged.items[0].sources[0] = { ...forged.items[0].sources[0], availability: 'Unavailable' }
    expect(() => decodeChains(forged, arVessel)).toThrow()
    const duplicate = arChains(); duplicate.items.push(structuredClone(duplicate.items[0]))
    expect(() => decodeChains(duplicate, arVessel)).toThrow()
  })
  it('decodes the audit-ready trace with reason/role audit items and rejects a non-decision authority role', () => {
    const anchor = { vesselId: arVessel, evidenceId: arEvidence, evidenceRevision: 2, controlId: arControl, controlRevision: 1 }
    const trace = decodeTrace(structuredClone(arTrace()), anchor, 0)
    expect(trace.auditItems?.[1].authorityRole).toBe('DPA')
    const bad = arTrace(); bad.auditItems![0] = { ...bad.auditItems![0], authorityRole: 'DPA' }
    expect(() => decodeTrace(bad, anchor, 0)).toThrow()
    const admin = arTrace(); admin.auditItems![1] = { ...admin.auditItems![1], authorityRole: 'Admin' as never }
    expect(() => decodeTrace(admin, anchor, 0)).toThrow()
    // Legacy audit items without the additive fields remain valid.
    const legacy = arTrace(); legacy.auditItems = legacy.auditItems!.map(e => { const copy = { ...e }; delete copy.reasonCode; delete copy.authorityRole; return copy })
    expect(() => decodeTrace(legacy, anchor, 0)).not.toThrow()
  })
})

describe('audit-ready route', () => {
  it('parses vessel and exact chain anchor and round-trips the href', () => {
    const chain = { evidenceId: arEvidence, evidenceRevision: 2, controlId: arControl, controlRevision: 1 }
    const href = auditReadyHref(arVessel, chain), [path, query] = href.split('?')
    expect(readAuditReadyLocation(path, '?' + query)).toEqual({ invalid: false, vessel: arVessel, anchor: { vesselId: arVessel, ...chain } })
    expect(readAuditReadyLocation('/app/audit-ready', '')).toEqual({ invalid: false, vessel: '', anchor: null })
  })
  it.each(['?evidenceId=' + arEvidence, '?actorId=' + arEvidence, `?evidenceId=${arEvidence}&evidenceRevision=01&controlId=${arControl}&controlRevision=1`,
    `?evidenceId=${arEvidence}&evidenceRevision=2&controlId=${arControl}&controlRevision=1&tenantId=${arControl}`])('rejects partial, widened or malformed query %s', query => {
    expect(readAuditReadyLocation('/app/audit-ready/' + arVessel, query).invalid).toBe(true)
  })
  it('rejects an anchor without a vessel and a non-GUID vessel', () => {
    expect(readAuditReadyLocation('/app/audit-ready', `?evidenceId=${arEvidence}&evidenceRevision=2&controlId=${arControl}&controlRevision=1`).invalid).toBe(true)
    expect(readAuditReadyLocation('/app/audit-ready/not-a-guid-not-a-guid-not-a-guid-0000', '').invalid).toBe(true)
  })
})
