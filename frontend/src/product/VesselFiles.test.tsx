import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { VesselFiles } from './VesselFiles'
import { decodeFile, filePage } from './file-api'
import * as files from './file-api'

const vessel = '10000000-0000-0000-0000-000000000001'
const id = '10000000-0000-0000-0000-000000000002'
const row = { id, vesselId: vessel, name: 'synthetic.png', state: 'Receiving', length: 32, checksum: null, revision: null, version: 1 }
const page = { items: [row], offset: 0, hasMore: false, canWrite: false, available: false, maxBytes: 0 }
const response = (value: unknown, status = 200) => ({ ok: status === 200, status, json: async () => value }) as Response
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('shows quarantine and operator gate honestly in TR/EN and cannot download pending bytes', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => response(path.includes('?') ? page : row)))
  const user = userEvent.setup(); const view = render(<VesselFiles vessel={vessel} language="en" csrf="synthetic-csrf" onDirty={vi.fn()} onSaving={vi.fn()} />)
  expect(await screen.findByText(/Storage, scanner and operator settings/)).toBeVisible()
  await user.click(screen.getByRole('button', { name: row.name }))
  expect(await screen.findByRole('article', { name: 'File details' })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Download file' })).not.toBeInTheDocument()
  view.rerender(<VesselFiles vessel={vessel} language="tr" csrf="synthetic-csrf" onDirty={vi.fn()} onSaving={vi.fn()} />)
  expect(screen.getByText(/kanıtı doğrulamaz/)).toBeVisible()
})
it('withholds malformed cross-vessel or coerced transport responses', async () => {
  for (const value of [{ ...row, vesselId: id }, { ...row, state: ['CleanTransport'] }, { ...row, state: 'Verified' }, { ...row, name: '../a.png' }, { ...row, name: 'a.png\u2028' }, { ...row, length: '32' }, { ...row, checksum: 'A'.repeat(64) }])
    expect(() => decodeFile(value, vessel)).toThrow()
  vi.stubGlobal('fetch', vi.fn(async () => response({ ...page, items: [row, row] })))
  await expect(filePage(vessel, 0, new AbortController().signal)).rejects.toThrow()
})
it('does not retain stale file details after an authorization error and refreshes explicitly', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response({}, 403)).mockResolvedValue(response({ ...page, items: [] })))
  const user = userEvent.setup(); render(<VesselFiles vessel={vessel} language="en" csrf="synthetic-csrf" onDirty={vi.fn()} onSaving={vi.fn()} />)
  expect(await screen.findByRole('alert')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Refresh files' }))
  expect(await screen.findByText('No files in this vessel scope.')).toBeVisible()
})
it('preserves selected file dirty state and does not submit while unavailable', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response({ ...page, canWrite: true })))
  const dirty = vi.fn(); render(<VesselFiles vessel={vessel} language="en" csrf="synthetic-csrf" onDirty={dirty} onSaving={vi.fn()} />)
  await waitFor(() => expect(screen.getByLabelText('PNG file')).toBeDisabled())
  expect(screen.getByRole('button', { name: 'Upload file' })).toBeDisabled()
  expect(dirty).not.toHaveBeenCalled()
})
it('freezes ambiguous upload payload and key; retry reconciles without a second byte submission', async () => {
  const upload = vi.spyOn(files, 'uploadFile').mockRejectedValue(new Error('synthetic lost acknowledgement'))
  vi.stubGlobal('crypto', { randomUUID: () => id, subtle: { digest: async () => new Uint8Array(32).buffer } })
  let attempts = 0
  const fetch = vi.fn(async (path: string, init?: RequestInit) => response(path.includes('?') ? { ...page, available: true, canWrite: true, maxBytes: 1024 } : init?.method === 'POST' ? { ...row, state: ++attempts === 1 ? 'Pending' : 'Receiving' } : row))
  vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup(); const dirty = vi.fn()
  render(<VesselFiles vessel={vessel} language="en" csrf="synthetic-csrf" onDirty={dirty} onSaving={vi.fn()} />)
  const input = await screen.findByLabelText('PNG file'); await waitFor(() => expect(input).toBeEnabled())
  await user.upload(input, new File([new Uint8Array(32)], 'synthetic.png', { type: 'image/png' }))
  expect(dirty).toHaveBeenCalledWith(true)
  await user.click(screen.getByRole('button', { name: 'Upload file' }))
  expect(await screen.findByText(/Outcome unknown or rejected/)).toBeVisible()
  expect(input).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'Retry same attempt' }))
  expect(await screen.findByText(/Quarantined or interrupted/)).toBeVisible()
  expect(upload).toHaveBeenCalledTimes(1)
  const posts = fetch.mock.calls.filter(([, init]) => init?.method === 'POST')
  expect(posts).toHaveLength(2); expect(posts[0][1]?.body).toEqual(posts[1][1]?.body); expect(posts[0][1]?.headers).toEqual(posts[1][1]?.headers)
  expect(dirty).not.toHaveBeenCalledWith(false)
})
