// THEME-BUILDER-01: closed theme-builder vocabulary, token resolution and WCAG contrast guard.
// Self-contained (no imports) so the generated stylesheet can be rebuilt with plain Node type stripping.
// Colour values are Tailwind CSS v4 palette oklch values as used by shadcn/ui themes (MIT).

export const baseColors = ['stone', 'zinc', 'neutral', 'slate', 'gray'] as const
export const accentColors = ['emerald', 'teal', 'cyan', 'sky', 'blue', 'indigo', 'violet', 'rose', 'orange', 'amber'] as const
export const radiusOptions = ['0', '0.3', '0.5', '0.625', '0.75', '1'] as const
export const builtInPalettes = ['emerald', 'ocean', 'graphite'] as const
export type BaseColor = typeof baseColors[number]
export type AccentColor = typeof accentColors[number]
export type ChartColor = 'accent' | AccentColor
export type RadiusOption = typeof radiusOptions[number]
export type BuiltInPalette = typeof builtInPalettes[number]
export type CustomTheme = Readonly<{ base: BaseColor; accent: AccentColor; chart: ChartColor; radius: RadiusOption }>
export type ThemeSelection = Readonly<{ palette: BuiltInPalette | 'custom'; custom: CustomTheme }>
export type TokenMode = 'light' | 'dark'
export type TokenMap = Readonly<Record<string, string>>

/** User's shadcn preset b3Zheoix4U: Stone base, Emerald accent, Cyan chart, default radius. */
export const presetB3Zheoix4U: CustomTheme = { base: 'stone', accent: 'emerald', chart: 'cyan', radius: '0.625' }
export const defaultCustomTheme: CustomTheme = presetB3Zheoix4U

const oklch = (value: string) => `oklch(${value})`

// Neutral scales: 50, 100, 200, 400, 500, 800, 900, 950.
type NeutralScale = readonly [string, string, string, string, string, string, string, string]
const neutralScales: Record<BaseColor, NeutralScale> = {
  stone: ['0.985 0.001 106.423', '0.97 0.001 106.424', '0.923 0.003 48.717', '0.709 0.01 56.259', '0.553 0.013 58.071', '0.268 0.007 34.298', '0.216 0.006 56.043', '0.147 0.004 49.25'],
  zinc: ['0.985 0 0', '0.967 0.001 286.375', '0.92 0.004 286.32', '0.705 0.015 286.067', '0.552 0.016 285.938', '0.274 0.006 286.033', '0.21 0.006 285.885', '0.141 0.005 285.823'],
  neutral: ['0.985 0 0', '0.97 0 0', '0.922 0 0', '0.708 0 0', '0.556 0 0', '0.269 0 0', '0.205 0 0', '0.145 0 0'],
  slate: ['0.984 0.003 247.858', '0.968 0.007 247.896', '0.929 0.013 255.508', '0.704 0.04 256.788', '0.554 0.046 257.417', '0.279 0.041 260.031', '0.208 0.042 265.755', '0.129 0.042 264.695'],
  gray: ['0.985 0.002 247.839', '0.967 0.003 264.542', '0.928 0.006 264.531', '0.707 0.022 261.325', '0.551 0.027 264.364', '0.278 0.033 256.848', '0.21 0.034 264.665', '0.13 0.028 261.692'],
}

// Accent scales: 50, 300, 500, 600, 700, 800, 950.
type AccentScale = readonly [string, string, string, string, string, string, string]
const accentScales: Record<AccentColor, AccentScale> = {
  emerald: ['0.979 0.021 166.113', '0.845 0.143 164.978', '0.696 0.17 162.48', '0.596 0.145 163.225', '0.508 0.118 165.612', '0.432 0.095 166.913', '0.262 0.051 172.552'],
  teal: ['0.984 0.014 180.72', '0.855 0.138 181.071', '0.704 0.14 182.503', '0.6 0.118 184.704', '0.511 0.096 186.391', '0.437 0.078 188.216', '0.277 0.046 192.524'],
  cyan: ['0.984 0.019 200.873', '0.865 0.127 207.078', '0.715 0.143 215.221', '0.609 0.126 221.723', '0.52 0.105 223.128', '0.45 0.085 224.283', '0.302 0.056 229.695'],
  sky: ['0.977 0.013 236.62', '0.828 0.111 230.318', '0.685 0.169 237.323', '0.588 0.158 241.966', '0.5 0.134 242.749', '0.443 0.11 240.79', '0.293 0.066 243.157'],
  blue: ['0.97 0.014 254.604', '0.809 0.105 251.813', '0.623 0.214 259.815', '0.546 0.245 262.881', '0.488 0.243 264.376', '0.424 0.199 265.638', '0.282 0.091 267.935'],
  indigo: ['0.962 0.018 272.314', '0.785 0.115 274.713', '0.585 0.233 277.117', '0.511 0.262 276.966', '0.457 0.24 277.023', '0.398 0.195 277.366', '0.257 0.09 281.288'],
  violet: ['0.969 0.016 293.756', '0.811 0.111 293.571', '0.606 0.25 292.717', '0.541 0.281 293.009', '0.491 0.27 292.581', '0.432 0.232 292.759', '0.283 0.141 291.089'],
  rose: ['0.969 0.015 12.422', '0.81 0.117 11.638', '0.645 0.246 16.439', '0.586 0.253 17.585', '0.514 0.222 16.935', '0.455 0.188 13.697', '0.271 0.105 12.094'],
  orange: ['0.98 0.016 73.684', '0.837 0.128 66.29', '0.705 0.213 47.604', '0.646 0.222 41.116', '0.553 0.195 38.402', '0.47 0.157 37.304', '0.266 0.079 36.259'],
  amber: ['0.987 0.022 95.277', '0.879 0.169 91.605', '0.769 0.188 70.08', '0.666 0.179 58.318', '0.555 0.163 48.998', '0.473 0.137 46.201', '0.279 0.077 45.635'],
}

/** Tokens the builder may change. Semantic warning/danger/info/destructive/focus tokens are deliberately absent. */
export const builderTokenNames = [
  'background', 'foreground', 'card', 'card-foreground', 'popover', 'popover-foreground',
  'primary', 'primary-foreground', 'secondary', 'secondary-foreground', 'muted', 'muted-foreground',
  'accent', 'accent-foreground', 'border', 'input', 'ring', 'chart-1', 'chart-2', 'chart-3', 'chart-4', 'chart-5', 'radius',
  'sidebar', 'sidebar-foreground', 'sidebar-primary', 'sidebar-primary-foreground', 'sidebar-accent',
  'sidebar-accent-foreground', 'sidebar-border', 'sidebar-ring',
] as const
export const protectedSemanticTokens = ['destructive', 'warning', 'warning-surface', 'warning-border', 'danger-text', 'danger-surface', 'danger-border', 'info-text', 'info-surface', 'info-border', 'focus-ring'] as const

export function baseTokens(base: BaseColor, mode: TokenMode): Record<string, string> {
  const [s50, s100, s200, s400, s500, s800, s900, s950] = neutralScales[base].map(oklch)
  if (mode === 'light') return {
    background: oklch('1 0 0'), foreground: s950, card: oklch('1 0 0'), 'card-foreground': s950, popover: oklch('1 0 0'), 'popover-foreground': s950,
    secondary: s100, 'secondary-foreground': s900, muted: s100, 'muted-foreground': s500, accent: s100, 'accent-foreground': s900,
    border: s200, input: s200, ring: s400, sidebar: s50, 'sidebar-foreground': s950, 'sidebar-accent': s100,
    'sidebar-accent-foreground': s900, 'sidebar-border': s200, 'sidebar-ring': s400,
  }
  return {
    background: s950, foreground: s50, card: s900, 'card-foreground': s50, popover: s900, 'popover-foreground': s50,
    secondary: s800, 'secondary-foreground': s50, muted: s800, 'muted-foreground': s400, accent: s800, 'accent-foreground': s50,
    border: oklch('1 0 0 / 10%'), input: oklch('1 0 0 / 15%'), ring: s500, sidebar: s900, 'sidebar-foreground': s50, 'sidebar-accent': s800,
    'sidebar-accent-foreground': s50, 'sidebar-border': oklch('1 0 0 / 10%'), 'sidebar-ring': s500,
  }
}

export function accentTokens(accent: AccentColor, mode: TokenMode): Record<string, string> {
  const [a50, , a500, , a700, a800, a950] = accentScales[accent].map(oklch)
  const raw = mode === 'light'
    ? { primary: a700, 'primary-foreground': a50, 'sidebar-primary': a700, 'sidebar-primary-foreground': a50 }
    : { primary: a800, 'primary-foreground': a50, 'sidebar-primary': a500, 'sidebar-primary-foreground': a950 }
  // Foregrounds are auto-picked when the default fails 4.5:1 (THEME-BUILDER-CONTRAST).
  const candidates = [a50, a950, oklch('1 0 0'), oklch('0 0 0')]
  return {
    ...raw,
    'primary-foreground': pickForeground(raw.primary, raw['primary-foreground'], candidates),
    'sidebar-primary-foreground': pickForeground(raw['sidebar-primary'], raw['sidebar-primary-foreground'], candidates),
  }
}

/** Foreground tokens the contrast guard replaced for this accent (shown to the user, never silent). */
export function autoAdjustedForegrounds(accent: AccentColor): ReadonlyArray<Readonly<{ mode: TokenMode; token: string }>> {
  const [a50, , , , , , a950] = accentScales[accent].map(oklch)
  const defaultsByMode: Record<TokenMode, Record<string, string>> = {
    light: { 'primary-foreground': a50, 'sidebar-primary-foreground': a50 },
    dark: { 'primary-foreground': a50, 'sidebar-primary-foreground': a950 },
  }
  return (['light', 'dark'] as const).flatMap(mode => {
    const resolved = accentTokens(accent, mode)
    return Object.entries(defaultsByMode[mode]).filter(([token, value]) => resolved[token] !== value).map(([token]) => ({ mode, token }))
  })
}

export function chartTokens(color: AccentColor): Record<string, string> {
  const [, a300, a500, a600, a700, a800] = accentScales[color].map(oklch)
  return { 'chart-1': a300, 'chart-2': a500, 'chart-3': a600, 'chart-4': a700, 'chart-5': a800 }
}

/** Representative swatch for builder option chips (base 500, accent 600). */
export function swatchColor(name: BaseColor | AccentColor): string {
  return (baseColors as readonly string[]).includes(name) ? oklch(neutralScales[name as BaseColor][4]) : oklch(accentScales[name as AccentColor][3])
}

export function resolveChart(theme: CustomTheme): AccentColor { return theme.chart === 'accent' ? theme.accent : theme.chart }

export function customTokens(theme: CustomTheme, mode: TokenMode): Record<string, string> {
  return { ...baseTokens(theme.base, mode), ...accentTokens(theme.accent, mode), ...chartTokens(resolveChart(theme)), radius: `${theme.radius}rem` }
}

// Mirrors design-system.css root defaults (preset b3Zheoix4U) plus the fixed ocean/graphite overrides.
const builtInOverrides: Record<BuiltInPalette, Record<TokenMode, Record<string, string>>> = {
  emerald: { light: {}, dark: {} },
  ocean: {
    light: { primary: '#1d4ed8', 'primary-foreground': '#ffffff', ring: '#2563eb', 'sidebar-primary': '#1d4ed8', 'sidebar-primary-foreground': '#ffffff' },
    dark: { primary: '#93c5fd', 'primary-foreground': '#172554', ring: '#60a5fa', 'sidebar-primary': '#93c5fd', 'sidebar-primary-foreground': '#172554' },
  },
  graphite: {
    light: { primary: '#292524', 'primary-foreground': '#fafaf9', ring: '#78716c', 'sidebar-primary': '#292524', 'sidebar-primary-foreground': '#fafaf9' },
    dark: { primary: '#e7e5e4', 'primary-foreground': '#1c1917', ring: '#a8a29e', 'sidebar-primary': '#e7e5e4', 'sidebar-primary-foreground': '#1c1917' },
  },
}

export function selectionTokens(selection: ThemeSelection, mode: TokenMode): Record<string, string> {
  if (selection.palette === 'custom') return customTokens(selection.custom, mode)
  const root = mode === 'light'
    // Preset light sidebar-primary (emerald-600 on emerald-50, 3.48:1) was raised to emerald-700 by THEME-BUILDER-CONTRAST.
    ? { ...customTokens(presetB3Zheoix4U, 'light'), secondary: oklch('0.967 0.001 286.375'), 'secondary-foreground': oklch('0.21 0.006 285.885') }
    : { ...customTokens(presetB3Zheoix4U, 'dark'), secondary: oklch('0.274 0.006 286.033'), 'secondary-foreground': oklch('0.985 0 0'), 'sidebar-primary': oklch('0.696 0.17 162.48'), 'sidebar-primary-foreground': oklch('0.262 0.051 172.552') }
  return { ...root, ...builtInOverrides[selection.palette][mode] }
}

// ---- Colour math: OKLCH -> OKLab -> linear sRGB (gamut-clipped) -> WCAG relative luminance. ----
type Rgb = readonly [number, number, number]

export function parseColor(value: string): Rgb | null {
  const hex = /^#([0-9a-f]{6})$/i.exec(value.trim())
  if (hex) {
    const n = Number.parseInt(hex[1], 16)
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255].map(c => toLinear(c / 255)) as unknown as Rgb
  }
  const match = /^oklch\(\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*(?:\/\s*[\d.]+%?\s*)?\)$/i.exec(value.trim())
  if (!match) return null
  const [l, c, h] = [Number(match[1]), Number(match[2]), Number(match[3])]
  const a = c * Math.cos((h * Math.PI) / 180), b = c * Math.sin((h * Math.PI) / 180)
  const l_ = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3
  const m_ = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3
  const s_ = (l - 0.0894841775 * a - 1.291485548 * b) ** 3
  const clip = (x: number) => Math.min(1, Math.max(0, x))
  return [
    clip(4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_),
    clip(-1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_),
    clip(-0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_),
  ]
}

function toLinear(c: number) { return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4 }
function toGamma(c: number) { return c <= 0.0031308 ? 12.92 * c : 1.055 * c ** (1 / 2.4) - 0.055 }

export function luminance(value: string): number | null {
  const rgb = parseColor(value)
  return rgb ? 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2] : null
}

export function toHex(value: string): string | null {
  const rgb = parseColor(value)
  return rgb ? `#${rgb.map(c => Math.round(toGamma(c) * 255).toString(16).padStart(2, '0')).join('')}` : null
}

/** WCAG 2.x contrast ratio; null when a colour cannot be evaluated (fails closed in the guard). */
export function contrastRatio(foreground: string, background: string): number | null {
  const a = luminance(foreground), b = luminance(background)
  if (a === null || b === null) return null
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
}

export const textContrastFloor = 4.5

function pickForeground(background: string, preferred: string, candidates: readonly string[]): string {
  const ok = (fg: string) => (contrastRatio(fg, background) ?? 0) >= textContrastFloor
  if (ok(preferred)) return preferred
  return candidates.find(ok) ?? preferred
}

export const contrastPairs = [
  ['primary-foreground', 'primary'], ['foreground', 'background'], ['card-foreground', 'card'],
  ['popover-foreground', 'popover'], ['secondary-foreground', 'secondary'], ['accent-foreground', 'accent'],
  ['muted-foreground', 'background'], ['sidebar-foreground', 'sidebar'], ['sidebar-primary-foreground', 'sidebar-primary'],
] as const
export type ContrastCheck = Readonly<{ mode: TokenMode; foreground: string; background: string; ratio: number | null; ok: boolean }>

/** Evaluates every text pair in both modes; any failing or unevaluable pair blocks applying the theme. */
export function contrastReport(selection: ThemeSelection, tokens: (selection: ThemeSelection, mode: TokenMode) => TokenMap = selectionTokens) {
  const checks: ContrastCheck[] = (['light', 'dark'] as const).flatMap(mode => {
    const map = tokens(selection, mode)
    return contrastPairs.map(([foreground, background]) => {
      const ratio = contrastRatio(map[foreground] ?? '', map[background] ?? '')
      return { mode, foreground, background, ratio, ok: ratio !== null && ratio >= textContrastFloor }
    })
  })
  return { checks, blocked: checks.some(check => !check.ok) }
}

// ---- Export / import ----
export function exportCss(selection: ThemeSelection): string {
  const block = (selector: string, mode: TokenMode) => {
    const map = selectionTokens(selection, mode)
    const names = builderTokenNames.filter(name => map[name] !== undefined && !(mode === 'dark' && name === 'radius'))
    return `${selector} {\n${names.map(name => `  --${name}: ${map[name]};`).join('\n')}\n}`
  }
  return `${block(':root', 'light')}\n\n${block('.dark', 'dark')}\n`
}

export type ThemeFile = Readonly<{ selection: ThemeSelection; mode?: 'light' | 'dark' | 'system' }>
export const themeFileVersion = 1
const maxImportLength = 1024

export function exportJson(selection: ThemeSelection): string {
  const body = selection.palette === 'custom'
    ? { version: themeFileVersion, palette: 'custom', ...selection.custom }
    : { version: themeFileVersion, palette: selection.palette }
  return JSON.stringify(body, null, 2)
}

const isOneOf = <T extends string>(list: readonly T[], value: unknown): value is T => typeof value === 'string' && (list as readonly string[]).includes(value)

/** Strict import: exact key set, closed values, size limit. Anything else is rejected, never coerced. */
export function parseThemeFile(text: string, fallbackCustom: CustomTheme = defaultCustomTheme): ThemeFile | null {
  if (typeof text !== 'string' || text.length === 0 || text.length > maxImportLength) return null
  let parsed: unknown
  try { parsed = JSON.parse(text) } catch { return null }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed) || Object.getPrototypeOf(parsed) !== Object.prototype) return null
  const record = parsed as Record<string, unknown>
  const keys = Object.keys(record)
  if (record.version !== themeFileVersion) return null
  if (record.mode !== undefined && !isOneOf(['light', 'dark', 'system'] as const, record.mode)) return null
  const mode = record.mode as ThemeFile['mode']
  if (isOneOf(builtInPalettes, record.palette)) {
    if (keys.some(key => !['version', 'palette', 'mode'].includes(key))) return null
    return { selection: { palette: record.palette, custom: fallbackCustom }, ...(mode ? { mode } : {}) }
  }
  if (record.palette !== 'custom') return null
  if (keys.some(key => !['version', 'palette', 'mode', 'base', 'accent', 'chart', 'radius'].includes(key))) return null
  const { base, accent, chart, radius } = record
  if (!isOneOf(baseColors, base) || !isOneOf(accentColors, accent) || !(chart === 'accent' || isOneOf(accentColors, chart)) || !isOneOf(radiusOptions, radius)) return null
  return { selection: { palette: 'custom', custom: { base, accent, chart, radius } }, ...(mode ? { mode } : {}) }
}

export function parseCustomTheme(value: unknown): CustomTheme {
  const record = value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {}
  return {
    base: isOneOf(baseColors, record.base) ? record.base : defaultCustomTheme.base,
    accent: isOneOf(accentColors, record.accent) ? record.accent : defaultCustomTheme.accent,
    chart: record.chart === 'accent' || isOneOf(accentColors, record.chart) ? record.chart as ChartColor : defaultCustomTheme.chart,
    radius: isOneOf(radiusOptions, record.radius) ? record.radius : defaultCustomTheme.radius,
  }
}

// ---- Generated stylesheet (theme-builder.css) ----
/** Builds the attribute-scoped stylesheet; theme-builder.css must equal this output (enforced by test). */
export function buildThemeStylesheet(): string {
  const rule = (selector: string, map: Record<string, string>) => `${selector} { ${Object.entries(map).map(([k, v]) => `--${k}: ${v};`).join(' ')} }`
  const scope = ':root[data-palette="custom"]'
  const lines = [
    '/* GENERATED from src/theme/theme-tokens.ts buildThemeStylesheet(); do not edit by hand (THEME-BUILDER-01).',
    '   Only applies when data-palette="custom"; semantic warning/danger/info/destructive/focus tokens are never set here. */',
  ]
  for (const base of baseColors) {
    lines.push(rule(`${scope}[data-base="${base}"]`, baseTokens(base, 'light')))
    lines.push(rule(`:root.dark[data-palette="custom"][data-base="${base}"]`, baseTokens(base, 'dark')))
  }
  for (const accent of accentColors) {
    lines.push(rule(`${scope}[data-accent="${accent}"]`, accentTokens(accent, 'light')))
    lines.push(rule(`:root.dark[data-palette="custom"][data-accent="${accent}"]`, accentTokens(accent, 'dark')))
  }
  for (const chart of accentColors) lines.push(rule(`${scope}[data-chart="${chart}"]`, chartTokens(chart)))
  for (const radius of radiusOptions) lines.push(rule(`${scope}[data-radius="${radius}"]`, { radius: `${radius}rem` }))
  return `${lines.join('\n')}\n`
}

/** Browser theme-color for the address bar: base background of the active mode. */
export function themeColorHex(selection: ThemeSelection, dark: boolean): string {
  if (selection.palette !== 'custom') return dark ? '#1c1917' : '#ffffff'
  return dark ? toHex(baseTokens(selection.custom.base, 'dark').background) ?? '#1c1917' : '#ffffff'
}
