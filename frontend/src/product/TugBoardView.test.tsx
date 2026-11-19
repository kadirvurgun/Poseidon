import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { TugBoardView, tugBoardHref } from './TugBoardView'
import { emptyTugInputs, type TugBoard } from './tug-api'
import { changeAppLanguage } from '../i18n'

vi.mock('./TugOwningEvidence', () => ({ TugOwningEvidence: () => <section aria-label="Current evidence detail">Synthetic UI projection</section> }))
const vessel = '11111111-1111-1111-1111-111111111111', id = '22222222-2222-2222-2222-222222222222'
const row: TugBoard['items'][number] = { request: { id, vesselId: vessel, jobNumber: 'FICTIONAL-JOB-01', revision: 2, version: 1,
  inputs: { ...emptyTugInputs(), assistedVessel: 'FICTIONAL Assisted Vessel', requiredBollardPull: '45.01', requestedStart: '2026-11-02T23:30:00-03:00', priority: 'High' },
  provenance: 'SyntheticUnverified', savedAt: '2026-11-02T12:00:00Z', missing: ['requested_end'], replayed: false, sourceVersion: 'synthetic-source', state: 'Draft' },
  windowState: 'EndNotSupplied', plannedTimeSource: 'RequestedWindowOnly;NoApprovedPlan', assignedTugs: [], assignmentCoverage: 'UnavailableControlledAssignmentAdapter',
  availableBollardPull: null, readiness: 'Unknown', conflict: 'Unknown', declaredComparisons: [], comparisonCoverage: 'VisiblePageEnteredDataOnly;NoBufferOrOperationalAssessment', blockingReasons: ['AssignmentCoverageUnavailable'], nextAction: 'CompleteDraft', detailHref: `/app/tug-job-detail/${id}?vessel=${vessel}` }
const defaults = { board: { items: [row], hasMore: true }, priorities: ['Normal', 'High', 'Critical'], filter: { q: 'FICTIONAL', priority: 'High', offset: 25 }, language: 'en' as const, offline: false, onFilter: vi.fn(), onNavigate: vi.fn(), onRefresh: vi.fn() }

it.each([
  { language: 'en' as const, timestamp: 'Nov 03, 2026, 02:30' },
  { language: 'en-GB' as const, timestamp: '03 Nov 2026, 02:30' },
])('shows exact BP, UTC instants and Unknown in $language without treating missing assignment coverage as zero available tugs', async ({ language, timestamp }) => {
  await changeAppLanguage(language, false)
  render(<TugBoardView {...defaults} language={language} />)
  const job = screen.getByRole('article', { name: 'FICTIONAL-JOB-01' })
  expect(job).toHaveTextContent('45.01 t')
  expect(within(job).getByText(timestamp)).toHaveAttribute('dateTime', '2026-11-02T23:30:00-03:00')
  expect(job).toHaveTextContent('End not supplied')
  expect(job).toHaveTextContent('Controlled assignment data unavailable')
  expect(job).not.toHaveTextContent('0 tugs')
  expect(within(job).queryByText('Ready', { exact: true })).not.toBeInTheDocument()
  const metrics = screen.getByLabelText('Current job page overview')
  expect(within(metrics).getByText('Readiness unknown').parentElement).toHaveTextContent('1')
  expect(screen.getByRole('navigation', { name: 'Job board pagination' })).toHaveTextContent('Counts cover this page only')
})

it('keeps search and priority together for server-side filtering, and preserves applied filters on pagination', async () => {
  const onFilter = vi.fn(), user = userEvent.setup()
  render(<TugBoardView {...defaults} onFilter={onFilter} />)
  await user.selectOptions(screen.getByLabelText('Priority'), 'Critical')
  expect(onFilter).toHaveBeenLastCalledWith({ q: 'FICTIONAL', priority: 'Critical', offset: 0 })
  await user.clear(screen.getByLabelText('Search requests')); await user.type(screen.getByLabelText('Search requests'), 'FICTIONAL & /?#')
  await user.click(screen.getByRole('button', { name: 'Apply filters' }))
  expect(onFilter).toHaveBeenLastCalledWith({ q: 'FICTIONAL & /?#', priority: 'High', offset: 0 })
  await user.click(screen.getByRole('button', { name: 'Next' }))
  expect(onFilter).toHaveBeenLastCalledWith({ q: 'FICTIONAL', priority: 'High', offset: 50 })
  await user.click(screen.getByRole('button', { name: 'Clear filters' }))
  expect(onFilter).toHaveBeenLastCalledWith({ q: '', priority: '', state: '', window: '', offset: 0 })
  expect(screen.getByLabelText('Search requests')).toHaveValue('')
})

it('delegates job navigation to the owning guard and reveals comparison/reasons through a native disclosure', async () => {
  const onNavigate = vi.fn(), user = userEvent.setup()
  render(<TugBoardView {...defaults} onNavigate={onNavigate} />)
  await user.click(screen.getByRole('link', { name: 'Open job' }))
  expect(onNavigate).toHaveBeenCalledWith(row.detailHref)
  const summary = screen.getByText('Why / next action'); await user.click(summary)
  expect(summary.closest('details')).toHaveAttribute('open')
  expect(screen.getByRole('region', { name: 'Current evidence detail' })).toBeVisible()
  expect(screen.getByText('Visible page times only; no buffer or operational assessment')).toBeVisible()
})

it('distinguishes a returned empty page from unavailable current data and disables offline refresh', () => {
  const mounted = render(<TugBoardView {...defaults} board={{ items: [], hasMore: false }} offline />)
  expect(screen.getByText('No draft requests.')).toBeVisible()
  expect(screen.getByRole('button', { name: 'Refresh board' })).toBeDisabled()
  mounted.rerender(<TugBoardView {...defaults} board={null} offline />)
  expect(screen.queryByText('No draft requests.')).not.toBeInTheDocument()
  expect(screen.queryByLabelText('Current job page overview')).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Current board unavailable')
})

it('encodes one scoped route without losing query delimiters or introducing duplicate selectors', () => {
  const href = tugBoardHref(vessel, { q: 'FICTIONAL & /?#', priority: 'High', offset: 25 })
  const url = new URL(href, 'https://localhost')
  expect(url.pathname).toBe('/app/tug-job-board')
  expect([...url.searchParams.entries()]).toEqual([['vessel', vessel], ['q', 'FICTIONAL & /?#'], ['priority', 'High'], ['offset', '25']])
  expect(url.hash).toBe('')
})

// DEV-082: state and server-relative window filters are closed codes kept in the URL.
it('filters by lifecycle state and server window, shows the state badge and lifecycle next action', async () => {
  const onFilter = vi.fn(), user = userEvent.setup()
  await changeAppLanguage('en', false)
  render(<TugBoardView {...defaults} board={{ items: [{ ...row, nextAction: 'SubmitRequest' }], hasMore: false }} filter={{ q: '', priority: '', offset: 0, state: 'Draft', window: 'next4h' }} onFilter={onFilter} />)
  const job = screen.getByRole('article', { name: 'FICTIONAL-JOB-01' })
  expect(job).toHaveAttribute('data-job-state', 'Draft')
  expect(job).toHaveTextContent('Next action: Submit request')
  expect(screen.getByText(/requests without a start time stay listed as unknown/)).toBeInTheDocument()
  await user.selectOptions(screen.getByLabelText('State'), 'Submitted')
  expect(onFilter).toHaveBeenLastCalledWith({ q: '', priority: '', offset: 0, state: 'Submitted', window: 'next4h' })
  await user.selectOptions(screen.getByLabelText('Time window'), 'next24h')
  expect(onFilter).toHaveBeenLastCalledWith({ q: '', priority: '', offset: 0, state: 'Draft', window: 'next24h' })
  expect(tugBoardHref(vessel, { q: '', priority: '', offset: 0, state: 'Cancelled', window: 'next72h' })).toBe(`/app/tug-job-board?vessel=${vessel}&state=Cancelled&window=next72h`)
})
