import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { App } from './App'

function response(payload: unknown = { service: 'poseidon', status: 'available' }, ok = true) {
  return { ok, json: async () => payload } as Response
}

describe('APP-FOUNDATION-01 technical connection', () => {
  it('shows an accessible loading state while awaiting the service', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})))
    render(<App />)
    expect(screen.getByRole('status')).toHaveTextContent('Bağlantı kontrol ediliyor')
    expect(screen.getByTestId('connection-retry')).toBeDisabled()
  })

  it('renders a validated heartbeat without operational approval', async () => {
    const fetchMock = vi.fn().mockResolvedValue(response())
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'API bağlantısı kuruldu' })).toBeVisible()
    expect(screen.getByText(/emniyet kararının onaylandığını göstermez/)).toBeVisible()
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/system/status', expect.objectContaining({ cache: 'no-store', signal: expect.any(AbortSignal) }))
  })

  it.each([
    ['network', () => Promise.reject(new TypeError('offline'))],
    ['HTTP', () => Promise.resolve(response({}, false))],
    ['unexpected service', () => Promise.resolve(response({ service: 'other', status: 'available' }))],
    ['unexpected status', () => Promise.resolve(response({ service: 'poseidon', status: 'approved' }))],
    ['null payload', () => Promise.resolve(response(null))],
    ['invalid JSON', () => Promise.resolve({ ok: true, json: async () => { throw new SyntaxError() } })],
  ])('shows a recoverable error for %s failure', async (_, result) => {
    vi.stubGlobal('fetch', vi.fn(result))
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'API bağlantısı kurulamadı' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Tekrar dene' })).toBeEnabled()
  })

  it('allows a user to retry after a failure', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn().mockRejectedValueOnce(new Error()).mockResolvedValueOnce(response())
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    await user.click(await screen.findByRole('button', { name: 'Tekrar dene' }))
    expect(await screen.findByRole('heading', { name: 'API bağlantısı kuruldu' })).toBeVisible()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('switches TR/EN without sending another request', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn().mockResolvedValue(response())
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    await screen.findByRole('heading', { name: 'API bağlantısı kuruldu' })
    await user.click(screen.getByRole('button', { name: 'EN' }))
    expect(screen.getByRole('heading', { name: 'API connection established' })).toBeVisible()
    expect(document.documentElement.lang).toBe('en')
    expect(screen.getByRole('button', { name: 'EN' })).toHaveAttribute('aria-pressed', 'true')
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('aborts pending requests when unmounted', async () => {
    const fetchMock = vi.fn(() => new Promise(() => {}))
    vi.stubGlobal('fetch', fetchMock)
    const view = render(<App />)
    const signal = (fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].signal
    view.unmount()
    await waitFor(() => expect(signal?.aborted).toBe(true))
  })

  it('times out a hanging request and exposes retry', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('fetch', vi.fn((_: string, init: RequestInit) => new Promise((_, reject) => {
      init.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')))
    })))
    render(<App />)
    await act(async () => { await vi.advanceTimersByTimeAsync(10_000) })
    expect(screen.getByRole('heading', { name: 'API bağlantısı kurulamadı' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Tekrar dene' })).toBeEnabled()
  })
})
