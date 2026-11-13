// QUALIFIED-OWNING-SIGNED-FIXTURE-01. Closed references are not authority.
import { closeSync, constants, fstatSync, lstatSync, openSync, readSync, realpathSync } from 'node:fs'
import type { Stats } from 'node:fs'
import { isAbsolute, join, relative, resolve, sep } from 'node:path'
import { labRuntimeFromEnvironment } from '../lab/lab-runtime.ts'
import { createHash } from 'node:crypto'

export const qualifiedModes = ['Ship', 'Tug', 'Compliance'] as const
export type QualifiedMode = typeof qualifiedModes[number]
export const qualifiedKinds = ['shipCargo', 'tugService', 'tugOperation', 'tugWinch', 'shipClass', 'shipMsmd'] as const
export type QualifiedKind = typeof qualifiedKinds[number]
export const qualifiedFields: Record<QualifiedKind, string> = { shipCargo: 'cargoPlan', tugService: 'serviceAuthorization',
  tugOperation: 'operation', tugWinch: 'winchFitment', shipClass: 'classPack', shipMsmd: 'msmd' }
export const qualifiedRuleNames: Record<QualifiedMode, string> = { Ship: 'SYNTHETIC-FOUNDATION-OWNING-SHIP',
  Tug: 'SYNTHETIC-FOUNDATION-OWNING-TUG', Compliance: 'SYNTHETIC-FOUNDATION-COMPLIANCE' }
export const qualifiedModeKinds: Record<QualifiedMode, readonly QualifiedKind[]> = {
  Ship: ['shipCargo'], Tug: ['tugService', 'tugOperation', 'tugWinch'], Compliance: ['shipClass', 'shipMsmd'] }
type Rule = Readonly<{ id: string; revision: number; sourceId: string; sourceRevision: number; requirementId: string;
  requirementRevision: number; qualificationRevision: number; qualificationHash: string; baselineEvaluationId: string }>
type Witness = Readonly<{ vesselId: string; id: string; revision: number; hash: string; controlId: string; controlRevision: number;
  operationId: string; baselineEvaluationId: string; fileId: string; fileRevision: number; fileChecksum: string;
  verificationId: string; verificationRevision: number }>
type VesselObservation = Readonly<{ id: string; companyId: string; kind: 'Ship' | 'Tug'; code: string; version: number;
  profileRevision: number; profileVersion: number; profileContentSha256: string }>
export type QualifiedOwningFixture = Readonly<{ schemaVersion: 'poseidon.qualified-owning.signed-fixture.v1';
  runId: string; tenantId: string; writerActorId: string; reviewerActorId: string; shipId: string; tugId: string; expiresAt: string;
  vessels: Readonly<Record<'Ship' | 'Tug', VesselObservation>>;
  rules: Readonly<Record<QualifiedMode, Rule>>; witnesses: Readonly<Record<QualifiedKind, Witness>> }>
type ObjectValue = Record<string, any>
export type QualifiedPage = { request: { get(path: string): Promise<{ status(): number; json(): Promise<any> }> } }
export const qualifiedGuid = (value: unknown): value is string => typeof value === 'string' && value.length === 36 &&
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value) && value !== '00000000-0000-0000-0000-000000000000'
const object = (value: unknown): value is ObjectValue => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, keys: readonly string[]): value is ObjectValue => object(value) &&
  Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key))
const positive = (value: unknown) => typeof value === 'number' && Number.isSafeInteger(value) && value > 0
const hash = (value: unknown) => typeof value === 'string' && value.length === 64 && /^[A-F0-9]{64}$/.test(value)
const utc = (value: unknown): value is string => typeof value === 'string' && value === value.trim() &&
  /^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,6})?(?:Z|\+00:00)$/.test(value) &&
  Number(value.slice(0,4)) > 0 && Number.isFinite(Date.parse(value)) && new Date(value).toISOString().slice(0,19) === value.slice(0,19)
function invalid(): never { throw new Error('qualified_owning_fixture_invalid') }
const seedVessels = ['30000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000003']
export const qualifiedVesselCode = (run: string, kind: 'Ship' | 'Tug') => 'ZZZ-FICTIONAL-QOF-'+kind.toUpperCase()+'-'+run.replaceAll('-', '')
function canonical(value: any): string {
  if (Array.isArray(value)) return '['+value.map(canonical).join(',')+']'
  if (value !== null && typeof value === 'object') return '{'+Object.entries(value).sort(([a],[b]) => a.localeCompare(b)).map(([key,item]) => JSON.stringify(key)+':'+canonical(item)).join(',')+'}'
  return JSON.stringify(value)
}
export const qualifiedContentHash = (value: unknown) => createHash('sha256').update(canonical(value)).digest('hex').toUpperCase()
export function decodeQualifiedVessels(value: unknown, expected: { runId: string; tenantId: string; writerActorId: string }) {
  if (!exact(value, ['schemaVersion', 'runId', 'tenantId', 'writerActorId', 'vessels']) || value.schemaVersion !== 'poseidon.qualified-owning.observed-vessels.v1' ||
      value.tenantId !== '10000000-0000-0000-0000-000000000001' ||
      (['runId', 'tenantId', 'writerActorId'] as const).some(key => !qualifiedGuid(value[key]) || value[key] !== expected[key]) ||
      !exact(value.vessels, ['Ship', 'Tug'])) invalid()
  for (const kind of ['Ship', 'Tug'] as const) {
    const row = value.vessels[kind]
    if (!exact(row, ['id', 'companyId', 'kind', 'code', 'version', 'profileRevision', 'profileVersion', 'profileContentSha256']) ||
        !qualifiedGuid(row.id) || !qualifiedGuid(row.companyId) || seedVessels.includes(row.id) || row.kind !== kind || row.code !== qualifiedVesselCode(value.runId, kind) ||
        !positive(row.version) || row.version > 4294967295 || !positive(row.profileRevision) || !positive(row.profileVersion) || row.profileVersion > 4294967295 || !hash(row.profileContentSha256)) invalid()
    Object.freeze(row)
  }
  if (value.vessels.Ship.id === value.vessels.Tug.id || value.vessels.Ship.companyId !== value.vessels.Tug.companyId) invalid()
  Object.freeze(value.vessels)
  return Object.freeze(value) as Readonly<{ schemaVersion: string; runId: string; tenantId: string; writerActorId: string; vessels: Record<'Ship' | 'Tug', VesselObservation> }>
}
export type QualifiedPlan = Readonly<{ schemaVersion: 'poseidon.qualified-owning.plan.v1'; runId: string; tenantId: string; shipId: string; tugId: string;
  writerActorId: string; reviewerActorId: string; writerRoleId: string; reviewerRoleId: string; positionId: string; notBefore: string; expiresAt: string }>
export function qualifiedScopedId(runId: string, purpose: string): string {
  if (!qualifiedGuid(runId)) invalid()
  const bytes = createHash('sha1').update(Buffer.from(runId.replaceAll('-', ''), 'hex')).update('qualified-owning/v1/'+purpose, 'utf8').digest().subarray(0, 16)
  bytes[6] = (bytes[6] & 0x0f) | 0x50; bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = bytes.toString('hex')
  return hex.slice(0,8)+'-'+hex.slice(8,12)+'-'+hex.slice(12,16)+'-'+hex.slice(16,20)+'-'+hex.slice(20)
}
export function decodeQualifiedPlan(value: unknown): QualifiedPlan {
  if (!exact(value, ['schemaVersion', 'runId', 'tenantId', 'shipId', 'tugId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId', 'positionId', 'notBefore', 'expiresAt']) ||
      value.schemaVersion !== 'poseidon.qualified-owning.plan.v1' ||
      !['runId', 'tenantId', 'shipId', 'tugId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId', 'positionId'].every(key => qualifiedGuid(value[key])) ||
      value.tenantId !== '10000000-0000-0000-0000-000000000001' || value.shipId === value.tugId || seedVessels.includes(value.shipId) || seedVessels.includes(value.tugId) ||
      value.writerActorId === value.reviewerActorId || value.writerRoleId === value.reviewerRoleId || value.positionId !== qualifiedScopedId(value.runId, 'position') ||
      !['notBefore', 'expiresAt'].every(key => utc(value[key])) ||
      Date.parse(value.expiresAt) <= Date.parse(value.notBefore) || Date.parse(value.expiresAt)-Date.parse(value.notBefore) > 9*3600000) invalid()
  return Object.freeze(value) as QualifiedPlan
}
export function decodeQualifiedFixture(value: unknown): QualifiedOwningFixture {
  if (!exact(value, ['schemaVersion', 'runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'shipId', 'tugId', 'expiresAt', 'vessels', 'rules', 'witnesses']) ||
      value.schemaVersion !== 'poseidon.qualified-owning.signed-fixture.v1' ||
      !['runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'shipId', 'tugId'].every(key => qualifiedGuid(value[key])) ||
      value.tenantId !== '10000000-0000-0000-0000-000000000001' || value.shipId === value.tugId || value.writerActorId === value.reviewerActorId ||
      !utc(value.expiresAt) ||
      !exact(value.rules, qualifiedModes) || !exact(value.witnesses, qualifiedKinds)) invalid()
  const vessels = decodeQualifiedVessels({ schemaVersion: 'poseidon.qualified-owning.observed-vessels.v1', runId: value.runId, tenantId: value.tenantId,
    writerActorId: value.writerActorId, vessels: value.vessels }, value as any)
  if (vessels.vessels.Ship.id !== value.shipId || vessels.vessels.Tug.id !== value.tugId) invalid()
  const ids = new Set<string>()
  for (const mode of qualifiedModes) {
    const rule = value.rules[mode]
    if (!exact(rule, ['id', 'revision', 'sourceId', 'sourceRevision', 'requirementId', 'requirementRevision', 'qualificationRevision', 'qualificationHash', 'baselineEvaluationId']) ||
        !['id', 'sourceId', 'requirementId', 'baselineEvaluationId'].every(key => qualifiedGuid(rule[key])) ||
        !['revision', 'sourceRevision', 'requirementRevision', 'qualificationRevision'].every(key => positive(rule[key])) || !hash(rule.qualificationHash)) invalid()
    for (const key of ['id', 'sourceId', 'requirementId', 'baselineEvaluationId']) { if (ids.has(rule[key])) invalid(); ids.add(rule[key]) }
    Object.freeze(rule)
  }
  for (const kind of qualifiedKinds) {
    const row = value.witnesses[kind], mode = qualifiedModeKinds.Tug.includes(kind) ? 'Tug' : kind === 'shipCargo' ? 'Ship' : 'Compliance'
    if (!exact(row, ['vesselId', 'id', 'revision', 'hash', 'controlId', 'controlRevision', 'operationId', 'baselineEvaluationId',
          'fileId', 'fileRevision', 'fileChecksum', 'verificationId', 'verificationRevision']) ||
        !['vesselId', 'id', 'controlId', 'operationId', 'baselineEvaluationId', 'fileId', 'verificationId'].every(key => qualifiedGuid(row[key])) ||
        !['revision', 'controlRevision', 'fileRevision', 'verificationRevision'].every(key => positive(row[key])) ||
        !hash(row.hash) || !hash(row.fileChecksum) || row.vesselId !== (mode === 'Tug' ? value.tugId : value.shipId) ||
        row.baselineEvaluationId !== value.rules[mode].baselineEvaluationId) invalid()
    for (const key of ['id', 'fileId', 'verificationId']) { if (ids.has(row[key])) invalid(); ids.add(row[key]) }
    Object.freeze(row)
  }
  for (const mode of qualifiedModes) {
    const rows = qualifiedModeKinds[mode].map(kind => value.witnesses[kind])
    if (rows.some(row => row.controlId !== rows[0].controlId || row.controlRevision !== rows[0].controlRevision)) invalid()
  }
  if (value.witnesses.tugService.operationId !== value.witnesses.tugOperation.operationId) invalid()
  Object.freeze(value.rules); Object.freeze(value.witnesses)
  return Object.freeze(value) as QualifiedOwningFixture
}

function privateMetadata(metadata: Stats, directory: boolean) {
  if (metadata.isSymbolicLink() || (directory ? !metadata.isDirectory() : !metadata.isFile() || metadata.nlink !== 1) ||
      process.platform !== 'win32' && ((metadata.mode & 0o077) !== 0 || typeof process.geteuid === 'function' && metadata.uid !== process.geteuid())) invalid()
}
const unchanged = (a: Stats, b: Stats) => a.dev === b.dev && a.ino === b.ino && a.nlink === b.nlink &&
  a.mode === b.mode && a.uid === b.uid && a.size === b.size && a.mtimeMs === b.mtimeMs && a.ctimeMs === b.ctimeMs
const same = (a: string, b: string) => process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b
export function qualifiedJson(raw: string): unknown {
  const value: unknown = JSON.parse(raw), stack: (Set<string> | null)[] = []
  for (const token of raw.matchAll(/"(?:\\.|[^"\\])*"|[{}\[\]]/g)) {
    const text = token[0]
    if (text === '{') stack.push(new Set())
    else if (text === '[') stack.push(null)
    else if (text === '}' || text === ']') stack.pop()
    else if (/^\s*:/.test(raw.slice(token.index! + text.length))) {
      const keys = stack.at(-1), key = JSON.parse(text) as string
      if (!keys || keys.has(key)) invalid()
      keys.add(key)
      if ((key === 'revision' || key.endsWith('Revision') || key === 'version' || key === 'profileVersion') && !/^\s*:\s*(?:null|[1-9][0-9]*)(?=\s*[,}\]])/.test(raw.slice(token.index! + text.length))) invalid()
    }
  }
  return value
}
export function readQualifiedPrivate(directory: string, name: string, maximum = 65536): any {
  try {
    if (!isAbsolute(directory) || directory.length > 32768 || !/^[a-z][a-z0-9-]*\.json$/.test(name) || maximum < 1 || maximum > 2097152) invalid()
    const root = resolve(directory), path = join(root, name), child = relative(root, path)
    if (!child || isAbsolute(child) || child.startsWith('..' + sep) || !same(realpathSync(root), root) || !same(realpathSync(path), path)) invalid()
    const parent = lstatSync(root); privateMetadata(parent, true)
    const before = lstatSync(path); privateMetadata(before, false)
    if (before.size < 1 || before.size > maximum) invalid()
    const file = openSync(path, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0) | (constants.O_NONBLOCK ?? 0))
    try {
      const opened = fstatSync(file); privateMetadata(opened, false)
      if (!unchanged(before, opened)) invalid()
      const buffer = Buffer.alloc(maximum + 1)
      let length = 0, count: number
      while (length < buffer.length && (count = readSync(file, buffer, length, buffer.length - length, length)) > 0) length += count
      const after = fstatSync(file), current = lstatSync(path), parentAfter = lstatSync(root)
      privateMetadata(after, false); privateMetadata(current, false); privateMetadata(parentAfter, true)
      if (length !== opened.size || length > maximum || !unchanged(opened, after) || !unchanged(before, current) ||
          parent.dev !== parentAfter.dev || parent.ino !== parentAfter.ino || !same(realpathSync(path), path) || !same(realpathSync(root), root) ||
          length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) invalid()
      return qualifiedJson(new TextDecoder('utf-8', { fatal: true }).decode(buffer.subarray(0, length)))
    } finally { closeSync(file) }
  } catch { invalid() }
}
export function loadQualifiedFixture(environment: Record<string, string | undefined> = process.env): QualifiedOwningFixture {
  try {
    labRuntimeFromEnvironment(environment) // Selected immutable endpoint/config generation is mandatory.
    const root = environment.POSEIDON_LAB_PRIVATE, file = environment.POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE
    if (!root || !file || !isAbsolute(file) || !same(resolve(file), join(resolve(root), 'qualified-owning-fixture.json'))) invalid()
    const fixture = decodeQualifiedFixture(readQualifiedPrivate(root, 'qualified-owning-fixture.json'))
    const base = readQualifiedPrivate(root, 'source-bundle-plan.json'), plan = decodeQualifiedPlan(readQualifiedPrivate(root, 'qualified-owning-plan.json'))
    const vessels = decodeQualifiedVessels(readQualifiedPrivate(root, 'qualified-owning-observed-vessels.json'), base)
    if (base.schemaVersion !== 'poseidon.bundle-fixture-plan.v1' || plan.schemaVersion !== 'poseidon.qualified-owning.plan.v1' ||
        (['runId', 'tenantId', 'writerActorId', 'reviewerActorId'] as const).some(key => base[key] !== fixture[key] || plan[key] !== base[key]) ||
        (['writerRoleId', 'reviewerRoleId'] as const).some(key => base[key] !== plan[key]) ||
        base.vesselId !== seedVessels[0] || plan.shipId !== fixture.shipId || plan.tugId !== fixture.tugId ||
        (['Ship', 'Tug'] as const).some(kind => Object.keys(vessels.vessels[kind]).some(key => vessels.vessels[kind][key as keyof VesselObservation] !== fixture.vessels[kind][key as keyof VesselObservation])) ||
        plan.expiresAt !== fixture.expiresAt || Date.now() >= Date.parse(fixture.expiresAt) || Date.now() < Date.parse(plan.notBefore)) invalid()
    return fixture
  } catch { invalid() }
}

export async function qualifiedRead(page: QualifiedPage, path: string): Promise<any> {
  try {
    const response = await page.request.get(path)
    if (response.status() !== 200) invalid()
    return await response.json()
  } catch { invalid() }
}
// Traverse the complete bounded list. Never hide duplicate qualifying candidates
// by filtering to an expected ID before testing the original positive predicate.
export async function qualifiedList(page: QualifiedPage, path: string): Promise<any[]> {
  const rows: any[] = [], seen = new Set<string>()
  for (let offset = 0; offset < 5000; offset += 25) {
    const value = await qualifiedRead(page, path + (path.includes('?') ? '&' : '?') + 'offset=' + offset)
    if (!object(value) || !Array.isArray(value.items) || value.items.length > 25 || typeof value.hasMore !== 'boolean' ||
        value.offset !== undefined && value.offset !== offset || value.hasMore && value.items.length !== 25) invalid()
    for (const row of value.items) {
      if (!object(row)) invalid()
      const id = row.id ?? row.current?.id
      if (!qualifiedGuid(id) || seen.has(id)) invalid()
      seen.add(id); rows.push(row)
    }
    if (!value.hasMore) return rows
  }
  invalid()
}
export async function requireQualifiedContext(page: QualifiedPage, input: QualifiedOwningFixture, mode: QualifiedMode) {
  try {
    const fixture = decodeQualifiedFixture(input), vesselId = mode === 'Tug' ? fixture.tugId : fixture.shipId
    if (Date.now() >= Date.parse(fixture.expiresAt)) invalid()
    const owner = async () => {
      const user = await qualifiedRead(page, '/api/v1/auth/session')
      if (user.authenticated !== true || user.setupRequired !== false || user.canRead !== true ||
          user.actorId !== fixture.writerActorId || user.tenantId !== fixture.tenantId) invalid()
    }
    await owner()
    const vessel = await qualifiedRead(page, '/api/v1/vessels/' + vesselId)
    const vesselPin = fixture.vessels[mode === 'Tug' ? 'Tug' : 'Ship']
    if (vessel.id !== vesselId || vessel.kind !== vesselPin.kind || vessel.code !== vesselPin.code || vessel.companyId !== vesselPin.companyId) invalid()
    const profile = await qualifiedRead(page, '/api/v1/vessels/'+vesselId+'/regulatory-profile')
    if (profile.tenantId !== fixture.tenantId || profile.vessel?.id !== vesselId || profile.revision !== vesselPin.profileRevision || qualifiedContentHash(profile.content) !== vesselPin.profileContentSha256) invalid()
    const allRules = await qualifiedList(page, '/api/v1/rules'), rules = allRules.filter(row => row.current?.rule?.ruleId === qualifiedRuleNames[mode])
    const pin = fixture.rules[mode]
    if (rules.length !== 1 || rules[0].current.id !== pin.id || rules[0].current.revision !== pin.revision ||
        ['sourceId', 'sourceRevision', 'requirementId', 'requirementRevision'].some(key => rules[0].current[key] !== pin[key as keyof Rule])) invalid()
    const source = await qualifiedRead(page, '/api/v1/regulatory-source-governance/' + pin.id)
    if (source.items?.[0]?.state !== 'IN_FORCE' || source.items[0].ruleId !== pin.id || source.items[0].ruleRevision !== pin.revision ||
        source.items[0].actorId !== fixture.writerActorId || source.items[0].revision !== pin.qualificationRevision || source.items[0].hash !== pin.qualificationHash) invalid()
    const baseline = await qualifiedRead(page, '/api/v1/applicability/evaluations/' + pin.baselineEvaluationId + '/current')
    if (baseline.current !== true) invalid()
    const prefix = mode === 'Compliance' ? 'FICTIONAL signed-host compliance ' : 'FICTIONAL signed-host owning '
    const allRows = await qualifiedList(page, '/api/v1/evidence?vesselId=' + vesselId)
    const rows = allRows.filter(row => row.content?.title?.startsWith(prefix) && row.verification === 'VERIFIED')
    const selected: Record<string, any> = {}
    for (const kind of qualifiedModeKinds[mode]) {
      const field = qualifiedFields[kind], matches = rows.filter(row => row.content?.[field]), expected = fixture.witnesses[kind]
      if (matches.length !== 1) invalid()
      const row = matches[0]
      if (row.id !== expected.id || row.vesselId !== vesselId || row.revision !== expected.revision || row.hash !== expected.hash ||
          row.file?.id !== expected.fileId || row.file?.revision !== expected.fileRevision || row.file?.checksum !== expected.fileChecksum ||
          !row.requirements?.some((p: any) => p.ruleId === pin.id && p.ruleRevision === pin.revision && p.sourceId === pin.sourceId && p.sourceRevision === pin.sourceRevision && p.requirementId === pin.requirementId && p.requirementRevision === pin.requirementRevision) ||
          !row.links?.some((p: any) => p.kind === 'Control' && p.id === expected.controlId && p.revision === expected.controlRevision)) invalid()
      const trace = await qualifiedRead(page, `/api/v1/regulatory-trace?vesselId=${vesselId}&evidenceId=${row.id}&evidenceRevision=${row.revision}&controlId=${expected.controlId}&controlRevision=${expected.controlRevision}`)
      const event = trace.items?.find((item: any) => item.id === expected.verificationId && item.revision === expected.verificationRevision)
      if (trace.historical !== false || !event || event.kind !== 'Verification' || event.state !== 'VERIFIED' || event.currentState !== 'VERIFIED' ||
          event.actorId !== fixture.reviewerActorId || event.evaluationId !== expected.baselineEvaluationId || event.operationId !== expected.operationId) invalid()
      selected[field] = row
    }
    await owner()
    return { fixture, vessel, rule: rules[0], rows: selected,
      ruleOffset: Math.floor(allRules.findIndex(row => row.current.id === pin.id) / 25) * 25,
      evidenceOffsets: Object.fromEntries(Object.entries(selected).map(([field, row]) => [field, Math.floor(allRows.findIndex(item => item.id === row.id) / 25) * 25])) }
  } catch { invalid() } // No private response/parser/path values in diagnostics.
}
