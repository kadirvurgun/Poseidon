import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CertificateDocumentPicker } from './CertificateDocumentPicker'
const vessel = '10000000-0000-4000-8000-000000000001', file = { id: '10000000-0000-4000-8000-000000000002', vesselId: vessel, name: 'synthetic-certificate.pdf', state: 'CleanTransport', length: 1024, version: 19, revision: 1, checksum: 'A'.repeat(64) }
const page = { items: [file], offset: 0, hasMore: false, canWrite: false, available: true, maxBytes: 1048576, allowedTypes: ['application/pdf'] }
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
describe('T-S6-001 exact document declaration source (authored; not executed)', () => {
  it('returns an exact clean-transport revision/checksum pin without calling it verified', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => response(page))); const onChange = vi.fn()
    render(<CertificateDocumentPicker vessel={vessel} owner="synthetic-owner" value={null} language="en" disabled={false} onChange={onChange} />)
    const choice = await screen.findByRole('option', { name: 'synthetic-certificate.pdf · revision 1' })
    expect(choice).toBeVisible(); await userEvent.setup().selectOptions(screen.getByLabelText('Declared source document'), file.id)
    expect(onChange).toHaveBeenCalledWith({ id: file.id, revision: 1, checksum: file.checksum })
    expect(screen.getByText(/does not establish issuer authenticity/)).toBeVisible()
  })
  it('retains a selected pin on denied access and withdraws old file choices on identity-scope change', async () => {
    let denied = false; vi.stubGlobal('fetch', vi.fn(async () => response(denied ? {} : page, denied ? 403 : 200))); const onChange = vi.fn(), value = { id: file.id, revision: 1, checksum: file.checksum }
    const view = render(<CertificateDocumentPicker vessel={vessel} owner="original" value={value} language="en" disabled={false} onChange={onChange} />)
    await screen.findByRole('option', { name: 'synthetic-certificate.pdf · revision 1' })
    denied = true; view.rerender(<CertificateDocumentPicker vessel={vessel} owner="other" value={value} language="en" disabled={false} onChange={onChange} />)
    expect(screen.queryByRole('option', { name: 'synthetic-certificate.pdf · revision 1' })).not.toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('retained pin was not cleared'))
    expect(onChange).not.toHaveBeenCalled(); expect(screen.getByLabelText('Declared source document')).toBeDisabled()
  })
})
