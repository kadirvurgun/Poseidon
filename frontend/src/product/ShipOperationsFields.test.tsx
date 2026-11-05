import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ShipOperationsFields } from './ShipOperationsFields'
import { emptyShipOpsFields, type ShipOpsKind } from './ship-operation-fields'
import { shipOpsSchemaFixture } from './ship-operation-test-fixtures'
function Harness({ kind = 'Fuel' }: { kind?: ShipOpsKind }) {
  const [fields, setFields] = useState(() => emptyShipOpsFields(kind))
  return <><ShipOperationsFields kind={kind} fields={fields} schema={shipOpsSchemaFixture(kind)} language="en" disabled={false} onChange={setFields} /><output aria-label="Declared operational fields">{JSON.stringify(fields)}</output></>
}
describe('DEV100/101 source declaration form — authored, not executed', () => {
  it('preserves zero and long lexical decimal text separately from missing or unknown', async () => {
    render(<Harness />); const user = userEvent.setup()
    await user.selectOptions(screen.getByLabelText('Quantity received (mt) value state'), 'Known')
    await user.type(screen.getByLabelText('Quantity received (mt)'), '9007199254740993.123456789')
    await user.selectOptions(screen.getByLabelText('ROB before (mt) value state'), 'Known')
    await user.type(screen.getByLabelText('ROB before (mt)'), '0')
    let fields = JSON.parse(screen.getByLabelText('Declared operational fields').textContent!)
    expect(fields.quantity_received_mt).toEqual({ state: 'Known', value: '9007199254740993.123456789' })
    expect(fields.rob_before_mt).toEqual({ state: 'Known', value: '0' })
    await user.selectOptions(screen.getByLabelText('Quantity received (mt) value state'), 'Unknown')
    fields = JSON.parse(screen.getByLabelText('Declared operational fields').textContent!)
    expect(fields.quantity_received_mt).toEqual({ state: 'Unknown', value: null })
    expect(fields.quantity_consumed_mt).toEqual({ state: 'Missing', value: null })
  })
  it('allows explicit missing/unknown references without manufacturing catalog entries or editable approval states', async () => {
    render(<Harness />); const user = userEvent.setup()
    expect(screen.getByLabelText('Supplier reference')).toBeDisabled()
    expect(screen.getByLabelText('Supplier reference value state').querySelector('option[value="Known"]')).toBeDisabled()
    await user.selectOptions(screen.getByLabelText('Supplier reference value state'), 'Unknown')
    expect(JSON.parse(screen.getByLabelText('Declared operational fields').textContent!).supplier).toEqual({ state: 'Unknown', value: null })
    expect(screen.queryByLabelText('Compliance review state')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Verification review state')).not.toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'READY', exact: true })).not.toBeInTheDocument()
  })
  it('retains explicit offset timestamp text and multiline port remarks without guessing cargo/draught units', async () => {
    render(<Harness kind="Port" />); const user = userEvent.setup()
    await user.selectOptions(screen.getByLabelText('Event start value state'), 'Known')
    await user.type(screen.getByLabelText('Event start'), '2026-11-05T15:30:00+03:00')
    await user.selectOptions(screen.getByLabelText('Activity remarks value state'), 'Known')
    await user.type(screen.getByLabelText('Activity remarks'), 'Declared event{Enter}Source review pending')
    const fields = JSON.parse(screen.getByLabelText('Declared operational fields').textContent!)
    expect(fields.event_start.value).toBe('2026-11-05T15:30:00+03:00')
    expect(fields.remarks.value).toBe('Declared event\nSource review pending')
    expect(screen.getByLabelText('Cargo quantity')).toBeDisabled()
    expect(screen.queryByLabelText('Cargo quantity (mt)')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Draught forward (m)')).not.toBeInTheDocument()
  })
})
