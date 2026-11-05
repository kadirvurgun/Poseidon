import { ApiError } from './api'
import { calendar, timestamp } from './date-validation'
import { isGuid } from './identifiers'

export type PassportCertificateAssertion = { id: string; revision: number; version: number; state: 'Verified' | 'Not Verified' | 'Suspicious' | 'Suspended / Revoked'; qualification: 'RECORDED_UNASSESSED'; current: boolean; recordedAt: string; sourceVersion: string; blockers: string[] }
export type PassportCertificate = { id: string; revision: number; version: number; typeReference: string | null; number: string | null; issuer: string | null; issueDate: string | null; expiryDate: string | null; temporalState: 'VALID' | 'DUE' | 'SURVEY_WINDOW' | 'EXPIRED' | 'REVIEW_REQUIRED'; qualification: 'REVIEW_REQUIRED'; document: { id: string; revision: number; checksum: string } | null; sourceVersion: string; hash: string; href: string; assertion: PassportCertificateAssertion | null }
export type PassportCertificates = { coverage: 'UNAVAILABLE' | 'AUTHORIZED_PAGE_ONLY'; items: PassportCertificate[]; offset: number; limit: 25; hasMore: boolean; referenceDate: string; blockers: string[] }
export const passportCertificateSource = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:33-40,1634-1657,1708-1755:v46.3'
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, keys: string[]): value is Record<string, unknown> => object(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key))
const positive = (value: unknown) => typeof value === 'number' && Number.isSafeInteger(value) && value > 0
const hash = (value: unknown) => typeof value === 'string' && /^[A-Fa-f0-9]{64}$/.test(value)
const nullableText = (value: unknown) => value === null || typeof value === 'string' && value.length <= 4096 && !/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]/.test(value)

export function decodePassportCertificates(value: unknown, vessel: string, expectedOffset?: number): PassportCertificates {
  const fail = (): never => { throw new ApiError(502, 'invalid_passport_certificate_contract') }
  if (!isGuid(vessel) || !exact(value, ['coverage', 'items', 'offset', 'limit', 'hasMore', 'referenceDate', 'blockers']) ||
    !['UNAVAILABLE', 'AUTHORIZED_PAGE_ONLY'].includes(String(value.coverage)) || !Array.isArray(value.items) || value.items.length > 25 ||
    typeof value.offset !== 'number' || !Number.isSafeInteger(value.offset) || value.offset < 0 || value.offset > 100000 || expectedOffset !== undefined && value.offset !== expectedOffset ||
    value.limit !== 25 || typeof value.hasMore !== 'boolean' || value.hasMore && value.items.length !== 25 || !calendar(value.referenceDate) ||
    !Array.isArray(value.blockers) || value.blockers.length > 64 || !value.blockers.every(code => typeof code === 'string' && /^[A-Z_]{1,200}$/.test(code)) ||
    !value.blockers.includes('CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED') ||
    value.coverage === 'UNAVAILABLE' && (value.items.length !== 0 || value.hasMore || !value.blockers.includes('CERTIFICATE_READ_ACCESS_UNAVAILABLE')) ||
    value.coverage === 'AUTHORIZED_PAGE_ONLY' && !value.blockers.includes('CERTIFICATE_QUALIFICATION_REQUIRED')) fail()
  const ids = new Set<string>()
  for (const row of value.items as unknown[]) {
    if (!exact(row, ['id', 'revision', 'version', 'typeReference', 'number', 'issuer', 'issueDate', 'expiryDate', 'temporalState', 'qualification', 'document', 'sourceVersion', 'hash', 'href', 'assertion']) ||
      !isGuid(row.id) || ids.has(row.id) || !positive(row.revision) || !positive(row.version) || Number(row.version) > 4294967295 ||
      !nullableText(row.typeReference) || !nullableText(row.number) || !nullableText(row.issuer) || !(row.issueDate === null || calendar(row.issueDate)) || !(row.expiryDate === null || calendar(row.expiryDate)) ||
      !['VALID', 'DUE', 'SURVEY_WINDOW', 'EXPIRED', 'REVIEW_REQUIRED'].includes(String(row.temporalState)) || row.qualification !== 'REVIEW_REQUIRED' || row.sourceVersion !== passportCertificateSource || !hash(row.hash) ||
      row.href !== `/app/certificate-command-center/${vessel}?id=${row.id}` ||
      !(row.document === null || exact(row.document, ['id', 'revision', 'checksum']) && isGuid(row.document.id) && positive(row.document.revision) && hash(row.document.checksum))) fail()
    const assertion = row.assertion
    if (assertion !== null && (!exact(assertion, ['id', 'revision', 'version', 'state', 'qualification', 'current', 'recordedAt', 'sourceVersion', 'blockers']) ||
      row.document === null || !isGuid(assertion.id) || !positive(assertion.revision) || !positive(assertion.version) || Number(assertion.version) > 4294967295 ||
      !['Verified', 'Not Verified', 'Suspicious', 'Suspended / Revoked'].includes(String(assertion.state)) || assertion.qualification !== 'RECORDED_UNASSESSED' ||
      typeof assertion.current !== 'boolean' || !timestamp(assertion.recordedAt) || assertion.sourceVersion !== row.sourceVersion || !Array.isArray(assertion.blockers) || assertion.blockers.length > 64 ||
      !assertion.blockers.every(code => typeof code === 'string' && /^[A-Z_]{1,200}$/.test(code)) || !assertion.blockers.includes('LEGAL_TRANSITION_SOURCE_UNAVAILABLE') ||
      !assertion.current && !assertion.blockers.includes('RECORDED_ASSERTION_NOT_CURRENT'))) fail()
    ids.add(row.id as string)
  }
  return value as PassportCertificates
}
