import { afterEach, describe, expect, it, vi } from 'vitest'
import { currentRecord, loadSession, page, receiptMatches, save } from './control-evidence-api'
import { controlCommand, controlId, controlRow, controlSession, controlVessel, echoControl } from './control-evidence-recovery.test-fixtures'

const response = (value: unknown) => ({ ok: true, status: 200, json: async () => value }) as Response
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('CEO01/03 exact command receipt wire binding, authored execution DEFERRED', () => {
  it('accepts backend-null expansion and equivalent offset instants without losing 100ns distinctions', () => {
    const command = controlCommand(); command.content.observedAt = '2026-11-13T03:00:00.1234567+03:00'
    const row = echoControl(command); row.content = { ...row.content, observedAt: '2026-11-13T00:00:00.1234567Z', operation: null }
    expect(receiptMatches(row, command, undefined, 1)).toBe(true)
    expect(receiptMatches({ ...row, content: { ...row.content, observedAt: '2026-11-13T00:00:00.1234568Z' } }, command, undefined, 1)).toBe(false)
    expect(receiptMatches({ ...row, content: { ...row.content, critical: false } }, command, undefined, 1)).toBe(false)
  })
  it('compares complete immutable pin sets independent of database projection order, retaining target versions and checksum', () => {
    const command = controlCommand(); command.file = { id: crypto.randomUUID(), revision: 1, checksum: 'A'.repeat(64) }
    command.requirements = [1, 2].map(number => ({ requirementId: crypto.randomUUID(), requirementRevision: number, sourceId: crypto.randomUUID(), sourceRevision: 1, ruleId: null, ruleRevision: null }))
    command.links = [1, 2].map(number => ({ kind: 'Control' as const, id: crypto.randomUUID(), revision: number, expectedVersion: number+4 }))
    const row = echoControl(command, 'Evidence'); row.requirements = [...row.requirements].reverse(); row.links = [...row.links].reverse()
    expect(receiptMatches(row, command, undefined, 1)).toBe(true)
    expect(receiptMatches({ ...row, file: { ...row.file!, checksum: 'B'.repeat(64) } }, command, undefined, 1)).toBe(false)
    expect(receiptMatches({ ...row, links: row.links.map((pin, index) => index ? pin : { ...pin, expectedVersion: pin.expectedVersion+1 }) }, command, undefined, 1)).toBe(false)
    expect(receiptMatches({ ...row, requirements: row.requirements.map((pin, index) => index ? pin : { ...pin, sourceRevision: 2 }) }, command, undefined, 1)).toBe(false)
  })
  it.each(['content', 'file', 'revision', 'action'] as const)('rejects a structurally valid success with altered %s', async field => {
    const command = controlCommand(), row = echoControl(command)
    if (field === 'content') row.content = { ...row.content, provenance: 'Different fictional provenance' }
    if (field === 'file') { row.kind = 'Evidence'; row.file = { id: crypto.randomUUID(), revision: 1, checksum: 'A'.repeat(64) }; row.evidenceExists = true }
    if (field === 'revision') row.revision = 2
    vi.stubGlobal('fetch', vi.fn(async () => response(row)))
    await expect(save(controlVessel, row.kind, null, { body: JSON.stringify(command), key: crypto.randomUUID(), expectedRevision: 1,
      ...(field === 'action' ? { action: 'submit' as const } : {}) }, crypto.randomUUID(), new AbortController().signal)).rejects.toMatchObject({ status: 502 })
  })
  it('keeps historical expected revision/token independent of later live head data and sends unchanged serialized bytes', async () => {
    const command = { ...controlCommand(), expectedVersion: 5, reasonCode: 'AUTHORING_CORRECTION' as const }, row = echoControl(command, 'Control', 2)
    const body = JSON.stringify(command), key = crypto.randomUUID(), csrf = crypto.randomUUID()
    const fetch = vi.fn(async (_url: string, _init?: RequestInit) => response(row)); vi.stubGlobal('fetch', fetch)
    expect((await save(controlVessel, 'Control', controlId, { body, key, expectedRevision: 2 }, csrf, new AbortController().signal)).revision).toBe(2)
    const [url, init] = fetch.mock.calls[0]
    expect(url.includes(`/controls/${controlId}?`) && init?.method === 'PUT' && init.body === body).toBe(true)
    expect(init?.headers).toMatchObject({ 'Idempotency-Key': key, 'X-CSRF-TOKEN': csrf })
  })
  it('rejects another resource in an actual requested-ID versions page', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => response({ items: [{ ...controlRow(), id: crypto.randomUUID() }], canWrite: true, offset: 0, hasMore: false })))
    await expect(page(controlVessel, 'Control', 0, new AbortController().signal, controlId)).rejects.toMatchObject({ status: 502 })
  })
  it('does not promote an older visible version/current-token pair when the current-list route hides the resource', async () => {
    const historical = { ...controlRow(), version: 9 }
    vi.stubGlobal('fetch', vi.fn(async (url: string) => response({ items: url.includes('/versions?') ? [historical] : [], canWrite: true, offset: 0, hasMore: false })))
    expect((await page(controlVessel, 'Control', 0, new AbortController().signal, controlId)).items[0].version).toBe(9)
    await expect(currentRecord(controlVessel, 'Control', controlId, new AbortController().signal)).rejects.toMatchObject({ status: 502 })
  })
  it('requires real readable owner/session data and bounded fresh CSRF rather than a UI role hint', async () => {
    const user = controlSession(); vi.stubGlobal('fetch', vi.fn(async () => response(user)))
    expect(await loadSession(new AbortController().signal)).toMatchObject({ owner: `${user.tenantId}/${user.actorId}`, csrf: user.csrfToken })
    for (const patch of [{ authenticated: false }, { setupRequired: true }, { canRead: false }, { actorId: user.actorId+'\n' }, { tenantId: null }, { csrfToken: '' }]) {
      vi.stubGlobal('fetch', vi.fn(async () => response({ ...user, ...patch })))
      await expect(loadSession(new AbortController().signal)).rejects.toBeInstanceOf(Error)
    }
  })
})
