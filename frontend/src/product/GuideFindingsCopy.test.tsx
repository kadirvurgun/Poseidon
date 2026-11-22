import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { WorkOrderSourceSummary } from './WorkOrderRecordView'
import { ShipOpsRecordSummary } from './ShipOperationsRecordView'
import { workOrderRecordFixture } from './work-order-test-fixtures'
import { shipOpsRecordFixture } from './ship-operation-test-fixtures'
import { shipOpsFields } from './ship-operation-fields'
import { shipOpsFactLabel } from './ship-operation-copy'
import { localizedLabel } from '../i18n/messages'

// Guide findings 22.11.2026: summary copy must describe what the owning panels actually do.
describe('Work order summary copy (DEV-054 execution lifecycle)', () => {
  it('states that release, evidence, independent verification and closure run in the execution panel, with no separate evidence acceptance', () => {
    render(<WorkOrderSourceSummary value={workOrderRecordFixture()} language="en"/>)
    expect(screen.queryByText(/remain unavailable/)).not.toBeInTheDocument()
    expect(screen.getByText(/recorded in the execution panel/)).toBeInTheDocument()
    expect(screen.getByText(/never by the person who did the work; there is no separate evidence-acceptance step/)).toBeInTheDocument()
  })
  it('keeps the Turkish copy aligned', () => {
    render(<WorkOrderSourceSummary value={workOrderRecordFixture()} language="tr"/>)
    expect(screen.getByText(/yürütme panelinde kaydedilir/)).toBeInTheDocument()
  })
})

describe('Port activity SOF review state (DEV-101)', () => {
  it('shows the recorded DRAFT state without claiming finalization is unavailable', () => {
    render(<ShipOpsRecordSummary value={shipOpsRecordFixture('Port')} language="en"/>)
    expect(screen.getByText('Draft (not finalized)')).toBeInTheDocument()
    expect(screen.queryByText(/finalization unavailable/)).not.toBeInTheDocument()
  })
  it('shows FINAL only when the server revision records it, never as a signature', () => {
    const record = shipOpsRecordFixture('Port'); record.fields.sof_state = { state: 'Known', value: 'FINAL' }
    render(<ShipOpsRecordSummary value={record} language="en"/>)
    expect(screen.getByText('Final (internal; not a signature)')).toBeInTheDocument()
  })
  it('never turns an unknown SOF state into Draft or Final', () => {
    const record = shipOpsRecordFixture('Port'); record.fields.sof_state = { state: 'Unknown', value: null }
    render(<ShipOpsRecordSummary value={record} language="tr"/>)
    expect(screen.queryByText(/Taslak \(sonlandırılmadı\)|Nihai/)).not.toBeInTheDocument()
    // QA N4: the SOF state cell itself must say Unknown, not merely omit Draft/Final.
    const sofField = shipOpsFields('Port').find(field => field.key === 'sof_state')!
    const term = screen.getByText(localizedLabel(sofField, 'tr'), { selector: 'dt' })
    expect(term.nextElementSibling).toHaveTextContent(shipOpsFactLabel('Unknown', false))
    expect(shipOpsFactLabel('Unknown', false)).toBe('Bilinmiyor')
  })
})
