import { ApiError } from './api'
import { timestamp } from './date-validation'
import { isGuid } from './identifiers'
import { certificateSourceVersion } from './certificate-content'

export const assertionStates = ['Verified', 'Not Verified', 'Suspicious', 'Suspended / Revoked'] as const
export type CertificateAssertionState = typeof assertionStates[number]
export type CertificateVerification = { id: string; certificateId: string; certificateRevision: number; certificateVersion: number; fileId: string; fileRevision: number; checksum: string; revision: number; version: number; state: CertificateAssertionState; actorId: string; roleId: string; authorityId: string; authorityHash: string; authorityBindingHash: string; sourceVersion: string; savedAt: string; current: boolean; blockers: string[]; qualification: 'RECORDED_UNASSESSED' }
export type CertificateAssertionAuthority = { id: string; roleId: string; duty: 'Auditor'; states: CertificateAssertionState[] }
export type CertificateAssertionCapabilities = { certificateId: string; certificateRevision: number; certificateVersion: number; assertionVersion: number; canRecord: boolean; authorities: CertificateAssertionAuthority[]; blockers: string[]; sourceVersion: string }
export type CertificateAssertionPage = { items: CertificateVerification[]; offset: number; limit: 25; hasMore: boolean }
export type AssertionCertificatePin = { revision: number; version: number; currentRevision: boolean; document: { id: string; revision: number; checksum: string } | null }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const integer = (v: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const uint = (v: unknown, min = 1): v is number => integer(v, min, 4294967295)
const hash = (v: unknown): v is string => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 128 && v.every(code => typeof code === 'string' && code.length > 0 && code.length <= 200 && !/\p{Cc}/u.test(code))
function fail(): never { throw new ApiError(502, 'invalid_certificate_assertion_contract') }
function authority(value: unknown): value is CertificateAssertionAuthority {
  if (!exact(value, ['id', 'roleId', 'duty', 'states']) || !isGuid(value.id) || !isGuid(value.roleId) || value.duty !== 'Auditor' || !Array.isArray(value.states)) return false
  const states = value.states
  return states.length === assertionStates.length && assertionStates.every(state => states.includes(state))
}
export function decodeCertificateVerification(value: unknown, certificate: string, pin?: AssertionCertificatePin): CertificateVerification {
  if (!isGuid(certificate) || !exact(value, ['id', 'certificateId', 'certificateRevision', 'certificateVersion', 'fileId', 'fileRevision', 'checksum', 'revision', 'version', 'state', 'actorId', 'roleId', 'authorityId', 'authorityHash', 'authorityBindingHash', 'sourceVersion', 'savedAt', 'current', 'blockers', 'qualification']) || !['id', 'fileId', 'actorId', 'roleId', 'authorityId'].every(key => isGuid(value[key])) || value.certificateId !== certificate || !integer(value.certificateRevision) || !uint(value.certificateVersion) || !integer(value.fileRevision) || !integer(value.revision) || !uint(value.version) || !hash(value.checksum) || !hash(value.authorityHash) || !hash(value.authorityBindingHash) || !assertionStates.some(state => value.state === state) || value.sourceVersion !== certificateSourceVersion || !timestamp(value.savedAt) || typeof value.current !== 'boolean' || value.qualification !== 'RECORDED_UNASSESSED' || !codes(value.blockers) || !value.blockers.includes('LEGAL_TRANSITION_SOURCE_UNAVAILABLE')) fail()
  if (pin && (value.certificateRevision !== pin.revision || value.certificateVersion !== pin.version || !pin.document || value.fileId !== pin.document.id || value.fileRevision !== pin.document.revision || value.checksum !== pin.document.checksum || value.current && !pin.currentRevision)) fail()
  return value as CertificateVerification
}
export function decodeCertificateAssertionCapabilities(value: unknown, certificate: string): CertificateAssertionCapabilities {
  if (!exact(value, ['certificateId', 'certificateRevision', 'certificateVersion', 'assertionVersion', 'canRecord', 'authorities', 'blockers', 'sourceVersion']) || value.certificateId !== certificate || !integer(value.certificateRevision) || !uint(value.certificateVersion) || !uint(value.assertionVersion, 0) || typeof value.canRecord !== 'boolean' || value.sourceVersion !== certificateSourceVersion || !codes(value.blockers) || !Array.isArray(value.authorities) || value.authorities.length > 256 || !value.authorities.every(authority) || new Set(value.authorities.map(authority => authority.id)).size !== value.authorities.length || value.canRecord && value.authorities.length === 0) fail()
  return value as CertificateAssertionCapabilities
}
export function decodeCertificateAssertionPage(value: unknown, certificate: string, offset: number): CertificateAssertionPage {
  if (!exact(value, ['items', 'offset', 'limit', 'hasMore']) || !Array.isArray(value.items) || value.items.length > 25 || value.offset !== offset || value.limit !== 25 || typeof value.hasMore !== 'boolean') fail()
  const items = value.items.map(row => decodeCertificateVerification(row, certificate))
  if (new Set(items.map(row => row.id)).size !== items.length || new Set(items.map(row => row.revision)).size !== items.length || items.some(row => row.current)) fail()
  return { items, offset, limit: 25, hasMore: value.hasMore }
}
