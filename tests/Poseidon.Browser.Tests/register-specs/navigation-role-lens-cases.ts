import { openWorkView, revealEnterpriseDestination } from './enterprise-shell-navigation.ts'
import { expect, type Page } from '@playwright/test'

// Genuine signed HTTPS host cases; no route fulfillment, invented login, provider
// execution or implicit fixture provisioning. Missing explicit role config fails.
async function english(page: Page) { await page.getByRole('button', { name: 'EN', exact: true }).click() }
async function menu(page: Page) {
  const drawer = page.getByRole('button', { name: 'Menu', exact: true })
  if (await drawer.isVisible() && await drawer.getAttribute('aria-expanded') === 'false') await drawer.click()
  return page.locator('#workspace-navigation')
}
const labels: Record<string, string> = {
  'operational-downtime': 'Operational downtime',
  defects: 'Defects and safeguards', 'work-order-center': 'Work orders', 'fuel-bunker-log': 'Fuel, bunker and ROB', 'port-activity-sof': 'Port activity and SOF',
  'certificate-command-center': 'Certificate center', 'renewal-workflow': 'Certificate renewal', 'noon-report': 'Noon report', 'deck-daily-log': 'Deck daily log', 'engine-daily-log': 'Engine daily log',
  'system-health': 'System health', 'notification-escalation': 'Notifications',
  today: 'Today', fleet: 'Fleet', companies: 'Companies', crew: 'Crew', equipment: 'Equipment',
  maintenance: 'Maintenance plans', import: 'Controlled import', audit: 'Action history',
  sources: 'Sources', requirements: 'Requirements', 'tug-command': 'Tug command',
  'tug-job-board': 'Job board', 'tug-job-intake': 'Job intake',
}
async function expectPrimaryMenu(page: Page, items: { key: string; href: string }[]) {
  const primary = page.locator('#workspace-navigation')
  const response = await page.request.get('/api/v1/workspace'); expect(response.status()).toBe(200)
  const utilities = (await response.json()).destinations.filter((item: { key: string }) => ['system-health', 'notification-escalation'].includes(item.key)) as { key: string; href: string }[]
  const expected = [...items, ...utilities.filter(item => !items.some(candidate => candidate.key === item.key))]
  await expect(primary.locator('.enterprise-nav-item')).toHaveCount(expected.length)
  for (const item of expected) {
    expect(labels[item.key], `No controlled menu label for ${item.key}`).toBeTruthy()
    await expect(await revealEnterpriseDestination(page, labels[item.key])).toBeVisible()
  }
}
async function projection(page: Page, vessel: string) {
  const response = await page.request.get(`/api/v1/workspace/lenses/${vessel}`)
  expect(response.status()).toBe(200)
  expect(response.headers()['cache-control']).toContain('no-store')
  const value = await response.json()
  expect(value.state === 'Configured' && value.lenses.length >= 2).toBe(true)
  return value as { mode: string; lenses: { reference: string; en: string; items: { key: string; href: string }[] }[] }
}
async function vessels(page: Page) {
  const response = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(response.status()).toBe(200)
  const items = (await response.json()).items as { id: string; kind: string; canEdit: boolean }[]
  const ship = items.find(x => x.kind === 'Ship' && x.canEdit), tug = items.find(x => x.kind === 'Tug')
  expect(!!ship && !!tug).toBe(true)
  return { ship: ship!, tug: tug! }
}
async function sharedFamilies(page: Page) {
  const { ship, tug } = await vessels(page)
  for (const [href, vessel] of [
    [`/app/fleet/${ship.id}`, ship.id], [`/app/crew-roster/${ship.id}`, ship.id],
    [`/app/equipment-hierarchy/${ship.id}`, ship.id], [`/app/excel-migration/${ship.id}`, ship.id],
    [`/app/tug-command?vessel=${tug.id}`, tug.id],
  ]) {
    const current = await projection(page, vessel)
    await page.goto(href); await english(page)
    const region = await openWorkView(page), select = region.getByRole('combobox', { name: 'Menu lens', exact: true })
    await expect(select).toHaveValue(current.lenses[0].reference)
    await expectPrimaryMenu(page, current.lenses[0].items)
    if (current.mode === 'Ship') expect(current.lenses.every(l => l.items.every(i => !i.key.startsWith('tug-')))).toBe(true)
    await select.selectOption(current.lenses[1].reference)
    await expectPrimaryMenu(page, current.lenses[1].items)
    await select.focus(); await expect(select).toBeFocused()
    await page.keyboard.press('ArrowUp')
    // Reopening and presentation changes cannot alter scope at the server.
    const after = await projection(page, vessel)
    expect(JSON.stringify(after) === JSON.stringify(current)).toBe(true)
  }
}
async function realDirtyGuard(page: Page) {
  const { ship } = await vessels(page), data = await projection(page, ship.id)
  const daily = data.lenses.find(l => l.items.some(i => i.key === 'crew')); expect(!!daily).toBe(true)
  await page.goto(`/app/fleet/${ship.id}`); await english(page)
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  const draft = 'FICTIONAL-UNSAVED-' + crypto.randomUUID()
  await page.getByLabel('Name *', { exact: true }).fill(draft)
  let region = await openWorkView(page)
  await region.getByRole('combobox', { name: 'Menu lens', exact: true }).selectOption(daily!.reference)
  await (await revealEnterpriseDestination(page, 'Crew')).click()
  const dialog = page.getByRole('dialog', { name: 'Discard the unsaved draft?', exact: true })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue(draft)
  expect(new URL(page.url()).pathname === `/app/fleet/${ship.id}`).toBe(true)
  region = await menu(page)
  await (await revealEnterpriseDestination(page, 'Crew')).click()
  await dialog.getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/crew-roster/${ship.id}$`))
  expect((await (await page.request.get(`/api/v1/vessels/${ship.id}`)).json()).name === draft).toBe(false)
}
async function restrictedContext(page: Page) {
  const unknown = crypto.randomUUID()
  const response = await page.request.get(`/api/v1/workspace/lenses/${unknown}`)
  expect(response.status()).toBe(404)
  expect((await response.text()).includes('lenses')).toBe(false)
  expect((await page.request.get('/api/v1/workspace/lenses?role=admin')).status()).toBe(400)
  await page.goto(`/app/fleet/${unknown}`); await english(page)
  const region = await menu(page)
  await expect(region.getByText('Navigation unavailable.', { exact: true })).toBeVisible()
  await expect(region.getByRole('combobox')).toHaveCount(0)
  await expect(region.getByRole('button', { name: 'Search workspace', exact: true })).toHaveCount(0)
}
async function contextTargets(page: Page) {
  const response = await page.request.get('/api/v1/vessels?offset=0&limit=25')
  expect(response.status()).toBe(200)
  const rows = (await response.json()).items as { id: string; code: string; name: string; kind: 'Ship' | 'Tug'; canEdit: boolean }[]
  const ship = rows.find(row => row.kind === 'Ship' && row.canEdit)
  const tug = rows.find(row => row.kind === 'Tug')
  expect(ship, 'The owned signed fixture must expose an editable ship').toBeTruthy()
  expect(tug, 'The owned signed fixture must expose an authorized tug').toBeTruthy()
  return { ship: ship!, tug: tug! }
}
async function contextChooser(page: Page) {
  const trigger = page.getByRole('region', { name: 'Current workspace context', exact: true })
    .getByRole('button', { name: 'Change vessel context', exact: true })
  await trigger.focus(); await page.keyboard.press('Enter')
  const chooser = page.getByRole('dialog', { name: 'Change vessel context', exact: true })
  await expect(chooser.getByRole('searchbox', { name: 'Vessel name or code', exact: true })).toBeFocused()
  return chooser
}
async function realGlobalContextSwitch(page: Page) {
  const { ship, tug } = await contextTargets(page)
  await page.goto(`/app/today/${ship.id}`); await english(page)
  for (const target of [tug, ship]) {
    const chooser = await contextChooser(page)
    const button = chooser.getByRole('button', { name: `${target.code} · ${target.name} · ${target.kind}`, exact: true })
    await button.focus()
    const checked = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/workspace/context/${target.id}`)
    await page.keyboard.press('Enter')
    const response = await checked
    expect(response.status()).toBe(200)
    const value = await response.json()
    expect(value.vessel.id).toBe(target.id)
    expect(value.vessel.kind).toBe(target.kind)
    await expect(page).toHaveURL(new RegExp(`/app/today/${target.id}$`))
    await expect(chooser).toHaveCount(0)
    const context = page.getByRole('region', { name: 'Current workspace context', exact: true })
    await expect(context).toContainText(`${target.code} · ${target.name}`)
    await expect(context).toContainText('Role context:')
  }
}
async function realGlobalContextDirtyGuard(page: Page) {
  const { ship, tug } = await contextTargets(page)
  const original = await (await page.request.get(`/api/v1/vessels/${ship.id}`)).json()
  await page.goto(`/app/fleet/${ship.id}`); await english(page)
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  const draft = 'FICTIONAL-CONTEXT-DRAFT-' + crypto.randomUUID()
  await page.getByLabel('Name *', { exact: true }).fill(draft)
  let chooser = await contextChooser(page)
  const targetName = `${tug.code} · ${tug.name} · Tug`
  await chooser.getByRole('button', { name: targetName, exact: true }).click()
  const confirmation = page.getByRole('dialog', { name: 'Discard the unsaved draft?', exact: true })
  await expect(confirmation).toBeVisible()
  await confirmation.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${ship.id}$`))
  await expect(chooser).toBeVisible()
  // The older native chooser must become usable again after the newer native
  // dirty confirmation closes; Escape returns to the untouched editor.
  await page.keyboard.press('Escape')
  await expect(chooser).toHaveCount(0)
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue(draft)
  chooser = await contextChooser(page)
  await chooser.getByRole('button', { name: targetName, exact: true }).click()
  await expect(confirmation).toBeVisible()
  await confirmation.getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${tug.id}$`))
  const after = await (await page.request.get(`/api/v1/vessels/${ship.id}`)).json()
  expect(after.name).toBe(original.name)
  expect(after.version).toBe(original.version)
}
export const navigationRoleLensCases = [
  { name: 'navigation-role-lens-five-real-workspace-families', run: sharedFamilies, restricted: false },
  { name: 'navigation-role-lens-real-dirty-cancel-and-discard', run: realDirtyGuard, restricted: false },
  { name: 'navigation-role-lens-restricted-context-and-selector-denial', run: restrictedContext, restricted: true },
  { name: 'navigation-role-lens-global-context-ship-tug-keyboard-switch', run: realGlobalContextSwitch, restricted: false },
  { name: 'navigation-role-lens-global-context-real-nested-dirty-guard', run: realGlobalContextDirtyGuard, restricted: false },
] as const
