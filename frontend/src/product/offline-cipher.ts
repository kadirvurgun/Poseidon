// Encryption primitive for the real durable offline store. The caller must first
// enforce the current versioned field/command/device/session policy and quotas.
// sessionBinding is a noncredential context identifier, NEVER an SSO/session token.
export type OfflineCipherContext = Readonly<{
  tenantId: string
  actorId: string
  vesselId: string
  deviceId: string
  sessionBinding: string
  policyVersion: string
  commandId: string
}>
export type OfflineCipherLimits = Readonly<{ plaintextBytes: number; contextBytes: number }>
export type OfflineSealedRecord = Readonly<{
  version: 1
  contextHash: string
  iv: Uint8Array<ArrayBuffer>
  ciphertext: ArrayBuffer
}>
export type OfflineCipherErrorCode = 'invalid_context' | 'invalid_limits' | 'context_too_large' |
  'payload_too_large' | 'invalid_key' | 'crypto_unavailable' | 'invalid_envelope' |
  'context_mismatch' | 'authentication_failed'

export class OfflineCipherError extends Error {
  readonly code: OfflineCipherErrorCode
  constructor(code: OfflineCipherErrorCode) { super(code); this.name = 'OfflineCipherError'; this.code = code }
}

function limits(input: OfflineCipherLimits) {
  if (!input || !Number.isSafeInteger(input.plaintextBytes) || input.plaintextBytes <= 0 ||
    input.plaintextBytes > Number.MAX_SAFE_INTEGER - 16 ||
    !Number.isSafeInteger(input.contextBytes) || input.contextBytes <= 0)
    throw new OfflineCipherError('invalid_limits')
}

function keyIsUsable(key: CryptoKey) {
  return key && key.type === 'secret' && key.extractable === false && key.algorithm.name === 'AES-GCM' &&
    (key.algorithm as AesKeyAlgorithm).length === 256 && key.usages.includes('encrypt') && key.usages.includes('decrypt')
}

function additionalData(context: OfflineCipherContext, maximum: number): Uint8Array<ArrayBuffer> {
  if (!context) throw new OfflineCipherError('invalid_context')
  const parts = [context.tenantId, context.actorId, context.vesselId, context.deviceId,
    context.sessionBinding, context.policyVersion, context.commandId]
  if (parts.some(value => typeof value !== 'string' || !value.trim() || /\p{Cc}/u.test(value)))
    throw new OfflineCipherError('invalid_context')
  // Ordered JSON prevents ambiguous delimiter joins; no silent normalization.
  const encoded = new TextEncoder().encode(JSON.stringify(['poseidon.offline.capture.v1', ...parts]))
  if (encoded.byteLength > maximum) throw new OfflineCipherError('context_too_large')
  return encoded
}

function cryptoIsAvailable(provider: Crypto) {
  if (!provider?.subtle || typeof provider.getRandomValues !== 'function')
    throw new OfflineCipherError('crypto_unavailable')
}

// Native brand check works across IndexedDB/worker realms and rejects shared or
// merely lookalike buffers. instanceof can reject a genuine foreign-realm buffer.
function bufferLength(value: unknown): number | null {
  try {
    return Object.getOwnPropertyDescriptor(ArrayBuffer.prototype, 'byteLength')!.get!.call(value) as number
  } catch { return null }
}

async function digest(provider: Crypto, data: Uint8Array<ArrayBuffer>) {
  const bytes = new Uint8Array(await provider.subtle.digest('SHA-256', data))
  return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('')
}

export async function sealOfflineRecord(provider: Crypto, key: CryptoKey, context: OfflineCipherContext,
  plaintext: Uint8Array<ArrayBuffer>, policyLimits: OfflineCipherLimits): Promise<OfflineSealedRecord> {
  limits(policyLimits); cryptoIsAvailable(provider)
  if (!keyIsUsable(key)) throw new OfflineCipherError('invalid_key')
  if (!plaintext || plaintext.byteLength > policyLimits.plaintextBytes) throw new OfflineCipherError('payload_too_large')
  const aad = additionalData(context, policyLimits.contextBytes)
  // New random 96-bit IV for every encryption, including an update of one UUID.
  // Invocation/key-rotation limits are the actual device key lifecycle's concern.
  try {
    const iv = provider.getRandomValues(new Uint8Array(12))
    const ciphertext = await provider.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: aad, tagLength: 128 }, key, plaintext)
    return { version: 1, contextHash: await digest(provider, aad), iv, ciphertext }
  } catch { throw new OfflineCipherError('crypto_unavailable') }
}

export async function openOfflineRecord(provider: Crypto, key: CryptoKey, context: OfflineCipherContext,
  sealed: OfflineSealedRecord, policyLimits: OfflineCipherLimits): Promise<Uint8Array<ArrayBuffer>> {
  limits(policyLimits); cryptoIsAvailable(provider)
  if (!keyIsUsable(key)) throw new OfflineCipherError('invalid_key')
  const ciphertextLength = sealed ? bufferLength(sealed.ciphertext) : null
  if (!sealed || sealed.version !== 1 || typeof sealed.contextHash !== 'string' || !/^[a-f0-9]{64}$/.test(sealed.contextHash) ||
    !ArrayBuffer.isView(sealed.iv) || sealed.iv.byteLength !== 12 || bufferLength(sealed.iv.buffer) === null ||
    ciphertextLength === null || ciphertextLength < 16)
    throw new OfflineCipherError('invalid_envelope')
  if (ciphertextLength - 16 > policyLimits.plaintextBytes) throw new OfflineCipherError('payload_too_large')
  const aad = additionalData(context, policyLimits.contextBytes)
  let expectedHash: string
  try { expectedHash = await digest(provider, aad) }
  catch { throw new OfflineCipherError('crypto_unavailable') }
  if (expectedHash !== sealed.contextHash) throw new OfflineCipherError('context_mismatch')
  try {
    return new Uint8Array(await provider.subtle.decrypt({ name: 'AES-GCM', iv: sealed.iv, additionalData: aad, tagLength: 128 }, key, sealed.ciphertext))
  } catch { throw new OfflineCipherError('authentication_failed') }
}
