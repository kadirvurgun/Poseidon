import assert from 'node:assert/strict'
import test from 'node:test'

// REGISTER-SOURCE-PIN-SEPARATION-01 / GEN-S01. This test runs with native
// Node before any provider/profile/browser. No loader, Vite transform or glob shim.
// The source-reviewed family declaration is independent of cases.ts aggregation:
// dropping a family from that aggregator must fail this whole-manifest gate.
const declaredFamilies = [
  ['../register-specs/daily-cases.ts', 'dailyCases'],
  ['../register-specs/daily-offline-cases.ts', 'dailyOfflineCases'],
  ['../register-specs/certificate-extraction-cases.ts', 'certificateExtractionCases'],
  ['../register-specs/certificate-cases.ts', 'certificateCases'],
  ['../register-specs/ship-operations-cases.ts', 'shipOperationsCases'],
  ['../register-specs/defect-cases.ts', 'defectCases'],
  ['../register-specs/work-order-cases.ts', 'workOrderCases'],
  ['../register-specs/downtime-cases.ts', 'downtimeCases'],
  ['../register-specs/enterprise-shell-cases.ts', 'enterpriseShellCases'],
  ['../register-specs/operation-equipment-cases.ts', 'operationEquipmentCases'],
  ['../register-specs/compliance-input-cases.ts', 'complianceInputCases'],
  ['../register-specs/crew-catalog-cases.ts', 'crewCatalogCases'],
  ['../register-specs/lifecycle-applicability-cases.ts', 'lifecycleApplicabilityCases'],
  ['../register-specs/control-evidence-cases.ts', 'controlEvidenceCases'],
  ['../register-specs/human-review-cases.ts', 'humanReviewCases'],
  ['../register-specs/regulatory-admission-cases.ts', 'regulatoryAdmissionCases'],
  ['../register-specs/offline-application-cases.ts', 'offlineApplicationCases'],
  ['../register-specs/crew-identity-cases.ts', 'crewIdentityCases'],
  ['../register-specs/crew-legacy-cases.ts', 'crewLegacyCases'],
  ['../register-specs/person-file-cases.ts', 'personFileCases'],
  ['../register-specs/search-cases.ts', 'searchCases'],
  ['../register-specs/context-cases.ts', 'contextCases'],
  ['../register-specs/navigation-role-lens-cases.ts', 'navigationRoleLensCases'],
  ['../register-specs/import-cases.ts', 'importCases'],
  ['../register-specs/tug-cases.ts', 'tugCases'],
  ['../register-specs/today-tug-cases.ts', 'todayTugCases'],
  ['../register-specs/technical-cases.ts', 'technicalCases'],
  ['../register-specs/crew-cases.ts', 'crewCases'],
  ['../register-specs/audit-cases.ts', 'auditCases'],
  ['../register-specs/audit-ready-cases.ts', 'auditReadyCases'],
  ['../register-specs/file-cases.ts', 'fileCases'],
] as const
type Entry = { name: string; run: unknown; restricted: boolean; account?: 'lab-crew-boundary' }

test('native Node imports the entire declared signed register manifest with every unique callable case', async () => {
  const manifest = await import('../register-specs/cases.ts')
  const actual = manifest.registerCases
  assert.ok(Array.isArray(actual) && actual.length > 0, 'nonempty whole register manifest required')
  const expected: Entry[] = []
  for (const [source, exported] of declaredFamilies) {
    const module = await import(new URL(source, import.meta.url).href)
    const group: unknown = module[exported]
    assert.ok(Array.isArray(group) && group.length > 0, 'declared register family missing')
    expected.push(...group as Entry[])
  }
  expected.push(
    { name: 'draft-citation', run: manifest.draftCitation, restricted: false },
    { name: 'response-loss-conflict', run: manifest.responseLossConflict, restricted: false },
    { name: 'restricted-membership', run: manifest.restrictedRegister, restricted: true },
  )
  const names = actual.map(entry => entry.name)
  assert.equal(new Set(names).size, names.length, 'register case IDs must be unique')
  assert.deepEqual(names, expected.map(entry => entry.name), 'whole declared manifest/order required')
  actual.forEach((entry, index) => {
    assert.equal(typeof entry.name, 'string')
    assert.ok(entry.name.length > 0 && entry.name.trim() === entry.name, 'nonempty declared case ID required')
    assert.equal(typeof entry.run, 'function', 'every registered journey must be callable')
    assert.equal(typeof entry.restricted, 'boolean', 'explicit genuine account scope required')
    assert.equal(entry.run, expected[index].run, 'the declared journey callback must be registered')
    assert.equal(entry.restricted, expected[index].restricted, 'the declared account scope must be retained')
    assert.equal(('account' in entry ? entry.account : undefined), expected[index].account, 'declared account metadata required')
  })
  const overrides = actual.filter(entry => 'account' in entry)
  assert.equal(overrides.length, 1, 'only the crew boundary journey selects its dedicated persona')
  assert.equal(overrides[0].name, 'crew-assignment-policy-history-and-owning-input-boundary')
  assert.equal('account' in overrides[0] && overrides[0].account, 'lab-crew-boundary')
  assert.equal(overrides[0].restricted, false)
})
