import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it } from 'vitest'
import { RegulatoryTraceLineage } from './RegulatoryTraceLineage'
import type { Segment } from './human-review-api'

const id = '10000000-0000-4000-8000-000000000001'
const savedAt = '2026-11-04T10:30:00+03:00'
const source: Segment = { kind: 'Source', id, revision: 4, availability: 'Available', state: 'RETAINED', why: 'HISTORICAL_INPUTS_ONLY', hash: 'A'.repeat(64), savedAt }

it('T-S3-007 shows exact retained lineage and time, with integrity separate from authority', async () => {
  const user = userEvent.setup()
  render(<RegulatoryTraceLineage segments={[source]} language="en" kinds={{ Source: 'Source' }} states={{ RETAINED: 'Retained history' }} reasons={{ HISTORICAL_INPUTS_ONLY: 'Retained historical inputs only' }} />)
  const trace = screen.getByRole('list', { name: 'Source-grounded trace' })
  const node = within(trace).getByRole('article', { name: 'Source' })
  expect(node).toHaveTextContent(`${id} / 4`)
  expect(within(node).getByText(savedAt)).toHaveAttribute('datetime', savedAt)
  expect(node).toHaveTextContent('Retained history'); expect(node).toHaveTextContent('Retained historical inputs only')
  const hash = within(node).getByText('A'.repeat(64)); expect(hash).not.toBeVisible()
  await user.click(within(node).getByText('Integrity hash (not authority)', { selector: 'summary' }))
  expect(hash).toBeVisible()
  expect(screen.queryByText(/ready|approved|verified/i)).not.toBeInTheDocument()
})

it('keeps unavailable source references, timestamps and hashes undisclosed without inventing a ready state', () => {
  render(<RegulatoryTraceLineage segments={[{ ...source, availability: 'Unavailable', state: 'REVIEW_REQUIRED' }]} language="en" kinds={{ Source: 'Source' }} states={{ REVIEW_REQUIRED: 'Review required' }} reasons={{ HISTORICAL_INPUTS_ONLY: 'Retained historical inputs only' }} />)
  const node = screen.getByRole('article', { name: 'Source' })
  expect(node).toHaveTextContent('Unavailable'); expect(node).toHaveTextContent('Unknown')
  expect(node).not.toHaveTextContent(id); expect(node).not.toHaveTextContent(savedAt); expect(node).not.toHaveTextContent('A'.repeat(64))
  expect(node.querySelector('time')).toBeNull()
})

it('preserves the same ordered source references across translation', () => {
  const other: Segment = { ...source, kind: 'Control', revision: 7 }
  const props = { segments: [source, other], states: { RETAINED: 'Saklanan geçmiş' }, reasons: { HISTORICAL_INPUTS_ONLY: 'Yalnız saklanan tarihsel girdiler' } }
  const view = render(<RegulatoryTraceLineage {...props} language="en" kinds={{ Source: 'Source', Control: 'Control' }} />)
  expect(screen.getAllByRole('article').map(node => node.getAttribute('aria-label'))).toEqual(['Source', 'Control'])
  view.rerender(<RegulatoryTraceLineage {...props} language="tr" kinds={{ Source: 'Kaynak', Control: 'Kontrol' }} />)
  expect(screen.getByRole('list', { name: 'Kaynağa dayalı iz' })).toBeVisible()
  expect(screen.getAllByRole('article').map(node => node.getAttribute('aria-label'))).toEqual(['Kaynak', 'Kontrol'])
  expect(screen.getByRole('article', { name: 'Kaynak' })).toHaveTextContent(`${id} / 4`)
  expect(screen.getByRole('article', { name: 'Kontrol' })).toHaveTextContent(`${id} / 7`)
})
