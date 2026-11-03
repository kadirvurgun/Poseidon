import { afterEach, describe, expect, it, vi } from 'vitest'
import { decodeDocument, documentRevision, documents, saveDocument } from './document-api'

// Synthetic public wire projections of the real seven-field DocumentDraftDto.
// These transport tests grant no verification, safety authority or file ownership.
const vessel = '10000000-0000-4000-8000-000000000001'
const id = '10000000-0000-4000-8000-000000000002'
const key = '10000000-0000-4000-8000-000000000003'
const wire = () => ({ id, vesselId: vessel, revision: 1, version: 7, title: null,
  status: 'DraftUnverified', provenance: 'DeclaredUnverified' })
const response = (value: unknown) => ({ ok: true, status: 200, json: async () => value }) as Response
const signal = () => new AbortController().signal
const invalid = { status: 502, code: 'invalid_document_contract' }
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('closed current document wire contract', () => {
  it('accepts the real seven public fields with nullable title and preserves unverified provenance', () => {
    const row = wire()
    expect(decodeDocument(row, vessel)).toBe(row)
    expect(decodeDocument({ ...row, title: 'FICTIONAL draft', provenance: 'SyntheticUnverified' }, vessel))
      .toMatchObject({ title: 'FICTIONAL draft', status: 'DraftUnverified', provenance: 'SyntheticUnverified' })
  })
  it('decodes the actual list and saved revision shapes without an invented eighth property', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(response({ items: [wire()], canWrite: true, offset: 0, hasMore: false }))
      .mockResolvedValueOnce(response(wire())).mockResolvedValueOnce(response(wire()))
    vi.stubGlobal('fetch', fetch)
    expect((await documents(vessel, 0, signal())).items[0]).toEqual(wire())
    const saved = await saveDocument(vessel, '', key, 'synthetic-csrf', signal())
    expect(saved.title).toBeNull(); expect(saved.status).toBe('DraftUnverified')
    const [url, init] = fetch.mock.calls[1]
    expect(url).toBe(`/api/v1/vessels/${vessel}/documents`)
    expect(init.body).toBe('{"title":null}')
    expect(init.headers).toMatchObject({ 'Idempotency-Key': key, 'X-CSRF-TOKEN': 'synthetic-csrf' })
    expect(await documentRevision(vessel, id, 1, signal())).toEqual(wire())
  })
  it('rejects private or unknown extras, including an eighth property and equal-count substitution', () => {
    for (const extra of [{ privateSnapshot: 'FICTIONAL withheld' }, { verified: true }, { arbitrary: null }])
      expect(() => decodeDocument({ ...wire(), ...extra }, vessel)).toThrow(expect.objectContaining(invalid))
    const withoutTitle: Record<string, unknown> = { ...wire() }; delete withoutTitle.title
    expect(() => decodeDocument(withoutTitle, vessel)).toThrow(expect.objectContaining(invalid))
    expect(() => decodeDocument({ ...withoutTitle, privateTitle: null }, vessel)).toThrow(expect.objectContaining(invalid))
  })
  it('rejects wrong scope, forged approval/provenance and invalid revision or concurrency tokens', () => {
    for (const change of [
      { vesselId: id }, { id: id + '\n' }, { revision: 0 }, { revision: '1' }, { revision: Number.MAX_SAFE_INTEGER + 1 },
      { version: 0 }, { version: 1.5 }, { version: 4294967296 }, { version: '7' }, { title: 'x'.repeat(201) },
      { status: 'Verified' }, { provenance: 'Verified' }, { provenance: ['DeclaredUnverified'] },
    ]) expect(() => decodeDocument({ ...wire(), ...change }, vessel)).toThrow(expect.objectContaining(invalid))
    expect(() => decodeDocument({ ...wire(), vesselId: 'not-a-vessel' }, 'not-a-vessel')).toThrow(expect.objectContaining(invalid))
  })
  it('does not accept a different immutable document revision returned by the server', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response({ ...wire(), revision: 2 }))
      .mockResolvedValueOnce(response({ ...wire(), id: key })))
    await expect(documentRevision(vessel, id, 1, signal())).rejects.toMatchObject(invalid)
    await expect(documentRevision(vessel, id, 1, signal())).rejects.toMatchObject(invalid)
  })
})
