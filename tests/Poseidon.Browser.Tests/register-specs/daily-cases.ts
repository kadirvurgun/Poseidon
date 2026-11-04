import { expect, type Page } from '@playwright/test'
import { dailyAuthorityId, dailyRecordId, dailySignedSession, dailySourceVersion, requiredDailyFixture, type DailyFixture, type DailyKind } from './daily-fixture.ts'

const families = [
  { kind: 'Noon', api: 'noon-reports', screen: 'noon-report', field: 'weather_summary', label: 'Weather / sea summary', duty: 'Master' },
  { kind: 'Deck', api: 'deck-daily-logs', screen: 'deck-daily-log', field: 'handover_note', label: 'Watch handover note', duty: 'ChiefOfficer' },
  { kind: 'Engine', api: 'engine-daily-logs', screen: 'engine-daily-log', field: 'alarms', label: 'Alarms / trips', duty: 'ChiefEngineer' },
] as const
type Family = typeof families[number]
type Value = { state: 'Known' | 'Missing' | 'Unknown'; value: string | null }
type Row = { id: string; vesselId: string; kind: DailyKind; revision: number; version: number; fields: Record<string, Value>; hash: string; state: string; provenance: string; reviewState: string; reviewedRevision: number | null; reviewedBy: string | null }
type Draft = { id: string; expectedVersion: number; fields: Record<string, Value>; reasonCode: 'INITIAL_AUTHORING' | 'AUTHORING_CORRECTION' }
const path = (family: Family, fixture: DailyFixture, suffix = '') => `/api/v1/${family.api}${suffix}?vesselId=${fixture.vesselId}`
const english = async (page: Page) => page.getByRole('button', { name: 'EN', exact: true }).click()
async function command(page: Page, url: string, method: 'post' | 'put', body: unknown, csrf: string, key = crypto.randomUUID()) {
  return page.request[method](url, { headers: { 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': key }, data: body })
}
async function create(page: Page, family: Family, fixture: DailyFixture, purpose: 'ordinary' | 'review') {
  const user = await dailySignedSession(page, fixture)
  const vesselResponse = await page.request.get(`/api/v1/vessels/${fixture.vesselId}`); expect(vesselResponse.status()).toBe(200)
  const vessel = await vesselResponse.json(); expect(vessel.id === fixture.vesselId && vessel.kind === 'Ship').toBe(true)
  const id = dailyRecordId(fixture, family.kind, purpose)
  // Reusing a run or missing an actual disposable fixture fails before mutation.
  expect((await page.request.get(path(family, fixture, `/${id}`))).status()).toBe(404)
  const capResponse = await page.request.get(path(family, fixture, '/capabilities')); expect(capResponse.status()).toBe(200)
  const caps = await capResponse.json(); expect(caps.sourceVersion).toBe(dailySourceVersion)
  expect(caps.canRead === true && caps.canWrite === true && caps.canSubmit === false).toBe(true)
  const body: Draft = { id, expectedVersion: 0, fields: { [family.field]: { state: 'Known', value: `FICTIONAL ${fixture.runId} incomplete ${family.kind}\nsoftware declaration only` } }, reasonCode: 'INITIAL_AUTHORING' }
  const key = crypto.randomUUID(), response = await command(page, path(family, fixture), 'post', body, user.csrfToken, key)
  expect(response.status()).toBe(200)
  const row = await response.json() as Row
  expect(row.id === id && row.vesselId === fixture.vesselId && row.kind === family.kind && row.revision === 1 && row.version > 0).toBe(true)
  expect(row.state === 'DRAFT' && row.provenance === 'UserDeclaredUnverified' && row.reviewState === 'PENDING' && row.reviewedBy === null).toBe(true)
  const replay = await command(page, path(family, fixture), 'post', body, user.csrfToken, key); expect(replay.status()).toBe(200)
  expect(await replay.text() === await response.text()).toBe(true)
  return { row, user, body }
}

async function ordinary(page: Page, family: Family) {
  const fixture = requiredDailyFixture(page), { row, user } = await create(page, family, fixture, 'ordinary')
  await page.goto(`/app/${family.screen}/${fixture.vesselId}?id=${row.id}`); await english(page)
  const editor = page.getByRole('region', { name: 'Daily draft editor', exact: true })
  await expect(editor).toBeVisible()
  await editor.getByLabel(family.label, { exact: true }).fill(`FICTIONAL ${fixture.runId} retained update\nnot operational evidence`)
  let firstBody = '', firstKey = '', same = false, actualRevision = 0, deliveries = 0
  const pattern = `**/api/v1/${family.api}/${row.id}?vesselId=${fixture.vesselId}`
  // Genuine loss AFTER the actual audited server transaction. No fulfilled
  // response, mocked business data, response retarget or assertion substitution.
  await page.route(pattern, async route => {
    if (route.request().method() !== 'PUT') { await route.continue(); return }
    deliveries++
    if (deliveries === 1) {
      firstBody = route.request().postData()!; firstKey = route.request().headers()['idempotency-key']
      const actual = await route.fetch(); expect(actual.status()).toBe(200); actualRevision = (await actual.json()).revision
      await route.abort('connectionreset')
    } else {
      same = route.request().postData() === firstBody && route.request().headers()['idempotency-key'] === firstKey
      await route.continue()
    }
  })
  try {
    await editor.getByRole('button', { name: 'Save draft', exact: true }).click()
    await expect(editor.getByRole('button', { name: 'Retry exact saved command', exact: true })).toBeVisible()
    await expect(editor.getByLabel(family.label, { exact: true })).toBeDisabled()
    const retry = page.waitForResponse(r => r.request().method() === 'PUT' && new URL(r.url()).pathname === `/api/v1/${family.api}/${row.id}`)
    await editor.getByRole('button', { name: 'Retry exact saved command', exact: true }).click()
    expect((await retry).status()).toBe(200); expect(same && deliveries === 2 && actualRevision === 2).toBe(true)
  } finally { await page.unroute(pattern) }
  const historyResponse = await page.request.get(path(family, fixture, `/${row.id}/history`)); expect(historyResponse.status()).toBe(200)
  const history = await historyResponse.json(); expect(history.items.length === 2 && history.hasMore === false).toBe(true)
  expect(history.items[1].version === row.version && history.items[1].hash === row.hash && history.items[1].fields[family.field].value === row.fields[family.field].value).toBe(true)
  const current = history.items[0] as Row
  const stale = await command(page, path(family, fixture, `/${row.id}`), 'put', { id: row.id, expectedVersion: row.version, fields: {}, reasonCode: 'AUTHORING_CORRECTION' }, user.csrfToken)
  expect(stale.status()).toBe(409)
  const changed = JSON.parse(firstBody) as Draft; changed.fields[family.field] = { state: 'Known', value: 'FICTIONAL different frozen body' }
  expect((await command(page, path(family, fixture, `/${row.id}`), 'put', changed, user.csrfToken, firstKey)).status()).toBe(409)
  const forged = { id: row.id, expectedVersion: current.version, fields: { [family.kind === 'Engine' ? 'chief_engineer_review' : 'master_review']: { state: 'Known', value: 'REVIEWED' } }, reasonCode: 'AUTHORING_CORRECTION' }
  expect((await command(page, path(family, fixture, `/${row.id}`), 'put', forged, user.csrfToken)).status()).toBe(400)
  const after = await (await page.request.get(path(family, fixture, `/${row.id}`))).json()
  expect(after.revision === 2 && after.reviewState === 'PENDING' && after.state === 'DRAFT').toBe(true)
  const context = await page.request.get(`/api/v1/workspace/context/${fixture.vesselId}`); expect(context.status()).toBe(200)
  expect((await context.json()).vessel.id === fixture.vesselId).toBe(true)
}

async function review(page: Page, family: Family) {
  const fixture = requiredDailyFixture(page), { row, user } = await create(page, family, fixture, 'review')
  const capabilityPath = path(family, fixture, `/${row.id}/review-capabilities`), reviewsPath = path(family, fixture, `/${row.id}/reviews`)
  const writerCap = await page.request.get(capabilityPath); expect(writerCap.status()).toBe(200)
  expect((await writerCap.json()).canRecord).toBe(false) // Writer role is not a human appointment.
  const invalid = { expectedVersion: row.version, expectedReviewVersion: 0, reviewedRevision: row.revision, authorityId: dailyAuthorityId(fixture, family.kind), decision: 'REVIEWED', reasonCode: 'REVIEW_RECORDED', rationale: 'FICTIONAL unauthorized writer request' }
  expect((await command(page, reviewsPath, 'post', invalid, user.csrfToken)).status()).toBe(403)
  const { strictContext } = await import('../lab/strict-browser.ts'), { login } = await import('../lab/journeys.ts')
  const peer = await strictContext({ baseURL: 'https://localhost:5443', viewport: page.viewportSize()! })
  try {
    const reviewerPage = await peer.context.newPage()
    await login(reviewerPage, fixture.reviewerAccount, `/app/${family.screen}/${fixture.vesselId}?id=${row.id}`)
    const reviewer = await dailySignedSession(reviewerPage, fixture, 'reviewer')
    const capResponse = await reviewerPage.request.get(capabilityPath); expect(capResponse.status()).toBe(200)
    const cap = await capResponse.json(); expect(cap.canRecord === true && cap.recordRevision === row.revision && cap.recordVersion === row.version && cap.reviewVersion === 0 && cap.sourceVersion === dailySourceVersion).toBe(true)
    const authority = cap.authorities.filter((g: { id: string; duty: string }) => g.id === dailyAuthorityId(fixture, family.kind) && g.duty === family.duty)
    expect(authority.length === 1 && authority[0].decisions.includes('REVIEWED')).toBe(true)
    const region = reviewerPage.getByRole('region', { name: 'Human review recording', exact: true })
    await region.getByLabel('Authorized human appointment', { exact: true }).selectOption(authority[0].id)
    await region.getByLabel('Review entry', { exact: true }).selectOption('REVIEWED')
    await region.getByLabel('Review rationale', { exact: true }).fill(`FICTIONAL ${fixture.runId} assigned software review; unresolved declarations retained; no safety approval`)
    let capturedBody = '', capturedKey = ''
    const observe = (request: import('@playwright/test').Request) => {
      if (request.method() === 'POST' && new URL(request.url()).pathname === `/api/v1/${family.api}/${row.id}/reviews`) { capturedBody = request.postData()!; capturedKey = request.headers()['idempotency-key'] }
    }
    reviewerPage.on('request', observe)
    let saved: import('@playwright/test').Response
    try {
      const pending = reviewerPage.waitForResponse(r => r.request().method() === 'POST' && new URL(r.url()).pathname === `/api/v1/${family.api}/${row.id}/reviews`)
      await region.getByRole('button', { name: 'Record review entry', exact: true }).click(); saved = await pending
    } finally { reviewerPage.off('request', observe) }
    expect(saved.status()).toBe(200)
    const recorded = await saved.json()
    expect(recorded.state === 'RECORDED_UNASSESSED' && recorded.recordId === row.id && recorded.recordRevision === row.revision && recorded.recordHash === row.hash && recorded.actorId === reviewer.actorId && recorded.authorityId === authority[0].id && recorded.sequence === 1).toBe(true)
    expect(!!capturedBody && !!capturedKey).toBe(true)
    const exact = await command(reviewerPage, reviewsPath, 'post', JSON.parse(capturedBody), reviewer.csrfToken, capturedKey); expect(exact.status()).toBe(200)
    expect(await exact.text() === await saved.text()).toBe(true)
    expect((await command(reviewerPage, reviewsPath, 'post', JSON.parse(capturedBody), reviewer.csrfToken)).status()).toBe(409)
    const reflected = await (await reviewerPage.request.get(path(family, fixture, `/${row.id}`))).json()
    expect(reflected.reviewState === 'RECORDED_UNASSESSED' && reflected.reviewedRevision === row.revision && reflected.reviewedBy === reviewer.actorId && reflected.state === 'DRAFT').toBe(true)
    expect(reflected.fields[family.kind === 'Engine' ? 'chief_engineer_review' : 'master_review'].value === (family.kind === 'Noon' ? 'DRAFT' : 'PENDING')).toBe(true)
    const entries = await (await reviewerPage.request.get(reviewsPath)).json(); expect(entries.items.length === 1 && entries.items[0].id === recorded.id).toBe(true)
    await expect(region.getByText('Recorded · unassessed', { exact: true }).first()).toBeVisible()
  } finally { await peer.close() }
}

async function restricted(page: Page) {
  const fixture = requiredDailyFixture(page), user = await dailySignedSession(page, fixture, 'restricted')
  for (const family of families) {
    const id = dailyRecordId(fixture, family.kind)
    expect((await page.request.get(path(family, fixture, '/capabilities'))).status()).toBe(403)
    const denied = await command(page, path(family, fixture), 'post', { id, expectedVersion: 0, fields: {}, reasonCode: 'INITIAL_AUTHORING', actorKind: 'Human', roles: ['Master', 'Administrator'] }, user.csrfToken)
    expect(denied.status()).toBe(403)
    const body = await denied.text(); expect(!body.includes('fields') && !body.includes('authorities')).toBe(true)
  }
}
async function foreignScope(page: Page) {
  const fixture = requiredDailyFixture(page); await dailySignedSession(page, fixture)
  const { strictContext } = await import('../lab/strict-browser.ts'), { login } = await import('../lab/journeys.ts')
  const foreign = await strictContext({ baseURL: 'https://localhost:5443', viewport: page.viewportSize()! })
  try {
    const peer = await foreign.context.newPage(); await login(peer, fixture.foreignAccount, '/app/today'); await dailySignedSession(peer, fixture, 'foreign')
    const actual = await peer.request.get(`/api/v1/vessels/${fixture.foreignVesselId}`); expect(actual.status()).toBe(200)
    expect((await actual.json()).id === fixture.foreignVesselId).toBe(true) // Proves a real foreign row, not a random missing UUID.
    for (const family of families) {
      expect((await page.request.get(`/api/v1/${family.api}?vesselId=${fixture.foreignVesselId}`)).status()).toBe(404)
      expect((await peer.request.get(path(family, fixture))).status()).toBe(404)
    }
  } finally { await foreign.close() }
}

export const dailyCases = [
  { name: 'daily-noon-signed-owning-create-ui-loss-exact-retry-stale-reforge', run: (page: Page) => ordinary(page, families[0]), restricted: false },
  { name: 'daily-deck-signed-owning-create-ui-loss-exact-retry-stale-reforge', run: (page: Page) => ordinary(page, families[1]), restricted: false },
  { name: 'daily-engine-signed-owning-create-ui-loss-exact-retry-stale-reforge', run: (page: Page) => ordinary(page, families[2]), restricted: false },
  { name: 'daily-noon-signed-qualified-human-recording-exact-revision', run: (page: Page) => review(page, families[0]), restricted: false },
  { name: 'daily-deck-signed-qualified-human-recording-exact-revision', run: (page: Page) => review(page, families[1]), restricted: false },
  { name: 'daily-engine-signed-qualified-human-recording-exact-revision', run: (page: Page) => review(page, families[2]), restricted: false },
  { name: 'daily-signed-restricted-no-role-escalation-or-object-disclosure', run: restricted, restricted: true },
  { name: 'daily-signed-actual-foreign-vessel-isolation', run: foreignScope, restricted: false },
] as const
