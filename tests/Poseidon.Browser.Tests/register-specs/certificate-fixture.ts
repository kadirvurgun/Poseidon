import { createHash } from 'node:crypto'
import { readFileSync, realpathSync, statSync } from 'node:fs'
import { isAbsolute, relative, sep } from 'node:path'
import { expect, type Page } from '@playwright/test'

type Pin = { id: string; revision: number; checksum: string }
export type CertificateFixture = { schemaVersion: 'poseidon.certificate.signed-fixture.v1'; runId: string; tenantId: string; vesselId: string; actorId: string; ownerRoleId: string | null; files: { desktop: Pin; narrow: Pin } }
const guid = (value: unknown): value is string => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value) && value !== '00000000-0000-0000-0000-000000000000'
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, keys: string[]): value is Record<string, unknown> => object(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key))
export function certificateFixture(): CertificateFixture {
  const name = process.env.POSEIDON_BROWSER_CERTIFICATE_FIXTURE, privateRoot = process.env.POSEIDON_LAB_PRIVATE
  if (!name || !privateRoot) throw new Error('certificate_signed_fixture_required')
  const root = realpathSync(privateRoot), path = realpathSync(name), child = relative(root, path)
  if (!child || isAbsolute(child) || child === '..' || child.startsWith('..' + sep) || statSync(path).size > 8192) throw new Error('certificate_fixture_boundary')
  let value: unknown
  try { value = JSON.parse(readFileSync(path, 'utf8')) } catch { throw new Error('certificate_fixture_invalid') }
  if (!exact(value, ['schemaVersion', 'runId', 'tenantId', 'vesselId', 'actorId', 'ownerRoleId', 'files']) || value.schemaVersion !== 'poseidon.certificate.signed-fixture.v1' || !['runId', 'tenantId', 'vesselId', 'actorId'].every(key => guid(value[key])) || !(value.ownerRoleId === null || guid(value.ownerRoleId)) || !exact(value.files, ['desktop', 'narrow'])) throw new Error('certificate_fixture_invalid')
  for (const pin of Object.values(value.files)) if (!exact(pin, ['id', 'revision', 'checksum']) || !guid(pin.id) || typeof pin.revision !== 'number' || !Number.isSafeInteger(pin.revision) || pin.revision < 1 || typeof pin.checksum !== 'string' || !/^[A-Fa-f0-9]{64}$/.test(pin.checksum)) throw new Error('certificate_fixture_invalid')
  return value as CertificateFixture
}
export function certificateLane(page: Page) {
  const viewport = page.viewportSize()
  if (!viewport || viewport.width < 320) throw new Error('certificate_fixture_lane_required')
  return viewport.width <= 420 ? 'narrow' : 'desktop'
}
export function certificateResource(fixture: CertificateFixture, lane: string, purpose: string) {
  const h = createHash('sha256').update(fixture.schemaVersion + ':' + fixture.runId + ':' + lane + ':' + purpose).digest('hex')
  return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20, 32)
}
export async function certificateSession(page: Page, fixture: CertificateFixture) {
  const response = await page.request.get('/api/v1/auth/session'); expect(response.status()).toBe(200)
  const session = await response.json()
  expect(session.authenticated === true && session.canRead === true && session.actorId === fixture.actorId && session.tenantId === fixture.tenantId).toBe(true)
  expect(typeof session.csrfToken === 'string' && session.csrfToken.length > 16).toBe(true)
  return session as { csrfToken: string }
}
export async function prepareCertificate(page: Page, fixture: CertificateFixture, lane: 'desktop' | 'narrow', purpose: string) {
  const session = await certificateSession(page, fixture), file = fixture.files[lane], id = certificateResource(fixture, lane, purpose)
  const base = '/api/v1/vessels/' + fixture.vesselId + '/certificates'
  const fileRead = await page.request.get('/api/v1/vessels/' + fixture.vesselId + '/files/' + file.id); expect(fileRead.status()).toBe(200)
  const actual = await fileRead.json()
  expect(actual.id === file.id && actual.revision === file.revision && actual.checksum.toUpperCase() === file.checksum.toUpperCase() && actual.state === 'CleanTransport').toBe(true)
  expect((await page.request.get(base + '/' + id)).status()).toBe(404)
  const metadata = { declaration: { certificateTypeReference: null, certificateNumber: 'SYNTHETIC-' + purpose, issuer: null, issueDate: null, expiryDate: null, limitations: null }, issuingCountryNumeric: null, endorsementRequired: null, endorsementNumber: null, endorsementExpiry: null, stcwReference: null, tankerRelevance: null, vesselTypeScope: null, documentReference: file.id, renewalLeadDaysNumeric: null, sourceSystem: null, notes: 'SYNTHETIC SOFTWARE REGRESSION; no operational or verified evidence' }
  const content = { metadata, documentName: 'SYNTHETIC ' + purpose, regulatoryBasis: null, declaredFlag: null, availability: null, surveyWindowStart: null, surveyWindowEnd: null, document: file }
  const saved = await page.request.post(base, { headers: { 'X-CSRF-TOKEN': session.csrfToken, 'Idempotency-Key': certificateResource(fixture, lane, purpose + '-create') }, data: { id, expectedVersion: 0, content, reasonCode: 'INITIAL_AUTHORING' } }); expect(saved.status()).toBe(200)
  const receipt = await saved.json()
  expect(receipt.state === 'DRAFT' && receipt.currentRevision === false && receipt.canEdit === false && receipt.qualification === 'REVIEW_REQUIRED' && receipt.calendar.state === 'REVIEW_REQUIRED').toBe(true)
  const current = await page.request.get(base + '/' + id); expect(current.status()).toBe(200)
  const row = await current.json(); expect(row.currentRevision === true && row.canEdit === true && row.version === receipt.version).toBe(true)
  return { id, base, row }
}
