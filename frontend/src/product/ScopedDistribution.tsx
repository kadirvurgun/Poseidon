import { useEffect, useRef } from 'react'
import { loadScopeChartRenderer } from './scope-chart-loader'

export type DistributionRow = { name: string; value: number; color: string }
// Only the parent-owned, already authorized page is summarized. No global totals,
// trends or readiness claims are inferred from incomplete/unavailable adapters.
export function ScopedDistribution({ data, label }: { data: DistributionRow[]; label: string }) {
  const node = useRef<HTMLDivElement>(null)
  useEffect(() => {
    let active = true; let chart: { resize: () => void; dispose: () => void } | null = null
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
      if (chart) chart.resize()
      else chart = renderer.renderScopeChart(element, data)
    }
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(resize)
    observer?.observe(element)
    resize()
    return () => { active = false; observer?.disconnect(); chart?.dispose(); chart = null }
  }, [data])
  return <figure aria-label={label}><div className="enterprise-chart" ref={node} aria-hidden="true" /><figcaption className="enterprise-chart-legend">{data.map((row, index) => <span key={row.name}><i aria-hidden="true" style={{ backgroundColor: row.color }} />{row.name}: {row.value}{index < data.length - 1 ? ' · ' : ''}</span>)}</figcaption></figure>
}
