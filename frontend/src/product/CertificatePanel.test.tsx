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

it('offers only scanned current-person document versions and withdraws choices after same-session denial', async () => {
  const fileId = '33333333-3333-3333-3333-333333333333'
  const manifest = { personId: assignment, policyVersion: 'synthetic-v1', purpose: 'synthetic-files', kinds: ['CertificateDocument', 'Photo'], canWrite: false,
    hasMore: false, next: {}, items: [{ id: fileId, personId: assignment, kind: 'CertificateDocument', revision: 2, expiresOn: null,
      policyVersion: 'synthetic-v1', purpose: 'synthetic-files', sourceVersion: 'poseidon-v46.3-20261030', createdAt: '2026-11-01T10:00:00Z', state: 'CleanTransport' }] }
  fetcher.mockImplementation(async (url: string) => json(url.endsWith('/personal-files') ? manifest : []))
  render(<CertificatePanel vessel={vessel} assignment={assignment} person={assignment} language="en" csrf="SYNTHETIC" onDirty={dirty} />)
  await userEvent.click(await screen.findByRole('button', { name: 'Add certificate declaration' }))
  const choice = await screen.findByRole('option', { name: 'Scanned unverified person version 2 · Expiry unknown' })
  expect(choice).toHaveValue(fileId)
  const selector = screen.getByLabelText('Scanned person document version')
  await userEvent.selectOptions(selector, fileId)
  expect(selector).toHaveValue(fileId)
  fetcher.mockImplementation(async (url: string) => url.endsWith('/personal-files') ? json({}, 403) : json([]))
  await userEvent.click(screen.getByRole('button', { name: 'Refresh document choices' }))
  expect(await screen.findByRole('option', { name: 'Current reference unavailable; refresh or clear explicitly' })).toBeDisabled()
  expect(screen.queryByRole('option', { name: 'Scanned unverified person version 2 · Expiry unknown' })).not.toBeInTheDocument()
  // Explicit removal is distinct from unknown permissions silently clearing a captured declaration.
  await userEvent.selectOptions(selector, '')
  expect(selector).toHaveValue('')
})
