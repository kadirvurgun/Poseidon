import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AgentsDashboard } from './AgentsDashboard'
import { App } from '../App'
import { parseStatus } from './contract'
import type { OpsStatus } from './contract'

// OPS-DASH-UI-01: mocked /ops/status verifies frontend behavior only.
const instant = '2026-10-31T15:00:00Z'
const epoch = Date.parse(instant) / 1000
function fixture(): OpsStatus {
  return {
    schemaVersion: 1, generatedAt: instant,
    quota: { timestamp: instant, next_check: '2026-10-31T16:00:00Z', selected: 'esra-pro', profiles: {
      'esra-pro': { state: 'known', ready: true, plan: 'Pro', windows: [{ usedPercent: 20, windowDurationMins: 10080, resetsAt: epoch + 90 }] },
      'kadir-plus': { state: 'known', ready: true, plan: 'Plus', windows: [{ usedPercent: 30, windowDurationMins: 300, resetsAt: epoch + 60 }, { usedPercent: 50, windowDurationMins: 10080, resetsAt: epoch + 300 }] },
    } },
    coordinator: { enabled: true, status: 'verifying', modelCalls: 2, maxCalls: 6, maxJobs: 3, deadline: epoch + 3600,
      jobs: [{ id: 'OPS-DASH-UI-01', status: 'verifying', profile: 'kadir-plus', phase: 'component', reason: 'Review pending', commit: 'abc123' }] },
    activities: [], events: [], alarms: [],
    project: { completionPercent: 12.5, acceptedStoryPoints: 10, totalStoryPoints: 80, acceptedTasks: 1, totalTasks: 8, stage: 'Local package' },
  }
}
const response = (data: unknown) => ({ ok: true, json: async () => data }) as Response
async function mount(data: unknown = fixture()) {
  const fetchMock = vi.fn().mockResolvedValue(response(data))
  vi.stubGlobal('fetch', fetchMock)
  const view = render(<AgentsDashboard />)
  await act(async () => {})
  return { ...view, fetchMock }
}
const card = (name: string) => within(screen.getByRole('region', { name }))

describe('OPS-DASH-UI-01 development operations (mock API)', () => {
  beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(new Date(instant)) })

  it('shows a running task separately from unknown quota with real service UTC offsets', async () => {
    const data = fixture()
    data.generatedAt = '2026-10-31T15:00:00.200000+00:00'
    data.quota.timestamp = '2026-10-31T15:00:00+00:00'
    data.quota.profiles['esra-pro'] = { state: 'busy', ready: false,
      lastKnown: { windows: [{ usedPercent: 18, windowDurationMins: 10080, resetsAt: epoch + 300 }], observedAt: instant } }
    data.activities = [{ id: 'runtime', profile: 'esra-pro', taskId: 'runtime', role: 'development', status: 'running', startedAt: instant, model: 'gpt-6.1-sol', effort: 'medium' }]
    await mount(data)
    expect(card('Esra Pro').getByText('Çalışıyor')).toBeVisible()
    expect(card('Esra Pro').getByText('Görev sürüyor · kota ölçümü görev bitince yenilenecek')).toBeVisible()
    expect(card('Esra Pro').getByText('82% kalan')).toBeVisible()
    expect(card('Esra Pro').getByText(/gpt-6.1-sol/)).toBeVisible()
    expect(card('Esra Pro').queryByText('Yeni göreve uygun')).not.toBeInTheDocument()
  })

  it('does not claim completed activities are still running', async () => {
    const data = fixture()
    data.activities = [{ id: 'finished', profile: 'esra-pro', taskId: 'finished', role: 'development', status: 'produced', startedAt: instant }]
    await mount(data)
    expect(card('Esra Pro').queryByText('Çalışıyor')).not.toBeInTheDocument()
  })

  it('displays the most used bucket when several plan buckets have the same duration', async () => {
    const data = fixture()
    data.quota.profiles['kadir-plus'].windows!.push({ usedPercent: 92, windowDurationMins: 300, resetsAt: epoch + 300 })
    await mount(data)
    expect(card('Kadir Plus').getByText('8% kalan')).toBeVisible()
    expect(card('Kadir Plus').getByText('Uygun değil / bilinmiyor')).toBeVisible()
  })

  it('preserves quota data without an advertised reset time and never invents a countdown', async () => {
    const data = fixture()
    delete data.quota.profiles['esra-pro'].windows![0].resetsAt
    await mount(data)
    expect(card('Esra Pro').getByText('80% kalan')).toBeVisible()
    expect(card('Esra Pro').getByText('Sıfırlanma zamanı bilinmiyor')).toBeVisible()
    expect(card('Esra Pro').queryByText(/Sıfırlanma:/)).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Canlı servis bağlantısı')
  })

  it('renders both profiles, missing Pro 5h, raw quota, accepted progress and CLI budget', async () => {
    const { fetchMock } = await mount()
    expect(card('Esra Pro').getByText('Pencere bildirilmedi')).toBeVisible()
    expect(card('Esra Pro').getByText('80% kalan')).toBeVisible()
    expect(card('Kadir Plus').getByText('70% kalan')).toBeVisible()
    expect(card('Kadir Plus').getByText(/component/)).toBeVisible()
    expect(card('Kadir Plus').getByText('Review pending')).toBeVisible()
    expect(screen.getByRole('progressbar', { name: 'Kabul edilen proje ilerlemesi' })).toHaveAttribute('value', '12.5')
    expect(screen.getByText('10 / 80 kabul edilen hikâye puanı')).toBeVisible()
    expect(screen.getByText('1 / 8 kabul edilen görev')).toBeVisible()
    expect(screen.getByText('CLI oturum bütçesi')).toBeVisible()
    expect(screen.getByText(/ham token kotası değildir/)).toBeVisible()
    expect(fetchMock).toHaveBeenCalledWith('/ops/status', expect.objectContaining({ cache: 'no-store', signal: expect.any(AbortSignal) }))
  })

  it('updates reset countdown without announcing each second or inventing eligibility after reset', async () => {
    await mount()
    expect(card('Kadir Plus').getByText('Sıfırlanma: 0g 00:01:00')).toBeVisible()
    await act(async () => { await vi.advanceTimersByTimeAsync(1000) })
    expect(card('Kadir Plus').getByText('Sıfırlanma: 0g 00:00:59')).toBeVisible()
    expect(screen.getByRole('status')).not.toHaveTextContent('00:00:59')
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    expect(card('Kadir Plus').getByText(/Süre doldu/)).toBeVisible()
  })

  it('uses quota timestamp rather than fresh generatedAt and shows last known windows', async () => {
    const data = fixture()
    data.quota.timestamp = '2026-10-31T13:44:59Z'
    data.quota.profiles['esra-pro'].lastKnown = data.quota.profiles['esra-pro'].windows
    delete data.quota.profiles['esra-pro'].windows
    await mount(data)
    expect(card('Esra Pro').getByText('Son bilinen ölçüm')).toBeVisible()
    expect(card('Esra Pro').getByText('Uygun değil / bilinmiyor')).toBeVisible()
    expect(card('Kadir Plus').getByText('Uygun değil / bilinmiyor')).toBeVisible()
    expect(screen.getByText(/Bilinmiyor \/ eski ölçüm/)).toBeVisible()
  })

  it.each(['busy', 'unavailable', 'reconcile_required'] as const)('does not mark %s ready despite server ready=true', async state => {
    const data = fixture()
    data.quota.profiles['esra-pro'].state = state
    await mount(data)
    expect(card('Esra Pro').getByText('Uygun değil / bilinmiyor')).toBeVisible()
    expect(card('Kadir Plus').getByText('Yeni göreve uygun')).toBeVisible()
  })

  it('requires reported windows and enforces the documented 85% reserve', async () => {
    const data = fixture()
    data.quota.profiles['esra-pro'].windows = []
    data.quota.profiles['kadir-plus'].windows![0].usedPercent = 85
    await mount(data)
    expect(card('Esra Pro').getByText('Uygun değil / bilinmiyor')).toBeVisible()
    expect(card('Kadir Plus').getByText('Uygun değil / bilinmiyor')).toBeVisible()
  })

  it('shows empty history without synthetic records', async () => {
    await mount()
    expect(screen.getByText('Geçmiş kaydı yok')).toBeVisible()
    expect(screen.getByText('Alarm kaydı yok')).toBeVisible()
    expect(screen.getAllByText('Etkin atama bildirilmedi')).toHaveLength(2)
  })

  it('shows supplied assignment, handover reason, local dates and alarm resume countdown', async () => {
    const data = fixture()
    data.activities = [{ id: 'a1', profile: 'esra-pro', taskId: 'OPS-REAL', role: 'developer', status: 'running', startedAt: instant }]
    data.events = [{ id: 'e1', at: instant, type: 'handover', from: 'esra-pro', to: 'kadir-plus', message: 'Independent review', taskId: 'OPS-REAL' }]
    data.alarms = [{ id: 'al1', profile: 'esra-pro', state: 'waiting', resumeAt: epoch + 120, reason: 'Reserve window', resetAt: epoch + 60 }]
    await mount(data)
    expect(card('Esra Pro').getByText('OPS-REAL')).toBeVisible()
    expect(screen.getByText('Esra Pro → Kadir Plus')).toBeVisible()
    expect(screen.getByText('Independent review')).toBeVisible()
    expect(document.querySelector('time')).toHaveAttribute('dateTime', instant)
    expect(screen.getByText('Bekliyor')).toBeVisible()
    expect(screen.getByText('Reserve window')).toBeVisible()
    expect(screen.getByText('0g 00:02:00')).toBeVisible()
  })

  it('switches TR/EN without fetching or losing data', async () => {
    const { fetchMock } = await mount()
    fireEvent.click(screen.getByRole('button', { name: 'EN' }))
    expect(screen.getByRole('heading', { name: 'Two accounts. One view.' })).toBeVisible()
    expect(screen.getByText('No history records')).toBeVisible()
    expect(document.documentElement.lang).toBe('en')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    fireEvent.click(screen.getByRole('button', { name: 'TR' }))
    expect(screen.getByRole('heading', { name: 'İki hesap. Tek görünüm.' })).toBeVisible()
  })

  it.each([null, {}, { ...fixture(), schemaVersion: 2 }, { ...fixture(), alarms: [{ state: 'ready' }] }])('rejects invalid schema and supports manual retry', async payload => {
    const { fetchMock } = await mount(payload)
    expect(screen.getByRole('status')).toHaveTextContent('veri geçersiz')
    expect(screen.getByText('Henüz operasyon verisi yok')).toBeVisible()
    fetchMock.mockResolvedValue(response(fixture()))
    fireEvent.click(screen.getByRole('button', { name: 'Tekrar dene' }))
    await act(async () => {})
    expect(card('Esra Pro').getByText('Yeni göreve uygun')).toBeVisible()
  })

  it('polls after 5 seconds, retains data on HTTP failure, and removes eligibility', async () => {
    const { fetchMock } = await mount()
    fetchMock.mockResolvedValue({ ok: false })
    await act(async () => { await vi.advanceTimersByTimeAsync(5_000) })
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(screen.getByRole('status')).toHaveTextContent('Servise ulaşılamadı')
    expect(card('Esra Pro').getByText('Uygun değil / bilinmiyor')).toBeVisible()
    expect(screen.getByText('Son başarılı sorgu: 5 sn önce')).toBeVisible()
    fetchMock.mockResolvedValue(response(fixture()))
    await act(async () => { await vi.advanceTimersByTimeAsync(5_000) })
    expect(card('Esra Pro').getByText('Yeni göreve uygun')).toBeVisible()
  })

  it('times out a hanging request, prevents overlap and aborts on unmount', async () => {
    const fetchMock = vi.fn(() => new Promise<Response>(() => {}))
    vi.stubGlobal('fetch', fetchMock)
    const view = render(<AgentsDashboard />)
    const signal = (fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].signal
    await act(async () => { await vi.advanceTimersByTimeAsync(5_000) })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    await act(async () => { await vi.advanceTimersByTimeAsync(3_000) })
    expect(signal?.aborted).toBe(true)
    expect(screen.getByRole('button', { name: 'Tekrar dene' })).toBeEnabled()
    fireEvent.click(screen.getByRole('button', { name: 'Tekrar dene' }))
    const nextSignal = (fetchMock.mock.calls[1] as unknown as [string, RequestInit])[1].signal
    view.unmount()
    expect(nextSignal?.aborted).toBe(true)
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('rejects out-of-range and malformed optional nested data', () => {
    const data = fixture()
    data.quota.profiles['esra-pro'].windows![0].usedPercent = 101
    expect(() => parseStatus(data)).toThrow()
    expect(() => parseStatus({ ...fixture(), events: [{ id: 'e', at: 'not UTC', type: 'handover' }] })).toThrow()
  })

  it('routes /agents through the minimal App wrapper', async () => {
    window.history.replaceState({}, '', '/agents')
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response(fixture())))
    try {
      render(<App />)
      await act(async () => {})
      expect(screen.getByRole('heading', { name: 'İki hesap. Tek görünüm.' })).toBeVisible()
    } finally { window.history.replaceState({}, '', '/') }
  })
})
