import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { versionOfflineShell } from '../../scripts/offline-shell-build'
import provenance from './manual/provenance.json'
import { formatManualSize, userManualDownloads, userManualOtherLocale, userManualReviewed } from './user-manual'

// HELP-MANUAL-DOWNLOAD-01: the shipped PDFs are exactly the files the provenance record describes,
// stay drafts (reviewed:false until a human expert review, UG-6/UG-REV-01) and never enter the offline shell.
const folder = resolve(process.cwd(), 'src/help/manual') // vitest runs from frontend/

describe('shipped user manual', () => {
  it('matches its provenance record byte for byte', () => {
    expect(provenance.schemaVersion).toBe('poseidon.help.user-manual-provenance.v1')
    expect(provenance.generator).toBe('tests/Poseidon.Browser.Tests/manual/build-manual.ts')
    expect(provenance.generatorCommit).toMatch(/^[0-9a-f]{40}$/)
    expect(provenance.generatedAt).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/)
    expect(Object.keys(provenance.files).sort()).toEqual(['en', 'tr'])
    for (const locale of ['tr', 'en'] as const) {
      const record = provenance.files[locale]
      expect(record.file).toBe(`poseidon-user-manual-${locale}.pdf`)
      const bytes = readFileSync(resolve(folder, record.file))
      expect(bytes.subarray(0, 5).toString('latin1')).toBe('%PDF-')
      expect(bytes.length).toBe(record.bytes)
      expect(createHash('sha256').update(bytes).digest('hex')).toBe(record.sha256)
    }
  })

  it('is a draft: no review is claimed', () => {
    expect(provenance.reviewed).toBe(false)
    expect(userManualReviewed).toBe(false)
  })

  it('lists both manuals with the reader language first and flags other locales', () => {
    expect(userManualDownloads('tr').map(item => item.locale)).toEqual(['tr', 'en'])
    expect(userManualDownloads('en').map(item => item.locale)).toEqual(['en', 'tr'])
    expect(userManualDownloads('de').map(item => item.locale)).toEqual(['en', 'tr'])
    expect(userManualDownloads('en').every(item => item.bytes === provenance.files[item.locale].bytes)).toBe(true)
    expect(['tr', 'en', 'en-GB'].map(userManualOtherLocale)).toEqual([false, false, false])
    expect(['de', 'ar', 'zh-Hans'].map(userManualOtherLocale)).toEqual([true, true, true])
  })

  it('formats sizes in megabytes with the reader locale', () => {
    expect(formatManualSize(5_662_310, 'en')).toBe('5.4 MB')
    expect(formatManualSize(5_662_310, 'tr')).toBe('5,4 MB')
  })

  it('keeps PDF assets out of the offline shell precache', () => {
    const template = "const files = '__POSEIDON_STATIC_MANIFEST__'; const build = '__POSEIDON_STATIC_BUILD__'"
    const shell = versionOfflineShell(template, [
      { fileName: 'index.html', bytes: '<html></html>' },
      { fileName: 'assets/index-abc123.js', bytes: 'console.log(1)' },
      { fileName: 'assets/poseidon-user-manual-tr-abc123.pdf', bytes: new Uint8Array([37, 80, 68, 70]) },
      { fileName: 'assets/poseidon-user-manual-en-abc123.pdf', bytes: new Uint8Array([37, 80, 68, 70]) },
    ])
    expect(shell).not.toContain('.pdf')
    expect(shell).toContain('/assets/index-abc123.js')
  })
})
