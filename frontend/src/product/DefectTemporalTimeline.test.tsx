import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DefectTemporalTimeline } from './DefectRecordView'
import { defectRecordFixture } from './defect-test-fixtures'
afterEach(() => vi.useRealTimers())
describe('T-S8-004 saved temporal observations — source only', () => {
  it('uses explicit server assessment rather than silently reclassifying with the browser clock', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2090-01-01T00:00:00Z'))
    const record = defectRecordFixture(); record.fields.temporary_measure = { state: 'Known', value: 'Unassessed temporary declaration' }; record.fields.temporary_measure_expires_at = { state: 'Known', value: '2026-11-06T02:00:00Z' }; record.temporal.temporaryMeasureState = 'NOT_DUE_DECLARED'
    render(<DefectTemporalTimeline value={record} language="en" dirty />)
    expect(screen.getByText('Declared expiry not yet reached')).toBeVisible(); expect(screen.getByText('2026-11-06T01:00:00Z')).toBeVisible()
    expect(screen.getByText(/local edits are not included/)).toBeVisible(); expect(screen.queryByText('Declared expiry reached')).not.toBeInTheDocument()
  })
  it('keeps unknown restriction and expiry information distinct from clear or no restriction', () => {
    const record = defectRecordFixture(); record.fields.temporary_measure = { state: 'Known', value: 'Unassessed declaration' }; record.fields.temporary_measure_expires_at = { state: 'Unknown', value: null }; record.fields.restriction = { state: 'Unknown', value: null }; record.temporal.temporaryMeasureState = 'DEADLINE_UNKNOWN'
    render(<DefectTemporalTimeline value={record} language="en" />)
    expect(screen.getByText('Declared expiry remains unknown')).toBeVisible(); expect(screen.getAllByText('Unknown')).toHaveLength(2)
    expect(screen.queryByText(/No restriction|Cleared|Ready/)).not.toBeInTheDocument()
  })
})
