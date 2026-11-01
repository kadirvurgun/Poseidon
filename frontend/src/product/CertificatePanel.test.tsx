import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CertificatePanel } from './CertificatePanel'

const vessel = '11111111-1111-1111-1111-111111111111', assignment = '22222222-2222-2222-2222-222222222222'
const fetcher = vi.fn(), dirty = vi.fn()
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
beforeEach(() => { fetcher.mockResolvedValue(json([])); vi.stubGlobal('fetch', fetcher); dirty.mockReset() })
afterEach(() => { cleanup(); vi.unstubAllGlobals(); fetcher.mockReset() })
function panel() { return render(<CertificatePanel vessel={vessel} assignment={assignment} language="en" csrf="SYNTHETIC" onDirty={dirty} />) }
it('uses the private authorized person route and never supplies owner or verification fields', async () => {
  panel(); await userEvent.click(await screen.findByRole('button', { name: 'Add certificate declaration' }))
  await userEvent.type(screen.getByLabelText('Certificate number'), 'FICTIONAL PRIVATE NUMBER')
  await userEvent.type(screen.getByLabelText('Issuing country: source numeric type; precision unresolved'), '123456789012345678901234567890.000000000001')
  fetcher.mockImplementation(async (_url: string, init?: RequestInit) => init?.method === 'POST' ? json({ id: vessel }) : json([]))
  await userEvent.click(screen.getByRole('button', { name: 'Save unverified draft' }))
  const post = fetcher.mock.calls.find(([, init]) => init?.method === 'POST')!
  expect(post[0]).toContain(`/vessels/${vessel}/crew/${assignment}/certificates`)
  const body = JSON.parse(post[1].body); expect(body.metadata.issuingCountryNumeric).toBe('123456789012345678901234567890.000000000001')
  expect(body.metadata.endorsementRequired).toBeNull(); expect(body.personId).toBeUndefined(); expect(body.tenantId).toBeUndefined(); expect(body.verifiedBy).toBeUndefined()
})
it('retains exact declaration and idempotency key after an uncertain response', async () => {
  panel(); await userEvent.click(await screen.findByRole('button', { name: 'Add certificate declaration' }))
  const keys: unknown[] = []; fetcher.mockImplementation(async (_url: string, init?: RequestInit) => {
    if (init?.method !== 'POST') return json([])
    keys.push((init.headers as Record<string, string>)['Idempotency-Key']); if (keys.length === 1) throw new TypeError('network'); return json({})
  })
  await userEvent.click(screen.getByRole('button', { name: 'Save unverified draft' })); await userEvent.click(await screen.findByRole('button', { name: 'Retry same declaration' }))
  expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1])
})
it('withholds server diagnostics and private data when certificate permission is denied', async () => {
  fetcher.mockResolvedValue(json({ code: 'permission_denied', diagnostic: 'PRIVATE' }, 403)); panel()
  expect(await screen.findByRole('alert')).toHaveTextContent('Private certificate access denied.')
  expect(screen.queryByText(/PRIVATE/)).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Add certificate declaration' })).not.toBeInTheDocument()
})
