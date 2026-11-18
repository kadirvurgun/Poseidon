import { afterEach, describe, expect, it, vi } from 'vitest'
import { decodeMetricCatalog, decodeMetricScalar, metricIds, metricScalar } from './metrics-api'

const vessel = '30000000-0000-0000-0000-000000000001'
const scalar = (change: Record<string, unknown> = {}) => ({
  metricId: metricIds.onboardingReviewRequired, definitionVersion: 1, ownerModule: '59.01/18.01 vessel onboarding (ComplianceOnboardingPolicy)',
  state: 'VALUE', value: '2', unitCode: 'vessel', quality: 'COMPLETE', asOf: '2026-11-18T12:00:00.1234567+00:00',
  sourceProjectionId: 'projection.compliance-onboarding.v1', sourceWatermark: null,
  scope: { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 2, readSources: 2, withheldSources: 0 },
  unknownCount: 0, reasons: [], basis: 'DECLARED_UNVERIFIED', freshnessRule: 'LIVE_READ;55.01 rpt.freshness.online_min',
  source: '55.01/paragraph:156', ...change,
})

afterEach(() => vi.unstubAllGlobals())

describe('PLAN-1511-5501-SLICE-01 metric scalar consumer contract', () => {
  it('accepts a VALUE scalar and the exact vessel scope', () => {
    expect(decodeMetricScalar(scalar(), metricIds.onboardingReviewRequired).value).toBe('2')
    const scoped = scalar({ scope: { kind: 'VESSEL', vesselId: vessel, expectedSources: 1, readSources: 1, withheldSources: 0 } })
    expect(decodeMetricScalar(scoped, metricIds.onboardingReviewRequired, vessel).scope.kind).toBe('VESSEL')
    expect(() => decodeMetricScalar(scoped, metricIds.onboardingReviewRequired, '30000000-0000-0000-0000-000000000002')).toThrow()
    expect(() => decodeMetricScalar(scalar(), metricIds.crewDeclaredPersons)).toThrow()
  })
  it('never accepts a value or green quality for suppressed, unknown or unavailable scalars', () => {
    const unknown = scalar({ state: 'UNKNOWN', value: null, quality: 'UNKNOWN', reasons: ['SOURCE_WITHHELD_OR_UNREADABLE'],
      scope: { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 1, readSources: 0, withheldSources: 1 }, unknownCount: 1 })
    expect(decodeMetricScalar(unknown, metricIds.onboardingReviewRequired).value).toBeNull()
    for (const forged of [{ ...unknown, value: '0' }, { ...unknown, quality: 'COMPLETE' }, scalar({ state: 'SUPPRESSED', value: '2', reasons: ['SMALL_GROUP'] }),
      scalar({ state: 'SUPPRESSED', value: null, reasons: [] }), scalar({ quality: 'COMPLETE', unknownCount: 1 }),
      scalar({ scope: { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 2, readSources: 1, withheldSources: 1 } }),
      scalar({ state: 'UNAVAILABLE', value: null, quality: 'PARTIAL', reasons: ['QUERY_LIMIT_EXCEEDED'] }),
      scalar({ value: '-1' }), scalar({ value: 2 }), scalar({ extra: true }), scalar({ asOf: 'yesterday' })])
      expect(() => decodeMetricScalar(forged, metricIds.onboardingReviewRequired)).toThrow()
    const suppressed = scalar({ metricId: metricIds.crewDeclaredPersons, state: 'SUPPRESSED', value: null, reasons: ['SMALL_GROUP'] })
    expect(decodeMetricScalar(suppressed, metricIds.crewDeclaredPersons).state).toBe('SUPPRESSED')
  })
  it('requests only the declared selectors and decodes the catalog', async () => {
    const fetch = vi.fn(async () => new Response(JSON.stringify(scalar({ scope: { kind: 'VESSEL', vesselId: vessel, expectedSources: 1, readSources: 1, withheldSources: 0 } }))))
    vi.stubGlobal('fetch', fetch)
    await metricScalar(metricIds.onboardingReviewRequired, new AbortController().signal, vessel)
    expect(fetch).toHaveBeenCalledWith(`/api/v1/metrics/scalar?metricId=onboarding.vessels.review_required&vesselId=${vessel}`, expect.anything())
    const definition = { id: 'a.b', version: 1, ownerModule: 'm', label: 'l', unitCode: 'vessel', sourceProjectionId: 'p', aggregation: 'SUM', minCohort: 1,
      freshnessRule: 'f', requiredPermissions: ['directory.read'], allowedActions: ['view'], scopes: ['VESSEL'], basis: 'DECLARED_UNVERIFIED', source: 's' }
    expect(decodeMetricCatalog({ contractVersion: 'metrics.v1', items: [definition] }).items).toHaveLength(1)
    expect(() => decodeMetricCatalog({ contractVersion: 'metrics.v1', items: [definition, definition] })).toThrow()
    expect(() => decodeMetricCatalog({ contractVersion: 'metrics.v2', items: [] })).toThrow()
  })
})
