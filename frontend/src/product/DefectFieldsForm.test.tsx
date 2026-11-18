import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { DefectFieldsForm } from './DefectFieldsForm'
import { defectSchema, emptyDefectFields } from './defect-fields'
import { changeAppLanguage, type Locale } from '../i18n'
function Harness() { const [fields, setFields] = useState(emptyDefectFields); return <><DefectFieldsForm fields={fields} schema={defectSchema()} language="en" disabled={false} onChange={setFields} /><output aria-label="Declared defect fields">{JSON.stringify(fields)}</output></> }
describe('T-S8-004 enterprise declaration fields — source only', () => {
  it('keeps the same controlled field and canonical workflow value when switching its options to Arabic', async () => {
    function LocalizedHarness({ language }: { language: Locale }) {
      const [fields, setFields] = useState(emptyDefectFields)
      return <><DefectFieldsForm fields={fields} schema={defectSchema()} language={language} disabled={false} onChange={setFields} />
        <output aria-label="Retained canonical fields">{JSON.stringify(fields)}</output></>
    }
    const user = userEvent.setup(), { rerender } = render(<LocalizedHarness language="en" />)
    await user.selectOptions(screen.getByLabelText('Declared workflow phase value state'), 'Known')
    const phase = screen.getByLabelText('Declared workflow phase')
    await user.selectOptions(phase, 'Verify')
    const original = screen.getByLabelText('Retained canonical fields').textContent
    await changeAppLanguage('ar', false)
    rerender(<LocalizedHarness language="ar" />)
    expect(phase).toBeInTheDocument()
    expect(phase).toHaveValue('Verify')
    expect(screen.getByRole('option', { name: 'مرحلة التحقق' })).toHaveValue('Verify')
    expect(screen.getByLabelText('Retained canonical fields').textContent).toBe(original)
    await user.selectOptions(phase, 'Effectiveness')
    expect(JSON.parse(screen.getByLabelText('Retained canonical fields').textContent!).lifecycle_stage).toEqual({ state: 'Known', value: 'Effectiveness' })
  })

  it('preserves unknown expiry and multiline safeguard declarations without implying a cleared measure', async () => {
    render(<Harness />); const user = userEvent.setup()
    await user.selectOptions(screen.getByLabelText('Declared temporary measure expiry value state'), 'Unknown')
    await user.selectOptions(screen.getByLabelText('Safeguard declaration value state'), 'Known'); await user.type(screen.getByLabelText('Safeguard declaration'), 'Declared safeguard{Enter}Not verified')
    const fields = JSON.parse(screen.getByLabelText('Declared defect fields').textContent!)
    expect(fields.temporary_measure_expires_at).toEqual({ state: 'Unknown', value: null }); expect(fields.temporary_measure).toEqual({ state: 'Missing', value: null }); expect(fields.safeguard.value).toBe('Declared safeguard\nNot verified')
  })
  it('records a workflow phase without offering an approval, closure or effective operating state', async () => {
    render(<Harness />); const user = userEvent.setup()
    await user.selectOptions(screen.getByLabelText('Declared workflow phase value state'), 'Known'); await user.selectOptions(screen.getByLabelText('Declared workflow phase'), 'Verify')
    expect(JSON.parse(screen.getByLabelText('Declared defect fields').textContent!).lifecycle_stage).toEqual({ state: 'Known', value: 'Verify' })
    for (const name of ['Closed', 'Verified', 'Effective', 'Ready']) expect(screen.queryByRole('option', { name })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Declared criticality value state')).toHaveValue('Missing')
  })
  it('associates explicit value errors and keeps readonly fields keyboard-ineligible', () => {
    render(<DefectFieldsForm fields={emptyDefectFields()} schema={defectSchema()} language="en" disabled errors={{ temporary_measure_expires_at: ['invalid_value'] }} onChange={() => {}} />)
    const expiry = screen.getByLabelText('Declared temporary measure expiry')
    expect(expiry).toBeDisabled(); expect(expiry).toHaveAttribute('aria-invalid', 'true'); expect(expiry).toHaveAccessibleDescription(/Check this declared value/)
    expect(screen.getByLabelText('Declared workflow phase value state')).toBeDisabled()
  })
})
