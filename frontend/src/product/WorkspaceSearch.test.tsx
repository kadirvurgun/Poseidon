import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { WorkspaceSearch } from './WorkspaceSearch'

const item = { key: '10000000-0000-0000-0000-000000000001', href: '/app/fleet/10000000-0000-0000-0000-000000000001', label: 'Synthetic Tug', kind: 'Tug' }
const page = (items: typeof item[] = [], offset = 0, hasMore = false) => ({ items, offset, limit: 25, hasMore })
const reply = (body: unknown, status = 200) => ({ ok: status === 200, status, json: async () => body }) as Response
const grant = {}
const props = () => ({ authorization: 'allowed' as const, grant, onRetryAuthorization: vi.fn(), language: 'en' as const, onNavigate: vi.fn(), onClose: vi.fn() })
describe('NAV-02/05 authorized search UI, mocked API', () => {
  it('encodes bounded input and pages on the server before selecting an implemented result', async () => {
    const fetch = vi.fn(async (url: string) => reply(page(Array.from({ length: 25 }, (_, i) => ({ ...item, key: `10000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, href: `/app/fleet/10000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, label: i === 0 ? item.label : `Synthetic ${i}` })), url.includes('offset=25') ? 25 : 0, true))); vi.stubGlobal('fetch', fetch)
    const callbacks = props(); const user = userEvent.setup(); render(<WorkspaceSearch {...callbacks} />)
    await screen.findByRole('link', { name: /Synthetic Tug/ })
    const input = screen.getByRole('searchbox')
    expect(input).toHaveAttribute('maxLength', '100')
    await user.type(input, 'Tug & Ship'); await user.click(screen.getByRole('button', { name: 'Search' }))
    await waitFor(() => expect(fetch).toHaveBeenLastCalledWith('/api/v1/workspace/search?q=Tug%20%26%20Ship&offset=0&limit=25', expect.anything()))
    await user.click(await screen.findByRole('button', { name: 'Next' }))
    await waitFor(() => expect(fetch).toHaveBeenLastCalledWith('/api/v1/workspace/search?q=Tug%20%26%20Ship&offset=25&limit=25', expect.anything()))
    await user.click(await screen.findByRole('link', { name: /Synthetic Tug/ }))
    expect(callbacks.onNavigate).toHaveBeenCalledWith(item.href)
  })
  it.each([401, 403, 503])('shows failed authorization or transport (%s) without stale results and supports retry', async status => {
    const fetch = vi.fn().mockResolvedValueOnce(reply({}, status)).mockResolvedValue(reply(page())); vi.stubGlobal('fetch', fetch)
    const user = userEvent.setup(); render(<WorkspaceSearch {...props()} />)
    expect(await screen.findByRole('alert')).toBeVisible()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByText('No records found.')).toBeVisible()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
  it.each(['https://untrusted.example', '/app/unknown', '/app/fleet/%2foutside'])('fails closed for an invalid result link %s', async href => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply(page([{ ...item, href }]))))
    render(<WorkspaceSearch {...props()} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('unverified')
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })
})
