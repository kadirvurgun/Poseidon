import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { accentColors, baseColors, builtInPalettes, contrastRatio, presetB3Zheoix4U, radiusOptions, selectionTokens } from './theme-tokens'
import type { ThemeSelection, TokenMode } from './theme-tokens'

// DEV025-AC-10 / 65.03 paragraph 257-262, 572 (UX-014): shell elements meet text 4.5:1 and
// meaningful component (focus ring, icon) 3:1 in every built-in palette and every builder
// combination, in light and dark. Ratios come from theme-tokens contrastRatio (oklch -> sRGB -> WCAG).
// Protected semantic tokens are fixed in design-system.css and never produced by the builder.
const fixed: Record<TokenMode, Record<string, string>> = {
  light: { 'focus-ring': '#245fba', 'danger-text': '#b54554', 'danger-surface': '#fff0f1', 'info-text': '#245caa', 'info-surface': '#edf4ff', warning: '#835d27', 'warning-surface': '#fffbf4' },
  dark: { 'focus-ring': '#a5d8ff', 'danger-text': '#fda4af', 'danger-surface': '#3f1822', 'info-text': '#93c5fd', 'info-surface': '#162a46', warning: '#fde68a', 'warning-surface': '#352a18' },
}
// [foreground, background, floor, where it is used in the shell]
const shellPairs = [
  ['foreground', 'card', 4.5, 'top bar title / breadcrumb'],
  ['muted-foreground', 'card', 4.5, 'top bar context bar text and connection'],
  ['muted-foreground', 'muted', 4.5, 'search trigger label'],
  ['card-foreground', 'card', 4.5, 'context summary button, appearance summary'],
  ['primary', 'card', 4.5, 'links on card surfaces'],
  ['sidebar-foreground', 'sidebar', 4.5, 'menu items'],
  ['muted-foreground', 'sidebar', 4.5, 'menu filter hint, footer'],
  ['sidebar-foreground', 'sidebar-accent', 4.5, 'hovered / current menu item'],
  ['sidebar-primary', 'sidebar-accent', 3, 'brand mark icon'],
  ['sidebar-primary', 'sidebar', 3, 'current-item indicator'],
  ['focus-ring', 'background', 3, 'focus ring on page'],
  ['focus-ring', 'card', 3, 'focus ring in top bar / dialogs'],
  ['focus-ring', 'sidebar', 3, 'focus ring in menu'],
  ['focus-ring', 'muted', 3, 'focus ring on muted controls'],
  ['danger-text', 'danger-surface', 4.5, 'form error summary'],
  ['info-text', 'info-surface', 4.5, 'workspace context notice'],
  ['warning', 'warning-surface', 4.5, 'warning notice'],
] as const

function failures(selection: ThemeSelection) {
  return (['light', 'dark'] as const).flatMap(mode => {
    const tokens = { ...selectionTokens(selection, mode), ...fixed[mode] }
    return shellPairs.flatMap(([fg, bg, floor, where]) => {
      const ratio = contrastRatio(tokens[fg] ?? '', tokens[bg] ?? '')
      return ratio !== null && ratio >= floor ? [] : [`${mode} ${fg}/${bg} ${ratio?.toFixed(2) ?? '?'} < ${floor} (${where})`]
    })
  })
}

describe('DEV-025 shell contrast in every palette', () => {
  it.each(builtInPalettes)('built-in palette %s meets text 4.5:1 and component 3:1 for shell elements', palette => {
    expect(failures({ palette, custom: presetB3Zheoix4U })).toEqual([])
  })
  it('every theme builder base x accent combination meets the shell floors', () => {
    const all = baseColors.flatMap(base => accentColors.flatMap(accent =>
      failures({ palette: 'custom', custom: { base, accent, chart: 'accent', radius: radiusOptions[3] } }).map(f => `${base}/${accent}: ${f}`)))
    expect(all).toEqual([])
  })
})

describe('DEV-025 shell colours come from theme tokens', () => {
  const read = (file: string) => readFileSync(join(dirname(fileURLToPath(import.meta.url)), file), 'utf8')
  it('the context bar uses no fixed text/background colours that ignore dark mode', () => {
    // The removed connection colour failed in dark mode: #3f717c on the dark card is below 4.5:1.
    expect(contrastRatio('#3f717c', selectionTokens({ palette: 'emerald', custom: presetB3Zheoix4U }, 'dark').card)).toBeLessThan(4.5)
    expect(read('../product/workspace-context-bar.css')).not.toMatch(/#[0-9a-f]{3,8}\b/i)
    expect(read('../product/enterprise.css')).not.toContain('#3f717c')
  })
})
