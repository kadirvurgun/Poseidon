import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { WorkspaceSearch } from './WorkspaceSearch'

const id = '10000000-0000-0000-0000-000000000001'
const item = { key: id, href: `/app/fleet/${id}`, label: 'T1 · Earlier private result', kind: 'Tug' }
const page = (items: unknown[]) => ({ items, offset: 0, limit: 25, hasMore: false })
const response = (body: unknown, status = 200) => ({ ok: status === 200, status, json: async () => body }) as Response
const props = { language: 'en' as const, onClose: vi.fn(), onNavigate: vi.fn() }
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })

it('renders localized audit and Tug destinations from the actual validated server result', async () => {
  const results = [
    { key: 'audit', href: '/app/audit', label: 'audit', kind: 'Destination' },
    { key: 'tug-job-board', href: '/app/tug-job-board', label: 'tug-job-board', kind: 'Destination' },
  ]
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response(page(results))))
  const user = userEvent.setup(); const navigate = vi.fn()
  const view = render(<WorkspaceSearch {...props} onNavigate={navigate} />)
  await user.click(await screen.findByRole('link', { name: 'Tug job board' }))
  expect(navigate).toHaveBeenCalledWith('/app/tug-job-board')
  expect(screen.getByRole('link', { name: 'Action history' })).toHaveAttribute('href', '/app/audit')
  view.rerender(<WorkspaceSearch {...props} language="tr" onNavigate={navigate} />)
  expect(screen.getByRole('link', { name: 'Römorkör iş panosu' })).toBeVisible()
})

it.each([403, 502])('withdraws loaded results when the next search is denied or malformed (%s)', async status => {
  const fetch = vi.fn().mockResolvedValueOnce(response(page([item])))
    .mockResolvedValueOnce(status === 403 ? response({}, 403) : response({ ...page([]), offset: 25 }))
  vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); render(<WorkspaceSearch {...props} />)
  await screen.findByRole('link', { name: /Earlier private result/ })
  await user.type(screen.getByRole('searchbox'), 'next')
  await user.click(screen.getByRole('button', { name: 'Search' }))
  expect(await screen.findByRole('alert')).toBeVisible()
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  expect(screen.queryByText('No records found.')).not.toBeInTheDocument()
})

it('discards a late successful result after its search was superseded by a denied search', async () => {
  let finish!: (value: Response) => void
  let firstSignal: AbortSignal | undefined
  const fetch = vi.fn().mockImplementationOnce((_url: string, init: RequestInit) => {
    firstSignal = init.signal as AbortSignal
    return new Promise<Response>(resolve => { finish = resolve })
  }).mockResolvedValueOnce(response({}, 403))
  vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); render(<WorkspaceSearch {...props} />)
  await user.type(screen.getByRole('searchbox'), 'restricted')
  await user.click(screen.getByRole('button', { name: 'Search' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('not permitted')
  expect(firstSignal?.aborted).toBe(true)
  await act(async () => finish(response(page([item]))))
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
  expect(screen.getByRole('alert')).toHaveTextContent('not permitted')
})
