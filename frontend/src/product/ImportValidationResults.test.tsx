import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ImportValidationResults } from './ImportValidationResults'
import type { ImportValidationPage } from './ImportValidationResults'

const page: ImportValidationPage = {
  previewId: 'synthetic-preview', revision: 'synthetic-revision', templateVersion: 'synthetic-v1',
  contextKey: 'session/vessel/upload/schema', totalRows: 3, validRows: 1, rejectedRows: 2,
  offset: 0, limit: 2, hasMore: true,
  rows: [
    { rowNumber: 2, outcome: 'valid', issues: [] },
    { rowNumber: 4, outcome: 'rejected', issues: [{ code: 'required', fieldOrdinal: 0 }] },
  ],
}
const base = {
  language: 'tr' as const, contextKey: page.contextKey, state: 'ready' as const,
  page, fieldLabels: ['Ad'], canDownloadRejectReport: true,
  onPage: vi.fn(), onDownloadRejectReport: vi.fn(),
}

describe('DEV-069 safe validation outcome presentation (authored; execution deferred)', () => {
  it('shows actual aggregate counts and physical source rows without treating preview as imported records', () => {
    render(<ImportValidationResults {...base} />)
    expect(screen.getByText('Bu önizleme kayıtları içe aktarmadı.', { exact: false })).toBeInTheDocument()
    const totals = screen.getByText('Toplam satır').parentElement!
    expect(totals.querySelector('dd')).toHaveTextContent('3')
    expect(screen.getByText('Geçerli satır').parentElement!.querySelector('dd')).toHaveTextContent('1')
    expect(screen.getByText('Reddedilen satır').parentElement!.querySelector('dd')).toHaveTextContent('2')
    expect(screen.getByRole('rowheader', { name: '2' })).toBeInTheDocument()
    expect(screen.getByRole('rowheader', { name: '4' })).toBeInTheDocument()
    expect(screen.getByText('Önizleme için geçerli')).toBeInTheDocument()
    expect(screen.getByText('Zorunlu değer eksik.', { exact: false })).toBeInTheDocument()
    expect(screen.getByText('Gösterilen 1–2 / 3')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /başlat|commit|onayla|aktar/i })).not.toBeInTheDocument()
  })

  it('loads the actual next offset without confusing sparse physical row numbers with paging', async () => {
    const user = userEvent.setup(), next = vi.fn()
    render(<ImportValidationResults {...base} onPage={next} />)
    expect(screen.getByRole('button', { name: 'Önceki sayfa' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Sonraki sayfa' }))
    expect(next).toHaveBeenCalledWith(2)
  })

  it('navigates back from the final page and cannot fetch past the reported end', async () => {
    const user = userEvent.setup(), previous = vi.fn()
    const final: ImportValidationPage = { ...page, offset: 2, hasMore: false, rows: [
      { rowNumber: 9, outcome: 'rejected', issues: [{ code: 'duplicate', fieldOrdinal: null }] },
    ] }
    render(<ImportValidationResults {...base} page={final} onPage={previous} />)
    expect(screen.getByRole('button', { name: 'Sonraki sayfa' })).toBeDisabled()
    expect(screen.getByText('Gösterilen 3–3 / 3')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Önceki sayfa' }))
    expect(previous).toHaveBeenCalledWith(0)
  })

  it('requests the current authorized reject report through the workspace callback', async () => {
    const user = userEvent.setup(), download = vi.fn()
    render(<ImportValidationResults {...base} onDownloadRejectReport={download} />)
    await user.click(screen.getByRole('button', { name: 'Ret raporunu indir' }))
    expect(download).toHaveBeenCalledOnce()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('does not expose a report action without the explicit current capability', () => {
    render(<ImportValidationResults {...base} canDownloadRejectReport={false} />)
    expect(screen.queryByRole('button', { name: 'Ret raporunu indir' })).not.toBeInTheDocument()
  })

  it('does not expose a reject report for a preview containing only valid rows', () => {
    const valid: ImportValidationPage = { ...page, totalRows: 1, validRows: 1, rejectedRows: 0,
      hasMore: false, rows: [page.rows[0]] }
    render(<ImportValidationResults {...base} page={valid} />)
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ret raporunu indir' })).not.toBeInTheDocument()
  })

  it('distinguishes a real empty source from missing results', () => {
    const empty: ImportValidationPage = { ...page, totalRows: 0, validRows: 0, rejectedRows: 0, hasMore: false, rows: [] }
    render(<ImportValidationResults {...base} page={empty} />)
    expect(screen.getByRole('status')).toHaveTextContent('İçe aktarılacak kaynak satırı bulunamadı.')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it.each(['loading', 'denied', 'unavailable', 'stale'] as const)('hides old counts, rows and report controls while state is %s', state => {
    render(<ImportValidationResults {...base} state={state} />)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByText('Toplam satır')).not.toBeInTheDocument()
    expect(screen.queryByText('synthetic-v1')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ret raporunu indir' })).not.toBeInTheDocument()
  })

  it('hides all previous results as soon as the session/vessel/upload/schema context changes', () => {
    const { rerender } = render(<ImportValidationResults {...base} />)
    expect(screen.getByRole('table')).toBeInTheDocument()
    rerender(<ImportValidationResults {...base} contextKey="new-session/new-vessel/new-upload/new-schema" />)
    expect(screen.getByRole('status')).toHaveTextContent('Geçerli oturum, kaynak ve şablon için yeniden doğrulayın.')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('uses a generic fixed diagnostic for unknown issue codes, including prototype-like keys', () => {
    const safe: ImportValidationPage = { ...page, totalRows: 1, validRows: 0, rejectedRows: 1, hasMore: false, rows: [
      { rowNumber: 2, outcome: 'rejected', issues: [
        { code: 'private-cell-content-do-not-echo', fieldOrdinal: null },
        { code: '__proto__', fieldOrdinal: null },
      ] },
    ] }
    render(<ImportValidationResults {...base} page={safe} />)
    expect(screen.getAllByText('Satır doğrulanamadı. Yetkili ret raporunu inceleyin.')).toHaveLength(2)
    expect(screen.queryByText(/private-cell-content|__proto__/)).not.toBeInTheDocument()
  })

  it('renders authorized field labels as inert text', () => {
    const { container } = render(<ImportValidationResults {...base} fieldLabels={['<img src=x onerror=alert(1)>']} />)
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeInTheDocument()
    expect(container.querySelector('img')).toBeNull()
  })

  const invalidPages: Array<{ name: string; invalid: ImportValidationPage }> = [
    { name: 'inconsistent aggregate totals', invalid: { ...page, validRows: 3 } },
    { name: 'impossible result counts on the current page', invalid: { ...page, validRows: 0, rejectedRows: 3 } },
    { name: 'unsafe integer totals', invalid: { ...page, totalRows: Number.MAX_SAFE_INTEGER + 1 } },
    { name: 'duplicate physical row numbers', invalid: { ...page, rows: [page.rows[0], { ...page.rows[1], rowNumber: 2 }] } },
    { name: 'out-of-order source rows', invalid: { ...page, rows: [page.rows[1], page.rows[0]] } },
    { name: 'a valid row with validation errors', invalid: { ...page, rows: [{ ...page.rows[0], issues: [{ code: 'required', fieldOrdinal: 0 }] }, page.rows[1]] } },
    { name: 'a rejected row without diagnostics', invalid: { ...page, rows: [page.rows[0], { ...page.rows[1], issues: [] }] } },
    { name: 'a field outside the current template', invalid: { ...page, rows: [page.rows[0], { ...page.rows[1], issues: [{ code: 'required', fieldOrdinal: 1 }] }] } },
    { name: 'contradictory pagination metadata', invalid: { ...page, hasMore: false } },
    { name: 'empty page with unreturned records', invalid: { ...page, rows: [] } },
  ]
  it.each(invalidPages)('fails closed for $name without exposing partial counts or rows', ({ invalid }) => {
    render(<ImportValidationResults {...base} page={invalid} />)
    expect(screen.getByRole('alert')).toHaveTextContent('İçe aktarma sonuçları yüklenemedi.')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByText('Toplam satır')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ret raporunu indir' })).not.toBeInTheDocument()
  })

  it('disables paging and download throughout an in-flight operation', async () => {
    const user = userEvent.setup(), next = vi.fn(), download = vi.fn()
    render(<ImportValidationResults {...base} busy onPage={next} onDownloadRejectReport={download} />)
    expect(screen.getByRole('region', { name: 'İçe aktarma doğrulama sonuçları' })).toHaveAttribute('aria-busy', 'true')
    await user.click(screen.getByRole('button', { name: 'Sonraki sayfa' }))
    await user.click(screen.getByRole('button', { name: 'Ret raporunu indir' }))
    expect(next).not.toHaveBeenCalled()
    expect(download).not.toHaveBeenCalled()
  })

  it('offers an explicit reload for missing results instead of interpreting them as an empty source', async () => {
    const user = userEvent.setup(), retry = vi.fn()
    render(<ImportValidationResults {...base} page={null} onRetry={retry} />)
    expect(screen.getByRole('alert')).toHaveTextContent('İçe aktarma sonuçları yüklenemedi.')
    await user.click(screen.getByRole('button', { name: 'Tekrar dene' }))
    expect(retry).toHaveBeenCalledOnce()
  })

  it('renders English fixed error descriptions and accessible table captions', () => {
    render(<ImportValidationResults {...base} language="en" />)
    expect(screen.getByRole('table', { name: 'Source row outcomes' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Validation details' })).toBeInTheDocument()
    expect(screen.getByText('A required value is missing.', { exact: false })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Download reject report' })).toBeEnabled()
  })
})
