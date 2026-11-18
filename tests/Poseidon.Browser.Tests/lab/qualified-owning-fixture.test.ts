// Pure private-boundary/actual-response-contract unit sources. No genuine host,
// provider, browser or authorization is manufactured by these synthetic transports.
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { createHash, randomUUID } from 'node:crypto'
import { chmodSync, linkSync, mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { decodeQualifiedFixture, decodeQualifiedPlan, decodeQualifiedVessels, loadQualifiedFixture, qualifiedContentHash, qualifiedFields, qualifiedJson, qualifiedKinds, qualifiedList, qualifiedModeKinds, qualifiedScopedId, qualifiedVesselCode,
  qualifiedModes, qualifiedRuleNames, readQualifiedPrivate, requireQualifiedContext } from '../register-specs/qualified-owning-fixture.ts'
import type { QualifiedMode, QualifiedOwningFixture } from '../register-specs/qualified-owning-fixture.ts'
import { labConfigurationIdentity, labEndpoints, type OwnedLabRuntime } from './lab-runtime.ts'

function fixture(): QualifiedOwningFixture {
  const rules: Record<string, any> = {}, witnesses: Record<string, any> = {}, controls = Object.fromEntries(qualifiedModes.map(mode => [mode, randomUUID()]))
  for (const mode of qualifiedModes) rules[mode] = { id: randomUUID(), revision: 1, sourceId: randomUUID(), sourceRevision: 1,
    requirementId: randomUUID(), requirementRevision: 1, qualificationRevision: 3, qualificationHash: 'A'.repeat(64), baselineEvaluationId: randomUUID() }
  const shipId = randomUUID(), tugId = randomUUID(), runId = randomUUID(), companyId = randomUUID()
  const vessels = Object.fromEntries(([['Ship', shipId], ['Tug', tugId]] as const).map(([kind, id]) => [kind, {
    id, companyId, kind, code: qualifiedVesselCode(runId, kind), version: 1, profileRevision: 1, profileVersion: 1, profileContentSha256: qualifiedContentHash({}) }]))
  for (const kind of qualifiedKinds) {
    const mode = qualifiedModeKinds.Tug.includes(kind) ? 'Tug' : kind === 'shipCargo' ? 'Ship' : 'Compliance'
    witnesses[kind] = { vesselId: mode === 'Tug' ? tugId : shipId, id: randomUUID(), revision: 2, hash: 'B'.repeat(64), controlId: controls[mode], controlRevision: 1,
      operationId: randomUUID(), baselineEvaluationId: rules[mode].baselineEvaluationId, fileId: randomUUID(), fileRevision: 1, fileChecksum: 'C'.repeat(64),
      verificationId: randomUUID(), verificationRevision: 2 }
  }
  witnesses.tugOperation.operationId = witnesses.tugService.operationId
  return { schemaVersion: 'poseidon.qualified-owning.signed-fixture.v1', runId, tenantId: '10000000-0000-0000-0000-000000000001',
    writerActorId: randomUUID(), reviewerActorId: randomUUID(), shipId, tugId, expiresAt: new Date(Date.now()+3600000).toISOString(), vessels, rules, witnesses } as QualifiedOwningFixture
}
const clone = <T,>(value: T): T => JSON.parse(JSON.stringify(value))
function transport(f: QualifiedOwningFixture, mode: QualifiedMode = 'Ship') {
  const vessel = mode === 'Tug' ? f.tugId : f.shipId
  const state = { user: { authenticated: true, setupRequired: false, canRead: true, actorId: f.writerActorId, tenantId: f.tenantId },
    status: 200, paths: [] as string[], rows: [] as any[], rules: [] as any[], changedSource: false, profileRevision: 1, profileContent: {} as any }
  state.rules = qualifiedModes.map(mode => ({ current: { ...f.rules[mode], rule: { ruleId: qualifiedRuleNames[mode] } } }))
  state.rows = qualifiedKinds.filter(kind => f.witnesses[kind].vesselId === vessel).map(kind => {
    const witness = f.witnesses[kind], m = qualifiedModeKinds.Tug.includes(kind) ? 'Tug' : kind === 'shipCargo' ? 'Ship' : 'Compliance', rule = f.rules[m]
    return { id: witness.id, vesselId: vessel, revision: witness.revision, hash: witness.hash, verification: 'VERIFIED',
      content: { title: (m === 'Compliance' ? 'FICTIONAL signed-host compliance ' : 'FICTIONAL signed-host owning ')+kind, [qualifiedFields[kind]]: {} },
      file: { id: witness.fileId, revision: witness.fileRevision, checksum: witness.fileChecksum },
      links: [{ kind: 'Control', id: witness.controlId, revision: witness.controlRevision }], requirements: [{ ruleId: rule.id, ruleRevision: rule.revision,
        sourceId: rule.sourceId, sourceRevision: rule.sourceRevision, requirementId: rule.requirementId, requirementRevision: rule.requirementRevision }] }
  })
  const page = { request: { get: async (path: string) => {
    state.paths.push(path)
    const url = new URL(path, 'https://localhost:5443'), offset = Number(url.searchParams.get('offset') ?? 0)
    let value: any
    if (url.pathname === '/api/v1/auth/session') value = state.user
    else if (url.pathname === '/api/v1/vessels/'+vessel) value = f.vessels[mode === 'Tug' ? 'Tug' : 'Ship']
    else if (url.pathname === '/api/v1/vessels/'+vessel+'/regulatory-profile') value = { tenantId: f.tenantId, vessel: { id: vessel }, revision: state.profileRevision, content: state.profileContent }
    else if (url.pathname === '/api/v1/rules' || url.pathname === '/api/v1/evidence') {
      const rows = url.pathname === '/api/v1/rules' ? state.rules : state.rows
      value = { items: rows.slice(offset, offset+25), offset, hasMore: rows.length > offset+25 }
    } else if (url.pathname.startsWith('/api/v1/regulatory-source-governance/')) value = { items: [{ state: 'IN_FORCE', ruleId: f.rules[mode].id, ruleRevision: f.rules[mode].revision,
      actorId: f.writerActorId, revision: f.rules[mode].qualificationRevision,
      hash: state.changedSource ? 'D'.repeat(64) : f.rules[mode].qualificationHash }] }
    else if (url.pathname.endsWith('/current')) value = { current: true }
    else if (url.pathname === '/api/v1/regulatory-trace') {
      const witness = Object.values(f.witnesses).find(row => row.id === url.searchParams.get('evidenceId'))!
      value = { historical: false, complete: false, disposition: 'REVIEW_REQUIRED', items: [{ id: witness.verificationId, revision: witness.verificationRevision,
        kind: 'Verification', state: 'VERIFIED', currentState: 'VERIFIED', actorId: f.reviewerActorId, evaluationId: witness.baselineEvaluationId, operationId: witness.operationId }] }
    } else throw new Error('unexpected synthetic unit route')
    return { status: () => state.status, json: async () => value }
  } } }
  return { state, page }
}

test('closed descriptor accepts six distinct typed witnesses and rejects excess/missing/malformed/foreign/reused tuples', () => {
  const f = fixture()
  assert.equal(decodeQualifiedFixture(clone(f)).witnesses.shipCargo.id, f.witnesses.shipCargo.id)
  const mutations: ((value: any) => void)[] = [v => { v.extra = true }, v => { delete v.witnesses.shipMsmd },
    v => { v.runId += '\n' }, v => { v.tenantId = randomUUID() }, v => { v.writerActorId = v.reviewerActorId },
    v => { v.rules.Ship.revision = true }, v => { v.rules.Tug.sourceId = v.rules.Ship.sourceId },
    v => { v.witnesses.tugWinch.vesselId = v.shipId }, v => { v.witnesses.shipMsmd.id = v.witnesses.shipClass.id },
    v => { v.witnesses.shipCargo.baselineEvaluationId = randomUUID() }, v => { v.witnesses.shipCargo.fileChecksum = 'bad' }]
  for (const mutate of mutations) { const changed = clone(f); mutate(changed); assert.throws(() => decodeQualifiedFixture(changed), /qualified_owning_fixture_invalid/) }
})

test('plan binds distinct actual owners/roles and the exact run-scoped Position UUID without expanding the source-bundle schema', () => {
  const f = fixture(), plan = { schemaVersion: 'poseidon.qualified-owning.plan.v1', runId: f.runId, tenantId: f.tenantId, shipId: f.shipId, tugId: f.tugId,
    writerActorId: f.writerActorId, reviewerActorId: f.reviewerActorId, writerRoleId: randomUUID(), reviewerRoleId: randomUUID(),
    positionId: qualifiedScopedId(f.runId, 'position'), notBefore: new Date(Date.now()-60000).toISOString(), expiresAt: f.expiresAt }
  assert.equal(decodeQualifiedPlan(plan).positionId, plan.positionId)
  for (const changed of [{ ...plan, runId: randomUUID() }, { ...plan, positionId: randomUUID() },
    { ...plan, writerRoleId: plan.reviewerRoleId }, { ...plan, tenantId: randomUUID() }, { ...plan, extra: true }])
    assert.throws(() => decodeQualifiedPlan(changed), /qualified_owning_fixture_invalid/)
})

test('observed vessels reject seed adoption, wrong tenant/actor/type/company/code and nonintegral or missing profile lineage', () => {
  const f = fixture(), expected = { runId: f.runId, tenantId: f.tenantId, writerActorId: f.writerActorId }
  const value = { schemaVersion: 'poseidon.qualified-owning.observed-vessels.v1', ...expected, vessels: clone(f.vessels) }
  assert.equal(decodeQualifiedVessels(clone(value), expected).vessels.Ship.id, f.shipId)
  const changes: ((v: any) => void)[] = [v => { v.writerActorId = randomUUID() }, v => { v.tenantId = randomUUID() },
    v => { v.vessels.Ship.id = '30000000-0000-0000-0000-000000000001' }, v => { v.vessels.Ship.kind = 'Tug' },
    v => { v.vessels.Tug.companyId = randomUUID() }, v => { v.vessels.Tug.id = v.vessels.Ship.id },
    v => { v.vessels.Ship.code = 'OPERATOR' }, v => { v.vessels.Ship.version = 1.5 }, v => { v.vessels.Ship.profileRevision = 0 },
    v => { v.vessels.Ship.profileContentSha256 = 'bad' }]
  for (const change of changes) { const altered = clone(value); change(altered); assert.throws(() => decodeQualifiedVessels(altered, expected), /qualified_owning_fixture_invalid/) }
  for (const field of ['version', 'profileRevision', 'profileVersion'])
    for (const lexeme of ['1.0', '1e0']) assert.throws(() => qualifiedJson(JSON.stringify(value).replace('"'+field+'":1', '"'+field+'":'+lexeme)), /qualified_owning_fixture_invalid/)
})

test('JSON rejects duplicate escaped keys and non-native revision lexemes', () => {
  const raw = JSON.stringify(fixture())
  for (const lexeme of ['1.0', '1e0', '-1', '9007199254740992']) {
    const changed = raw.replace('"revision":1', '"revision":'+lexeme)
    assert.throws(() => decodeQualifiedFixture(qualifiedJson(changed)), /qualified_owning_fixture_invalid/)
  }
  assert.throws(() => qualifiedJson('{"runId":1,"r\\u0075nId":2}'), /qualified_owning_fixture_invalid/)
})

test('private reader rejects BOM/invalid UTF8/oversize/foreign path and changed private boundaries without retaining parser data', () => {
  const root = mkdtempSync(join(tmpdir(), 'poseidon-qualified-private-')), name = 'qualified-owning-fixture.json', path = join(root, name)
  try {
    chmodSync(root, 0o700)
    const raw = Buffer.from(JSON.stringify(fixture()))
    writeFileSync(path, raw, { mode: 0o600 })
    assert.equal(readQualifiedPrivate(root, name).schemaVersion, 'poseidon.qualified-owning.signed-fixture.v1')
    for (const bytes of [Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), raw]), Buffer.from([0xff]), Buffer.alloc(65537)]) {
      writeFileSync(path, bytes)
      assert.throws(() => readQualifiedPrivate(root, name), /^Error: qualified_owning_fixture_invalid$/)
    }
    assert.throws(() => readQualifiedPrivate(root, '../outside.json'), /qualified_owning_fixture_invalid/)
    if (process.platform !== 'win32') {
      writeFileSync(path, raw); chmodSync(path, 0o644)
      assert.throws(() => readQualifiedPrivate(root, name), /qualified_owning_fixture_invalid/)
      chmodSync(path, 0o600); chmodSync(root, 0o755)
      assert.throws(() => readQualifiedPrivate(root, name), /qualified_owning_fixture_invalid/)
      chmodSync(root, 0o700)
      const alias = join(root, 'linked.json'); linkSync(path, alias)
      assert.throws(() => readQualifiedPrivate(root, name), /qualified_owning_fixture_invalid/)
    } else {
      const child = join(root, 'target'); mkdirSync(child)
      const alias = join(root, 'alias'); symlinkSync(child, alias, 'junction')
      writeFileSync(join(child, name), raw, { mode: 0o600 })
      assert.throws(() => readQualifiedPrivate(alias, name), /qualified_owning_fixture_invalid/)
    }
  } finally { rmSync(root, { recursive: true, force: true }) }
})

test('loader binds the additive descriptor to the selected immutable config, run plan and exact path without legacy fallback', () => {
  const root = mkdtempSync(join(tmpdir(), 'poseidon-qualified-loader-')), f = fixture()
  const runtime: OwnedLabRuntime = { schemaVersion: 'poseidon.lab.owned-runtime.v1', runId: f.runId, appPort: 15443, providerPort: 18443, databasePort: 25432 }
  const put = (name: string, value: unknown) => writeFileSync(join(root, name), JSON.stringify(value), { mode: 0o600 })
  const plan = { schemaVersion: 'poseidon.qualified-owning.plan.v1', runId: f.runId, tenantId: f.tenantId, shipId: f.shipId, tugId: f.tugId,
    writerActorId: f.writerActorId, reviewerActorId: f.reviewerActorId, writerRoleId: randomUUID(), reviewerRoleId: randomUUID(),
    positionId: qualifiedScopedId(f.runId, 'position'), notBefore: new Date(Date.now()-60000).toISOString(), expiresAt: f.expiresAt }
  const base = { schemaVersion: 'poseidon.bundle-fixture-plan.v1', runId: f.runId, ownedRuntime: runtime,
    tenantId: f.tenantId, vesselId: '30000000-0000-0000-0000-000000000001', writerActorId: f.writerActorId, reviewerActorId: f.reviewerActorId,
    writerRoleId: plan.writerRoleId, reviewerRoleId: plan.reviewerRoleId }
  const endpoints = labEndpoints(runtime)
  const config = { DevelopmentLab: { OwnedRuntime: runtime, OidcPort: runtime.providerPort, OidcCaPath: '/synthetic/ca.pem', WebRoot: '/synthetic/frontend/dist' },
    Authentication: { Oidc: { Authority: endpoints.authority, Issuer: endpoints.authority, ClientId: 'poseidon-local', ClientSecret: randomUUID(),
      Memberships: [{ Issuer: endpoints.authority, Subject: randomUUID(), ActorId: f.writerActorId, TenantId: f.tenantId }] } },
    Kestrel: { Endpoints: { LocalLab: { Url: endpoints.appOrigin } } }, ConnectionStrings: { Poseidon: 'Host=localhost;Port=25432;Database=synthetic' } }
  const file = 'source-bundle-config-000001.json', raw = JSON.stringify(config)
  const env = { POSEIDON_LAB_PRIVATE: root, POSEIDON_LAB_RUNTIME_FILE: join(root, 'owned-runtime.json'), POSEIDON_LAB_CONFIG: join(root, file),
    POSEIDON_REGISTER_URL: endpoints.appOrigin, POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE: join(root, 'qualified-owning-fixture.json') }
  try {
    chmodSync(root, 0o700)
    put('owned-runtime.json', runtime); put('source-bundle-plan.json', base); put('appsettings.json', config)
    put(file, config)
    put('source-bundle-host-000001.json', { schemaVersion: 'poseidon.bundle.private-config-generation.v1', generation: 1, configFile: file,
      configSha256: createHash('sha256').update(raw).digest('hex'), previousManifestSha256: null, immutableIdentitySha256: labConfigurationIdentity(config, runtime) })
    put('qualified-owning-plan.json', plan); put('qualified-owning-fixture.json', f)
    put('qualified-owning-observed-vessels.json', { schemaVersion: 'poseidon.qualified-owning.observed-vessels.v1', runId: f.runId, tenantId: f.tenantId, writerActorId: f.writerActorId, vessels: f.vessels })
    assert.equal(loadQualifiedFixture(env).runId, f.runId)
    for (const altered of [{ ...env, POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE: join(root, 'other.json') },
      { ...env, POSEIDON_LAB_CONFIG: undefined }, { ...env, POSEIDON_REGISTER_URL: 'https://localhost:5443' }])
      assert.throws(() => loadQualifiedFixture(altered), /qualified_owning_fixture_invalid/)
    put('qualified-owning-fixture.json', { ...f, runId: randomUUID() })
    assert.throws(() => loadQualifiedFixture(env), /qualified_owning_fixture_invalid/)
    put('qualified-owning-fixture.json', f); put('source-bundle-plan.json', { ...base, writerActorId: randomUUID() })
    assert.throws(() => loadQualifiedFixture(env), /qualified_owning_fixture_invalid/)
  } finally { rmSync(root, { recursive: true, force: true }) }
})

test('actual-context validation consumes the complete pages and allows verification without inventing a recorded decision', async () => {
  const f = fixture()
  for (const mode of qualifiedModes) {
    const t = transport(f, mode)
    t.state.rows.unshift(...Array.from({ length: 25 }, () => ({ id: randomUUID(), verification: 'UNVERIFIED', content: {} })))
    const context = await requireQualifiedContext(t.page, f, mode)
    assert.equal(context.rule.current.id, f.rules[mode].id)
    assert.equal(context.evidenceOffsets[qualifiedFields[qualifiedModeKinds[mode][0]]], 25)
    assert.ok(t.state.paths.some(path => path.includes('offset=25')))
    assert.equal(t.state.paths.filter(path => path === '/api/v1/auth/session').length, 2)
    assert.ok(t.state.paths.some(path => path === '/api/v1/vessels/'+context.vessel.id+'/regulatory-profile'))
    assert.ok(!t.state.paths.some(path => path.includes('30000000-0000-0000-0000-000000000001')))
    assert.ok(!t.state.paths.some(path => path.includes('30000000-0000-0000-0000-000000000002')))
  }
})

test('a duplicate qualifying record on a later page is not hidden by filtering first to the descriptor ID', async () => {
  const f = fixture(), t = transport(f), duplicate = { ...t.state.rows[0], id: randomUUID() }
  t.state.rows.unshift(...Array.from({ length: 24 }, () => ({ id: randomUUID(), verification: 'UNVERIFIED', content: {} })))
  t.state.rows.push(duplicate)
  await assert.rejects(requireQualifiedContext(t.page, f, 'Ship'), /qualified_owning_fixture_invalid/)
})

test('wrong current actor/tenant/access, stale source or typed tuple is a hard failure', async () => {
  const f = fixture()
  const mutations: ((state: ReturnType<typeof transport>['state']) => void)[] = [s => { s.user.actorId = randomUUID() },
    s => { s.user.tenantId = randomUUID() }, s => { s.user.canRead = false }, s => { s.status = 403 }, s => { s.changedSource = true },
    s => { s.rows[0].revision++ }, s => { s.rows[0].file.checksum = 'D'.repeat(64) }, s => { s.rows[0].verification = 'UNVERIFIED' },
    s => { s.profileRevision++ }, s => { s.profileContent = { declaration: 'changed' } },
    s => { s.rows[0].content.title = 'operator content' }, s => { s.rows[0].requirements[0].sourceRevision++ }]
  for (const mutate of mutations) { const t = transport(f); mutate(t.state); await assert.rejects(requireQualifiedContext(t.page, f, 'Ship'), /qualified_owning_fixture_invalid/) }
  await assert.rejects(requireQualifiedContext(transport(f).page, { ...f, expiresAt: new Date(0).toISOString() }, 'Ship'), /qualified_owning_fixture_invalid/)
})

test('bounded list cannot accept partial paging, repeated IDs or missing actual HTTP result', async () => {
  const id = randomUUID()
  for (const value of [{ items: [{ id }], offset: 0, hasMore: true }, { items: [{ id }, { id }], offset: 0, hasMore: false }]) {
    await assert.rejects(qualifiedList({ request: { get: async () => ({ status: () => 200, json: async () => value }) } }, '/api/v1/evidence'), /qualified_owning_fixture_invalid/)
  }
  await assert.rejects(qualifiedList({ request: { get: async () => ({ status: () => 404, json: async () => { throw new Error('must not read denied payload') } }) } }, '/api/v1/evidence'), /qualified_owning_fixture_invalid/)
})
