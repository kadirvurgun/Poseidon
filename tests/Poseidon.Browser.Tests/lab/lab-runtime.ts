import { createHash } from 'node:crypto'
import { closeSync, constants, existsSync, fstatSync, lstatSync, readdirSync, openSync, readSync, realpathSync } from 'node:fs'
import { basename, isAbsolute, join, resolve } from 'node:path'

// BUNDLE-PORT-ISOLATION-01: public endpoint metadata inside the same owned
// private root. No origin/issuer comes from requests or arbitrary environment URLs.
const schema = 'poseidon.lab.owned-runtime.v1' as const
const names = ['schemaVersion', 'runId', 'appPort', 'providerPort', 'databasePort'] as const
export type OwnedLabRuntime = Readonly<{ schemaVersion: typeof schema; runId: string; appPort: number; providerPort: number; databasePort: number }>
export type LabEndpoints = Readonly<{ appOrigin: string; providerOrigin: string; authority: string; appPort: number; providerPort: number; databasePort: number; runId: string | null }>
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
function invalid(): never { throw new Error('owned_lab_runtime_invalid') }
const samePath = (a: string, b: string) => process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b

export function decodeOwnedLabRuntime(value: unknown): OwnedLabRuntime {
  if (!object(value) || Object.keys(value).length !== names.length || !names.every(name => Object.hasOwn(value, name)) ||
      value.schemaVersion !== schema || typeof value.runId !== 'string' || value.runId.length !== 36 ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value.runId) ||
      value.runId === '00000000-0000-0000-0000-000000000000') invalid()
  const ports = [value.appPort, value.providerPort, value.databasePort]
  if (ports.some(port => typeof port !== 'number' || !Number.isSafeInteger(port) || port < 1024 || port > 65535) || new Set(ports).size !== 3) invalid()
  return Object.freeze({ schemaVersion: schema, runId: value.runId, appPort: value.appPort as number,
    providerPort: value.providerPort as number, databasePort: value.databasePort as number })
}

export function labEndpoints(runtime: OwnedLabRuntime | null = null): LabEndpoints {
  const ports = runtime === null ? { appPort: 5443, providerPort: 8443, databasePort: 55432 } : decodeOwnedLabRuntime(runtime)
  const appOrigin = `https://localhost:${ports.appPort}`, providerOrigin = `https://localhost:${ports.providerPort}`
  return Object.freeze({ appPort: ports.appPort, providerPort: ports.providerPort, databasePort: ports.databasePort,
    appOrigin, providerOrigin, authority: providerOrigin + '/realms/poseidon-lab', runId: runtime?.runId ?? null })
}

export function validateLabOrigin(origin: unknown, endpoints: LabEndpoints): void {
  if (origin !== undefined && origin !== endpoints.appOrigin) invalid()
}

function read(path: string, maximum: number, integerFields: readonly string[] = ['appPort', 'providerPort', 'databasePort', 'OidcPort']): { raw: string; value: unknown } {
  const before = lstatSync(path)
  if (before.isSymbolicLink() || !before.isFile() || before.nlink !== 1 || before.size < 1 || before.size > maximum ||
      !samePath(realpathSync(path), path)) invalid()
  const descriptor = openSync(path, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0) | (constants.O_NONBLOCK ?? 0))
  try {
    const metadata = fstatSync(descriptor)
    if (metadata.dev !== before.dev || metadata.ino !== before.ino || metadata.size !== before.size) invalid()
    const buffer = Buffer.alloc(maximum + 1)
    let length = 0, count: number
    while (length < buffer.length && (count = readSync(descriptor, buffer, length, buffer.length - length, length)) > 0) length += count
    const after = fstatSync(descriptor), current = lstatSync(path)
    if (length !== metadata.size || length > maximum || after.size !== metadata.size || after.mtimeMs !== metadata.mtimeMs ||
        current.isSymbolicLink() || current.dev !== metadata.dev || current.ino !== metadata.ino || !samePath(realpathSync(path), path)) invalid()
    const raw = new TextDecoder('utf-8', { fatal: true }).decode(buffer.subarray(0, length))
    return { raw, value: strictJson(raw, integerFields) }
  } finally { closeSync(descriptor) }
}
function strictJson(raw: string, integerFields: readonly string[]): unknown {
  const value: unknown = JSON.parse(raw)
  const stack: (Set<string> | null)[] = []
  for (const token of raw.matchAll(/"(?:\\.|[^"\\])*"|[{}\[\]]/g)) {
    const text = token[0]
    if (text === '{') stack.push(new Set())
    else if (text === '[') stack.push(null)
    else if (text === '}' || text === ']') stack.pop()
    else if (/^\s*:/.test(raw.slice(token.index! + text.length))) {
      const keys = stack.at(-1), key = JSON.parse(text) as string
      if (integerFields.includes(key) &&
          !/^\s*:\s*(?:0|[1-9][0-9]*)(?=\s*[,}\]])/.test(raw.slice(token.index! + text.length))) invalid()
      if (!keys || keys.has(key)) invalid()
      keys.add(key)
    }
  }
  return value
}

export function validateLabConfiguration(value: unknown, runtime: OwnedLabRuntime | null): void {
  if (!object(value) || !object(value.DevelopmentLab)) invalid()
  const dev = value.DevelopmentLab
  if (runtime === null) {
    if (Object.hasOwn(dev, 'OwnedRuntime') || Object.hasOwn(dev, 'OidcPort')) invalid()
    return
  }
  const configured = decodeOwnedLabRuntime(dev.OwnedRuntime), endpoints = labEndpoints(runtime)
  if (names.some(key => configured[key] !== runtime[key]) || dev.OidcPort !== runtime.providerPort ||
      !object(value.Authentication) || !object(value.Authentication.Oidc) ||
      value.Authentication.Oidc.Authority !== endpoints.authority || value.Authentication.Oidc.Issuer !== endpoints.authority ||
      !object(value.Kestrel) || !object(value.Kestrel.Endpoints) || Object.keys(value.Kestrel.Endpoints).length !== 1 ||
      !object(value.Kestrel.Endpoints.LocalLab) || value.Kestrel.Endpoints.LocalLab.Url !== endpoints.appOrigin ||
      !object(value.ConnectionStrings) || typeof value.ConnectionStrings.Poseidon !== 'string') invalid()
  const connection = value.ConnectionStrings.Poseidon
  const entries = connection.split(';').filter(Boolean).map(part => {
    const separator = part.indexOf('=')
    if (separator < 1) invalid()
    return [part.slice(0, separator), part.slice(separator + 1)] as const
  })
  const keys = entries.map(([key]) => key.trim().toLowerCase())
  if (new Set(keys).size !== keys.length || keys.some(key => ['server', 'data source', 'address', 'network address'].includes(key))) invalid()
  const hosts = entries.filter(([key]) => key === 'Host').map(([, value]) => value)
  const ports = entries.filter(([key]) => key === 'Port').map(([, value]) => value)
  if (hosts.length !== 1 || hosts[0] !== 'localhost' || ports.length !== 1 || ports[0] !== String(runtime.databasePort)) invalid()
}

function compare(a: string, b: string): number {
  const left = [...a].map(char => char.codePointAt(0)!), right = [...b].map(char => char.codePointAt(0)!)
  for (let index = 0; index < Math.min(left.length, right.length); index++) {
    if (left[index] !== right[index]) return left[index] - right[index]
  }
  return left.length - right.length
}
function canonical(value: unknown): string {
  if (Array.isArray(value)) return '[' + value.map(canonical).join(',') + ']'
  if (object(value)) return '{' + Object.keys(value).sort(compare).map(key => canonical(key) + ':' + canonical(value[key])).join(',') + '}'
  if (typeof value === 'string' && !value.isWellFormed()) invalid()
  const encoded = JSON.stringify(value)
  if (encoded === undefined) invalid()
  return encoded
}

export function labConfigurationIdentity(value: unknown, runtime: OwnedLabRuntime | null): string {
  validateLabConfiguration(value, runtime)
  if (!object(value) || !object(value.Authentication) || !object(value.Authentication.Oidc) || !object(value.DevelopmentLab)) invalid()
  const federation = value.Authentication.Oidc, development = value.DevelopmentLab
  const pick = (record: Record<string, unknown>, keys: readonly string[]) => Object.fromEntries(keys.map(key => {
    if (typeof record[key] !== 'string') invalid()
    return [key, record[key]]
  }))
  if (!Array.isArray(federation.Memberships)) invalid()
  const members = federation.Memberships.map(member => {
    if (!object(member)) invalid()
    return pick(member, ['Issuer', 'Subject', 'ActorId', 'TenantId'])
  }).sort((a, b) => compare(a.Issuer as string, b.Issuer as string) || compare(a.Subject as string, b.Subject as string))
  const dev = pick(development, ['OidcCaPath', 'WebRoot'])
  if (runtime !== null) Object.assign(dev, { OwnedRuntime: runtime, OidcPort: runtime.providerPort })
  return createHash('sha256').update(canonical({
    ConnectionStrings: value.ConnectionStrings, Kestrel: value.Kestrel,
    Oidc: pick(federation, ['Authority', 'Issuer', 'ClientId', 'ClientSecret']),
    Memberships: members, DevelopmentLab: dev,
  })).digest('hex')
}

function configurations(root: string, environment: Record<string, string | undefined>, runtime: OwnedLabRuntime | null) {
  const genesis = join(root, 'appsettings.json')
  let genesisIdentity: string | null = null
  if (existsSync(genesis)) genesisIdentity = labConfigurationIdentity(read(genesis, 2097152).value, runtime)
  else if (runtime !== null) invalid()
  const selected = environment.POSEIDON_LAB_CONFIG
  const manifests = readdirSync(root).filter(name => /^source-bundle-host-.*\.json$/.test(name)).sort()
  if (runtime !== null && (!selected || manifests.length === 0)) invalid()
  if (!manifests.length) {
    if (selected && (!isAbsolute(selected) || !samePath(resolve(selected), genesis))) invalid()
    return
  }
  let previous: string | null = null, identity: string | null = null, latest = ''
  for (let index = 0; index < manifests.length; index++) {
    const suffix = String(index + 1).padStart(6, '0'), manifestName = `source-bundle-host-${suffix}.json`
    if (manifests[index] !== manifestName) invalid()
    const manifest = read(join(root, manifestName), 4096, ['generation']), value = manifest.value
    if (!object(value) || Object.keys(value).length !== 6 || value.schemaVersion !== 'poseidon.bundle.private-config-generation.v1' ||
        value.generation !== index + 1 || value.configFile !== `source-bundle-config-${suffix}.json` ||
        value.previousManifestSha256 !== previous || typeof value.immutableIdentitySha256 !== 'string' ||
        !/^[0-9a-f]{64}$/.test(value.immutableIdentitySha256) || identity !== null && identity !== value.immutableIdentitySha256) invalid()
    latest = join(root, value.configFile as string)
    const config = read(latest, 2097152)
    if (value.configSha256 !== createHash('sha256').update(config.raw).digest('hex')) invalid()
    const observed = labConfigurationIdentity(config.value, runtime)
    if (value.immutableIdentitySha256 !== observed || genesisIdentity !== null && genesisIdentity !== observed) invalid()
    previous = createHash('sha256').update(manifest.raw).digest('hex'); identity = value.immutableIdentitySha256
  }
  const payloads = readdirSync(root).filter(name => /^source-bundle-config-.*\.json$/.test(name))
  if (payloads.length !== manifests.length || !selected || !isAbsolute(selected) || !samePath(resolve(selected), latest) || basename(selected) !== basename(latest)) invalid()
}

export function labRuntimeFromEnvironment(environment: Record<string, string | undefined> = process.env): LabEndpoints {
  try {
    const privateRoot = environment.POSEIDON_LAB_PRIVATE, supplied = environment.POSEIDON_LAB_RUNTIME_FILE
    if (!privateRoot) {
      if (supplied || environment.POSEIDON_LAB_CONFIG) invalid()
      const fallback = labEndpoints()
      if (environment.POSEIDON_REGISTER_URL && environment.POSEIDON_REGISTER_URL !== fallback.appOrigin) invalid()
      return fallback
    }
    if (!isAbsolute(privateRoot)) invalid()
    const root = resolve(privateRoot)
    if (!samePath(realpathSync(root), root) || lstatSync(root).isSymbolicLink() || !lstatSync(root).isDirectory()) invalid()
    const expected = join(root, 'owned-runtime.json'), planFile = join(root, 'source-bundle-plan.json')
    if (supplied && (!isAbsolute(supplied) || !samePath(resolve(supplied), expected))) invalid()
    const plan = existsSync(planFile) ? read(planFile, 65536) : null
    if (!existsSync(expected)) {
      if (supplied || object(plan?.value) && Object.hasOwn(plan.value, 'ownedRuntime')) invalid()
      configurations(root, environment, null)
      const fallback = labEndpoints()
      if (environment.POSEIDON_REGISTER_URL && environment.POSEIDON_REGISTER_URL !== fallback.appOrigin) invalid()
      return fallback
    }
    const descriptor = read(expected, 4096), runtime = decodeOwnedLabRuntime(descriptor.value)
    if (!plan || !object(plan.value) || plan.value.schemaVersion !== 'poseidon.bundle-fixture-plan.v1' || plan.value.runId !== runtime.runId) invalid()
    const planned = decodeOwnedLabRuntime(plan.value.ownedRuntime)
    if (names.some(name => planned[name] !== runtime[name])) invalid()
    configurations(root, environment, runtime)
    const selected = labEndpoints(runtime)
    if (environment.POSEIDON_REGISTER_URL && environment.POSEIDON_REGISTER_URL !== selected.appOrigin) invalid()
    return selected
  } catch { invalid() } // No path, parser, private metadata or previous exception escapes.
}
