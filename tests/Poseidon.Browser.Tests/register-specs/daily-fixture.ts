import { createHash } from 'node:crypto'
import { readFileSync, realpathSync, statSync } from 'node:fs'
import { isAbsolute, relative, sep } from 'node:path'
import { expect, type Page } from '@playwright/test'

export type DailyKind = 'Noon' | 'Deck' | 'Engine'
export type DailyFixture = {
  schemaVersion: 'poseidon.daily.signed-fixture.v1'; sourceVersion: string; runId: string
  tenantId: string; vesselId: string; foreignTenantId: string; foreignVesselId: string
  writerActorId: string; reviewerActorId: string; restrictedActorId: string; foreignActorId: string
  reviewerAccount: string; foreignAccount: string; lane: 'desktop' | 'narrow'
}
export const dailySourceVersion = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3'
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/
const validId = (value: unknown): value is string => typeof value === 'string' && uuid.test(value) && value !== '00000000-0000-0000-0000-000000000000'

// This metadata grants nothing. The later disposable-lab preparation must bind
// the actual signed accounts and exact scoped appointments; HTTP proves them.
// Never copy credentials, provider claims, cookies, DOM or outcomes into this file.
export function requiredDailyFixture(page: Page): DailyFixture {
  const name = process.env.POSEIDON_BROWSER_DAILY_FIXTURE
  if (!name || !process.env.POSEIDON_LAB_PRIVATE) throw new Error('daily_signed_fixture_required')
  const root = realpathSync(process.env.POSEIDON_LAB_PRIVATE), path = realpathSync(name), child = relative(root, path)
  if (!child || isAbsolute(child) || child.startsWith('..' + sep) || child === '..' || statSync(path).size > 8192) throw new Error('daily_signed_fixture_boundary')
  let value: Record<string, unknown>
  try { value = JSON.parse(readFileSync(path, 'utf8')) as Record<string, unknown> } catch { throw new Error('daily_signed_fixture_invalid') }
  const ids = ['runId', 'tenantId', 'vesselId', 'foreignTenantId', 'foreignVesselId', 'writerActorId', 'reviewerActorId', 'restrictedActorId', 'foreignActorId']
  const keys = ['schemaVersion', 'sourceVersion', ...ids, 'reviewerAccount', 'foreignAccount']
  if (!value || Array.isArray(value) || Object.keys(value).length !== keys.length || Object.keys(value).some(key => !keys.includes(key)) ||
      value.schemaVersion !== 'poseidon.daily.signed-fixture.v1' || value.sourceVersion !== dailySourceVersion || ids.some(key => !validId(value[key])) ||
      value.tenantId === value.foreignTenantId || value.vesselId === value.foreignVesselId ||
      new Set([value.writerActorId, value.reviewerActorId, value.restrictedActorId, value.foreignActorId]).size !== 4 ||
      ['reviewerAccount', 'foreignAccount'].some(key => typeof value[key] !== 'string' || !/^[a-z][a-z0-9-]{2,63}$/.test(value[key] as string))) throw new Error('daily_signed_fixture_invalid')
  const viewport = page.viewportSize()
  if (!viewport || viewport.width < 320) throw new Error('daily_signed_fixture_lane_required')
  return { ...value, lane: viewport.width <= 420 ? 'narrow' : 'desktop' } as DailyFixture
}
function scopedId(fixture: DailyFixture, kind: DailyKind, purpose: string) {
  const h = createHash('sha256').update(`${fixture.schemaVersion}:${fixture.runId}:${fixture.lane}:${kind}:${purpose}`).digest('hex')
  return `${h.slice(0,8)}-${h.slice(8,12)}-${h.slice(12,16)}-${h.slice(16,20)}-${h.slice(20,32)}`
}
export const dailyRecordId = (fixture: DailyFixture, kind: DailyKind, purpose: 'ordinary' | 'review' | 'offline' = 'ordinary') => scopedId(fixture, kind, purpose)
export const dailyAuthorityId = (fixture: DailyFixture, kind: DailyKind) => scopedId(fixture, kind, 'review-authority')
export async function dailySignedSession(page: Page, fixture: DailyFixture, role: 'writer' | 'reviewer' | 'restricted' | 'foreign' = 'writer') {
  const response = await page.request.get('/api/v1/auth/session'); expect(response.status()).toBe(200)
  const user = await response.json()
  expect(user.authenticated === true && user.setupRequired === false).toBe(true)
  expect(user.actorId === fixture[`${role}ActorId`]).toBe(true)
  expect(user.tenantId === (role === 'foreign' ? fixture.foreignTenantId : fixture.tenantId)).toBe(true)
  expect(typeof user.csrfToken === 'string' && user.csrfToken.length > 16).toBe(true)
  return user as { actorId: string; tenantId: string; csrfToken: string }
}
