import { render, screen, within } from '@testing-library/react'
import { expect, it } from 'vitest'
import { captureDisplayMetadata, OfflineCaptureMetadata, OfflineQueueSummary, type QueueCapture } from './OfflineQueueSummary'

const commandId = '10000000-0000-4000-8000-000000000001', parent = '10000000-0000-4000-8000-000000000002'
const capturedAt = '2026-11-04T12:00:00Z'
const capture = (): QueueCapture => ({ commandId, revision: 2, body: JSON.stringify({ commandId, baseVersion: 7, capturedAt, watchGroup: 'PRIVATE_DOMAIN_VALUE', keyId: 'UNDISCLOSED_KEY', parentCommandId: parent }) })

it('DEV-033 exposes durable command lineage without displaying domain fields, encryption keys or treating pending as committed', () => {
  render(<OfflineCaptureMetadata capture={capture()} language="en" />)
  expect(screen.getByText(commandId)).toBeVisible(); expect(screen.getByText(parent)).toBeVisible()
  expect(screen.getByText(capturedAt)).toHaveAttribute('datetime', capturedAt)
  expect(screen.getByText('Base version').nextElementSibling).toHaveTextContent('7')
  expect(screen.getByText('Pending server confirmation')).toBeVisible()
  expect(screen.queryByText(/PRIVATE_DOMAIN_VALUE|UNDISCLOSED_KEY|healthy|approved/i)).not.toBeInTheDocument()
})
it.each([
  '{broken', JSON.stringify({ commandId: parent, baseVersion: 7, capturedAt }),
  JSON.stringify({ commandId, baseVersion: 0, capturedAt }), JSON.stringify({ commandId, baseVersion: 7, capturedAt: 'yesterday' }),
])('keeps unavailable metadata explicit instead of inventing a base version or timestamp %#', body => {
  const row = { ...capture(), body }
  expect(captureDisplayMetadata(row)).toBeNull()
  render(<OfflineCaptureMetadata capture={row} language="en" />)
  expect(screen.getByText(/Capture metadata unavailable/)).toBeVisible()
  expect(screen.getByText('Pending server confirmation')).toBeVisible()
  expect(screen.queryByText(capturedAt)).not.toBeInTheDocument()
})
it('counts only the unlocked current local context and preserves the distinct pending/conflict/receipt states', () => {
  const rows: QueueCapture[] = [capture(), { ...capture(), commandId: parent, outcome: { result: 'MaterialConflict', revision: 3, serverVersion: 9, parentCommandId: null } },
    { ...capture(), commandId: '10000000-0000-4000-8000-000000000003', outcome: { result: 'Committed', revision: 4, serverVersion: 10, parentCommandId: null } }]
  const view = render(<OfflineQueueSummary captures={rows} language="en" />)
  const scope = screen.getByRole('region', { name: 'Unlocked local queue overview' })
  expect(scope).toHaveTextContent('This authorized local context only')
  for (const label of ['Unsent / outcome unknown', 'Material conflicts', 'Server receipts']) expect(within(scope).getByText(label).nextElementSibling).toHaveTextContent('1')
  view.rerender(<OfflineQueueSummary captures={rows} language="tr" />)
  expect(screen.getByRole('region', { name: 'Açık yerel kuyruk özeti' })).toHaveTextContent('Sunucu alındıları')
})
