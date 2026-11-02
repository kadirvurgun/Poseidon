import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CrewAssignmentHistory } from './CrewAssignmentHistory'
const vessel = '11111111-1111-4111-8111-111111111111', assignment = '22222222-2222-4222-8222-222222222222'
const json = (v: unknown, status = 200) => new Response(JSON.stringify(v), { status, headers: { 'Content-Type': 'application/json' } })
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
it('loads actual contract shape, distinguishes unknown historical policy and clears it after current denial', async () => {
  const fetcher = vi.fn().mockResolvedValue(json({ items: [{ revision: 1, from: null, to: null, contractEndDate: null, positionId: null,
    status: 'Draft', provenance: 'SyntheticUnverified', savedAt: '2026-11-02T10:00:00Z', sourceObservedAt: null, assignmentPolicyVersion: null, positionPolicyVersion: null }], historicalIdentity: 'Unavailable', offset: 0, hasMore: false }))
  vi.stubGlobal('fetch', fetcher)
  render(<CrewAssignmentHistory vessel={vessel} assignment={assignment} language="en" />)
  expect(await screen.findByText('Recorded at')).toBeVisible(); expect(screen.getAllByText('Historical policy unknown')).toHaveLength(2)
  fetcher.mockResolvedValue(json({}, 403)); await userEvent.click(screen.getByRole('button', { name: 'Reload assignment history' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Assignment history access denied.')
  expect(screen.queryByText('Recorded at')).not.toBeInTheDocument()
})
it('ignores a late response for a previous assignment', async () => {
  let finish!: (v: Response) => void
  vi.stubGlobal('fetch', vi.fn().mockImplementationOnce(() => new Promise<Response>(resolve => { finish = resolve })).mockResolvedValue(json({ items: [], historicalIdentity: 'Unavailable', offset: 0, hasMore: false })))
  const view = render(<CrewAssignmentHistory vessel={vessel} assignment={assignment} language="tr" />)
  view.rerender(<CrewAssignmentHistory vessel={vessel} assignment={vessel} language="tr" />)
  expect(await screen.findByText('Atama sürümü yok.')).toBeVisible()
  finish(json({}, 403)); expect(screen.queryByRole('alert')).not.toBeInTheDocument()
})
