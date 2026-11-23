import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, it } from 'vitest'

// N6 (HELP-MANUAL-DOWNLOAD-01 follow-up): jsdom applies no cascade, so these guard the stylesheet rules that fix the
// defects seen in the UG-2 help screenshots. Browser rendering is re-checked when the screenshots are regenerated.
const here = dirname(fileURLToPath(import.meta.url))
const read = (path: string) => readFileSync(join(here, path), 'utf8')
const rule = (css: string, selector: string) => {
  const start = css.indexOf(`${selector} {`)
  return start < 0 ? '' : css.slice(start + selector.length + 2, css.indexOf('}', start))
}

it('keeps room for the globe icon with a selector that outranks the shell select rules', () => {
  const css = read('../components/LanguageSelector.css')
  const body = rule(css, '.language-selector > .language-selector-control > select')
  expect(body).toMatch(/padding-inline:\s*2rem\b/)
  // The 0,1,1 form lost to `.enterprise-shell select` / `.tug-workspace select` loaded later.
  expect(css).not.toMatch(/^\.language-selector-control select\b/m)
  expect(read('enterprise.css')).toMatch(/\.enterprise-shell select \{[^}]*padding:8px 12px/)
})

it('sizes workspace checkboxes and radios as compact controls beside their label on desktop and mobile', () => {
  const css = read('tug.css')
  expect(css).toContain('.tug-workspace input:where(:not([type="checkbox"], [type="radio"])), .tug-workspace select { width: 100%;')
  expect(css).not.toMatch(/^\.tug-workspace input, \.tug-workspace select \{/m)
  const box = rule(css, '.tug-workspace input:is([type="checkbox"], [type="radio"])')
  expect(box).toMatch(/inline-size: 18px/)
  expect(box).toMatch(/padding: 0/)
  const label = rule(css, '.tug-workspace label:has(> input:is([type="checkbox"], [type="radio"]))')
  expect(label).toMatch(/flex-direction: row/)
  expect(css).toMatch(/@media \(pointer: coarse\), \(max-width: 900px\) \{\s*\.tug-workspace input:is\(\[type="checkbox"\], \[type="radio"\]\) \{ inline-size: 24px/)
  expect(css).toMatch(/label:has\(> input:is\(\[type="checkbox"\], \[type="radio"\]\)\) \{ min-block-size: 44px; \}/)
})

it('wraps machine codes and long safety badges instead of overflowing narrow viewports', () => {
  expect(rule(read('enterprise.css'), '.enterprise-shell main :is(code, kbd, samp)')).toMatch(/overflow-wrap: anywhere/)
  expect(rule(read('tug.css'), '.enterprise-shell .safety-blockers [data-slot="badge"]')).toMatch(/white-space: normal; overflow-wrap: anywhere/)
})

it('lets long tug job numbers wrap inside the mobile job card instead of being clipped', () => {
  const css = read('tug-board.css')
  expect(rule(css, '.tug-board-identity')).toMatch(/min-inline-size: 0/)
  expect(rule(css, '.enterprise-shell .tug-board-identity h3')).toMatch(/overflow-wrap: anywhere/)
})
