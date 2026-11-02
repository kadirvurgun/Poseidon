import { test, expect } from '@playwright/test'

// Browser transport negative case only; no mock successful upload or backend authority claim.
test('quarantined transport cannot be downloaded and malformed success is withheld', async ({ page }) => {
  const vessel = { id: '10000000-0000-0000-0000-000000000001', companyId: '10000000-0000-0000-0000-000000000002', code: 'SYN', name: 'Synthetic Vessel', kind: 'Ship', version: 1, canEdit: false }
  const pending = { id: '10000000-0000-0000-0000-000000000003', vesselId: vessel.id, name: 'quarantined.png', state: 'Receiving', length: 10, checksum: null, revision: null, version: 1 }
  let malformed = false
  await page.route('**/api/v1/**', async route => {
    const url = route.request().url()
    const body = url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, csrfToken: 'synthetic' }
      : url.includes('/files?') ? { items: [{ ...pending, state: malformed ? ['CleanTransport'] : 'Receiving' }], offset: 0, hasMore: false, canWrite: false, available: false, maxBytes: 0 }
      : url.endsWith(`/files/${pending.id}`) ? pending
      : url.includes('workspace') ? { destinations: [] } : url.includes('vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel
    await route.fulfill({ json: body })
  })
  await page.goto(`/app/fleet/${vessel.id}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const region = page.getByRole('region', { name: 'Vessel files', exact: true })
  await region.getByRole('button', { name: pending.name, exact: true }).click()
  await expect(region.getByRole('article', { name: 'File details' }).getByText('Quarantined / interrupted', { exact: true })).toHaveCount(1)
  await expect(region.getByRole('button', { name: 'Download file' })).toHaveCount(0)
  malformed = true; await region.getByRole('button', { name: 'Refresh files' }).click()
  await expect(region.getByRole('alert')).toBeVisible()
  await expect(region.getByRole('article')).toHaveCount(0)
})
