import { test, expect } from '@playwright/test'

// Browser editor/transport contract with synthetic routed API; no external IdP claim.
test('profile exact decimal save and malformed background history recovery retain edits', async ({ page }) => {
  const vessel = { id: '10000000-0000-0000-0000-000000000001', companyId: '10000000-0000-0000-0000-000000000002', code: 'S1', name: 'Synthetic Ship', kind: 'Ship', version: 1, canEdit: true }
  const keys = ['imo', 'flag', 'shipType', 'classSociety', 'classNumber', 'gt', 'nt', 'dwt', 'loa', 'breadth', 'draft', 'buildDate', 'tradingArea', 'cargoContext', 'declaration', 'sourceReference', 'sourceObservedAt']
  const content = Object.fromEntries(keys.map(key => [key, { state: 'Missing', value: null }]))
  content.gt = { state: 'Known', value: '9007199254740993' }
  content.nt = { state: 'Known', value: '0.1234567890123456789012345678' }
  const profile = { tenantId: '10000000-0000-0000-0000-000000000003', vessel, version: 19, revision: 1, savedAt: '2026-01-01T00:00:00Z', provenance: 'Synthetic', content }
  let release!: () => void
  const held = new Promise<void>(resolve => { release = resolve })
  let histories = 0
  await page.route('**/api/v1/**', async route => {
    const url = route.request().url()
    let body: unknown
    if (url.includes('/auth/session')) body = { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, csrfToken: 'synthetic', tenantId: profile.tenantId }
    else if (url.includes('/history?')) {
      if (++histories === 1) { await held; body = { items: [null], offset: 0, limit: 25, hasMore: false } }
      else body = { items: [{ revision: 1, savedAt: profile.savedAt, provenance: profile.provenance, content }], offset: 0, limit: 25, hasMore: false }
    } else if (url.includes('/regulatory-profile')) {
      if (route.request().method() === 'PUT') {
        const command = route.request().postDataJSON()
        expect(command.content.gt.value).toBe('9007199254740993')
        expect(command.content.nt.value).toBe('0.1234567890123456789012345678')
        body = { ...profile, revision: 2, version: 20, content: command.content }
      } else body = profile
    } else body = url.includes('/vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel
    await route.fulfill({ json: body })
  })
  await page.goto('/app')
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Synthetic Ship', exact: true }).click()
  await page.getByRole('button', { name: 'Regulatory profile', exact: true }).click()
  await expect(page.getByLabel('GT', { exact: true })).toHaveValue('9007199254740993')
  await page.getByLabel('Flag — Data state').selectOption('Known')
  await page.getByLabel('Flag', { exact: true }).fill('Local retained')
  release()
  await expect(page.getByRole('alert')).toContainText('Unable to load')
  await page.getByRole('button', { name: 'Retry loading', exact: true }).click()
  await expect(page.getByText(/Version 1 ·/)).toBeVisible()
  await expect(page.getByLabel('Flag', { exact: true })).toHaveValue('Local retained')
  await page.getByRole('button', { name: 'Save draft', exact: true }).click()
  await expect(page.getByText('Draft saved.', { exact: true })).toBeVisible()
  await page.getByText(/Version 1 ·/).click()
  await expect(page.getByText('9007199254740993', { exact: true })).toBeVisible()
})
