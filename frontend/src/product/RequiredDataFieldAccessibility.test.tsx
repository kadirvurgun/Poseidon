import { render, screen } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { RequiredDataField } from './RequiredDataField'

it.each(['en', 'tr'] as const)('DEV-025 associates a %s missing/unknown-state error with the keyboard-accessible state selector as well as its value', language => {
  render(<RequiredDataField label="IMO" language={language} kind="text" value={{ state: 'Unknown', value: null }} error onChange={vi.fn()} />)
  const state = screen.getByRole('combobox', { name: language === 'en' ? 'IMO — Data state' : 'IMO — Bilgi durumu' })
  const input = screen.getByRole('textbox', { name: 'IMO' })
  expect(state).toHaveAttribute('aria-invalid', 'true'); expect(input).toHaveAttribute('aria-invalid', 'true')
  const message = language === 'en' ? 'Check the value and data state.' : 'Değeri ve bilgi durumunu kontrol edin.'
  expect(state).toHaveAccessibleDescription(message); expect(input).toHaveAccessibleDescription(message)
  expect(state).toBeEnabled(); expect(input).toBeDisabled()
  state.focus(); expect(state).toHaveFocus()
})
