import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { components } from '../generated/api';
import { ApiError, request } from './api';
import type { ImportMappingTemplate, ImportMappingBinding } from './ImportMappingEditor';
import type { ImportValidationPage } from './ImportValidationResults';
export type ImportTemplate = components['schemas']['ImportTemplateDto'];
export type ImportBatch = components['schemas']['ImportBatchDto'];
export type Preview = components['schemas']['ImportPreviewPageDto'];
export type Committed = components['schemas']['ImportCommittedDto'];
export const base = (vessel: string) => `vessels/${encodeURIComponent(vessel)}/imports`;
const json = (csrf: string, method: string, body?: unknown, key?: string): RequestInit => ({ method,
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, ...(key ? { 'Idempotency-Key': key } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
export const templates = (v: string, signal: AbortSignal) => request<ImportTemplate[]>(`${base(v)}/templates`, signal);
export const detail = (v: string, id: string, signal: AbortSignal) => request<ImportBatch>(`${base(v)}/${encodeURIComponent(id)}`, signal);
export const history = (v: string, offset: number, signal: AbortSignal) => request<{
    items: {
        id: string;
        category: string;
        state: string;
        rowCount: number;
        createdAt: string;
    }[];
    hasMore: boolean;
}>(`${base(v)}?offset=${offset}`, signal);
export const upload = (v: string, category: string, assignment: string, file: File, csrf: string, signal: AbortSignal) => request<ImportBatch>(`${base(v)}/uploads/${encodeURIComponent(category)}${category === 'certificate' ? `?assignment=${encodeURIComponent(assignment)}` : ''}`, signal, { method: 'POST', headers: { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'X-CSRF-TOKEN': csrf }, body: file });
export const draft = (v: string, batch: ImportBatch, bindings: readonly ImportMappingBinding[], csrf: string, signal: AbortSignal) => {
    const command: components['schemas']['ImportMappingCommand'] = { version: batch.version, bindings: [...bindings] };
    return request<ImportBatch>(`${base(v)}/${batch.id}/mapping`, signal, json(csrf, 'PUT', command));
};
export const preview = (v: string, batch: string, csrf: string, signal: AbortSignal) => request<Preview>(`${base(v)}/${batch}/preview`, signal, json(csrf, 'POST'));
const exact = (p: Pick<Preview, 'previewId' | 'revision' | 'templateFingerprint'>) => `previews/${encodeURIComponent(p.previewId)}?revision=${encodeURIComponent(p.revision)}&fingerprint=${encodeURIComponent(p.templateFingerprint)}`;
export const page = (v: string, batch: string, p: Preview, offset: number, signal: AbortSignal) => request<Preview>(`${base(v)}/${batch}/${exact(p)}&offset=${offset}`, signal);
export const commit = (v: string, batch: string, p: Preview, csrf: string, key: string, signal: AbortSignal) => {
    const command: components['schemas']['ImportCommitCommand'] = { previewId: p.previewId, revision: Number(p.revision), templateFingerprint: p.templateFingerprint };
    return request<Committed>(`${base(v)}/${batch}/commit`, signal, json(csrf, 'POST', command, key));
};
export function mapping(t: ImportTemplate, language: AppLocale): ImportMappingTemplate {
    return { version: t.version, fingerprint: t.fingerprint, allowSourceColumnReuse: t.allowSourceColumnReuse,
        fields: t.fields.map(f => ({ key: f.key, label: label(f.key, language), required: f.requiredInput, writable: f.userWritable })) };
}
const labels: Record<string, [
    string,
    string
]> = {
    record_key: [sourceText("tr", "pairs.m_339fa786633f836c"), sourceText("en", "pairs.m_339fa786633f836c")], base_version: [sourceText("tr", "pairs.m_84e37cfff02104c4"), sourceText("en", "pairs.m_84e37cfff02104c4")], code: [sourceText("tr", "pairs.m_936851238bbad438"), sourceText("en", "pairs.m_936851238bbad438")], name: [sourceText("tr", "pairs.m_d9b80c085ae8fcb9"), sourceText("en", "pairs.m_d9b80c085ae8fcb9")],
    full_name: [sourceText("tr", "pairs.m_c252d6e14983afd3"), sourceText("en", "pairs.m_c252d6e14983afd3")], employee_no: [sourceText("tr", "pairs.m_e9b5c1a710bc9750"), sourceText("en", "pairs.m_e9b5c1a710bc9750")], position_ref: [sourceText("tr", "pairs.m_d98238c9ec0116a8"), sourceText("en", "pairs.m_d98238c9ec0116a8")],
    person_base_version: [sourceText("tr", "pairs.m_a87f5829f3ab5b35"), sourceText("en", "pairs.m_a87f5829f3ab5b35")], identity_policy_version: [sourceText("tr", "pairs.m_37e6b2152a281774"), sourceText("en", "pairs.m_37e6b2152a281774")],
    identity_purpose: [sourceText("tr", "pairs.m_a4e2ccd589e1a4c2"), sourceText("en", "pairs.m_a4e2ccd589e1a4c2")], position_policy_version: [sourceText("tr", "pairs.m_d58f2187c8fa6d3b"), sourceText("en", "pairs.m_d58f2187c8fa6d3b")],
    profile_base_version: [sourceText("tr", "pairs.m_3842f94b908b68e9"), sourceText("en", "pairs.m_3842f94b908b68e9")], profile_policy_version: [sourceText("tr", "pairs.m_f37051e9f7bf8812"), sourceText("en", "pairs.m_f37051e9f7bf8812")],
    assignment_status: [sourceText("tr", "pairs.m_0a0d99752eb81b8e"), sourceText("en", "pairs.m_0a0d99752eb81b8e")], assignment_policy_version: [sourceText("tr", "pairs.m_e7285c0d6de52fb5"), sourceText("en", "pairs.m_e7285c0d6de52fb5")],
    nationality: [sourceText("tr", "pairs.m_6555036af02cc82d"), sourceText("en", "pairs.m_6555036af02cc82d")], date_of_birth: [sourceText("tr", "pairs.m_95f7dac1f70de34f"), sourceText("en", "pairs.m_95f7dac1f70de34f")], passport_no: [sourceText("tr", "pairs.m_7b8e61edc3dab98b"), sourceText("en", "pairs.m_7b8e61edc3dab98b")], passport_expiry: [sourceText("tr", "pairs.m_7923c2ede7804e0d"), sourceText("en", "pairs.m_7923c2ede7804e0d")],
    seaman_book_no: [sourceText("tr", "pairs.m_4e6a99a84ff4105b"), sourceText("en", "pairs.m_4e6a99a84ff4105b")], seaman_book_expiry: [sourceText("tr", "pairs.m_df49c1d9d6b5ae01"), sourceText("en", "pairs.m_df49c1d9d6b5ae01")], company_start_date: [sourceText("tr", "pairs.m_2224bd6d0eeb2e9b"), sourceText("en", "pairs.m_2224bd6d0eeb2e9b")],
    sea_service_months: [sourceText("tr", "pairs.m_56b6e3f129ff293c"), sourceText("en", "pairs.m_56b6e3f129ff293c")], tanker_experience_months: [sourceText("tr", "pairs.m_58fe30c1b0a3e5c1"), sourceText("en", "pairs.m_58fe30c1b0a3e5c1")], vessel_type_experience: [sourceText("tr", "pairs.m_06991bb512988903"), sourceText("en", "pairs.m_06991bb512988903")],
    company_experience_months: [sourceText("tr", "pairs.m_1f6a401f5fae03ce"), sourceText("en", "pairs.m_1f6a401f5fae03ce")], emergency_contact: [sourceText("tr", "pairs.m_eba2738bb3410c50"), sourceText("en", "pairs.m_eba2738bb3410c50")], preferred_language: [sourceText("tr", "pairs.m_0ef8be49224b6023"), sourceText("en", "pairs.m_0ef8be49224b6023")], contact_phone: [sourceText("tr", "pairs.m_9591201e8a464c41"), sourceText("en", "pairs.m_9591201e8a464c41")], email: [sourceText("tr", "pairs.m_b2881dde054d43fd"), sourceText("en", "pairs.m_b2881dde054d43fd")],
    from: [sourceText("tr", "pairs.m_ace755a5fc39cbbe"), sourceText("en", "pairs.m_ace755a5fc39cbbe")], to: [sourceText("tr", "pairs.m_5058b6701ecf7ac8"), sourceText("en", "pairs.m_5058b6701ecf7ac8")], contract_end_date: [sourceText("tr", "pairs.m_f4b2c67e3a79f19d"), sourceText("en", "pairs.m_f4b2c67e3a79f19d")], watch_group: [sourceText("tr", "pairs.m_5054e17e5335521e"), sourceText("en", "pairs.m_5054e17e5335521e")],
    certificate_type: [sourceText("tr", "pairs.m_79290b09f2171a33"), sourceText("en", "pairs.m_79290b09f2171a33")], certificate_no: [sourceText("tr", "pairs.m_c7de1be23dbdbf03"), sourceText("en", "pairs.m_c7de1be23dbdbf03")], issuer: [sourceText("tr", "pairs.m_381062193b369f22"), sourceText("en", "pairs.m_381062193b369f22")],
    issuing_country: [sourceText("tr", "pairs.m_735dd2ded2b747ec"), sourceText("en", "pairs.m_735dd2ded2b747ec")], issue_date: [sourceText("tr", "pairs.m_e27857429af7d7c6"), sourceText("en", "pairs.m_e27857429af7d7c6")], expiry_date: [sourceText("tr", "pairs.m_ccb4b1b86cd4a34b"), sourceText("en", "pairs.m_ccb4b1b86cd4a34b")],
    endorsement_required: [sourceText("tr", "pairs.m_d4afa9c01d83970e"), sourceText("en", "pairs.m_d4afa9c01d83970e")], endorsement_no: [sourceText("tr", "pairs.m_61dfb3b9b62d054c"), sourceText("en", "pairs.m_61dfb3b9b62d054c")], endorsement_expiry: [sourceText("tr", "pairs.m_556bc5aaff44dfdb"), sourceText("en", "pairs.m_556bc5aaff44dfdb")],
    stcw_reference: [sourceText("tr", "pairs.m_28c0c47330f20e17"), sourceText("en", "pairs.m_28c0c47330f20e17")], tanker_relevance: [sourceText("tr", "pairs.m_c6b273b13f34a53a"), sourceText("en", "pairs.m_c6b273b13f34a53a")], vessel_type_scope: [sourceText("tr", "pairs.m_f4ad2ef263d5561c"), sourceText("en", "pairs.m_f4ad2ef263d5561c")],
    document_ref: [sourceText("tr", "pairs.m_2c2b41d42f32cade"), sourceText("en", "pairs.m_2c2b41d42f32cade")], renewal_lead_days: [sourceText("tr", "pairs.m_ed3fef3ed54fa051"), sourceText("en", "pairs.m_ed3fef3ed54fa051")], restriction: [sourceText("tr", "pairs.m_253055bf751ec143"), sourceText("en", "pairs.m_253055bf751ec143")], source_system: [sourceText("tr", "pairs.m_53c5178b208d62f6"), sourceText("en", "pairs.m_53c5178b208d62f6")], notes: [sourceText("tr", "pairs.m_0fb9c90ba8619153"), sourceText("en", "pairs.m_0fb9c90ba8619153")],
    parent_ref: [sourceText("tr", "pairs.m_afeb4c4e7bc8d1a2"), sourceText("en", "pairs.m_afeb4c4e7bc8d1a2")], level: [sourceText("tr", "pairs.m_ef20bef0dea17a4f"), sourceText("en", "pairs.m_ef20bef0dea17a4f")], system_family: [sourceText("tr", "pairs.m_84fb1e15e63aa348"), sourceText("en", "pairs.m_84fb1e15e63aa348")], equipment_type: [sourceText("tr", "pairs.m_878477d3725463a1"), sourceText("en", "pairs.m_878477d3725463a1")], criticality: [sourceText("tr", "pairs.m_d0fc74ec8343ca39"), sourceText("en", "pairs.m_d0fc74ec8343ca39")], maker: [sourceText("tr", "pairs.m_1740a7b3ca48db26"), sourceText("en", "pairs.m_1740a7b3ca48db26")], model: [sourceText("tr", "pairs.m_62352f83d423a51b"), sourceText("en", "pairs.m_62352f83d423a51b")], serial_number: [sourceText("tr", "pairs.m_de1ce634c1fdfc49"), sourceText("en", "pairs.m_de1ce634c1fdfc49")], location: [sourceText("tr", "pairs.m_1062fe03d49acbec"), sourceText("en", "pairs.m_1062fe03d49acbec")], commission_date: [sourceText("tr", "pairs.m_277d70874b980dc5"), sourceText("en", "pairs.m_277d70874b980dc5")],
    person_id: [sourceText("tr", "pairs.m_fa3c1557fea13910"), sourceText("en", "pairs.m_fa3c1557fea13910")], verification_status: [sourceText("tr", "pairs.m_a81255ccecd5600d"), sourceText("en", "pairs.m_a81255ccecd5600d")], verified_by: [sourceText("tr", "pairs.m_583c3fc8e4c7cca8"), sourceText("en", "pairs.m_583c3fc8e4c7cca8")], verified_at: [sourceText("tr", "pairs.m_4f3b0ae7f8447dca"), sourceText("en", "pairs.m_4f3b0ae7f8447dca")], alert_state: [sourceText("tr", "pairs.m_df5597d2cce70d9a"), sourceText("en", "pairs.m_df5597d2cce70d9a")], sync_state: [sourceText("tr", "pairs.m_b43c429fb6d28cd5"), sourceText("en", "pairs.m_b43c429fb6d28cd5")], status: [sourceText("tr", "pairs.m_2abd66dba746a7de"), sourceText("en", "pairs.m_2abd66dba746a7de")],
};
export const label = (field: string, language: AppLocale) => Object.hasOwn(labels, field) ? localizedTuple(labels[field], language, 1) : (localizedText(language, "pairs.m_50862c1d010eb4b9"));
const known = new Set(['required', 'invalid_format', 'duplicate', 'conflict', 'unresolved_reference', 'unsupported_value', 'domain_validation']);
export function results(p: Preview, t: ImportTemplate, contextKey: string): ImportValidationPage {
    if (p.templateVersion !== t.version || p.templateFingerprint !== t.fingerprint)
        throw new ApiError(409, 'version_conflict');
    return { ...p, contextKey, rows: p.rows.map(r => {
            if (r.outcome !== 'valid' && r.outcome !== 'rejected')
                throw new ApiError(502, 'invalid_contract');
            return { rowNumber: r.rowNumber, outcome: r.outcome, issues: r.issues.map(i => ({ fieldOrdinal: i.fieldOrdinal,
                    code: known.has(i.code) ? i.code : 'unknown' })) };
        }) };
}
export async function download(v: string, category: string, batch: string | null, p: Preview | null, signal: AbortSignal) {
    const path = batch && p ? `${base(v)}/${batch}/previews/${encodeURIComponent(p.previewId)}/rejects?revision=${encodeURIComponent(p.revision)}&fingerprint=${encodeURIComponent(p.templateFingerprint)}` : `${base(v)}/templates/${encodeURIComponent(category)}/download`;
    const response = await fetch(`/api/v1/${path}`, { signal, credentials: 'same-origin', cache: 'no-store' });
    if (!response.ok)
        throw new ApiError(response.status);
    const contentType = response.headers.get('Content-Type') ?? '';
    if (!(batch ? contentType.startsWith('text/csv') : contentType.startsWith('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')))
        throw new ApiError(502);
    return response.blob();
}
