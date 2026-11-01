import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { TugAuditRevision } from './TugAuditRevision'
const revision = { kind: 'TugJob', profile: null, register: null, tug: { jobId: 'synthetic-job', revision: 1, jobNumber: 'TUG-SYNTHETIC',
  sourceVersion: 'synthetic-source', savedAt: '2026-11-01T00:00:00Z', provenance: 'UserDeclaredUnverified', requestedStart: null, requestedEnd: null,
  requiredBollardPull: '45.01', requiredTugCount: null, jobType: 'Escort', priority: 'Normal', ownerRoleReference: null } }
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })
it('loads the exact requested immutable revision only on explicit review and labels it historical', async () => {
  const fetcher = vi.fn(async (_path: string) => json(revision)); vi.stubGlobal('fetch', fetcher)
  render(<TugAuditRevision auditId="synthetic-audit" language="en" />)
  expect(fetcher).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole('button', { name: 'Review exact immutable tug revision' }))
  expect(await screen.findByText('45.01')).toBeInTheDocument()
  expect(fetcher.mock.calls[0][0]).toBe('/api/v1/audit/synthetic-audit/revision')
  expect(screen.getByText(/Historic unverified request facts/)).toBeInTheDocument()
})
it('clears historic facts when an exact revision request is denied', async () => {
  let deny = false; vi.stubGlobal('fetch', vi.fn(async () => deny ? json({}, 404) : json(revision)))
  render(<TugAuditRevision auditId="synthetic-audit" language="en" />)
  fireEvent.click(screen.getByRole('button', { name: 'Review exact immutable tug revision' })); await screen.findByText('45.01')
  deny = true; fireEvent.click(screen.getByRole('button', { name: 'Review exact immutable tug revision' }))
  expect(await screen.findByText('Authorized historical revision unavailable.')).toBeInTheDocument()
  expect(screen.queryByText('45.01')).not.toBeInTheDocument()
})
it('aborts a stale audit selection before accepting its delayed revision', async () => {
  let finish!: (value: Response) => void
  const fetcher = vi.fn((_path: string, _init?: RequestInit) => new Promise<Response>(resolve => { finish = resolve })); vi.stubGlobal('fetch', fetcher)
  const view = render(<TugAuditRevision auditId="first" language="en" />)
  fireEvent.click(screen.getByRole('button', { name: 'Review exact immutable tug revision' }))
  const signal = fetcher.mock.calls[0][1]!.signal!
  view.rerender(<TugAuditRevision auditId="second" language="tr" />); finish(json(revision))
  await waitFor(() => expect(signal.aborted).toBe(true))
  expect(screen.queryByText('45.01')).not.toBeInTheDocument()
})
