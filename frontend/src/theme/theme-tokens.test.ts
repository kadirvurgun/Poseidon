import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  accentColors, autoAdjustedForegrounds, baseColors, buildThemeStylesheet, builtInPalettes, contrastRatio, contrastReport,
  customTokens, defaultCustomTheme, exportCss, exportJson, parseThemeFile, protectedSemanticTokens, radiusOptions, toHex,
} from './theme-tokens'
import type { ThemeSelection } from './theme-tokens'

const custom = (patch: Partial<ThemeSelection['custom']> = {}): ThemeSelection => ({ palette: 'custom', custom: { ...defaultCustomTheme, ...patch } })

describe('THEME-BUILDER-01 token resolution and contrast guard', () => {
  it('THEME-BUILDER-CSS: theme-builder.css is exactly the generated stylesheet', () => {
    const file = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'theme-builder.css'), 'utf8').replace(/\r\n/g, '\n')
    expect(file).toBe(buildThemeStylesheet())
  })

  it('THEME-BUILDER-CONTRAST: converts oklch to sRGB and computes WCAG ratios', () => {
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 5)
    expect(contrastRatio('#ffffff', '#ffffff')).toBeCloseTo(1, 5)
    expect(toHex('oklch(0.508 0.118 165.612)')).toBe('#007a55') // Tailwind v4 emerald-700
    expect(toHex('oklch(1 0 0)')).toBe('#ffffff')
    expect(contrastRatio('not-a-color', '#ffffff')).toBeNull()
  })

  it('THEME-BUILDER-CONTRAST: every allowed and built-in combination meets 4.5:1 text in light and dark', () => {
    for (const base of baseColors) for (const accent of accentColors) {
      const report = contrastReport(custom({ base, accent }))
      expect(report.blocked, `${base}/${accent}`).toBe(false)
      expect(report.checks).toHaveLength(18)
    }
    for (const palette of builtInPalettes) expect(contrastReport({ palette, custom: defaultCustomTheme }).blocked, palette).toBe(false)
  })

  it('THEME-BUILDER-CONTRAST: auto-picks a compliant foreground and reports it', () => {
    const adjusted = autoAdjustedForegrounds('blue')
    expect(adjusted).toContainEqual({ mode: 'dark', token: 'sidebar-primary-foreground' })
    const dark = customTokens(custom({ accent: 'blue' }).custom, 'dark')
    expect(contrastRatio(dark['sidebar-primary-foreground'], dark['sidebar-primary'])!).toBeGreaterThanOrEqual(4.5)
    expect(autoAdjustedForegrounds('emerald')).toEqual([])
  })

  it('THEME-BUILDER-CONTRAST: blocks a failing or unevaluable pair (fails closed)', () => {
    const failing = contrastReport(custom(), (selection, mode) => ({ ...customTokens(selection.custom, mode), 'primary-foreground': 'oklch(0.55 0.1 160)' }))
    expect(failing.blocked).toBe(true)
    expect(failing.checks.find(check => !check.ok)?.foreground).toBe('primary-foreground')
    const unknown = contrastReport(custom(), (selection, mode) => ({ ...customTokens(selection.custom, mode), foreground: 'var(--evil)' }))
    expect(unknown.blocked).toBe(true)
  })

  it('THEME-BUILDER-SAFETY: never emits warning/danger/info/destructive/focus tokens', () => {
    const generated = buildThemeStylesheet()
    for (const token of protectedSemanticTokens) {
      expect(generated).not.toMatch(new RegExp(`--${token}:`))
      expect(exportCss(custom({ accent: 'rose' }))).not.toMatch(new RegExp(`--${token}:`))
    }
    expect(generated.split('\n').filter(line => line.startsWith(':root')).every(line => line.includes('[data-palette="custom"]'))).toBe(true)
  })

  it('THEME-BUILDER-EXPORT: exports CSS variables and a round-trippable JSON', () => {
    const selection = custom({ base: 'zinc', accent: 'violet', chart: 'accent', radius: '0.3' })
    const css = exportCss(selection)
    expect(css).toMatch(/^:root \{\n {2}--background: oklch\(1 0 0\);/)
    expect(css).toContain('--radius: 0.3rem;')
    expect(css).toContain('.dark {')
    expect(parseThemeFile(exportJson(selection))).toEqual({ selection })
    expect(parseThemeFile(exportJson({ palette: 'ocean', custom: defaultCustomTheme }))?.selection.palette).toBe('ocean')
  })

  it.each([
    ['not json', '{bad'],
    ['array', '[]'],
    ['wrong version', '{"version":2,"palette":"custom","base":"zinc","accent":"blue","chart":"accent","radius":"0.5"}'],
    ['unknown key', '{"version":1,"palette":"custom","base":"zinc","accent":"blue","chart":"accent","radius":"0.5","css":"x"}'],
    ['arbitrary color', '{"version":1,"palette":"custom","base":"zinc","accent":"url(https://evil.test)","chart":"accent","radius":"0.5"}'],
    ['unlisted radius', '{"version":1,"palette":"custom","base":"zinc","accent":"blue","chart":"accent","radius":"0.4"}'],
    ['missing field', '{"version":1,"palette":"custom","base":"zinc","accent":"blue","radius":"0.5"}'],
    ['bad mode', '{"version":1,"palette":"ocean","mode":"neon"}'],
    ['extra key on preset', '{"version":1,"palette":"ocean","base":"zinc"}'],
    ['prototype key', '{"version":1,"palette":"ocean","__proto__":{"x":1}}'],
    ['oversized', `{"version":1,"palette":"ocean","mode":"dark"${' '.repeat(1100)}}`],
  ])('THEME-BUILDER-IMPORT: rejects %s', (_, text) => {
    expect(parseThemeFile(text)).toBeNull()
  })

  it('THEME-BUILDER-IMPORT: accepts every allowed radius and an optional mode', () => {
    for (const radius of radiusOptions) {
      expect(parseThemeFile(JSON.stringify({ version: 1, palette: 'custom', base: 'gray', accent: 'amber', chart: 'sky', radius, mode: 'dark' })))
        .toEqual({ selection: { palette: 'custom', custom: { base: 'gray', accent: 'amber', chart: 'sky', radius } }, mode: 'dark' })
    }
  })
})
