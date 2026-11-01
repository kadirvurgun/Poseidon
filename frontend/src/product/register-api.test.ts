import { afterEach, describe, expect, it, vi } from 'vitest'
import { blankContent, decodeDraft, decodeRevision, historyDraft, listDrafts, readDraft, saveDraft, validContent } from './register-api'

const id = '10000000-0000-0000-0000-000000000001'
const sourceId = '10000000-0000-0000-0000-000000000002'
const revision = () => ({ id, kind: 'Source', revision: 1, savedAt: '2026-11-01T00:00:00Z', provenance: 'DeclaredUnverified', hash: 'A'.repeat(64), content: blankContent(), sourceId: null, sourceRevision: null, lifecycle: 'DRAFT' })
const draft = () => ({ version: 19, canEdit: true, current: revision() })
const signal = () => new AbortController().signal
const page = () => ({ items: [draft()], offset: 0, limit: 25, hasMore: false })
function respond(body: unknown) { vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, status: 200, json: async () => body }))) }
afterEach(() => vi.unstubAllGlobals())

describe('REG-BOUNDARY-TLS-01 exact serialized register boundary', () => {
  it('preserves partial states, provenance and exact declared strings without normalization', () => {
    const d = draft()
    d.current.content.title = { state: 'Known', value: '  9007199254740993.000  ' }
    d.current.content.authority = { state: 'Unknown', value: null }
    expect(decodeDraft(d, 'sources')).toEqual(d)
    expect(decodeDraft({ ...d, current: { ...d.current, provenance: 'SyntheticUnverified' } }, 'sources').current.provenance).toBe('SyntheticUnverified')
    expect(decodeRevision({ ...revision(), kind: 'Requirement' }, 'requirements').sourceId).toBeNull()
    expect(decodeRevision({ ...revision(), kind: 'Requirement', sourceId, sourceRevision: 1 }, 'requirements').sourceRevision).toBe(1)
  })
  it.each(['draft', 'revision', 'content', 'field'])('rejects extra keys at %s level', level => {
    const d = draft()
    const target = { draft: d, revision: d.current, content: d.current.content, field: d.current.content.title }[level]
    Object.assign(target!, { unexpected: false })
    expect(() => decodeDraft(d, 'sources')).toThrow('Request failed (502)')
  })
  it.each(['version', 'canEdit', 'current'])('rejects missing required draft member %s', key => {
    const d = draft(); Reflect.deleteProperty(d, key)
    expect(() => decodeDraft(d, 'sources')).toThrow()
  })
  it.each(Object.keys(revision()))('rejects missing serialized revision member %s including explicit nullable links', key => {
    const r = revision(); Reflect.deleteProperty(r, key)
    expect(() => decodeRevision(r, 'sources')).toThrow()
  })
  it.each(Object.keys(blankContent()))('rejects missing declared field %s', key => {
    const d = draft(); Reflect.deleteProperty(d.current.content, key)
    expect(() => decodeDraft(d, 'sources')).toThrow()
  })
  it.each([
    null, [], {}, { state: 'Known' }, { value: null }, { state: ['Missing'], value: null },
    { state: 'Ready', value: null }, { state: 'Unknown', value: false }, { state: 'Missing', value: '' },
    { state: 'Known', value: '' }, { state: 'Known', value: ' '.repeat(2) },
    { state: 'Known', value: 'a'.repeat(201) }, { state: 'Known', value: 'bad\u0085text' },
  ])('rejects malformed declared fields %#', field => {
    expect(validContent({ ...blankContent(), title: field })).toBe(false)
  })
  const badTimes = ['2026-02-30T12:00:00Z', '2025-02-29T12:00:00Z', '1900-02-29T12:00:00Z', '0000-01-01T00:00:00Z', '2026-13-01T00:00:00Z', '2026-04-31T00:00:00Z', '2026-01-01T24:00:00Z', '2026-01-01T12:60:00Z', '2026-01-01T12:00:60Z', '2026-01-01T12:00:00', '2026-01-01T12:00:00+14:01', '2026-01-01T12:00:00+15:00', '2026-01-01T12:00:00+01:60', '2026-01-01T12:00:00.12345678Z', '2026-01-01T12:00:00Z\n', '0001-01-01T00:00:00+00:01', '9999-12-31T23:59:59-00:01']
  it.each(badTimes)('rejects invalid timestamp components %s in both saved and observed metadata', value => {
    expect(() => decodeRevision({ ...revision(), savedAt: value }, 'sources')).toThrow()
    expect(validContent({ ...blankContent(), observedAt: { state: 'Known', value } })).toBe(false)
  })
  it.each(['2026-02-30', '2025-02-29', '1900-02-29', '0000-01-01', '2026-13-01', '2026-04-31', '2026-01-00'])('rejects invalid declared calendar %s', value => {
    expect(validContent({ ...blankContent(), effectiveDate: { state: 'Known', value } })).toBe(false)
  })
  it.each(['2000-02-29T23:59:59.1234567+14:00', '2024-02-29T00:00:00-14:00', '0001-01-01T00:00:00Z', '9999-12-31T23:59:59.9999999Z'])('preserves valid timestamp precision and offsets %s', value => {
    expect(decodeRevision({ ...revision(), savedAt: value }, 'sources').savedAt).toBe(value)
  })
  it.each([
    { revision: 0 }, { revision: Number.MAX_SAFE_INTEGER + 1 }, { revision: 1.5 }, { id: '00000000-0000-0000-0000-000000000000' },
    { id: id + '\n' }, { kind: 'Requirement' }, { lifecycle: 'Effective' }, { provenance: ['DeclaredUnverified'] }, { hash: 'A'.repeat(64) + '\n' },
    { sourceId, sourceRevision: null }, { sourceId: null, sourceRevision: 1 }, { sourceId, sourceRevision: 1 },
  ])('rejects invalid revision metadata %#', changes => expect(() => decodeRevision({ ...revision(), ...changes }, 'sources')).toThrow())
  it.each([
    { sourceId, sourceRevision: 0 }, { sourceId, sourceRevision: Number.MAX_SAFE_INTEGER + 1 },
    { sourceId: id, sourceRevision: 1 }, { sourceId: null, sourceRevision: 1 },
    { sourceId, sourceRevision: null },
  ])('rejects inconsistent requirement source links %#', changes => expect(() => decodeRevision({ ...revision(), kind: 'Requirement', ...changes }, 'requirements')).toThrow())
  it.each([0, -1, 1.1, 4294967296, Number.MAX_SAFE_INTEGER + 1, '19', null])('rejects invalid xmin %s', version => expect(() => decodeDraft({ ...draft(), version }, 'sources')).toThrow())
  it.each([
    { unexpected: true }, { offset: 25 }, { offset: 100001 }, { offset: -1 }, { offset: 1.5 }, { offset: Number.MAX_SAFE_INTEGER + 1 },
    { limit: 26 }, { limit: '25' }, { hasMore: true }, { hasMore: 'false' }, { items: new Array(26).fill(draft()) },
  ])('rejects malformed/mismatched list and history pages %#', async changes => {
    respond({ ...page(), ...changes }); await expect(listDrafts('sources', '', 0, signal())).rejects.toThrow()
    respond({ ...page(), items: [revision()], ...changes }); await expect(historyDraft('sources', id, 0, signal())).rejects.toThrow()
  })
  it('accepts the exact requested bounded page, including an empty terminal page', async () => {
    respond({ items: [], offset: 100000, limit: 25, hasMore: false })
    expect((await listDrafts('sources', '', 100000, signal())).offset).toBe(100000)
    respond({ ...page(), offset: 25 }); await expect(listDrafts('sources', '', 0, signal())).rejects.toThrow()
  })
  it('rejects malformed nested page entries, duplicate history revisions and another resource', async () => {
    respond({ ...page(), items: [{ ...draft(), extra: true }] }); await expect(listDrafts('sources', '', 0, signal())).rejects.toThrow()
    respond({ ...page(), items: [{ ...revision(), extra: true }] }); await expect(historyDraft('sources', id, 0, signal())).rejects.toThrow()
    respond({ ...page(), items: [revision(), revision()] }); await expect(historyDraft('sources', id, 0, signal())).rejects.toThrow()
    respond({ ...draft(), current: { ...revision(), id: sourceId } })
    await expect(readDraft('sources', id, signal())).rejects.toThrow()
    await expect(saveDraft('sources', id, '{}', 'synthetic', 'synthetic', signal())).rejects.toThrow()
  })
})
