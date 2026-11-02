import { init, use as registerECharts } from 'echarts/core'
import { PieChart } from 'echarts/charts'
import { AriaComponent, TooltipComponent } from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'

registerECharts([PieChart, AriaComponent, TooltipComponent, SVGRenderer])

export function renderScopeChart(element: HTMLDivElement, data: { name: string; value: number; color: string }[]) {
  const chart = init(element, undefined, { renderer: 'svg' })
  chart.setOption({
    animation: false, backgroundColor: 'transparent', aria: { enabled: true },
    tooltip: { trigger: 'item', renderMode: 'richText', confine: true },
    series: [{ type: 'pie', radius: ['62%', '82%'], center: ['50%', '50%'], startAngle: 90,
      avoidLabelOverlap: true, label: { show: false }, labelLine: { show: false },
      itemStyle: { borderColor: '#ffffff', borderWidth: 4, borderRadius: 5 },
      emphasis: { scale: false },
      data: data.filter(row => row.value > 0).map(row => ({ name: row.name, value: row.value, itemStyle: { color: row.color } }))
    }]
  })
  return { resize: () => chart.resize(), dispose: () => chart.dispose() }
}
