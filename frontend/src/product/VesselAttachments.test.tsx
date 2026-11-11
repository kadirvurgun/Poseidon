import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { VesselAttachments } from './VesselAttachments'

vi.mock('./VesselFiles', () => ({ VesselFiles: ({ onDirty, onSaving }: { onDirty: (value: boolean) => void; onSaving: (value: boolean) => void }) => {
  const [draft, setDraft] = useState('')
  return <>
    <label>Synthetic file draft<input value={draft} onChange={event => { setDraft(event.target.value); onDirty(event.target.value.length > 0) }} /></label>
    <label>Synthetic transfer busy<input type="checkbox" onChange={event => onSaving(event.target.checked)} /></label>
  </>
} }))
vi.mock('./VesselDocuments', () => ({ VesselDocuments: () => <p>Synthetic document editor</p> }))

it('retains the owning editor and locks mode buttons while a draft or transfer survives a locale switch', async () => {
  const user = userEvent.setup(), onDirty = vi.fn(), onSaving = vi.fn()
  const props = { vessel: '11111111-1111-4111-8111-111111111111', csrf: 'synthetic-csrf', onDirty, onSaving }
  const { rerender } = render(<VesselAttachments {...props} language="en" />)
  const input = screen.getByLabelText('Synthetic file draft')
  await user.type(input, 'SYNTHETIC unsaved file')
  await changeAppLanguage('it', false)
  rerender(<VesselAttachments {...props} language="it" />)
  expect(input).toBeInTheDocument()
  expect(input).toHaveValue('SYNTHETIC unsaved file')
  const files = screen.getByRole('button', { name: 'Allegati della nave' })
  const documents = screen.getByRole('button', { name: 'Bozze dei documenti' })
  expect(files).toHaveAttribute('data-slot', 'button')
  expect(files).toHaveAttribute('aria-pressed', 'true')
  expect(documents).toHaveAttribute('aria-pressed', 'false')
  expect(files).toBeDisabled()
  expect(documents).toBeDisabled()
  expect(screen.queryByText('Synthetic document editor')).not.toBeInTheDocument()
  await user.clear(input)
  await user.click(screen.getByLabelText('Synthetic transfer busy'))
  expect(documents).toBeDisabled()
  await user.click(screen.getByLabelText('Synthetic transfer busy'))
  expect(documents).toBeEnabled()
  await user.click(documents)
  expect(screen.getByText('Synthetic document editor')).toBeVisible()
  expect(onDirty).toHaveBeenLastCalledWith(false)
  expect(onSaving).toHaveBeenLastCalledWith(false)
})
