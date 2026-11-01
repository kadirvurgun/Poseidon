import { expect, type Page } from '@playwright/test'
import { createHash } from 'node:crypto'

// Real private signed host/current PostgreSQL/real clamd/storage and explicit synthetic
// CrewFilePolicy + disposable memberships required. Missing configuration FAILS; no mocks/skips.
async function personFiles(page: Page) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json()
  expect(vessels.items.length > 0).toBe(true); const vessel = vessels.items[0].id
  const roster = await (await page.request.get(`/api/v1/vessels/${vessel}/crew`)).json()
  expect(roster.items.length > 0).toBe(true); const assignment = roster.items[0].id
  const path = `/api/v1/vessels/${vessel}/crew/${assignment}/personal-files`
  const manifestResponse = await page.request.get(path); expect(manifestResponse.status()).toBe(200)
  const before = await manifestResponse.json(); expect(before.canWrite && before.kinds.includes('Passport')).toBe(true)
  const revision = before.next.Passport; expect(Number.isSafeInteger(revision)).toBe(true)
  await page.goto(`/app/crew-roster/${vessel}/crew-profile/${assignment}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const region = page.getByRole('region', { name: 'Private person files', exact: true })
  await region.getByLabel('Private file kind', { exact: true }).selectOption('Passport')
  await region.getByLabel('Declared expiry date (unknown if blank)', { exact: true }).fill('2030-01-02')
  await region.getByRole('button', { name: 'Start new private version', exact: true }).click()
  const transport = region.getByRole('region', { name: 'Private person file transport', exact: true })
  const bytes = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==', 'base64')
  await transport.getByLabel('Attachment file', { exact: true }).setInputFiles({ name: 'synthetic-private-original.png', mimeType: 'image/png', buffer: bytes })
  await page.getByRole('link', { name: 'Back to roster', exact: true }).click()
  const dialog = page.getByRole('dialog', { name: 'Unsaved changes', exact: true })
  await expect(dialog).toBeVisible(); await dialog.getByRole('button', { name: 'Cancel', exact: true }).click()
  const committed = page.waitForResponse(r => r.url().includes(`/personal-files/Passport/versions/${revision}/`) && r.url().endsWith('/bytes') && r.request().method() === 'PUT')
  await transport.getByRole('button', { name: 'Upload file', exact: true }).click()
  expect((await committed).status()).toBe(200)
  await expect(region.getByRole('button', { name: `Passport scan · ${revision}`, exact: true })).toBeVisible()
  // Actual page reload tests server persistence and reauthorization, not an in-memory UI snapshot.
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await region.getByRole('button', { name: `Passport scan · ${revision}`, exact: true }).click()
  await transport.getByRole('button', { name: 'document.png', exact: true }).click()
  await expect(transport.getByRole('article', { name: 'File details', exact: true })).toBeVisible()
  const after = await (await page.request.get(path)).json()
  const version = after.items.find((x: { kind: string; revision: number }) => x.kind === 'Passport' && x.revision === revision)
  expect(version.personId).toBe(roster.items[0].personId); expect(version.expiresOn).toBe('2030-01-02'); expect(version.state).toBe('CleanTransport')
  const session = await (await page.request.get('/api/v1/auth/session')).json()
  const access = await page.request.post(`${path}/Passport/versions/${revision}/${version.id}/access`, { headers: { 'X-CSRF-TOKEN': session.csrfToken } })
  expect(access.status()).toBe(200); const capability = await access.json()
  const delivered = await page.request.get(`${path}/Passport/versions/${revision}/${version.id}/bytes`, { headers: { 'X-File-Capability': capability.token } })
  expect(delivered.status()).toBe(200); expect(createHash('sha256').update(await delivered.body()).digest('hex')).toBe(createHash('sha256').update(bytes).digest('hex'))
  const generic = await (await page.request.get(`/api/v1/vessels/${vessel}/files`)).json()
  expect(generic.items.some((x: { id: string }) => x.id === version.id)).toBe(false)
  expect((await page.request.get(`/api/v1/vessels/${vessel}/files/${version.id}`)).status()).toBe(404)
  expect(page.url().includes(capability.token)).toBe(false)
}
async function deniedPersonFiles(page: Page) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json()
  expect(vessels.items.length > 0).toBe(true); const vessel = vessels.items[0].id
  const unknownAssignment = crypto.randomUUID()
  const result = await page.request.get(`/api/v1/vessels/${vessel}/crew/${unknownAssignment}/personal-files`, { headers: { 'X-Role': 'Crewing' } })
  expect(result.status()).toBe(404); expect((await result.text()).includes('items')).toBe(false)
}
async function explicitPhotoSelection(page: Page) {
  const vessels = await (await page.request.get('/api/v1/vessels?limit=25')).json()
  expect(vessels.items.length > 0).toBe(true); const vessel = vessels.items[0].id
  const roster = await (await page.request.get(`/api/v1/vessels/${vessel}/crew`)).json()
  expect(roster.items.length > 0).toBe(true); const assignment = roster.items[0].id
  const root = `/api/v1/vessels/${vessel}/crew/${assignment}`
  const beforeResponse = await page.request.get(`${root}/personal-files`); expect(beforeResponse.status()).toBe(200)
  const manifest = await beforeResponse.json(); expect(manifest.canWrite && manifest.kinds.includes('Photo')).toBe(true)
  const revision = manifest.next.Photo; expect(Number.isSafeInteger(revision)).toBe(true)
  const initialResponse = await page.request.get(`${root}/photo`); expect(initialResponse.status()).toBe(200)
  const initial = await initialResponse.json()
  await page.goto(`/app/crew-roster/${vessel}/crew-profile/${assignment}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  const region = page.getByRole('region', { name: 'Private person files', exact: true })
  await region.getByLabel('Private file kind', { exact: true }).selectOption('Photo')
  await region.getByRole('button', { name: 'Start new private version', exact: true }).click()
  const transport = region.getByRole('region', { name: 'Private person file transport', exact: true })
  const bytes = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==', 'base64')
  await transport.getByLabel('Attachment file', { exact: true }).setInputFiles({ name: 'synthetic-photo.png', mimeType: 'image/png', buffer: bytes })
  const uploaded = page.waitForResponse(r => r.request().method() === 'PUT' && r.url().includes(`/personal-files/Photo/versions/${revision}/`) && r.url().endsWith('/bytes'))
  await transport.getByRole('button', { name: 'Upload file', exact: true }).click()
  expect((await uploaded).status()).toBe(200)
  const unchanged = await (await page.request.get(`${root}/photo`)).json()
  expect(unchanged.selected?.id ?? null).toBe(initial.selected?.id ?? null) // Upload is never selection.
  const photo = region.getByRole('region', { name: 'Selected profile photo', exact: true })
  const committed = page.waitForResponse(r => r.request().method() === 'PUT' && r.url().endsWith(`${root}/photo`))
  await photo.getByRole('button', { name: `Select photo version · ${revision}`, exact: true }).click()
  expect((await committed).status()).toBe(200)
  await expect(photo.getByRole('button', { name: `Review selected photo · ${revision}`, exact: true })).toBeVisible()
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await photo.getByRole('button', { name: `Review selected photo · ${revision}`, exact: true }).click()
  await transport.getByRole('button', { name: 'photo.png', exact: true }).click()
  await expect(transport.getByRole('article', { name: 'File details', exact: true })).toBeVisible()
  const stored = await (await page.request.get(`${root}/photo`)).json()
  expect(stored.state).toBe('SelectedUnverified'); expect(stored.selected.personId).toBe(roster.items[0].personId)
  expect(stored.selected.revision).toBe(revision)
  const cleared = page.waitForResponse(r => r.request().method() === 'PUT' && r.url().endsWith(`${root}/photo`))
  await photo.getByRole('button', { name: 'Clear photo selection', exact: true }).click()
  expect((await cleared).status()).toBe(200)
  await expect(photo.getByText('No photo selected; unknown.', { exact: true })).toBeVisible()
}
export const personFileCases = [
  { name: 'person-file-scanned-private-version-restart', run: personFiles, restricted: false },
  { name: 'person-file-restricted-owner-spoof', run: deniedPersonFiles, restricted: true },
  { name: 'person-photo-explicit-selection-reload-clear', run: explicitPhotoSelection, restricted: false },
] as const
