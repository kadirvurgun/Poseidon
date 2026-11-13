import { expect, type Locator, type Page } from '@playwright/test'
import type { requireQualifiedContext } from './qualified-owning-fixture.ts'
type Context = Awaited<ReturnType<typeof requireQualifiedContext>>

// Exercise real pagination controls. Descriptor IDs are never injected into DOM
// options or requests, and late-lane fixtures need not remain in the first25.
export async function openQualifiedContext(page: Page, context: Context): Promise<Locator> {
  await page.goto('/app/sources')
  await page.getByRole('combobox', { name: /^(Dil seçin|Choose language)$/ }).selectOption('en')
  await page.getByRole('button', { name: 'Lifecycle and applicability', exact: true }).click()
  const region = page.getByRole('region', { name: 'Lifecycle and applicability preparation', exact: true })
  await expect(region.getByRole('list').first()).toBeVisible()
  for (let offset = 0; offset < context.ruleOffset; offset += 25) {
    const [response] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/rules' && new URL(r.url()).searchParams.get('offset') === String(offset + 25)),
      region.getByRole('navigation', { name: 'Rule and context pages', exact: true }).first().getByRole('button', { name: 'Next', exact: true }).click()])
    expect(response.status()).toBe(200)
    const value = await response.json()
    expect(value.items.length > 0).toBe(true)
    await expect(region.getByRole('button', { name: new RegExp(value.items[0].current.id) }).first()).toBeVisible()
  }
  const vessels = region.getByRole('combobox', { name: 'Vessel context', exact: true })
  await expect.poll(() => vessels.locator('option').count()).toBeGreaterThan(1)
  for (let offset = 0; await vessels.locator(`option[value="${context.vessel.id}"]`).count() === 0; offset += 25) {
    expect(offset < 4975).toBe(true)
    const [response] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/vessels' && new URL(r.url()).searchParams.get('offset') === String(offset + 25)),
      region.getByRole('navigation', { name: 'Rule and context pages', exact: true }).nth(1).getByRole('button', { name: 'Next', exact: true }).click()])
    expect(response.status()).toBe(200)
    const value = await response.json()
    expect(value.items.length > 0).toBe(true)
    await expect(vessels.locator(`option[value="${value.items[0].id}"]`)).toHaveCount(1)
  }
  await vessels.selectOption(context.vessel.id)
  await region.getByRole('checkbox', { name: new RegExp(context.rule.current.id) }).check()
  return region
}

export async function selectQualifiedOwningReferences(page: Page, region: Locator, context: Context, fields: readonly string[]) {
  const selector = region.getByRole('group', { name: 'Owning input references', exact: true })
  const labels: Record<string, string> = { cargoPlan: 'Cargo plan', operation: 'Operation', winchFitment: 'Winch fitment' }
  const selected: Record<string, string> = {}
  let offset = 0
  for (const field of fields) {
    const target = context.evidenceOffsets[field], control = selector.getByRole('combobox', { name: labels[field], exact: true })
    while (offset !== target) {
      const next = offset < target, destination = offset + (next ? 25 : -25)
      const [response] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/evidence' && new URL(r.url()).searchParams.get('offset') === String(destination) && new URL(r.url()).searchParams.get('vesselId') === context.vessel.id),
        selector.getByRole('button', { name: next ? 'More references' : 'Previous references', exact: true }).click()])
      expect(response.status()).toBe(200)
      const value = await response.json(), ids = value.items.filter((row: any) => row.content[field]).map((row: any) => row.id)
      const expected = ['', ...(selected[field] && !ids.includes(selected[field]) ? [selected[field]] : []), ...ids]
      await expect.poll(() => control.locator('option').evaluateAll(options => options.map(option => (option as HTMLOptionElement).value))).toEqual(expected)
      offset = destination
    }
    await control.selectOption(context.rows[field].id)
    selected[field] = context.rows[field].id
  }
}
