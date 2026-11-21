// THEME-CHART-01: the single source of ECharts colours. Every chart option reads the active
// mode/palette tokens (CSS custom properties) at render time and re-renders when the root theme
// attributes change. No series colour is hard-coded; an unresolvable token fails closed (the
// caller keeps its accessible text legend instead of drawing with ECharts' default palette).
import { toHex } from './theme-tokens'

/** Series order: adjacent slices alternate light/dark chart tones so neighbours stay distinguishable. */
export const chartSeriesTokens = ['chart-2', 'chart-4', 'chart-3', 'chart-5', 'chart-1'] as const

/**
 * Semantic status colours come from the protected (non-builder) semantic tokens. There is deliberately
 * no success/green entry: UNKNOWN and REVIEW_REQUIRED never render as an approved/ready tone.
 */
export const chartStatusTokens = {
  danger: 'danger-text',
  warning: 'warning',
  reviewRequired: 'warning',
  info: 'info-text',
  unknown: 'muted-foreground',
} as const
export type ChartStatus = keyof typeof chartStatusTokens

const surfaceTokens = { surface: 'card', text: 'foreground', mutedText: 'muted-foreground', border: 'border', tooltip: 'popover', tooltipText: 'popover-foreground' } as const

export type ChartTheme = Readonly<{
  series: readonly string[]
  status: Readonly<Record<ChartStatus, string>>
} & Record<keyof typeof surfaceTokens, string>>

/** Root attributes written by the appearance store / theme builder (mode class + palette data-*). */
export const chartThemeAttributes = ['class', 'style', 'data-palette', 'data-base', 'data-accent', 'data-chart', 'data-radius', 'data-appearance-mode'] as const

/** CSS colour token -> a value ECharts/zrender can parse (hex, or rgba when the token has alpha). */
export function toChartColor(raw: string): string | null {
  const value = raw.trim()
  if (!value) return null
  const alpha = /\/\s*([\d.]+)(%?)\s*\)$/.exec(value)
  const opaque = alpha ? value.replace(/\s*\/\s*[\d.]+%?\s*\)$/, ')') : value
  const hex = toHex(opaque)
  if (!hex) return null
  if (!alpha) return hex
  const a = Math.min(1, Math.max(0, Number(alpha[1]) / (alpha[2] ? 100 : 1)))
  const [r, g, b] = [1, 3, 5].map(i => Number.parseInt(hex.slice(i, i + 2), 16))
  return `rgba(${[r, g, b].join(', ')}, ${a})`
}

/** Reads the active tokens; null when any required token is missing or not a colour (fail closed). */
export function readChartTheme(root: HTMLElement = document.documentElement): ChartTheme | null {
  const style = getComputedStyle(root)
  const read = (token: string) => toChartColor(style.getPropertyValue(`--${token}`))
  const series = chartSeriesTokens.map(read)
  const status = Object.fromEntries(Object.entries(chartStatusTokens).map(([key, token]) => [key, read(token)]))
  const surfaces = Object.fromEntries(Object.entries(surfaceTokens).map(([key, token]) => [key, read(token)]))
  if (series.some(v => v === null) || Object.values(status).some(v => v === null) || Object.values(surfaces).some(v => v === null)) return null
  return { series: series as string[], status: status as Record<ChartStatus, string>, ...(surfaces as Record<keyof typeof surfaceTokens, string>) }
}

/** CSS reference for HTML legend swatches (follows theme changes without re-rendering). */
export function chartSwatchVar(index: number, status?: ChartStatus): string {
  return `var(--${status ? chartStatusTokens[status] : chartSeriesTokens[index % chartSeriesTokens.length]})`
}

/** Colour for one datum: semantic status wins over the categorical series slot. */
export function chartColor(theme: ChartTheme, index: number, status?: ChartStatus): string {
  return status ? theme.status[status] : theme.series[index % theme.series.length]
}

/** Calls listener whenever the root theme (mode class, palette, builder data-*) or the OS scheme changes. */
export function subscribeChartTheme(listener: () => void, root: HTMLElement = document.documentElement): () => void {
  const observer = typeof MutationObserver === 'undefined' ? null : new MutationObserver(listener)
  observer?.observe(root, { attributes: true, attributeFilter: [...chartThemeAttributes] })
  const media = typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null
  media?.addEventListener?.('change', listener)
  return () => { observer?.disconnect(); media?.removeEventListener?.('change', listener) }
}
