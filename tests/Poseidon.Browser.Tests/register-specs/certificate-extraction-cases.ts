import { createHash } from 'node:crypto'
import { readFileSync, realpathSync, statSync } from 'node:fs'
import { isAbsolute, relative, sep } from 'node:path'
import { expect, type Page } from '@playwright/test'

// Genuine signed disposable-host source only. A later operator-owned setup must
// supply the real clean synthetic file + explicit processing policy/protocol
// peer. No fulfilled business responses, external activation, injected actor or
// invented model accuracy. Missing prerequisites FAIL; this case never skips.
type Fixture = { schemaVersion: 'poseidon.certificate-extraction.signed-fixture.v1'; runId: string; tenantId: string; vesselId: string; actorId: string; modelVersion: string; promptVersion: string; files: { desktop: Pin; narrow: Pin } }
type Pin = { id: string; revision: number; checksum: string }
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/
const guid = (value: unknown): value is string => typeof value === 'string' && uuid.test(value) && value !== '00000000-0000-0000-0000-000000000000'
const obj = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, keys: string[]): value is Record<string, unknown> => obj(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key))
function requiredFixture() {
  const name = process.env.POSEIDON_BROWSER_CERTIFICATE_EXTRACTION_FIXTURE, privateRoot = process.env.POSEIDON_LAB_PRIVATE
  if (!name || !privateRoot) throw new Error('certificate_extraction_signed_fixture_required')
  const root = realpathSync(privateRoot), path = realpathSync(name), child = relative(root, path)
  if (!child || isAbsolute(child) || child === '..' || child.startsWith('..' + sep) || statSync(path).size > 8192) throw new Error('certificate_extraction_fixture_boundary')
  let data: unknown
  try { data = JSON.parse(readFileSync(path, 'utf8')) } catch { throw new Error('certificate_extraction_fixture_invalid') }
  if (!exact(data, ['schemaVersion', 'runId', 'tenantId', 'vesselId', 'actorId', 'modelVersion', 'promptVersion', 'files']) || data.schemaVersion !== 'poseidon.certificate-extraction.signed-fixture.v1' || !['runId', 'tenantId', 'vesselId', 'actorId'].every(key => guid(data[key])) || !['modelVersion', 'promptVersion'].every(key => typeof data[key] === 'string' && /^[A-Za-z0-9._:@/-]{1,128}$/.test(data[key] as string)) || !exact(data.files, ['desktop', 'narrow'])) throw new Error('certificate_extraction_fixture_invalid')
  for (const pin of Object.values(data.files)) if (!exact(pin, ['id', 'revision', 'checksum']) || !guid(pin.id) || typeof pin.revision !== 'number' || !Number.isSafeInteger(pin.revision) || pin.revision < 1 || typeof pin.checksum !== 'string' || !/^[A-F0-9]{64}$/.test(pin.checksum)) throw new Error('certificate_extraction_fixture_invalid')
  return data as Fixture
}
async function signedJourney(page: Page) {
  const fixture = requiredFixture(), viewport = page.viewportSize()
  if (!viewport || viewport.width < 320) throw new Error('certificate_extraction_fixture_lane_required')
  const lane = viewport.width <= 420 ? 'narrow' : 'desktop', file = fixture.files[lane]
  const hash = createHash('sha256').update(`${fixture.schemaVersion}:${fixture.runId}:${lane}`).digest('hex')
  const id = `${hash.slice(0, 8)}-${hash.slice(8, 12)}-${hash.slice(12, 16)}-${hash.slice(16, 20)}-${hash.slice(20, 32)}`, base = `/api/v1/vessels/${fixture.vesselId}/certificates`
  const sessionResponse = await page.request.get('/api/v1/auth/session'); expect(sessionResponse.status()).toBe(200); const session = await sessionResponse.json()
  expect(session.authenticated === true && session.actorId === fixture.actorId && session.tenantId === fixture.tenantId).toBe(true)
  expect(typeof session.csrfToken === 'string' && session.csrfToken.length > 16).toBe(true)
  expect((await page.request.get(`${base}/${id}`)).status()).toBe(404)
  const fileResponse = await page.request.get(`/api/v1/vessels/${fixture.vesselId}/files/${file.id}`); expect(fileResponse.status()).toBe(200); const actualFile = await fileResponse.json()
  expect(actualFile.id === file.id && actualFile.revision === file.revision && actualFile.checksum === file.checksum && actualFile.state === 'CleanTransport').toBe(true)
  const metadata = { declaration: { certificateTypeReference: null, certificateNumber: 'SYNTHETIC-ORIGINAL', issuer: 'SYNTHETIC-PRESERVED-ISSUER', issueDate: null, expiryDate: null, limitations: null }, issuingCountryNumeric: null, endorsementRequired: null, endorsementNumber: null, endorsementExpiry: null, stcwReference: null, tankerRelevance: null, vesselTypeScope: null, documentReference: file.id, renewalLeadDaysNumeric: null, sourceSystem: null, notes: 'SYNTHETIC extraction software case; no operational evidence' }
  const content = { metadata, documentName: 'SYNTHETIC masked extraction file', regulatoryBasis: null, declaredFlag: null, availability: null, surveyWindowStart: null, surveyWindowEnd: null, document: file }
  const response = await page.request.post(base, { headers: { 'X-CSRF-TOKEN': session.csrfToken, 'Idempotency-Key': crypto.randomUUID() }, data: { id, expectedVersion: 0, content, reasonCode: 'INITIAL_AUTHORING' } }); expect(response.status()).toBe(200)
  const original = await response.json(); expect(original.currentRevision === false && original.qualification === 'REVIEW_REQUIRED').toBe(true)
  let extractionRequests = 0, confirmationBody = '', confirmationKey = '', actualOutcome = ''
  page.on('request', request => { if (request.url().endsWith(`/certificates/${id}/extractions`)) extractionRequests++ })
  await page.goto(`/app/certificate-command-center/${fixture.vesselId}?id=${id}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const panel = page.getByRole('region', { name: 'Certificate document assistance', exact: true }); await expect(panel).toBeVisible()
  expect(extractionRequests).toBe(0)
  const extracted = page.waitForResponse(r => r.url().endsWith(`/certificates/${id}/extractions`) && r.request().method() === 'POST')
  await panel.getByRole('button', { name: 'Extract from linked document', exact: true }).click(); const extraction = await extracted
  expect(extraction.status()).toBe(200); const preview = await extraction.json(); expect(preview.modelVersion === fixture.modelVersion && preview.promptVersion === fixture.promptVersion && preview.state === 'REVIEW_REQUIRED' && preview.canConfirm === true).toBe(true)
  expect(preview.candidates.every((field: { state: string }) => field.state === 'REVIEW_REQUIRED')).toBe(true)
  const unchanged = await page.request.get(`${base}/${id}`); expect(unchanged.status()).toBe(200); expect((await unchanged.json()).version).toBe(original.version)
  await panel.getByRole('button', { name: 'Confirm selected facts and save draft', exact: true }).click(); await expect(panel.getByRole('alert')).toContainText('Confirm vessel context')
  await panel.getByLabel('Reviewed value · Certificate number', { exact: true }).fill('SYNTHETIC-CORRECTED')
  await panel.getByLabel('I checked this fact against the source · Certificate number', { exact: true }).check(); await panel.getByLabel('I checked that this document belongs to this vessel and certificate', { exact: true }).check()
  // Forward the real mutation, capture its real result, then lose transport.
  // There is no fabricated business response. Explicit retry must use receipt.
  await page.route(`**/certificates/${id}/extractions/confirm`, async route => {
    const request = route.request(); confirmationBody = request.postData() ?? ''; confirmationKey = request.headers()['idempotency-key'] ?? ''
    const actual = await route.fetch(); expect(actual.status()).toBe(200); actualOutcome = await actual.text(); await route.abort('connectionfailed')
    await page.unroute(`**/certificates/${id}/extractions/confirm`)
  }, { times: 1 })
  await panel.getByRole('button', { name: 'Confirm selected facts and save draft', exact: true }).click(); await expect(panel.getByRole('alert')).toContainText('Save outcome unknown')
  const retryResponse = page.waitForResponse(r => r.url().endsWith(`/certificates/${id}/extractions/confirm`) && r.request().method() === 'POST')
  await panel.getByRole('button', { name: 'Retry exact confirmation', exact: true }).click(); const retried = await retryResponse
  expect(retried.status()).toBe(200); expect(retried.request().postData()).toBe(confirmationBody); expect(retried.request().headers()['idempotency-key']).toBe(confirmationKey); expect(await retried.text()).toBe(actualOutcome)
  const saved = await retried.json(); expect(saved.revision === 2 && saved.currentRevision === false && saved.canEdit === false && saved.qualification === 'REVIEW_REQUIRED').toBe(true)
  expect(saved.content.metadata.declaration.certificateNumber).toBe('SYNTHETIC-CORRECTED'); expect(saved.content.metadata.declaration.issuer).toBe('SYNTHETIC-PRESERVED-ISSUER')
  const historyResponse = await page.request.get(`${base}/${id}/history`); expect(historyResponse.status()).toBe(200); expect((await historyResponse.json()).items).toHaveLength(2)
  expect(extractionRequests).toBe(1)
}
export const certificateExtractionCases = [ { name: 'certificate-extraction-signed-explicit-facts-loss-exact-receipt', run: signedJourney, restricted: false } ] as const
