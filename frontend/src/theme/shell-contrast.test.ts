import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { accentColors, baseColors, builtInPalettes, contrastRatio, presetB3Zheoix4U, protectedSemanticTokens, radiusOptions, selectionTokens } from './theme-tokens'
import type { ThemeSelection, TokenMode } from './theme-tokens'

// DEV025-AC-10 / 65.03 paragraph 257-262, 572 (UX-014): shell elements meet text 4.5:1 and
// meaningful component (focus ring, icon) 3:1 in every built-in palette and every builder
// combination, in light and dark. Ratios come from theme-tokens contrastRatio (oklch -> sRGB -> WCAG).
// Nothing here is hand-copied: protected semantic token values are parsed from design-system.css and
// every shell focus indicator is parsed from the shell stylesheets and resolved to the token it consumes.
const here = dirname(fileURLToPath(import.meta.url))
const read = (file: string) => readFileSync(join(here, file), 'utf8')
const stripComments = (css: string) => css.replace(/\/\*[\s\S]*?\*\//g, '')

type Rule = Readonly<{ file: string; selector: string; declarations: Record<string, string> }>
/** Innermost `selector { declarations }` blocks; @media wrappers are transparent. */
function rules(file: string, css = read(file)): Rule[] {
  return Array.from(stripComments(css).matchAll(/([^{}]+)\{([^{}]*)\}/g)).map(([, selector, body]) => ({
    file,
    // Statements before a rule (@import, @custom-variant ...;) are not part of its selector.
    selector: selector.slice(selector.lastIndexOf(';') + 1).trim().replace(/\s+/g, ' '),
    declarations: Object.fromEntries(body.split(';').map(part => part.split(/:(.*)/s)).filter(pair => pair.length >= 2 && pair[0].trim())
      .map(([name, value]) => [name.trim(), value.trim()])),
  }))
}

const designSystem = rules('../design-system.css')
const rootBlock = (selector: string) => designSystem.find(rule => rule.selector === selector)?.declarations ?? {}
/** Protected semantic tokens (never produced by the builder) as declared in design-system.css. */
function protectedTokens(mode: TokenMode): Record<string, string> {
  const declared = { ...rootBlock(':root, :root.light'), ...(mode === 'dark' ? rootBlock(':root.dark') : {}) }
  const names = [...protectedSemanticTokens, 'focus-ring-on-dark']
  const resolve = (value: string, depth = 0): string => {
    const reference = /^var\(--([\w-]+)\)$/.exec(value)
    return reference && depth < 5 ? resolve(declared[`--${reference[1]}`] ?? '', depth + 1) : value
  }
  return Object.fromEntries(names.map(name => [name, resolve(declared[`--${name}`] ?? '')]))
}
/** Classes whose element (and subtree) re-point --ring at the protected --focus-ring. */
const protectedScopes = designSystem.filter(rule => rule.declarations['--ring'] === 'var(--focus-ring)')
  .flatMap(rule => rule.selector.split(',').map(part => part.trim()))

// Stylesheets of the product shell chrome (top bar, sidebar, context bar, role navigation, search and
// vessel dialogs, appearance popover, theme builder portal, language selector, error summary).
// product.css also holds workspace components (control evidence, regulatory qualification) with their own
// focus styling; only its shell-wide rules are in scope here.
const shellStylesheets = ['../product/enterprise.css', '../product/workspace-context-bar.css',
  '../product/workspace-role-navigation.css', '../product/workspace-vessel-switcher.css', './appearance-controls.css',
  '../components/LanguageSelector.css', '../components/a11y/form-error-summary.css']
const focusProperties = ['outline', 'outline-color', 'box-shadow', 'border-color']
type Consumer = Readonly<{ where: string; token: string; protectedScope: boolean; literalSurface?: string }>

/** Every focus indicator rule in the given rules, with the colour it actually consumes. */
function focusConsumers(all: Rule[]): Consumer[] {
  return all.filter(rule => /:focus/.test(rule.selector)).flatMap(rule => focusProperties.filter(name => rule.declarations[name])
    .map(name => {
      const value = rule.declarations[name]
      const where = `${rule.file} ${rule.selector} { ${name} }`
      const variable = /var\(--([\w-]+)/.exec(value)
      const literal = /#[0-9a-f]{3,8}\b/i.exec(value)
      // Whole class match: .theme-builder-option is not the .theme-builder scope.
      const protectedScope = rule.selector.split(',').every(part => protectedScopes.some(scope => new RegExp(`${scope.replace(/[.]/g, '\\.')}(?![\\w-])`).test(part)))
      if (variable) return { where, token: variable[1], protectedScope }
      // A literal colour is only acceptable on a component that paints its own fixed surface.
      const root = /^\.[\w-]+/.exec(rule.selector)?.[0]
      const surface = all.find(candidate => candidate.selector === root && candidate.file === rule.file)?.declarations.background
        ?.replace(/^#([0-9a-f])([0-9a-f])([0-9a-f])$/i, '#$1$1$2$2$3$3')
      return { where, token: literal ? literal[0] : value, protectedScope: false, literalSurface: surface }
    }))
}

const surfaces = ['background', 'card', 'sidebar', 'muted', 'popover'] as const
function consumerFailures(consumers: Consumer[], selection: ThemeSelection): string[] {
  return (['light', 'dark'] as const).flatMap(mode => {
    const palette = selectionTokens(selection, mode), fixed = protectedTokens(mode)
    return consumers.flatMap(consumer => {
      if (consumer.token.startsWith('#')) {
        const ratio = contrastRatio(consumer.token, consumer.literalSurface ?? '')
        return ratio !== null && ratio >= 3 ? [] : [`${mode} ${consumer.where}: literal ${consumer.token} on ${consumer.literalSurface ?? 'an unfixed surface'} ${ratio?.toFixed(2) ?? '?'} < 3`]
      }
      const token = consumer.token === 'ring' && consumer.protectedScope ? 'focus-ring' : consumer.token
      const colour = fixed[token] || palette[token]
      if (!colour) return [`${mode} ${consumer.where}: --${consumer.token} is not a known colour token`]
      return surfaces.flatMap(surface => {
        const ratio = contrastRatio(colour, palette[surface] ?? '')
        return ratio !== null && ratio >= 3 ? [] : [`${mode} ${consumer.where}: --${token} on --${surface} ${ratio?.toFixed(2) ?? '?'} < 3`]
      })
    })
  })
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
  ['danger-text', 'danger-surface', 4.5, 'form error summary'],
  ['info-text', 'info-surface', 4.5, 'workspace context notice'],
  ['warning', 'warning-surface', 4.5, 'warning notice'],
] as const

const shellRules = [...rules('../product/product.css').filter(rule => rule.selector.startsWith('.product-shell')), ...shellStylesheets.flatMap(file => rules(file))]
const consumers = focusConsumers(shellRules)
function failures(selection: ThemeSelection) {
  const pairs = (['light', 'dark'] as const).flatMap(mode => {
    const tokens = { ...selectionTokens(selection, mode), ...protectedTokens(mode) }
    return shellPairs.flatMap(([fg, bg, floor, where]) => {
      const ratio = contrastRatio(tokens[fg] ?? '', tokens[bg] ?? '')
      return ratio !== null && ratio >= floor ? [] : [`${mode} ${fg}/${bg} ${ratio?.toFixed(2) ?? '?'} < ${floor} (${where})`]
    })
  })
  return [...pairs, ...consumerFailures(consumers, selection)]
}
const builderCombos = baseColors.flatMap(base => accentColors.map(accent => ({ base, accent, chart: 'accent' as const, radius: radiusOptions[3] })))

describe('DEV-025 protected tokens and focus consumers are read from the stylesheets', () => {
  it('parses the protected focus/status tokens for both modes from design-system.css', () => {
    for (const mode of ['light', 'dark'] as const) {
      const tokens = protectedTokens(mode)
      for (const name of ['focus-ring', 'focus-ring-on-dark', 'danger-text', 'danger-surface', 'info-text', 'info-surface', 'warning', 'warning-surface'])
        expect(contrastRatio(tokens[name], '#ffffff'), `${mode} --${name} = ${tokens[name]}`).not.toBeNull()
    }
    expect(protectedTokens('light')['focus-ring']).not.toBe(protectedTokens('dark')['focus-ring'])
  })
  it('finds the shell focus indicators, including the portal-rendered theme builder', () => {
    const where = consumers.map(consumer => consumer.where).join('\n')
    expect(consumers.length).toBeGreaterThanOrEqual(10)
    expect(where).toContain('.theme-builder .theme-builder-option:has(input:focus-visible)')
    expect(where).toContain('.theme-builder h2:focus-visible')
    expect(where).toContain('.product-shell :focus-visible')
  })
  it('the theme builder portal root (rendered on <body>, outside .product-shell) is a protected --ring scope', () => {
    const portalRoot = /createPortal\(<div[^>]*className="([\w-]+)"/.exec(read('./ThemeBuilder.tsx'))?.[1]
    expect(portalRoot).toBe('theme-builder-backdrop')
    expect(protectedScopes).toEqual(expect.arrayContaining(['.product-shell', `.${portalRoot}`, '.theme-builder', '.appearance-controls']))
  })
  it('detects the original defect: an unscoped --ring outline fails in builder combinations (emerald light 2.59:1)', () => {
    const regression = focusConsumers(rules('regression.css', '.theme-builder-option:has(input:focus-visible) { outline: 2px solid var(--ring); }'))
    expect(regression).toEqual([expect.objectContaining({ token: 'ring', protectedScope: false })])
    const emerald = consumerFailures(regression, { palette: 'emerald', custom: presetB3Zheoix4U })
    expect(emerald.some(failure => failure.startsWith('light '))).toBe(true)
    const all = builderCombos.flatMap(custom => consumerFailures(regression, { palette: 'custom', custom }))
    expect(all.length).toBeGreaterThan(0)
  })
})

describe('DEV-025 shell contrast in every palette', () => {
  it.each(builtInPalettes)('built-in palette %s meets text 4.5:1 and every shell focus indicator 3:1', palette => {
    expect(failures({ palette, custom: presetB3Zheoix4U })).toEqual([])
  })
  it('every theme builder base x accent combination meets the shell floors', () => {
    const all = builderCombos.flatMap(custom => failures({ palette: 'custom', custom }).map(f => `${custom.base}/${custom.accent}: ${f}`))
    expect(all).toEqual([])
  })
})

describe('DEV-025 shell colours come from theme tokens', () => {
  it('the context bar uses no fixed text/background colours that ignore dark mode', () => {
    // The removed connection colour failed in dark mode: #3f717c on the dark card is below 4.5:1.
    expect(contrastRatio('#3f717c', selectionTokens({ palette: 'emerald', custom: presetB3Zheoix4U }, 'dark').card)).toBeLessThan(4.5)
    expect(read('../product/workspace-context-bar.css')).not.toMatch(/#[0-9a-f]{3,8}\b/i)
    expect(read('../product/enterprise.css')).not.toContain('#3f717c')
  })
})
