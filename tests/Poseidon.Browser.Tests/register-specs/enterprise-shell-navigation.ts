import { expect, type Locator, type Page } from '@playwright/test'

export async function openEnterpriseMenu(page: Page): Promise<Locator> {
  const trigger = page.getByRole('button', { name: 'Menu', exact: true })
  if (await trigger.isVisible() && await trigger.getAttribute('aria-expanded') !== 'true') await trigger.click()
  return page.locator('#workspace-navigation')
}

export async function openWorkView(page: Page) {
  const nav = await openEnterpriseMenu(page), settings = nav.locator('.enterprise-role-settings')
  if (await settings.getAttribute('open') === null) await settings.locator('summary').click()
  return nav
}

export async function revealEnterpriseDestination(page: Page, label: string) {
  const nav = await openEnterpriseMenu(page)
  const destination = nav.locator('.enterprise-nav-item').filter({ has: page.getByText(label, { exact: true }) })
  await expect(destination).toHaveCount(1)
  const group = destination.locator('..').locator('..'), trigger = group.locator('.enterprise-nav-group-trigger')
  if (await trigger.getAttribute('aria-expanded') !== 'true') await trigger.click()
  await expect(destination).toBeVisible()
  return destination
}
