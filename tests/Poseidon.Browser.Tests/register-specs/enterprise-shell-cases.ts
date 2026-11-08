import { expect, type Page } from '@playwright/test'
import { openEnterpriseMenu } from './enterprise-shell-navigation.ts'

// Genuine signed-host sources. These require a dense server-provisioned menu
// (at least 12 actual destinations across 5 groups), not a mocked navigation
// payload. Missing authority/configuration fails the later bundle gate.
async function signedMenu(page: Page) {
  const response = await page.request.get('/api/v1/auth/session'); expect(response.status()).toBe(200)
  const user = await response.json(); expect(user.authenticated === true && user.canRead === true).toBe(true)
  const wsResponse = await page.request.get('/api/v1/workspace'); expect(wsResponse.status()).toBe(200)
  const workspace = await wsResponse.json()
  const lensResponse = await page.request.get('/api/v1/workspace/lenses'); expect(lensResponse.status()).toBe(200)
  const lenses = await lensResponse.json()
  expect(lenses.actorId === user.actorId && lenses.tenantId === user.tenantId && lenses.vesselId === null).toBe(true)
  const utilities = workspace.destinations.filter((item: { key: string }) => ['system-health', 'notification-escalation'].includes(item.key))
  const items = lenses.state === 'Configured' && lenses.lenses.length ? [...lenses.lenses[0].items, ...utilities.filter((item: { key: string }) => !lenses.lenses[0].items.some((candidate: { key: string }) => candidate.key === item.key))] : workspace.destinations
  expect(items.length >= 12).toBe(true)
  return items.map((item: { key: string }) => item.key).sort() as string[]
}

async function singleNavigationScroll(page: Page) {
  const expectedKeys = await signedMenu(page), previous = page.viewportSize()
  try {
    for (const viewport of [{ width: 1280, height: 900 }, { width: 1280, height: 720 }, { width: 390, height: 844 }, { width: 844, height: 390 }]) {
      await page.setViewportSize(viewport); await page.goto('/app/fleet'); await page.getByRole('button', { name: 'EN', exact: true }).click()
      const nav = await openEnterpriseMenu(page), items = nav.locator('.enterprise-nav-item')
      await expect(items).toHaveCount(expectedKeys.length)
      expect((await items.evaluateAll(nodes => nodes.map(node => node.getAttribute('data-navigation-key')!).sort()))).toEqual(expectedKeys)
      const groups = nav.locator('.enterprise-nav-group-trigger'); expect(await groups.count()).toBeGreaterThanOrEqual(5)
      await expect(nav.locator('.enterprise-role-settings')).not.toHaveAttribute('open')
      for (const trigger of await groups.all()) {
        if (await trigger.getAttribute('aria-expanded') !== 'true') await trigger.click()
        await expect(nav.locator('.enterprise-nav-group-trigger[aria-expanded="true"]')).toHaveCount(1)
        const metrics = await nav.evaluate(element => {
          const scroll = element.querySelector<HTMLElement>('.enterprise-nav-scroll')!, footer = element.querySelector<HTMLElement>('.enterprise-sidebar-footer')!
          return { outer: getComputedStyle(element).overflowY, inner: getComputedStyle(scroll).overflowY,
            scrollHeight: scroll.scrollHeight, clientHeight: scroll.clientHeight, footerBottom: footer.getBoundingClientRect().bottom,
            documentFits: document.documentElement.scrollWidth <= innerWidth }
        })
        expect(metrics.outer).toBe('hidden'); expect(metrics.inner).toBe('auto'); expect(metrics.documentFits).toBe(true)
        expect(metrics.footerBottom <= viewport.height + 1).toBe(true)
        if (viewport.height >= 900) expect(metrics.scrollHeight <= metrics.clientHeight + 1).toBe(true)
      }
      if (viewport.width <= 900) {
        expect(await page.evaluate(() => document.body.style.overflow)).toBe('hidden')
        const overlay = page.getByRole('button', { name: 'Close menu', exact: true })
        const style = await overlay.evaluate(element => { const s = getComputedStyle(element); return { background: s.backgroundColor, padding: s.padding, radius: s.borderRadius, shadow: s.boxShadow } })
        expect(style.background).not.toBe('rgb(255, 255, 255)'); expect(style.padding).toBe('0px'); expect(style.radius).toBe('0px'); expect(style.shadow).toBe('none')
        await page.keyboard.press('Escape'); await expect(page.getByRole('button', { name: 'Menu', exact: true })).toBeFocused()
        expect(await page.evaluate(() => document.body.style.overflow)).not.toBe('hidden')
      }
    }
  } finally { if (previous) await page.setViewportSize(previous) }
}

async function sharedFormControls(page: Page) {
  const response = await page.request.get('/api/v1/vessels?offset=0&limit=25'); expect(response.status()).toBe(200)
  const vessel = (await response.json()).items.find((item: { canEdit: boolean }) => item.canEdit === true); expect(!!vessel).toBe(true)
  await page.goto('/app/fleet/' + vessel.id); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  const form = page.getByRole('form', { name: 'Directory record form', exact: true }), actions = form.getByRole('group', { name: 'Form actions', exact: true })
  const previous = page.viewportSize()
  try {
    for (const viewport of [{ width: 1280, height: 900 }, { width: 390, height: 844 }, { width: 320, height: 640 }]) {
      await page.setViewportSize(viewport); await actions.scrollIntoViewIfNeeded()
      await expect(actions.locator('[data-slot="button"]')).toHaveCount(2)
      await expect(actions.locator('[data-variant="default"]')).toHaveCount(1)
      const boxes = await actions.locator('[data-slot="button"]').evaluateAll(nodes => nodes.map(node => { const r = node.getBoundingClientRect(); return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, height: r.height, width: r.width } }))
      for (const box of boxes) expect(box.left >= 0 && box.right <= viewport.width && box.height >= (viewport.width <= 650 ? 44 : 40)).toBe(true)
      expect(boxes[0].right <= boxes[1].left || boxes[0].bottom <= boxes[1].top || boxes[1].right <= boxes[0].left || boxes[1].bottom <= boxes[0].top).toBe(true)
      if (Math.abs(boxes[0].top - boxes[1].top) < 1) expect(Math.abs(boxes[0].height - boxes[1].height) < 1).toBe(true)
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    }
  } finally { if (previous) await page.setViewportSize(previous) }
}

export const enterpriseShellCases = [
  { name: 'enterprise-shell-signed-single-scroll-and-group-navigation', run: singleNavigationScroll, restricted: false },
  { name: 'enterprise-shell-signed-shared-form-control-geometry', run: sharedFormControls, restricted: false },
] as const
