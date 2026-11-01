import { ApiError, request } from './api'
import { isGuid } from './identifiers'

export type FileRecord = { id: string; vesselId: string; name: string; state: 'Pending' | 'Receiving' | 'CleanTransport' | 'Abandoned' | 'SyntheticUnverified'; length: number; checksum: string | null; revision: number | null; version: number }
export type FilePage = { items: FileRecord[]; offset: number; hasMore: boolean; canWrite: boolean; available: boolean; maxBytes: number }
const bad = (): never => { throw new ApiError(502, 'invalid_file_contract') }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const integer = (v: unknown, min = 0): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const hash = (v: unknown) => typeof v === 'string' && v.length === 64 && /^[A-F0-9]+$/.test(v)
export function decodeFile(v: unknown, vessel: string): FileRecord {
  if (!object(v) || Object.keys(v).length !== 8 || !isGuid(v.id) || v.vesselId !== vessel || typeof v.name !== 'string' || v.name.length > 120 ||
    !/^[A-Za-z0-9_.-]+\.png$(?![\s\S])/i.test(v.name) || v.name.includes('..') ||
    typeof v.state !== 'string' || !['Pending', 'Receiving', 'CleanTransport', 'Abandoned', 'SyntheticUnverified'].includes(v.state) ||
    !integer(v.length) || !integer(v.version, 1) || v.version > 4294967295 ||
    (v.state === 'CleanTransport' ? !hash(v.checksum) || v.revision !== 1 || v.length === 0 : v.checksum !== null || v.revision !== null)) return bad()
  return v as FileRecord
}
export async function filePage(vessel: string, offset: number, signal: AbortSignal): Promise<FilePage> {
  if (!isGuid(vessel) || !integer(offset)) return bad()
  const v = await request<unknown>(`vessels/${vessel}/files?offset=${offset}`, signal)
  if (!object(v) || Object.keys(v).length !== 6 || !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset ||
    typeof v.hasMore !== 'boolean' || v.hasMore && v.items.length !== 25 || typeof v.canWrite !== 'boolean' || typeof v.available !== 'boolean' || !integer(v.maxBytes) || v.available && v.maxBytes === 0) return bad()
  const items = v.items.map(x => decodeFile(x, vessel)); if (new Set(items.map(x => x.id)).size !== items.length) return bad()
  return { items, offset, hasMore: v.hasMore, canWrite: v.canWrite, available: v.available, maxBytes: v.maxBytes }
}
export const fileDetail = async (vessel: string, id: string, signal: AbortSignal) => {
  if (!isGuid(vessel) || !isGuid(id)) return bad()
  const row = decodeFile(await request<unknown>(`vessels/${vessel}/files/${id}`, signal), vessel)
  if (row.id !== id) return bad(); return row
}
export type FileAttempt = { key: string; file: File; checksum: string }
export async function stageFile(vessel: string, attempt: FileAttempt, csrf: string, signal: AbortSignal) {
  return decodeFile(await request<unknown>(`vessels/${vessel}/files`, signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify({ name: attempt.file.name, mime: attempt.file.type, length: attempt.file.size, checksum: attempt.checksum }) }), vessel)
}
export function uploadFile(vessel: string, id: string, file: File, csrf: string, signal: AbortSignal, progress: (n: number) => void): Promise<FileRecord> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    const abort = () => xhr.abort()
    const finish = () => signal.removeEventListener('abort', abort)
    xhr.open('PUT', `/api/v1/vessels/${vessel}/files/${id}/bytes`)
    xhr.withCredentials = true; xhr.setRequestHeader('Content-Type', 'image/png'); xhr.setRequestHeader('X-CSRF-TOKEN', csrf)
    xhr.upload.onprogress = e => { if (e.lengthComputable) progress(Math.round(e.loaded * 100 / e.total)) }
    xhr.onerror = () => { finish(); reject(new ApiError(0, 'file_attempt_unknown')) }
    xhr.onabort = () => { finish(); reject(new ApiError(0, 'file_attempt_unknown')) }
    xhr.onload = () => { finish(); if (xhr.status !== 200) { reject(new ApiError(xhr.status, 'file_attempt_unknown')); return }; try { const row = decodeFile(JSON.parse(xhr.responseText), vessel); if (row.id !== id || row.state !== 'CleanTransport') return reject(new ApiError(502)); resolve(row) } catch { reject(new ApiError(502)) } }
    signal.addEventListener('abort', abort, { once: true }); if (signal.aborted) { finish(); reject(new ApiError(0)); return }
    xhr.send(file)
  })
}
export async function downloadFile(vessel: string, row: FileRecord, csrf: string, signal: AbortSignal): Promise<Blob> {
  const v = await request<unknown>(`vessels/${vessel}/files/${row.id}/access`, signal, { method: 'POST', headers: { 'X-CSRF-TOKEN': csrf } })
  if (!object(v) || Object.keys(v).length !== 3 || typeof v.token !== 'string' || v.token.length > 2048 || typeof v.expiresAt !== 'string' || !Number.isFinite(Date.parse(v.expiresAt)) || v.revision !== row.revision) return bad()
  const response = await fetch(`/api/v1/vessels/${vessel}/files/${row.id}/bytes`, { credentials: 'same-origin', signal, headers: { 'X-File-Capability': v.token } })
  if (!response.ok) throw new ApiError(response.status)
  if (response.headers.get('content-type') !== 'image/png') return bad()
  // Bound delivery by committed length, including chunked/malformed responses.
  const reader = response.body?.getReader(); if (!reader) return bad()
  const chunks: Uint8Array<ArrayBuffer>[] = []; let total = 0
  try { while (true) { const result = await reader.read(); if (result.done) break; total += result.value.length; if (total > row.length) return bad(); chunks.push(new Uint8Array(result.value)) } }
  finally { await reader.cancel() }
  if (total !== row.length) return bad()
  const blob = new Blob(chunks, { type: 'image/png' })
  const checksum = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', await blob.arrayBuffer())), x => x.toString(16).padStart(2, '0')).join('').toUpperCase()
  if (checksum !== row.checksum) return bad()
  return blob
}
