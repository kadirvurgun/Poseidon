import { useEffect, useRef } from 'react'
import { chartSwatchVar, readChartTheme, subscribeChartTheme, type ChartStatus } from '../theme/chart-theme'
import { loadScopeChartRenderer } from './scope-chart-loader'

/** Colour is never passed in: it is the theme chart slot for the row position, or a semantic status token. */
export type DistributionRow = { name: string; value: number; status?: ChartStatus }
type ScopeChart = { resize: () => void; update: (theme: NonNullable<ReturnType<typeof readChartTheme>>) => void; dispose: () => void }
// Only the parent-owned, already authorized page is summarized. No global totals,
// trends or readiness claims are inferred from incomplete/unavailable adapters.
export function ScopedDistribution({ data, label }: { data: DistributionRow[]; label: string }) {
  const node = useRef<HTMLDivElement>(null)
  useEffect(() => {
    let active = true; let chart: ScopeChart | null = null
    let renderer: typeof import('./scope-chart-renderer') | null = null
    let pending: ReturnType<typeof loadScopeChartRenderer> | null = null
    let failed = false
    const element = node.current
    if (!element || !data.some(row => row.value > 0) || data.some(row => !Number.isSafeInteger(row.value) || row.value < 0)) return
    const resize = () => {
      if (!active || !element.isConnected || element.getBoundingClientRect().width <= 0) return
      if (!renderer) {
        if (pending || failed) return
        pending = loadScopeChartRenderer()
        pending.then(module => {
          if (!active) return
          pending = null; renderer = module; resize()
        }).catch(() => {
          pending = null; failed = true
          // Keep the accessible source legend when the optional engine fails.
        })
        return
      }
      if (chart) { chart.resize(); return }
      // THEME-CHART-01: unresolvable theme tokens fail closed; the text legend stays authoritative.
      const theme = readChartTheme()
      if (theme) chart = renderer.renderScopeChart(element, data, theme)
    }
    const retheme = () => {
      if (!active) return
      const theme = readChartTheme()
      if (!theme) { chart?.dispose(); chart = null; return }
      if (chart) chart.update(theme)
      else resize()
    }
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(resize)
    observer?.observe(element)
    const unsubscribe = subscribeChartTheme(retheme)
    resize()
    return () => { active = false; observer?.disconnect(); unsubscribe(); chart?.dispose(); chart = null }
  }, [data])
  return <figure aria-label={label}><div className="enterprise-chart" ref={node} aria-hidden="true" /><figcaption className="enterprise-chart-legend">{data.map((row, index) => <span key={row.name}><i aria-hidden="true" data-chart-slot={row.status ?? index} style={{ backgroundColor: chartSwatchVar(index, row.status) }} />{row.name}: {row.value}{index < data.length - 1 ? ' · ' : ''}</span>)}</figcaption></figure>
}
