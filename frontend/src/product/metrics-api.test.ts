import { afterEach, describe, expect, it, vi } from 'vitest'
import { decodeMetricCatalog, decodeMetricScalar, metricIds, metricScalar } from './metrics-api'

const vessel = '30000000-0000-0000-0000-000000000001'
const scalar = (change: Record<string, unknown> = {}) => ({
  metricId: metricIds.onboardingReviewRequired, definitionVersion: 1, ownerModule: '59.01/18.01 vessel onboarding (ComplianceOnboardingPolicy)',
  state: 'VALUE', value: '2', unitCode: 'vessel', quality: 'COMPLETE', asOf: '2026-11-18T12:00:00.1234567+00:00',
  sourceProjectionId: 'projection.compliance-onboarding.v1', sourceWatermark: null,
  scope: { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 2, readSources: 2, withheldSources: 0, unreadableSources: 0, notCollectedSources: 0 },
  unknownCount: 0, reasons: [], basis: 'DECLARED_UNVERIFIED', freshnessRule: 'LIVE_READ;55.01 rpt.freshness.online_min',
  source: '55.01/paragraph:156', ...change,
})

const tenant = { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 2, readSources: 2, withheldSources: 0, unreadableSources: 0, notCollectedSources: 0 }
const definition = { id: 'a.b', version: 1, ownerModule: 'm', label: 'l', unitCode: 'vessel', sourceProjectionId: 'p', aggregation: 'SUM', minCohort: 1,
  freshnessRule: 'f', requiredPermissions: ['directory.read'], allowedActions: ['view'], scopes: ['VESSEL'], basis: 'DECLARED_UNVERIFIED', source: 's' }

afterEach(() => vi.unstubAllGlobals())

describe('PLAN-1511-5501-SLICE-01 metric scalar consumer contract', () => {
  it('accepts a VALUE scalar and the exact vessel scope', () => {
    expect(decodeMetricScalar(scalar(), metricIds.onboardingReviewRequired).value).toBe('2')
    const scoped = scalar({ scope: { kind: 'VESSEL', vesselId: vessel, expectedSources: 1, readSources: 1, withheldSources: 0, unreadableSources: 0, notCollectedSources: 0 } })
    expect(decodeMetricScalar(scoped, metricIds.onboardingReviewRequired, vessel).scope.kind).toBe('VESSEL')
    expect(() => decodeMetricScalar(scoped, metricIds.onboardingReviewRequired, '30000000-0000-0000-0000-000000000002')).toThrow()
    expect(() => decodeMetricScalar(scalar(), metricIds.crewDeclaredPersons)).toThrow()
  })
  it('never accepts a value or green quality for suppressed, unknown or unavailable scalars', () => {
    const unknown = scalar({ state: 'UNKNOWN', value: null, quality: 'UNKNOWN', reasons: ['SOURCE_WITHHELD'],
      scope: { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 1, readSources: 0, withheldSources: 1, unreadableSources: 0, notCollectedSources: 0 }, unknownCount: 1 })
    expect(decodeMetricScalar(unknown, metricIds.onboardingReviewRequired).value).toBeNull()
    for (const forged of [{ ...unknown, value: '0' }, { ...unknown, quality: 'COMPLETE' }, scalar({ state: 'SUPPRESSED', value: '2', reasons: ['SMALL_GROUP'] }),
      scalar({ state: 'SUPPRESSED', value: null, reasons: [] }), scalar({ quality: 'COMPLETE', unknownCount: 1 }),
      scalar({ scope: { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 2, readSources: 1, withheldSources: 1, unreadableSources: 0, notCollectedSources: 0 } }),
      scalar({ state: 'UNAVAILABLE', value: null, quality: 'PARTIAL', reasons: ['QUERY_LIMIT_EXCEEDED'], scope: { ...tenant, readSources: 0 } }),
      scalar({ value: '-1' }), scalar({ value: 2 }), scalar({ extra: true }), scalar({ asOf: 'yesterday' })])
      expect(() => decodeMetricScalar(forged, metricIds.onboardingReviewRequired)).toThrow()
    const suppressed = scalar({ metricId: metricIds.crewDeclaredPersons, state: 'SUPPRESSED', value: null, reasons: ['SMALL_GROUP'] })
    expect(decodeMetricScalar(suppressed, metricIds.crewDeclaredPersons).state).toBe('SUPPRESSED')
  })
  it('rejects coverage that does not add up and qualities that contradict the state', () => {
    const notCollected = scalar({ quality: 'PARTIAL', unknownCount: 1, reasons: ['PARTIAL_SOURCE_COVERAGE', 'SOURCE_NOT_COLLECTED'],
      scope: { ...tenant, readSources: 1, notCollectedSources: 1 } })
    expect(decodeMetricScalar(notCollected, metricIds.onboardingReviewRequired).scope.notCollectedSources).toBe(1)
    const unreadable = scalar({ state: 'UNKNOWN', value: null, quality: 'UNKNOWN', unknownCount: 2, reasons: ['SOURCE_UNREADABLE'],
      scope: { ...tenant, readSources: 0, unreadableSources: 2 } })
    expect(decodeMetricScalar(unreadable, metricIds.onboardingReviewRequired).state).toBe('UNKNOWN')
    const unavailable = scalar({ state: 'UNAVAILABLE', value: null, quality: 'UNKNOWN', reasons: ['QUERY_LIMIT_EXCEEDED'],
      scope: { ...tenant, expectedSources: 1001, readSources: 0 } })
    expect(decodeMetricScalar(unavailable, metricIds.onboardingReviewRequired).state).toBe('UNAVAILABLE')
    for (const forged of [
      scalar({ quality: 'UNKNOWN' }), // VALUE never carries quality UNKNOWN
      scalar({ quality: 'PARTIAL' }), // PARTIAL naming nothing unknown, withheld, unreadable or not collected
      scalar({ scope: { ...tenant, readSources: 1 } }), // one source unaccounted for: read + unread < expected
      scalar({ quality: 'COMPLETE', scope: { ...tenant, readSources: 1, unreadableSources: 1 } }),
      scalar({ quality: 'COMPLETE', scope: { ...tenant, readSources: 1, notCollectedSources: 1 } }),
      scalar({ scope: { ...tenant, notCollectedSources: -1 } }),
      scalar({ scope: { kind: 'TENANT_VISIBLE', vesselId: null, expectedSources: 2, readSources: 2, withheldSources: 0 } }),
      { ...unavailable, scope: { ...tenant, expectedSources: 1001, readSources: 1000 } },
    ]) expect(() => decodeMetricScalar(forged, metricIds.onboardingReviewRequired)).toThrow()
  })
  it('rejects catalog scopes and actions outside the contract enums', () => {
    for (const change of [{ scopes: ['FLEET'] }, { scopes: ['VESSEL', 'VESSEL'] }, { scopes: [] }, { allowedActions: ['export'] },
      { allowedActions: ['view', 'share'] }, { allowedActions: ['VIEW'] }])
      expect(() => decodeMetricCatalog({ contractVersion: 'metrics.v1', items: [{ ...definition, ...change }] })).toThrow()
    expect(decodeMetricCatalog({ contractVersion: 'metrics.v1', items: [{ ...definition, scopes: ['TENANT_VISIBLE', 'VESSEL'] }] }).items[0].scopes)
      .toEqual(['TENANT_VISIBLE', 'VESSEL'])
  })
  it('requests only the declared selectors and decodes the catalog', async () => {
    const fetch = vi.fn(async () => new Response(JSON.stringify(scalar({ scope: { kind: 'VESSEL', vesselId: vessel, expectedSources: 1, readSources: 1, withheldSources: 0, unreadableSources: 0, notCollectedSources: 0 } }))))
    vi.stubGlobal('fetch', fetch)
    await metricScalar(metricIds.onboardingReviewRequired, new AbortController().signal, vessel)
    expect(fetch).toHaveBeenCalledWith(`/api/v1/metrics/scalar?metricId=onboarding.vessels.review_required&vesselId=${vessel}`, expect.anything())
    expect(decodeMetricCatalog({ contractVersion: 'metrics.v1', items: [definition] }).items).toHaveLength(1)
    expect(() => decodeMetricCatalog({ contractVersion: 'metrics.v1', items: [definition, definition] })).toThrow()
    expect(() => decodeMetricCatalog({ contractVersion: 'metrics.v2', items: [] })).toThrow()
  })
})
