import { test, expect } from '@playwright/test'

// Mocked transport: UI/keyboard/mobile evidence only. Real authorization is in TodayTests.cs.
test('Today honest coverage, Why reauthorization, refresh and offline on desktop/mobile', async ({ page }) => {
  const id = '10000000-0000-0000-0000-000000000001'
  const vessel = { id, companyId: '10000000-0000-0000-0000-000000000002', code: 'T1', name: 'Synthetic Tug', kind: 'Tug', version: 1, canEdit: false }
  const now = '2026-11-01T00:00:00Z'; const href = `/app/fleet/${id}/profile`
  let reads = 0; let deny = false
  await page.route('**/api/v1/**', async route => {
    const url = route.request().url()
    if (url.includes('why-engine')) {
      reads++
      if (deny) { await route.fulfill({ status: 403, json: {} }); return }
      await route.fulfill({ json: { vesselId: id, cardKey: 'regulatory-draft', rule: 'TW-DRAFT-01', reason: 'DraftUnverified', inputs: { known: 0, missing: 17, unknown: 0, reason: 'DraftUnverified' }, readAt: now, savedAt: now, sourceObservedAt: null, freshness: 'Unverified', provenance: 'Synthetic', blockers: ['VerificationUnavailable', 'OperationalSourcesUnavailable'], owner: null, nextAction: 'ViewDraft', href } }); return
    }
    const body = url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, csrfToken: 'synthetic' }
      : { vessel, readAt: now, coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification'].map((source, i) => ({ source, status: i ? 'Unavailable' : 'Available' })), cards: { items: [{ key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftUnverified', owner: null, nextAction: 'ViewDraft', href }], offset: 0, limit: 25, hasMore: false } }
    await route.fulfill({ json: body })
  })
  await page.goto(`/app/today/${id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText(/Current vessel/)).toContainText('Tug')
  await expect(page.getByText(/Unavailable: source not connected/)).toHaveCount(4)
  const why = page.getByRole('button', { name: 'Why?', exact: true })
  await why.focus(); await page.keyboard.press('Enter')
  await expect(page.getByText(/Input source observation: Unknown/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Close Why', exact: true })).toBeFocused()
  await page.getByRole('button', { name: 'Close Why' }).click(); deny = true
  await why.click(); await expect(page.getByText('Access denied.', { exact: true })).toBeVisible()
  await expect(page.getByText(/Current vessel/)).toHaveCount(0)
  await expect(page.getByRole('alert')).toBeFocused()
  expect(reads).toBe(2)
  await expect(page.getByText(/Declared \/ missing/)).toHaveCount(0)
  await page.getByRole('button', { name: 'Refresh', exact: true }).click()
  await expect(page.getByText(/Stored regulatory draft is unverified/)).toBeVisible()
  // navigator.onLine is controlled by the browser context for the offline state.
  await page.context().setOffline(true)
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeDisabled()
  await expect(page.getByText('Offline. Displayed data is stale and unverified.', { exact: true })).toBeVisible()
  await page.context().setOffline(false)
})
