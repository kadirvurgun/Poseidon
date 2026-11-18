import { expect, test } from '@playwright/test'
import { complianceVessel, otherComplianceVessel } from '../../../frontend/src/product/compliance-test-fixtures.ts'

// DEV-039 / T-S5-001 / 18.01 Backlog row40. Synthetic transport proves only
// wizard navigation, retained edits and layout. No real authority/UAT claim.
// Authored only; execute with the larger bundle at actual PR preparation.
for (const width of [390, 1440]) test(`onboarding retains draft between steps and guards leaving at ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 900 })
  const vessel = { id: complianceVessel, companyId: otherComplianceVessel, code: 'SYN', name: 'FICTIONAL onboarding vessel', kind: 'Ship', version: 1, canEdit: true }
  const keys = ['imo', 'flag', 'shipType', 'classSociety', 'classNumber', 'gt', 'nt', 'dwt', 'loa', 'breadth', 'draft', 'buildDate', 'tradingArea', 'cargoContext', 'declaration', 'sourceReference', 'sourceObservedAt', 'mainPropulsionKw', 'contractForConstructionDate', 'constructionRecordReference']
  const content = { ...Object.fromEntries(keys.map(key => [key, { state: 'Missing', value: null }])), constructionRecord: null }
  const profile = { tenantId: otherComplianceVessel, vessel, version: 19, revision: 1, savedAt: '2026-11-04T08:00:00Z', provenance: 'DeclaredUnverified', content }
  let writes = 0
  await page.route('**/api/v1/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname
    if (request.method() !== 'GET') { writes++; await route.fulfill({ status: 405, json: {} }); return }
    if (path.endsWith('/auth/session')) {
      await route.fulfill({ json: { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false,
        actorId: otherComplianceVessel, tenantId: otherComplianceVessel, tenantName: 'FICTIONAL tenant', csrfToken: 'synthetic-only' } }); return
    }
    if (path.endsWith('/regulatory-profile/history')) { await route.fulfill({ json: { items: [], offset: 0, limit: 25, hasMore: false } }); return }
    if (path.endsWith('/regulatory-profile')) { await route.fulfill({ json: profile }); return }
    if (path.endsWith('/files')) { await route.fulfill({ json: { items: [], offset: 0, limit: 25, hasMore: false, available: true, canWrite: false, maxBytes: 1048576, allowedTypes: ['image/png'] } }); return }
    if (path.endsWith('/compliance-onboarding')) { await route.fulfill({ status: 503, json: {} }); return }
    if (path === '/api/v1/vessels') { await route.fulfill({ json: { items: [vessel], offset: 0, limit: 25, hasMore: false } }); return }
    if (path === `/api/v1/vessels/${complianceVessel}`) { await route.fulfill({ json: vessel }); return }
    await route.fulfill({ status: 403, json: {} })
  })
  await page.goto(`/app/fleet/${complianceVessel}/onboarding`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const wizard = page.getByRole('region', { name: 'Pilot vessel onboarding', exact: true })
  await wizard.getByLabel('Flag — Data state').selectOption('Known')
  await wizard.getByLabel('Flag', { exact: true }).fill('Retained local declaration')
  await wizard.getByRole('button', { name: 'Next step', exact: true }).click()
  await expect(wizard.getByLabel('GT', { exact: true })).toBeVisible()
  await wizard.getByRole('button', { name: 'Next step', exact: true }).click()
  await expect(wizard.getByLabel('Trading area', { exact: true })).toBeVisible()
  await wizard.getByRole('button', { name: 'Next step', exact: true }).click()
  await expect(wizard.getByText('Retained local declaration', { exact: true })).toBeVisible()
  await expect(wizard.getByText('Onboarding assessment unavailable.', { exact: true })).toBeVisible()
  await expect(wizard.getByRole('button', { name: 'Submit for review', exact: true })).toBeDisabled()
  expect(writes).toBe(0)

  // No overlap between adjacent step controls at either supported width.
  const previous = await wizard.getByRole('button', { name: 'Previous step', exact: true }).boundingBox()
  const next = await wizard.getByRole('button', { name: 'Next step', exact: true }).boundingBox()
  expect(previous).not.toBeNull(); expect(next).not.toBeNull()
  const overlapX = Math.min(previous!.x + previous!.width, next!.x + next!.width) - Math.max(previous!.x, next!.x)
  const overlapY = Math.min(previous!.y + previous!.height, next!.y + next!.height) - Math.max(previous!.y, next!.y)
  expect(overlapX <= 0 || overlapY <= 0).toBe(true)
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1)
  expect(overflow).toBe(false)

  await page.getByRole('button', { name: 'Back to vessel', exact: true }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(wizard.getByText('Retained local declaration', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Back to vessel', exact: true }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/fleet/${complianceVessel}$`))
  expect(writes).toBe(0)
})
