import { expect, test } from '@playwright/test'
import { notificationInbox, notificationPreference, noticeSource, noticeVessel } from '../../../frontend/src/product/notification.test-fixture.ts'

// T-S4-004 supporting UI only. Synthetic transport does not prove real server
// authority or delivery. Authored; execution is deferred to the actual PR gate.
test('notification preference discard navigates once without a second browser unload confirmation', async ({ page }) => {
  let writes = 0
  const vessel = { id: noticeVessel, companyId: noticeSource, code: 'SYN', name: 'FICTIONAL notification vessel', kind: 'Ship', version: 1, canEdit: false }
  await page.route('**/api/v1/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname
    if (request.method() !== 'GET') { writes++; await route.fulfill({ status: 405, json: {} }); return }
    if (path.endsWith('/auth/session')) {
      await route.fulfill({ json: { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false,
        actorId: noticeSource, tenantId: noticeSource, tenantName: 'FICTIONAL tenant', csrfToken: 'synthetic-only' } }); return
    }
    if (path.endsWith('/notification-escalation/preference')) { await route.fulfill({ json: notificationPreference() }); return }
    if (path.endsWith('/notification-escalation')) { await route.fulfill({ json: notificationInbox() }); return }
    if (path === '/api/v1/vessels') { await route.fulfill({ json: { items: [vessel], offset: 0, limit: 25, hasMore: false } }); return }
    if (path === `/api/v1/vessels/${noticeVessel}`) { await route.fulfill({ json: vessel }); return }
    await route.fulfill({ status: 403, json: {} })
  })
  await page.goto(`/app/notification-escalation?vessel=${noticeVessel}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const preference = page.getByRole('checkbox', { name: 'Mute nonmandatory advisory notifications' })
  await preference.check()
  await page.getByRole('link', { name: 'Open source plan' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Keep editing', exact: true }).click()
  await expect(preference).toBeChecked()
  const unexpectedDialogs: string[] = []
  page.on('dialog', async dialog => { unexpectedDialogs.push(dialog.type()); await dialog.dismiss() })
  await page.getByRole('link', { name: 'Open source plan' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Discard draft', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/app/maintenance-plan/${noticeVessel}`))
  expect(unexpectedDialogs).toEqual([])
  expect(writes).toBe(0)
})
