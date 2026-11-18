import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { CertificateReplacementPicker } from './CertificateReplacementPicker'
import { certificatesApi, type CertificatePage } from './certificates-api'
import { certificateActor, certificateAssignment, certificateFixture, certificateId, certificatePageFixture, certificateVessel } from './certificate-test-fixtures'

afterEach(() => { vi.restoreAllMocks() })

it('waits for the first authorized page and selects its candidate when there are no further pages', async () => {
  let complete!: (page: CertificatePage) => void
  const pending = new Promise<CertificatePage>(resolve => { complete = resolve })
  const read = vi.spyOn(certificatesApi, 'center').mockReturnValue(pending)
  const change = vi.fn(), user = userEvent.setup()
  render(<CertificateReplacementPicker vessel={certificateVessel} owner={`${certificateVessel}/${certificateActor}`} originalId={certificateId} value={null} language="en" disabled={false} onChange={change} />)

  const replacement = screen.getByLabelText('Declared replacement certificate')
  const next = screen.getByRole('button', { name: 'More candidates' })
  expect(screen.getByText('Loading authorized candidates…')).toBeVisible()
  expect(replacement).toBeDisabled()
  expect(next).toBeDisabled()
  expect(change).not.toHaveBeenCalled()

  const candidate = { ...certificateFixture(), id: certificateAssignment, href: `/app/certificate-command-center/${certificateVessel}?id=${certificateAssignment}` }
  await act(async () => { complete(certificatePageFixture([candidate])) })
  expect(replacement).toBeEnabled()
  expect(next).toBeDisabled()
  expect(screen.queryByText('Loading authorized candidates…')).not.toBeInTheDocument()
  await user.selectOptions(replacement, candidate.id)
  expect(change).toHaveBeenCalledExactlyOnceWith({ id: candidate.id, revision: candidate.revision, version: candidate.version })
  expect(read).toHaveBeenCalledTimes(1)
})
