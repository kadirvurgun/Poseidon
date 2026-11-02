import { expect, type Page } from '@playwright/test'

// Deferred actual private HTTPS/signed-provider/PostgreSQL journeys. The bundle
// operator must configure the disposable actor's real local role labels/capabilities;
// no route mocks, production login bypass or inferred role appointment.
async function authorizedHeaders(page: Page) {
  const vesselsResponse = await page.request.get('/api/v1/vessels?limit=25')
  expect(vesselsResponse.status()).toBe(200)
  const vessels = (await vesselsResponse.json()).items
  const ship = vessels.find((v: { kind: string }) => v.kind === 'Ship')
  expect(ship).toBeTruthy()
  const tugResponse = await page.request.get('/api/v1/tug-command'); expect(tugResponse.status()).toBe(200)
  const tug = (await tugResponse.json()).vessels[0]; expect(tug).toBeTruthy()
  const routes = [
    { url: `/app/fleet/${ship.id}`, id: ship.id },
    { url: `/app/crew-roster/${ship.id}`, id: ship.id },
    { url: `/app/equipment-hierarchy/${ship.id}`, id: ship.id },
    { url: `/app/excel-migration/${ship.id}`, id: ship.id },
    { url: `/app/tug-command?vessel=${tug.id}`, id: tug.id },
  ]
  for (const route of routes) {
    const expected = await page.request.get(`/api/v1/workspace/context/${route.id}`)
    expect(expected.status()).toBe(200)
    const context = await expected.json()
    // Missing fixture configuration is a real failure, not a skip/fallback assertion.
    expect(context.roleContext.state).toBe('Configured'); expect(context.roleContext.roles.length).toBeGreaterThan(0)
    await page.goto(route.url); await page.getByRole('button', { name: 'EN', exact: true }).click()
    const bar = page.getByRole('region', { name: 'Current workspace context', exact: true })
    await expect(bar).toContainText(context.vessel.code)
    await expect(bar).toContainText(context.vessel.name)
    for (const role of context.roleContext.roles) await expect(bar).toContainText(role.en)
    await expect(bar).not.toContainText(context.actorId)
  }
}

async function restrictedVessel(page: Page) {
  const list = await page.request.get('/api/v1/vessels?limit=25'); expect(list.status()).toBe(200)
  const granted = (await list.json()).items; expect(granted.length).toBeGreaterThan(0)
  // Random missing scope is verified against the actual server before direct URL.
  const unknown = crypto.randomUUID()
  expect(granted.some((v: { id: string }) => v.id === unknown)).toBe(false)
  const denied = await page.request.get(`/api/v1/workspace/context/${unknown}`)
  expect(denied.status()).toBe(404); expect(await denied.text()).not.toContain('roleContext')
  await page.goto(`/app/fleet/${unknown}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const bar = page.getByRole('region', { name: 'Current workspace context', exact: true })
  await expect(bar).toContainText('Context access unavailable.')
  await expect(bar).not.toContainText(unknown)
}

export const contextCases = [
  { name: 'workspace-context-all-five-authorized-headers', run: authorizedHeaders, restricted: false },
  { name: 'workspace-context-restricted-direct-url-no-context', run: restrictedVessel, restricted: true },
] as const
