import { webcrypto } from 'node:crypto'
import { beforeAll, describe, expect, it } from 'vitest'
import { OfflineCipherError, openOfflineRecord, sealOfflineRecord } from './offline-cipher'
import type { OfflineCipherContext } from './offline-cipher'

// Real built-in WebCrypto, not a mocked encrypt/decrypt function. IndexedDB,
// enrollment/restart/logout and actual-host journeys remain separate coverage.
const provider = webcrypto as unknown as Crypto
const context: OfflineCipherContext = { tenantId: 'synthetic-tenant', actorId: 'synthetic-actor',
  vesselId: 'synthetic-vessel', deviceId: 'synthetic-device', sessionBinding: 'synthetic-noncredential-session-context',
  policyVersion: 'synthetic-policy-1', commandId: 'synthetic-command-uuid' }
const limits = { plaintextBytes: 4096, contextBytes: 2048 }
const data = new TextEncoder().encode('Synthetic protected draft: İ, ğ, ⚓ and decimal 000.1000; Unknown stays Unknown.')
let key: CryptoKey
beforeAll(async () => { key = await provider.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']) })

describe('bound offline capture encryption primitive (authored; execution deferred)', () => {
  it('roundtrips exact declared UTF-8 data under a nonextractable key without storing plaintext context', async () => {
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    expect(Array.from(await openOfflineRecord(provider, key, context, sealed, limits))).toEqual(Array.from(data))
    expect(sealed.iv.byteLength).toBe(12)
    expect(sealed.ciphertext.byteLength).toBe(data.byteLength + 16)
    expect(sealed.contextHash).toMatch(/^[a-f0-9]{64}$/)
    expect(sealed).not.toHaveProperty('actorId')
    expect(sealed).not.toHaveProperty('plaintext')
    expect(new TextDecoder().decode(sealed.ciphertext)).not.toContain('Synthetic protected draft')
  })

  it('uses a fresh nonce and ciphertext even when updating the same command with the same data', async () => {
    const first = await sealOfflineRecord(provider, key, context, data, limits)
    const second = await sealOfflineRecord(provider, key, context, data, limits)
    expect(second.iv).not.toEqual(first.iv)
    expect(Array.from(new Uint8Array(second.ciphertext))).not.toEqual(Array.from(new Uint8Array(first.ciphertext)))
    expect(second.contextHash).toBe(first.contextHash)
  })

  it.each(['tenantId', 'actorId', 'vesselId', 'deviceId', 'sessionBinding', 'policyVersion', 'commandId'] as const)('cannot open the record under a changed %s binding', async field => {
      const sealed = await sealOfflineRecord(provider, key, context, data, limits)
      await expect(openOfflineRecord(provider, key, { ...context, [field]: 'other-context' }, sealed, limits))
        .rejects.toMatchObject({ code: 'context_mismatch' })
    })

  it('does not confuse ambiguous delimiter-joined identities', async () => {
    const first = { ...context, tenantId: 'a/b', actorId: 'c' }
    const second = { ...context, tenantId: 'a', actorId: 'b/c' }
    const sealed = await sealOfflineRecord(provider, key, first, data, limits)
    await expect(openOfflineRecord(provider, key, second, sealed, limits)).rejects.toMatchObject({ code: 'context_mismatch' })
  })

  it('rejects ciphertext modification instead of returning a partially decoded draft', async () => {
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    const altered = sealed.ciphertext.slice(0)
    new Uint8Array(altered)[0] ^= 1
    await expect(openOfflineRecord(provider, key, context, { ...sealed, ciphertext: altered }, limits))
      .rejects.toMatchObject({ code: 'authentication_failed' })
  })

  it('rejects nonce modification even when the stored context hash remains unchanged', async () => {
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    const iv = sealed.iv.slice(); iv[0] ^= 1
    await expect(openOfflineRecord(provider, key, context, { ...sealed, iv }, limits))
      .rejects.toMatchObject({ code: 'authentication_failed' })
  })

  it('cannot decrypt with another valid device key', async () => {
    const other = await provider.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    await expect(openOfflineRecord(provider, other, context, sealed, limits)).rejects.toMatchObject({ code: 'authentication_failed' })
  })

  it('does not let a changed public context hash bypass authenticated additional data', async () => {
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    const otherContext = { ...context, actorId: 'other-actor' }
    const other = await sealOfflineRecord(provider, key, otherContext, data, limits)
    await expect(openOfflineRecord(provider, key, otherContext, { ...sealed, contextHash: other.contextHash }, limits))
      .rejects.toMatchObject({ code: 'authentication_failed' })
  })

  it('refuses an extractable key instead of silently downgrading custody policy', async () => {
    const extractable = await provider.subtle.generateKey({ name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt'])
    await expect(sealOfflineRecord(provider, extractable, context, data, limits)).rejects.toMatchObject({ code: 'invalid_key' })
  })

  it('requires the declared AES-256-GCM capability instead of accepting another key size', async () => {
    const smaller = await provider.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt', 'decrypt'])
    await expect(sealOfflineRecord(provider, smaller, context, data, limits)).rejects.toMatchObject({ code: 'invalid_key' })
  })

  it('bounds byte length rather than truncating Unicode text or decimal declarations', async () => {
    const bytes = new TextEncoder().encode('⚓')
    expect(bytes.byteLength).toBe(3)
    await expect(sealOfflineRecord(provider, key, context, bytes, { ...limits, plaintextBytes: 2 }))
      .rejects.toMatchObject({ code: 'payload_too_large' })
    const sealed = await sealOfflineRecord(provider, key, context, bytes, { ...limits, plaintextBytes: 3 })
    expect(new TextDecoder().decode(await openOfflineRecord(provider, key, context, sealed, limits))).toBe('⚓')
  })

  it('enforces the current read quota before decrypting an older larger capture', async () => {
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    await expect(openOfflineRecord(provider, key, context, sealed, { ...limits, plaintextBytes: 1 }))
      .rejects.toMatchObject({ code: 'payload_too_large' })
  })

  it('rejects missing/oversized binding metadata without echoing its private values', async () => {
    await expect(sealOfflineRecord(provider, key, { ...context, actorId: ' ' }, data, limits))
      .rejects.toMatchObject({ code: 'invalid_context' })
    await expect(sealOfflineRecord(provider, key, context, data, { ...limits, contextBytes: 1 }))
      .rejects.toMatchObject({ code: 'context_too_large' })
    expect(new OfflineCipherError('context_mismatch').message).toBe('context_mismatch')
  })

  it('refuses malformed envelopes and disabled crypto without deleting captured records', async () => {
    const sealed = await sealOfflineRecord(provider, key, context, data, limits)
    await expect(openOfflineRecord(provider, key, context, { ...sealed, iv: new Uint8Array(11) }, limits))
      .rejects.toMatchObject({ code: 'invalid_envelope' })
    await expect(sealOfflineRecord({} as Crypto, key, context, data, limits)).rejects.toMatchObject({ code: 'crypto_unavailable' })
    expect(Array.from(await openOfflineRecord(provider, key, context, sealed, limits))).toEqual(Array.from(data))
  })
})
