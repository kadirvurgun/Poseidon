import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import type { PersonFileOwner } from './file-api'

export type CrewFileKind = PersonFileOwner['kind']
export type CrewFileVersion = { id: string; personId: string; kind: CrewFileKind; revision: number; expiresOn: string | null;
  policyVersion: string; purpose: string; sourceVersion: string; createdAt: string; state: string }
export type CrewFileManifest = { personId: string; policyVersion: string; purpose: string; kinds: CrewFileKind[]; canWrite: boolean;
  items: CrewFileVersion[]; hasMore: boolean; next: Partial<Record<CrewFileKind, number>> }
const kinds = ['Photo', 'Passport', 'SeamanBook', 'CertificateDocument']
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const token = (v: unknown): v is string => typeof v === 'string' && /^[A-Za-z0-9_.-]{1,80}$/.test(v)
const integer = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const date = (v: unknown) => v === null || typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v) && Number.isFinite(Date.parse(v)) && new Date(v).toISOString().slice(0, 10) === v
const bad = (): never => { throw new ApiError(502, 'invalid_person_file_contract') }
export async function crewFileVersions(vessel: string, assignment: string, person: string, signal: AbortSignal): Promise<CrewFileManifest> {
  if (![vessel, assignment, person].every(isGuid)) return bad()
  const v = await request<unknown>(`vessels/${vessel}/crew/${assignment}/personal-files`, signal)
  if (!object(v) || v.personId !== person || !token(v.policyVersion) || !token(v.purpose) || typeof v.canWrite !== 'boolean' ||
    typeof v.hasMore !== 'boolean' || !Array.isArray(v.kinds) || !v.kinds.length || v.kinds.some(k => !kinds.includes(k)) ||
    new Set(v.kinds).size !== v.kinds.length || !Array.isArray(v.items) || v.items.length > 100 || !object(v.next)) return bad()
  if (Object.entries(v.next).some(([k, n]) => !(v.kinds as string[]).includes(k) || !integer(n)) ||
    v.canWrite && v.kinds.some(k => !integer((v.next as Record<string, unknown>)[k]))) return bad()
  for (const row of v.items) {
    if (!object(row) || !isGuid(row.id) || row.personId !== person || typeof row.kind !== 'string' || !v.kinds.includes(row.kind) || !integer(row.revision) ||
      !date(row.expiresOn) || row.kind === 'Photo' && row.expiresOn !== null || row.policyVersion !== v.policyVersion || row.purpose !== v.purpose ||
      row.sourceVersion !== 'poseidon-v46.3-20261030' || typeof row.createdAt !== 'string' || !Number.isFinite(Date.parse(row.createdAt)) ||
      !['Pending', 'Receiving', 'CleanTransport', 'Abandoned'].includes(String(row.state))) return bad()
  }
  if (new Set(v.items.map(r => r.id)).size !== v.items.length || new Set(v.items.map(r => `${r.kind}:${r.revision}`)).size !== v.items.length) return bad()
  return v as CrewFileManifest
}
