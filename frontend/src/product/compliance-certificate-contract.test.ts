import { expect, it } from 'vitest'
import { decodePassport } from './compliance-api'
import { decodePassportCertificates } from './compliance-certificate-contract'
import { authorizedCertificatePageFixture, certificateDeclarationFixture, certificatePageFixture, complianceVessel, otherComplianceVessel, passportFixture, recordedCertificatePageFixture } from './compliance-test-fixtures'

it('keeps authorized empty pages distinct from unavailable access and inventory completeness', () => {
  expect(decodePassportCertificates(certificatePageFixture(), complianceVessel).coverage).toBe('UNAVAILABLE')
  const page = { ...authorizedCertificatePageFixture(), items: [] }
  expect(decodePassportCertificates(page, complianceVessel)).toMatchObject({ coverage: 'AUTHORIZED_PAGE_ONLY', hasMore: false })
})

it.each([{ qualification: 'VERIFIED' }, { temporalState: 'READY' }, { version: 0 }, { hash: null }, { sourceVersion: 'invented' }, { href: 'https://foreign.invalid' }, { document: { id: otherComplianceVessel, revision: 0, checksum: 'D'.repeat(64) } }])('rejects fabricated qualification and malformed owning pins: %j', patch => {
  expect(() => decodePassportCertificates({ ...authorizedCertificatePageFixture(), items: [{ ...certificateDeclarationFixture(), ...patch }] }, complianceVessel)).toThrow()
})

it('rejects cross-vessel links, duplicated records, false paging and inaccessible rows', () => {
  const page = authorizedCertificatePageFixture()
  expect(() => decodePassportCertificates(page, otherComplianceVessel)).toThrow()
  expect(() => decodePassportCertificates({ ...page, items: [page.items[0], page.items[0]] }, complianceVessel)).toThrow()
  expect(() => decodePassportCertificates({ ...page, hasMore: true }, complianceVessel)).toThrow()
  expect(() => decodePassportCertificates({ ...page, coverage: 'UNAVAILABLE' }, complianceVessel)).toThrow()
  expect(() => decodePassportCertificates({ ...page, blockers: [] }, complianceVessel)).toThrow()
  expect(() => decodePassportCertificates(page, complianceVessel, 25)).toThrow()
})

it('pins outer coverage and server calendar context to the owning projection', () => {
  const passport = { ...passportFixture(), certificateCoverage: 'AUTHORIZED_PAGE_ONLY', certificates: authorizedCertificatePageFixture() }
  expect(decodePassport(passport, complianceVessel).certificates.items).toHaveLength(1)
  expect(() => decodePassport({ ...passport, certificateCoverage: 'COMPLETE' }, complianceVessel)).toThrow()
  expect(() => decodePassport({ ...passport, certificates: { ...passport.certificates, referenceDate: '2026-11-03' } }, complianceVessel)).toThrow()
})

it('preserves exact recorded human statements with assessment separate from legal qualification', () => {
  for (const current of [true, false]) {
    const page = decodePassportCertificates(recordedCertificatePageFixture(current), complianceVessel)
    expect(page.items[0].assertion).toMatchObject({ state: 'Verified', qualification: 'RECORDED_UNASSESSED', current })
    expect(page.items[0].qualification).toBe('REVIEW_REQUIRED')
  }
  const page = recordedCertificatePageFixture(), row = page.items[0]
  for (const assertion of [{ ...row.assertion, qualification: 'HUMAN_VERIFIED' }, { ...row.assertion, current: false }, { ...row.assertion, state: 'READY' }, { ...row.assertion, version: 0 }])
    expect(() => decodePassportCertificates({ ...page, items: [{ ...row, assertion }] }, complianceVessel)).toThrow()
  expect(() => decodePassportCertificates({ ...page, items: [{ ...row, document: null }] }, complianceVessel)).toThrow()
})
