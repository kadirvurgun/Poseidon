import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { chartColor, chartSwatchVar, readChartTheme, subscribeChartTheme, toChartColor } from './chart-theme'
import { toHex } from './theme-tokens'
import { scopeChartOption } from '../product/scope-chart-renderer'

// THEME-CHART-01: chart colours are read from the real token stylesheets (design-system.css and the
// generated theme-builder.css), so a mode or palette switch changes series colours while protected
// semantic status colours stay fixed and UNKNOWN never takes a categorical/primary (possibly green) tone.
vi.mock('echarts/core', () => ({ init: vi.fn(), use: vi.fn() }))
vi.mock('echarts/charts', () => ({ PieChart: {} }))
vi.mock('echarts/components', () => ({ AriaComponent: {}, TooltipComponent: {} }))
vi.mock('echarts/renderers', () => ({ SVGRenderer: {} }))

const here = dirname(fileURLToPath(import.meta.url))
// Tailwind-only at-rules are not tokens; strip them so jsdom parses just the custom properties.
const tokenCss = (file: string) => readFileSync(join(here, file), 'utf8')
  .replace(/@import[^;]+;/g, '').replace(/@custom-variant[^;]+;/g, '').replace(/@theme inline \{[^}]*\}/g, '')
const root = document.documentElement
let style: HTMLStyleElement

beforeEach(() => {
  style = document.createElement('style')
  style.textContent = `${tokenCss('../design-system.css')}\n${tokenCss('./theme-builder.css')}`
  document.head.append(style)
  root.className = 'light'
})
afterEach(() => {
  style.remove(); root.className = ''
  for (const key of ['palette', 'base', 'accent', 'chart', 'radius']) delete root.dataset[key]
})

const declared = (selector: string, token: string) => {
  const block = tokenCss('../design-system.css').split('}').find(part => part.trimStart().startsWith(selector + ' {') || part.includes(`${selector} {`))
  return new RegExp(`--${token}:\\s*([^;]+);`).exec(block ?? '')?.[1].trim() ?? ''
}

describe('THEME-CHART-01 chart theme helper', () => {
  it('reads series and surfaces from the active light tokens', () => {
    const theme = readChartTheme()
    expect(theme).not.toBeNull()
    expect(theme!.series[0]).toBe(toHex(declared(':root, :root.light', 'chart-2')))
    expect(theme!.surface).toBe(toHex(declared(':root, :root.light', 'card')))
    expect(theme!.series).toHaveLength(5)
    expect(new Set(theme!.series).size).toBe(5)
  })

  it('changes surface/border colours on a dark mode switch', () => {
    const light = readChartTheme()!
    root.className = 'dark'
    const dark = readChartTheme()!
    expect(dark.surface).not.toBe(light.surface)
    expect(dark.surface).toBe(toHex(declared(':root.dark', 'card')))
    expect(dark.border).toBe('rgba(255, 255, 255, 0.1)')
    expect(dark.status.danger).toBe(toHex(declared(':root.dark', 'danger-text')))
    expect(dark.status.danger).not.toBe(light.status.danger)
  })

  it('changes series colours with the builder chart palette and keeps semantic status colours fixed', () => {
    const preset = readChartTheme()!
    root.dataset.palette = 'custom'; root.dataset.base = 'stone'; root.dataset.accent = 'emerald'; root.dataset.chart = 'rose'; root.dataset.radius = '0.625'
    const rose = readChartTheme()!
    expect(rose.series).not.toEqual(preset.series)
    expect(rose.status).toEqual(preset.status)
    root.dataset.palette = 'ocean'
    for (const key of ['base', 'accent', 'chart', 'radius']) delete root.dataset[key]
    expect(readChartTheme()!.status).toEqual(preset.status)
  })

  it('keeps danger, warning, info and unknown distinct; UNKNOWN is never a series or primary tone', () => {
    for (const mode of ['light', 'dark']) {
      root.className = mode
      const theme = readChartTheme()!
      const { danger, warning, info, unknown, reviewRequired } = theme.status
      expect(new Set([danger, warning, info, unknown]).size).toBe(4)
      expect(reviewRequired).toBe(warning)
      expect(theme.series).not.toContain(unknown)
      expect(unknown).toBe(theme.mutedText)
      expect(Object.keys(theme.status)).not.toContain('success')
      expect(chartColor(theme, 0, 'unknown')).toBe(unknown)
      expect(chartColor(theme, 7)).toBe(theme.series[2])
    }
  })

  it('builds ECharts options with no literal colours and status before series slot', () => {
    const theme = readChartTheme()!
    const option = scopeChartOption([{ name: 'Ships', value: 2 }, { name: 'Empty', value: 0 }, { name: 'Unknown', value: 1, status: 'unknown' }], theme)
    const series = option.series[0]
    expect(series.itemStyle.borderColor).toBe(theme.surface)
    expect(series.data.map(row => row.itemStyle.color)).toEqual([theme.series[0], theme.status.unknown])
    expect(option.tooltip.backgroundColor).toBe(theme.tooltip)
    expect(chartSwatchVar(0)).toBe('var(--chart-2)')
    expect(chartSwatchVar(2, 'unknown')).toBe('var(--muted-foreground)')
  })

  it('fails closed when a token is missing or not a colour', () => {
    style.remove()
    expect(readChartTheme()).toBeNull()
    expect(toChartColor('')).toBeNull()
    expect(toChartColor('var(--x)')).toBeNull()
    expect(toChartColor('#245caa')).toBe('#245caa')
    expect(toChartColor('oklch(1 0 0 / 15%)')).toBe('rgba(255, 255, 255, 0.15)')
  })

  it('notifies subscribers on mode/palette attribute changes and stops after unsubscribe', async () => {
    const listener = vi.fn()
    const unsubscribe = subscribeChartTheme(listener)
    root.className = 'dark'
    await Promise.resolve()
    expect(listener).toHaveBeenCalledTimes(1)
    root.dataset.palette = 'graphite'
    await Promise.resolve()
    expect(listener).toHaveBeenCalledTimes(2)
    unsubscribe()
    root.dataset.palette = 'ocean'
    await Promise.resolve()
    expect(listener).toHaveBeenCalledTimes(2)
  })
})
