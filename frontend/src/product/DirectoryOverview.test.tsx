import { render, screen, within } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { DirectoryOverview } from './DirectoryOverview'
import type { Company, Vessel } from './directory-contract'

vi.mock('./ScopedDistribution', () => ({ ScopedDistribution: ({ data, label }: { data: { name: string; value: number }[]; label: string }) =>
  <figure aria-label={label}>{data.map(row => `${row.name}: ${row.value}`).join(' · ')}</figure> }))

const tug: Vessel = { id: 'fictional-tug', companyId: 'fictional-company', code: 'DEMO-T1', name: 'FICTIONAL Tug', kind: 'Tug', version: 1, canEdit: false }
const ship: Vessel = { ...tug, id: 'fictional-ship', code: 'DEMO-S1', name: 'FICTIONAL Ship', kind: 'Ship', canEdit: true }
const company: Company = { id: 'fictional-company', code: 'DEMO-C1', name: 'FICTIONAL Company', version: 1, canEdit: false }

it('describes only the returned page, preserves edit permissions and replaces the old composition when filtering', () => {
  const mounted = render(<DirectoryOverview records={[ship, tug]} fleet language="en" offset={25} hasMore />)
  const chart = screen.getByRole('figure', { name: 'Vessel types on this page' })
  expect(chart).toHaveTextContent('Ships: 1 · Tugs: 1')
  const summary = screen.getByRole('region', { name: 'Current page overview' })
  expect(summary).toHaveTextContent('Current access on this page')
  expect(summary).toHaveTextContent('This page only. Operational readiness is evaluated separately.')
  expect(within(summary).getByText('Editable records').parentElement).toHaveTextContent('1')
  mounted.rerender(<DirectoryOverview records={[tug]} fleet language="en" offset={0} hasMore={false} />)
  expect(screen.getByRole('figure')).toHaveTextContent('Ships: 0 · Tugs: 1')
  expect(within(summary).getByText('Editable records').parentElement).toHaveTextContent('0')
  expect(summary).toHaveTextContent('End of list')
})

it('shows the actual company page and read-only count without inventing vessel metrics or a chart', () => {
  render(<DirectoryOverview records={[company]} fleet={false} language="tr" offset={25} hasMore={false} />)
  const summary = screen.getByRole('region', { name: 'Geçerli sayfa özeti' })
  expect(within(summary).getByText('Sayfa').parentElement).toHaveTextContent('2')
  expect(within(summary).getByText('Salt okunur').parentElement).toHaveTextContent('1')
  expect(within(summary).queryByText('Gemiler')).not.toBeInTheDocument()
  expect(within(summary).queryByRole('figure')).not.toBeInTheDocument()
})
