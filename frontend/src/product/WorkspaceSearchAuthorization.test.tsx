import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { WorkspaceSearch } from './WorkspaceSearch'

const item = { key: '10000000-0000-0000-0000-000000000001', href: '/app/fleet/10000000-0000-0000-0000-000000000001', label: 'FICTIONAL earlier result', kind: 'Tug' }
const page = { items: [item], offset: 0, limit: 25, hasMore: false }
const reply = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const props = { language: 'en' as const, onClose: vi.fn(), onNavigate: vi.fn(), onRetryAuthorization: vi.fn() }
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('withdraws a page immediately on a pending grant and does not reuse it for a new allowed grant', async () => {
  let finish!: (response: Response) => void
  const fetch = vi.fn().mockResolvedValueOnce(reply(page)).mockImplementationOnce(() => new Promise<Response>(resolve => { finish = resolve }))
  vi.stubGlobal('fetch', fetch)
  const view = render(<WorkspaceSearch {...props} authorization="allowed" grant={{}} />)
  await screen.findByRole('link', { name: /FICTIONAL earlier result/ })
  view.rerender(<WorkspaceSearch {...props} authorization="pending" grant={null} />)
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Search' })).toBeDisabled()
  expect(fetch).toHaveBeenCalledTimes(1)
  view.rerender(<WorkspaceSearch {...props} authorization="allowed" grant={{}} />)
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Loading')
  await act(async () => finish(reply({ ...page, items: [{ ...item, label: 'FICTIONAL newly authorized result' }] })))
  expect(await screen.findByRole('link', { name: /FICTIONAL newly authorized result/ })).toBeVisible()
  expect(screen.queryByRole('link', { name: /FICTIONAL earlier result/ })).not.toBeInTheDocument()
})

it.each(['denied', 'unavailable'] as const)('aborts and ignores an old result while authorization is %s and retries only navigation', async authorization => {
  let finish!: (response: Response) => void
  let signal: AbortSignal | undefined
  const fetch = vi.fn((_url: string, init: RequestInit) => {
    signal = init.signal as AbortSignal
    return new Promise<Response>(resolve => { finish = resolve })
  })
  vi.stubGlobal('fetch', fetch)
  const retry = vi.fn(); const user = userEvent.setup()
  const view = render(<WorkspaceSearch {...props} onRetryAuthorization={retry} authorization="allowed" grant={{}} />)
  view.rerender(<WorkspaceSearch {...props} onRetryAuthorization={retry} authorization={authorization} grant={null} />)
  expect(signal?.aborted).toBe(true)
  await act(async () => finish(reply(page)))
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  expect(screen.getByRole('alert')).toHaveTextContent(authorization === 'denied' ? 'not permitted' : 'unavailable')
  expect(screen.getByRole('button', { name: 'Search' })).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'Retry authorization' }))
  expect(retry).toHaveBeenCalledTimes(1)
  expect(fetch).toHaveBeenCalledTimes(1)
})

it('does not reuse an old page when a newly allowed grant returns malformed data or a fresh 403', async () => {
  let finish!: (response: Response) => void
  const fetch = vi.fn().mockResolvedValueOnce(reply(page)).mockImplementationOnce(() => new Promise<Response>(resolve => { finish = resolve })).mockResolvedValueOnce(reply({}, 403))
  vi.stubGlobal('fetch', fetch)
  const view = render(<WorkspaceSearch {...props} authorization="allowed" grant={{}} />)
  await screen.findByRole('link', { name: /FICTIONAL earlier result/ })
  view.rerender(<WorkspaceSearch {...props} authorization="allowed" grant={{}} />)
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  await act(async () => finish(reply({ ...page, offset: 25 })))
  expect(await screen.findByRole('alert')).toHaveTextContent('unverified')
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  view.rerender(<WorkspaceSearch {...props} authorization="allowed" grant={{}} />)
  expect(await screen.findByRole('alert')).toHaveTextContent('not permitted')
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
})

it.each([
  { name: 'missing', grant: undefined }, { name: 'null', grant: null },
  { name: 'boolean', grant: false }, { name: 'number', grant: 0 },
  { name: 'string', grant: 'not-a-grant' }, { name: 'array', grant: [] },
  { name: 'function', grant: () => 'not-a-grant' },
])('fails closed for a runtime $name token even when the status says allowed', ({ grant }) => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  // Deliberately bypass static types to exercise a missing or malformed caller.
  render(<WorkspaceSearch {...props} authorization="allowed" grant={grant as unknown as object | null} />)
  expect(screen.getByRole('button', { name: 'Search' })).toBeDisabled()
  expect(screen.getByRole('alert')).toHaveTextContent('authorization unavailable')
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  expect(fetch).not.toHaveBeenCalled()
})
