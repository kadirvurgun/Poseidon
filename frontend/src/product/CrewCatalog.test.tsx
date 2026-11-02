import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CrewCatalog } from './CrewCatalog'
import { decodeCatalog } from './crew-catalog-api'
const vessel = '11111111-1111-4111-8111-111111111111', authority = '22222222-2222-4222-8222-222222222222'
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const empty = { version: 0, activeRevision: null, items: [], hasMore: false, actions: [{ action: 'draft', authorityId: authority }] }
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
it('freezes an uncertain proposal and retries the exact command UUID and bytes', async () => {
  const user = userEvent.setup(), dirty = vi.fn(), changed = vi.fn()
  const fetcher = vi.fn().mockResolvedValueOnce(response(empty)).mockRejectedValueOnce(new TypeError('network'))
    .mockResolvedValueOnce(response({ revision: 1, version: 11, replayed: true })).mockResolvedValue(response({ ...empty, version: 11 }))
  vi.stubGlobal('fetch', fetcher)
  render(<CrewCatalog vessel={vessel} csrf="synthetic-csrf" language="en" onDirty={dirty} onChanged={changed} />)
  await screen.findByRole('button', { name: 'Propose revision' })
  await user.type(screen.getByLabelText('Reason'), 'Synthetic source proposal')
  await user.click(screen.getByLabelText('Source-defined catalog proposal (JSON)'))
  await user.paste(JSON.stringify({ version: 'fictional-v1', sourceReference: 'Synthetic', timeZone: '', periodSemantics: '', positions: [] }))
  await user.click(screen.getByRole('button', { name: 'Propose revision' }))
  expect(await screen.findByText('Result uncertain. Retry the same command before editing.')).toBeVisible()
  expect(screen.getByLabelText('Reason')).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'Retry exact command' }))
  expect(changed).toHaveBeenCalledTimes(1)
  expect(fetcher.mock.calls[1][1].body).toEqual(fetcher.mock.calls[2][1].body)
  expect(fetcher.mock.calls[1][1].headers['Idempotency-Key']).toEqual(fetcher.mock.calls[2][1].headers['Idempotency-Key'])
  expect(dirty).toHaveBeenCalledWith(true)
})
it('withdraws the catalog and proposal controls after current access denial', async () => {
  const fetcher = vi.fn().mockResolvedValueOnce(response(empty)).mockResolvedValue(response({}, 403)); vi.stubGlobal('fetch', fetcher)
  render(<CrewCatalog vessel={vessel} csrf="synthetic" language="en" onDirty={vi.fn()} onChanged={vi.fn()} />)
  await screen.findByRole('button', { name: 'Propose revision' })
  await userEvent.click(screen.getByRole('button', { name: 'Discard and reload catalog' }))
  expect(await screen.findByText('Catalog access denied.')).toBeVisible()
  expect(screen.queryByLabelText('Source-defined catalog proposal (JSON)')).not.toBeInTheDocument()
})
it('rejects coerced versions, hidden fields and wrong-vessel catalog tuples', () => {
  expect(() => decodeCatalog({ ...empty, version: '0' }, vessel)).toThrow()
  expect(() => decodeCatalog({ ...empty, approved: true }, vessel)).toThrow()
  const item = { revision: 1, state: 'Draft', policyVersion: 'v1', actorId: authority, roleId: authority, reason: 'Synthetic', savedAt: '2026-11-02T00:00:00Z', hash: 'A'.repeat(64), priorHash: null,
    content: { version: 'v1', sourceReference: 'Synthetic', timeZone: 'UTC', periodSemantics: 'StartInclusiveEndExclusive', positions: [{ tenantId: authority, vesselId: authority, positionId: authority, code: 'Synthetic', rank: null, capacity: null, department: null, provenance: 'SyntheticUnverified', from: null, to: null }] } }
  expect(() => decodeCatalog({ ...empty, items: [item] }, vessel)).toThrow()
})
