import { expect, type Page } from '@playwright/test'

// Actual HTTPS API, EF/PostgreSQL and authenticated browser only. No lab is run by authoring.
// Bundle operator provisions import.read/write + equipment.read/write for the disposable actor.
// The restricted actor deliberately has no import capability. No role/signature is fabricated.
function workbook(headers: string[], values: string[]) {
  const escape = (s: string) => s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;')
  const row = (cells: string[], n: number) => `<row r="${n}">${cells.map((s, i) => `<c r="${String.fromCharCode(65 + i)}${n}" t="inlineStr"><is><t>${escape(s)}</t></is></c>`).join('')}</row>`
  const entries: [string, string][] = [
    ['[Content_Types].xml', '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'],
    ['_rels/.rels', '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'],
    ['xl/workbook.xml', '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Import" sheetId="1" r:id="rId1"/></sheets></workbook>'],
    ['xl/_rels/workbook.xml.rels', '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'],
    ['xl/worksheets/sheet1.xml', `<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${row(headers, 1)}${row(values, 2)}</sheetData></worksheet>`],
  ]
  // Small stored ZIP fixture; no parser dependency or generated private artifact.
  const chunks: Buffer[] = [], directory: Buffer[] = []; let offset = 0
  for (const [name, text] of entries) {
    const filename = Buffer.from(name), data = Buffer.from(text); let crc = 0xffffffff
    for (const byte of data) { crc ^= byte; for (let i = 0; i < 8; i++) crc = crc & 1 ? 0xedb88320 ^ (crc >>> 1) : crc >>> 1 }
    crc = (crc ^ 0xffffffff) >>> 0
    const local = Buffer.alloc(30); local.writeUInt32LE(0x04034b50); local.writeUInt16LE(20, 4); local.writeUInt32LE(crc, 14); local.writeUInt32LE(data.length, 18); local.writeUInt32LE(data.length, 22); local.writeUInt16LE(filename.length, 26)
    const central = Buffer.alloc(46); central.writeUInt32LE(0x02014b50); central.writeUInt16LE(20, 4); central.writeUInt16LE(20, 6); central.writeUInt32LE(crc, 16); central.writeUInt32LE(data.length, 20); central.writeUInt32LE(data.length, 24); central.writeUInt16LE(filename.length, 28); central.writeUInt32LE(offset, 42)
    chunks.push(local, filename, data); directory.push(central, filename); offset += local.length + filename.length + data.length
  }
  const dir = Buffer.concat(directory), end = Buffer.alloc(22); end.writeUInt32LE(0x06054b50); end.writeUInt16LE(entries.length, 8); end.writeUInt16LE(entries.length, 10); end.writeUInt32LE(dir.length, 12); end.writeUInt32LE(offset, 16)
  return Buffer.concat([...chunks, dir, end])
}
async function vessel(page: Page) { const response = await page.request.get('/api/v1/vessels?limit=25'); expect(response.status()).toBe(200); return (await response.json()).items[0].id as string }
async function uploadAndMap(page: Page, invalid: boolean) {
  const id = await vessel(page); await page.goto(`/app/excel-migration/${id}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await page.getByLabel('Category', { exact: true }).selectOption('equipment')
  await page.getByLabel('Single-sheet XLSX source').setInputFiles({ name: 'synthetic-trial.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: workbook(['record_key', 'level'], [invalid ? 'FICTIONAL PRIVATE BAD KEY' : crypto.randomUUID(), 'Equipment']) })
  await page.getByRole('button', { name: 'Upload for mapping', exact: true }).click()
  await page.getByLabel('Record key (UUID) (required)').selectOption('0')
  await page.getByLabel('Level (System/Equipment/Component) (required)').selectOption('1')
  await page.getByRole('button', { name: 'Validate and preview', exact: true }).click()
  await expect(page.getByRole('table', { name: 'Source row outcomes' })).toBeVisible()
  return id
}
async function actualPreviewCommit(page: Page) {
  const id = await uploadAndMap(page, false)
  const batch = new URL(page.url()).searchParams.get('batch')!
  const before = await page.request.get(`/api/v1/vessels/${id}/imports/${batch}`)
  expect((await before.json()).state).toBe('Previewed')
  const committed = page.waitForResponse(r => r.url().endsWith(`/imports/${batch}/commit`) && r.request().method() === 'POST')
  await page.getByRole('button', { name: 'Commit atomic trial import', exact: true }).click()
  expect((await committed).status()).toBe(200)
  await expect(page.getByRole('status')).toContainText('Committed rows: 1')
  await page.reload(); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Commit atomic trial import', exact: true })).toHaveCount(0)
}
async function privateReject(page: Page) {
  await uploadAndMap(page, true)
  await expect(page.getByRole('button', { name: 'Commit atomic trial import', exact: true })).toBeDisabled()
  const url = new URL(page.url()), root = `/api/v1/vessels/${url.pathname.split('/').at(-1)}/imports/${url.searchParams.get('batch')}/previews/${url.searchParams.get('preview')}/rejects`
  const response = await page.request.get(`${root}?revision=${url.searchParams.get('revision')}&fingerprint=${url.searchParams.get('fingerprint')}`)
  expect(response.status()).toBe(200); const csv = await response.text(); expect(csv).toContain('invalid_format'); expect(csv).not.toContain('PRIVATE')
  const stale = await page.request.get(`${root}?revision=0&fingerprint=changed`); expect(stale.status()).toBe(409)
  await expect(page.getByText('FICTIONAL PRIVATE BAD KEY', { exact: true })).toHaveCount(0)
}
async function restrictedImport(page: Page) {
  const id = await vessel(page)
  const response = await page.request.get(`/api/v1/vessels/${id}/imports`); expect(response.status()).toBe(403); expect(await response.text()).not.toContain('items')
  await page.goto(`/app/excel-migration/${id}`); await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText('Import access denied.', { exact: true })).toBeVisible(); await expect(page.getByRole('table')).toHaveCount(0)
}
export const importCases = [
  { name: 'import-real-preview-atomic-commit-reload', run: actualPreviewCommit, restricted: false },
  { name: 'import-real-private-reject-exact-context', run: privateReject, restricted: false },
  { name: 'import-restricted-direct-url-no-counts', run: restrictedImport, restricted: true },
] as const
