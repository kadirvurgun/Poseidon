import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TugPlanningPanel } from './TugPlanningPanel'
import { planningApi, type Plan, type PlanningOptions } from './tug-planning-api'
import { ApiError } from './api'

vi.mock('./tug-planning-api', () => ({ planningApi: { detail: vi.fn(), options: vi.fn(), history: vi.fn(), save: vi.fn(), revision: vi.fn() } }))
const id = '22222222-2222-2222-2222-222222222222', tug = '11111111-1111-1111-1111-111111111111'
const plan: Plan = { jobId: id, revision: 1, version: 41, requestRevision: 2, policyVersion: 'synthetic-v1', sourceVersion: '14.02:2316:1932-1957:v46.3', inputs: { plannedStart: null, plannedEnd: null, assignedTugs: [] }, operation: 'Window', provenance: 'UserDeclaredUnverified', savedAt: '2026-11-01T12:00:00Z', replayed: false }
const options: PlanningOptions = { state: 'Configured', policyVersion: 'synthetic-v1', canWrite: true, tugs: [{ id: tug, code: 'FICTIONAL-TUG' }], types: [], offline: 'UnavailableForTug' }
const dirty = vi.fn()
function panel(language: 'en' | 'tr' = 'en', vessel = tug) { return <TugPlanningPanel id={id} vessel={vessel} requestVersion={33} csrf="synthetic" language={language} onDirty={dirty} /> }
beforeEach(() => {
  vi.mocked(planningApi.detail).mockResolvedValue({ state: 'DraftUnverified', plan, evidence: null })
  vi.mocked(planningApi.options).mockResolvedValue(options)
  vi.mocked(planningApi.history).mockResolvedValue({ items: [plan], coverage: 'Latest100CurrentlyAuthorizedRevisions' })
  vi.mocked(planningApi.save).mockResolvedValue({ ...plan, revision: 2, version: 42 })
  vi.mocked(planningApi.revision).mockResolvedValue(plan)
})
afterEach(() => { cleanup(); vi.resetAllMocks() })
it('saves an explicitly incomplete planned window without substituting requested times and guards parent navigation', async () => {
  render(panel()); const start = await screen.findByLabelText('Planned start (UTC)'); expect(start).toHaveValue('')
  fireEvent.change(start, { target: { value: '2026-11-02T10:00' } }); await waitFor(() => expect(dirty).toHaveBeenLastCalledWith(true))
  await userEvent.click(screen.getByRole('button', { name: 'Save planning window' }))
  await waitFor(() => expect(planningApi.save).toHaveBeenCalledOnce())
  const attempt = vi.mocked(planningApi.save).mock.calls[0][2]
  expect(attempt.body).toEqual({ baseVersion: 41, requestVersion: 33, policyVersion: 'synthetic-v1', change: { operation: 'Window', plannedStart: '2026-11-02T10:00:00Z', plannedEnd: null } })
})
it('uses explicit controlled assignment commands and excludes arbitrary free text references', async () => {
  render(panel()); await userEvent.selectOptions(await screen.findByLabelText('Eligible tug', { selector: 'select' }), tug)
  await userEvent.click(screen.getByRole('button', { name: 'Assign to draft' }))
  await waitFor(() => expect(planningApi.save).toHaveBeenCalledOnce())
  expect(vi.mocked(planningApi.save).mock.calls[0][2].body.change).toEqual({ operation: 'Assign', tugId: tug })
})
it('freezes a lost-response command and retries the exact body and key', async () => {
  vi.mocked(planningApi.save).mockRejectedValueOnce(new TypeError('network')).mockResolvedValueOnce({ ...plan, replayed: true })
  render(panel()); await screen.findByLabelText('Planned start (UTC)')
  await userEvent.click(screen.getByRole('button', { name: 'Save planning window' }))
  await screen.findByText('Planning outcome uncertain. Retry the retained command.')
  expect(screen.getByLabelText('Planned start (UTC)')).toBeDisabled()
  await userEvent.click(screen.getByRole('button', { name: 'Retry exact planning command' }))
  await waitFor(() => expect(planningApi.save).toHaveBeenCalledTimes(2))
  expect(vi.mocked(planningApi.save).mock.calls[0][2]).toEqual(vi.mocked(planningApi.save).mock.calls[1][2])
})
it('retains conflicts until explicit discard and never automatically reapplies', async () => {
  vi.mocked(planningApi.save).mockRejectedValue(new ApiError(409, 'version_conflict'))
  render(panel()); await screen.findByLabelText('Planned start (UTC)'); await userEvent.click(screen.getByRole('button', { name: 'Save planning window' }))
  await screen.findByText('Planning conflict. Input retained; discard and reload to continue.')
  expect(screen.queryByRole('button', { name: 'Retry exact planning command' })).not.toBeInTheDocument()
  await userEvent.click(screen.getByRole('button', { name: 'Discard and reload planning' }))
  await waitFor(() => expect(screen.getByLabelText('Planned start (UTC)')).toBeEnabled()); expect(planningApi.save).toHaveBeenCalledOnce()
})
it('withdraws plan history and references after current access denial', async () => {
  vi.mocked(planningApi.save).mockRejectedValue(new ApiError(403, 'permission_denied'))
  render(panel()); await screen.findByLabelText('Planned start (UTC)'); await userEvent.click(screen.getByRole('button', { name: 'Save planning window' }))
  await screen.findByText('Planning access unavailable. Current rights and policy are required.')
  expect(screen.queryByText('FICTIONAL-TUG')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Review exact revision 1' })).not.toBeInTheDocument()
})
it('reviews a scoped exact historic revision with separate unverified notice and TR/EN content', async () => {
  const r = render(panel()); await userEvent.click(await screen.findByRole('button', { name: 'Review exact revision 1' }))
  const history = await screen.findByRole('article', { name: 'Historical unverified plan. No current availability or approval is implied.' })
  expect(within(history).getByText(/synthetic-v1/)).toBeVisible(); expect(planningApi.revision).toHaveBeenCalledWith(id, tug, 1, expect.any(AbortSignal))
  r.rerender(panel('tr')); expect(screen.getByRole('heading', { name: 'Taslak planlama' })).toBeVisible(); expect(screen.getByText(/Güncel komut politikası/)).toBeVisible()
})
it('ignores a delayed old-context result after switching vessel', async () => {
  let finish!: (value: Awaited<ReturnType<typeof planningApi.detail>>) => void
  vi.mocked(planningApi.detail).mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
  const r = render(panel()); r.rerender(panel('en', id)); await screen.findByLabelText('Planned start (UTC)')
  finish({ state: 'DraftUnverified', plan: { ...plan, policyVersion: 'OLD-CONTEXT' }, evidence: null })
  await waitFor(() => expect(screen.queryByText(/OLD-CONTEXT/)).not.toBeInTheDocument())
})
it('shows unavailable policy without mutation controls or verified capacity', async () => {
  vi.mocked(planningApi.options).mockResolvedValue({ ...options, state: 'Unavailable', policyVersion: null, canWrite: false, tugs: [] })
  render(panel()); await screen.findByText('Planning access unavailable. Current rights and policy are required.')
  expect(screen.queryByRole('button', { name: 'Save planning window' })).not.toBeInTheDocument()
  expect(screen.getByText('Available verified BP: Unknown · Unknown')).toBeVisible()
})
