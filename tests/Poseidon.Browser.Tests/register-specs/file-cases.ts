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
  await region.getByLabel('Attachment file', { exact: true }).setInputFiles({ name, mimeType: 'image/png', buffer: bytes })
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

// FSC-UI-01: real signed host and owner relationships; later gate selects local/S3 adapter
// explicitly. This case never provides fake records, storage or a synthetic clean verdict.
async function documentAttachments(page: Page) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json()
  expect(vessels.items.length > 0).toBe(true)
  const vessel = vessels.items[0].id
  await page.goto(`/app/fleet/${vessel}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByRole('button', { name: 'Document drafts', exact: true }).click()
  const region = page.getByRole('region', { name: 'Document drafts', exact: true })
  await region.getByRole('button', { name: 'New document draft', exact: true }).click()
  const title = 'FICTIONAL document ' + crypto.randomUUID()
  await region.getByLabel('Draft title (optional)', { exact: true }).fill(title)
  await expect(page.getByRole('button', { name: 'Vessel attachments', exact: true })).toBeDisabled()
  await region.getByRole('button', { name: 'Save unverified draft', exact: true }).click()
  const revision = region.getByRole('article', { name: 'Document revision', exact: true })
  await expect(revision.getByRole('heading', { name: title, exact: true })).toBeVisible()
  const listing = await (await page.request.get(`/api/v1/vessels/${vessel}/documents?offset=0`)).json()
  const document = listing.items.find((x: { title: string }) => x.title === title)
  expect(document.status).toBe('DraftUnverified'); expect(document.revision).toBe(1)
  const bytes = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==', 'base64')
  const name = 'synthetic-document-' + crypto.randomUUID() + '.png'
  const attachments = revision.getByRole('region', { name: 'Vessel files', exact: true })
  await attachments.getByLabel('Attachment file', { exact: true }).setInputFiles({ name, mimeType: 'image/png', buffer: bytes })
  await attachments.getByRole('button', { name: 'Upload file', exact: true }).click()
  await expect(attachments.getByText('Transport scanned. Evidence remains unverified.', { exact: true })).toBeVisible()
  const prefix = `/api/v1/vessels/${vessel}/documents/${document.id}/versions/1/files`
  const files = await (await page.request.get(prefix + '?offset=0')).json()
  const file = files.items.find((x: { name: string }) => x.name === name)
  expect(file.checksum).toBe(createHash('sha256').update(bytes).digest('hex').toUpperCase())
  expect((await page.request.get(`/api/v1/vessels/${vessel}/files/${file.id}`)).status()).toBe(404)
  expect((await page.request.get(prefix.replace('/versions/1/', '/versions/2/') + `/${file.id}`)).status()).toBe(404)
  await region.getByRole('button', { name: 'Append draft revision', exact: true }).click()
  await region.getByLabel('Draft title (optional)', { exact: true }).fill(title + ' revision 2')
  await region.getByRole('button', { name: 'Save unverified draft', exact: true }).click()
  await expect(region.getByRole('article', { name: 'Document revision' })).toContainText('Revision 2')
  await expect(region.getByText('No files in this vessel scope.', { exact: true })).toBeVisible()
  const old = await (await page.request.get(`/api/v1/vessels/${vessel}/documents/${document.id}/versions/1`)).json()
  expect(old.title).toBe(title); expect(old.status).toBe('DraftUnverified')
  await page.getByRole('button', { name: 'TR', exact: true }).click()
  await expect(page.getByRole('region', { name: 'Belge taslakları', exact: true })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
}

export const fileCases = [
  { name: 'document-owner-revision-real-attachments', run: documentAttachments, restricted: false },
  { name: 'file-transport-real-scanner', run: secureFiles, restricted: false },
  { name: 'file-default-deny', run: deniedFiles, restricted: true },
] as const
