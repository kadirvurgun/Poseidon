import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import type { CertificateDto } from './certificates-api'
import { decodeCertificateAssertionCapabilities, decodeCertificateAssertionPage, decodeCertificateVerification, type CertificateAssertionCapabilities, type CertificateAssertionState } from './certificate-assertion-contract'
export type CertificateAssertionAttempt = { state: CertificateAssertionState; key: string; owner: string; vessel: string; id: string; authorityId: string; roleId: string; certificateRevision: number; certificateVersion: number; file: { id: string; revision: number; checksum: string }; body: string }
function root(vessel: string, id: string) {
  if (!isGuid(vessel) || !isGuid(id)) throw new ApiError(400, 'invalid_context')
  return 'vessels/' + vessel + '/certificates/' + id
}
export function freezeCertificateAssertion(record: CertificateDto, caps: CertificateAssertionCapabilities, owner: string, authorityId: string, state: CertificateAssertionState, rationale: string): CertificateAssertionAttempt {
  const ids = owner.split('/'), authority = caps.authorities.find(option => option.id === authorityId)
  if (ids.length !== 2 || !ids.every(isGuid) || !record.currentRevision || record.scope !== 'Vessel' || !record.content.document || !caps.canRecord || caps.certificateId !== record.id || caps.certificateRevision !== record.revision || caps.certificateVersion !== record.version || !authority || !authority.states.includes(state) || !rationale.trim() || rationale.length > 2000 || /\p{Cc}/u.test(rationale.replace(/[\r\n\t]/g, ''))) throw new ApiError(400, 'invalid_assertion_input')
  const body = JSON.stringify({ expectedVersion: record.version, certificateRevision: record.revision, expectedAssertionVersion: caps.assertionVersion, authorityId, state, rationale })
  return { state, key: crypto.randomUUID(), owner, vessel: record.vesselId, id: record.id, authorityId, roleId: authority.roleId, certificateRevision: record.revision, certificateVersion: record.version, file: structuredClone(record.content.document), body }
}
export const certificateAssertionApi = {
  capabilities: async (vessel: string, id: string, signal: AbortSignal) => decodeCertificateAssertionCapabilities(await request<unknown>(root(vessel, id) + '/verification-capabilities', signal), id),
  history: async (vessel: string, id: string, offset: number, signal: AbortSignal) => {
    if (!Number.isSafeInteger(offset) || offset < 0 || offset > 100000) throw new ApiError(400, 'invalid_context')
    return decodeCertificateAssertionPage(await request<unknown>(root(vessel, id) + '/verification-assertions?offset=' + offset, signal), id, offset)
  },
  record: async (attempt: CertificateAssertionAttempt, csrf: string, signal: AbortSignal) => {
    const value = await request<unknown>(root(attempt.vessel, attempt.id) + '/verification-assertions', signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body })
    const receipt = decodeCertificateVerification(value, attempt.id, { revision: attempt.certificateRevision, version: attempt.certificateVersion, currentRevision: false, document: attempt.file })
    if (receipt.actorId !== attempt.owner.split('/')[1] || receipt.roleId !== attempt.roleId || receipt.authorityId !== attempt.authorityId) throw new ApiError(502, 'invalid_certificate_assertion_contract')
    return receipt
  },
}
