import { init, use as registerECharts } from 'echarts/core'
import { PieChart } from 'echarts/charts'
import { AriaComponent, TooltipComponent } from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'
import { chartColor, type ChartStatus, type ChartTheme } from '../theme/chart-theme'

registerECharts([PieChart, AriaComponent, TooltipComponent, SVGRenderer])

export type ScopeChartRow = { name: string; value: number; status?: ChartStatus }

/** Pure option builder: every colour comes from the active theme tokens (THEME-CHART-01). */
export function scopeChartOption(data: ScopeChartRow[], theme: ChartTheme) {
  return {
    animation: false, backgroundColor: 'transparent', aria: { enabled: true },
    tooltip: { trigger: 'item', renderMode: 'richText', confine: true, backgroundColor: theme.tooltip, borderColor: theme.border, textStyle: { color: theme.tooltipText } },
    series: [{ type: 'pie', radius: ['62%', '82%'], center: ['50%', '50%'], startAngle: 90,
      avoidLabelOverlap: true, label: { show: false }, labelLine: { show: false },
      itemStyle: { borderColor: theme.surface, borderWidth: 4, borderRadius: 5 },
      emphasis: { scale: false },
      // Colour slot follows the row's position in the full list, so the HTML legend swatch always matches.
      data: data.map((row, index) => ({ row, index })).filter(({ row }) => row.value > 0)
        .map(({ row, index }) => ({ name: row.name, value: row.value, itemStyle: { color: chartColor(theme, index, row.status) } }))
    }]
  }
}

export function renderScopeChart(element: HTMLDivElement, data: ScopeChartRow[], theme: ChartTheme) {
  const chart = init(element, undefined, { renderer: 'svg' })
  chart.setOption(scopeChartOption(data, theme))
  return {
    resize: () => chart.resize(),
    update: (next: ChartTheme) => chart.setOption(scopeChartOption(data, next), { notMerge: true }),
    dispose: () => chart.dispose(),
  }
}
