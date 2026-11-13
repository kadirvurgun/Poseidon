import { afterEach, describe, expect, it, vi } from 'vitest'
import { loadProfileSession, profileContentMatches, saveProfileAttempt, type ProfileAttempt } from './profile-api'
import { profileContent, profileOwner, profileResponse, profileRow, profileSession, profileVessel } from './profile-recovery.test-fixtures'
const attempt = (): ProfileAttempt => ({ owner: profileOwner, vessel: profileVessel, route: 'vessels/'+profileVessel+'/regulatory-profile', method: 'PUT', key: crypto.randomUUID(), body: JSON.stringify({ expectedVersion: 12, content: profileContent() }), expectedVersion: 12, nextRevision: 2, unknown: true })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('PRO01/04 closed profile receipt and session wire sources, execution DEFERRED', () => {
  it('binds normalized text, exact decimals, construction pin and UTC timestamp at 100ns precision', () => {
    const content = profileContent(), pin = { id: crypto.randomUUID(), revision: 2, checksum: 'A'.repeat(64) }
    const raw = { ...content, flag: { state: 'Known', value: ' FICTIONAL flag ' }, gt: { state: 'Known', value: '0009007199254740993.0000' }, declaration: { state: 'Known', value: '   ' }, constructionRecord: pin, sourceObservedAt: { state: 'Known', value: '2026-11-13T03:00:00.1234567+03:00' } }
    const canonical = { ...raw, flag: { state: 'Known', value: 'FICTIONAL flag' }, gt: { state: 'Known', value: '9007199254740993' }, declaration: { state: 'Missing', value: null }, sourceObservedAt: { state: 'Known', value: '2026-11-13T00:00:00.1234567Z' } }
    expect(profileContentMatches(raw, canonical)).toBe(true)
    expect(profileContentMatches(raw, { ...canonical, gt: { state: 'Known', value: '9007199254740992' } })).toBe(false)
    expect(profileContentMatches(raw, { ...canonical, sourceObservedAt: { state: 'Known', value: '2026-11-13T00:00:00.1234568Z' } })).toBe(false)
    expect(profileContentMatches(raw, { ...canonical, flag: { state: 'Unknown', value: null } })).toBe(false)
    expect(profileContentMatches(raw, { ...canonical, constructionRecord: { ...pin, revision: 3 } })).toBe(false)
  })
  it('sends the exact original serialized bytes/key/PUT route and accepts its historical revision rather than a newer xmin', async () => {
    const value = attempt(), row = { ...profileRow(), revision: 2, version: 13, provenance: 'Missing' }, csrf = crypto.randomUUID()
    const fetch = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>().mockResolvedValue(profileResponse(row)); vi.stubGlobal('fetch', fetch)
    expect((await saveProfileAttempt(value, csrf, new AbortController().signal)).revision).toBe(2)
    expect(fetch.mock.calls[0][0]).toBe('/api/v1/'+value.route)
    expect(fetch.mock.calls[0][1]).toMatchObject({ credentials: 'same-origin', method: 'PUT', body: value.body, headers: { 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': value.key } })
  })
  it.each(['tenant', 'vessel', 'revision', 'content', 'provenance', 'construction'] as const)('rejects a successful receipt with altered %s while retaining the caller envelope', async field => {
    const value = attempt(), row = { ...profileRow(), revision: 2, version: 13, provenance: 'Missing' }
    if (field === 'tenant') row.tenantId = crypto.randomUUID()
    if (field === 'vessel') row.vessel.id = crypto.randomUUID()
    if (field === 'revision') row.revision = 3
    if (field === 'content') row.content.flag = { state: 'Unknown', value: null }
    if (field === 'provenance') row.provenance = 'DeclaredUnverified'
    if (field === 'construction') row.content.constructionRecord = { id: crypto.randomUUID(), revision: 1, checksum: 'A'.repeat(64) }
    const before = JSON.stringify(value); vi.stubGlobal('fetch', vi.fn(async () => profileResponse(row)))
    await expect(saveProfileAttempt(value, crypto.randomUUID(), new AbortController().signal)).rejects.toMatchObject({ status: 502 })
    expect(JSON.stringify(value)).toBe(before)
  })
  it.each([{ label: 'NUL', code: 0 }, { label: 'DEL', code: 127 }])('rejects bounded wire CSRF containing $label', async ({ code }) => {
    const user = profileSession(); user.csrfToken = crypto.randomUUID()+String.fromCharCode(code)+crypto.randomUUID()
    vi.stubGlobal('fetch', vi.fn(async () => profileResponse(user)))
    await expect(loadProfileSession(new AbortController().signal)).rejects.toMatchObject({ status: 502 })
  })
  it.each([{ authenticated: false }, { setupRequired: true }, { canRead: false }, { actorId: 'invalid' }, { tenantId: null }, { csrfToken: '' }])('fails closed on unreadable or unbound actual session %j', async patch => {
    vi.stubGlobal('fetch', vi.fn(async () => profileResponse({ ...profileSession(), ...patch })))
    await expect(loadProfileSession(new AbortController().signal)).rejects.toBeInstanceOf(Error)
  })
})
