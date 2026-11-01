import { expect, type Page } from '@playwright/test'
import { createHash } from 'node:crypto'

// Actual signed provider + PostgreSQL + configured local storage + REAL clamd required.
// No route fulfillment, successful upload mock, authentication replacement or implicit operator configuration.
async function secureFiles(page: Page) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json()
  expect(vessels.items.length > 1).toBe(true)
  const vessel = vessels.items[0].id
  const initial = await page.request.get(`/api/v1/vessels/${vessel}/files`)
  expect(initial.status()).toBe(200)
  const settings = await initial.json(); expect(settings.available && settings.canWrite).toBe(true)
  const name = `synthetic-${crypto.randomUUID()}.png`
  const bytes = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==', 'base64')
  await page.goto(`/app/fleet/${vessel}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  const region = page.getByRole('region', { name: 'Vessel files', exact: true })
  await region.getByLabel('PNG file', { exact: true }).setInputFiles({ name, mimeType: 'image/png', buffer: bytes })
  await page.getByRole('button', { name: 'Switch vessel', exact: true }).click()
  await expect(page.getByRole('dialog', { name: 'Discard the unsaved draft?' })).toBeVisible()
  await page.getByRole('button', { name: 'Keep editing', exact: true }).click()
  await region.getByRole('button', { name: 'Upload file', exact: true }).click()
  await expect(region.getByText('Transport scanned. Evidence remains unverified.', { exact: true })).toBeVisible()
  await region.getByRole('button', { name, exact: true }).click()
  await expect(region.getByRole('article', { name: 'File details' })).toBeVisible()
  const rows = await (await page.request.get(`/api/v1/vessels/${vessel}/files`)).json()
  const file = rows.items.find((x: { name: string }) => x.name === name)
  expect(file.state).toBe('CleanTransport'); expect(file.revision).toBe(1)
  expect(file.checksum).toBe(createHash('sha256').update(bytes).digest('hex').toUpperCase())
  const session = await (await page.request.get('/api/v1/auth/session')).json()
  const issued = await page.request.post(`/api/v1/vessels/${vessel}/files/${file.id}/access`, { headers: { 'X-CSRF-TOKEN': session.csrfToken } })
  expect(issued.status()).toBe(200)
  const capability = await issued.json()
  const delivered = await page.request.get(`/api/v1/vessels/${vessel}/files/${file.id}/bytes`, { headers: { 'X-File-Capability': capability.token } })
  expect(delivered.status()).toBe(200); expect(await delivered.body()).toEqual(bytes)
  expect((await page.request.get(`/api/v1/vessels/${vessel}/files/${file.id}/bytes`)).status()).toBe(403)
  expect((await page.request.get(`/api/v1/vessels/${vessels.items[1].id}/files/${file.id}/bytes`, { headers: { 'X-File-Capability': capability.token } })).status()).toBe(404)
  // URL contains no capability. No file activates readiness or supplies verification/signature.
  expect(page.url().includes(capability.token)).toBe(false)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
}

async function deniedFiles(page: Page) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json()
  expect(vessels.items.length > 0).toBe(true)
  const vessel = vessels.items[0].id
  expect((await page.request.get(`/api/v1/vessels/${vessel}/files`)).status()).toBe(403)
  expect((await page.request.post(`/api/v1/vessels/${vessel}/files`, { data: { name: 'synthetic.png', role: 'Marine-admin', clean: true } })).status()).toBe(403)
}

export const fileCases = [
  { name: 'file-transport-real-scanner', run: secureFiles, restricted: false },
  { name: 'file-default-deny', run: deniedFiles, restricted: true },
] as const
