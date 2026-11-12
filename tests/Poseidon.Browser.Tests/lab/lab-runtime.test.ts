import assert from 'node:assert/strict'
import { createHash, randomUUID } from 'node:crypto'
import { linkSync, mkdirSync, mkdtempSync, readFileSync, rmSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { decodeOwnedLabRuntime, labEndpoints, labConfigurationIdentity, labRuntimeFromEnvironment, validateLabOrigin, validateLabConfiguration, type OwnedLabRuntime } from './lab-runtime.ts'

// Pure protocol/file tests. No browser, TLS/provider request, credentials or host.
const selected = (): OwnedLabRuntime => ({ schemaVersion: 'poseidon.lab.owned-runtime.v1', runId: randomUUID(), appPort: 15443, providerPort: 18443, databasePort: 25432 })
const config = (runtime: OwnedLabRuntime) => ({
  DevelopmentLab: { OwnedRuntime: runtime, OidcPort: runtime.providerPort, OidcCaPath: '/synthetic/private/ca.pem', WebRoot: '/synthetic/checkout/frontend/dist' },
  Authentication: { Oidc: { Authority: labEndpoints(runtime).authority, Issuer: labEndpoints(runtime).authority,
    ClientId: 'poseidon-local', ClientSecret: randomUUID(), Memberships: [{ Issuer: labEndpoints(runtime).authority, Subject: randomUUID(), ActorId: randomUUID(), TenantId: randomUUID() }] } },
  Kestrel: { Endpoints: { LocalLab: { Url: labEndpoints(runtime).appOrigin } } },
  ConnectionStrings: { Poseidon: `Host=localhost;Port=${runtime.databasePort};Database=synthetic` },
})
const hash = (raw: string) => createHash('sha256').update(raw).digest('hex')
function fixture() {
  const root = resolve(mkdtempSync(join(tmpdir(), 'poseidon-runtime-unit-'))), runtime = selected()
  const put = (name: string, value: unknown) => writeFileSync(join(root, name), JSON.stringify(value), { mode: 0o600 })
  put('owned-runtime.json', runtime)
  put('source-bundle-plan.json', { schemaVersion: 'poseidon.bundle-fixture-plan.v1', runId: runtime.runId, ownedRuntime: runtime })
  const initial = config(runtime)
  put('appsettings.json', initial)
  const generation = (number: number, value: unknown = initial) => {
    const suffix = String(number).padStart(6, '0'), file = `source-bundle-config-${suffix}.json`, raw = JSON.stringify(value)
    writeFileSync(join(root, file), raw, { mode: 0o600 })
    put(`source-bundle-host-${suffix}.json`, {
      schemaVersion: 'poseidon.bundle.private-config-generation.v1', generation: number, configFile: file,
      configSha256: hash(raw), previousManifestSha256: number === 1 ? null : hash(readFileSync(join(root, `source-bundle-host-${String(number - 1).padStart(6, '0')}.json`), 'utf8')),
      immutableIdentitySha256: labConfigurationIdentity(initial, runtime),
    })
    return join(root, file)
  }
  const environment = { POSEIDON_LAB_PRIVATE: root, POSEIDON_LAB_RUNTIME_FILE: join(root, 'owned-runtime.json'),
    POSEIDON_LAB_CONFIG: generation(1), POSEIDON_REGISTER_URL: labEndpoints(runtime).appOrigin }
  return { root, runtime, initial, put, generation, environment, close: () => rmSync(root, { recursive: true }) }
}
function rejected(action: () => unknown) { assert.throws(action, { message: 'owned_lab_runtime_invalid' }) }

test('legacy origins are unchanged and arbitrary ambient origins cannot select a runtime', () => {
  assert.deepEqual(labEndpoints(), { appPort: 5443, providerPort: 8443, databasePort: 55432,
    appOrigin: 'https://localhost:5443', providerOrigin: 'https://localhost:8443', authority: 'https://localhost:8443/realms/poseidon-lab', runId: null })
  assert.equal(labRuntimeFromEnvironment({}).appPort, 5443)
  rejected(() => labRuntimeFromEnvironment({ POSEIDON_REGISTER_URL: 'https://localhost:15443' }))
  rejected(() => labRuntimeFromEnvironment({ POSEIDON_LAB_RUNTIME_FILE: '/foreign' }))
  rejected(() => labRuntimeFromEnvironment({ POSEIDON_LAB_CONFIG: '/foreign' }))
})

test('exact selected descriptor derives only HTTPS localhost endpoints', () => {
  const runtime = selected(), endpoints = labEndpoints(runtime)
  assert.equal(endpoints.appOrigin, 'https://localhost:15443')
  assert.equal(endpoints.authority, 'https://localhost:18443/realms/poseidon-lab')
  assert.equal(endpoints.databasePort, 25432)
  assert.equal(endpoints.runId, runtime.runId)
  assert.ok(Object.isFrozen(decodeOwnedLabRuntime(runtime)))
  validateLabConfiguration(config(runtime), runtime)
})

test('main and independent peer contexts must use the same selected app origin', () => {
  const f = fixture()
  try {
    const main = labRuntimeFromEnvironment(f.environment), peer = labRuntimeFromEnvironment(f.environment)
    validateLabOrigin(main.appOrigin, peer)
    validateLabOrigin(undefined, peer) // Deliberate TLS probes have no default URL.
    for (const origin of ['https://localhost:5443', main.providerOrigin, 'https://127.0.0.1:15443', 'https://foreign.invalid', main.appOrigin + '/']) {
      rejected(() => validateLabOrigin(origin, peer))
    }
  } finally { f.close() }
})

test('closed schema rejects duplicate ports, coercion, out of bounds, unknown keys and UUID suffix', () => {
  const runtime = selected()
  for (const field of ['appPort', 'providerPort', 'databasePort']) {
    for (const value of [true, '15443', 15443.5, 0, 1023, 65536, NaN, Infinity]) rejected(() => decodeOwnedLabRuntime({ ...runtime, [field]: value }))
  }
  for (const value of [{ ...runtime, providerPort: runtime.appPort }, { ...runtime, appOrigin: 'https://foreign.invalid' },
    { ...runtime, runId: runtime.runId + '\n' }, { ...runtime, runId: runtime.runId.toUpperCase() },
    { ...runtime, runId: '00000000-0000-0000-0000-000000000000' }]) rejected(() => decodeOwnedLabRuntime(value))
})

test('one run binds descriptor, plan, genesis, immutable generations and browser URL', () => {
  const f = fixture()
  try {
    assert.equal(labRuntimeFromEnvironment(f.environment).runId, f.runtime.runId)
    const second = f.generation(2)
    rejected(() => labRuntimeFromEnvironment(f.environment)) // Cannot reuse stale first generation.
    assert.equal(labRuntimeFromEnvironment({ ...f.environment, POSEIDON_LAB_CONFIG: second }).appOrigin, 'https://localhost:15443')
    rejected(() => labRuntimeFromEnvironment({ ...f.environment, POSEIDON_LAB_CONFIG: second, POSEIDON_REGISTER_URL: 'https://localhost:5443' }))
  } finally { f.close() }
})

test('selected runs never fall back when file, environment, plan or genesis binding is missing', () => {
  for (const missing of ['owned-runtime.json', 'source-bundle-plan.json', 'appsettings.json', 'source-bundle-host-000001.json']) {
    const f = fixture()
    try { unlinkSync(join(f.root, missing)); rejected(() => labRuntimeFromEnvironment(f.environment)) } finally { f.close() }
  }
  const f = fixture()
  try {
    rejected(() => labRuntimeFromEnvironment({ ...f.environment, POSEIDON_LAB_CONFIG: undefined }))
    rejected(() => labRuntimeFromEnvironment({ ...f.environment, POSEIDON_LAB_RUNTIME_FILE: join(f.root, 'other.json') }))
    // The config marker still blocks fallback if both public metadata markers disappear.
    unlinkSync(join(f.root, 'owned-runtime.json')); unlinkSync(join(f.root, 'source-bundle-plan.json'))
    rejected(() => labRuntimeFromEnvironment({ ...f.environment, POSEIDON_LAB_RUNTIME_FILE: undefined }))
  } finally { f.close() }
})

test('foreign run or selected generation endpoints cannot be adopted', () => {
  const f = fixture()
  try {
    f.put('source-bundle-plan.json', { schemaVersion: 'poseidon.bundle-fixture-plan.v1', runId: randomUUID(), ownedRuntime: f.runtime })
    rejected(() => labRuntimeFromEnvironment(f.environment))
    f.put('source-bundle-plan.json', { schemaVersion: 'poseidon.bundle-fixture-plan.v1', runId: f.runtime.runId, ownedRuntime: { ...f.runtime, providerPort: 19443 } })
    rejected(() => labRuntimeFromEnvironment(f.environment))
  } finally { f.close() }
  const mutations = [
    (c: ReturnType<typeof config>) => { c.Authentication.Oidc.Authority = 'https://127.0.0.1:18443/realms/poseidon-lab' },
    (c: ReturnType<typeof config>) => { c.Authentication.Oidc.Issuer = 'https://localhost:8443/realms/poseidon-lab' },
    (c: ReturnType<typeof config>) => { c.Kestrel.Endpoints.LocalLab.Url = 'https://localhost:5443' },
    (c: ReturnType<typeof config>) => { c.ConnectionStrings.Poseidon += ';Host=external.invalid' },
    (c: ReturnType<typeof config>) => { c.ConnectionStrings.Poseidon += ';host=external.invalid' },
    (c: ReturnType<typeof config>) => { c.ConnectionStrings.Poseidon += ';Server=external.invalid' },
    (c: ReturnType<typeof config>) => { c.DevelopmentLab.OidcPort = 8443 },
  ]
  for (const mutate of mutations) {
    const f = fixture()
    try { const changed = config(f.runtime); mutate(changed); f.generation(1, changed); rejected(() => labRuntimeFromEnvironment(f.environment)) } finally { f.close() }
  }
})

test('valid payload and chain hashes cannot conceal a changed immutable configuration', () => {
  const changes = [
    (c: ReturnType<typeof config>) => { c.Authentication.Oidc.ClientId += '-foreign' },
    (c: ReturnType<typeof config>) => { c.Authentication.Oidc.ClientSecret = randomUUID() },
    (c: ReturnType<typeof config>) => { c.DevelopmentLab.OidcCaPath += '.foreign' },
    (c: ReturnType<typeof config>) => { c.DevelopmentLab.WebRoot += '/foreign' },
    (c: ReturnType<typeof config>) => { c.ConnectionStrings.Poseidon += ';Password=' + randomUUID() },
    (c: ReturnType<typeof config>) => { c.Authentication.Oidc.Memberships[0].ActorId = randomUUID() },
  ]
  for (const change of changes) {
    const f = fixture()
    try {
      const changed = structuredClone(f.initial); change(changed)
      const selected = f.generation(2, changed) // Valid bytes/hash/previous chain; original immutable hash retained.
      rejected(() => labRuntimeFromEnvironment({ ...f.environment, POSEIDON_LAB_CONFIG: selected }))
    } finally { f.close() }
  }
})

test('raw decimal and exponent port spellings reject consistently in every binding file', () => {
  for (const [name, key, number] of [
    ['owned-runtime.json', 'appPort', 15443],
    ['source-bundle-plan.json', 'providerPort', 18443],
    ['appsettings.json', 'OidcPort', 18443],
    ['source-bundle-config-000001.json', 'OidcPort', 18443],
    ['source-bundle-host-000001.json', 'generation', 1],
  ] as const) {
    for (const spelling of [String(number) + '.0', String(number) + 'e0', String(number / 10000) + 'e4']) {
      const f = fixture()
      try {
        const path = join(f.root, name), raw = readFileSync(path, 'utf8').replace(`"${key}":${number}`, `"${key}":${spelling}`)
        writeFileSync(path, raw)
        if (name === 'source-bundle-config-000001.json') {
          const manifest = JSON.parse(readFileSync(join(f.root, 'source-bundle-host-000001.json'), 'utf8'))
          f.put('source-bundle-host-000001.json', { ...manifest, configSha256: hash(raw) })
        }
        rejected(() => labRuntimeFromEnvironment(f.environment))
      } finally { f.close() }
    }
  }
})

test('private loader rejects duplicate escaped properties, oversized/invalid UTF8 and hard links', () => {
  const f = fixture()
  try {
    const file = join(f.root, 'owned-runtime.json'), raw = readFileSync(file, 'utf8')
    writeFileSync(file, raw.slice(0, -1) + ',"app\\u0050ort":15443}')
    rejected(() => labRuntimeFromEnvironment(f.environment))
    writeFileSync(file, Buffer.from([0xff]))
    rejected(() => labRuntimeFromEnvironment(f.environment))
    writeFileSync(file, ' '.repeat(4097))
    rejected(() => labRuntimeFromEnvironment(f.environment))
    writeFileSync(file, raw)
    const link = join(f.root, 'hardlink.json'); linkSync(file, link)
    rejected(() => labRuntimeFromEnvironment(f.environment)); unlinkSync(link)
    unlinkSync(file); mkdirSync(file)
    rejected(() => labRuntimeFromEnvironment(f.environment))
  } finally { f.close() }
})

test('orphan payloads, chain edits, hash edits and ambiguous generation names fail closed', () => {
  for (const change of [
    (f: ReturnType<typeof fixture>) => f.put('source-bundle-config-000002.json', config(f.runtime)),
    (f: ReturnType<typeof fixture>) => f.put('source-bundle-host-unowned.json', {}),
    (f: ReturnType<typeof fixture>) => writeFileSync(join(f.root, 'source-bundle-config-000001.json'), JSON.stringify(config(f.runtime)) + ' '),
    (f: ReturnType<typeof fixture>) => { f.generation(2); const path = join(f.root, 'source-bundle-host-000002.json'); const value = JSON.parse(readFileSync(path, 'utf8')); value.previousManifestSha256 = null; writeFileSync(path, JSON.stringify(value)) },
  ]) {
    const f = fixture()
    try { change(f); rejected(() => labRuntimeFromEnvironment(f.environment)) } finally { f.close() }
  }
})
