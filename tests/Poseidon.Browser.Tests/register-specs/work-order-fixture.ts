import { createHash } from 'node:crypto'
import { readFileSync, realpathSync, statSync } from 'node:fs'
import { isAbsolute, relative, sep } from 'node:path'
import { expect, type Page } from '@playwright/test'
import { workOrderSourceVersion } from '../../../frontend/src/product/work-order-fields.ts'
export type WorkOrderSignedFixture = { schemaVersion: 'poseidon.work-order.signed-fixture.v1'; runId: string; tenantId: string; vesselId: string; actorId: string; planId: string; planRevision: number }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const guid = (v: unknown): v is string => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(v) && v !== '00000000-0000-0000-0000-000000000000'
export function requireWorkOrderFixture(): WorkOrderSignedFixture {
  const name = process.env.POSEIDON_BROWSER_WORK_ORDER_FIXTURE, privateRoot = process.env.POSEIDON_LAB_PRIVATE
  if (!name || !privateRoot) throw new Error('work_order_signed_fixture_required')
  const root = realpathSync(privateRoot), path = realpathSync(name), child = relative(root, path)
  if (!child || isAbsolute(child) || child === '..' || child.startsWith('..' + sep) || !statSync(path).isFile() || statSync(path).size > 8192) throw new Error('work_order_fixture_boundary')
  let value: unknown
  try { value = JSON.parse(readFileSync(path, 'utf8')) } catch { throw new Error('work_order_fixture_invalid') }
  if (!exact(value, ['schemaVersion', 'runId', 'tenantId', 'vesselId', 'actorId', 'planId', 'planRevision']) || value.schemaVersion !== 'poseidon.work-order.signed-fixture.v1' || !['runId', 'tenantId', 'vesselId', 'actorId', 'planId'].every(key => guid(value[key])) || typeof value.planRevision !== 'number' || !Number.isSafeInteger(value.planRevision) || value.planRevision < 1) throw new Error('work_order_fixture_invalid')
  return value as WorkOrderSignedFixture
}
export function workOrderSignedId(fixture: WorkOrderSignedFixture, page: Page, purpose: string) { const viewport = page.viewportSize(); if (!viewport || viewport.width < 320) throw new Error('work_order_fixture_lane_required'); const h = createHash('sha256').update(fixture.schemaVersion + ':' + fixture.runId + ':' + (viewport.width <= 420 ? 'narrow' : 'desktop') + ':' + purpose).digest('hex'); return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20, 32) }
export async function requireWorkOrderContext(page: Page, fixture: WorkOrderSignedFixture) {
  const session = await page.request.get('/api/v1/auth/session'); expect(session.status()).toBe(200); const user = await session.json()
  expect(user.authenticated === true && user.canRead === true && user.actorId === fixture.actorId && user.tenantId === fixture.tenantId).toBe(true); expect(typeof user.csrfToken === 'string' && user.csrfToken.length > 16).toBe(true)
  const capsResponse = await page.request.get('/api/v1/work-orders/capabilities?vessel=' + fixture.vesselId); expect(capsResponse.status()).toBe(200); const caps = await capsResponse.json()
  expect(caps.vesselId === fixture.vesselId && caps.canRead === true && caps.canWrite === true && caps.canClose === false && caps.canVerify === false && caps.canRelease === false && caps.canSubmitEvidence === false && caps.sourceVersion === workOrderSourceVersion).toBe(true); expect(caps.schema).toHaveLength(25)
  const planResponse = await page.request.get('/api/v1/vessels/' + fixture.vesselId + '/work-order-plan-origins/' + fixture.planId + '?revision=' + fixture.planRevision); expect(planResponse.status()).toBe(200); const plan = await planResponse.json()
  expect(plan.vesselId === fixture.vesselId && plan.current === true && plan.qualification === 'REVIEW_REQUIRED' && plan.pin.id === fixture.planId && plan.pin.revision === fixture.planRevision && plan.pin.version > 0 && /^[A-F0-9]{64}$/.test(plan.pin.hash)).toBe(true)
  const fields: Record<string, { state: 'Known' | 'Missing' | 'Unknown'; value: string | null }> = Object.fromEntries(caps.schema.map((field: { key: string }) => [field.key, { state: 'Missing', value: null }]))
  return { user: user as { csrfToken: string }, fields, plan: plan.pin as { id: string; revision: number; version: number; hash: string; sourceVersion: 'poseidon.maintenance-plan-context.v1' }, sourceVersion: workOrderSourceVersion }
}
