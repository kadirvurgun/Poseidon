import { expect, type Page } from '@playwright/test'

// Genuine signed HTTPS host cases; no route fulfillment, invented login, provider
// execution or implicit fixture provisioning. Missing explicit role config fails.
async function english(page: Page) { await page.getByRole('button', { name: 'EN', exact: true }).click() }
async function menu(page: Page) {
  const drawer = page.getByRole('button', { name: 'Menu', exact: true })
  if (await drawer.isVisible() && await drawer.getAttribute('aria-expanded') === 'false') await drawer.click()
  const workView = page.locator('summary').filter({ hasText: /^Work view$/ })
  if (await workView.locator('..').getAttribute('open') === null) await workView.click()
  return page.getByRole('region', { name: 'Role navigation', exact: true })
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
    const region = await menu(page), select = region.getByLabel('Menu lens', { exact: true })
    await expect(select).toHaveValue('')
    await select.selectOption(current.lenses[0].reference)
    await expect(region.getByRole('navigation', { name: 'Daily work menu', exact: true }).getByRole('button')).toHaveCount(current.lenses[0].items.length)
    if (current.mode === 'Ship') expect(current.lenses.every(l => l.items.every(i => !i.key.startsWith('tug-')))).toBe(true)
    await select.selectOption(current.lenses[1].reference)
    await expect(region.getByRole('navigation', { name: 'Daily work menu', exact: true }).getByRole('button')).toHaveCount(current.lenses[1].items.length)
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
  let region = await menu(page)
  await region.getByLabel('Menu lens', { exact: true }).selectOption(daily!.reference)
  await region.getByRole('button', { name: 'Crew', exact: true }).click()
  const dialog = page.getByRole('dialog', { name: 'Discard the unsaved draft?', exact: true })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(page.getByLabel('Name *', { exact: true })).toHaveValue(draft)
  expect(new URL(page.url()).pathname === `/app/fleet/${ship.id}`).toBe(true)
  region = await menu(page)
  await region.getByRole('button', { name: 'Crew', exact: true }).click()
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
export const navigationRoleLensCases = [
  { name: 'navigation-role-lens-five-real-workspace-families', run: sharedFamilies, restricted: false },
  { name: 'navigation-role-lens-real-dirty-cancel-and-discard', run: realDirtyGuard, restricted: false },
  { name: 'navigation-role-lens-restricted-context-and-selector-denial', run: restrictedContext, restricted: true },
] as const
