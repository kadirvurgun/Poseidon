// QUALIFIED-OWNING-SIGNED-FIXTURE-01: actual owned HTTPS/OIDC commands only.
// FICTIONAL test declarations are not source adoption or maritime approvals.
import { createHash, randomUUID } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { existsSync, realpathSync, writeFileSync } from 'node:fs'
import { relative, resolve, sep } from 'node:path'
import type { Page } from '@playwright/test'
import { strictContext } from '../tests/Poseidon.Browser.Tests/lab/strict-browser.ts'
import { login } from '../tests/Poseidon.Browser.Tests/lab/journeys.ts'
import { labRuntimeFromEnvironment } from '../tests/Poseidon.Browser.Tests/lab/lab-runtime.ts'
import { canonical, FixtureJournal } from './bundle_fixture_journal.ts'
import { decodeQualifiedFixture, decodeQualifiedPlan, decodeQualifiedVessels, qualifiedContentHash, qualifiedFields, qualifiedGuid, qualifiedKinds, qualifiedModeKinds, qualifiedModes, qualifiedVesselCode,
  qualifiedRuleNames, qualifiedList, readQualifiedPrivate, requireQualifiedContext } from '../tests/Poseidon.Browser.Tests/register-specs/qualified-owning-fixture.ts'
import type { QualifiedMode, QualifiedOwningFixture } from '../tests/Poseidon.Browser.Tests/register-specs/qualified-owning-fixture.ts'

async function provision() {
  const phase = process.argv[2]
  if (!['vessels', 'sources', 'witnesses', 'verify'].includes(phase) || process.argv.length !== 3) throw new Error('qualified_fixture_phase_required')
  const runtime = labRuntimeFromEnvironment(), root = realpathSync(process.env.POSEIDON_LAB_PRIVATE!)
  if (root !== realpathSync(resolve(import.meta.dirname, '../.runtime/oidc-lab'))) throw new Error('qualified_fixture_boundary')
  const base = readQualifiedPrivate(root, 'source-bundle-plan.json')
  // The vessels phase runs before the immutable12-field plan exists. Its journal
  // is bound to the original source bundle/run/provider; no seed vessel is edited.
  const plan: any = phase === 'vessels' ? { ...base, shipId: base.vesselId, tugId: '30000000-0000-0000-0000-000000000002' }
    : decodeQualifiedPlan(readQualifiedPrivate(root, 'qualified-owning-plan.json'))
  const configPath = process.env.POSEIDON_LAB_CONFIG!
  if (resolve(configPath) !== realpathSync(configPath) || !configPath.startsWith(root + sep) || relative(root, configPath).includes(sep)) throw new Error('qualified_fixture_boundary')
  const config = readQualifiedPrivate(root, configPath.slice(root.length + 1), 2097152)
  if (base.schemaVersion !== 'poseidon.bundle-fixture-plan.v1' || base.tenantId !== '10000000-0000-0000-0000-000000000001' || base.vesselId !== '30000000-0000-0000-0000-000000000001' ||
      (['runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId'] as const).some(key => plan[key] !== base[key]) ||
      !(['runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId'] as const).every(key => qualifiedGuid(plan[key])) ||
      plan.writerActorId === plan.reviewerActorId || phase !== 'vessels' && (Date.now() < Date.parse(plan.notBefore) || Date.now() >= Date.parse(plan.expiresAt))) throw new Error('qualified_fixture_plan_invalid')
  const report = { schemaVersion: 'poseidon.qualified-owning.preparation.v1', phase, stage: 'begin', status: 'RUNNING', accepted: false,
    httpChecks: [] as { stage: string; status: number }[] }
  function fail(): never { throw new Error('qualified_fixture_actual_contract_invalid') }
  const persist = (name: string, value: unknown) => {
    if (!/^[a-z][a-z0-9-]*\.json$/.test(name)) fail()
    if (existsSync(resolve(root, name))) {
      if (canonical(readQualifiedPrivate(root, name, 2097152)) !== canonical(value)) fail()
      return
    }
    const temporary = resolve(root, '.' + randomUUID() + '.json'), target = resolve(root, name)
    writeFileSync(temporary, JSON.stringify(value), { flag: 'wx', mode: 0o600, flush: true })
    const result = spawnSync(process.env.POSEIDON_LAB_PYTHON!, [resolve(import.meta.dirname, 'bundle_private.py'), 'publish-temp', '--source', temporary, '--target', target],
      { stdio: 'ignore', windowsHide: true, timeout: 30000 })
    if (result.status !== 0) fail()
  }
  const journal = new FixtureJournal(root, { metadata: base, origin: runtime.appOrigin, authority: runtime.authority }, persist, undefined,
    'qualified-owning', name => readQualifiedPrivate(root, name, 2097152))
  type Who = 'writer' | 'reviewer'
  const actor = (who: Who) => who === 'writer' ? plan.writerActorId : plan.reviewerActorId
  const contexts: Awaited<ReturnType<typeof strictContext>>[] = []
  let preparedFixture: QualifiedOwningFixture | null = null
  async function owner(page: Page, who: Who) {
    const response = await page.request.get('/api/v1/auth/session')
    report.httpChecks.push({ stage: report.stage + '_session', status: response.status() })
    if (response.status() !== 200) fail()
    const value = await response.json()
    if (value.authenticated !== true || value.setupRequired !== false || value.canRead !== true || value.actorId !== actor(who) ||
        value.tenantId !== plan.tenantId || typeof value.csrfToken !== 'string' || value.csrfToken.length < 16) fail()
    return value
  }
  async function signed(who: Who): Promise<Page> {
    const owned = await strictContext({ baseURL: runtime.appOrigin }); contexts.push(owned)
    const page = await owned.context.newPage(); page.setDefaultTimeout(15000)
    report.stage = who + '_actual_login'
    await login(page, who === 'writer' ? 'lab-manager' : 'lab-reviewer', '/app/today')
    await owner(page, who)
    for (const [id, kind] of [[plan.shipId, 'Ship'], [plan.tugId, 'Tug']]) {
      const value = await read(page, who, '/api/v1/vessels/' + id)
      if (value.id !== id || value.kind !== kind) fail()
    }
    return page
  }
  async function read(page: Page, who: Who, path: string): Promise<any> {
    await owner(page, who)
    const response = await page.request.get(path); report.httpChecks.push({ stage: report.stage, status: response.status() })
    if (response.status() !== 200) fail()
    const value = await response.json(); await owner(page, who)
    return value
  }
  async function command(page: Page, who: Who, stage: string, path: string, make: () => Promise<any> | any,
    verify: (receipt: any, body: any) => Promise<void>, method: 'POST' | 'PUT' = 'POST', idempotent = true) {
    report.stage = stage
    const previous = journal.load('qualified-owning-command-' + stage.replaceAll('_', '-') + '-intent.json') as any
    const identity = { actorId: actor(who), tenantId: plan.tenantId }
    if (previous && (previous.path !== path || previous.method !== method || canonical(previous.identity) !== canonical(identity))) fail()
    const body = previous ? previous.body : await make()
    return journal.command(stage, identity, method, path, body, idempotent, async intent => {
      const actual = await owner(page, who) // New actual session/CSRF immediately before every send.
      const headers = { 'X-CSRF-TOKEN': actual.csrfToken, 'Idempotency-Key': intent.key }
      const response = method === 'PUT' && path.endsWith('/bytes')
        ? await page.request.put(path, { headers: { ...headers, 'Content-Type': 'image/png' }, data: Buffer.from((intent.body as any).base64, 'base64') })
        : await page.request.fetch(path, { method, headers, data: intent.body })
      report.httpChecks.push({ stage, status: response.status() })
      const received = response.status() === 200 ? await response.json() : null
      await owner(page, who)
      return { status: response.status(), body: received }
    }, receipt => verify(receipt, body)) as Promise<any>
  }
  const match = (actual: any, expected: any, keys: string[]) => { if (!actual || keys.some(key => actual[key] !== expected[key])) fail() }
  async function graphReceipt(page: Page, who: Who, route: string, vessel: string, receipt: any) {
    const value = await read(page, who, `/api/v1/${route}/${receipt.id}/versions?vesselId=${vessel}&offset=0`)
    const row = value.items?.find((row: any) => row.revision === receipt.revision)
    match(row, receipt, ['id', 'vesselId', 'kind', 'revision', 'hash', 'lifecycle'])
  }
  const pin = (rule: any) => ({ requirementId: rule.requirementId, requirementRevision: rule.requirementRevision,
    sourceId: rule.sourceId, sourceRevision: rule.sourceRevision, ruleId: rule.id, ruleRevision: rule.revision })
  const vesselFor = (mode: QualifiedMode) => mode === 'Tug' ? plan.tugId : plan.shipId
  const ruleContract = '09.03:309afa07b393fb96556a0ea4ec70098b1e2b8648ab6daec181f5a81ce9ac8825'
  const prefixFor = (mode: QualifiedMode) => mode === 'Compliance' ? 'FICTIONAL signed-host compliance ' : 'FICTIONAL signed-host owning '
  const observedSources = () => readQualifiedPrivate(root, 'qualified-owning-observed-sources.json')

  async function vessels() {
    const writer = await signed('writer'), observed: Record<string, any> = {}
    const seedShip = await read(writer, 'writer', '/api/v1/vessels/'+base.vesselId)
    const seedTug = await read(writer, 'writer', '/api/v1/vessels/30000000-0000-0000-0000-000000000002')
    if (seedShip.kind !== 'Ship' || seedTug.kind !== 'Tug' || seedShip.companyId !== seedTug.companyId) fail()
    const existing = await qualifiedList(writer, '/api/v1/vessels?limit=25')
    for (const kind of ['Ship', 'Tug'] as const) {
      const code = qualifiedVesselCode(base.runId, kind), stage = 'qualified_'+kind.toLowerCase()+'_vessel'
      const prior = journal.load('qualified-owning-command-'+stage.replaceAll('_', '-')+'-receipt.json') as any
      const collisions = existing.filter(row => row.code === code)
      if (collisions.length > 1 || collisions.length === 1 && (!prior || collisions[0].id !== prior.body.id)) fail()
      const created = await command(writer, 'writer', stage, '/api/v1/vessels', () => ({ code,
        name: 'ZZZ FICTIONAL qualified '+kind+' software fixture '+base.runId, kind, companyId: seedShip.companyId }), async (receipt, body) => {
        if (!qualifiedGuid(receipt.id) || receipt.id === base.vesselId || receipt.id === seedTug.id) fail()
        match(receipt, body, ['companyId', 'code', 'name', 'kind'])
        const actual = await read(writer, 'writer', '/api/v1/vessels/'+receipt.id)
        match(actual, receipt, ['id', 'companyId', 'code', 'name', 'kind'])
      })
      const path = '/api/v1/vessels/'+created.id+'/regulatory-profile'
      const profile = await command(writer, 'writer', stage+'_profile', path, async () => {
        const empty = await read(writer, 'writer', path)
        if (empty.version !== 0 || empty.revision !== 0 || Object.values(empty.content).some((field: any) => field !== null &&
            (!['Missing', 'Unknown'].includes(field.state) || field.value !== null))) fail()
        return { expectedVersion: 0, content: empty.content }
      }, async receipt => {
        const actual = await read(writer, 'writer', path)
        if (receipt.tenantId !== base.tenantId || receipt.vessel?.id !== created.id || !Number.isSafeInteger(receipt.revision) || receipt.revision < 1 ||
            actual.revision !== receipt.revision || qualifiedContentHash(actual.content) !== qualifiedContentHash(receipt.content)) fail()
      }, 'PUT')
      observed[kind] = { id: created.id, companyId: created.companyId, kind, code, version: created.version,
        profileRevision: profile.revision, profileVersion: profile.version, profileContentSha256: qualifiedContentHash(profile.content) }
    }
    const value = decodeQualifiedVessels({ schemaVersion: 'poseidon.qualified-owning.observed-vessels.v1', runId: base.runId,
      tenantId: base.tenantId, writerActorId: base.writerActorId, vessels: observed }, base)
    persist('qualified-owning-observed-vessels.json', value)
  }

  async function sources() {
    const page = await signed('reviewer'), rules: Record<string, any> = {}
    const current = await qualifiedList(page, '/api/v1/rules')
    for (const mode of qualifiedModes) {
      const prior = journal.load(`qualified-owning-command-${mode.toLowerCase()}-rule-receipt.json`) as any
      const collision = current.filter(row => row.current?.rule?.ruleId === qualifiedRuleNames[mode])
      if (collision.length > 1 || collision.length === 1 && (!prior || collision[0].current.id !== prior.body.current.id)) fail()
      const known = (value: string) => ({ state: 'Known', value })
      const source = await command(page, 'reviewer', mode.toLowerCase()+'_source', '/api/v1/source-drafts', () => ({ expectedVersion: 0,
        content: { title: known('FICTIONAL qualified software source '+mode+' '+plan.runId), authority: known('FICTIONAL publisher; no maritime authority'), locator: known('FICTIONAL/'+plan.runId+'/'+mode) } }),
      async row => { const actual = await read(page, 'reviewer', '/api/v1/source-drafts/'+row.current.id); match(actual.current, row.current, ['id', 'revision', 'hash']) })
      const requirement = await command(page, 'reviewer', mode.toLowerCase()+'_requirement', '/api/v1/requirement-drafts', () => ({ expectedVersion: 0,
        content: { instrument: known('FICTIONAL software requirement '+mode), amendment: known('1') }, sourceId: source.current.id, sourceRevision: source.current.revision }),
      async row => { const actual = await read(page, 'reviewer', '/api/v1/requirement-drafts/'+row.current.id); match(actual.current, row.current, ['id', 'revision', 'hash', 'sourceId', 'sourceRevision']) })
      const rule = await command(page, 'reviewer', mode.toLowerCase()+'_rule', '/api/v1/rules', () => ({ expectedVersion: 0, contract: ruleContract,
        rule: { ruleId: qualifiedRuleNames[mode], version: 1, source: { sourceId: source.current.id, locator: 'FICTIONAL/'+plan.runId+'/'+mode, authorityLayer: 'PRODUCT_CONTROL' },
          lifecycle: { state: 'DRAFT', effectiveFrom: '2000-01-01' }, scope: {}, when: { field: 'context.mode', operator: 'EXISTS' }, then: { result: 'APPLICABLE' }, unknownPolicy: 'REVIEW_REQUIRED' },
        sourceId: source.current.id, sourceRevision: source.current.revision, requirementId: requirement.current.id, requirementRevision: requirement.current.revision, reasonCode: 'INITIAL_AUTHORING' }),
      async row => { const actual = await read(page, 'reviewer', '/api/v1/rules/'+row.current.id+'/versions?offset=0'); match(actual.items?.[0]?.current, row.current, ['id', 'revision', 'hash', 'sourceId', 'sourceRevision', 'requirementId', 'requirementRevision']) })
      rules[mode] = { id: rule.current.id, revision: rule.current.revision, sourceId: source.current.id, sourceRevision: source.current.revision,
        requirementId: requirement.current.id, requirementRevision: requirement.current.revision }
    }
    persist('qualified-owning-observed-sources.json', { schemaVersion: 'poseidon.qualified-owning.observed-sources.v1', runId: plan.runId, rules })
  }

  function qualificationContent(mode: QualifiedMode) {
    const fields = mode === 'Ship' ? ['cargo.packagedDangerousGoods'] : mode === 'Tug'
      ? ['vessel.operatingArea', 'operation.jobType', 'operation.profile', 'operation.serviceContext', 'equipment.towingWinchFitted']
      : ['class.rulePack.IACS_M79', 'regulatory.msmdVerified', 'evidence.sourceVerified']
    const documentKinds: Record<string, string> = { cargoPlan: 'CargoPlan', serviceAuthorization: 'ServiceAuthorization', operation: 'Operation', winchFitment: 'WinchFitment', classPack: 'ClassPack', msmd: 'Msmd' }
    const kinds = qualifiedModeKinds[mode].map(kind => documentKinds[qualifiedFields[kind]])
    return { sourceBytesBase64: Buffer.from('FICTIONAL '+plan.runId+' '+mode+' software source; not a maritime rule').toString('base64'),
      publisher: 'FICTIONAL software fixture publisher', edition: '1', ownerId: plan.reviewerActorId, ownerRole: plan.reviewerRoleId,
      validationReview: 'FICTIONAL software test review; no real source adoption', inputs: [{ field: 'vessel.flag', basis: 'Declared', maxAgeHours: null }, ...fields.map(field => ({ field, basis: 'HumanAttested', maxAgeHours: 24 }))],
      control: { ownerRole: plan.writerRoleId, frequency: 'FICTIONAL frequency', evidencePolicy: 'FICTIONAL evidence policy', verificationPolicy: 'FICTIONAL independent review',
        maxAgeHours: 24, requireFile: true, requireAcknowledgment: false, acknowledgmentRole: null, requiredVerificationMechanism: null, verificationMechanismSourceLocator: null, sourceLocator: 'FICTIONAL/'+plan.runId+'/'+mode },
      trigger: null, impactReview: null, supersedingRuleId: null, supersedingRuleRevision: null,
      owningDocuments: kinds.map(documentKind => ({ documentKind, authorRole: plan.writerRoleId, verifierRole: plan.reviewerRoleId, sourceLocator: 'FICTIONAL/'+plan.runId+'/'+documentKind })),
      ...(mode === 'Tug' ? { literalBindings: [
        { field: 'vessel.operatingArea', ownerValue: 'FICTIONAL-AREA', ruleValue: 'FICTIONAL-AREA', sourceLocator: 'FICTIONAL/local-overlay/source' },
        { field: 'operation.jobType', ownerValue: 'Harbour Towage', ruleValue: 'HARBOUR_ASSIST', sourceLocator: 'FICTIONAL/job/source' },
        { field: 'operation.profile', ownerValue: 'CLOSE_QUARTERS_TOWAGE', ruleValue: 'CLOSE_QUARTERS_TOWAGE', sourceLocator: 'FICTIONAL/operation/source' },
        { field: 'operation.serviceContext', ownerValue: 'AUTHORIZED_TUG_SERVICE', ruleValue: 'AUTHORIZED_TUG_SERVICE', sourceLocator: 'FICTIONAL/service/source' }] } : {}),
      ...(mode === 'Compliance' ? { classPack: { packReference: 'FICTIONAL-PACK', edition: 'FICTIONAL-EDITION', sourceLocator: 'FICTIONAL/class/source' } } : {}) }
  }
  async function qualification(page: Page, who: Who, mode: QualifiedMode, rule: any, state: string) {
    const path = '/api/v1/regulatory-source-governance/'+rule.id
    return command(page, who, mode.toLowerCase()+'_qualification_'+state.toLowerCase(), path+'/events', async () => {
      const current = await read(page, who, path), action = state === 'DRAFT' ? 'draft' : state === 'IN_REVIEW' ? 'review' : 'qualify'
      const grants = current.authorities.filter((g: any) => g.action === action)
      if (grants.length !== 1 || !current.actions.includes(action)) fail()
      return { expectedVersion: current.items[0]?.version ?? 0, ruleRevision: rule.revision, authorityId: grants[0].authorityId, state,
        content: state === 'DRAFT' ? qualificationContent(mode) : null }
    }, async (receipt, body) => {
      if (receipt.ruleId !== rule.id || receipt.ruleRevision !== rule.revision || receipt.state !== state || receipt.actorId !== actor(who) || receipt.authorityId !== body.authorityId || receipt.blockers?.length !== 0) fail()
      const current = await read(page, who, path), actual = current.items.find((row: any) => row.revision === receipt.revision)
      match(actual, receipt, ['ruleId', 'revision', 'hash', 'state', 'authorityId', 'actorId'])
    })
  }

  async function witnesses() {
    const reviewer = await signed('reviewer'), writer = await signed('writer'), source = observedSources(), details: Record<string, any> = {}
    if (source.runId !== plan.runId) fail()
    const knownEvidence = new Set(qualifiedKinds.flatMap(kind => {
      const receipt = journal.load('qualified-owning-command-'+kind.toLowerCase()+'-evidence-receipt.json') as any
      return receipt ? [receipt.body.id] : []
    }))
    for (const vessel of [plan.shipId, plan.tugId]) {
      const rows = await qualifiedList(writer, '/api/v1/evidence?vesselId='+vessel)
      if (rows.some(row => ['FICTIONAL signed-host owning ', 'FICTIONAL signed-host compliance '].some(prefix => row.content?.title?.startsWith(prefix)) && !knownEvidence.has(row.id))) fail()
    }
    for (const mode of qualifiedModes) {
      const rule = source.rules[mode]
      await qualification(reviewer, 'reviewer', mode, rule, 'DRAFT')
      await qualification(reviewer, 'reviewer', mode, rule, 'IN_REVIEW')
      const qualified = await qualification(writer, 'writer', mode, rule, 'IN_FORCE')
      const baseline = await command(writer, 'writer', mode.toLowerCase()+'_baseline', '/api/v1/applicability/evaluate',
        () => ({ vesselId: vesselFor(mode), ruleIds: [rule.id], asOf: null }), async receipt => {
          if (!qualifiedGuid(receipt.id) || receipt.authoritative !== true || receipt.simulation !== false || receipt.vesselId !== vesselFor(mode) ||
              receipt.results?.length !== 1 || receipt.results[0].ruleId !== rule.id || receipt.results[0].revision !== rule.revision ||
              receipt.results[0].prediction.result !== 'APPLICABLE' || receipt.snapshot?.owningInputs != null) fail()
          if ((await read(writer, 'writer', '/api/v1/applicability/evaluations/'+receipt.id+'/current')).current !== true) fail()
        })
      const control = await command(writer, 'writer', mode.toLowerCase()+'_control', '/api/v1/controls?vesselId='+vesselFor(mode), () => ({ expectedVersion: 0,
        content: { title: 'FICTIONAL qualified '+mode+' control '+plan.runId, ownerId: plan.writerActorId, ownerRole: plan.writerRoleId,
          frequency: 'FICTIONAL frequency', evidencePolicy: 'FICTIONAL evidence policy', verificationPolicy: 'FICTIONAL independent review', critical: true }, requirements: [pin(rule)], links: [], file: null, reasonCode: 'INITIAL_AUTHORING' }),
      async receipt => { await graphReceipt(writer, 'writer', 'controls', vesselFor(mode), receipt) })
      details[mode] = { ...rule, qualificationRevision: qualified.revision, qualificationHash: qualified.hash, baselineEvaluationId: baseline.id, control }
    }
    const position = await command(writer, 'writer', 'ship_position', `/api/v1/vessels/${plan.shipId}/crew/positions`, () => ({ id: plan.positionId,
      code: 'FICTIONAL-QOF', rank: null, capacity: null, department: null, policyVersion: config.CrewPositionPolicy.Version }), async receipt => {
        if (receipt.id !== plan.positionId) fail()
        const current = await read(writer, 'writer', `/api/v1/vessels/${plan.shipId}/crew/positions`)
        const rows = current.filter((row: any) => row.id === plan.positionId)
        if (rows.length !== 1 || rows[0].code !== 'FICTIONAL-QOF' || rows[0].rank !== null || rows[0].provenance !== 'UserDeclaredUnverified') fail()
      })
    const job = await command(writer, 'writer', 'tug_job', '/api/v1/tug-jobs?vessel='+plan.tugId, () => ({ clientId: journal.id('qualified-tug-job'), baseVersion: null,
      inputs: { assistedVessel: 'FICTIONAL software vessel', jobType: 'Harbour Towage', requestSource: 'Internal', requestedStart: plan.notBefore,
        requestedEnd: plan.expiresAt, operationArea: 'FICTIONAL-AREA', requiredTugCount: 1, priority: 'Normal', requestOwner: plan.writerRoleId,
        notes: 'FICTIONAL software test; no real service request' } }), async receipt => {
          const current = await read(writer, 'writer', '/api/v1/tug-jobs/'+receipt.id+'?vessel='+plan.tugId)
          match(current, receipt, ['id', 'vesselId', 'revision', 'version']); if (receipt.id !== journal.id('qualified-tug-job')) fail()
        })
    const planPath = '/api/v1/tug-jobs/'+job.id+'/plan?vessel='+plan.tugId
    const window = await command(writer, 'writer', 'tug_window', planPath, () => ({ baseVersion: null, requestVersion: job.version,
      policyVersion: config.TugPlanning.Version, change: { operation: 'Window', plannedStart: plan.notBefore, plannedEnd: plan.expiresAt } }),
      async receipt => { const actual = await read(writer, 'writer', '/api/v1/tug-jobs/'+job.id+'/plan/history/'+receipt.revision+'?vessel='+plan.tugId); match(actual, receipt, ['jobId', 'revision', 'requestRevision', 'policyVersion']) }, 'PUT')
    const assigned = await command(writer, 'writer', 'tug_assign', planPath, () => ({ baseVersion: window.version, requestVersion: job.version,
      policyVersion: config.TugPlanning.Version, change: { operation: 'Assign', tugId: plan.tugId } }), async receipt => {
        const actual = await read(writer, 'writer', planPath); match(actual, receipt, ['jobId', 'revision', 'version', 'requestRevision', 'policyVersion'])
        if (canonical(actual.inputs.assignedTugs) !== canonical([plan.tugId])) fail()
      }, 'PUT')
    const asset = await command(writer, 'writer', 'tug_asset', '/api/v1/equipment-hierarchy/'+plan.tugId, () => ({ id: journal.id('qualified-tug-asset'), baseVersion: null,
      parentId: null, code: 'FICTIONAL-QOF-'+plan.runId, name: 'FICTIONAL winch software fixture', level: 'Equipment', systemFamily: null, equipmentType: null,
      criticality: null, maker: null, model: null, serialNumber: null, location: null, commissionDate: null, responsibleRoleId: null, notes: 'FICTIONAL software declaration' }), async receipt => {
        const current = await read(writer, 'writer', '/api/v1/equipment-hierarchy/'+plan.tugId)
        match(current.items?.find((row: any) => row.id === receipt.id), receipt, ['id', 'revision', 'version'])
      })
    const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==', 'base64')
    const checksum = createHash('sha256').update(png).digest('hex').toUpperCase(), observed: Record<string, any> = {}, final: Record<string, any> = {}
    for (const kind of qualifiedKinds) {
      const mode: QualifiedMode = qualifiedModeKinds.Tug.includes(kind) ? 'Tug' : kind === 'shipCargo' ? 'Ship' : 'Compliance'
      const vessel = vesselFor(mode), rule = details[mode], rootPath = `/api/v1/vessels/${vessel}/files`
      const file = await command(writer, 'writer', kind.toLowerCase()+'_file', rootPath,
        () => ({ name: 'fictional-'+kind+'-'+plan.runId+'.png', mime: 'image/png', length: png.length, checksum }), async receipt => {
          const actual = await read(writer, 'writer', rootPath+'/'+receipt.id)
          match(actual, receipt, ['id', 'vesselId', 'name', 'length']); if (actual.vesselId !== vessel || !['Pending', 'CleanTransport'].includes(actual.state)) fail()
        })
      const clean = await command(writer, 'writer', kind.toLowerCase()+'_upload', rootPath+'/'+file.id+'/bytes', () => ({ base64: png.toString('base64'), checksum }), async receipt => {
        const actual = await read(writer, 'writer', rootPath+'/'+file.id); match(actual, receipt, ['id', 'vesselId', 'revision', 'checksum', 'state'])
        if (actual.state !== 'CleanTransport' || actual.revision !== 1 || actual.checksum !== checksum) fail()
      }, 'PUT', false)
      const control = (await read(writer, 'writer', `/api/v1/controls/${rule.control.id}/versions?vesselId=${vessel}&offset=0`)).items[0]
      const jobPin = { jobId: job.id, jobRevision: job.revision, jobVersion: job.version, planRevision: assigned.revision, planVersion: assigned.version }
      const typed = kind === 'shipCargo' ? { cargoPlan: { planReference: 'FICTIONAL cargo software declaration', packagedDangerousGoods: true } }
        : kind === 'tugService' ? { serviceAuthorization: { ...jobPin, serviceContext: 'AUTHORIZED_TUG_SERVICE', issuerReference: 'FICTIONAL software declaration; no actual issuance' } }
        : kind === 'tugOperation' ? { operation: { ...jobPin, profile: 'CLOSE_QUARTERS_TOWAGE', serviceAuthorizationId: final.tugService.id, serviceAuthorizationRevision: final.tugService.revision } }
        : kind === 'tugWinch' ? { winchFitment: { assetId: asset.id, assetRevision: asset.revision, assetVersion: asset.version, fitted: true, registerReference: 'FICTIONAL software register' } }
        : kind === 'shipClass' ? { classPack: { packReference: 'FICTIONAL-PACK', edition: 'FICTIONAL-EDITION', surveyReference: 'FICTIONAL software record; no survey', iacsM79: true } }
        : { msmd: { documentReference: 'FICTIONAL software MSMD; no issuance', issuerReference: 'FICTIONAL issuer', roles: [{ positionId: position.id, count: 1, sourceLocator: 'FICTIONAL/msmd/software-role' }] } }
      const body = { expectedVersion: 0, content: { title: prefixFor(mode)+kind+' '+plan.runId, ownerId: plan.writerActorId, ownerRole: plan.writerRoleId,
        provenance: 'FICTIONAL software test only', observedAt: plan.notBefore, validFrom: plan.notBefore, validUntil: plan.expiresAt, freshUntil: plan.expiresAt, ...typed },
        requirements: [pin(rule)], links: [{ kind: 'Control', id: control.id, revision: control.revision, expectedVersion: control.version }],
        file: { id: clean.id, revision: clean.revision, checksum: clean.checksum }, reasonCode: 'INITIAL_AUTHORING' }
      const draft = await command(writer, 'writer', kind.toLowerCase()+'_evidence', '/api/v1/evidence?vesselId='+vessel, () => body,
        async receipt => { await graphReceipt(writer, 'writer', 'evidence', vessel, receipt) })
      const submitted = await command(writer, 'writer', kind.toLowerCase()+'_submit', `/api/v1/evidence/${draft.id}/submit?vesselId=${vessel}`,
        () => ({ ...body, expectedVersion: draft.version, reasonCode: 'SUBMIT' }), async receipt => {
          await graphReceipt(writer, 'writer', 'evidence', vessel, receipt)
          if (receipt.lifecycle !== 'SUBMITTED' || receipt.blockers?.length !== 0) fail()
        })
      final[kind] = submitted
      observed[kind] = { vesselId: vessel, id: submitted.id, revision: submitted.revision, controlId: control.id, controlRevision: control.revision,
        operationId: mode === 'Tug' && kind !== 'tugWinch' ? job.id : journal.id('review-operation-'+kind), baselineEvaluationId: rule.baselineEvaluationId }
    }
    persist('qualified-owning-details.json', { runId: plan.runId, rules: Object.fromEntries(qualifiedModes.map(mode =>
      [mode, Object.fromEntries(Object.entries(details[mode]).filter(([key]) => key !== 'control'))])) })
    persist('qualified-owning-observed-witnesses.json', { schemaVersion: 'poseidon.qualified-owning.observed-witnesses.v1', runId: plan.runId, witnesses: observed })
  }

  async function verify() {
    const writer = await signed('writer'), reviewer = await signed('reviewer'), observed = readQualifiedPrivate(root, 'qualified-owning-observed-witnesses.json')
    const details = readQualifiedPrivate(root, 'qualified-owning-details.json'), witnesses: Record<string, any> = {}
    if (observed.runId !== plan.runId || details.runId !== plan.runId) fail()
    for (const kind of qualifiedKinds) {
      const row = observed.witnesses[kind], tracePath = `/api/v1/regulatory-trace?vesselId=${row.vesselId}&evidenceId=${row.id}&evidenceRevision=${row.revision}&controlId=${row.controlId}&controlRevision=${row.controlRevision}`
      const make = async (page: Page, who: Who, capability: string, expectedVersion: number, state: string) => {
        const trace = await read(page, who, tracePath), authorities = trace.authorities.filter((g: any) => g.capability === capability && g.operationId === row.operationId)
        if (trace.historical !== false || trace.evidenceId !== row.id || trace.controlId !== row.controlId || authorities.length !== 1) fail()
        return { expectedVersion, authorityId: authorities[0].id, operationId: row.operationId, evidenceId: row.id, evidenceRevision: row.revision,
          evidenceVersion: trace.evidenceVersion, controlId: row.controlId, controlRevision: row.controlRevision, controlVersion: trace.controlVersion,
          evaluationId: row.baselineEvaluationId, verificationId: null, verificationRevision: null, state,
          reasonCode: state === 'PENDING' ? 'REQUEST_REVIEW' : 'HUMAN_VERIFIED', rationale: 'FICTIONAL independent software verification; no real certificate or operational approval' }
      }
      const check = (who: Who, state: string) => async (receipt: any) => {
        if (receipt.state !== state || receipt.actorId !== actor(who) || receipt.evidenceId !== row.id || receipt.evidenceRevision !== row.revision ||
            receipt.controlId !== row.controlId || receipt.controlRevision !== row.controlRevision || receipt.operationId !== row.operationId || receipt.evaluationId !== row.baselineEvaluationId) fail()
        const trace = await read(who === 'writer' ? writer : reviewer, who, tracePath)
        const event = trace.items?.find((event: any) => event.id === receipt.id && event.revision === receipt.revision)
        match(event, receipt, ['id', 'revision', 'state', 'actorId', 'authorityId', 'contextHash'])
      }
      const pending = await command(writer, 'writer', kind.toLowerCase()+'_pending', '/api/v1/verifications?vesselId='+row.vesselId,
        () => make(writer, 'writer', 'human.review.submit', 0, 'PENDING'), check('writer', 'PENDING'))
      const verified = await command(reviewer, 'reviewer', kind.toLowerCase()+'_verified', `/api/v1/verifications/${pending.id}/events?vesselId=${row.vesselId}`,
        () => make(reviewer, 'reviewer', 'human.verify', pending.version, 'VERIFIED'), check('reviewer', 'VERIFIED'))
      const current = (await read(writer, 'writer', `/api/v1/evidence/${row.id}/versions?vesselId=${row.vesselId}&offset=0`)).items[0]
      if (current.revision !== row.revision || current.verification !== 'VERIFIED' || !current.content?.[qualifiedFields[kind]]) fail()
      witnesses[kind] = { ...row, hash: current.hash, fileId: current.file.id, fileRevision: current.file.revision, fileChecksum: current.file.checksum,
        verificationId: verified.id, verificationRevision: verified.revision }
    }
    const fixture = decodeQualifiedFixture({ schemaVersion: 'poseidon.qualified-owning.signed-fixture.v1', runId: plan.runId, tenantId: plan.tenantId,
      writerActorId: plan.writerActorId, reviewerActorId: plan.reviewerActorId, shipId: plan.shipId, tugId: plan.tugId,
      expiresAt: plan.expiresAt, vessels: decodeQualifiedVessels(readQualifiedPrivate(root, 'qualified-owning-observed-vessels.json'), base).vessels, rules: details.rules, witnesses })
    for (const mode of qualifiedModes) {
      report.stage = mode.toLowerCase()+'_current_source_witness_context'
      await requireQualifiedContext(writer, fixture, mode)
      const refs = mode === 'Ship' ? { cargoPlanId: witnesses.shipCargo.id }
        : mode === 'Tug' ? { operationId: witnesses.tugOperation.id, winchFitmentId: witnesses.tugWinch.id }
        : { classPackId: witnesses.shipClass.id, msmdId: witnesses.shipMsmd.id, sourceEvidenceId: witnesses.shipClass.id }
      await command(writer, 'writer', mode.toLowerCase()+'_positive', '/api/v1/applicability/evaluate',
        () => ({ vesselId: vesselFor(mode), ruleIds: [fixture.rules[mode].id], asOf: null, owningInputs: refs }), async receipt => {
          if (receipt.authoritative !== true || receipt.simulation !== false || receipt.vesselId !== vesselFor(mode) ||
              receipt.results?.length !== 1 || receipt.results[0].ruleId !== fixture.rules[mode].id || receipt.results[0].revision !== fixture.rules[mode].revision || receipt.results[0].prediction.result !== 'APPLICABLE') fail()
          const fields = mode === 'Ship' ? ['cargo.packagedDangerousGoods'] : mode === 'Tug'
            ? ['vessel.operatingArea', 'operation.jobType', 'operation.profile', 'operation.serviceContext', 'equipment.towingWinchFitted']
            : ['class.rulePack.IACS_M79', 'regulatory.msmdVerified', 'evidence.sourceVerified']
          for (const field of fields) {
            const fact = receipt.snapshot?.qualifications?.[0]?.facts?.[field]
            if (fact?.knowledge !== 'Known' || fact.provenance !== 'QualifiedOwningDocument') fail()
          }
          if ((await read(writer, 'writer', '/api/v1/applicability/evaluations/'+receipt.id+'/current')).current !== true) fail()
        })
    }
    // No ready publication before all real host receipts, owner fences and current
    // positive lineage checks complete. A descriptor never creates authority.
    preparedFixture = fixture
  }
  try {
    if (phase === 'vessels') await vessels()
    else if (phase === 'sources') await sources()
    else if (phase === 'witnesses') await witnesses()
    else await verify()
    report.stage = 'complete'; report.status = 'PRECONDITIONS_ONLY'
  } catch { report.status = 'FAILED' }
  finally {
    for (const owned of contexts.reverse()) {
      try { await owned.close() } catch { report.status = 'FAILED' }
    }
    if (phase === 'verify' && report.status === 'PRECONDITIONS_ONLY' && preparedFixture) persist('qualified-owning-fixture.json', preparedFixture)
    persist('qualified-owning-report-'+randomUUID()+'.json', report)
  }
  process.exitCode = report.status === 'PRECONDITIONS_ONLY' ? 0 : 1

}
try { await provision() } catch { process.exitCode = 1 } // Closed failure: no parser/path/private response diagnostics.
