// USER-GUIDE-01 UG-4 (UG-DOC-01): writes the offline TR/EN user manual.
//   node manual/build-manual.ts [--locale tr|en] [--out <dir>] [--pdf]
// Default output: <repo>/.runtime/user-manual/poseidon-user-manual-<locale>.html (git-ignored; the
// manual is regenerated from the committed guides and screenshots, never committed itself).
// --pdf additionally prints each HTML file to PDF with the already-installed Playwright Chromium.
// No network access: the HTML is self-contained (inline CSS, screenshots embedded as data URIs).

import { mkdirSync, writeFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { loadManualInputs, manualLocales, renderManual, repoRoot } from './manual.ts'
import type { HelpLocale } from '../../../frontend/src/help/help-model.ts'

function parseArgs(argv: string[]) {
  let locales: HelpLocale[] = [...manualLocales], out = join(repoRoot, '.runtime/user-manual'), pdf = false
  for (let index = 0; index < argv.length; index++) {
    const arg = argv[index]
    if (arg === '--pdf') pdf = true
    else if (arg === '--locale') {
      const value = argv[++index]
      if (!(manualLocales as readonly string[]).includes(value)) throw new Error(`unsupported locale: ${value}`)
      locales = [value as HelpLocale]
    } else if (arg === '--out') {
      const value = argv[++index]
      if (!value) throw new Error('--out requires a directory')
      out = resolve(value)
    } else throw new Error(`unknown argument: ${arg}`)
  }
  return { locales, out, pdf }
}

const { locales, out, pdf } = parseArgs(process.argv.slice(2))
mkdirSync(out, { recursive: true })
const written: string[] = []
for (const locale of locales) {
  const html = renderManual(loadManualInputs(locale))
  const file = join(out, `poseidon-user-manual-${locale}.html`)
  writeFileSync(file, html, 'utf8')
  written.push(file)
  console.log(`${file} (${(Buffer.byteLength(html) / 1024 / 1024).toFixed(1)} MB)`)
}

if (pdf) {
  const { chromium } = await import('@playwright/test')
  const browser = await chromium.launch()
  try {
    const page = await browser.newPage()
    for (const file of written) {
      await page.goto(new URL(`file:///${file.replace(/\\/g, '/').replace(/^\/+/, '')}`).href, { waitUntil: 'load' })
      await page.emulateMedia({ media: 'print' })
      const target = file.replace(/\.html$/, '.pdf')
      await page.pdf({ path: target, format: 'A4', printBackground: true, outline: true, tagged: true, displayHeaderFooter: true,
        headerTemplate: '<span></span>',
        footerTemplate: '<div style="width:100%;font-size:8px;color:#6b7280;text-align:center"><span class="pageNumber"></span> / <span class="totalPages"></span></div>',
        margin: { top: '16mm', bottom: '18mm', left: '14mm', right: '14mm' } })
      console.log(target)
    }
  } finally { await browser.close() }
}
