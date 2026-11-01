import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { VesselDocuments } from './VesselDocuments'
import { VesselAttachments } from './VesselAttachments'
import { decodeDocument, type DocumentDraft } from './document-api'
import * as documents from './document-api'
import * as files from './file-api'

const vessel = '10000000-0000-0000-0000-000000000001'
const id = '10000000-0000-0000-0000-000000000002'
const row: DocumentDraft = { id, vesselId: vessel, title: 'FICTIONAL document', revision: 2, version: 7, status: 'DraftUnverified', provenance: 'DeclaredUnverified' }
const props = () => ({ vessel, language: 'en' as const, csrf: 'synthetic-csrf', onDirty: vi.fn(), onSaving: vi.fn() })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('uses persisted owner/revision for attachments and shows unverified TR/EN states', async () => {
  vi.spyOn(documents, 'documents').mockResolvedValue({ items: [row], canWrite: false, offset: 0, hasMore: false })
  const page = vi.spyOn(files, 'filePage').mockResolvedValue({ items: [], canWrite: false, available: false, maxBytes: 0, allowedTypes: ['image/png'], offset: 0, hasMore: false })
  const user = userEvent.setup(); const view = render(<VesselDocuments {...props()} />)
  await user.click(await screen.findByRole('button', { name: row.title! }))
  await waitFor(() => expect(page).toHaveBeenCalledWith(vessel, 0, expect.any(AbortSignal), { id, revision: 2 }))
  expect(screen.getByRole('article', { name: 'Document revision' })).toHaveTextContent('Unverified')
  expect(screen.queryByRole('button', { name: 'Append draft revision' })).not.toBeInTheDocument()
  view.rerender(<VesselDocuments {...props()} language="tr" />)
  expect(screen.getByRole('article', { name: 'Belge revizyonu' })).toHaveTextContent('Doğrulanmamış')
})

it('keeps the original draft key and payload on ambiguous retry and permits incomplete drafts', async () => {
  vi.spyOn(documents, 'documents').mockResolvedValue({ items: [], canWrite: true, offset: 0, hasMore: false })
  const save = vi.spyOn(documents, 'saveDocument').mockRejectedValueOnce(new Error('synthetic lost acknowledgement')).mockResolvedValue({ ...row, revision: 1, title: null })
  vi.spyOn(files, 'filePage').mockResolvedValue({ items: [], canWrite: false, available: false, maxBytes: 0, allowedTypes: ['application/pdf'], offset: 0, hasMore: false })
  const user = userEvent.setup(); const callbacks = props(); render(<VesselDocuments {...callbacks} />)
  await user.click(await screen.findByRole('button', { name: 'New document draft' }))
  await user.click(screen.getByRole('button', { name: 'Save unverified draft' }))
  expect(await screen.findByRole('alert')).toBeVisible()
  expect(screen.getByLabelText('Draft title (optional)')).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'Retry same draft' }))
  await waitFor(() => expect(save).toHaveBeenCalledTimes(2))
  expect(save.mock.calls[0].slice(0, 4)).toEqual(save.mock.calls[1].slice(0, 4))
  expect(save.mock.calls[0][1]).toBe('')
  expect(callbacks.onDirty).toHaveBeenLastCalledWith(false)
})

it('withholds old document rows after a failed refresh and rejects forged authoritative states', async () => {
  vi.spyOn(documents, 'documents').mockResolvedValueOnce({ items: [row], canWrite: false, offset: 0, hasMore: false }).mockRejectedValue(new Error('synthetic revocation'))
  const user = userEvent.setup(); render(<VesselDocuments {...props()} />)
  expect(await screen.findByRole('button', { name: row.title! })).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Refresh documents' }))
  expect(await screen.findByRole('alert')).toBeVisible()
  expect(screen.queryByRole('button', { name: row.title! })).not.toBeInTheDocument()
  for (const value of [{ ...row, status: 'Effective' }, { ...row, vesselId: id }, { ...row, revision: '2' }]) expect(() => decodeDocument(value, vessel)).toThrow()
})

it('prevents a context switch while a document draft is dirty', async () => {
  vi.spyOn(files, 'filePage').mockResolvedValue({ items: [], canWrite: false, available: false, maxBytes: 0, allowedTypes: ['image/png'], offset: 0, hasMore: false })
  vi.spyOn(documents, 'documents').mockResolvedValue({ items: [], canWrite: true, offset: 0, hasMore: false })
  const user = userEvent.setup(); render(<VesselAttachments {...props()} />)
  await user.click(screen.getByRole('button', { name: 'Document drafts', exact: true }))
  await user.click(await screen.findByRole('button', { name: 'New document draft' }))
  expect(screen.getByRole('button', { name: 'Vessel attachments', exact: true })).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'Discard local draft' }))
  expect(screen.getByRole('button', { name: 'Vessel attachments', exact: true })).toBeEnabled()
})
