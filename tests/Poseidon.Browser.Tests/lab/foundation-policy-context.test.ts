import assert from 'node:assert/strict'
import { randomUUID, createHash } from 'node:crypto'
import { chmodSync, linkSync, mkdtempSync, realpathSync, rmdirSync, symlinkSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { assertOriginalFoundationShip, decodeFoundationConsumerPlan, readFoundationMetadata, requiredFoundationConsumerPlan } from '../register-specs/foundation-policy-context.ts'
import { labConfigurationIdentity, labEndpoints, type OwnedLabRuntime } from './lab-runtime.ts'

// FOUNDATION-POLICY-SIGNED-FIXTURE-01: pure/private-file regressions only.
// These synthetic objects confer no actual session, policy or provider approval.
const tenant = '10000000-0000-0000-0000-000000000001', ship = '30000000-0000-0000-0000-000000000001'
const source = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3'
const error = { message: 'foundation_consumer_scope_invalid' }
const now = Date.parse('2026-11-13T12:00:00Z')
function plan() {
  return { schemaVersion: 'poseidon.foundation-policy-fixture.v1', runId: randomUUID(), tenantId: tenant, vesselId: ship,
    writerActorId: randomUUID(), reviewerActorId: randomUUID(), writerRoleId: randomUUID(), reviewerRoleId: randomUUID(),
    notBefore: '2026-11-13T11:59:00+00:00', expiresAt: '2026-11-13T20:00:00+00:00' }
}
function base(value: ReturnType<typeof plan>) { return { ...value, schemaVersion: 'poseidon.bundle-fixture-plan.v1', sourceVersion: source } }
function user(value: ReturnType<typeof plan>) { return { authenticated: true, setupRequired: false, canRead: true, actorId: value.writerActorId, tenantId: tenant } }
function fleet() { return { items: [{ id: ship, kind: 'Ship' }] } }
function files(raw: string | Buffer = '{}') {
  const root = mkdtempSync(join(realpathSync(tmpdir()), 'poseidon-foundation-unit-'))
  chmodSync(root, 0o700)
  const paths: string[] = [], links: { path: string; directory: boolean }[] = []
  const put = (name: string, value: string | Buffer) => {
    const path = join(root, name)
    if (!paths.includes(path)) paths.push(path)
    writeFileSync(path, value, { mode: 0o600 }); return path
  }
  const file = put('foundation-policy-fixture-plan.json', raw)
  return { root, file, put, links, close() {
    for (const link of links.reverse()) {
      if (link.directory && process.platform === 'win32') rmdirSync(link.path)
      else unlinkSync(link.path)
    }
    for (const path of paths.reverse()) unlinkSync(path)
    rmdirSync(root)
  } }
}

test('strict same-run plan retains its original finite window and freezes the returned metadata', () => {
  const value = plan(), result = decodeFoundationConsumerPlan(value, base(value), value.runId, now)
  assert.equal(result, value); assert.ok(Object.isFrozen(result))
  assert.equal(result.expiresAt, '2026-11-13T20:00:00+00:00')
  assert.equal(decodeFoundationConsumerPlan({ ...value }, base(value), null, now).runId, value.runId)
})

test('rejects unknown schema, missing keys, foreign base/run, UUID suffixes and expired or unbounded windows', () => {
  const value = plan(), missing: Record<string, unknown> = { ...value }; delete missing.writerActorId
  const inputs: unknown[] = [null, [], missing, { ...value, approved: true }, { ...value, schemaVersion: 'other' },
    { ...value, writerActorId: value.reviewerActorId }, { ...value, reviewerRoleId: value.writerRoleId },
    { ...value, expiresAt: value.notBefore }, { ...value, expiresAt: '2026-11-13T22:00:00Z' },
    { ...value, notBefore: '2026-11-13T12:01:00Z' }, { ...value, expiresAt: '2026-11-13T12:00:00Z' },
    { ...value, notBefore: '2026-02-30T00:00:00Z' }, { ...value, notBefore: '2026-11-13T12:00:00+01:00' }]
  for (const key of ['runId', 'tenantId', 'vesselId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId']) {
    inputs.push({ ...value, [key]: value[key as keyof typeof value] + '\n' })
    assert.throws(() => decodeFoundationConsumerPlan(value, { ...base(value), [key]: randomUUID() }, value.runId, now), error)
  }
  for (const input of inputs) assert.throws(() => decodeFoundationConsumerPlan(input, base(value), value.runId, now), error)
  for (const changed of [{ ...base(value), sourceVersion: 'other' }, { ...base(value), schemaVersion: 'other' }])
    assert.throws(() => decodeFoundationConsumerPlan(value, changed, value.runId, now), error)
  assert.throws(() => decodeFoundationConsumerPlan(value, base(value), randomUUID(), now), error)
  assert.throws(() => decodeFoundationConsumerPlan(value, base(value), value.runId, NaN), error)
})

test('requires actual original writer and original first Ship without selecting a later matching item', () => {
  const value = plan(); assertOriginalFoundationShip(value, user(value), fleet())
  for (const changed of [{ ...user(value), authenticated: false }, { ...user(value), setupRequired: true },
    { ...user(value), canRead: false }, { ...user(value), actorId: value.reviewerActorId }, { ...user(value), tenantId: randomUUID() }, null])
    assert.throws(() => assertOriginalFoundationShip(value, changed, fleet()), error)
  for (const changed of [null, { items: [] }, { items: [{ id: ship, kind: 'Tug' }] },
    { items: [{ id: randomUUID(), kind: 'Ship' }, ...fleet().items] }])
    assert.throws(() => assertOriginalFoundationShip(value, user(value), changed), error)
})

test('bounded private regular reads reject BOM, malformed UTF8, duplicate decoded keys and closed paths', () => {
  const f = files()
  try {
    assert.deepEqual(readFoundationMetadata(f.root, 'foundation-policy-fixture-plan.json'), {})
    f.put('foundation-policy-fixture-plan.json', '{}'.padEnd(65536, ' '))
    assert.deepEqual(readFoundationMetadata(f.root, 'foundation-policy-fixture-plan.json'), {})
    for (const raw of ['', '{}'.padEnd(65537, ' '), '\ufeff{}', Buffer.from([0xff, 0xfe]), '{',
      '{"runId":1,"runId":2}', '{"runId":1,"\\u0072unId":2}', '{"nested":{"same":1,"same":2}}']) {
      f.put('foundation-policy-fixture-plan.json', raw)
      assert.throws(() => readFoundationMetadata(f.root, 'foundation-policy-fixture-plan.json'), error)
    }
    for (const name of ['../foundation-policy-fixture-plan.json', 'operator.json', 'missing.json'])
      assert.throws(() => readFoundationMetadata(f.root, name), error)
    assert.throws(() => readFoundationMetadata('relative', 'foundation-policy-fixture-plan.json'), error)
  } finally { f.close() }
})

test('rejects hardlinked files and aliased private roots, plus widened POSIX root or file metadata', () => {
  const f = files()
  try {
    const extra = join(f.root, 'hardlink.json'); linkSync(f.file, extra)
    try { assert.throws(() => readFoundationMetadata(f.root, 'foundation-policy-fixture-plan.json'), error) }
    finally { unlinkSync(extra) }
    const alias = join(f.root, 'alias'); symlinkSync(f.root, alias, process.platform === 'win32' ? 'junction' : 'dir')
    f.links.push({ path: alias, directory: true })
    assert.throws(() => readFoundationMetadata(alias, 'foundation-policy-fixture-plan.json'), error)
    if (process.platform !== 'win32') {
      chmodSync(f.file, 0o644)
      assert.throws(() => readFoundationMetadata(f.root, 'foundation-policy-fixture-plan.json'), error)
      chmodSync(f.file, 0o600); chmodSync(f.root, 0o755)
      assert.throws(() => readFoundationMetadata(f.root, 'foundation-policy-fixture-plan.json'), error)
      chmodSync(f.root, 0o700)
    }
  } finally { f.close() }
})

test('full selected-runtime consumer binds both plans to the immutable generation before checking actual context', () => {
  const value = plan(), current = Date.now()
  value.notBefore = new Date(current-60000).toISOString(); value.expiresAt = new Date(current+3600000).toISOString()
  const f = files(JSON.stringify(value)), runtime: OwnedLabRuntime = { schemaVersion: 'poseidon.lab.owned-runtime.v1',
    runId: value.runId, appPort: 15443, providerPort: 18443, databasePort: 25432 }
  const origin = labEndpoints(runtime)
  const config = { DevelopmentLab: { OwnedRuntime: runtime, OidcPort: runtime.providerPort, OidcCaPath: '/synthetic/ca.pem', WebRoot: '/synthetic/dist' },
    Authentication: { Oidc: { Authority: origin.authority, Issuer: origin.authority, ClientId: 'poseidon-local', ClientSecret: randomUUID(), Memberships: [] } },
    Kestrel: { Endpoints: { LocalLab: { Url: origin.appOrigin } } }, ConnectionStrings: { Poseidon: 'Host=localhost;Port=25432;Database=synthetic' } }
  const raw = JSON.stringify(config), environment = { POSEIDON_LAB_PRIVATE: f.root, POSEIDON_LAB_RUNTIME_FILE: join(f.root, 'owned-runtime.json'),
    POSEIDON_LAB_CONFIG: join(f.root, 'source-bundle-config-000001.json'), POSEIDON_REGISTER_URL: origin.appOrigin }
  try {
    f.put('owned-runtime.json', JSON.stringify(runtime)); f.put('source-bundle-plan.json', JSON.stringify({ ...base(value), ownedRuntime: runtime }))
    f.put('appsettings.json', raw); f.put('source-bundle-config-000001.json', raw)
    f.put('source-bundle-host-000001.json', JSON.stringify({ schemaVersion: 'poseidon.bundle.private-config-generation.v1', generation: 1,
      configFile: 'source-bundle-config-000001.json', configSha256: createHash('sha256').update(raw).digest('hex'), previousManifestSha256: null,
      immutableIdentitySha256: labConfigurationIdentity(config, runtime) }))
    assert.equal(requiredFoundationConsumerPlan(environment).writerActorId, value.writerActorId)
    f.put('foundation-policy-fixture-plan.json', JSON.stringify({ ...value, runId: randomUUID() }))
    assert.throws(() => requiredFoundationConsumerPlan(environment), error)
    f.put('foundation-policy-fixture-plan.json', JSON.stringify(value))
    assert.throws(() => requiredFoundationConsumerPlan({ ...environment, POSEIDON_LAB_CONFIG: join(f.root, 'source-bundle-config-000002.json') }), error)
  } finally { f.close() }
})
