import { test, expect, type Page } from '@playwright/test'

// DEV-021/022/025; T-S2-002/003/006. Authored, execution deferred to S2 bundle.
// Synthetic transport proves browser behavior only, never server authorization.
const tenantId = '10000000-0000-0000-0000-000000000098'
const actorId = '10000000-0000-0000-0000-000000000099'
const masterRole = '20000000-0000-0000-0000-000000000001'
const engineerRole = '20000000-0000-0000-0000-000000000002'
const vessels = [
  { id: '30000000-0000-0000-0000-000000000001', companyId: tenantId, code: 'SYN-A', name: 'FICTIONAL Alpha', kind: 'Ship', version: 1, canEdit: true },
  { id: '30000000-0000-0000-0000-000000000002', companyId: tenantId, code: 'SYN-B', name: 'FICTIONAL Beta', kind: 'Ship', version: 1, canEdit: true },
]
const roles = [
  { reference: masterRole, tr: 'KURGUSAL kaptan', en: 'FICTIONAL Master' },
  { reference: engineerRole, tr: 'KURGUSAL başmühendis', en: 'FICTIONAL Engineer' },
]

async function mockShell(page: Page) {
  const control = { denyTarget: false, wrongTenant: false, targetContextReads: 0, targetTodayReads: 0, writes: 0 }
  await page.route('**/api/v1/**', async route => {
    const request = route.request(), url = new URL(request.url()), path = url.pathname
    if (request.method() !== 'GET') {
      control.writes++
      await route.fulfill({ status: 405, json: { code: 'unexpected_mutation' } }); return
    }
    if (path.endsWith('/auth/session')) {
      await route.fulfill({ json: { authenticated: true, tenantId, actorId, tenantName: 'FICTIONAL workspace',
        canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, csrfToken: 'synthetic-browser-only' } }); return
    }
    if (path === '/api/v1/workspace') {
      await route.fulfill({ json: { destinations: [{ key: 'fleet', href: '/app/fleet' }, { key: 'today', href: '/app/today' }],
        canSearch: false, canWriteRegulatory: false } }); return
    }
    if (path.startsWith('/api/v1/workspace/context')) {
      const selected = vessels.find(v => path.endsWith('/' + v.id))
      if (selected?.id === vessels[1].id) {
        control.targetContextReads++
        if (control.denyTarget) { await route.fulfill({ status: 404, json: {} }); return }
      }
      await route.fulfill({ json: { tenantId: selected?.id === vessels[1].id && control.wrongTenant ? actorId : tenantId,
        actorId, roleContext: { state: 'Configured', catalogVersion: 'synthetic-menu-v1', roles },
        vessel: selected ? { id: selected.id, code: selected.code, name: selected.name, kind: selected.kind } : null } }); return
    }
    if (path.startsWith('/api/v1/workspace/lenses')) {
      const selected = vessels.find(v => path.endsWith('/' + v.id)), vesselId = selected?.id ?? null
      await route.fulfill({ json: { tenantId, actorId, vesselId, mode: selected ? 'Ship' : 'Fleet', state: 'Configured',
        version: 'synthetic-menu-v1', canSearch: true, lenses: [
          { ...roles[0], items: [{ key: 'today', href: '/app/today' + (vesselId ? '/' + vesselId : ''), group: 'daily' },
            { key: 'fleet', href: '/app/fleet', group: 'daily' }] },
          { ...roles[1], items: vesselId ? [{ key: 'maintenance', href: '/app/maintenance-plan/' + vesselId, group: 'daily' }] : [] },
        ] } }); return
    }
    if (path === '/api/v1/vessels') {
      await route.fulfill({ json: { items: vessels, offset: 0, limit: 25, hasMore: false } }); return
    }
    const selected = vessels.find(v => path === '/api/v1/vessels/' + v.id)
    if (selected) { await route.fulfill({ json: selected }); return }
    if (path === '/api/v1/poseidon-today') {
      const vessel = vessels.find(v => v.id === url.searchParams.get('vesselId'))!
      if (vessel.id === vessels[1].id) control.targetTodayReads++
      await route.fulfill({ json: { vessel, readAt: '2026-11-04T12:00:00Z',
        coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning']
          .map((source, i) => ({ source, status: i === 0 ? 'Missing' : 'Unavailable' })),
        cards: { items: [{ key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftAbsent', owner: null,
          nextAction: 'ReviewDraft', href: `/app/fleet/${vessel.id}/profile` }], offset: 0, limit: 25, hasMore: false } } }); return
    }
    await route.fulfill({ status: 404, json: { code: 'unconfigured_synthetic_read' } })
  })
  return control
}

async function openChooser(page: Page) {
  const context = page.getByRole('region', { name: 'Current workspace context', exact: true })
  const trigger = context.getByRole('button', { name: 'Change vessel context', exact: true })
  await trigger.focus(); await page.keyboard.press('Enter')
  return page.getByRole('dialog', { name: 'Change vessel context', exact: true })
}

test('S2 keyboard vessel switch rechecks current access and rejects a mismatched tenant projection', async ({ page }) => {
  const control = await mockShell(page)
  await page.goto(`/app/today/${vessels[0].id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const context = await openChooser(page)
  const target = context.getByRole('button', { name: 'SYN-B · FICTIONAL Beta · Ship', exact: true })
  control.denyTarget = true
  await Promise.all([page.waitForResponse(r => r.url().endsWith('/workspace/context/' + vessels[1].id) && r.status() === 404), target.click()])
  await expect(context.getByText('Vessel access could not be confirmed. Choose another vessel or retry.', { exact: true })).toBeVisible()
  await expect(page).toHaveURL(new RegExp(`/app/today/${vessels[0].id}$`))
  expect(control.targetTodayReads).toBe(0)
  control.denyTarget = false; control.wrongTenant = true
  await expect(target).toBeEnabled()
  await Promise.all([page.waitForResponse(r => r.url().endsWith('/workspace/context/' + vessels[1].id) && r.status() === 200), target.click()])
  await expect(context.getByText('Vessel access could not be confirmed. Choose another vessel or retry.', { exact: true })).toBeVisible()
  await expect(page).toHaveURL(new RegExp(`/app/today/${vessels[0].id}$`))
  expect(control.targetTodayReads).toBe(0)
  control.wrongTenant = false
  await expect(target).toBeEnabled()
  await target.focus(); await page.keyboard.press('Enter')
  await expect(page).toHaveURL(new RegExp(`/app/today/${vessels[1].id}$`))
  await expect(page.getByRole('region', { name: 'Current workspace context' })).toContainText('FICTIONAL Beta')
  expect(control.targetContextReads).toBeGreaterThanOrEqual(3)
  expect(control.writes).toBe(0)
})

test('S2 global context change preserves a dirty form until explicit discard', async ({ page }) => {
  const control = await mockShell(page)
  await page.goto(`/app/fleet/${vessels[0].id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  await page.getByLabel('Name *', { exact: true }).fill('Unsaved operator draft')
  let context = await openChooser(page)
  await context.getByRole('button', { name: 'SYN-B · FICTIONAL Beta · Ship', exact: true }).click()
  const dialog = page.getByRole('dialog', { name: 'Discard the unsaved draft?', exact: true })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${vessels[0].id}$`))
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue('Unsaved operator draft')
  context = page.getByRole('dialog', { name: 'Change vessel context', exact: true })
  await context.getByRole('button', { name: 'SYN-B · FICTIONAL Beta · Ship', exact: true }).click()
  await dialog.getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${vessels[1].id}$`))
  await expect(page.getByRole('region', { name: 'Details', exact: true })).toContainText('FICTIONAL Beta')
  expect(control.writes).toBe(0)
})

test('S2 primary menu follows the selected role lens without granting full product search', async ({ page }) => {
  await mockShell(page)
  await page.goto(`/app/today/${vessels[0].id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const trigger = page.getByRole('button', { name: 'Menu', exact: true })
  if (await trigger.isVisible()) await trigger.click()
  const navigation = page.getByRole('navigation', { name: 'POSEIDON', exact: true })
    .or(page.getByRole('dialog', { name: 'POSEIDON', exact: true }))
  await expect(navigation.getByRole('button', { name: 'Today', exact: true })).toBeVisible()
  await navigation.getByRole('combobox', { name: 'Menu lens', exact: true }).selectOption(engineerRole)
  await expect(navigation.getByRole('button', { name: 'Maintenance plans', exact: true })).toBeVisible()
  await expect(navigation.getByRole('button', { name: 'Today', exact: true })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Search workspace', exact: true })).toHaveCount(0)
  if (await trigger.isVisible()) { await page.keyboard.press('Escape'); await expect(trigger).toBeFocused() }
  await page.keyboard.press('Control+k')
  await expect(page.getByRole('dialog', { name: 'Search workspace', exact: true })).toHaveCount(0)
})

test('existing vessel form actions align and remain keyboard accessible without horizontal overflow', async ({ page }) => {
  await mockShell(page)
  await page.goto(`/app/fleet/${vessels[0].id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue(vessels[0].name)
  const actions = page.getByRole('group', { name: 'Form actions', exact: true })
  const cancel = actions.getByRole('button', { name: 'Cancel', exact: true })
  const save = actions.getByRole('button', { name: 'Save', exact: true })
  await expect(cancel).toBeVisible(); await expect(save).toBeVisible()
  await cancel.focus(); await page.keyboard.press('Tab'); await expect(save).toBeFocused()
  const [cancelBox, saveBox, actionsBox] = await Promise.all([cancel.boundingBox(), save.boundingBox(), actions.boundingBox()])
  expect(cancelBox).not.toBeNull(); expect(saveBox).not.toBeNull(); expect(actionsBox).not.toBeNull()
  expect(cancelBox!.height).toBeGreaterThanOrEqual(40)
  expect(saveBox!.height).toBeGreaterThanOrEqual(40)
  expect(Math.abs(cancelBox!.y - saveBox!.y)).toBeLessThanOrEqual(2)
  expect(Math.abs(cancelBox!.height - saveBox!.height)).toBeLessThanOrEqual(2)
  expect(saveBox!.x - (cancelBox!.x + cancelBox!.width)).toBeGreaterThanOrEqual(8)
  expect(saveBox!.x + saveBox!.width).toBeLessThanOrEqual(actionsBox!.x + actionsBox!.width + 1)
  const width = await page.evaluate(() => ({ content: document.documentElement.scrollWidth, viewport: document.documentElement.clientWidth }))
  expect(width.content).toBeLessThanOrEqual(width.viewport + 1)
})
