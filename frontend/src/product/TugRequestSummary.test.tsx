import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { TugRequestSummary } from './TugRequestSummary'
import { emptyTugInputs, type TugRequest } from './tug-api'

const record: TugRequest = { id: '22222222-2222-2222-2222-222222222222', vesselId: '11111111-1111-1111-1111-111111111111',
  jobNumber: 'FICTIONAL-JOB', revision: 4, version: 1, sourceVersion: 'synthetic-source/v1', savedAt: '2026-11-02T12:00:00Z', replayed: false,
  provenance: 'SyntheticUnverified', missing: ['requested_start'], inputs: { ...emptyTugInputs(), assistedVessel: 'FICTIONAL Assisted Vessel', requiredBollardPull: '45.01', notes: 'FICTIONAL operator notes', priority: 'High' } }

it('shows grouped source-owned request data with exact decimals and explicit unknown times', () => {
  render(<TugRequestSummary record={record} language="en" editing={false} />)
  const detail = screen.getByRole('region', { name: 'Current request detail' })
  expect(detail).toHaveTextContent('45.01')
  expect(detail).toHaveTextContent('Missing required fields: Requested start')
  expect(within(detail).getByText('Requested end · UTC').nextElementSibling).toHaveTextContent('Unknown')
  expect(detail).toHaveTextContent('FICTIONAL operator notes')
  expect(detail).toHaveTextContent('Requested times remain separate from the draft plan')
  expect(within(detail).queryByRole('button', { name: 'Edit draft' })).not.toBeInTheDocument()
})

it('offers editing only through the owning callback and hides the duplicate read-only body while editing', async () => {
  const onEdit = vi.fn(), user = userEvent.setup()
  const mounted = render(<TugRequestSummary record={record} language="en" editing={false} onEdit={onEdit} />)
  await user.click(screen.getByRole('button', { name: 'Edit draft' }))
  expect(onEdit).toHaveBeenCalledTimes(1)
  mounted.rerender(<TugRequestSummary record={record} language="en" editing onEdit={onEdit} />)
  expect(screen.queryByRole('button', { name: 'Edit draft' })).not.toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Planning demand' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'FICTIONAL-JOB' })).toBeVisible()
})

it('never labels a field-complete draft as approved and retains readable revision lineage', async () => {
  render(<TugRequestSummary record={{ ...record, missing: [] }} language="tr" editing={false} />)
  expect(screen.getByText(/Talep alanlarında eksik bildirilmedi/)).toHaveTextContent('Planlama ve operasyon onayı ayrıca değerlendirilir.')
  await userEvent.click(screen.getByText('Talep sürümü ve kaynak bağlamı'))
  expect(screen.getByText('synthetic-source/v1')).toBeVisible()
  expect(screen.queryByText('Hazır', { exact: true })).not.toBeInTheDocument()
})
