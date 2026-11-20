import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { ApiError } from './api'
import { PilotMetricsPanel } from './PilotMetricsPanel'
import { loadPilotCapabilities, loadPilotSummary, type PilotCapabilities, type PilotSummary } from './pilot-telemetry'

// S11 WP-B DEV-072 "Pilot Metrics": suppressed/unknown/unavailable never render as a value or green.
vi.mock('./pilot-telemetry', async original => ({ ...await original<typeof import('./pilot-telemetry')>(), loadPilotCapabilities: vi.fn(), loadPilotSummary: vi.fn() }))
const caps = (over: Partial<PilotCapabilities> = {}): PilotCapabilities => ({ canContribute: true, canRead: true, retentionState: 'CONFIGURED', retentionDays: 30,
  schemaVersion: 'pilot-event.v1', maxEventsPerBatch: 50, requestsPerMinute: 30, kinds: [], areas: ['SHIP'], shipJourneys: [], tugJourneys: [], aiSources: [], windowDays: [7, 30],
  minContributors: 5, ...over })
const at = '2026-11-20T10:00:00Z'
const summary = (): PilotSummary => ({ contractVersion: 'metrics.v1', windowDays: 7, from: at, asOf: at, basis: 'DECLARED_UNVERIFIED', retentionState: 'CONFIGURED', areas: ['SHIP'], items: [
  { metricId: 'pilot.journey.completion', definitionVersion: 1, unitCode: 'permille', state: 'VALUE', value: '833', contributors: 6, reasons: [], breakdown: [] },
  { metricId: 'pilot.role.adoption', definitionVersion: 1, unitCode: 'person', state: 'SUPPRESSED', value: null, contributors: null, reasons: ['SMALL_GROUP'], breakdown: [] },
  { metricId: 'pilot.ai.correction_rate', definitionVersion: 1, unitCode: 'permille', state: 'UNKNOWN', value: null, contributors: null, reasons: ['NO_OBSERVATIONS'], breakdown: [] },
  { metricId: 'pilot.sync.conflicts', definitionVersion: 1, unitCode: 'conflict', state: 'UNKNOWN', value: null, contributors: null, reasons: ['NO_OBSERVATIONS'], breakdown: [] },
  { metricId: 'pilot.evidence.search_p95', definitionVersion: 1, unitCode: 'millisecond', state: 'UNAVAILABLE', value: null, contributors: null, reasons: ['QUERY_LIMIT_EXCEEDED'], breakdown: [] },
] })
beforeEach(async () => { await changeAppLanguage('en', false); localStorage.clear(); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); localStorage.clear() })

it('shows a value only for VALUE and withholds suppressed, unknown and unavailable metrics', async () => {
  vi.mocked(loadPilotCapabilities).mockResolvedValue(caps()); vi.mocked(loadPilotSummary).mockResolvedValue(summary())
  const { container } = render(<PilotMetricsPanel language="en"/>)
  expect(await screen.findByText('83.3%')).toBeVisible()
  expect(screen.getByText('6 contributors')).toBeVisible()
  const adoption = container.querySelector('[data-metric="pilot.role.adoption"]') as HTMLElement
  expect(within(adoption).getByText('Hidden: fewer than 5 contributors')).toBeVisible()
  expect(within(container.querySelector('[data-metric="pilot.sync.conflicts"]') as HTMLElement).getByText('No observations')).toBeVisible()
  expect(within(container.querySelector('[data-metric="pilot.evidence.search_p95"]') as HTMLElement).getByText('Not available')).toBeVisible()
  expect(screen.queryByText(/^0$|^0 ms$|^0%$/)).not.toBeInTheDocument()
  expect(screen.getByText(/declared and unverified, not an acceptance claim/)).toBeVisible()
})

it('switches the window, offers an opt-out and states when retention is unconfigured', async () => {
  vi.mocked(loadPilotCapabilities).mockResolvedValue(caps({ retentionState: 'UNCONFIGURED', retentionDays: null, canContribute: false }))
  vi.mocked(loadPilotSummary).mockResolvedValue(summary())
  const user = userEvent.setup(); render(<PilotMetricsPanel language="en"/>)
  expect(await screen.findByText('Retention is not configured; no pilot telemetry is collected.')).toBeVisible()
  expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  await screen.findByText('83.3%')
  await user.selectOptions(screen.getByRole('combobox'), '30')
  expect(loadPilotSummary).toHaveBeenLastCalledWith(30, expect.anything())
})

it('lets an enrolled contributor opt out in this browser', async () => {
  vi.mocked(loadPilotCapabilities).mockResolvedValue(caps({ canRead: false }))
  const user = userEvent.setup(); render(<PilotMetricsPanel language="en"/>)
  const share = await screen.findByRole('checkbox', { name: 'Share pilot usage data from this browser' })
  expect(share).toBeChecked()
  await user.click(share)
  expect(share).not.toBeChecked()
  expect(localStorage.getItem('poseidon.pilot-telemetry.opt-out')).toBe('1')
  expect(loadPilotSummary).not.toHaveBeenCalled()
})

it('renders nothing for a membership that neither reads nor contributes, and a forbidden or offline state otherwise', async () => {
  vi.mocked(loadPilotCapabilities).mockResolvedValue(caps({ canRead: false, canContribute: false }))
  const { container, unmount } = render(<PilotMetricsPanel language="en"/>)
  await vi.waitFor(() => expect(loadPilotCapabilities).toHaveBeenCalled())
  await vi.waitFor(() => expect(container).toBeEmptyDOMElement())
  unmount()
  vi.mocked(loadPilotCapabilities).mockRejectedValue(new ApiError(403))
  const second = render(<PilotMetricsPanel language="en"/>)
  expect(await screen.findByRole('alert')).toHaveTextContent('You are not permitted to view pilot metrics.')
  second.unmount()
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
  render(<PilotMetricsPanel language="tr"/>)
  expect(screen.getByRole('status')).toHaveTextContent('Çevrimdışı: pilot metrikleri yenilenmiyor.')
})
