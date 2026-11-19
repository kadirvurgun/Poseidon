import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LegalEntitySummary } from './LegalEntitySummary'

// PLAN-1511-5201 mocked UI behavior (not real backend acceptance).
const reply = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response
const draft = { id: 'company-one', code: 'C1', name: 'Synthetic Company', legalName: null, countryCode: null, hrTimeZone: null, baseLocale: null,
  status: 'Draft', effectiveFrom: null, effectiveTo: null, revision: 0, version: 3, missingForActivation: ['legalName', 'countryCode', 'hrTimeZone', 'baseLocale', 'effectiveFrom'],
  identifiers: [], roles: [], canEdit: false }
afterEach(() => vi.restoreAllMocks())

describe('PLAN-1511-5201 legal entity summary', () => {
  it('makes no HR read until requested and shows an incomplete draft as unknown, never active', async () => {
    const fetchMock = vi.fn(async () => reply(draft)); vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<LegalEntitySummary companyId="company-one" language="en" />)
    expect(fetchMock).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Show legal entity record' }))
    expect(await screen.findByText('Draft – incomplete, not active')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/hr/legal-entities/company-one', expect.objectContaining({ credentials: 'same-origin' }))
    expect(screen.getAllByText('Unknown')).toHaveLength(3)
    expect(screen.getByText('Missing values before activation').nextElementSibling).toHaveTextContent('5')
    expect(screen.queryByText('Active')).not.toBeInTheDocument()
  })

  it('shows only masked current identifiers and accepts responses without the full value', async () => {
    const identifiers = [
      { id: 'i1', type: 'TradeRegistry', issuer: 'SYN', value: null, maskedValue: '*****0011', sourceKind: 'Document', createdAt: '2026-01-01T00:00:00Z', retiredAt: null, version: 1 },
      { id: 'i2', type: 'TaxId', issuer: 'SYN', value: 'SYN77770022', maskedValue: '*******0022', sourceKind: 'Document', createdAt: '2026-01-01T00:00:00Z', retiredAt: null, version: 1 },
      { id: 'i3', type: 'Other', issuer: 'SYN', value: null, maskedValue: '****9999', sourceKind: 'Document', createdAt: '2026-01-01T00:00:00Z', retiredAt: '2026-02-01T00:00:00Z', version: 2 }]
    vi.stubGlobal('fetch', vi.fn(async () => reply({ ...draft, identifiers })))
    const user = userEvent.setup()
    render(<LegalEntitySummary companyId="company-one" language="en" />)
    await user.click(screen.getByRole('button', { name: 'Show legal entity record' }))
    expect(await screen.findByText('Registration identifiers (masked)')).toBeInTheDocument()
    expect(screen.getByText('*****0011')).toBeInTheDocument()
    expect(screen.getByText('*******0022')).toBeInTheDocument()
    expect(screen.queryByText(/SYN77770022/)).not.toBeInTheDocument()
    expect(screen.queryByText('****9999')).not.toBeInTheDocument()
  })

  it('rejects an identifier without a mask as an invalid contract', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => reply({ ...draft, identifiers: [{ id: 'i1', type: 'Other', value: 'RAW' }] })))
    const user = userEvent.setup()
    render(<LegalEntitySummary companyId="company-one" language="en" />)
    await user.click(screen.getByRole('button', { name: 'Show legal entity record' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Legal entity details could not be loaded.')
    expect(screen.queryByText(/RAW/)).not.toBeInTheDocument()
  })

  it('renders Turkish copy and explains unavailable access without exposing details', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => reply({ code: 'permission_denied' }, 403)))
    const user = userEvent.setup()
    render(<LegalEntitySummary companyId="company-one" language="tr" />)
    await user.click(screen.getByRole('button', { name: 'Tüzel kişi kaydını göster' }))
    expect(await screen.findByText('Mevcut erişiminizle tüzel kişi ayrıntıları görüntülenemez.')).toBeInTheDocument()
  })

  it('keeps a retry path after a transport failure', async () => {
    const fetchMock = vi.fn().mockRejectedValueOnce(new TypeError('offline')).mockResolvedValueOnce(reply({ ...draft, status: 'Active', legalName: 'Synthetic A.S.',
      countryCode: 'TR', hrTimeZone: 'Europe/Istanbul', baseLocale: 'tr-TR', effectiveFrom: '2020-01-01T00:00:00Z', missingForActivation: [] }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<LegalEntitySummary companyId="company-one" language="en" />)
    await user.click(screen.getByRole('button', { name: 'Show legal entity record' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Legal entity details could not be loaded.')
    await user.click(screen.getByRole('button', { name: 'Show legal entity record' }))
    expect(await screen.findByText('Synthetic A.S.')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.queryByText('Missing values before activation')).not.toBeInTheDocument()
  })
})
