import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CrewIdentityHistory } from './CrewIdentityHistory'

const fetcher = vi.fn()
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const props = { vessel: '11111111-1111-1111-1111-111111111111', assignment: '22222222-2222-2222-2222-222222222222', language: 'en' as const }
beforeEach(() => {
  Object.defineProperty(navigator, 'onLine', { value: true, configurable: true })
  Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true })
  fetcher.mockReset(); fetcher.mockResolvedValue(json({ historicalValues: 'Unavailable', items: [{ revision: 2, operation: 'LegacyConversion', savedAt: '2026-11-01T10:00:00Z', fullName: 'FICTIONAL forbidden history value' }] }))
  vi.stubGlobal('fetch', fetcher)
})
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
describe('protected identity history (AUTHORED / NOT RUN)', () => {
  it('loads only on explicit expansion and renders safe action metadata while excluding unexpected private fields', async () => {
    render(<CrewIdentityHistory {...props} />); expect(fetcher).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Identity action history' }))
    expect(await screen.findByText('2 · LegacyConversion · 2026-11-01T10:00:00Z')).toBeVisible()
    expect(screen.getByText('Historical private values are unavailable.')).toBeVisible()
    expect(screen.queryByText('FICTIONAL forbidden history value')).not.toBeInTheDocument()
    expect(fetcher.mock.calls[0][0]).toBe(`/api/v1/vessels/${props.vessel}/crew/${props.assignment}/person/history`)
    await userEvent.click(screen.getByRole('button', { name: 'Identity action history' })); expect(screen.queryByRole('list')).not.toBeInTheDocument()
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
  })
  it('withdraws cached metadata while hidden and reauthorizes with the same session on return, clearing denied history', async () => {
    render(<CrewIdentityHistory {...props} />); await userEvent.click(screen.getByRole('button', { name: 'Identity action history' }))
    await screen.findByRole('list')
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true }); fireEvent(document, new Event('visibilitychange'))
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
    fetcher.mockResolvedValue(json({ code: 'field_policy_denied' }, 403))
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true }); fireEvent(document, new Event('visibilitychange'))
    await screen.findByRole('alert'); expect(screen.getByRole('alert')).toHaveTextContent('Identity history unavailable.')
    expect(screen.queryByRole('list')).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledTimes(2)
  })
  it('withdraws history offline and ignores a late response from the old person after context changes', async () => {
    let complete!: (value: Response) => void
    fetcher.mockImplementationOnce(() => new Promise<Response>(resolve => { complete = resolve }))
    const { rerender } = render(<CrewIdentityHistory {...props} />)
    await userEvent.click(screen.getByRole('button', { name: 'Identity action history' }))
    fetcher.mockResolvedValue(json({ items: [{ revision: 9, operation: 'Declaration', savedAt: 'new-person-time' }] }))
    rerender(<CrewIdentityHistory {...props} assignment="33333333-3333-3333-3333-333333333333" />)
    await screen.findByText('9 · Declaration · new-person-time')
    complete(json({ items: [{ revision: 1, operation: 'OLD-PERSON', savedAt: 'old' }] }))
    await waitFor(() => expect(screen.queryByText(/OLD-PERSON/)).not.toBeInTheDocument())
    Object.defineProperty(navigator, 'onLine', { value: false, configurable: true }); fireEvent(window, new Event('offline'))
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
    fetcher.mockResolvedValue(json({ code: 'authentication_required' }, 401))
    Object.defineProperty(navigator, 'onLine', { value: true, configurable: true }); fireEvent(window, new Event('online'))
    await screen.findByRole('alert'); expect(screen.queryByRole('list')).not.toBeInTheDocument()
  })
  it('keeps TR history unavailability explicit on denied access', async () => {
    fetcher.mockResolvedValue(json({ code: 'resource_not_found' }, 404))
    render(<CrewIdentityHistory {...props} language="tr" />)
    await userEvent.click(screen.getByRole('button', { name: 'Kimlik işlem geçmişi' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Kimlik geçmişi kullanılamıyor.')
    expect(screen.getByText('Geçmiş özel alan değerleri kullanılamıyor.')).toBeVisible()
  })
})
