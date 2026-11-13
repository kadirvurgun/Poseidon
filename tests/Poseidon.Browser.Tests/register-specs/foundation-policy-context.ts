// FOUNDATION-POLICY-SIGNED-FIXTURE-01. Metadata confers no runtime authority.
import { closeSync, constants, fstatSync, lstatSync, openSync, readSync, realpathSync } from 'node:fs'
import { isAbsolute, join, resolve } from 'node:path'
import { labRuntimeFromEnvironment } from '../lab/lab-runtime.ts'

const tenant = '10000000-0000-0000-0000-000000000001', ship = '30000000-0000-0000-0000-000000000001'
const source = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3'
const keys = ['schemaVersion', 'runId', 'tenantId', 'vesselId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId', 'notBefore', 'expiresAt'] as const
type Obj = Record<string, any>
export type FoundationConsumerPlan = Readonly<{ schemaVersion: string; runId: string; tenantId: string; vesselId: string;
  writerActorId: string; reviewerActorId: string; writerRoleId: string; reviewerRoleId: string; notBefore: string; expiresAt: string }>
const object = (value: unknown): value is Obj => !!value && typeof value === 'object' && !Array.isArray(value)
const guid = (value: unknown) => typeof value === 'string' && value.length === 36 && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value) && value !== '00000000-0000-0000-0000-000000000000'
const utc = (value: unknown): value is string => typeof value === 'string' && value === value.trim() &&
  /^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,6})?(?:Z|\+00:00)$/.test(value) &&
  Number(value.slice(0, 4)) > 0 && Number.isFinite(Date.parse(value)) && new Date(value).toISOString().slice(0, 19) === value.slice(0, 19)
function invalid(): never { throw new Error('foundation_consumer_scope_invalid') }
export function decodeFoundationConsumerPlan(value: unknown, base: unknown, runtimeRun: string | null, now = Date.now()): FoundationConsumerPlan {
  if (!object(value) || Object.keys(value).length !== keys.length || !keys.every(key => Object.hasOwn(value, key)) ||
      value.schemaVersion !== 'poseidon.foundation-policy-fixture.v1' || !Number.isFinite(now) || value.tenantId !== tenant || value.vesselId !== ship ||
      !['runId', 'tenantId', 'vesselId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId'].every(key => guid(value[key])) ||
      value.writerActorId === value.reviewerActorId || value.writerRoleId === value.reviewerRoleId ||
      !utc(value.notBefore) || !utc(value.expiresAt) || Date.parse(value.expiresAt) <= Date.parse(value.notBefore) ||
      Date.parse(value.expiresAt)-Date.parse(value.notBefore) > 9*3600000 || now < Date.parse(value.notBefore) || now >= Date.parse(value.expiresAt) ||
      !object(base) || base.schemaVersion !== 'poseidon.bundle-fixture-plan.v1' || base.sourceVersion !== source ||
      ['runId', 'tenantId', 'vesselId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId'].some(key => base[key] !== value[key]) ||
      runtimeRun !== null && runtimeRun !== value.runId) invalid()
  return Object.freeze(value) as FoundationConsumerPlan
}
function parse(raw: string): unknown {
  const value: unknown = JSON.parse(raw), stack: (Set<string> | null)[] = []
  for (const token of raw.matchAll(/"(?:\\.|[^"\\])*"|[{}\[\]]/g)) {
    const text = token[0]
    if (text === '{') stack.push(new Set())
    else if (text === '[') stack.push(null)
    else if (text === '}' || text === ']') stack.pop()
    else if (/^\s*:/.test(raw.slice(token.index!+text.length))) {
      const names = stack.at(-1), name = JSON.parse(text) as string
      if (!names || names.has(name)) invalid()
      names.add(name)
    }
  }
  return value
}
export function readFoundationMetadata(root: string, name: string): unknown {
  try {
    if (!isAbsolute(root) || root.length > 32768 || !['source-bundle-plan.json', 'foundation-policy-fixture-plan.json'].includes(name)) invalid()
    const same = (a: string, b: string) => process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b
    const directory = resolve(root), path = join(directory, name), parent = lstatSync(directory), before = lstatSync(path)
    const privateNode = (node: typeof before, file: boolean) => {
      if (node.isSymbolicLink() || (file ? !node.isFile() || node.nlink !== 1 : !node.isDirectory()) ||
          process.platform !== 'win32' && ((node.mode & 0o077) !== 0 || typeof process.geteuid === 'function' && node.uid !== process.geteuid())) invalid()
    }
    const stable = (a: typeof before, b: typeof before) => a.dev === b.dev && a.ino === b.ino && a.mode === b.mode && a.uid === b.uid &&
      a.nlink === b.nlink && a.size === b.size && a.mtimeMs === b.mtimeMs && a.ctimeMs === b.ctimeMs
    privateNode(parent, false); privateNode(before, true)
    if (!same(realpathSync(directory), directory) || !same(realpathSync(path), path) || before.size < 1 || before.size > 65536) invalid()
    const file = openSync(path, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0) | (constants.O_NONBLOCK ?? 0))
    try {
      const opened = fstatSync(file); privateNode(opened, true)
      if (!stable(before, opened)) invalid()
      const buffer = Buffer.alloc(65537)
      let length = 0, count: number
      while (length < buffer.length && (count = readSync(file, buffer, length, buffer.length-length, length)) > 0) length += count
      const bytes = buffer.subarray(0, length)
      const after = fstatSync(file), current = lstatSync(path), parentAfter = lstatSync(directory)
      privateNode(after, true); privateNode(current, true); privateNode(parentAfter, false)
      if (bytes.length !== opened.size || bytes.length > 65536 || !stable(opened, after) || !stable(before, current) ||
          parent.dev !== parentAfter.dev || parent.ino !== parentAfter.ino || !same(realpathSync(path), path) || !same(realpathSync(directory), directory) ||
          bytes.length >= 3 && bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) invalid()
      return parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes))
    } finally { closeSync(file) }
  } catch { invalid() } // Never expose private paths, JSON contents or parser errors.
}
export function requiredFoundationConsumerPlan(environment: Record<string, string | undefined> = process.env): FoundationConsumerPlan {
  try {
    const runtime = labRuntimeFromEnvironment(environment), root = environment.POSEIDON_LAB_PRIVATE
    if (!root) invalid()
    return decodeFoundationConsumerPlan(readFoundationMetadata(root, 'foundation-policy-fixture-plan.json'),
      readFoundationMetadata(root, 'source-bundle-plan.json'), runtime.runId)
  } catch { invalid() }
}
export function assertOriginalFoundationShip(plan: FoundationConsumerPlan, user: unknown, fleet: unknown): void {
  if (!object(user) || user.authenticated !== true || user.setupRequired !== false || user.canRead !== true ||
      user.actorId !== plan.writerActorId || user.tenantId !== plan.tenantId || !object(fleet) || !Array.isArray(fleet.items) ||
      !fleet.items.length || fleet.items[0]?.id !== plan.vesselId || fleet.items[0]?.kind !== 'Ship') invalid()
}
