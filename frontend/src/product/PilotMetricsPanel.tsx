import { useEffect, useState } from 'react'
import { localizedText } from '../i18n/messages'
import type { Locale as AppLocale } from '../i18n'
import { Card } from '../components/ui/card'
import { ApiError } from './api'
import { loadPilotCapabilities, loadPilotSummary, pilotTelemetryOptedOut, setPilotTelemetryOptOut, type PilotCapabilities, type PilotMetric, type PilotSummary } from './pilot-telemetry'

// S11 WP-B DEV-072 "Pilot Metrics" (18.01 Backlog row 73). Read-only observation panel. A value is
// shown ONLY for state VALUE; suppressed, unknown and unavailable metrics never render as zero or green.
const labels: Record<string, string> = {
  'pilot.journey.completion': 'pairs.m_1e4e264f37a7f0df',
  'pilot.role.adoption': 'pairs.m_f202fa0907b65058',
  'pilot.ai.correction_rate': 'pairs.m_7d1e92f2c9b93806',
  'pilot.sync.conflicts': 'pairs.m_717076f60234c6a8',
  'pilot.evidence.search_p95': 'pairs.m_07587adbd5b767bb',
}

function formatValue(metric: PilotMetric, language: AppLocale) {
  const value = Number(metric.value)
  if (metric.unitCode === 'permille') return new Intl.NumberFormat(language, { style: 'percent', maximumFractionDigits: 1 }).format(value / 1000)
  if (metric.unitCode === 'millisecond') return new Intl.NumberFormat(language, { style: 'unit', unit: 'millisecond', maximumFractionDigits: 0 }).format(value)
  return new Intl.NumberFormat(language, { maximumFractionDigits: 0 }).format(value)
}

export function PilotMetricsPanel({ language }: { language: AppLocale }) {
  const [windowDays, setWindowDays] = useState<7 | 30>(7)
  const [caps, setCaps] = useState<{ value?: PilotCapabilities; error?: number } | null>(null)
  const [summary, setSummary] = useState<{ windowDays: number; value?: PilotSummary; error?: number } | null>(null)
  const [optedOut, setOptedOut] = useState(pilotTelemetryOptedOut)
  const online = typeof navigator === 'undefined' || navigator.onLine
  useEffect(() => {
    if (!online) return
    const c = new AbortController(); let active = true
    loadPilotCapabilities(c.signal).then(value => { if (active) setCaps({ value }) })
      .catch(error => { if (active) setCaps({ error: error instanceof ApiError ? error.status : 0 }) })
    return () => { active = false; c.abort() }
  }, [online])
  const canRead = caps?.value?.canRead === true
  useEffect(() => {
    if (!canRead || !online) return
    const c = new AbortController(); let active = true
    loadPilotSummary(windowDays, c.signal).then(value => { if (active) setSummary({ windowDays, value }) })
      .catch(error => { if (active) setSummary({ windowDays, error: error instanceof ApiError ? error.status : 0 }) })
    return () => { active = false; c.abort() }
  }, [canRead, online, windowDays])
  // Nothing to show for a membership that neither reads nor contributes.
  if (caps?.value && !caps.value.canRead && !caps.value.canContribute) return null
  const title = localizedText(language, 'pairs.m_890c89eeb732e540')
  const current = summary?.windowDays === windowDays ? summary : null
  const minimum = caps?.value?.minContributors ?? 5
  const state = (m: PilotMetric) => m.state === 'VALUE' ? formatValue(m, language)
    : m.state === 'SUPPRESSED' ? localizedText(language, 'pairs.m_491e25056de3a616', { count: minimum })
      : m.state === 'UNKNOWN' ? localizedText(language, 'pairs.m_9bede0b7d465c30f')
        : m.reasons.includes('RETENTION_UNCONFIGURED') ? localizedText(language, 'pairs.m_09483c324f9a07f6') : localizedText(language, 'pairs.m_9b65549adf9f7a6e')
  return <section aria-label={title} className="pilot-metrics">
    <h2>{title}</h2>
    <p>{localizedText(language, 'pairs.m_a58de4c61f6aaa0e')}</p>
    {!online ? <p role="status">{localizedText(language, 'pairs.m_909aee4a8e414ae0')}</p>
      : !caps ? <p role="status">{localizedText(language, 'pairs.m_9adc5ee5e5721a00')}</p>
        : caps.error !== undefined ? <p role="alert">{[401, 403].includes(caps.error) ? localizedText(language, 'pairs.m_927893496f0b3625') : localizedText(language, 'pairs.m_2a84c38675e0a79c')}</p>
          : <>
            {caps.value!.retentionState === 'UNCONFIGURED' && <p role="status" data-state="unconfigured">{localizedText(language, 'pairs.m_09483c324f9a07f6')}</p>}
            {caps.value!.canContribute && <label><input type="checkbox" checked={!optedOut} onChange={event => { setPilotTelemetryOptOut(!event.target.checked); setOptedOut(!event.target.checked) }}/> {localizedText(language, 'pairs.m_f8b0b166c51fa3f8')}</label>}
            {canRead && <>
              <label>{localizedText(language, 'pairs.m_6d69ca1d9fb68c62')} <select value={windowDays} onChange={event => setWindowDays(event.target.value === '30' ? 30 : 7)}>
                {([7, 30] as const).map(days => <option key={days} value={days}>{localizedText(language, 'pairs.m_b5bbb70736081cf1', { count: days })}</option>)}
              </select></label>
              {!current ? <p role="status">{localizedText(language, 'pairs.m_9adc5ee5e5721a00')}</p>
                : !current.value ? <p role="alert">{current.error === 403 ? localizedText(language, 'pairs.m_927893496f0b3625') : localizedText(language, 'pairs.m_2a84c38675e0a79c')}</p>
                  : <div className="health-observation-grid">{current.value.items.map(m => <Card key={m.metricId} data-metric={m.metricId} data-state={m.state}>
                    <h3>{localizedText(language, labels[m.metricId])}</h3>
                    <p className={m.state === 'VALUE' ? 'pilot-metric-value' : 'pilot-metric-withheld'}>{state(m)}</p>
                    {m.state === 'VALUE' && m.contributors !== null && <p>{localizedText(language, 'pairs.m_761a38d0c8645634', { count: m.contributors })}</p>}
                  </Card>)}</div>}
            </>}
          </>}
  </section>
}
