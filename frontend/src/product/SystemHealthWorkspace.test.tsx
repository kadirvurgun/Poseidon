import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import * as api from './api'
import { SystemHealthWorkspace } from './SystemHealthWorkspace'
import { loadSystemHealth } from './system-health-api'
import { loadPilotCapabilities } from './pilot-telemetry'
import { healthVessel, unobservedHealth } from './system-health.test-fixture'
import { changeAppLanguage } from '../i18n'

vi.mock('./EnterpriseChrome', () => ({ EnterpriseChrome: ({ onLanguageChange }: { onLanguageChange: (value: 'en' | 'tr') => void }) => <button onClick={() => onLanguageChange('en')}>EN</button> }))
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), session: vi.fn(), vessels: vi.fn() }))
vi.mock('./system-health-api', async original => ({ ...await original<typeof import('./system-health-api')>(), loadSystemHealth: vi.fn() }))
vi.mock('./pilot-telemetry', async original => ({ ...await original<typeof import('./pilot-telemetry')>(), loadPilotCapabilities: vi.fn(), loadPilotSummary: vi.fn() }))
const tenant = '10000000-0000-4000-8000-000000000002'
beforeEach(() => {
  history.replaceState(null, '', `/app/system-health?vessel=${healthVessel}`)
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible')
  vi.mocked(api.session).mockResolvedValue({ authenticated: true, canRead: true, actorId: tenant, tenantId: tenant, tenantName: 'FICTIONAL tenant', csrfToken: 'synthetic-only', setupRequired: false, canCreate: false, canReadCompanies: false, canSearch: false })
  vi.mocked(api.vessels).mockResolvedValue({ items: [{ id: healthVessel, companyId: tenant, code: 'SYN-1', name: 'FICTIONAL vessel', kind: 'Ship', version: 1, canEdit: false }], offset: 0, limit: 25, hasMore: false })
  vi.mocked(loadSystemHealth).mockResolvedValue(unobservedHealth())
  // S11 WP-B: a membership without pilot read/contribute renders no pilot panel.
  vi.mocked(loadPilotCapabilities).mockResolvedValue({ canContribute: false, canRead: false, retentionState: 'UNCONFIGURED', retentionDays: null, schemaVersion: 'pilot-event.v1',
    maxEventsPerBatch: 50, requestsPerMinute: 30, kinds: [], areas: [], shipJourneys: [], tugJourneys: [], aiSources: [], windowDays: [7, 30], minContributors: 5 })
})
afterEach(() => { vi.resetAllMocks(); vi.restoreAllMocks(); history.replaceState(null, '', '/') })

it('localizes the freshness categories without re-reading observations or turning missing telemetry into zero', async () => {
  await changeAppLanguage('de', false)
  const user = userEvent.setup()
  render(<SystemHealthWorkspace />)
  const future = await screen.findByText('Zukünftig datiert')
  expect(future.nextElementSibling).toHaveTextContent('Nicht verfügbar')
  expect(loadSystemHealth).toHaveBeenCalledTimes(1)
  await user.click(screen.getByRole('button', { name: 'EN' }))
  expect(await screen.findByText('Future dated')).toBeVisible()
  expect(screen.getByText('Future dated').nextElementSibling).toHaveTextContent('Unavailable')
  expect(loadSystemHealth).toHaveBeenCalledTimes(1)
})

it('DEV-037 shows unobserved signals and unprobed configuration without fabricated API zeroes or green health', async () => {
  const user = userEvent.setup(); render(<SystemHealthWorkspace />)
  await user.click(screen.getByRole('button', { name: 'EN' }))
  await screen.findByRole('region', { name: 'Authorized service observations' })
  const apiCard = screen.getByRole('heading', { name: 'API' }).parentElement!
  expect(within(apiCard).getAllByText('Unavailable')).toHaveLength(3)
  expect(within(apiCard).queryByText('0')).not.toBeInTheDocument()
  expect(screen.getByText('Device backlog').nextElementSibling).toHaveTextContent('Unavailable')
  expect(screen.getByText('Storage observation').nextElementSibling).toHaveTextContent('Configured · unprobed')
  expect(screen.queryByText(/^healthy$|^ready$|^approved$/i)).not.toBeInTheDocument()
})
it('denies an unauthorized health context without showing any observation cards', async () => {
  vi.mocked(loadSystemHealth).mockRejectedValue(new api.ApiError(403))
  const user = userEvent.setup(); render(<SystemHealthWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Health access is unavailable for this context.')
  expect(screen.queryByRole('region', { name: 'Authorized service observations' })).not.toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Audit writer' })).not.toBeInTheDocument()
})
it('withdraws prior observations on connection loss and waits for a fresh authorized read', async () => {
  const user = userEvent.setup(); render(<SystemHealthWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
  await screen.findByRole('region', { name: 'Authorized service observations' })
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
  act(() => window.dispatchEvent(new Event('offline')))
  expect(screen.queryByRole('region', { name: 'Authorized service observations' })).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Offline. Current service observations are unavailable.')
  vi.mocked(loadSystemHealth).mockRejectedValue(new api.ApiError(503))
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  act(() => window.dispatchEvent(new Event('online')))
  expect(await screen.findByRole('alert')).toHaveTextContent('No healthy state is inferred.')
})
it('does not issue health or session reads for a URL carrying unsupported tenant overrides', async () => {
  history.replaceState(null, '', `/app/system-health?vessel=${healthVessel}&tenant=${tenant}`)
  const user = userEvent.setup(); render(<SystemHealthWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
  expect(screen.getByRole('alert')).toHaveTextContent('Health context URL is invalid.')
  expect(api.session).not.toHaveBeenCalled(); expect(loadSystemHealth).not.toHaveBeenCalled()
})
it('DEV-037 shows real notification job, last-sync and liveness observations with abandoned runs named, not green', async () => {
  vi.mocked(loadSystemHealth).mockResolvedValue({ ...unobservedHealth(),
    sync: { ...unobservedHealth().sync, committed: 4, lastCommittedAt: '2026-11-04T11:58:00Z' },
    notifications: { state: 'Observed', scope: 'SelectedVesselDurable', openUnacknowledged: 3, overdueEscalations: 1, dispatch: 'LeaseExpired', lastCompletedAt: '2026-11-04T11:00:00Z', nextRunAt: null } })
  const user = userEvent.setup(); render(<SystemHealthWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
  const jobs = (await screen.findByRole('heading', { name: 'Notification jobs' })).parentElement!
  expect(within(jobs).getByText('Open, unacknowledged').nextElementSibling).toHaveTextContent('3')
  expect(within(jobs).getByText('Escalations past due').nextElementSibling).toHaveTextContent('1')
  expect(within(jobs).getByText('Evaluation job').nextElementSibling).toHaveTextContent('Run abandoned (lease expired)')
  expect(within(jobs).getByText('Next scheduled run').nextElementSibling).toHaveTextContent('Not observed')
  expect(within(jobs).getByText(/not proof of delivery or of any safety decision/)).toBeVisible()
  expect(screen.getByText('Last committed sync').nextElementSibling).toHaveTextContent('2026-11-04T11:58:00Z')
  const liveness = screen.getByRole('heading', { name: 'Service liveness' }).parentElement!
  expect(within(liveness).getByText('Uptime (seconds)').nextElementSibling).toHaveTextContent('3,600')
  expect(screen.queryByText(/^healthy$|^ready$|^ok$/i)).not.toBeInTheDocument()
})
it('DEV-037 marks a snapshot whose server read time is two minutes old as stale and a current one as not stale', async () => {
  vi.mocked(loadSystemHealth).mockResolvedValueOnce({ ...unobservedHealth(), readAt: new Date(Date.now() - 180000).toISOString() })
  const user = userEvent.setup(); const view = render(<SystemHealthWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
  expect(await screen.findByText(/older than two minutes/)).toBeVisible()
  expect(screen.getByRole('region', { name: 'Authorized service observations' })).toBeVisible()
  view.unmount()
  vi.mocked(loadSystemHealth).mockResolvedValueOnce({ ...unobservedHealth(), readAt: new Date().toISOString() })
  render(<SystemHealthWorkspace />); await user.click(screen.getByRole('button', { name: 'EN' }))
  await screen.findByRole('region', { name: 'Authorized service observations' })
  expect(screen.queryByText(/older than two minutes/)).not.toBeInTheDocument()
})
